using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using TMPro;
using System;
using System.Collections.Generic;

public class MouseInteraction : MonoBehaviour
{
    public StarSystemManager manager;
    public Slider massSlider;
    public TextMeshProUGUI massText;

    [Header("Configuração de Prefabs")]
    public GameObject starPrefab;
    public GameObject planetPrefab;
    private GameObject currentPrefab;
    
    [Header("Configuração do Preview")]
    public GameObject previewStar;
    public Material ghostMaterial;
    private GameObject ghostInstance;
    private StarComponent ghostComponent;

    [Header("Configuração do Lançamento")]
    public LineRenderer dragLine;
    private Vector3 dragStartPos;
    private bool isDragging = false;
    public float launchForceMultiplier = 0.5f;
    public float dragThreshold = 0.3f; // Distância mínima para validar o lançamento

    [Header("Previsão de Trajetória")]
    public LineRenderer trajectoryLine; // LineRenderer separado para a previsão
    public int trajectorySteps = 150;
    public float trajectoryStepSize = 0.05f; // tamanho de cada passo de simulação
    [Tooltip("Softening factor para planetas — evita que entrem na estrela")]
    public float planetSoftening = 40f;

    [Header("Linha de Distância + HUD")]
    public DashedLine starToMouseLine; // linha tracejada do cursor à estrela mais próxima
    public TextMeshProUGUI distanceText; // texto HUD com distância em AU
    public Minimap minimap; // minimap — toggle com T
    private bool showSpatialTools = true; // controla linha + grid + minimap com T

    [Header("Modo de Mira (Colisão Direta)")]
    public LineRenderer aimLine; // linha tracejada do cursor ao alvo
    public TextMeshProUGUI aimText; // texto HUD "Click to launch toward target"
    public Material aimRingMaterial; // material do anel de highlight no alvo

    // Estado interno do modo de mira
    private StarComponent aimTarget = null; // planeta alvo selecionado
    private GameObject aimRingInstance = null; // anel pulsante à volta do alvo
    private bool isAiming = false; // true quando modo de mira está ativo

    [HideInInspector]
    public GameObject lastCreatedObject; // Variável para a câmara saber onde voltar

    void Start()
    {
        currentPrefab = starPrefab;
        ResetGhost();

        // Configuração inicial da linha visual
        if (dragLine != null)
        {
            dragLine.positionCount = 2;
            dragLine.enabled = false;
        }
    }

    // Função para botões trocarem entre Estrela e Planeta
    public void MudarTipo(bool eEstrela)
    {
        // Escolhe o prefab
        currentPrefab = eEstrela ? starPrefab : planetPrefab;

        if (eEstrela)
        {
            massSlider.minValue = 10;
            massSlider.maxValue = 500;
            massSlider.value = 100;
        }
        else
        {
            // Slider logarítmico: vai de 0.0 a 1.0 internamente
            // A massa real é calculada em SliderToPlanetMass()
            // Cobre Marte (0.33 u.i.) a Júpiter (~955 u.i.) em escala proporcional
            massSlider.minValue = 0f;
            massSlider.maxValue = 1f;
            massSlider.value    = PlanetMassToSlider(3f); // começa na Terra (~1 M_earth)
        }
        // Reset ao fantasma para mudar de visual no ecrã
        ResetGhost();
    }

    // Converte posição do slider (0-1) para massa interna usando escala logarítmica.
    // Mínimo: 0.33 u.i. (~0.11 M_earth, Marte)   Máximo: 51 u.i. (~17 M_earth, Neptuno/Urano)
    // Gigantes gasosos excluídos — Júpiter perturbaria gravemente as estrelas nesta escala
    const float planetLogMin = 0.33f;
    const float planetLogMax = 51f;
    float SliderToPlanetMass(float sliderVal)
    {
        return planetLogMin * Mathf.Pow(planetLogMax / planetLogMin, sliderVal);
    }

    // Inverso: dada uma massa interna, devolve a posição do slider correspondente
    float PlanetMassToSlider(float mass)
    {
        return Mathf.Log(mass / planetLogMin) / Mathf.Log(planetLogMax / planetLogMin);
    }

    void ResetGhost()
    {
        if (ghostInstance != null) Destroy(ghostInstance);
        
        if (currentPrefab != null)
        {
            ghostInstance = Instantiate(currentPrefab);
            // Removi o TrailRenderer e a Física do fantasma para não interferir
            if (ghostInstance.TryGetComponent<TrailRenderer>(out var tr)) Destroy(tr);
            if (ghostInstance.TryGetComponent<Collider>(out var col)) Destroy(col);

            // Efeito ghost para não atrapalhar o utilizador e consequentemente a visualização das estrelas aleatórias criadas
            Renderer rend = ghostInstance.GetComponent<Renderer>();
            if (rend != null && ghostMaterial != null)
            {
                rend.material = ghostMaterial;
            }

            ghostComponent = ghostInstance.GetComponent<StarComponent>();
        }
    }

    void Update()
    {
        // Update ao texto da massa para as unidades "reais"
        if (massText != null && massSlider != null)
        {
            if (currentPrefab == starPrefab)
            {
                float massReal = massSlider.value * massStarToSolar;
                massText.text = $"Mass: {massReal:F3} M_sun";
            }
            else
            {
                // Converte o slider logarítmico para massa real em M_earth
                float massInternal = SliderToPlanetMass(massSlider.value);
                float massReal = massInternal * massPlanetToEarth;
                massText.text = $"Mass: {massReal:F2} M_earth";
            }
        }

        UpdateDistanceHUD();
        HandleAimMode();
        HandleInput();
    }

    // Modo de Mira
    // Clique direito num planeta - marca como alvo (anel pulsante)
    // Clique esquerdo - cria planeta lançado diretamente para o alvo
    // Clique direito novamente ou Escape - cancela
    void HandleAimMode()
    {
        // Clique direito — seleciona ou cancela alvo
        if (Mouse.current.rightButton.wasPressedThisFrame
            && !UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject()
            && currentPrefab == planetPrefab
            && !isDragging)
        {
            Ray ray = Camera.main.ScreenPointToRay(Mouse.current.position.ReadValue());
            if (Physics.Raycast(ray, out RaycastHit hit))
            {
                StarComponent sc = hit.collider.GetComponent<StarComponent>();
                if (sc != null && sc.isPlanet)
                {
                    if (aimTarget == sc)
                        CancelAimMode(); // clique direito no mesmo → cancela
                    else
                        EnterAimMode(sc);
                    return;
                }
            }
            // Clique direito no vazio → cancela
            if (isAiming) CancelAimMode();
        }

        // Escape — cancela modo de mira
        if (isAiming && Keyboard.current.escapeKey.wasPressedThisFrame)
        {
            CancelAimMode();
            return;
        }

        if (!isAiming) return;

        // Atualiza o anel pulsante à volta do alvo
        if (aimTarget == null) { CancelAimMode(); return; }
        UpdateAimRing();

        // Linha de mira do cursor ao alvo
        Vector3 mousePos = GetMouseWorldPos();
        if (aimLine != null)
        {
            aimLine.enabled = true;
            aimLine.positionCount = 2;
            aimLine.SetPosition(0, mousePos);
            aimLine.SetPosition(1, aimTarget.transform.position);
        }

        // Previsão do ponto de impacto — marcador X no alvo
        if (aimText != null)
        {
            float dist = Vector3.Distance(mousePos, aimTarget.transform.position);
            float distReal = dist * distToAU;
            aimText.enabled = true;
            aimText.text = "<b>TARGET LOCKED</b>" + " " + "Right-click or Esc to cancel" + " " + $"Dist: {distReal:F2} AU";
        }

        // Clique esquerdo no modo de mira — lança planeta diretamente para o alvo
        if (Input.GetMouseButtonDown(0)
            && !UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject())
        {
            LaunchTowardTarget(mousePos);
        }
    }

    void EnterAimMode(StarComponent target)
    {
        CancelAimMode(); // limpa estado anterior se houver

        isAiming = true;
        aimTarget = target;

        // Esconde o ghost enquanto está em modo de mira
        if (ghostInstance != null) ghostInstance.SetActive(false);

        // Cria o anel de highlight à volta do alvo
        aimRingInstance = new GameObject("AimRing");
        LineRenderer lr = aimRingInstance.AddComponent<LineRenderer>();
        int segs = 48;
        lr.positionCount = segs + 1;
        lr.useWorldSpace = true;
        lr.loop = false;

        Material mat = aimRingMaterial != null
            ? new Material(aimRingMaterial)
            : new Material(Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Color"));
        mat.color = new Color(1f, 0.3f, 0.2f, 1f); // vermelho — alvo selecionado
        mat.SetColor("_BaseColor", new Color(1f, 0.3f, 0.2f, 1f));
        lr.material = mat;
        lr.startWidth = 0.08f;
        lr.endWidth = 0.08f;

    }

    void UpdateAimRing()
    {
        if (aimRingInstance == null || aimTarget == null) return;

        LineRenderer lr = aimRingInstance.GetComponent<LineRenderer>();
        if (lr == null) return;

        // Anel pulsante — raio oscila suavemente
        float baseRadius = aimTarget.transform.localScale.x * 0.8f;
        float pulse = Mathf.Sin(Time.time * 4f) * 0.1f;
        float radius = baseRadius + pulse;
        int segs = 48;

        aimRingInstance.transform.position = aimTarget.transform.position;

        for (int i = 0; i <= segs; i++)
        {
            float angle = (float)i / segs * Mathf.PI * 2f;
            lr.SetPosition(i, aimTarget.transform.position + new Vector3(
                Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius));
        }

        // Cor pulsa entre vermelho e laranja
        float colorPulse = Mathf.Sin(Time.time * 3f) * 0.5f + 0.5f;
        Color ringColor  = Color.Lerp(new Color(1f, 0.2f, 0.1f), new Color(1f, 0.7f, 0.1f), colorPulse);
        lr.material.color = ringColor;
        lr.material.SetColor("_BaseColor", ringColor);
    }

    void LaunchTowardTarget(Vector3 launchPos)
    {
        if (aimTarget == null) return;

        // Velocidade calculada para colidir diretamente com o alvo
        // v = distância / tempo estimado, apontada ao alvo
        Vector3 toTarget = aimTarget.transform.position - launchPos;
        float dist = toTarget.magnitude;

        // Velocidade de colisão: suficiente para chegar ao alvo num tempo razoável
        // Usa a mesma escala do estilingue normal para consistência
        float speed = Mathf.Clamp(dist * launchForceMultiplier * 1.5f, 2f, 50f);
        Vector3 launchVel = toTarget.normalized * speed;

        float spawnMass = SliderToPlanetMass(massSlider.value);
        lastCreatedObject = manager.CreateStarCustom(planetPrefab, launchPos, launchVel, spawnMass);

        // Mantém o alvo selecionado para poder lançar múltiplos planetas
        // (cancela só com clique direito ou Escape)
    }

    void CancelAimMode()
    {
        isAiming  = false;
        aimTarget = null;

        if (aimRingInstance != null)
        {
            Destroy(aimRingInstance);
            aimRingInstance = null;
        }
        if (aimLine != null) aimLine.enabled = false;
        if (aimText != null) aimText.enabled = false;

        // Mostra o ghost novamente ao sair do modo de mira
        if (ghostInstance != null) ghostInstance.SetActive(true);
    }

    void HandleInput()
    {
        if (ghostInstance == null) return;
        
        // Se estamos em modo de mira, o clique esquerdo é tratado por HandleAimMode
        if (isAiming) return;

        // Deteta o início do clique (Prepara o nascimento da estrela)
        if (Input.GetMouseButtonDown(0) && !UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject())
        {
            isDragging = true;
            dragStartPos = GetMouseWorldPos();
            
            if (dragLine != null) dragLine.enabled = true;
        }

        // Enquanto o botão está a ser pressionado (Calcula a trajetória/força)
        if (isDragging)
        {
            Vector3 currentMousePos = GetMouseWorldPos();
            
            // O Fantasma fica fixo no ponto onde clicamos (onde a estrela vai nascer)
            ghostInstance.transform.position = dragStartPos;
            UpdateGhostVisuals();

            // Desenha a linha do estilingue
            if (dragLine != null)
            {
                dragLine.SetPosition(0, dragStartPos);
                dragLine.SetPosition(1, currentMousePos);
            }

            // Previsão de trajetória (só no modo planeta e sem O pressionado)
            if (currentPrefab == planetPrefab && trajectoryLine != null)
            {
                if (Keyboard.current.oKey.isPressed)
                    trajectoryLine.enabled = false;
                else
                {
                    Vector3 launchVel = (dragStartPos - currentMousePos) * launchForceMultiplier;
                    DrawTrajectoryPreview(dragStartPos, launchVel);
                }
            }

            // Se soltar o botão, cria com velocidade ou modo estático
            if (Input.GetMouseButtonUp(0))
            {
                isDragging = false;
                if (dragLine != null) dragLine.enabled = false;

                Vector3 dragEndPos = GetMouseWorldPos();
                
                // Calculamos a distância do arrasto para evitar cliques simples sem querer
                float dragDistance = Vector3.Distance(dragStartPos, dragEndPos);

                // O + clique esquerdo — órbita automática em relação à estrela mais próxima
                // Usa sempre dragStartPos (ponto do clique) e ignora o arrasto completamente
                if (Keyboard.current.oKey.isPressed && currentPrefab == planetPrefab)
                {
                    Vector3 orbitalVel = CalcOrbitalVelocity(dragStartPos);
                    float spawnMass1 = (currentPrefab == planetPrefab) ? SliderToPlanetMass(massSlider.value) : massSlider.value;
                    lastCreatedObject = manager.CreateStarCustom(currentPrefab, dragStartPos, orbitalVel, spawnMass1);
                    isDragging = false;
                    if (dragLine != null) dragLine.enabled = false;
                }
                // SHIFT — cria parado
                else if (Keyboard.current.leftShiftKey.isPressed || Keyboard.current.rightShiftKey.isPressed)
                {
                    float spawnMass2  = (currentPrefab == planetPrefab) ? SliderToPlanetMass(massSlider.value) : massSlider.value;
                    lastCreatedObject = manager.CreateStarCustom(currentPrefab, dragStartPos, Vector3.zero, spawnMass2);
                }
                // Estilingue normal
                else if (dragDistance > dragThreshold)
                {
                    Vector3 launchVelocity = (dragStartPos - dragEndPos) * launchForceMultiplier;
                    float spawnMass3  = (currentPrefab == planetPrefab) ? SliderToPlanetMass(massSlider.value) : massSlider.value;
                    lastCreatedObject = manager.CreateStarCustom(currentPrefab, dragStartPos, launchVelocity, spawnMass3);
                }
            }
        }
        else
        {
            // Se não está a arrastar, o fantasma segue o rato normalmente para exploração
            MovePreview();
            if (dragLine != null) dragLine.enabled = false;
            if (trajectoryLine != null) trajectoryLine.enabled = false;
        }
    }

    void MovePreview()
    {
        if (ghostInstance == null || Camera.main == null) return;

        ghostInstance.transform.position = GetMouseWorldPos();
        UpdateGhostVisuals();
    }

    void UpdateGhostVisuals()
    {
        if (ghostInstance == null || ghostComponent == null || massSlider == null) return;

        // Para planetas usa a massa interna real (escala logarítmica), não o valor direto do slider
        ghostComponent.mass = (currentPrefab == planetPrefab)
            ? SliderToPlanetMass(massSlider.value)
            : massSlider.value;
        ghostComponent.UpdateAppearance();

        Renderer rend = ghostInstance.GetComponent<Renderer>();
        if (rend != null)
        {
            float t = Mathf.InverseLerp(massSlider.minValue, massSlider.maxValue, massSlider.value);
            Color targetColor;

            if (currentPrefab == starPrefab)
            {
                // Harvard spectral sequence - PAPER
                if (t < 0.3f)
                    targetColor = Color.Lerp(new Color(1.0f, 0.2f, 0.1f), new Color(1.0f, 0.5f, 0.2f), t / 0.3f);
                else if (t < 0.5f)
                    targetColor = Color.Lerp(new Color(1.0f, 0.5f, 0.2f), new Color(1.0f, 0.95f, 0.6f), (t - 0.3f) / 0.2f);
                else if (t < 0.7f)
                    targetColor = Color.Lerp(new Color(1.0f, 0.95f, 0.6f), new Color(1.0f, 1.0f, 1.0f), (t - 0.5f) / 0.2f);
                else
                    targetColor = Color.Lerp(new Color(1.0f, 1.0f, 1.0f), new Color(0.5f, 0.7f, 1.0f), (t - 0.7f) / 0.3f);
            }
            else
            {
                // Ghost do planeta usa a mesma sequência de cores por composição química que o StarComponent — calculada a partir da massa interna real (não do slider 0-1)
                float massInternal = SliderToPlanetMass(massSlider.value);
                if (massInternal < 1.5f)
                    targetColor = Color.Lerp(new Color(0.45f, 0.35f, 0.30f), new Color(0.60f, 0.50f, 0.42f), Mathf.InverseLerp(0.33f, 1.5f, massInternal));
                else if (massInternal < 6.0f)
                    targetColor = Color.Lerp(new Color(0.35f, 0.50f, 0.40f), new Color(0.20f, 0.50f, 0.65f), Mathf.InverseLerp(1.5f, 6.0f, massInternal));
                else
                    targetColor = Color.Lerp(new Color(0.20f, 0.50f, 0.65f), new Color(0.15f, 0.40f, 0.75f), Mathf.InverseLerp(6.0f, 51f, massInternal));
            }

            targetColor.a = 0.4f;
            rend.material.color = targetColor;
            rend.material.SetColor("_BaseColor", targetColor);
        }
    }

    public float spawnDistance = 20f;
    // Conversão de fatores (game units -> realidade)
    const float massStarToSolar   = 0.004f;  // 250 unidades = 1.0 M_sun
    const float massPlanetToEarth = 0.333f;  // 1 unidade = 0.333 M_earth
    const float distToAU          = 0.1f;    // 1 game unit = 0.1 AU

    // Simula a trajetória do planeta em memória e desenha-a com o trajectoryLine
    void DrawTrajectoryPreview(Vector3 startPos, Vector3 startVel)
    {
        List<StarComponent> stars = manager.GetStars();
        if (stars == null || stars.Count == 0)
        {
            trajectoryLine.enabled = false;
            return;
        }

        trajectoryLine.enabled      = true;
        trajectoryLine.positionCount = trajectorySteps;

        Vector3 pos = startPos;
        Vector3 vel = startVel;

        for (int step = 0; step < trajectorySteps; step++)
        {
            trajectoryLine.SetPosition(step, pos);

            // Calcula aceleração gravitacional de todas as estrelas
            Vector3 acc = Vector3.zero;
            foreach (StarComponent star in stars)
            {
                if (star == null) continue;
                Vector3 dir    = star.transform.position - pos;
                float   distSq = Mathf.Max(dir.sqrMagnitude, planetSoftening);
                acc += dir.normalized * (manager.G * star.mass / distSq);
            }

            vel += acc * trajectoryStepSize;
            pos += vel * trajectoryStepSize;
        }
    }

    void UpdateDistanceHUD()
    {
        // Toggle T — linha tracejada + grid + minimap
        if (Keyboard.current.tKey.wasPressedThisFrame)
        {
            showSpatialTools = !showSpatialTools;
            if (minimap != null) minimap.Toggle(showSpatialTools);
        }

        if (currentPrefab != planetPrefab)
        {
            if (starToMouseLine != null) starToMouseLine.Hide();
            if (distanceText != null) distanceText.enabled = false;
            return;
        }

        Vector3 mousePos = GetMouseWorldPos();
        StarComponent nearest  = manager.GetNearestStar(mousePos);

        if (nearest == null)
        {
            if (starToMouseLine != null) starToMouseLine.Hide();
            if (distanceText != null) distanceText.enabled = false;
            return;
        }

        float dist = Vector3.Distance(mousePos, nearest.transform.position);
        float distReal = dist * distToAU;

        // Texto de distância — sempre visível no modo planeta
        if (distanceText != null)
        {
            distanceText.enabled = true;
            distanceText.text = $"Dist. to nearest star: {distReal:F2} AU";
        }

        // Linha tracejada cursor -> estrela mais próxima (toggle com T)
        if (starToMouseLine != null)
        {
            if (showSpatialTools)
                starToMouseLine.SetPoints(mousePos, nearest.transform.position);
            else
                starToMouseLine.Hide();
        }
    }

    Vector3 GetMouseWorldPos()
    {
        if (Camera.main == null) return Vector3.zero;
        Ray ray = Camera.main.ScreenPointToRay(Mouse.current.position.ReadValue());
        Plane plane = new Plane(-Camera.main.transform.forward,
                                Camera.main.transform.position + Camera.main.transform.forward * spawnDistance);
        if (plane.Raycast(ray, out float distance))
            return ray.GetPoint(distance);
        return ray.GetPoint(spawnDistance);
    }

    // Calcula a velocidade orbital ideal em relação à estrela mais próxima de 'position'.
    // Usa a fórmula v = sqrt(G * M / r), perpendicular ao vetor posição-estrela.
    Vector3 CalcOrbitalVelocity(Vector3 position)
    {
        StarComponent nearest = manager.GetNearestStar(position);
        if (nearest == null) return Vector3.zero;

        Vector3 toStar = nearest.transform.position - position;
        float r = toStar.magnitude;
        if (r < 0.01f) return Vector3.zero;

        float speed = Mathf.Sqrt(manager.G * nearest.mass / r);

        // Cross de Vector3.up com o vetor estrela -> planeta dá sempre a tangente orbital correta no plano horizontal, independentemente do lado em que o planeta está colocado
        Vector3 fromStar = -toStar.normalized;
        Vector3 perpendicular = Vector3.Cross(Vector3.up, fromStar).normalized;

        return perpendicular * speed;
    }
}
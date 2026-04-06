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
            massSlider.minValue = 0.1f;
            massSlider.maxValue = 10f;
            massSlider.value = 1.0f;
        }
        // Reset ao fantasma para mudar de visual no ecrã
        ResetGhost();
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
                float massReal = massSlider.value * massPlanetToEarth;
                massText.text = $"Mass: {massReal:F2} M_earth";
            }
        }

        UpdateDistanceHUD();
        HandleInput();
    }

    void HandleInput()
    {
        if (ghostInstance == null) return;

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
                    lastCreatedObject = manager.CreateStarCustom(currentPrefab, dragStartPos, orbitalVel, massSlider.value);
                    isDragging = false;
                    if (dragLine != null) dragLine.enabled = false;
                }
                // SHIFT — cria parado
                else if (Keyboard.current.leftShiftKey.isPressed || Keyboard.current.rightShiftKey.isPressed)
                {
                    lastCreatedObject = manager.CreateStarCustom(currentPrefab, dragStartPos, Vector3.zero, massSlider.value);
                }
                // Estilingue normal
                else if (dragDistance > dragThreshold)
                {
                    Vector3 launchVelocity = (dragStartPos - dragEndPos) * launchForceMultiplier;
                    lastCreatedObject = manager.CreateStarCustom(currentPrefab, dragStartPos, launchVelocity, massSlider.value);
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

        ghostComponent.mass = massSlider.value;
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
                if (t < 0.5f)
                    targetColor = Color.Lerp(new Color(0.7f, 0.4f, 0.3f), new Color(0.9f, 0.7f, 0.5f), t * 2);
                else
                    targetColor = Color.Lerp(new Color(0.9f, 0.7f, 0.5f), new Color(0.2f, 0.5f, 1.0f), (t - 0.5f) * 2);
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
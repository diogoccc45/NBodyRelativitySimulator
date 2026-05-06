using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using TMPro;

// Gere a colocação, arrasto e remoção de massas na grid de espaço-tempo
// Clique esquerdo na grid — coloca uma massa
// Clique esquerdo + arrastar numa massa — move-a
// Clique direito numa massa — remove-a
public class RelativityManager : MonoBehaviour
{
    [Header("Referências")]
    public SpacetimeGrid grid;
    public Camera mainCamera;
    public RelativityTimeline timeline;
    public RelativityTrajectory trajectoryPreview;

    [Header("Amortecimento nas Bordas")]
    [Tooltip("Zona de amortecimento progressivo — distância da borda onde começa a abrandar")]
    public float edgeDampingZone = 20f;
    [Tooltip("Força de amortecimento máxima na borda")]
    public float edgeDampingStrength = 8f;

    [Header("Prefabs")]
    [Tooltip("Prefab da massa pesada (estrela) — deforma a grid")]
    public GameObject heavyBodyPrefab;
    [Tooltip("Prefab da massa leve (planeta) — desliza pela curvatura")]
    public GameObject lightBodyPrefab;

    [Header("Configuração das Massas")]
    public float heavyBodyMass = 200f;
    public float lightBodyMass = 5f;

    [Header("UI")]
    [Tooltip("Slider para ajustar a massa da estrela")]
    public Slider massSlider;
    public TextMeshProUGUI massText;
    [Tooltip("Texto que mostra o modo atual (Estrela / Planeta)")]
    public TextMeshProUGUI modeText;

    [Header("Plano de Colocação")]
    [Tooltip("Altura Y do plano de raycast — deve ser 0 (nível da grid não deformada)")]
    public float placementPlaneY = 0f;

    // Estado interno
    private bool isHeavyMode = true;
    private RelativityBody draggedBody = null;
    private Vector3 dragOffset = Vector3.zero;
    private Vector3 lastDragPos = Vector3.zero;
    private Vector3 dragVelocity = Vector3.zero;

    // Média suavizada da velocidade de drag — evita linhas instáveis com massas pequenas
    private Vector3[] velocitySamples = new Vector3[12];
    private int velocitySampleIndex = 0;
    private Vector3 smoothDragVelocity = Vector3.zero;

    // Contadores para nomes automáticos
    private int heavyCounter = 0;
    private int lightCounter = 0;

    void Start()
    {
        if (mainCamera == null) mainCamera = Camera.main;

        // Inicializa o slider
        if (massSlider != null)
        {
            massSlider.minValue = 50f;
            massSlider.maxValue = 500f;
            massSlider.value = heavyBodyMass;
            massSlider.onValueChanged.AddListener(OnMassSliderChanged);
        }

        UpdateModeUI();
    }

    void Update()
    {
        HandleInput();
        UpdateMassText();

        // Tecla O — aplica velocidade orbital automática ao planeta selecionado
        // Segue o mesmo padrão do laboratório (O + clique esquerdo)
        if (Keyboard.current.oKey.wasPressedThisFrame && timeline != null && timeline.IsPaused)
        {
            // Aplica a todos os planetas leves na cena
            foreach (Transform child in transform)
            {
                RelativityBody body = child.GetComponent<RelativityBody>();
                if (body == null || body.deformsGrid) continue;
                ApplyOrbitalVelocity(body);
            }
        }
    }

    void HandleInput()
    {
        //Clique esquerdo
        if (Mouse.current.leftButton.wasPressedThisFrame)
        {
            // Verifica se clicou numa massa existente para arrastar
            RelativityBody hit = RaycastBody();
            if (hit != null)
            {
                // Inicia o drag
                draggedBody = hit;
                draggedBody.StartDrag();
                lastDragPos = GetMouseWorldPos();
                dragVelocity = Vector3.zero;
                smoothDragVelocity = Vector3.zero;
                velocitySampleIndex = 0;
                for (int i = 0; i < velocitySamples.Length; i++) velocitySamples[i] = Vector3.zero;
                return;
            }

            // Clique na grid — coloca uma nova massa
            Vector3 pos = GetMouseWorldPos();
            if (IsInsideGrid(pos))
                PlaceBody(pos);
        }

        //Arrastar
        if (Mouse.current.leftButton.isPressed && draggedBody != null)
        {
            Vector3 currentPos = GetMouseWorldPosAtHeight(draggedBody.transform.position.y);

            // Calcula velocidade instantânea e suaviza
            // Só atualiza se o movimento for significativo — evita tremor com micromovimentos (coisa que ainda acontece mas com menor impacto)
            float moveDist = Vector3.Distance(currentPos, lastDragPos);
            if (moveDist > 0.05f)
            {
                Vector3 instantVel = (currentPos - lastDragPos) / Mathf.Max(Time.deltaTime, 0.016f);
                velocitySamples[velocitySampleIndex % velocitySamples.Length] = instantVel;
                velocitySampleIndex++;
            }

            // Média dos samples
            Vector3 avgVel = Vector3.zero;
            foreach (Vector3 s in velocitySamples) avgVel += s;
            avgVel /= velocitySamples.Length;

            // Lerp suave entre a velocidade anterior e a nova — amortece picos bruscos
            smoothDragVelocity = Vector3.Lerp(smoothDragVelocity, avgVel, 0.15f);
            dragVelocity = smoothDragVelocity;
            lastDragPos = currentPos;

            draggedBody.MoveTo(currentPos);

            // Desenha a trajetória prevista só para massas leves
            if (trajectoryPreview != null && !draggedBody.deformsGrid)
                trajectoryPreview.DrawPreview(draggedBody.transform.position, dragVelocity * 0.8f);
        }

        // Largar
        if (Mouse.current.leftButton.wasReleasedThisFrame && draggedBody != null)
        {
            if (trajectoryPreview != null) trajectoryPreview.Hide();
            draggedBody.EndDrag(dragVelocity);
            draggedBody = null;
        }

        // Clique direito — remove massa
        if (Mouse.current.rightButton.wasPressedThisFrame)
        {
            RelativityBody hit = RaycastBody();
            if (hit != null)
                RemoveBody(hit);
        }
    }

    // Coloca um novo corpo na posição dada
    void PlaceBody(Vector3 worldPos)
    {
        GameObject prefab = isHeavyMode ? heavyBodyPrefab : lightBodyPrefab;
        if (prefab == null) return;

        GameObject obj = Instantiate(prefab, worldPos, Quaternion.identity);
        obj.transform.parent = this.transform;

        RelativityBody body = obj.GetComponent<RelativityBody>();
        if (body == null) body = obj.AddComponent<RelativityBody>();

        body.grid = grid;
        body.timeline = timeline;
        body.deformsGrid = isHeavyMode;
        body.mass = isHeavyMode ? heavyBodyMass : lightBodyMass;
        // Massas leves começam sem velocidade — o utilizador dá-lhes velocidade arrastando-as

        // Nome automático
        obj.name = isHeavyMode
            ? $"HeavyMass #{++heavyCounter}"
            : $"LightMass #{++lightCounter}";

        // Atualiza a aparência se tiver StarComponent (reutiliza prefabs do laboratório)
        StarComponent sc = obj.GetComponent<StarComponent>();
        if (sc != null)
        {
            sc.mass = body.mass;
            sc.isPlanet = !isHeavyMode;
            sc.velocity = Vector3.zero; // sem física N-body nesta cena
            sc.UpdateAppearance();

            // Remove o TrailRenderer — não faz sentido nesta cena
            TrailRenderer tr = obj.GetComponent<TrailRenderer>();
            if (tr != null) tr.enabled = false;
        }
    }

    // Remove um corpo da cena e da grid
    void RemoveBody(RelativityBody body)
    {
        if (body == null) return;
        // O OnDestroy do RelativityBody trata de chamar grid.UnregisterBody
        Destroy(body.gameObject);
    }

    // Devolve o RelativityBody mais próximo do ponto do rato
    // Usa a altura Y de cada corpo para converter o clique corretamente
    RelativityBody RaycastBody()
    {
        RelativityBody nearest = null;
        float minDist = 8f;

        foreach (Transform child in transform)
        {
            RelativityBody body = child.GetComponent<RelativityBody>();
            if (body == null) continue;

            // Converte o clique do rato usando a altura Y real do corpo
            Vector3 mousePos = GetMouseWorldPosAtHeight(child.position.y);

            float dx = child.position.x - mousePos.x;
            float dz = child.position.z - mousePos.z;
            float dist = Mathf.Sqrt(dx * dx + dz * dz);

            if (dist < minDist) { minDist = dist; nearest = body; }
        }
        return nearest;
    }

    // Devolve a posição do rato num plano horizontal à altura Y especificada
    Vector3 GetMouseWorldPosAtHeight(float y)
    {
        Ray ray = mainCamera.ScreenPointToRay(Mouse.current.position.ReadValue());
        Plane plane = new Plane(Vector3.up, new Vector3(0f, y, 0f));

        if (plane.Raycast(ray, out float dist))
            return ray.GetPoint(dist);

        return Vector3.zero;
    }

    // Devolve a posição do rato no plano horizontal Y = placementPlaneY
    Vector3 GetMouseWorldPos()
    {
        Ray ray = mainCamera.ScreenPointToRay(Mouse.current.position.ReadValue());
        Plane plane = new Plane(Vector3.up, new Vector3(0f, placementPlaneY, 0f));

        if (plane.Raycast(ray, out float dist))
            return ray.GetPoint(dist);

        return Vector3.zero;
    }

    // Verifica se uma posição está dentro dos limites da grid
    bool IsInsideGrid(Vector3 pos)
    {
        if (grid == null) return false;
        float half = grid.GridWorldSize * 0.5f;
        float gx = grid.transform.position.x;
        float gz = grid.transform.position.z;
        return pos.x >= gx - half && pos.x <= gx + half
            && pos.z >= gz - half && pos.z <= gz + half;
    }

    // Chamado pelo botão "Estrela" na UI
    public void SetHeavyMode()
    {
        isHeavyMode = true;
        if (massSlider != null)
        {
            massSlider.minValue = 50f;
            massSlider.maxValue = 500f;
            massSlider.value = heavyBodyMass;
        }
        UpdateModeUI();
    }

    // Chamado pelo botão "Planeta" na UI
    public void SetLightMode()
    {
        isHeavyMode = false;
        if (massSlider != null)
        {
            massSlider.minValue = 1f;
            massSlider.maxValue = 50f;
            massSlider.value = lightBodyMass;
        }
        UpdateModeUI();
    }

    void OnMassSliderChanged(float value)
    {
        if (isHeavyMode) heavyBodyMass = value;
        else lightBodyMass = value;
    }

    void UpdateModeUI()
    {
        if (modeText != null)
            modeText.text = isHeavyMode ? "Modo: Estrela" : "Modo: Planeta";
    }

    void UpdateMassText()
    {
        if (massText == null || massSlider == null) return;

        if (isHeavyMode)
        {
            // Converte para massas solares (mesmo fator do laboratório)
            float solar = massSlider.value * 0.004f;
            massText.text = $"Massa: {solar:F2} M_sun";
        }
        else
        {
            float earth = massSlider.value * 0.333f;
            massText.text = $"Massa: {earth:F1} M_earth";
        }
    }

    // Remove todas as massas da cena — botão Reset
    public void ResetScene()
    {
        for (int i = transform.childCount - 1; i >= 0; i--)
            Destroy(transform.GetChild(i).gameObject);

        heavyCounter = 0;
        lightCounter  = 0;
        draggedBody = null;
    }

    // Calcula e aplica velocidade orbital ao planeta leve em relação à estrela mais próxima
    // Mesmo padrão do CalcOrbitalVelocity do MouseInteraction no laboratório
    void ApplyOrbitalVelocity(RelativityBody lightBody)
    {
        RelativityBody nearest = GetNearestHeavyBody(lightBody.transform.position);
        if (nearest == null) return;

        Vector3 toHeavy = nearest.transform.position - lightBody.transform.position;
        toHeavy.y = 0f;
        float r = toHeavy.magnitude;
        if (r < 0.1f) return;

        // Velocidade orbital calibrada para a grid:
        // v = sqrt(deformStrength * massRatio * slideForce / r)
        float massRatio = Mathf.Clamp(nearest.mass / grid.referenceMass, 0f, 2f);
        float speed = Mathf.Sqrt(grid.deformStrength * massRatio * lightBody.slideForce / Mathf.Max(r, 1f));
        speed = Mathf.Clamp(speed, 0.5f, 30f);

        // Direção perpendicular ao vetor planeta-estrela no plano XZ
        // Cross de Vector3.up com toHeavy.normalized — mesmo cálculo do laboratório
        Vector3 tangent = Vector3.Cross(Vector3.up, toHeavy.normalized).normalized;
        lightBody.EndDrag(tangent * speed);
    }

    // Devolve a massa pesada mais próxima de uma posição
    RelativityBody GetNearestHeavyBody(Vector3 position)
    {
        RelativityBody nearest = null;
        float minDist = float.MaxValue;

        foreach (Transform child in transform)
        {
            RelativityBody body = child.GetComponent<RelativityBody>();
            if (body == null || !body.deformsGrid) continue;

            float d = Vector3.Distance(position, child.position);
            if (d < minDist) { minDist = d; nearest = body; }
        }
        return nearest;
    }
}
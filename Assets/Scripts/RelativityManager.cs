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
    public GravitationalWaves gravitationalWaves;

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
    [Tooltip("Prefab do buraco negro — massa extrema com horizonte de eventos e disco de acreção")]
    public GameObject blackHolePrefab;

    [Header("Configuração das Massas")]
    public float heavyBodyMass = 200f;
    public float lightBodyMass = 5f;
    public float blackHoleMass = 800f;

    // Modo atual — Estrela, Planeta ou Buraco Negro
    private enum PlacementMode { Heavy, Light, BlackHole }
    private PlacementMode currentMode = PlacementMode.Heavy;

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

            // Dispara onda gravitacional ao largar a massa após drag
            if (gravitationalWaves != null)
                gravitationalWaves.SpawnWave(draggedBody.transform.position, draggedBody.mass);

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
        GameObject prefab = currentMode == PlacementMode.Heavy     ? heavyBodyPrefab
                          : currentMode == PlacementMode.BlackHole ? blackHolePrefab
                          : lightBodyPrefab;
        if (prefab == null) return;

        GameObject obj = Instantiate(prefab, worldPos, Quaternion.identity);
        obj.transform.parent = this.transform;

        RelativityBody body = obj.GetComponent<RelativityBody>();
        if (body == null) body = obj.AddComponent<RelativityBody>();

        body.grid = grid;
        body.timeline = timeline;

        if (currentMode == PlacementMode.BlackHole)
        {
            body.deformsGrid = true;
            body.mass = blackHoleMass;
            body.overrideDeformation = true;
            body.customDeformStrength = 80f;
            body.customDeformRadius = 50f;
            body.customDeformFalloff  = 3.5f;
            obj.name = "BlackHole";

            // Liga o GravitationalWaves ao BlackHoleBody para a absorção disparar ondas
            BlackHoleBody bhb = obj.GetComponent<BlackHoleBody>();
            if (bhb != null) bhb.gravitationalWaves = gravitationalWaves;
        }
        else
        {
            body.deformsGrid = currentMode == PlacementMode.Heavy;
            body.mass = currentMode == PlacementMode.Heavy ? heavyBodyMass : lightBodyMass;
            obj.name = currentMode == PlacementMode.Heavy
                ? $"HeavyMass #{++heavyCounter}"
                : $"LightMass #{++lightCounter}";
        }

        // Dispara onda gravitacional ao colocar a massa
        if (gravitationalWaves != null)
            gravitationalWaves.SpawnWave(worldPos, body.mass);
        StarComponent sc = obj.GetComponent<StarComponent>();
        if (sc != null)
        {
            sc.mass = body.mass;
            sc.isPlanet = currentMode == PlacementMode.Light;
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

    // Devolve a posição do rato na superfície real da grid
    // Usa Physics.Raycast contra o MeshCollider da grid — posição sempre correta
    Vector3 GetMouseWorldPos()
    {
        Ray ray = mainCamera.ScreenPointToRay(Mouse.current.position.ReadValue());

        if (Physics.Raycast(ray, out RaycastHit hit, 2000f))
        {
            // Acertou na grid ou noutro objeto — usa o ponto de impacto
            return hit.point;
        }

        // Fallback — plano Y=0 se não acertar em nada
        Plane plane = new Plane(Vector3.up, Vector3.zero);
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
        currentMode = PlacementMode.Heavy;
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
        currentMode = PlacementMode.Light;
        if (massSlider != null)
        {
            massSlider.minValue = 1f;
            massSlider.maxValue = 50f;
            massSlider.value = lightBodyMass;
        }
        UpdateModeUI();
    }

    // Chamado pelo botão "Buraco Negro" na UI
    public void SetBlackHoleMode()
    {
        currentMode = PlacementMode.BlackHole;
        UpdateModeUI();
    }

    void OnMassSliderChanged(float value)
    {
        if (currentMode == PlacementMode.Heavy)      heavyBodyMass = value;
        else if (currentMode == PlacementMode.Light) lightBodyMass = value;
    }

    void UpdateModeUI()
    {
        // Esconde o slider no modo buraco negro — massa é fixa
        if (massSlider != null)
            massSlider.gameObject.SetActive(currentMode != PlacementMode.BlackHole);

        if (modeText != null)
            modeText.text = currentMode == PlacementMode.Heavy ? "Modo: Estrela"
                          : currentMode == PlacementMode.BlackHole ? "Modo: Buraco Negro"
                          : "Modo: Planeta";
    }

    void UpdateMassText()
    {
        if (massText == null || massSlider == null) return;

        if (currentMode == PlacementMode.BlackHole)
        {
            massText.text = $"Massa: Buraco Negro";
        }
        else if (currentMode == PlacementMode.Heavy)
        {
            float solar = massSlider.value * 0.004f;
            massText.text = $"Massa: {solar:F2} M_sun";
        }
        else
        {
            float earth = massSlider.value * 0.333f;
            massText.text = $"Massa: {earth:F1} M_earth";
        }
    }

    // Remove todas as massas da cena com animação suave
    // Cada massa dispara uma onda gravitacional final e encolhe antes de desaparecer
    public void ResetScene()
    {
        StartCoroutine(SoftReset());
    }

    System.Collections.IEnumerator SoftReset()
    {
        // Bloqueia input durante o reset
        draggedBody = null;
        if (trajectoryPreview != null) trajectoryPreview.Hide();

        // Recolhe todos os corpos antes de começar a destruir
        var bodies = new System.Collections.Generic.List<RelativityBody>();
        foreach (Transform child in transform)
        {
            RelativityBody b = child.GetComponent<RelativityBody>();
            if (b != null) bodies.Add(b);
        }

        // Dispara uma onda gravitacional final para cada massa — com delay escalonado
        // para as ondas não se sobreporem todas ao mesmo tempo
        for (int i = 0; i < bodies.Count; i++)
        {
            if (bodies[i] == null) continue;
            if (gravitationalWaves != null)
                gravitationalWaves.SpawnWave(bodies[i].transform.position, bodies[i].mass * 0.5f);
            yield return new WaitForSeconds(0.08f); // pequeno delay entre cada onda
        }

        // Aguarda um momento para as ondas começarem a propagar
        yield return new WaitForSeconds(0.4f);

        // Encolhe e destrói cada corpo gradualmente
        float shrinkDuration = 0.6f;
        var shrinkCoroutines = new System.Collections.Generic.List<System.Collections.IEnumerator>();

        foreach (RelativityBody b in bodies)
        {
            if (b != null)
                StartCoroutine(ShrinkAndDestroy(b.gameObject, shrinkDuration));
        }

        // Aguarda o fim das animações de encolhimento
        yield return new WaitForSeconds(shrinkDuration + 0.1f);

        // Reset dos contadores
        heavyCounter = 0;
        lightCounter = 0;
    }

    // Encolhe o objeto suavemente antes de o destruir
    System.Collections.IEnumerator ShrinkAndDestroy(GameObject obj, float duration)
    {
        if (obj == null) yield break;

        Vector3 startScale = obj.transform.localScale;
        float   elapsed    = 0f;

        while (elapsed < duration)
        {
            if (obj == null) yield break;
            elapsed += Time.deltaTime;
            float t  = Mathf.Clamp01(elapsed / duration);
            // Ease in cubic — encolhe devagar no início e rápido no fim
            float ease = t * t * t;
            obj.transform.localScale = Vector3.Lerp(startScale, Vector3.zero, ease);
            yield return null;
        }

        if (obj != null) Destroy(obj);
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
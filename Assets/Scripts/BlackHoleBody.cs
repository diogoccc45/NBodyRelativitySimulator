using UnityEngine;

// Buraco negro — massa extremamente alta que deforma a grid ao máximo
// Tem horizonte de eventos (esfera negra) e disco de acreção animado
// Herda o comportamento do RelativityBody mas com efeitos visuais próprios
[RequireComponent(typeof(RelativityBody))]
public class BlackHoleBody : MonoBehaviour
{
    [Header("Configurações")]
    [Tooltip("Massa do buraco negro — muito maior que uma estrela normal")]
    public float blackHoleMass = 800f;

    [Header("Referências Visuais")]
    [Tooltip("GameObject filho com a esfera do horizonte de eventos")]
    public GameObject eventHorizon;
    [Tooltip("GameObject filho com o disco de acreção (mesh achatada)")]
    public GameObject accretionDisk;
    [Tooltip("GameObject filho com a metade frontal do disco — aparece por cima da esfera")]
    public GameObject accretionDiskFront;

    [Header("Escala")]
    [Tooltip("Raio do horizonte de eventos em game units")]
    public float horizonRadius = 3f;
    [Tooltip("Raio exterior do disco de acreção")]
    public float diskRadius    = 8f;
    [Tooltip("Espessura do disco")]
    public float diskThickness = 0.6f;

    [Header("Inclinação do Disco")]
    [Tooltip("Inclinação do disco de acreção em graus — visualmente mais realista inclinado")]
    public float diskTilt = 15f;

    [Header("Referências")]
    public GravitationalWaves gravitationalWaves;

    private RelativityBody body;
    // Regista corpos já em absorção para não iniciar duas vezes
    private System.Collections.Generic.HashSet<RelativityBody> absorbing =
        new System.Collections.Generic.HashSet<RelativityBody>();

    void Awake()
    {
        body = GetComponent<RelativityBody>();
        SetupVisuals();
    }

    void Start()
    {
        // Configura o RelativityBody com massa extrema e deformação personalizada
        if (body != null)
        {
            body.mass = blackHoleMass;
            body.deformsGrid = true;
            body.overrideDeformation = true;
            body.customDeformStrength = 80f; // fosso muito mais profundo
            body.customDeformRadius = 50f; // raio de influência maior
            body.customDeformFalloff = 3.5f; // curva abrupta — fosso vertical
        }
    }

    void SetupVisuals()
    {
        Debug.Log($"[BlackHoleBody] SetupVisuals — horizonRadius:{horizonRadius} diskRadius:{diskRadius}");

        // Horizonte de eventos — esfera negra
        if (eventHorizon != null)
        {
            eventHorizon.transform.localPosition = Vector3.zero;
            eventHorizon.transform.localScale = Vector3.one * horizonRadius * 2f;
        }
        else Debug.LogWarning("[BlackHoleBody] EventHorizon não está ligado!");

        // Disco de acreção — Quad plano com escala correta
        // Para um Quad: X e Y definem o tamanho, Z é irrelevante
        if (accretionDisk != null)
        {
            accretionDisk.transform.localPosition = Vector3.zero;
            accretionDisk.transform.localScale = new Vector3(diskRadius * 2f,
                                                                diskRadius * 2f,
                                                                1f);
            accretionDisk.transform.localRotation = Quaternion.Euler(diskTilt, 0f, 0f);
        }
        else Debug.LogWarning("[BlackHoleBody] AccretionDisk não está ligado!");

        // Disco frontal — completamente plano (sem inclinação) para o arco aparecer sempre por cima da esfera independentemente do ângulo de câmara
        if (accretionDiskFront != null)
        {
            accretionDiskFront.transform.localPosition = Vector3.zero;
            accretionDiskFront.transform.localScale = new Vector3(diskRadius * 2f,
                                                                     diskRadius * 2f,
                                                                     1f);
            accretionDiskFront.transform.localRotation = Quaternion.identity; // sem inclinação
        }
    }

    // Absorve planetas leves que entrem no raio do horizonte de eventos
    void Update()
    {
        if (body == null || body.grid == null) return;

        // Durante o scattering binário não absorve nada — o BinaryBlackHoleManager gere tudo
        if (!body.enabled) return;

        RelativityBody[] allBodies = FindObjectsByType<RelativityBody>(FindObjectsSortMode.None);
        foreach (RelativityBody other in allBodies)
        {
            if (other == body || other.deformsGrid) continue;
            if (absorbing.Contains(other)) continue;

            float dx = transform.position.x - other.transform.position.x;
            float dz = transform.position.z - other.transform.position.z;
            float dist = Mathf.Sqrt(dx * dx + dz * dz);

            if (dist < horizonRadius * 2f)
            {
                absorbing.Add(other);
                StartCoroutine(AbsorbBody(other));
            }
        }
    }

    // Absorção cientificamente correta:
    // 1. Espiral acelerada (conservação de momento angular)
    // 2. Spaghettification (forças de maré esticam o objeto radialmente)
    // 3. Flash de radiação (energia libertada pelo disco de acreção)
    // 4. Onda gravitacional (perturbação do espaço-tempo)
    System.Collections.IEnumerator AbsorbBody(RelativityBody other)
    {
        if (other == null) yield break;

        other.enabled = false;

        Renderer rend = other.GetComponent<Renderer>();
        Vector3  startScale = other.transform.localScale;
        Color startColor = rend != null ? rend.material.color : Color.white;

        // Fase 1: Espiral acelerada 
        float spiralDuration = 2.5f;
        float elapsed = 0f;

        Vector3 toBlackHole = transform.position - other.transform.position;
        toBlackHole.y = 0f;
        float startRadius = Mathf.Max(toBlackHole.magnitude, horizonRadius * 2f);
        Vector3 orbitAxis = Vector3.up;
        float angularSpeed = 90f;

        while (elapsed < spiralDuration)
        {
            if (other == null) yield break;
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / spiralDuration);

            float currentRadius = Mathf.Lerp(startRadius, horizonRadius * 0.5f, t * t);
            float currentAngSpeed = angularSpeed * (startRadius / Mathf.Max(currentRadius, 0.5f));
            currentAngSpeed = Mathf.Min(currentAngSpeed, 540f);

            Vector3 dir = (other.transform.position - transform.position);
            dir.y = 0f;
            if (dir.sqrMagnitude > 0.001f)
            {
                dir = Quaternion.AngleAxis(currentAngSpeed * Time.deltaTime, orbitAxis) * dir.normalized;
                Vector3 newPos = transform.position + dir * currentRadius;
                // Mantém o planeta colado à grid durante a espiral
                if (other.grid != null)
                    newPos.y = other.grid.GetGridHeightAt(newPos.x, newPos.z) + other.transform.localScale.x * 0.5f;
                other.transform.position = newPos;
            }

            // Spaghettification
            float spaghetti = Mathf.Lerp(1f, 0.1f, t * t);
            float stretch = Mathf.Lerp(1f, 3.0f, t * t);
            Vector3 radialDir = (transform.position - other.transform.position).normalized;
            other.transform.rotation  = radialDir != Vector3.zero
                ? Quaternion.LookRotation(radialDir) : Quaternion.identity;
            other.transform.localScale = new Vector3(
                startScale.x * spaghetti,
                startScale.y * spaghetti,
                startScale.z * stretch);

            // Aquece a cor progressivamente
            if (rend != null)
            {
                Color hotColor = Color.Lerp(startColor, new Color(1f, 0.5f, 0.05f), t);
                rend.material.color = hotColor;
                rend.material.SetColor("_EmissionColor", hotColor * Mathf.Lerp(2f, 12f, t));
                rend.material.EnableKeyword("_EMISSION");
            }

            yield return null;
        }

        if (other == null) yield break;

        // Jatos partem da posição do buraco negro — sempre visíveis independentemente do fosso
        Vector3 jetOrigin = transform.position;

        // Fase 3: Jatos Relativísticos
        GameObject jetUp = CreateJet(jetOrigin,  Vector3.up);
        GameObject jetDown = CreateJet(jetOrigin, -Vector3.up);

        // Fase 4: Shockwave na grid
        if (gravitationalWaves != null)
            gravitationalWaves.SpawnShockwave(jetOrigin, other.mass);

        // Anima os jatos
        float jetDuration  = 1.8f;
        elapsed = 0f;
        float maxJetLength = horizonRadius * 30f;

        while (elapsed < jetDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / jetDuration);
            float ease = 1f - Mathf.Pow(1f - t, 2f);

            float jetLength = Mathf.Lerp(0f, maxJetLength, ease);
            float jetAlpha  = t < 0.5f ? 1f : Mathf.Lerp(1f, 0f, (t - 0.5f) / 0.5f);

            if (jetUp != null) UpdateJet(jetUp,   jetOrigin,  Vector3.up,  jetLength, jetAlpha);
            if (jetDown != null) UpdateJet(jetDown,  jetOrigin, -Vector3.up, jetLength, jetAlpha);

            if (other != null)
                other.transform.localScale = Vector3.Lerp(Vector3.one * 0.3f, Vector3.zero, t);

            yield return null;
        }

        Destroy(jetUp);
        Destroy(jetDown);
        absorbing.Remove(other);
        if (other != null) Destroy(other.gameObject);
    }

    // Cria um jato relativístico como LineRenderer
    GameObject CreateJet(Vector3 origin, Vector3 direction)
    {
        GameObject go = new GameObject("RelJet");
        go.transform.position = origin;

        LineRenderer lr = go.AddComponent<LineRenderer>();
        lr.positionCount = 2;
        lr.useWorldSpace = true;
        lr.startWidth = horizonRadius * 2.5f;
        lr.endWidth = horizonRadius * 0.05f;
        lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        lr.numCapVertices = 8;

        Material mat = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
        mat.color = new Color(0.5f, 0.8f, 1.0f);
        mat.SetColor("_BaseColor", new Color(0.5f, 0.8f, 1.0f));
        lr.material = mat;

        // Gradiente branco na base, azul na ponta
        Gradient grad = new Gradient();
        grad.SetKeys(
            new GradientColorKey[] {
                new GradientColorKey(Color.white,                   0.0f),
                new GradientColorKey(new Color(0.5f, 0.8f, 1.0f),  0.3f),
                new GradientColorKey(new Color(0.2f, 0.5f, 1.0f),  1.0f)
            },
            new GradientAlphaKey[] {
                new GradientAlphaKey(1.0f, 0.0f),
                new GradientAlphaKey(0.8f, 0.5f),
                new GradientAlphaKey(0.0f, 1.0f)
            }
        );
        lr.colorGradient = grad;

        lr.SetPosition(0, origin);
        lr.SetPosition(1, origin);

        return go;
    }

    // Atualiza posição e largura do jato
    void UpdateJet(GameObject jet, Vector3 origin, Vector3 direction, float length, float alpha)
    {
        if (jet == null) return;
        LineRenderer lr = jet.GetComponent<LineRenderer>();
        if (lr == null) return;

        lr.SetPosition(0, origin);
        lr.SetPosition(1, origin + direction * length);
        lr.startWidth = horizonRadius * 2.5f * alpha;
        lr.endWidth   = horizonRadius * 0.05f * alpha;
    }
}
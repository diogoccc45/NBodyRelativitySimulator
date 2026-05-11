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

    private RelativityBody body;

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

        // Verifica todos os RelativityBody na cena
        RelativityBody[] allBodies = FindObjectsByType<RelativityBody>(FindObjectsSortMode.None);
        foreach (RelativityBody other in allBodies)
        {
            if (other == body || other.deformsGrid) continue;

            float dist = Vector3.Distance(transform.position, other.transform.position);
            if (dist < horizonRadius * 1.2f)
            {
                // Absorção — destrói o planeta leve com efeito
                StartCoroutine(AbsorbBody(other));
            }
        }
    }

    // Coroutine de absorção — o planeta espirala para o centro e desaparece
    System.Collections.IEnumerator AbsorbBody(RelativityBody other)
    {
        if (other == null) yield break;

        float duration = 0.4f;
        float elapsed  = 0f;
        Vector3 startPos = other.transform.position;
        Vector3 startScale = other.transform.localScale;

        // Desativa o script para parar o movimento normal
        other.enabled = false;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;

            // Espirala para o centro do buraco negro
            other.transform.position = Vector3.Lerp(startPos, transform.position, t * t);
            other.transform.localScale = Vector3.Lerp(startScale, Vector3.zero, t);
            yield return null;
        }

        Destroy(other.gameObject);
    }
}
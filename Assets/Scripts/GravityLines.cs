using UnityEngine;
using System.Collections.Generic;

// Desenha linhas de força gravitacional entre os corpos da simulação
// Ativado/desativado pelo MouseInteraction com a tecla G
public class GravityLines : MonoBehaviour
{
    [Header("Referências")]
    public StarSystemManager manager;

    [Header("Configurações Visuais")]
    [Tooltip("Espessura máxima da linha (força máxima)")]
    public float maxLineWidth = 0.8f;
    [Tooltip("Espessura mínima da linha (força mínima visível)")]
    public float minLineWidth = 0.05f;
    [Tooltip("Força mínima para desenhar a linha — evita spam de linhas fracas")]
    public float minForceThreshold = 0.5f;

    [Header("Limite de Pares (Performance)")]
    [Tooltip("Número máximo de linhas desenhadas por frame — aumenta gradualmente conforme o teu PC aguentar")]
    public int maxPairs = 15;

    [Header("Cor")]
    public Color lineColorWeak = new Color(0.2f, 0.4f, 1.0f, 0.3f);  // azul fraco
    public Color lineColorStrong = new Color(1.0f, 0.8f, 0.2f, 0.8f);  // amarelo forte

    private bool isActive = false;
    private List<LineRenderer> linePool = new List<LineRenderer>();

    void Start()
    {
        // Cria o pool de LineRenderers com maxPairs linhas
        // Pool evita criar/destruir objetos a cada frame — mais eficiente
        for (int i = 0; i < maxPairs; i++)
        {
            GameObject go = new GameObject($"GravLine_{i}");
            go.transform.parent = this.transform;

            LineRenderer lr = go.AddComponent<LineRenderer>();
            lr.positionCount = 2;
            lr.useWorldSpace = true;
            lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            lr.receiveShadows = false;

            // Material simples unlit
            Material mat = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
            mat.color = lineColorWeak;
            // Transparência aditiva — linhas brilham sobre o fundo escuro
            mat.SetFloat("_Surface", 1f); // Transparent
            mat.SetFloat("_Blend", 0f); // Alpha
            mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            lr.material = mat;

            lr.enabled = false;
            linePool.Add(lr);
        }

        gameObject.SetActive(false); // começa inativo
    }

    // Chamado pelo MouseInteraction quando G é pressionado
    public void SetActive(bool active)
    {
        isActive = active;
        gameObject.SetActive(active);

        // Esconde todas as linhas ao desligar
        if (!active)
            foreach (var lr in linePool)
                lr.enabled = false;
    }

    void LateUpdate()
    {
        if (!isActive || manager == null) return;

        var stars = manager.GetStars();
        if (stars == null || stars.Count < 2)
        {
            foreach (var lr in linePool) lr.enabled = false;
            return;
        }

        // Calcula todos os pares e a força entre eles
        // Guarda numa lista ordenada por força — só desenha os maxPairs mais fortes
        var pairs = new List<(StarComponent a, StarComponent b, float force)>();

        for (int i = 0; i < stars.Count; i++)
        {
            if (stars[i] == null) continue;
            for (int j = i + 1; j < stars.Count; j++)
            {
                if (stars[j] == null) continue;

                // Fragmentos não geram linhas de força — reduz ruído visual
                if (stars[i].gameObject.CompareTag("Fragment")) continue;
                if (stars[j].gameObject.CompareTag("Fragment")) continue;

                float dist  = Vector3.Distance(stars[i].transform.position,
                                               stars[j].transform.position);
                float distSq = Mathf.Max(dist * dist, 25f);

                // F ∝ G * m_1 * m_2 / r^2 — igual ao FixedUpdate do StarSystemManager
                float force = (manager.G * stars[i].mass * stars[j].mass) / distSq;

                if (force >= minForceThreshold)
                    pairs.Add((stars[i], stars[j], force));
            }
        }

        // Ordena por força decrescente e limita ao máximo configurado
        pairs.Sort((x, y) => y.force.CompareTo(x.force));
        int count = Mathf.Min(pairs.Count, maxPairs);

        // Força máxima atual para normalizar a espessura e cor
        float maxForce = count > 0 ? pairs[0].force : 1f;

        // Atualiza as linhas do pool
        for (int i = 0; i < linePool.Count; i++)
        {
            if (i >= count)
            {
                linePool[i].enabled = false;
                continue;
            }

            var (a, b, force) = pairs[i];
            float t = Mathf.Clamp01(force / maxForce);

            LineRenderer lr = linePool[i];
            lr.enabled = true;
            lr.SetPosition(0, a.transform.position);
            lr.SetPosition(1, b.transform.position);

            // Espessura proporcional à força — linhas fortes são mais grossas
            float width = Mathf.Lerp(minLineWidth, maxLineWidth, t);
            lr.startWidth = width;
            lr.endWidth   = width;

            // Cor interpolada fraco→forte
            Color c = Color.Lerp(lineColorWeak, lineColorStrong, t);
            lr.material.color = c;
            lr.material.SetColor("_BaseColor", c);
        }
    }
}
using UnityEngine;

public class StarComponent : MonoBehaviour
{
    public float mass = 1.0f;
    public Vector3 velocity;
    
    // Booleano para distinguir se este objeto é um planeta
    public bool isPlanet = false;
    public int mergeCount = 0; // número de fusões que este objeto sofreu

    private Renderer starRenderer;
    private TrailRenderer starTrail;

    void Awake()
    {
        // Pega no Prefab
        starRenderer = GetComponent<Renderer>();
        starTrail = GetComponent<TrailRenderer>();
    }

    // Esta função será chamada sempre que a massa mudar
    public void UpdateAppearance()
    {
        float t = Mathf.InverseLerp(10f, 500f, mass);

        // Harvard spectral sequence: massa "pequena" = red (M-type), massa "elevada" = blue-white (O/B-type)
        // 0.0 (10) -> red (M-type dwarf)
        // 0.3 (157) -> orange (K-type)
        // 0.5 (255) -> yellow (G-type, Sun-like)
        // 0.7 (360) -> white (F/A-type)
        // 1.0 (500) -> blue-white (B/O-type) - COPIADO DE PAPER
        Color targetColor;
        if (t < 0.3f)
            targetColor = Color.Lerp(new Color(1.0f, 0.05f, 0.05f), new Color(1.0f, 0.5f, 0.2f), t / 0.3f);
        else if (t < 0.5f)
            targetColor = Color.Lerp(new Color(1.0f, 0.5f, 0.2f), new Color(1.0f, 0.95f, 0.6f), (t - 0.3f) / 0.2f);
        else if (t < 0.7f)
            targetColor = Color.Lerp(new Color(1.0f, 0.95f, 0.6f), new Color(1.0f, 1.0f, 1.0f), (t - 0.5f) / 0.2f);
        else
            targetColor = Color.Lerp(new Color(1.0f, 1.0f, 1.0f), new Color(0.5f, 0.7f, 1.0f), (t - 0.7f) / 0.3f);

        if (isPlanet)
        {
            // Sequência de composição química baseada na massa interna (unidades internas):
            // 0.33 – 1.5 → Rochosos pequenos (Marte, Mercúrio) — cinzento-acastanhado
            // 1.5 – 6.0 → Rochosos grandes + super-Terras (Terra, Vénus) — azul-esverdeado
            // 6.0 – 51 → Gigantes de gelo (Urano ~51, Neptuno ~51) — azul-ciano frio
            // Gigantes gasosos excluídos — massa de Júpiter (~955 u.i.) perturbaria as estrelas
            if (mass < 1.5f)
            {
                float t_p = Mathf.InverseLerp(0.33f, 1.5f, mass);
                targetColor = Color.Lerp(new Color(0.45f, 0.35f, 0.30f), // cinzento-acastanhado escuro (Marte)
                                         new Color(0.60f, 0.50f, 0.42f), // acastanhado mais claro (Mercúrio)
                                         t_p);
            }
            else if (mass < 6.0f)
            {
                float t_p = Mathf.InverseLerp(1.5f, 6.0f, mass);
                targetColor = Color.Lerp(new Color(0.35f, 0.50f, 0.40f), // azul-esverdeado acinzentado (Terra)
                                         new Color(0.20f, 0.50f, 0.65f), // azul-esverdeado saturado (super-Terra)
                                         t_p);
            }
            else
            {
                float t_p = Mathf.InverseLerp(6.0f, 51f, mass);
                targetColor = Color.Lerp(new Color(0.20f, 0.50f, 0.65f), // azul-esverdeado frio
                                         new Color(0.15f, 0.40f, 0.75f), // azul profundo (Neptuno/Urano)
                                         t_p);
            }
        }

        float scale = 0.5f + (mass / 100f);
        
        // Planetas: escala proporcional à massa real
        // Terra (~3 u.i.) → 1.2,  Neptuno (~51 u.i.) → 3.0
        // Mantém-se sempre abaixo da menor estrela (escala ~0.6)
        if (isPlanet) scale = Mathf.Lerp(1.0f, 3.0f, Mathf.InverseLerp(0.33f, 51f, mass)); 

        transform.localScale = Vector3.one * scale;

        // Muda a cor do Material apenas nesta instância
        if (starRenderer != null)
        {
            starRenderer.material.color = targetColor;
            starRenderer.material.SetColor("_BaseColor", targetColor);

            if (isPlanet)
            {
                // Só desativa a emissão se o PlanetAppearance não gerou uma EmissionMap
                // Se tiver EmissionMap (luzes noturnas), mantém a emissão ativa
                PlanetAppearance pa = GetComponent<PlanetAppearance>();
                bool hasEmissionMap = pa != null
                    && starRenderer.material.GetTexture("_EmissionMap") != null;

                if (hasEmissionMap)
                    starRenderer.material.EnableKeyword("_EMISSION");
                else
                {
                    starRenderer.material.DisableKeyword("_EMISSION");
                    starRenderer.material.SetColor("_EmissionColor", Color.black);
                }
            }
            else
            {
                // Emissão com intensidade fixa para não distorcer a cor
                // Multiplicar só o brilho (HDR) sem alterar o hue
                Color emissionColor = new Color(
                    targetColor.r * targetColor.r,
                    targetColor.g * targetColor.g,
                    targetColor.b * targetColor.b
                ) * 2.5f;
                starRenderer.material.EnableKeyword("_EMISSION");
                starRenderer.material.SetColor("_EmissionColor", emissionColor);
            }
        }

        // Muda a cor do Trail Renderer
        if (starTrail != null)
        {
            // Criar o gradiente
            Gradient gradient = new Gradient();
            gradient.SetKeys(
                new GradientColorKey[] { new GradientColorKey(targetColor, 0.0f), new GradientColorKey(targetColor, 1.0f) },
                new GradientAlphaKey[] { new GradientAlphaKey(0.8f, 0.0f), new GradientAlphaKey(0.0f, 1.0f) }
            );
            starTrail.colorGradient = gradient;

            if (transform.parent != null) 
            {
                // Valores para o Laboratório
                starTrail.startWidth = 0.5f; 
                starTrail.endWidth = 0.05f;
            }
            else 
            {
                // Valores para o Modo User Control
                starTrail.startWidth = 0.1f * scale;
                starTrail.endWidth = 0.02f;
            }
            
            // Força a atualização do material
            if (starTrail.material.HasProperty("_BaseColor"))
                starTrail.material.SetColor("_BaseColor", targetColor);
        }
    }

    // Animação de destruição do planeta — encolhe e fica brilhante antes de desaparecer
    public System.Collections.IEnumerator DestroyAnimation()
    {
        float duration = 0.4f;
        float elapsed  = 0f;

        Vector3 startScale = transform.localScale;
        Color   startColor = starRenderer != null ? starRenderer.material.color : Color.white;
        Color   flashColor = Color.white;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t  = elapsed / duration;

            // Encolhe para zero
            transform.localScale = Vector3.Lerp(startScale, Vector3.zero, t);

            // Flash de branco
            if (starRenderer != null)
            {
                Color c = Color.Lerp(startColor, flashColor, t);
                starRenderer.material.color = c;
                starRenderer.material.SetColor("_EmissionColor", c * 4f);
            }

            yield return null;
        }

        Destroy(gameObject);
    }

    // Pulso de brilho na estrela ao absorver um planeta
    public System.Collections.IEnumerator AbsorptionPulse()
    {
        if (starRenderer == null) yield break;

        float duration  = 0.5f;
        float elapsed   = 0f;
        Color baseColor = starRenderer.material.color;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t  = Mathf.Sin((elapsed / duration) * Mathf.PI); // curva de pulso suave

            starRenderer.material.SetColor("_EmissionColor", baseColor * Mathf.Lerp(3f, 8f, t));
            yield return null;
        }

        // Repõe a emissão normal
        starRenderer.material.SetColor("_EmissionColor", baseColor * 3f);
    }
}
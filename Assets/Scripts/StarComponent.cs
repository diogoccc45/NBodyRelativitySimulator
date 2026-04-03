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
        // 1.0 (500) -> blue-white (B/O-type) ------ COPIADO DE PAPER
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
            float t_planet = Mathf.InverseLerp(0.1f, 10f, mass); 
            if (t_planet < 0.5f)
                targetColor = Color.Lerp(new Color(0.7f, 0.4f, 0.3f), new Color(0.9f, 0.7f, 0.5f), t_planet * 2);
            else
                targetColor = Color.Lerp(new Color(0.9f, 0.7f, 0.5f), new Color(0.2f, 0.5f, 1.0f), (t_planet - 0.5f) * 2);
        }

        float scale = 0.5f + (mass / 100f);
        
        // Se for planeta, garantimos que ele é sempre mais pequeno que a estrela mínima
        if (isPlanet) scale = 0.3f; 

        transform.localScale = Vector3.one * scale;

        // Muda a cor do Material apenas nesta instância
        if (starRenderer != null)
        {
            starRenderer.material.color = targetColor;
            starRenderer.material.SetColor("_BaseColor", targetColor);

            if (isPlanet)
            {
                starRenderer.material.DisableKeyword("_EMISSION");
                starRenderer.material.SetColor("_EmissionColor", Color.black);
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
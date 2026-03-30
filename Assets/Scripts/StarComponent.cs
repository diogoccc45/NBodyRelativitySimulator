using UnityEngine;

public class StarComponent : MonoBehaviour
{
    public float mass = 1.0f;
    public Vector3 velocity;
    
    // Booleano para distinguir se este objeto é um planeta
    public bool isPlanet = false;

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
        // Criamos uma cor baseada na massa
        // Massa baixa (10) = Vermelho | Massa alta (500) = Ciano/Azul
        float t = Mathf.InverseLerp(10f, 500f, mass);
        Color targetColor = Color.Lerp(Color.red, Color.cyan, t);

        if (isPlanet)
        {
            float t_planet = Mathf.InverseLerp(0.1f, 10f, mass); 
            if (t_planet < 0.5f)
                targetColor = Color.Lerp(new Color(0.7f, 0.4f, 0.3f), new Color(0.9f, 0.7f, 0.5f), t_planet * 2);
            else
                targetColor = Color.Lerp(new Color(0.9f, 0.7f, 0.5f), new Color(0.2f, 0.5f, 1.0f), (t_planet - 0.5f) * 2);
        }

        float scale = Mathf.Lerp(0.5f, 2.5f, t);
        
        // Se for planeta, garantimos que ele é sempre mais pequeno que a estrela mínima
        if (isPlanet) scale = 0.3f; 

        transform.localScale = Vector3.one * scale;

        // Muda a cor do Material (M_Star_Base) apenas nesta instância
        if (starRenderer != null)
        {
            starRenderer.material.color = targetColor;

            // Se for planeta, desligamos o brilho (Emission)
            if (isPlanet)
            {
                starRenderer.material.DisableKeyword("_EMISSION");
            }
            else
            {
                starRenderer.material.EnableKeyword("_EMISSION");
                starRenderer.material.SetColor("_EmissionColor", targetColor * 3.0f);
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
}
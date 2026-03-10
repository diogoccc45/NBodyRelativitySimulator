using UnityEngine;

public class StarComponent : MonoBehaviour
{
    public float mass = 1.0f;
    public Vector3 velocity;
    
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

        // Muda a cor do Material (M_Star_Base) apenas nesta instância
        if (starRenderer != null)
        {
            starRenderer.material.color = targetColor;
            starRenderer.material.EnableKeyword("_EMISSION");
            starRenderer.material.SetColor("_EmissionColor", targetColor * 3.0f);
        }

        // Muda a cor do Trail Renderer
        if (starTrail != null)
        {
            starTrail.material = new Material(starTrail.material);
            starTrail.material.SetColor("_BaseColor", targetColor);
            starTrail.material.SetColor("_Color", targetColor);
            // Vou forçar o rasto a ser sólido e colorido
            starTrail.startColor = targetColor;
            starTrail.endColor = new Color(targetColor.r, targetColor.g, targetColor.b, 0f);
            starTrail.startWidth = 0.1f;
        }
    }
}
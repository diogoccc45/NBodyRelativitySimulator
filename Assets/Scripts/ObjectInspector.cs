using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;
public class ObjectInspector : MonoBehaviour
{
    [Header("Referências")]
    public StarSystemManager manager;

    [Header("Painel de UI")]
    public GameObject panel; // o painel inteiro (ativar/desativar)
    public TextMeshProUGUI nameText;
    public TextMeshProUGUI massText;
    public TextMeshProUGUI velocityText;
    public TextMeshProUGUI escapeVelText;
    public TextMeshProUGUI mergesText;
    public TextMeshProUGUI distanceText;
    public TextMeshProUGUI kineticText;
    public TextMeshProUGUI gravForceText;
    StarComponent target = null;

    void Start()
    {
        Hide();
    }

    void Update()
    {
        if (target == null) { Hide(); return; }
        UpdatePanel();
    }

    // Chamado pelo StarFollowCamera quando entra no modo Follow
    public void Show(StarComponent star)
    {
        target = star;
        if (panel != null) panel.SetActive(true);
    }

    // Chamado pelo StarFollowCamera quando sai do modo Follow 
    public void Hide()
    {
        target = null;
        if (panel != null) panel.SetActive(false);
    }

    // Fatores de Conversão para unidades reais
    // Massa: 1 unidade jogo = 0.004 M_sun  -> estrela 250 aproximadamente 1 Sol
    // Distância: 1 game unit = 0.1 UA
    // Velocidade: 1 game unit = 10 km s^-1
    // Energia: E_game * solar_mass_kg * (km s^-1 para m s^-1)^2 -> Joule
    const float massToSolar = 0.004f; // 250 unidades = 1.0 M_sun (Sol)
    const float distToAU = 0.1f; // UA
    const float velToKms = 10f; // km s^-1
    const double solarMassKg = 1.989e30; //M_sun em kg
    const double kmSToMs = 1000.0; // km s^-1
    const double newtonConversion = 6.674e-11; // G real em SI

    void UpdatePanel()
    {
        if (target == null) return;

        float mass = target.mass;
        float speed = target.velocity.magnitude;
        float radius = target.transform.localScale.x * 0.5f;

        // Converter para unidades reais
        float  massReal = target.isPlanet
            ? mass * 0.333f   // M_earth
            : mass * massToSolar; // M_sun
        float speedReal = speed * velToKms; // km s^-1
        float radiusReal = radius * distToAU; // UA

        // Velocidade de Escape: v = sqrt(2GM/r) - está em game units, depois converte no display
        float escapeVelGame = radius > 0
            ? Mathf.Sqrt(2f * manager.G * mass / radius)
            : 0f;
        float escapeVelReal = escapeVelGame * velToKms; // km s^-1

        // Energia Cinética: 1/2mv^2 em Joule
        // Converter massa para kg e velocidade para m s^-1
        double massKg = mass * massToSolar * solarMassKg;
        double speedMs = speed * velToKms * kmSToMs;
        double kinetic = 0.5 * massKg * speedMs * speedMs; // J

        // Estrela mais próxima e distância
        StarComponent nearest = manager.GetNearestStar(target.transform.position);
        float distGame = nearest != null
            ? Vector3.Distance(target.transform.position, nearest.transform.position)
            : 0f;
        float distReal = distGame * distToAU; // UA

        // Força Gravitacional total em Newton
        double gravForce = 0.0;
        List<StarComponent> stars = manager.GetStars();
        if (stars != null)
        {
            foreach (StarComponent sc in stars)
            {
                if (sc == null || sc == target) continue;
                float d = Vector3.Distance(target.transform.position, sc.transform.position);
                if (d < 0.01f) continue;
                double m1 = mass * massToSolar * solarMassKg;
                double m2 = sc.mass * massToSolar * solarMassKg;
                double dM = d * distToAU * 1.496e11; // AU para metros
                gravForce += newtonConversion * m1 * m2 / (dM * dM); // N
            }
        }

        // Atualiza os textos
        string type = target.isPlanet ? "Planet" : "Star";
        if (nameText != null) nameText.text = $"{type}: {target.gameObject.name}";
        if (massText != null) massText.text = $"Mass: {massReal:F3} M_sun";
        if (velocityText != null) velocityText.text = $"Velocity: {speedReal:F1} km/s";
        if (escapeVelText!= null) escapeVelText.text = $"Escape vel.: {escapeVelReal:F1} km/s";
        if (mergesText != null) mergesText.text = $"Merges: {target.mergeCount}";
        if (distanceText != null) distanceText.text = nearest != null
            ? $"Nearest star dist.: {distReal:F2} AU"
            : "Nearest star dist.: N/A";
        if (kineticText != null) kineticText.text = $"Kinetic energy: {FormatScientific(kinetic)} J";
        if (gravForceText!= null) gravForceText.text= $"Grav. force: {FormatScientific(gravForce)} N";
    }

    // Formata um número grande em notação científica (ex: 3.2 × 10^34)
    string FormatScientific(double value)
    {
        if (value <= 0) return "0";
        int exp = (int)System.Math.Floor(System.Math.Log10(value));
        double mantissa = value / System.Math.Pow(10, exp);
        return $"{mantissa:F2} × 10^{exp}";
    }
}
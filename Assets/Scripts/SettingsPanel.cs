using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;
public class SettingsPanel : MonoBehaviour
{
    [Header("Referências")]
    public PlanetCollision planetCollision;
    public CanvasGroup panelCanvasGroup; // CanvasGroup no SettingsPanelRoot para o fade
    public GameObject gearButton;

    [Header("Modo de Colisão")]
    public TMP_Dropdown collisionModeDropdown; // Fragment by Mass / Fragment All / Bounce

    [Header("Parâmetros — Fragmentação")]
    public Slider massRatioSlider;
    public TextMeshProUGUI massRatioText;
    public Slider fragmentCountSlider;
    public TextMeshProUGUI fragmentCountText;

    [Header("Parâmetros — Ricochete")]
    public Slider restitutionSlider;
    public TextMeshProUGUI restitutionText;

    [Header("Grupos de Parâmetros")]
    public GameObject fragmentationParams;
    public GameObject bounceParams;

    [Header("Animação")]
    public float fadeSpeed = 6f;
    public float scaleOnOpen  = 1.05f;

    [Header("Tooltip Fragment By Mass")]
    public GameObject tooltipPanel; // painel do tooltip (começa inativo)
    public TextMeshProUGUI tooltipText; // texto dentro do tooltip
    public GameObject infoButton; // botão "i" ao lado do mass ratio slider
    public TextMeshProUGUI  warningText; // ⚠ ao lado do slider (cor laranja)

    [Header("Referência Extra")]
    public MouseInteraction mouseInteraction;

    private bool isOpen = false;

    void Start()
    {
        // Inicializa o CanvasGroup — painel começa invisível e não-interativo
        if (panelCanvasGroup != null)
        {
            panelCanvasGroup.alpha = 0f;
            panelCanvasGroup.interactable = false;
            panelCanvasGroup.blocksRaycasts = false;
            panelCanvasGroup.gameObject.SetActive(false);
        }

        // Configura os sliders com os valores atuais
        if (planetCollision != null)
        {
            if (massRatioSlider != null)
            {
                massRatioSlider.minValue = 1.2f;
                massRatioSlider.maxValue = 5.0f;
                massRatioSlider.value = planetCollision.massRatioThreshold;
                massRatioSlider.onValueChanged.AddListener(OnMassRatioChanged);
            }
            if (fragmentCountSlider != null)
            {
                fragmentCountSlider.minValue = 0.2f;
                fragmentCountSlider.maxValue = 1.0f;
                fragmentCountSlider.value = planetCollision.fragmentationBeta;
                fragmentCountSlider.onValueChanged.AddListener(OnFragmentBetaChanged);
            }
            if (restitutionSlider != null)
            {
                restitutionSlider.minValue = 0.1f;
                restitutionSlider.maxValue = 1.0f;
                restitutionSlider.value = planetCollision.restitutionCoeff;
                restitutionSlider.onValueChanged.AddListener(OnRestitutionChanged);
            }
        }

        // Configura o dropdown com as opções de modo
        if (collisionModeDropdown != null)
        {
            collisionModeDropdown.ClearOptions();
            collisionModeDropdown.AddOptions(new System.Collections.Generic.List<string>
            {
                "Fragment by Mass",
                "Fragment All",
                "Bounce"
            });
            // Define o valor inicial de acordo com o modo atual
            collisionModeDropdown.value = (int)(planetCollision != null
                ? planetCollision.mode
                : PlanetCollision.CollisionMode.FragmentByMass);
            collisionModeDropdown.onValueChanged.AddListener(OnModeDropdownChanged);
        }

        UpdateUI();

        // Tooltip começa escondido
        if (tooltipPanel != null) tooltipPanel.SetActive(false);
        if (warningText  != null) warningText.gameObject.SetActive(false);
    }

    // Chamado pelo botão de engrenagem
    public void TogglePanel()
    {
        isOpen = !isOpen;

        if (isOpen)
        {
            // Ativa o GameObject ANTES de iniciar a Coroutine
            // (Coroutines não funcionam em GameObjects inativos)
            if (panelCanvasGroup != null)
                panelCanvasGroup.gameObject.SetActive(true);
            StopAllCoroutines();
            StartCoroutine(FadeIn());
        }
        else
        {
            StopAllCoroutines();
            StartCoroutine(FadeOut());
        }
    }

    IEnumerator FadeIn()
    {
        if (panelCanvasGroup == null) yield break;

        panelCanvasGroup.gameObject.SetActive(true);
        panelCanvasGroup.interactable   = true;
        panelCanvasGroup.blocksRaycasts = true;

        // Pop-in: escala de 0.85 → 1 + fade alpha 0 → 1
        RectTransform rt = panelCanvasGroup.GetComponent<RectTransform>();
        float elapsed = 0f;
        float duration = 1f / fadeSpeed;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            float ease = 1f - Mathf.Pow(1f - t, 3f); // ease out cubic

            panelCanvasGroup.alpha = ease;
            if (rt != null)
            {
                float scale = Mathf.Lerp(0.88f, 1f, ease);
                rt.localScale = Vector3.one * scale;
            }
            yield return null;
        }

        panelCanvasGroup.alpha = 1f;
        if (rt != null) rt.localScale = Vector3.one;
    }

    IEnumerator FadeOut()
    {
        if (panelCanvasGroup == null) yield break;

        panelCanvasGroup.interactable = false;
        panelCanvasGroup.blocksRaycasts = false;

        RectTransform rt = panelCanvasGroup.GetComponent<RectTransform>();
        float elapsed = 0f;
        float duration = 1f / fadeSpeed;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            float ease = Mathf.Pow(t, 2f); // ease in quadratic

            panelCanvasGroup.alpha = 1f - ease;
            if (rt != null)
            {
                float scale = Mathf.Lerp(1f, 0.92f, ease);
                rt.localScale = Vector3.one * scale;
            }
            yield return null;
        }

        panelCanvasGroup.alpha = 0f;
        panelCanvasGroup.gameObject.SetActive(false);
    }

    // Chamado pelo dropdown quando o utilizador muda o modo
    void OnModeDropdownChanged(int index)
    {
        SetMode((PlanetCollision.CollisionMode)index);
    }

    // Muda o modo de colisão e atualiza a UI
    void SetMode(PlanetCollision.CollisionMode newMode)
    {
        if (planetCollision != null)
            planetCollision.mode = newMode;
        UpdateUI();

        // Tooltip começa escondido
        if (tooltipPanel != null) tooltipPanel.SetActive(false);
        if (warningText != null) warningText.gameObject.SetActive(false);
    }

    // Atualiza visibilidade dos grupos de parâmetros conforme o modo selecionado
    void UpdateUI()
    {
        if (planetCollision == null) return;

        PlanetCollision.CollisionMode current = planetCollision.mode;

        // Mostra só os parâmetros relevantes para o modo atual
        bool isFragment = current == PlanetCollision.CollisionMode.FragmentByMass
                       || current == PlanetCollision.CollisionMode.FragmentAll;
        bool isBounce   = current == PlanetCollision.CollisionMode.Bounce;

        if (fragmentationParams != null) fragmentationParams.SetActive(isFragment);
        if (bounceParams        != null) bounceParams.SetActive(isBounce);

        // Esconde o slider de rácio de massa no modo FragmentAll (não é relevante)
        if (massRatioSlider != null)
            massRatioSlider.transform.parent.gameObject.SetActive(
                current == PlanetCollision.CollisionMode.FragmentByMass);

        UpdateTexts();
    }

    void UpdateTexts()
    {
        if (planetCollision == null) return;

        if (massRatioText != null)
            massRatioText.text = $"Mass ratio: {planetCollision.massRatioThreshold:F1}x";

        if (fragmentCountText != null)
        {
            string desc = planetCollision.fragmentationBeta < 0.4f ? "Few, large"
                        : planetCollision.fragmentationBeta < 0.7f ? "Moderate"
                        : "Many, small";
            fragmentCountText.text = $"Fragments: {desc}";
        }

        if (restitutionText != null)
        {
            string desc = planetCollision.restitutionCoeff < 0.3f ? "Inelastic"
                        : planetCollision.restitutionCoeff < 0.7f ? "Partial"
                        : "Elastic";
            restitutionText.text = $"Bounce: {desc} ({planetCollision.restitutionCoeff:F1})";
        }

        // Atualiza o aviso e o tooltip do Fragment By Mass
        if (planetCollision.mode == PlanetCollision.CollisionMode.FragmentByMass)
            UpdateFragmentWarning();
    }

    // Analisa os planetas existentes e atualiza o aviso + conteúdo do tooltip
    void UpdateFragmentWarning()
    {
        if (planetCollision == null || planetCollision.manager == null) return;

        var stars = planetCollision.manager.GetStars();
        var planets = stars != null
            ? stars.FindAll(s => s != null && s.isPlanet)
            : new System.Collections.Generic.List<StarComponent>();

        float threshold = planetCollision.massRatioThreshold;
        bool  anyWillBounce = false;
        bool  anyWillFrag = false;

        System.Text.StringBuilder sb = new System.Text.StringBuilder();
        sb.AppendLine("<b>Fragment By Mass — Analysis</b>");
        sb.AppendLine($"Threshold: <b>{threshold:F1}x</b>");

        if (planets.Count < 2)
        {
            sb.AppendLine("No planet pairs in scene yet.");
            sb.AppendLine("Create at least 2 planets to see collision predictions.");
        }
        else
        {
            sb.AppendLine("<b>Current planet pairs:</b>");
            for (int i = 0; i < planets.Count; i++)
            {
                for (int j = i + 1; j < planets.Count; j++)
                {
                    float larger = Mathf.Max(planets[i].mass, planets[j].mass);
                    float smaller = Mathf.Min(planets[i].mass, planets[j].mass);
                    float ratio = larger / Mathf.Max(smaller, 0.01f);
                    bool  frags = ratio >= threshold;

                    if (frags) anyWillFrag = true;
                    else anyWillBounce = true;

                    // Converte massas internas para M_Earth para exibição
                    float massA = planets[i].mass * 0.333f;
                    float massB = planets[j].mass * 0.333f;
                    string icon = frags ? "<color=#00ff88>✓ FRAGMENT</color>"
                                        : "<color=#ff8800>⚠ BOUNCE</color>";

                    sb.AppendLine($"• {planets[i].gameObject.name} ({massA:F1} M⊕)" +
                                  $" vs {planets[j].gameObject.name} ({massB:F1} M⊕)" +
                                  $" — ratio <b>{ratio:F1}x</b>  {icon}");
                }
            }

            sb.AppendLine();

            if (anyWillBounce && !anyWillFrag)
            {
                float minRatio = float.MaxValue;
                for (int i = 0; i < planets.Count; i++)
                    for (int j = i + 1; j < planets.Count; j++)
                    {
                        float r = Mathf.Max(planets[i].mass, planets[j].mass)
                                / Mathf.Max(Mathf.Min(planets[i].mass, planets[j].mass), 0.01f);
                        if (r < minRatio) minRatio = r;
                    }
                sb.AppendLine($"<color=#ff8800>All pairs will BOUNCE with threshold {threshold:F1}x.</color>");
                sb.AppendLine($"Lower threshold below <b>{minRatio:F1}x</b> to enable fragmentation.");
                sb.AppendLine("<i>Tip: Switch to 'Fragment All' to ignore mass ratios.</i>");
            }
            else if (anyWillBounce)
            {
                sb.AppendLine("<color=#ff8800>Some pairs will bounce — lower the threshold to fragment them.</color>");
            }
            else
            {
                sb.AppendLine("<color=#00ff88>All pairs will fragment! ✓</color>");
            }
        }

        // Atualiza o tooltip
        if (tooltipText != null)
            tooltipText.text = sb.ToString();

        // Mostra/esconde o aviso ⚠ ao lado do slider
        if (warningText != null)
        {
            warningText.gameObject.SetActive(anyWillBounce);
            warningText.text  = "⚠";
            warningText.color = new Color(1f, 0.55f, 0f); // laranja
        }

        // Mostra o botão "i" só no modo Fragment By Mass
        if (infoButton != null)
            infoButton.SetActive(planetCollision.mode == PlanetCollision.CollisionMode.FragmentByMass);
    }

    // Chamado pelo EventTrigger do botão "i" — PointerEnter
    public void ShowTooltip()
    {
        UpdateFragmentWarning(); // atualiza antes de mostrar
        if (tooltipPanel != null) tooltipPanel.SetActive(true);
    }

    // Chamado pelo EventTrigger do botão "i" — PointerExit
    public void HideTooltip()
    {
        if (tooltipPanel != null) tooltipPanel.SetActive(false);
    }



    void OnMassRatioChanged(float value)
    {
        if (planetCollision != null) planetCollision.massRatioThreshold = value;
        UpdateTexts();
    }

    void OnFragmentBetaChanged(float value)
    {
        if (planetCollision != null) planetCollision.fragmentationBeta = value;
        UpdateTexts();
    }

    void OnRestitutionChanged(float value)
    {
        if (planetCollision != null) planetCollision.restitutionCoeff = value;
        UpdateTexts();
    }
}
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;
using System.Collections;
using UnityEngine.SceneManagement;

// Menu principal — v3
// Fundo 100% UI:
//   - Gradiente escuro via painéis sobrepostos
//   - Estrelas como pequenos RawImage quadrados animados
//   - Nebulosa animada via shader NebulaClouds_UI (com domain warp + vórtice)
//   - Estrelas cadentes com trilho (ShootingStars)
// Layout centrado e proporcional a qualquer resolução
public class MainMenuBuilder : MonoBehaviour
{
    public MainMenuController controller;

    static readonly Color BG_DARK    = new Color(0.015f, 0.020f, 0.055f, 1f);
    static readonly Color BG_MID     = new Color(0.030f, 0.015f, 0.075f, 1f);
    static readonly Color ACCENT     = new Color(0.30f,  0.65f,  1.00f,  1f);
    static readonly Color ACCENT2    = new Color(0.60f,  0.28f,  0.95f,  1f);
    static readonly Color BTN_NORMAL = new Color(0.06f,  0.10f,  0.22f,  0.90f);
    static readonly Color BTN_HOVER  = new Color(0.12f,  0.25f,  0.50f,  0.95f);
    static readonly Color BTN_PRESS  = new Color(0.22f,  0.45f,  0.85f,  1.00f);
    static readonly Color TEXT_MAIN  = new Color(0.90f,  0.94f,  1.00f,  1f);
    static readonly Color TEXT_DIM   = new Color(0.50f,  0.65f,  0.85f,  1f);

    Canvas rootCanvas;
    public Sprite quitIcon;   // arrasta o Lock.png aqui no Inspector

    void Start()
    {
        if (controller == null) controller = GetComponent<MainMenuController>();
        BuildMenu();
    }

    void BuildMenu()
    {
        // Canvas raiz
        GameObject cGO    = new GameObject("MenuCanvas");
        rootCanvas        = cGO.AddComponent<Canvas>();
        rootCanvas.renderMode  = RenderMode.ScreenSpaceOverlay;
        rootCanvas.sortingOrder = 10;

        CanvasScaler cs        = cGO.AddComponent<CanvasScaler>();
        cs.uiScaleMode         = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        cs.referenceResolution = new Vector2(1920, 1080);
        cs.matchWidthOrHeight  = 0.5f;
        cGO.AddComponent<GraphicRaycaster>();

        if (FindFirstObjectByType<EventSystem>() == null)
        {
            var es = new GameObject("EventSystem");
            es.AddComponent<EventSystem>();
            es.AddComponent<StandaloneInputModule>();
        }

        // ── Fundo ─────────────────────────────────────────────────────
        BuildBackground(cGO.transform);

        // ── Conteúdo central ──────────────────────────────────────────
        // Painel invisível centrado — 700x580
        GameObject center = MakeRect("Center", cGO.transform);
        RectTransform cR  = center.GetComponent<RectTransform>();
        cR.anchorMin = cR.anchorMax = new Vector2(0.5f, 0.5f);
        cR.pivot     = new Vector2(0.5f, 0.5f);
        cR.sizeDelta = new Vector2(700, 580);
        cR.anchoredPosition = Vector2.zero;

        // ── Título ────────────────────────────────────────────────────
        // MakeText agora devolve o GameObject para podermos animar o título
        GameObject titleGO = MakeText(center.transform, "N-BODY SIMULATOR",
            new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1f),
            new Vector2(0, -20), new Vector2(0, 90),
            60, FontStyles.Bold, TEXT_MAIN, 6f);
        StartCoroutine(PulseTitle(titleGO.GetComponent<TextMeshProUGUI>()));

        MakeText(center.transform, "EDUCATION EDITION",
            new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1f),
            new Vector2(0, -118), new Vector2(0, 32),
            16, FontStyles.Normal, ACCENT, 16f);

        // Linha separadora
        GameObject sep = MakeRect("Sep", center.transform);
        Image sepI = sep.AddComponent<Image>();
        sepI.color = new Color(ACCENT.r, ACCENT.g, ACCENT.b, 0.40f);
        RectTransform sepR = sep.GetComponent<RectTransform>();
        sepR.anchorMin = new Vector2(0.15f, 1f); sepR.anchorMax = new Vector2(0.85f, 1f);
        sepR.pivot = new Vector2(0.5f, 1f);
        sepR.anchoredPosition = new Vector2(0, -158);
        sepR.sizeDelta = new Vector2(0, 1.5f);

        // ── Botões ────────────────────────────────────────────────────
        var scenes = new (string label, string icon, System.Action act)[]
        {
            ("Newton Aleatório",   "I",  () => controller.LoadNewton()),
            ("Laboratório Manual", "II", () => controller.LoadLaboratorio()),
            ("Relatividade Geral", "III",() => controller.LoadRelatividade()),
        };

        for (int i = 0; i < scenes.Length; i++)
            MakeButton(center.transform, scenes[i].label, scenes[i].icon,
                       -175f - i * 96f, scenes[i].act);

        // ── Botão Sair — canto inferior esquerdo ─────────────────────
        GameObject quitBtn = MakeRect("Btn_Sair", cGO.transform);
        RectTransform qR = quitBtn.GetComponent<RectTransform>();
        qR.anchorMin = qR.anchorMax = new Vector2(0f, 0f);
        qR.pivot     = new Vector2(0f, 0f);
        qR.anchoredPosition = new Vector2(20f, 20f);
        qR.sizeDelta = new Vector2(200, 52);

        Image qBg = quitBtn.AddComponent<Image>();
        qBg.color = BTN_NORMAL;

        // Borda esquerda
        GameObject qBord = MakeRect("Bar", quitBtn.transform);
        Image qBI = qBord.AddComponent<Image>();
        qBI.color = ACCENT;
        RectTransform qBdR = qBord.GetComponent<RectTransform>();
        qBdR.anchorMin = Vector2.zero; qBdR.anchorMax = new Vector2(0,1);
        qBdR.pivot = new Vector2(0,0.5f);
        qBdR.anchoredPosition = Vector2.zero;
        qBdR.sizeDelta = new Vector2(4,0);

        // Ícone
        GameObject qIc = MakeRect("Icon", quitBtn.transform);
        Image qIcImg = qIc.AddComponent<Image>();
        if (quitIcon != null)
        {
            qIcImg.sprite = quitIcon;
            qIcImg.preserveAspect = true;
            qIcImg.color = new Color(0.30f, 0.65f, 1.00f, 0.85f);
        }
        RectTransform qIcR = qIc.GetComponent<RectTransform>();
        qIcR.anchorMin = new Vector2(0,0); qIcR.anchorMax = new Vector2(0,1);
        qIcR.pivot = new Vector2(0,0.5f);
        qIcR.anchoredPosition = new Vector2(10,0);
        qIcR.sizeDelta = new Vector2(28,0);

        // Label
        GameObject qLb = MakeRect("Lbl", quitBtn.transform);
        TextMeshProUGUI qLbT = qLb.AddComponent<TextMeshProUGUI>();
        qLbT.text = "SAIR"; qLbT.fontSize = 15; qLbT.fontStyle = FontStyles.Bold;
        qLbT.color = TEXT_MAIN; qLbT.characterSpacing = 2f;
        qLbT.alignment = TextAlignmentOptions.MidlineLeft;
        RectTransform qLbR = qLb.GetComponent<RectTransform>();
        qLbR.anchorMin = new Vector2(0,0); qLbR.anchorMax = new Vector2(1,1);
        qLbR.pivot = new Vector2(0,0.5f);
        qLbR.anchoredPosition = new Vector2(46,0);
        qLbR.sizeDelta = new Vector2(-50,0);

        Button qB = quitBtn.AddComponent<Button>();
        qB.targetGraphic = qBg;
        ColorBlock qCb = ColorBlock.defaultColorBlock;
        qCb.normalColor = BTN_NORMAL; qCb.highlightedColor = BTN_HOVER;
        qCb.pressedColor = BTN_PRESS; qCb.fadeDuration = 0.10f;
        qB.colors = qCb;
        qB.onClick.AddListener(() => controller.QuitApplication());

        var qEt = quitBtn.AddComponent<EventTrigger>();
        AddTrigger(qEt, EventTriggerType.PointerEnter, _ => qBI.color = ACCENT2);
        AddTrigger(qEt, EventTriggerType.PointerExit,  _ => qBI.color = ACCENT);

        // ── Rodapé ────────────────────────────────────────────────────
        GameObject foot = MakeRect("Footer", cGO.transform);
        TextMeshProUGUI ft = foot.AddComponent<TextMeshProUGUI>();
        ft.text = "© 2026  ·  Diogo Carvalho";
        ft.fontSize = 13; ft.color = new Color(0.45f, 0.60f, 0.80f, 0.50f);
        ft.alignment = TextAlignmentOptions.Center;
        RectTransform fR = foot.GetComponent<RectTransform>();
        fR.anchorMin = new Vector2(0,0); fR.anchorMax = new Vector2(1,0);
        fR.pivot = new Vector2(0.5f,0);
        fR.anchoredPosition = new Vector2(0,20);
        fR.sizeDelta = new Vector2(0,26);

        // Animação de entrada
        StartCoroutine(FadeIn(center));
    }

    // ── FUNDO ─────────────────────────────────────────────────────────
    void BuildBackground(Transform parent)
    {
        // Camada base — azul muito escuro
        GameObject bg = MakeRect("BG", parent);
        Image bgI = bg.AddComponent<Image>();
        bgI.color = BG_DARK;
        StretchFull(bg);

        // Gradiente arroxado em baixo — opacidade reduzida para não criar faixa visível
        GameObject grad = MakeRect("Grad", parent);
        Image gI = grad.AddComponent<Image>();
        gI.color = new Color(0.04f, 0.01f, 0.08f, 0.35f);
        RectTransform gR = grad.GetComponent<RectTransform>();
        gR.anchorMin = Vector2.zero; gR.anchorMax = new Vector2(1f, 0.45f);
        gR.offsetMin = gR.offsetMax = Vector2.zero;

        // Glow central removido — era a faixa azul horizontal visível atrás dos botões

        // ── Camada 1: Estrelas (mais atrás) ───────────────────────────
        BuildStars(parent);

        // ── Camada 2: Estrelas cadentes ────────────────────────────────
        GameObject ssRoot = MakeRect("ShootingStars", parent);
        StretchFull(ssRoot);
        ssRoot.AddComponent<ShootingStars>();

        // ── Camada 3: Nebulosas (à frente de tudo no fundo) ───────────

        // Nebulosa esquerda — azul-violeta
        MakeNebula(parent, new Vector2(0.15f, 0.60f),
                   new Vector2(560, 560),
                   new Color(0.08f, 0.15f, 0.50f, 0.18f),
                   colorIndex: 0.0f, sizeNorm: 0.8f);

        // Nebulosa direita — arroxada
        MakeNebula(parent, new Vector2(0.82f, 0.35f),
                   new Vector2(480, 480),
                   new Color(0.35f, 0.08f, 0.45f, 0.14f),
                   colorIndex: 0.45f, sizeNorm: 0.7f);

        // Nebulosa centro-baixo — azul frio
        MakeNebula(parent, new Vector2(0.50f, 0.18f),
                   new Vector2(640, 360),
                   new Color(0.05f, 0.08f, 0.35f, 0.12f),
                   colorIndex: 0.9f, sizeNorm: 0.9f);

        // Nebulosa extra topo-direito — quente
        MakeNebula(parent, new Vector2(0.78f, 0.80f),
                   new Vector2(380, 380),
                   new Color(0.30f, 0.10f, 0.15f, 0.10f),
                   colorIndex: 0.55f, sizeNorm: 0.85f);
    }

    void MakeNebula(Transform parent, Vector2 anchorPos, Vector2 size, Color color,
                    float colorIndex = 0f, float sizeNorm = 0.5f)
    {
        GameObject n = MakeRect("Nebula", parent);
        RectTransform nR = n.GetComponent<RectTransform>();
        nR.anchorMin = nR.anchorMax = anchorPos;
        nR.pivot = new Vector2(0.5f, 0.5f);
        nR.sizeDelta = size;
        nR.anchoredPosition = Vector2.zero;
        n.transform.localScale = new Vector3(1f, 0.60f, 1f);

        // Tenta usar o shader NebulaClouds_UI — se não existir usa Image simples
        Shader nebulaShader = Shader.Find("Custom/NebulaClouds_UI");
        if (nebulaShader != null)
        {
            RawImage ri = n.AddComponent<RawImage>();
            Material mat = new Material(nebulaShader);
            mat.SetFloat("_ColorIndex",    colorIndex);
            mat.SetFloat("_SizeNorm",      sizeNorm);
            mat.SetFloat("_Brightness",    1.1f);
            mat.SetFloat("_NoiseScale",    5.5f);
            mat.SetFloat("_NoiseStrength", 0.5f);
            mat.SetFloat("_FalloffPower",  1.2f);
            // Novos parâmetros de animação do shader v3
            mat.SetFloat("_AnimSpeed",     0.18f);
            mat.SetFloat("_FilamentStr",   0.40f);
            mat.SetFloat("_VortexStr",     0.22f);
            mat.SetFloat("_PulseSpeed",    0.45f);
            ri.material = mat;
            ri.color = Color.white;
        }
        else
        {
            // Fallback — Image semitransparente se o shader não estiver no projeto
            Image nI = n.AddComponent<Image>();
            nI.color = color;
        }
    }

    void BuildStars(Transform parent)
    {
        GameObject starRoot = MakeRect("Stars", parent);
        StretchFull(starRoot);

        StarField sf = starRoot.AddComponent<StarField>();
        sf.parent    = starRoot.GetComponent<RectTransform>();
        sf.count     = 180;
    }

    // ── BOTÃO ─────────────────────────────────────────────────────────
    void MakeButton(Transform parent, string label, string icon, float posY, System.Action onClick)
    {
        GameObject btn = MakeRect("Btn_" + label, parent);
        RectTransform bR = btn.GetComponent<RectTransform>();
        bR.anchorMin = bR.anchorMax = new Vector2(0.5f, 1f);
        bR.pivot     = new Vector2(0.5f, 1f);
        bR.anchoredPosition = new Vector2(0, posY);
        bR.sizeDelta = new Vector2(560, 72);

        Image bg = btn.AddComponent<Image>();
        bg.color = BTN_NORMAL;

        // Borda esquerda
        GameObject bord = MakeRect("Bar", btn.transform);
        Image bI = bord.AddComponent<Image>();
        bI.color = ACCENT;
        RectTransform bdR = bord.GetComponent<RectTransform>();
        bdR.anchorMin = Vector2.zero; bdR.anchorMax = new Vector2(0,1);
        bdR.pivot = new Vector2(0, 0.5f);
        bdR.anchoredPosition = Vector2.zero;
        bdR.sizeDelta = new Vector2(4, 0);

        // Ícone
        GameObject ic = MakeRect("Icon", btn.transform);
        TextMeshProUGUI icT = ic.AddComponent<TextMeshProUGUI>();
        icT.text = icon; icT.fontSize = 13; icT.fontStyle = FontStyles.Bold;
        icT.color = new Color(0.30f, 0.65f, 1.00f, 0.7f);
        icT.alignment = TextAlignmentOptions.Center; icT.characterSpacing = 1f;
        RectTransform icR = ic.GetComponent<RectTransform>();
        icR.anchorMin = new Vector2(0,0); icR.anchorMax = new Vector2(0,1);
        icR.pivot = new Vector2(0,0.5f);
        icR.anchoredPosition = new Vector2(16,0);
        icR.sizeDelta = new Vector2(50,0);

        // Label
        GameObject lb = MakeRect("Lbl", btn.transform);
        TextMeshProUGUI lbT = lb.AddComponent<TextMeshProUGUI>();
        lbT.text = label.ToUpper();
        lbT.fontSize = 18; lbT.fontStyle = FontStyles.Bold;
        lbT.color = TEXT_MAIN; lbT.characterSpacing = 2f;
        lbT.alignment = TextAlignmentOptions.MidlineLeft;
        RectTransform lbR = lb.GetComponent<RectTransform>();
        lbR.anchorMin = new Vector2(0,0); lbR.anchorMax = new Vector2(1,1);
        lbR.pivot = new Vector2(0,0.5f);
        lbR.anchoredPosition = new Vector2(76,0);
        lbR.sizeDelta = new Vector2(-100,0);

        // Seta
        GameObject ar = MakeRect("Arrow", btn.transform);
        TextMeshProUGUI arT = ar.AddComponent<TextMeshProUGUI>();
        arT.text = "→"; arT.fontSize = 24;
        arT.color = new Color(ACCENT.r, ACCENT.g, ACCENT.b, 0.75f);
        arT.alignment = TextAlignmentOptions.MidlineRight;
        RectTransform arR = ar.GetComponent<RectTransform>();
        arR.anchorMin = new Vector2(1,0); arR.anchorMax = new Vector2(1,1);
        arR.pivot = new Vector2(1,0.5f);
        arR.anchoredPosition = new Vector2(-16,0);
        arR.sizeDelta = new Vector2(40,0);

        Button b = btn.AddComponent<Button>();
        b.targetGraphic = bg;
        ColorBlock cb = ColorBlock.defaultColorBlock;
        cb.normalColor = BTN_NORMAL; cb.highlightedColor = BTN_HOVER;
        cb.pressedColor = BTN_PRESS; cb.fadeDuration = 0.10f;
        b.colors = cb;
        b.onClick.AddListener(() => onClick());

        // Hover — anima seta e borda
        var et = btn.AddComponent<EventTrigger>();
        AddTrigger(et, EventTriggerType.PointerEnter, _ => {
            StartCoroutine(MoveX(arR, -6f, 0.14f));
            bI.color = ACCENT2;
        });
        AddTrigger(et, EventTriggerType.PointerExit, _ => {
            StartCoroutine(MoveX(arR, -16f, 0.14f));
            bI.color = ACCENT;
        });
    }

    // ── HELPERS ───────────────────────────────────────────────────────
    // Agora devolve o GameObject para permitir animar o texto após criação
    GameObject MakeText(Transform parent, string text,
                  Vector2 ancMin, Vector2 ancMax, Vector2 pivot,
                  Vector2 ancPos, Vector2 sizeDelta,
                  float size, FontStyles style, Color color, float charSpacing)
    {
        GameObject go = MakeRect(text, parent);
        TextMeshProUGUI t = go.AddComponent<TextMeshProUGUI>();
        t.text = text; t.fontSize = size; t.fontStyle = style;
        t.color = color; t.characterSpacing = charSpacing;
        t.alignment = TextAlignmentOptions.Center;
        t.textWrappingMode = TextWrappingModes.NoWrap;
        RectTransform r = go.GetComponent<RectTransform>();
        r.anchorMin = ancMin; r.anchorMax = ancMax; r.pivot = pivot;
        r.anchoredPosition = ancPos; r.sizeDelta = sizeDelta;
        return go;
    }

    static GameObject MakeRect(string name, Transform parent)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.AddComponent<RectTransform>();
        return go;
    }

    static void StretchFull(GameObject go)
    {
        RectTransform r = go.GetComponent<RectTransform>();
        r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one;
        r.offsetMin = r.offsetMax = Vector2.zero;
    }

    static void AddTrigger(EventTrigger et, EventTriggerType type,
                           UnityEngine.Events.UnityAction<BaseEventData> action)
    {
        var e = new EventTrigger.Entry { eventID = type };
        e.callback.AddListener(action);
        et.triggers.Add(e);
    }

    IEnumerator MoveX(RectTransform rt, float targetX, float dur)
    {
        float startX = rt.anchoredPosition.x, e = 0f;
        while (e < dur)
        {
            e += Time.deltaTime;
            float t = e / dur; t = t * t * (3f - 2f * t);
            rt.anchoredPosition = new Vector2(Mathf.Lerp(startX, targetX, t), rt.anchoredPosition.y);
            yield return null;
        }
        rt.anchoredPosition = new Vector2(targetX, rt.anchoredPosition.y);
    }

    IEnumerator FadeIn(GameObject go)
    {
        CanvasGroup cg = go.AddComponent<CanvasGroup>();
        cg.alpha = 0f;
        RectTransform rt = go.GetComponent<RectTransform>();
        Vector2 end = rt.anchoredPosition;
        rt.anchoredPosition = end - new Vector2(0, 24f);
        yield return new WaitForSeconds(0.08f);
        float e = 0f, dur = 0.55f;
        while (e < dur)
        {
            e += Time.deltaTime;
            float t = 1f - Mathf.Pow(1f - e / dur, 3f);
            cg.alpha = t;
            rt.anchoredPosition = Vector2.Lerp(end - new Vector2(0,24f), end, t);
            yield return null;
        }
        cg.alpha = 1f; rt.anchoredPosition = end;
    }

    // Botão com sprite no ícone (ex: botão Sair com Lock.png)
    void MakeButtonWithSprite(Transform parent, string label, Sprite icon,
                              float posY, System.Action onClick)
    {
        GameObject btn = MakeRect("Btn_" + label, parent);
        RectTransform bR = btn.GetComponent<RectTransform>();
        bR.anchorMin = bR.anchorMax = new Vector2(0.5f, 1f);
        bR.pivot     = new Vector2(0.5f, 1f);
        bR.anchoredPosition = new Vector2(0, posY);
        bR.sizeDelta = new Vector2(560, 72);

        Image bg = btn.AddComponent<Image>();
        bg.color = BTN_NORMAL;

        // Borda esquerda
        GameObject bord = MakeRect("Bar", btn.transform);
        Image bI = bord.AddComponent<Image>();
        bI.color = ACCENT;
        RectTransform bdR = bord.GetComponent<RectTransform>();
        bdR.anchorMin = Vector2.zero; bdR.anchorMax = new Vector2(0,1);
        bdR.pivot = new Vector2(0,0.5f);
        bdR.anchoredPosition = Vector2.zero;
        bdR.sizeDelta = new Vector2(4,0);

        // Ícone sprite
        GameObject ic = MakeRect("Icon", btn.transform);
        Image icImg = ic.AddComponent<Image>();
        if (icon != null)
        {
            icImg.sprite = icon;
            icImg.preserveAspect = true;
            icImg.color = new Color(0.30f, 0.65f, 1.00f, 0.85f);
        }
        else
        {
            // Fallback texto se o sprite não estiver atribuído
            Destroy(icImg);
            var icT = ic.AddComponent<TextMeshProUGUI>();
            icT.text = "X"; icT.fontSize = 18; icT.fontStyle = FontStyles.Bold;
            icT.color = new Color(0.30f, 0.65f, 1.00f, 0.7f);
            icT.alignment = TextAlignmentOptions.Center;
        }
        RectTransform icR = ic.GetComponent<RectTransform>();
        icR.anchorMin = new Vector2(0,0); icR.anchorMax = new Vector2(0,1);
        icR.pivot = new Vector2(0,0.5f);
        icR.anchoredPosition = new Vector2(16,0);
        icR.sizeDelta = new Vector2(36,0);

        // Label
        GameObject lb = MakeRect("Lbl", btn.transform);
        TextMeshProUGUI lbT = lb.AddComponent<TextMeshProUGUI>();
        lbT.text = label.ToUpper();
        lbT.fontSize = 18; lbT.fontStyle = FontStyles.Bold;
        lbT.color = TEXT_MAIN; lbT.characterSpacing = 2f;
        lbT.alignment = TextAlignmentOptions.MidlineLeft;
        RectTransform lbR = lb.GetComponent<RectTransform>();
        lbR.anchorMin = new Vector2(0,0); lbR.anchorMax = new Vector2(1,1);
        lbR.pivot = new Vector2(0,0.5f);
        lbR.anchoredPosition = new Vector2(76,0);
        lbR.sizeDelta = new Vector2(-100,0);

        // Seta
        GameObject ar = MakeRect("Arrow", btn.transform);
        TextMeshProUGUI arT = ar.AddComponent<TextMeshProUGUI>();
        arT.text = "→"; arT.fontSize = 24;
        arT.color = new Color(ACCENT.r, ACCENT.g, ACCENT.b, 0.75f);
        arT.alignment = TextAlignmentOptions.MidlineRight;
        RectTransform arR = ar.GetComponent<RectTransform>();
        arR.anchorMin = new Vector2(1,0); arR.anchorMax = new Vector2(1,1);
        arR.pivot = new Vector2(1,0.5f);
        arR.anchoredPosition = new Vector2(-16,0);
        arR.sizeDelta = new Vector2(40,0);

        Button b = btn.AddComponent<Button>();
        b.targetGraphic = bg;
        ColorBlock cb = ColorBlock.defaultColorBlock;
        cb.normalColor = BTN_NORMAL; cb.highlightedColor = BTN_HOVER;
        cb.pressedColor = BTN_PRESS; cb.fadeDuration = 0.10f;
        b.colors = cb;
        b.onClick.AddListener(() => onClick());

        var et = btn.AddComponent<EventTrigger>();
        AddTrigger(et, EventTriggerType.PointerEnter, _ => {
            StartCoroutine(MoveX(arR, -6f, 0.14f));
            bI.color = ACCENT2;
        });
        AddTrigger(et, EventTriggerType.PointerExit, _ => {
            StartCoroutine(MoveX(arR, -16f, 0.14f));
            bI.color = ACCENT;
        });
    }

    // Pulso de brilho subtil no título principal
    IEnumerator PulseTitle(TextMeshProUGUI tmp)
    {
        Color baseColor = tmp.color;
        while (true)
        {
            float glow = 1f + 0.08f * Mathf.Sin(Time.time * 1.2f);
            tmp.color = new Color(
                Mathf.Clamp01(baseColor.r * glow),
                Mathf.Clamp01(baseColor.g * glow),
                Mathf.Clamp01(baseColor.b * glow),
                baseColor.a);
            yield return null;
        }
    }
}

// ── Componente de estrelas UI ──────────────────────────────────────────
// Cria pequenos quadrados brancos animados diretamente no Canvas
public class StarField : MonoBehaviour
{
    public RectTransform parent;
    public int count = 180;

    struct Star
    {
        public RectTransform rt;
        public Image img;
        public float twinkleOffset;
        public float twinkleSpeed;
        public float baseBrightness;
    }

    Star[] stars;

    void Start()
    {
        stars = new Star[count];
        for (int i = 0; i < count; i++)
        {
            GameObject go = new GameObject("Star_" + i);
            go.transform.SetParent(parent, false);
            RectTransform rt = go.AddComponent<RectTransform>();
            Image img = go.AddComponent<Image>();

            float size = Random.Range(1.5f, 4.5f);
            rt.sizeDelta = new Vector2(size, size);
            float ax = Random.value, ay = Random.value;
            rt.anchorMin = rt.anchorMax = new Vector2(ax, ay);
            rt.anchoredPosition = Vector2.zero;
            rt.pivot = new Vector2(0.5f, 0.5f);

            float bright = Random.Range(0.45f, 1f);
            Color c = Random.value > 0.8f
                ? new Color(0.7f, 0.85f, 1f, bright)
                : new Color(bright, bright, bright, bright);
            img.color = c;

            stars[i] = new Star
            {
                rt = rt, img = img,
                twinkleOffset  = Random.Range(0f, Mathf.PI * 2f),
                twinkleSpeed   = Random.Range(0.3f, 1.5f),
                baseBrightness = bright
            };
        }
    }

    void Update()
    {
        float t = Time.time;
        foreach (var s in stars)
        {
            float flicker = s.baseBrightness * (0.5f + 0.5f * Mathf.Sin(t * s.twinkleSpeed + s.twinkleOffset));
            Color c = s.img.color;
            s.img.color = new Color(c.r, c.g, c.b, flicker);
        }
    }
}
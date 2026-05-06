using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;
public class Minimap : MonoBehaviour
{
    [Header("Referências")]
    public StarSystemManager manager;
    public Camera mainCamera;
    public Camera minimapCamRef;
    public RenderTexture minimapTexture;
    public RawImage minimapDisplay;

    [Header("UI Overlay")]
    public TextMeshProUGUI scaleText; // "1 div = X AU"
    public TextMeshProUGUI infoText; // "Stars: X | Planets: Y"
    public TextMeshProUGUI coordsText; // coordenadas do cursor

    [Header("Configuração")]
    public float minimapHeight = 500f;
    public float zoomPadding = 1.4f;
    public float smoothSpeed = 3f;
    public float minOrthoSize = 5f;
    public float scrollZoomSpeed = 5f;

    [Header("Cores")]
    public Color starColor = new Color(1.0f, 0.9f, 0.3f, 1f);
    public Color planetColor = new Color(0.4f, 0.7f, 1.0f, 1f);
    public Color cameraColor = new Color(0.0f, 1.0f, 0.5f, 1f);
    public Color barycenterColor = new Color(1.0f, 1.0f, 1.0f, 0.6f);
    public Color borderColor = new Color(0.3f, 0.6f, 1.0f, 0.8f);

    [Header("Tamanhos")]
    public float dotSize = 0.5f;
    public float barycenterSize = 0.3f;
    public float camDotSize = 0.6f;

    Camera minimapCam;
    Transform camIndicator;
    Transform barycenterMarker;
    GameObject autoBorder; // borda gerada automaticamente
    UnityEngine.UI.Image compassImage; // rosa dos ventos — roda com a câmara
    TMPro.TextMeshProUGUI[] windRoseLabels; // letras N/S/E/O da rosa dos ventos
    UnityEngine.UI.Image gridOverlay; // grid de referência — linhas fixas sobre o minimap
    bool isVisible = true;
    float manualZoom = 0f;
    Vector3 currentBarycenter;

    void Start()
    {
        SetupMinimapCamera();
        SetupMarkers();
        SetupBorder();
        SetupCompass();
        SetupGrid();
    }

    void SetupMinimapCamera()
    {
        // Usa a câmara criada manualmente no Unity
        minimapCam = minimapCamRef;
        minimapCam.targetTexture = minimapTexture;
    }

    void SetupMarkers()
    {
        // Indicador da câmara principal — círculo verde que segue a posição da câmara no mundo
        camIndicator = CreateMarker("CamIndicator", cameraColor, camDotSize);
        // Marcador do baricentro — cruz feita de dois cubos achatados
        barycenterMarker = CreateCrossMarker("Barycenter", barycenterColor, barycenterSize);
    }

    void SetupBorder()
    {
        // Gera um sprite circular suave em runtime
        Sprite circleSprite = GenerateCircleSprite(256);
        Sprite ringSprite = GenerateRingSprite(256, 4); // 4px de espessura

        // Aplica a máscara circular ao MinimapMask
        if (minimapDisplay != null)
        {
            Transform maskT = minimapDisplay.transform.parent;
            Image maskImage = maskT?.GetComponent<Image>();
            if (maskImage != null)
                maskImage.sprite = circleSprite;
        }

        // Cria a borda como Image filho do MinimapMask automaticamente
        if (minimapDisplay != null)
        {
            Transform maskT = minimapDisplay.transform.parent;
            if (maskT != null)
            {
                // Remove borda antiga se existir
                Transform old = maskT.parent?.Find("AutoBorder");
                if (old != null) Destroy(old.gameObject);

                // Cria nova borda como irmã do MinimapMask (fora da máscara)
                autoBorder = new GameObject("AutoBorder");
                autoBorder.transform.SetParent(maskT.parent ?? maskT, false);

                Image borderImg = autoBorder.AddComponent<Image>();
                borderImg.sprite = ringSprite;
                borderImg.color = borderColor;
                borderImg.type = Image.Type.Simple;
                borderImg.raycastTarget = false;

                // Mesmo tamanho que o MinimapMask
                RectTransform rt = autoBorder.GetComponent<RectTransform>();
                RectTransform maskRt = maskT.GetComponent<RectTransform>();
                if (maskRt != null)
                {
                    rt.anchorMin = maskRt.anchorMin;
                    rt.anchorMax = maskRt.anchorMax;
                    rt.anchoredPosition = maskRt.anchoredPosition;
                    rt.sizeDelta = maskRt.sizeDelta + Vector2.one * 4f;
                }
            }
        }
    }

    // Gera um sprite de anel (circunferência) com a espessura definida
    Sprite GenerateRingSprite(int resolution, float thickness)
    {
        Texture2D tex = new Texture2D(resolution, resolution, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Bilinear;
        Color[] pixels = new Color[resolution * resolution];
        float center = resolution * 0.5f;
        float outer = center - 1f;
        float inner = outer - thickness;
        float soft = 1.5f;

        for (int y = 0; y < resolution; y++)
        {
            for (int x = 0; x < resolution; x++)
            {
                float dist = Vector2.Distance(new Vector2(x, y), new Vector2(center, center));
                float outerA = Mathf.Clamp01((outer - dist) / soft);
                float innerA = Mathf.Clamp01((dist - inner) / soft);
                float alpha = outerA * innerA;
                pixels[y * resolution + x] = new Color(1f, 1f, 1f, alpha);
            }
        }

        tex.SetPixels(pixels);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, resolution, resolution),
            new Vector2(0.5f, 0.5f), resolution);
    }

    // Gera uma textura circular suave em runtime
    Sprite GenerateCircleSprite(int resolution)
    {
        Texture2D tex = new Texture2D(resolution, resolution, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Bilinear;
        Color[] pixels = new Color[resolution * resolution];
        float center = resolution * 0.5f;
        float radius = center - 1f;
        float soft = 1.5f; // pixels de anti-aliasing

        for (int y = 0; y < resolution; y++)
        {
            for (int x = 0; x < resolution; x++)
            {
                float dist = Vector2.Distance(new Vector2(x, y), new Vector2(center, center));
                float alpha = Mathf.Clamp01((radius - dist) / soft);
                pixels[y * resolution + x] = new Color(1f, 1f, 1f, alpha);
            }
        }
        tex.SetPixels(pixels);
        tex.Apply();
        return Sprite.Create(tex,
            new Rect(0, 0, resolution, resolution),
            new Vector2(0.5f, 0.5f),
            resolution);
    }

    Transform CreateCrossMarker(string markerName, Color color, float size)
    {
        // Objeto pai vazio
        GameObject parent = new GameObject(markerName);
        int minimapLayer = LayerMask.NameToLayer("Minimap");

        Material mat = new Material(Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Color"));
        mat.color = color;
        mat.SetColor("_BaseColor", color);

        // Barra horizontal
        GameObject h = GameObject.CreatePrimitive(PrimitiveType.Cube);
        h.name = "H";
        h.transform.SetParent(parent.transform, false);
        h.transform.localScale = new Vector3(size * 3f, size * 0.3f, size * 0.3f);
        Destroy(h.GetComponent<Collider>());
        h.GetComponent<Renderer>().material = mat;
        if (minimapLayer >= 0) h.layer = minimapLayer;

        // Barra vertical
        GameObject v = GameObject.CreatePrimitive(PrimitiveType.Cube);
        v.name = "V";
        v.transform.SetParent(parent.transform, false);
        v.transform.localScale = new Vector3(size * 0.3f, size * 0.3f, size * 3f);
        Destroy(v.GetComponent<Collider>());
        v.GetComponent<Renderer>().material = mat;
        if (minimapLayer >= 0) v.layer = minimapLayer;
        if (minimapLayer >= 0) parent.layer = minimapLayer;
        return parent.transform;
    }

    Transform CreateMarker(string markerName, Color color, float size)
    {
        GameObject go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        go.name = markerName;
        go.transform.localScale = Vector3.one * size;
        Destroy(go.GetComponent<Collider>());

        // Coloca na layer Minimap para não aparecer na câmara principal
        int minimapLayer = LayerMask.NameToLayer("Minimap");
        if (minimapLayer >= 0) go.layer = minimapLayer;

        Material mat = new Material(Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Color"));
        mat.color = color;
        mat.SetColor("_BaseColor", color);
        go.GetComponent<Renderer>().material = mat;
        return go.transform;
    }

    void LateUpdate()
    {
        // Bússola e indicador da câmara atualizam sempre — independentemente do manager
        UpdateCompass();
        UpdateCamIndicator();

        if (!isVisible || minimapCam == null) return;

        // Modo sem manager (cena de Relatividade) — câmara do minimap segue a câmara principal
        // com zoom fixo baseado no tamanho da grid
        if (manager == null)
        {
            if (mainCamera != null)
            {
                Vector3 camPos     = mainCamera.transform.position;
                Vector3 followPos  = new Vector3(camPos.x, minimapHeight, camPos.z);
                minimapCam.transform.position = Vector3.Lerp(
                    minimapCam.transform.position, followPos, smoothSpeed * Time.deltaTime);
            }
            // Zoom fixo para cobrir a grid toda
            minimapCam.orthographicSize = Mathf.Lerp(
                minimapCam.orthographicSize, minOrthoSize * 15f, smoothSpeed * Time.deltaTime);
            return;
        }

        if (!isVisible) return;

        List<StarComponent> stars = manager.GetStars();

        // Conta sempre os objetos para atualizar o infoText (mesmo após Reset)
        int starCount = 0;
        int planetCount = 0;
        Vector3 barycenter = Vector3.zero;
        float totalMass = 0f;

        if (stars != null)
        {
            foreach (StarComponent sc in stars)
            {
                if (sc == null) continue;
                barycenter += sc.transform.position * sc.mass;
                totalMass  += sc.mass;
                if (sc.isPlanet) planetCount++; else starCount++;
            }
        }
        if (totalMass > 0f) barycenter /= totalMass;
        currentBarycenter = barycenter;

        // Se não há objetos, atualiza o texto e a bússola e sai
        if (stars == null || stars.Count == 0)
        {
            if (infoText != null) infoText.text = "Stars: 0  |  Planets: 0";
            return;
        }

        // Cálculo do Extent
        float maxDist = minOrthoSize;
        foreach (StarComponent sc in stars)
        {
            if (sc == null) continue;
            float d = Vector2.Distance(
                new Vector2(sc.transform.position.x, sc.transform.position.z),
                new Vector2(barycenter.x, barycenter.z));
            if (d > maxDist) maxDist = d;
        }
        // Calcula o centro do minimap (ponto médio entre baricentro e câmara)
        // para garantir que o indicador verde fica sempre dentro do zoom
        Vector3 camXZ    = mainCamera != null
            ? new Vector3(mainCamera.transform.position.x, 0f, mainCamera.transform.position.z)
            : Vector3.zero;
        Vector3 baryXZ   = new Vector3(barycenter.x, 0f, barycenter.z);
        Vector3 centerXZ = (camXZ + baryXZ) * 0.5f;

        if (mainCamera != null)
        {
            float dCam = Vector2.Distance(
                new Vector2(mainCamera.transform.position.x, mainCamera.transform.position.z),
                new Vector2(centerXZ.x, centerXZ.z));
            maxDist = Mathf.Max(maxDist, dCam);
        }

        // Zoom manual via scroll (quando rato está sobre o minimap)
        if (IsMouseOverMinimap())
        {
            float scroll = Input.mouseScrollDelta.y;
            manualZoom -= scroll * scrollZoomSpeed;
        }

        float desiredSize = Mathf.Max(minOrthoSize, maxDist * zoomPadding + manualZoom);

        // Clique no minimap — teletransporta câmara principal para a posição clicada
        if (IsMouseOverMinimap() && Input.GetMouseButtonDown(0))
        {
            Vector3 worldPos = MinimapClickToWorld();
            if (mainCamera != null && worldPos != Vector3.zero)
                mainCamera.transform.position = new Vector3(worldPos.x,
                    mainCamera.transform.position.y, worldPos.z);
        }

        // Move câmara do minimap suavemente para o centro entre baricentro e câmara
        Vector3 targetPos  = new Vector3(centerXZ.x, minimapHeight, centerXZ.z);
        minimapCam.transform.position = Vector3.Lerp(
            minimapCam.transform.position, targetPos, smoothSpeed * Time.deltaTime);
        minimapCam.orthographicSize = Mathf.Lerp(
            minimapCam.orthographicSize, desiredSize, smoothSpeed * Time.deltaTime);

        // Baricentro
        float y = minimapHeight - 1f;
        if (barycenterMarker != null)
        {
            barycenterMarker.position = new Vector3(barycenter.x, y, barycenter.z);
            // Escala com o zoom para ficar sempre visível
            float s = minimapCam.orthographicSize * 0.06f;
            barycenterMarker.localScale = Vector3.one * Mathf.Max(s, barycenterSize * 3f);
        }

        // Atualiza textos de UI
        UpdateOverlayUI(starCount, planetCount, desiredSize);
    }

    // Roda a rosa dos ventos com a direção horizontal da câmara
    // Extraído do LateUpdate para correr sempre — mesmo sem manager (cena de Relatividade)
    void UpdateCompass()
    {
        if (compassImage == null || mainCamera == null) return;

        Vector3 camForward = mainCamera.transform.forward;
        camForward.y = 0f;
        if (camForward.sqrMagnitude > 0.001f)
        {
            float angle = Mathf.Atan2(camForward.x, camForward.z) * Mathf.Rad2Deg;
            compassImage.rectTransform.localRotation = Quaternion.Euler(0f, 0f, -angle);

            // Roda também as labels para se manterem sempre verticais
            if (windRoseLabels != null)
                foreach (var lbl in windRoseLabels)
                    if (lbl != null)
                        lbl.rectTransform.localRotation = Quaternion.Euler(0f, 0f, angle);
        }
    }

    // Atualiza o indicador verde da câmara no minimap
    // Extraído do LateUpdate para correr sempre — mesmo sem manager (cena de Relatividade)
    void UpdateCamIndicator()
    {
        if (camIndicator == null || mainCamera == null || minimapCam == null) return;

        // Afastado o suficiente do near clip plane da câmara do minimap
        float   y  = minimapHeight - 5f;
        Vector3 cp = mainCamera.transform.position;
        camIndicator.position = new Vector3(cp.x, y, cp.z);

        // Tamanho proporcional ao zoom do minimap
        // Usa um mínimo generoso para garantir visibilidade mesmo quando o zoom está a arrancar
        float s       = minimapCam.orthographicSize * 0.05f;
        float minSize = Mathf.Max(camDotSize, minimapCam.orthographicSize * 0.03f);
        camIndicator.localScale = Vector3.one * Mathf.Max(s, minSize);
    }

    void UpdateOverlayUI(int starCount, int planetCount, float orthoSize)
    {
        // Contador de objetos
        if (infoText != null)
            infoText.text = $"Stars: {starCount}  |  Planets: {planetCount}";

        // Escala — quanto representa cada "divisão" (10% do orthoSize) em AU
        if (scaleText != null)
        {
            float divAU = orthoSize * 0.2f * 0.1f; // distToAU = 0.1
            scaleText.text = $"Scale: 1 div = {divAU:F1} AU";
        }

        // Coordenadas do cursor em AU (só quando sobre o minimap)
        if (coordsText != null)
        {
            if (IsMouseOverMinimap())
            {
                Vector3 w = MinimapClickToWorld();
                coordsText.text = $"X: {w.x * 0.1f:F1} AU  Z: {w.z * 0.1f:F1} AU";
            }
            else
            {
                // Mostra as coordenadas do baricentro quando não há hover
                coordsText.text = $"X: {currentBarycenter.x * 0.1f:F1} AU  Z: {currentBarycenter.z * 0.1f:F1} AU";
            }
        }
    }

    bool IsMouseOverMinimap()
    {
        if (minimapDisplay == null) return false;
        return RectTransformUtility.RectangleContainsScreenPoint(
            minimapDisplay.rectTransform,
            Input.mousePosition,
            null);
    }

    Vector3 MinimapClickToWorld()
    {
        if (minimapDisplay == null || minimapCam == null) return Vector3.zero;

        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            minimapDisplay.rectTransform,
            Input.mousePosition,
            null,
            out Vector2 localPoint);

        Rect rect = minimapDisplay.rectTransform.rect;
        float normX = (localPoint.x - rect.x) / rect.width;
        float normY = (localPoint.y - rect.y) / rect.height;

        float size = minimapCam.orthographicSize;
        Vector3 camPos = minimapCam.transform.position;

        float worldX = camPos.x + (normX - 0.5f) * size * 2f;
        float worldZ = camPos.z + (normY - 0.5f) * size * 2f;

        return new Vector3(worldX, 0f, worldZ);
    }

    public void Toggle(bool visible)
    {
        isVisible = visible;
        if (minimapDisplay != null) minimapDisplay.enabled = visible;
        if (scaleText!= null) scaleText.enabled = visible;
        if (infoText != null) infoText.enabled = visible;
        if (coordsText != null) coordsText.enabled = visible;
        if (minimapCam != null) minimapCam.enabled = visible;
        if (autoBorder != null) autoBorder.SetActive(visible);
        if (camIndicator != null) camIndicator.gameObject.SetActive(visible);
        if (barycenterMarker != null) barycenterMarker.gameObject.SetActive(visible);
        if (compassImage != null) compassImage.gameObject.SetActive(visible);
        if (gridOverlay != null) gridOverlay.gameObject.SetActive(visible);
        if (windRoseLabels != null)
            foreach (var lbl in windRoseLabels)
                if (lbl != null) lbl.gameObject.SetActive(visible);
    }

    // Cria a rosa dos ventos UI — 4 pontas + letras N/S/E/O, fixa no quadrante superior esquerdo
    // Roda com a direção horizontal da câmara; o N aponta sempre para o norte do mundo (+Z)
    void SetupCompass()
    {
        if (minimapDisplay == null) return;

        RectTransform dispRt = minimapDisplay.rectTransform;
        float mapSize = dispRt.rect.width > 0 ? dispRt.rect.width : 200f;
        float roseSize = mapSize * 0.14f; // tamanho da rosa dos ventos
        // Posição: quadrante superior esquerdo dentro do círculo
        // A diagonal a 135 graus: borda está a raio*0.707 do centro
        // Ficamos a aproximadamente 32% do raio para garantir que a rosa + labels cabem dentro
        Vector2 rosePos = new Vector2(-mapSize * 0.17f, mapSize * 0.20f);

        // Contentor da rosa (roda em bloco)
        GameObject roseGO = new GameObject("WindRose");
        roseGO.transform.SetParent(minimapDisplay.transform, false);
        RectTransform roseRt = roseGO.AddComponent<RectTransform>();
        roseRt.anchorMin = new Vector2(0.5f, 0.5f);
        roseRt.anchorMax = new Vector2(0.5f, 0.5f);
        roseRt.pivot = new Vector2(0.5f, 0.5f);
        roseRt.anchoredPosition = rosePos;
        roseRt.sizeDelta = new Vector2(roseSize, roseSize);

        // Sprite da rosa dos ventos gerado em runtime
        compassImage = roseGO.AddComponent<UnityEngine.UI.Image>();
        compassImage.sprite = GenerateWindRoseSprite(128);
        compassImage.color = Color.white; // cores definidas no sprite por ponta
        compassImage.raycastTarget = false;

        // Letras N/S/E/O
        string[] dirs   = { "N", "S", "E", "O" };
        Vector2[] offsets = {
            new Vector2( 0f, roseSize * 0.62f), // N — topo
            new Vector2( 0f, -roseSize * 0.62f), // S — baixo
            new Vector2( roseSize * 0.62f,  0f), // E — direita
            new Vector2(-roseSize * 0.62f,  0f), // O — esquerda
        };
        Color[] labelColors = {
            new Color(0.95f, 0.15f, 0.15f, 1.0f), // N — vermelho vivo
            new Color(0.45f, 0.55f, 0.75f, 0.9f), // S — cinzento-azulado
            new Color(0.90f, 0.90f, 0.90f, 0.8f), // E — branco
            new Color(0.90f, 0.90f, 0.90f, 0.8f), // O — branco
        };

        windRoseLabels = new TMPro.TextMeshProUGUI[4];
        for (int i = 0; i < 4; i++)
        {
            GameObject lblGO = new GameObject(dirs[i]);
            lblGO.transform.SetParent(roseGO.transform, false);

            TMPro.TextMeshProUGUI lbl = lblGO.AddComponent<TMPro.TextMeshProUGUI>();
            lbl.text = dirs[i];
            lbl.fontSize = roseSize * 0.28f;
            lbl.color = labelColors[i];
            lbl.alignment = TMPro.TextAlignmentOptions.Center;
            lbl.raycastTarget = false;

            RectTransform lblRt = lblGO.GetComponent<RectTransform>();
            lblRt.anchorMin = new Vector2(0.5f, 0.5f);
            lblRt.anchorMax = new Vector2(0.5f, 0.5f);
            lblRt.pivot = new Vector2(0.5f, 0.5f);
            lblRt.anchoredPosition = offsets[i];
            lblRt.sizeDelta = new Vector2(roseSize * 0.35f, roseSize * 0.35f);

            windRoseLabels[i] = lbl;
        }
    }

    // Cria a grid de referência — linhas finas cruzadas e círculo intermédio
    // Mapa "Espacial" (um nome bonito que dei): ajuda o utilizador a estimar distâncias no minimap
    void SetupGrid()
    {
        if (minimapDisplay == null) return;

        RectTransform dispRt = minimapDisplay.rectTransform;
        float mapSize = dispRt.rect.width > 0 ? dispRt.rect.width : 200f;

        GameObject gridGO = new GameObject("GridOverlay");
        gridGO.transform.SetParent(minimapDisplay.transform, false);

        gridOverlay = gridGO.AddComponent<UnityEngine.UI.Image>();
        gridOverlay.sprite = GenerateGridSprite(256);
        gridOverlay.color = new Color(0.3f, 0.6f, 1.0f, 0.15f); // azul muito subtil
        gridOverlay.raycastTarget = false;

        RectTransform rt = gridGO.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;

        // Grid fica por baixo dos marcadores — manda para o fundo da hierarquia
        gridGO.transform.SetAsFirstSibling();
    }

    // Gera o sprite da rosa dos ventos com cores por ponta e traços intercardeais
    // N - vermelho vivo (maior)   S - cinzento-azulado (mais curto)   E/O - branco (intermédios)
    // Traços NE/SE/SW/NW marcam os pontos intercardeais no anel exterior
    Sprite GenerateWindRoseSprite(int res)
    {
        Texture2D tex = new Texture2D(res, res, TextureFormat.RGBA32, false);
        Color[] pixels = new Color[res * res];
        float cx = res * 0.5f;
        float cy = res * 0.5f;
        float r = res * 0.5f;

        // Cores das pontas
        Color colorN = new Color(0.95f, 0.15f, 0.15f, 1.0f); // vermelho vivo
        Color colorS = new Color(0.45f, 0.55f, 0.75f, 1.0f); // cinzento-azulado
        Color colorEO = new Color(0.95f, 0.95f, 0.95f, 1.0f); // branco
        Color colorRing = new Color(0.70f, 0.70f, 0.70f, 0.8f); // anel e traços intercardeais

        for (int y = 0; y < res; y++)
        {
            for (int x = 0; x < res; x++)
            {
                float nx = (x - cx) / r;
                float ny = (y - cy) / r;
                float dist = Mathf.Sqrt(nx * nx + ny * ny);

                Color pixel = Color.clear;

                // Ponta Norte (+Y) — maior (alcança 0.95) e mais larga (0.32)
                if (ny > 0.02f && Mathf.Abs(nx) < (1f - ny) * 0.32f && ny < 0.95f)
                    pixel = colorN;

                // Ponta Sul (-Y) — mais curta (só até 0.65) e mais estreita (0.22)
                if (ny < -0.02f && Mathf.Abs(nx) < (1f + ny) * 0.22f && ny > -0.65f)
                    pixel = colorS;

                // Ponta Este (+X) — comprimento intermédio (0.80)
                if (nx > 0.02f && Mathf.Abs(ny) < (1f - nx) * 0.25f && nx < 0.80f)
                    pixel = colorEO;

                // Ponta Oeste (-X) — comprimento intermédio (0.80)
                if (nx < -0.02f && Mathf.Abs(ny) < (1f + nx) * 0.25f && nx > -0.80f)
                    pixel = colorEO;

                // Círculo central duplo — anel interno e ponto central
                if (dist < 0.08f) pixel = Color.white;
                if (dist > 0.10f && dist < 0.16f) pixel = colorRing;

                // Anel exterior contínuo
                if (dist > 0.87f && dist < 0.93f) pixel = colorRing;

                // Traços intercardeais NE/SE/SW/NW — pequenos traços diagonais no anel exterior
                // Calculados pelo ângulo: NE=45 graus, SE=135 graus, SW=225 graus, NW=315 graus
                if (dist > 0.72f && dist < 0.86f)
                {
                    float angle = Mathf.Atan2(ny, nx) * Mathf.Rad2Deg; // -180 a 180
                    // Normaliza para 0–360
                    if (angle < 0) angle += 360f;

                    // Verifica se está perto de 45, 135, 225, 315 (+/-4) graus
                    float[] intercardinal = { 45f, 135f, 225f, 315f };
                    foreach (float target in intercardinal)
                    {
                        float diff = Mathf.Abs(Mathf.DeltaAngle(angle, target));
                        if (diff < 5f)
                        {
                            // Espessura do traço — mais fino que as pontas
                            pixel = Color.Lerp(colorRing, Color.clear, diff / 5f);
                            break;
                        }
                    }
                }

                pixels[y * res + x] = pixel;
            }
        }

        tex.SetPixels(pixels);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, res, res), new Vector2(0.5f, 0.5f), res);
    }

    // Gera o sprite da grid — linhas cruzadas + círculo intermédio
    // Divisão em quadrantes com referência de escala
    Sprite GenerateGridSprite(int res)
    {
        Texture2D tex = new Texture2D(res, res, TextureFormat.RGBA32, false);
        Color[] pixels = new Color[res * res];
        float cx = res * 0.5f;
        float cy = res * 0.5f;
        float r = res * 0.5f - 1f;
        float lineW = 0.012f; // espessura das linhas em proporção do raio

        for (int y = 0; y < res; y++)
        {
            for (int x = 0; x < res; x++)
            {
                float nx   = (x - cx) / r;
                float ny   = (y - cy) / r;
                float dist = Mathf.Sqrt(nx * nx + ny * ny);

                // Só dentro do círculo do minimap
                if (dist > 1.0f) { pixels[y * res + x] = Color.clear; continue; }

                bool draw = false;

                // Linha horizontal central
                if (Mathf.Abs(ny) < lineW) draw = true;
                // Linha vertical central
                if (Mathf.Abs(nx) < lineW) draw = true;
                // Círculo intermédio a 50% do raio
                if (dist > 0.49f && dist < 0.49f + lineW * 2f) draw = true;

                pixels[y * res + x] = draw ? Color.white : Color.clear;
            }
        }

        tex.SetPixels(pixels);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, res, res), new Vector2(0.5f, 0.5f), res);
    }
}
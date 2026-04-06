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
    bool isVisible = true;
    float manualZoom = 0f;
    Vector3 currentBarycenter;

    void Start()
    {
        SetupMinimapCamera();
        SetupMarkers();
        SetupBorder();
    }

    void SetupMinimapCamera()
    {
        // Usa a câmara criada manualmente no Unity
        minimapCam = minimapCamRef;
        minimapCam.targetTexture = minimapTexture;
    }

    void SetupMarkers()
    {
        // Indicador da câmara principal
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
        if (!isVisible || manager == null || minimapCam == null) return;

        List<StarComponent> stars = manager.GetStars();
        if (stars == null || stars.Count == 0) return;

        // Cálculo do baricentro
        Vector3 barycenter = Vector3.zero;
        float totalMass  = 0f;
        int starCount  = 0;
        int planetCount= 0;

        foreach (StarComponent sc in stars)
        {
            if (sc == null) continue;
            barycenter += sc.transform.position * sc.mass;
            totalMass += sc.mass;
            if (sc.isPlanet) planetCount++; else starCount++;
        }
        if (totalMass > 0f) barycenter /= totalMass;
        currentBarycenter = barycenter;

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
        if (mainCamera != null)
        {
            float dCam = Vector2.Distance(
                new Vector2(mainCamera.transform.position.x, mainCamera.transform.position.z),
                new Vector2(barycenter.x, barycenter.z));
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

        // Move câmara do minimap suavemente
        Vector3 targetPos = new Vector3(barycenter.x, minimapHeight, barycenter.z);
        minimapCam.transform.position = Vector3.Lerp(
            minimapCam.transform.position, targetPos, smoothSpeed * Time.deltaTime);
        minimapCam.orthographicSize = Mathf.Lerp(
            minimapCam.orthographicSize, desiredSize, smoothSpeed * Time.deltaTime);

        // Posiciona marcadores
        float y = minimapHeight - 1f;
        if (camIndicator != null && mainCamera != null)
        {
            Vector3 cp = mainCamera.transform.position;
            camIndicator.position = new Vector3(cp.x, y, cp.z);
            float s = minimapCam.orthographicSize * 0.05f;
            camIndicator.localScale = Vector3.one * Mathf.Max(s, camDotSize);
        }
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
    }
}
using UnityEngine;
using Unity.Mathematics;
using static Unity.Mathematics.math;

// Gera a aparência procedural de um planeta ao nascer.
// Cada planeta recebe uma seed aleatória que determina o padrão único de superfície.
// As camadas geradas são: superfície (continentes/oceanos), nuvens, emissão noturna e atmosfera (rim).
// A superfície e as nuvens rodam em tempo real a velocidades independentes.
[RequireComponent(typeof(StarComponent))]
public class PlanetAppearance : MonoBehaviour
{
    [Header("Resolução das Texturas")]
    [Tooltip("256 é suficiente para a maioria dos planetas. Use 512 para planetas muito grandes.")]
    public int textureResolution = 256;

    [Header("Rotação")]
    public float minRotationSpeed = 2f;   // graus por segundo
    public float maxRotationSpeed = 15f;
    public float cloudSpeedMultiplier = 1.4f; // nuvens rodam mais rápido que a superfície

    [Header("Nuvens")]
    [Range(0f, 1f)]
    public float cloudCoverage = 0.45f;   // 0 = sem nuvens, 1 = totalmente coberto
    public float cloudOpacity  = 0.85f;

    [Header("Atmosfera")]
    public float atmosphereScale    = 1.04f;  // tamanho relativo à esfera do planeta
    public float atmosphereOpacity  = 0.35f;

    // Estado interno
    private StarComponent   starComponent;
    private MeshRenderer    surfaceRenderer;
    private GameObject      cloudSphere;
    private GameObject      atmosphereSphere;
    private Material        surfaceMat;
    private Material        cloudMat;
    private Material        atmosphereMat;
    private float           rotationSpeed;
    private float           currentRotationAngle = 0f;
    private float3          rotationAxis;
    private float           seed;

    // Cores derivadas da massa (preenchidas em Init a partir do StarComponent)
    private Color surfaceColor;
    private Color oceanColor;
    private Color atmosphereColor;
    private Color nightEmissionColor;
    private bool  isIceGiant;  // true para gigantes de gelo (massa > 6 u.i.)
    private bool  isReplayMode = false; // true durante o replay — suspende as Coroutines de textura

    void Start()
    {
        starComponent   = GetComponent<StarComponent>();
        surfaceRenderer = GetComponent<MeshRenderer>();

        if (starComponent == null || !starComponent.isPlanet) return;

        // Seed aleatória única por planeta — determina o padrão de superfície
        seed = UnityEngine.Random.Range(0f, 1000f);

        // Eixo de rotação ligeiramente inclinado (como os planetas reais)
        float tilt  = UnityEngine.Random.Range(0f, 30f) * Mathf.Deg2Rad;
        rotationAxis = normalize(new float3(sin(tilt), cos(tilt), 0f));
        rotationSpeed = UnityEngine.Random.Range(minRotationSpeed, maxRotationSpeed);

        DeriveColors();
        BuildAtmosphereSphere(); // atmosfera instantânea — não gera texturas
        BuildCloudSphere();      // nuvens com placeholder enquanto a textura carrega
        StartCoroutine(GenerateTexturesAsync()); // texturas em background — sem freeze
    }

    // Controla o modo replay — suspende a geração de texturas para não interferir
    // com os materiais durante o rewind da SimulationTimeline
    public void SetReplayMode(bool replay)
    {
        isReplayMode = replay;

        // Durante o replay, esconde os filhos (nuvens e atmosfera) para evitar artefactos
        if (cloudSphere     != null) cloudSphere.SetActive(!replay);
        if (atmosphereSphere!= null) atmosphereSphere.SetActive(!replay);
    }

    // Gera as texturas distribuídas por vários frames para não bloquear a thread principal
    // O planeta aparece imediatamente com cor sólida e as texturas fazem fade-in quando ficam prontas
    System.Collections.IEnumerator GenerateTexturesAsync()
    {
        // Placeholder — material com cor sólida enquanto as texturas carregam
        Material placeholderMat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        placeholderMat.color = surfaceColor;
        placeholderMat.SetColor("_BaseColor", surfaceColor);
        placeholderMat.SetFloat("_Smoothness", isIceGiant ? 0.6f : 0.2f);
        surfaceRenderer.material = placeholderMat;
        surfaceMat = placeholderMat;

        yield return null; // espera 1 frame antes de começar

        // Gera superfície de forma assíncrona
        yield return StartCoroutine(GenerateSurfaceAsync());
        yield return null;

        // Gera emissão de forma assíncrona
        yield return StartCoroutine(GenerateEmissionAsync());
        yield return null;

        // Gera nuvens de forma assíncrona
        yield return StartCoroutine(GenerateCloudsAsync());
    }

    void Update()
    {
        if (!starComponent.isPlanet) return;

        // Avança o ângulo de rotação
        currentRotationAngle += rotationSpeed * Time.deltaTime;

        // Roda a superfície
        if (surfaceMat != null)
            surfaceMat.SetFloat("_RotationAngle", currentRotationAngle * Mathf.Deg2Rad);

        // Roda as nuvens mais rápido
        if (cloudMat != null)
            cloudMat.SetFloat("_RotationAngle", currentRotationAngle * cloudSpeedMultiplier * Mathf.Deg2Rad);
    }

    // Deriva as cores das camadas a partir da massa do planeta
    void DeriveColors()
    {
        float mass = starComponent.mass;
        isIceGiant = mass >= 6.0f;

        if (mass < 1.5f)
        {
            // Rochoso pequeno — Marte/Mercúrio: tons acastanhados, sem oceanos, atmosfera fina
            surfaceColor      = Color.Lerp(new Color(0.45f, 0.30f, 0.22f), new Color(0.60f, 0.45f, 0.35f), Mathf.InverseLerp(0.33f, 1.5f, mass));
            oceanColor        = new Color(0.30f, 0.22f, 0.15f); // "oceanos" são planícies rochosas
            atmosphereColor   = new Color(0.80f, 0.50f, 0.30f, atmosphereOpacity); // atmosfera fina alaranjada
            nightEmissionColor= new Color(0.90f, 0.60f, 0.20f, 0.3f); // luzes quentes
        }
        else if (mass < 6.0f)
        {
            // Rochoso grande / super-Terra — Terra/Vénus: continentes, oceanos azuis, atmosfera azul
            float t = Mathf.InverseLerp(1.5f, 6.0f, mass);
            surfaceColor      = Color.Lerp(new Color(0.30f, 0.50f, 0.25f), new Color(0.55f, 0.45f, 0.30f), t); // verde/castanho
            oceanColor        = Color.Lerp(new Color(0.10f, 0.35f, 0.65f), new Color(0.05f, 0.20f, 0.50f), t); // azul profundo
            atmosphereColor   = new Color(0.40f, 0.65f, 1.00f, atmosphereOpacity); // azul-celeste
            nightEmissionColor= new Color(1.00f, 0.90f, 0.50f, 0.5f); // luzes de cidades amarelas
        }
        else
        {
            // Gigante de gelo — Neptuno/Urano: sem continentes, bandas atmosféricas, brilho azul
            float t = Mathf.InverseLerp(6.0f, 51f, mass);
            surfaceColor      = Color.Lerp(new Color(0.20f, 0.55f, 0.75f), new Color(0.10f, 0.35f, 0.70f), t); // azul-ciano
            oceanColor        = Color.Lerp(new Color(0.15f, 0.45f, 0.80f), new Color(0.05f, 0.25f, 0.65f), t); // azul mais escuro
            atmosphereColor   = new Color(0.30f, 0.70f, 1.00f, atmosphereOpacity * 1.5f); // atmosfera densa azul
            nightEmissionColor= new Color(0.20f, 0.60f, 1.00f, 0.6f); // brilho atmosférico azul
        }
    }

    // Coroutine: gera a textura de superfície distribuída por vários frames
    // Cada linha de pixels é calculada num frame separado para evitar freeze
    System.Collections.IEnumerator GenerateSurfaceAsync()
    {
        int res = textureResolution;
        Texture2D tex    = new Texture2D(res, res, TextureFormat.RGBA32, true);
        Color[]   pixels = new Color[res * res];

        float oceanThreshold = isIceGiant
            ? UnityEngine.Random.Range(0.70f, 0.90f)
            : Mathf.Lerp(0.35f, 0.55f, Mathf.InverseLerp(0.33f, 6f, starComponent.mass));

        // Processa 8 linhas por frame — bom equilíbrio entre velocidade e fluidez
        int linesPerFrame = 8;
        for (int y = 0; y < res; y++)
        {
            // Aborta a geração se entrou em modo replay
            if (isReplayMode) yield break;
            for (int x = 0; x < res; x++)
            {
                float u = (float)x / res;
                float v = (float)y / res;
                float3 spherePos = UVToSphere(u, v);

                float continent = noise.snoise(spherePos * 1.8f + seed) * 0.5f + 0.5f;
                float detail    = noise.snoise(spherePos * 5.5f + seed + 100f) * 0.5f + 0.5f;
                float combined  = continent * 0.70f + detail * 0.30f;

                if (isIceGiant)
                {
                    float bands = noise.snoise(new float3(spherePos.x * 0.5f, spherePos.y * 3.0f, spherePos.z * 0.5f) + seed) * 0.5f + 0.5f;
                    combined    = combined * 0.4f + bands * 0.6f;
                }

                Color pixelColor;
                if (combined > oceanThreshold)
                {
                    float terrainVar = detail * 0.25f;
                    pixelColor = Color.Lerp(surfaceColor * (1f - terrainVar),
                                            surfaceColor * (1f + terrainVar * 0.5f), detail);
                    if (!isIceGiant && starComponent.mass > 1.5f)
                    {
                        float poleBlend = Mathf.Pow(Mathf.Abs(v * 2f - 1f), 4f);
                        pixelColor = Color.Lerp(pixelColor, Color.white * 0.9f, poleBlend * 0.8f);
                    }
                }
                else
                {
                    float depth = 1f - (combined / oceanThreshold);
                    pixelColor  = Color.Lerp(oceanColor * 1.1f, oceanColor * 0.6f, depth);
                }
                pixels[y * res + x] = pixelColor;
            }

            // Yield a cada linesPerFrame linhas
            if (y % linesPerFrame == 0) yield return null;
        }

        tex.SetPixels(pixels);
        tex.Apply();
        tex.wrapMode = TextureWrapMode.Repeat;

        // Aplica a textura ao material já existente
        if (surfaceMat != null)
        {
            surfaceMat.mainTexture = tex;
            surfaceMat.SetFloat("_Smoothness", isIceGiant ? 0.6f : 0.2f);
            surfaceMat.SetFloat("_Metallic", 0f);
        }
    }

    // Coroutine: gera a textura de emissão noturna distribuída por vários frames
    System.Collections.IEnumerator GenerateEmissionAsync()
    {
        int res = textureResolution;
        Texture2D tex    = new Texture2D(res, res, TextureFormat.RGBA32, false);
        Color[]   pixels = new Color[res * res];
        Color     black  = Color.black;

        float oceanThreshold = Mathf.Lerp(0.35f, 0.55f, Mathf.InverseLerp(1.5f, 6f, starComponent.mass));
        int linesPerFrame = 8;

        for (int y = 0; y < res; y++)
        {
            for (int x = 0; x < res; x++)
            {
                float u       = (float)x / res;
                float v       = (float)y / res;
                float3 sphPos = UVToSphere(u, v);

                if (isIceGiant)
                {
                    float glow = noise.snoise(sphPos * 2.0f + seed + 500f) * 0.5f + 0.5f;
                    glow       = Mathf.Pow(glow, 3f) * 0.4f;
                    pixels[y * res + x] = nightEmissionColor * glow;
                }
                else if (starComponent.mass >= 1.5f)
                {
                    float continent = noise.snoise(sphPos * 1.8f + seed) * 0.5f + 0.5f;
                    float detail    = noise.snoise(sphPos * 5.5f + seed + 100f) * 0.5f + 0.5f;
                    float combined  = continent * 0.70f + detail * 0.30f;
                    if (combined > oceanThreshold)
                    {
                        float cityNoise = noise.snoise(sphPos * 12f + seed + 200f) * 0.5f + 0.5f;
                        cityNoise       = Mathf.Pow(cityNoise, 6f);
                        pixels[y * res + x] = nightEmissionColor * cityNoise;
                    }
                    else pixels[y * res + x] = black;
                }
                else pixels[y * res + x] = black;
            }
            if (y % linesPerFrame == 0) yield return null;
        }

        tex.SetPixels(pixels);
        tex.Apply();

        if (surfaceMat != null)
        {
            surfaceMat.EnableKeyword("_EMISSION");
            surfaceMat.SetTexture("_EmissionMap", tex);
            surfaceMat.SetColor("_EmissionColor", nightEmissionColor);
        }
    }

    // Coroutine: gera a textura de nuvens distribuída por vários frames
    System.Collections.IEnumerator GenerateCloudsAsync()
    {
        int res = textureResolution;
        Texture2D tex    = new Texture2D(res, res, TextureFormat.RGBA32, false);
        Color[]   pixels = new Color[res * res];

        Color cloudColor = isIceGiant
            ? new Color(0.70f, 0.85f, 1.00f)
            : new Color(0.95f, 0.95f, 1.00f);

        int linesPerFrame = 8;
        for (int y = 0; y < res; y++)
        {
            for (int x = 0; x < res; x++)
            {
                float u       = (float)x / res;
                float v       = (float)y / res;
                float3 sphPos = UVToSphere(u, v);

                float cloud1  = noise.snoise(sphPos * 2.2f + seed + 300f) * 0.5f + 0.5f;
                float cloud2  = noise.snoise(sphPos * 4.5f + seed + 400f) * 0.5f + 0.5f;
                float cloud   = cloud1 * 0.65f + cloud2 * 0.35f;
                float alpha   = Mathf.Clamp01((cloud - (1f - cloudCoverage)) / 0.3f) * cloudOpacity;

                pixels[y * res + x] = new Color(cloudColor.r, cloudColor.g, cloudColor.b, alpha);
            }
            if (y % linesPerFrame == 0) yield return null;
        }

        tex.SetPixels(pixels);
        tex.Apply();
        tex.wrapMode = TextureWrapMode.Repeat;

        if (cloudMat != null) cloudMat.mainTexture = tex;
    }

    // Cria a esfera de nuvens como filho do planeta
    void BuildCloudSphere()
    {
        cloudSphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        cloudSphere.name = "Clouds";
        cloudSphere.transform.SetParent(transform, false);
        cloudSphere.transform.localScale = Vector3.one * 1.02f; // ligeiramente maior que o planeta

        // Remove o collider — as nuvens não devem interferir com física
        Destroy(cloudSphere.GetComponent<Collider>());

        // Remove do layer Minimap se existir — nuvens não aparecem no minimap
        int minimapLayer = LayerMask.NameToLayer("Minimap");
        if (minimapLayer >= 0) cloudSphere.layer = minimapLayer;

        // Textura gerada assincronamente em GenerateCloudsAsync — placeholder transparente por agora
        cloudMat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        cloudMat.SetFloat("_Smoothness", 0.1f);
        cloudMat.SetFloat("_Metallic",   0f);

        // Transparência nas nuvens — ativa o modo transparente do URP Lit
        cloudMat.SetFloat("_Surface",  1f); // 1 = Transparent
        cloudMat.SetFloat("_Blend",    0f); // Alpha blend
        cloudMat.SetOverrideTag("RenderType", "Transparent");
        cloudMat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
        cloudMat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");

        cloudSphere.GetComponent<MeshRenderer>().material = cloudMat;
        cloudSphere.GetComponent<MeshRenderer>().shadowCastingMode =
            UnityEngine.Rendering.ShadowCastingMode.Off;
    }

    // Cria a esfera de atmosfera com shader HLSL de rim light
    // O rim light calcula o ângulo entre a normal da superfície e a direção da câmara:
    // bordas perpendiculares à câmara ficam brilhantes, o centro fica transparente
    void BuildAtmosphereSphere()
    {
        atmosphereSphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        atmosphereSphere.name = "Atmosphere";
        atmosphereSphere.transform.SetParent(transform, false);
        atmosphereSphere.transform.localScale = Vector3.one * atmosphereScale;

        Destroy(atmosphereSphere.GetComponent<Collider>());

        // Shader HLSL inline — rim light genuíno baseado no ângulo normal/câmara
        // Parâmetros adaptados por tipo de planeta:
        // power alto = rim fino e concentrado (planetas rochosos)
        // power baixo = rim largo e difuso (gigantes de gelo)
        float rimPower     = starComponent.mass < 1.5f ? 8.0f    // rochoso pequeno — rim muito fino
                           : starComponent.mass < 6.0f ? 5.0f    // rochoso grande  — rim médio
                           : 3.0f;                                // gigante de gelo — rim largo

        float rimIntensity = starComponent.mass < 1.5f ? 0.4f    // atmosfera fina (Marte)
                           : starComponent.mass < 6.0f ? 0.7f    // atmosfera média (Terra)
                           : 1.1f;                                // atmosfera densa (Neptuno)

        atmosphereMat = CreateRimLightMaterial(atmosphereColor, rimPower, rimIntensity);

        MeshRenderer atmRend = atmosphereSphere.GetComponent<MeshRenderer>();
        atmRend.material          = atmosphereMat;
        atmRend.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        atmRend.receiveShadows    = false;
    }

    // Cria um material com o shader de rim light definido em PlanetRimLight.shader
    // O shader calcula: rimFactor = 1 - dot(normal, viewDir), elevado a rimPower
    // O resultado é um brilho concentrado nas bordas da esfera, transparente no centro
    // Funciona tanto no Editor como em builds standalone — o shader é incluído automaticamente
    // pelo Unity no build porque está na pasta Assets/Shaders do projeto
    Material CreateRimLightMaterial(Color rimColor, float rimPower, float rimIntensity)
    {
        Shader rimShader = Shader.Find("Custom/PlanetRimLight");
        if (rimShader == null)
        {
            Debug.LogWarning("[PlanetAppearance] Shader 'Custom/PlanetRimLight' não encontrado. " +
                             "Certifica-te que o ficheiro PlanetRimLight.shader está em Assets/Shaders/");
            rimShader = Shader.Find("Universal Render Pipeline/Unlit");
        }
        Material mat = new Material(rimShader);
        mat.SetColor("_RimColor",     rimColor);
        mat.SetFloat("_RimPower",     rimPower);
        mat.SetFloat("_RimIntensity", rimIntensity);
        return mat;
    }

    // ─── Geração de Texturas ─────────────────────────────────────────────────

    // Converte coordenadas UV para posição na esfera unitária
    // Evita a distorção que aconteceria com ruído 2D aplicado a UV plano
    float3 UVToSphere(float u, float v)
    {
        float theta = u * PI * 2f;          // longitude: 0 a 2π
        float phi   = v * PI;               // latitude:  0 a π
        return new float3(
            sin(phi) * cos(theta),
            cos(phi),
            sin(phi) * sin(theta)
        );
    }

    // Limpa as texturas e objetos criados ao destruir o planeta
    void OnDestroy()
    {
        if (surfaceMat      != null) Destroy(surfaceMat);
        if (cloudMat        != null) Destroy(cloudMat);
        if (atmosphereMat   != null) Destroy(atmosphereMat);
        if (cloudSphere     != null) Destroy(cloudSphere);
        if (atmosphereSphere!= null) Destroy(atmosphereSphere);
    }
}
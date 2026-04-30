using UnityEngine;
using System.Collections;

// Gere a sequência completa de absorção de um planeta por uma estrela.
// Sequência:
// Fase 1 — Ionização (~1.5s): rasto de plasma em direção à estrela + halo crescente
// Fase 2 — Vaporização (~0.5s): ondas de brilho na superfície, planeta encolhe
// Fase 3 — Impacto: flash intenso
// Fase 4 — Flare bipolar + onda de calor em paralelo
public class PlanetAbsorption : MonoBehaviour
{
    [Header("Espiral de Acreção")]
    [Tooltip("Duração total da espiral em segundos")]
    public float spiralDuration = 2.5f;
    [Tooltip("Expoente de decaimento do raio orbital")]
    public float spiralDecayRate = 2.2f;
    [Tooltip("Velocidade angular inicial em radianos/segundo")]
    public float initialAngularSpeed = 2.5f;

    [Header("Rasto de Plasma (Ionização)")]
    [Tooltip("Largura do rasto de plasma")]
    public float plasmaTrailWidth = 0.25f;
    [Tooltip("Duração do fade-in do halo de ionização")]
    public float ionizationFadeTime = 0.8f;

    [Header("Vaporização")]
    public float vaporizationDuration = 0.5f;
    [Tooltip("Número de pulsos de brilho durante a vaporização")]
    public int vaporizationPulses   = 3;

    [Header("Flare Bipolar")]
    public float flareDuration = 0.9f;
    public float flareLength = 10.0f;
    public float flareWidth = 0.35f;
    public float flareConeAngle = 8.0f;

    [Header("Onda de Calor")]
    public float waveDuration = 0.7f;
    public float waveMaxRadius = 6.0f;

    public void StartAbsorption(StarComponent planet, StarComponent star)
    {
        StartCoroutine(AbsorptionSequence(planet, star));
    }

    IEnumerator AbsorptionSequence(StarComponent planet, StarComponent star)
    {
        if (planet == null || star == null) yield break;

        Color starColor  = star.GetComponent<Renderer>()?.material.color ?? Color.white;
        float starRadius = star.transform.localScale.x * 0.5f;

        // Se a câmara está em modo Follow a seguir este planeta, sai para voo livre
        // para evitar que a câmara fique presa no ponto de destruição
        CameraManager camManager = FindFirstObjectByType<CameraManager>();
        if (camManager != null && camManager.CurrentMode == CameraManager.CameraMode.Follow)
        {
            StarFollowCamera followCam = FindFirstObjectByType<StarFollowCamera>();
            // Verifica se o planeta absorvido é o que está a ser seguido
            if (followCam != null && followCam.IsFollowing(planet))
                camManager.SetMode(CameraManager.CameraMode.Fly);
        }

        // Fase 1+2: Espiral com ionização e vaporização
        yield return StartCoroutine(SpiralWithEffects(planet, star, starColor, starRadius));

        if (planet == null || star == null) yield break;

        // Fase 3: Flash de impacto
        Vector3 impactPos = planet != null ? planet.transform.position : star.transform.position;
        yield return StartCoroutine(ImpactFlash(planet));

        if (star == null) yield break;

        // Fase 4: Flare + onda em paralelo
        StartCoroutine(BipolarFlare(star.transform.position, starColor, starRadius));
        StartCoroutine(HeatWave(star.transform, starColor, starRadius));
        star.StartCoroutine(star.AbsorptionPulse());
    }

    // Espiral com efeitos de ionização e vaporização
    IEnumerator SpiralWithEffects(StarComponent planet, StarComponent star, Color starColor, float starRadius)
    {
        float elapsed = 0f;
        Vector3 toStar = star.transform.position - planet.transform.position;
        float initialRadius = toStar.magnitude;
        Vector3 originalScale = planet.transform.localScale;
        Renderer planetRend = planet.GetComponent<Renderer>();
        Color originalColor = planetRend != null ? planetRend.material.color : Color.white;

        // Calcula o plano orbital robusto a partir da velocidade real do planeta
        // Se a velocidade for paralela a toStar (queda direta), usa o plano da velocidade
        Vector3 planetVelocity = planet.velocity;
        Vector3 toStarDir = toStar.normalized;

        // Produto externo entre a direção à estrela e a velocidade do planeta
        Vector3 orbitNormal = Vector3.Cross(toStarDir, planetVelocity.normalized);

        // Se o produto externo for muito pequeno (queda quase direta ou velocidade nula),
        // constrói um plano perpendicular à direção de queda usando a velocidade como referência
        if (orbitNormal.sqrMagnitude < 0.01f)
        {
            // Encontra um vetor perpendicular a toStarDir que não seja paralelo a ele
            Vector3 arbitrary = Mathf.Abs(Vector3.Dot(toStarDir, Vector3.up)) < 0.9f
                ? Vector3.up : Vector3.right;
            orbitNormal = Vector3.Cross(toStarDir, arbitrary).normalized;
        }
        else
        {
            orbitNormal = orbitNormal.normalized;
        }

        // Vetores do plano orbital — radial aponta do planeta para a estrela
        // tangent é perpendicular no plano orbital, na direção da velocidade projetada
        Vector3 radialDir  = toStarDir;
        Vector3 tangentDir = Vector3.Cross(orbitNormal, radialDir).normalized;

        // Garante que a tangente está alinhada com a componente da velocidade no plano orbital
        // (decide sentido horário ou anti-horário da órbita)
        Vector3 velInPlane = Vector3.ProjectOnPlane(planetVelocity, orbitNormal);
        if (Vector3.Dot(velInPlane, tangentDir) < 0f)
            tangentDir = -tangentDir;

        // Ângulo inicial — começa na posição atual do planeta
        float angle = 0f;

        // Rasto de plasma — usa o TrailRenderer já existente no prefab se houver,
        // ou cria um novo num GameObject filho para evitar duplicação
        TrailRenderer plasmaTrail = null;
        TrailRenderer existingTrail = planet.GetComponent<TrailRenderer>();
        if (existingTrail != null)
        {
            // Reutiliza o trail existente — apenas atualiza a cor para a da estrela
            plasmaTrail = existingTrail;
        }
        else
        {
            // Cria num filho para não conflituar com outros componentes
            GameObject trailGO = new GameObject("PlasmaTrail");
            trailGO.transform.SetParent(planet.transform, false);
            plasmaTrail = trailGO.AddComponent<TrailRenderer>();
        }

        plasmaTrail.time= 0.4f;
        plasmaTrail.startWidth = plasmaTrailWidth * originalScale.x;
        plasmaTrail.endWidth = 0f;
        plasmaTrail.minVertexDistance = 0.1f;

        // Material do rasto — cor da estrela com gradiente de alpha
        Material trailMat = new Material(Shader.Find("Universal Render Pipeline/Unlit")
                                      ?? Shader.Find("Unlit/Color"));
        trailMat.color = starColor;
        trailMat.SetColor("_BaseColor", starColor);
        plasmaTrail.material = trailMat;

        // Gradiente do rasto: cor da estrela → transparente
        Gradient trailGradient = new Gradient();
        trailGradient.SetKeys(
            new GradientColorKey[] {
                new GradientColorKey(starColor, 0f),
                new GradientColorKey(starColor * 0.5f, 1f)
            },
            new GradientAlphaKey[] {
                new GradientAlphaKey(0.9f, 0f),
                new GradientAlphaKey(0f, 1f)
            }
        );
        plasmaTrail.colorGradient = trailGradient;

        // Sistema de partículas — gases e poeira
        // Fase inicial: partículas radiais (gases a escapar)
        // Fase final: partículas fluem para a estrela (acreção)
        ParticleSystem gasParticles = CreateGasParticleSystem(planet, starColor, originalScale.x);

        // Halo de ionização — esfera de atmosfera temporária que cresce com a proximidade
        // Usa a mesh da esfera padrão do Unity se o MeshFilter do planeta não estiver disponível
        GameObject haloGO  = new GameObject("IonizationHalo");
        haloGO.transform.SetParent(planet.transform, false);
        haloGO.transform.localScale = Vector3.one * 1.05f;
        MeshFilter haloMF = haloGO.AddComponent<MeshFilter>();
        MeshRenderer haloRend = haloGO.AddComponent<MeshRenderer>();
        MeshFilter planetMF = planet.GetComponent<MeshFilter>();
        haloMF.mesh = planetMF != null ? planetMF.sharedMesh : Resources.GetBuiltinResource<Mesh>("Sphere.fbx");

        Material haloMat = new Material(Shader.Find("Custom/PlanetRimLight")
                                     ?? Shader.Find("Universal Render Pipeline/Unlit"));
        haloMat.SetColor("_RimColor", starColor);
        haloMat.SetFloat("_RimPower", 4.0f);
        haloMat.SetFloat("_RimIntensity", 0f); // começa invisível
        haloMat.SetFloat("_Surface", 1f);
        haloMat.SetFloat("_Blend", 0f);
        haloMat.SetOverrideTag("RenderType", "Transparent");
        haloMat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent + 1;
        haloMat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        haloMat.SetInt("_Cull", (int)UnityEngine.Rendering.CullMode.Front);
        haloRend.material= haloMat;
        haloRend.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

        while (elapsed < spiralDuration && planet != null && star != null)
        {
            float t = elapsed / spiralDuration;
            float radius = initialRadius * Mathf.Exp(-spiralDecayRate * t * t);

            if (radius <= starRadius) break;

            // Velocidade angular cresce com 1/r² (conservação de momento angular)
            float angularSpeed = initialAngularSpeed * (initialRadius * initialRadius)
                                 / (radius * radius);
            angle += angularSpeed * Time.deltaTime;

            // Posição no plano orbital real do planeta (não forçado no horizontal)
            Vector3 offset = (radialDir * Mathf.Cos(angle) + tangentDir * Mathf.Sin(angle)) * radius;
            planet.transform.position = star.transform.position - offset;

            // Intensidade do halo cresce com a proximidade — ionização mais intensa perto da estrela
            float proximityFactor = Mathf.Clamp01(1f - (radius - starRadius) / (initialRadius - starRadius));
            float haloIntensity = Mathf.Pow(proximityFactor, 2f) * 1.5f;
            haloMat.SetFloat("_RimIntensity", haloIntensity);

            // Cor do planeta vai gradualmente para a cor da estrela (aquecimento)
            if (planetRend != null && proximityFactor > 0.4f)
            {
                float colorBlend = Mathf.Clamp01((proximityFactor - 0.4f) / 0.6f);
                Color heatedColor = Color.Lerp(originalColor, starColor * 1.5f, colorBlend * 0.7f);
                planetRend.material.color = heatedColor;
                planetRend.material.SetColor("_BaseColor", heatedColor);

                // Emissão crescente — planeta começa a brilhar pelo aquecimento
                planetRend.material.EnableKeyword("_EMISSION");
                planetRend.material.SetColor("_EmissionColor", starColor * colorBlend * 2f);
            }

            // Atualiza as partículas:
            // proximityFactor 0→0.5: gases radiais (escapam do planeta)
            // proximityFactor 0.5→1: fluem para a estrela (acreção)
            if (gasParticles != null && star != null && planet != null)
                UpdateGasParticles(gasParticles, planet.transform.position,
                                   star.transform.position, proximityFactor, starColor, originalScale.x);

            elapsed += Time.deltaTime;
            yield return null;
        }

        // Limpa o halo, o rasto e as partículas
        if (haloGO != null) Destroy(haloGO);
        if (gasParticles != null)
        {
            gasParticles.Stop();
            Destroy(gasParticles.gameObject, 1.5f); // espera que as partículas restantes morram
        }

        // Fase de vaporização — pulsos de brilho antes do impacto
        if (planet != null)
            yield return StartCoroutine(VaporizationPhase(planet, starColor, originalScale));

        // Limpa o rasto de plasma — só destrói se foi criado num filho (não o trail original do prefab)
        if (plasmaTrail != null && plasmaTrail.transform.parent == planet?.transform
            && plasmaTrail.gameObject.name == "PlasmaTrail")
            Destroy(plasmaTrail.gameObject);
        else if (plasmaTrail != null && plasmaTrail == planet?.GetComponent<TrailRenderer>())
            plasmaTrail.Clear(); // limpa o trail original sem destruir
    }

    // Vaporização — ondas de brilho + encolhimento
    IEnumerator VaporizationPhase(StarComponent planet, Color starColor, Vector3 originalScale)
    {
        if (planet == null) yield break;

        Renderer rend = planet.GetComponent<Renderer>();
        float elapsed = 0f;

        while (elapsed < vaporizationDuration && planet != null)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / vaporizationDuration;

            // Encolhe progressivamente
            planet.transform.localScale = Vector3.Lerp(originalScale, originalScale * 0.3f, t);

            // Pulsos de brilho — frequência aumenta com o tempo
            if (rend != null)
            {
                float pulseFreq = vaporizationPulses * 2f * Mathf.PI;
                float pulseValue = (Mathf.Sin(t * pulseFreq) * 0.5f + 0.5f) * (1f - t * 0.5f);
                rend.material.SetColor("_EmissionColor", starColor * (2f + pulseValue * 4f));
            }

            yield return null;
        }
    }

    // Cria o sistema de partículas de gases/poeira
    // Fase inicial (radial): gases escapam da atmosfera em todas as direções
    // Fase final (acreção): partículas são atraídas para a estrela
    ParticleSystem CreateGasParticleSystem(StarComponent planet, Color starColor, float planetRadius)
    {
        GameObject psGO = new GameObject("GasParticles");
        psGO.transform.SetParent(planet.transform, false);

        ParticleSystem ps = psGO.AddComponent<ParticleSystem>();
        ParticleSystem.MainModule main = ps.main;
        main.loop = true;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.4f, 0.9f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.5f, 2.0f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.03f, 0.12f);
        main.startColor = new ParticleSystem.MinMaxGradient(
            new Color(starColor.r * 0.8f, starColor.g * 0.6f, starColor.b * 0.4f, 0.8f), // poeira
            new Color(starColor.r, starColor.g * 0.9f, starColor.b * 0.7f, 0.6f)// gases quentes
        );
        main.simulationSpace= ParticleSystemSimulationSpace.World;
        main.maxParticles = 800;

        // Emissão — começa lenta e acelera com a proximidade
        ParticleSystem.EmissionModule emission = ps.emission;
        emission.rateOverTime = 45f;

        // Forma esférica — partículas saem da superfície do planeta
        ParticleSystem.ShapeModule shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = planetRadius * 0.6f;

        // Cor desvanece ao longo da vida
        ParticleSystem.ColorOverLifetimeModule col = ps.colorOverLifetime;
        col.enabled = true;
        Gradient g = new Gradient();
        g.SetKeys(
            new GradientColorKey[] {
                new GradientColorKey(new Color(starColor.r, starColor.g * 0.8f, starColor.b * 0.5f), 0f),
                new GradientColorKey(new Color(starColor.r * 0.5f, starColor.g * 0.3f, starColor.b * 0.2f), 1f)
            },
            new GradientAlphaKey[] {
                new GradientAlphaKey(0.8f, 0f),
                new GradientAlphaKey(0f, 1f)
            }
        );
        col.color = new ParticleSystem.MinMaxGradient(g);

        // Renderer das partículas
        ParticleSystemRenderer psr = ps.GetComponent<ParticleSystemRenderer>();
        Material psMat = new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit")
                                   ?? Shader.Find("Universal Render Pipeline/Unlit")
                                   ?? Shader.Find("Unlit/Color"));
        psMat.color = starColor;
        psMat.SetColor("_BaseColor", starColor);
        psr.material = psMat;

        ps.Play();
        return ps;
    }

    // Atualiza o sistema de partículas conforme a proximidade à estrela:
    // proximityFactor 0→0.5: gases radiais (escapam do planeta em todas as direções)
    // proximityFactor 0.5→1: partículas fluem para a estrela (acreção — força externa)
    void UpdateGasParticles(ParticleSystem ps, Vector3 planetPos, Vector3 starPos,
                            float proximityFactor, Color starColor, float planetRadius)
    {
        ParticleSystem.MainModule main = ps.main;

        // Taxa de emissão cresce com a proximidade
        ParticleSystem.EmissionModule emission = ps.emission;
        emission.rateOverTime = Mathf.Lerp(45f, 250f, proximityFactor);

        // Velocidade inicial cresce — mais gases escapam com mais energia
        main.startSpeed = new ParticleSystem.MinMaxCurve(
            Mathf.Lerp(0.5f, 2.0f, proximityFactor),
            Mathf.Lerp(2.0f, 5.0f, proximityFactor)
        );

        if (proximityFactor > 0.5f)
        {
            // Fase de acreção: força externa atrai partículas para a estrela
            ParticleSystem.VelocityOverLifetimeModule vel = ps.velocityOverLifetime;
            vel.enabled = true;
            vel.space = ParticleSystemSimulationSpace.World;

            Vector3 toStar = (starPos - planetPos).normalized;
            float pullForce = Mathf.Lerp(0f, 8f, (proximityFactor - 0.5f) * 2f);

            vel.x = new ParticleSystem.MinMaxCurve(toStar.x * pullForce);
            vel.y = new ParticleSystem.MinMaxCurve(toStar.y * pullForce);
            vel.z = new ParticleSystem.MinMaxCurve(toStar.z * pullForce);

            // Cor fica mais quente (mais próxima da cor da estrela) na fase de acreção
            main.startColor = new ParticleSystem.MinMaxGradient(
                Color.Lerp(new Color(starColor.r * 0.8f, starColor.g * 0.6f, 0.3f), starColor, (proximityFactor - 0.5f) * 2f)
            );
        }
    }

    // Flash de impacto
    IEnumerator ImpactFlash(StarComponent planet)
    {
        if (planet == null) yield break;

        float duration = 0.2f;
        float elapsed  = 0f;
        Renderer rend = planet.GetComponent<Renderer>();

        while (elapsed < duration && planet != null)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;

            planet.transform.localScale = Vector3.Lerp(
                planet.transform.localScale, Vector3.zero, t * t);

            if (rend != null)
            {
                rend.material.color = Color.white;
                rend.material.SetColor("_BaseColor", Color.white);
                rend.material.SetColor("_EmissionColor", Color.white * (1f - t) * 8f);
            }
            yield return null;
        }

        if (planet != null) Destroy(planet.gameObject);
    }

    // Flare bipolar — dois jatos opostos na cor da estrela
    IEnumerator BipolarFlare(Vector3 starPos, Color starColor, float starRadius)
    {
        Vector3 polarAxis = (Vector3.up
            + Random.insideUnitSphere * Mathf.Sin(flareConeAngle * Mathf.Deg2Rad)).normalized;

        Shader unlitShader = Shader.Find("Universal Render Pipeline/Unlit")
                          ?? Shader.Find("Unlit/Color");

        LineRenderer[] jets   = new LineRenderer[2];
        Vector3[]      dirs   = { polarAxis, -polarAxis };

        for (int i = 0; i < 2; i++)
        {
            GameObject go = new GameObject($"FlareJet_{i}");
            LineRenderer lr = go.AddComponent<LineRenderer>();
            lr.positionCount = 2;
            lr.useWorldSpace = true;

            Material mat = new Material(unlitShader);
            mat.color = starColor;
            mat.SetColor("_BaseColor", starColor);
            lr.material = mat;
            lr.startWidth = flareWidth * starRadius;
            lr.endWidth = 0f;
            jets[i] = lr;
        }

        float elapsed = 0f;
        while (elapsed < flareDuration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / flareDuration;
            float growT = Mathf.Clamp01(t / 0.25f);
            float fadeT = Mathf.Clamp01((t - 0.25f) / 0.75f);
            float length = flareLength * starRadius * growT * (1f - fadeT * fadeT);
            float alpha = growT * (1f - fadeT);

            for (int i = 0; i < 2; i++)
            {
                if (jets[i] == null) continue;
                jets[i].SetPosition(0, starPos + dirs[i] * starRadius);
                jets[i].SetPosition(1, starPos + dirs[i] * (starRadius + length));
                Color c = new Color(starColor.r, starColor.g, starColor.b, alpha);
                jets[i].material.color = c;
                jets[i].material.SetColor("_BaseColor", c);
                jets[i].startWidth = flareWidth * starRadius * (1f - fadeT * 0.5f);
            }
            yield return null;
        }

        foreach (var jet in jets)
            if (jet != null) Destroy(jet.gameObject);
    }

    // Onda de calor — círculo expansivo na superfície da estrela
    IEnumerator HeatWave(Transform starTransform, Color starColor, float starRadius)
    {
        GameObject waveGO = new GameObject("HeatWave");
        LineRenderer lr = waveGO.AddComponent<LineRenderer>();
        int segs = 48;
        lr.positionCount = segs + 1;
        lr.useWorldSpace = true;
        lr.loop = true;

        Shader unlitShader = Shader.Find("Universal Render Pipeline/Unlit")
                          ?? Shader.Find("Unlit/Color");
        Material mat = new Material(unlitShader);
        mat.color    = Color.white;
        mat.SetColor("_BaseColor", Color.white);
        lr.material  = mat;

        float elapsed = 0f;
        while (elapsed < waveDuration && starTransform != null)
        {
            elapsed += Time.deltaTime;
            float t= elapsed / waveDuration;
            float radius = starRadius + waveMaxRadius * starRadius * t;
            float alpha  = 1f - t;

            Vector3 center = starTransform.position;
            for (int i = 0; i <= segs; i++)
            {
                float a = (float)i / segs * Mathf.PI * 2f;
                lr.SetPosition(i, center + new Vector3(Mathf.Cos(a) * radius, 0f, Mathf.Sin(a) * radius));
            }

            Color waveColor = new Color(starColor.r, starColor.g, starColor.b, alpha);
            lr.startWidth = 0.15f * (1f - t * 0.7f);
            lr.endWidth= lr.startWidth;
            mat.color= waveColor;
            mat.SetColor("_BaseColor", waveColor);
            yield return null;
        }

        Destroy(waveGO);
    }
}
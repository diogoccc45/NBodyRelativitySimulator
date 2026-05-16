using UnityEngine;
using System.Collections;

// Gere colisões na cena de Relatividade Geral
// Comportamentos simples mas visualmente dramáticos:
// 1. Planeta + Estrela — absorção com pulso de brilho
// 2. Estrela + Estrela — kilonova com flash, shockwave dupla e crescimento
// 3. Buraco Negro + Estrela — absorção com jatos relativísticos intensos
// 4. Planeta + Planeta — fusão simples - já temos mais modos no laboratório manual, aqui vou priorizar a grid, a relatividade
[RequireComponent(typeof(RelativityManager))]
public class RelativityCollision : MonoBehaviour
{
    [Header("Referências")]
    public SpacetimeGrid grid;
    public GravitationalWaves gravitationalWaves;

    [Header("Configuração")]
    [Tooltip("Distância mínima entre dois corpos para despoletar colisão")]
    public float collisionDistance = 4f;
    [Tooltip("Multiplicador do tamanho das partículas do Tidal Disruption Event")]
    public float tidalParticleScale = 0.4f;

    private RelativityManager manager;
    private System.Collections.Generic.HashSet<int> processingPairs =
        new System.Collections.Generic.HashSet<int>();

    void Awake()
    {
        manager = GetComponent<RelativityManager>();
    }

    void Update()
    {
        CheckCollisions();
    }

    void CheckCollisions()
    {
        var bodies = new System.Collections.Generic.List<RelativityBody>();
        foreach (Transform child in manager.transform)
        {
            RelativityBody b = child.GetComponent<RelativityBody>();
            if (b != null) bodies.Add(b);
        }

        for (int i = 0; i < bodies.Count; i++)
        {
            if (bodies[i] == null) continue;
            for (int j = i + 1; j < bodies.Count; j++)
            {
                if (bodies[j] == null) continue;

                int pairKey = bodies[i].GetInstanceID() ^ bodies[j].GetInstanceID();
                if (processingPairs.Contains(pairKey)) continue;

                float dx = bodies[i].transform.position.x - bodies[j].transform.position.x;
                float dz = bodies[i].transform.position.z - bodies[j].transform.position.z;
                float dist = Mathf.Sqrt(dx * dx + dz * dz);

                float combinedRadius = (bodies[i].transform.localScale.x +
                                        bodies[j].transform.localScale.x) * 0.5f;
                float threshold = Mathf.Max(collisionDistance, combinedRadius);

                if (dist > threshold) continue;

                processingPairs.Add(pairKey);
                ProcessCollision(bodies[i], bodies[j], pairKey);
            }
        }
    }

    void ProcessCollision(RelativityBody a, RelativityBody b, int pairKey)
    {
        // Planeta + Massa pesada
        if (!a.deformsGrid && b.deformsGrid)
        {
            StartCoroutine(AbsorbLightBody(a, b, pairKey));
            return;
        }
        if (a.deformsGrid && !b.deformsGrid)
        {
            StartCoroutine(AbsorbLightBody(b, a, pairKey));
            return;
        }

        // Estrela + Estrela ou Buraco Negro
        if (a.deformsGrid && b.deformsGrid)
        {
            bool aIsBlackHole = a.GetComponent<BlackHoleBody>() != null;
            bool bIsBlackHole = b.GetComponent<BlackHoleBody>() != null;

            if (aIsBlackHole || bIsBlackHole)
            {
                RelativityBody blackHole = aIsBlackHole ? a : b;
                RelativityBody star = aIsBlackHole ? b : a;
                StartCoroutine(AbsorbHeavyBody(star, blackHole, pairKey));
            }
            else
            {
                RelativityBody larger  = a.mass >= b.mass ? a : b;
                RelativityBody smaller = a.mass <  b.mass ? a : b;
                StartCoroutine(MergeStars(smaller, larger, pairKey));
            }
            return;
        }

        // Planeta + Planeta
        if (!a.deformsGrid && !b.deformsGrid)
        {
            RelativityBody larger = a.mass >= b.mass ? a : b;
            RelativityBody smaller = a.mass <  b.mass ? a : b;
            StartCoroutine(MergeLightBodies(smaller, larger, pairKey));
        }
    }

    // Planeta absorvido por estrela
    // Pulso de brilho na estrela + onda gravitacional suave
    IEnumerator AbsorbLightBody(RelativityBody planet, RelativityBody star, int pairKey)
    {
        if (planet == null) { processingPairs.Remove(pairKey); yield break; }

        planet.enabled = false;
        float duration = 0.8f;
        float elapsed = 0f;
        Vector3 startScale = planet.transform.localScale;
        Renderer rend = planet.GetComponent<Renderer>();
        Color startColor = rend != null ? rend.material.color : Color.white;

        while (elapsed < duration)
        {
            if (planet == null) break;
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            if (star != null)
                planet.transform.position = Vector3.Lerp(
                    planet.transform.position, star.transform.position, t * t);
            planet.transform.localScale = Vector3.Lerp(startScale, Vector3.zero, t);
            if (rend != null)
            {
                Color hot = Color.Lerp(startColor, Color.white, t);
                rend.material.color = hot;
                rend.material.SetColor("_EmissionColor", hot * Mathf.Lerp(1f, 8f, t));
                rend.material.EnableKeyword("_EMISSION");
            }
            yield return null;
        }

        if (star != null)
        {
            star.mass += planet.mass * 0.1f;
            StarComponent sc = star.GetComponent<StarComponent>();
            if (sc != null) { sc.mass = star.mass; sc.UpdateAppearance(); }
            StartCoroutine(StarBrightnessPulse(star, 1.5f, 0.6f));
            if (gravitationalWaves != null)
                gravitationalWaves.SpawnWave(star.transform.position, planet.mass * 2f);
        }

        if (planet != null) Destroy(planet.gameObject);
        processingPairs.Remove(pairKey);
    }

    // Estrela absorvida por buraco negro — Tidal Disruption Event
    // A estrela é destruída pelas forças de maré antes de cruzar o horizonte
    IEnumerator AbsorbHeavyBody(RelativityBody star, RelativityBody blackHole, int pairKey)
    {
        if (star == null) { processingPairs.Remove(pairKey); yield break; }

        star.enabled = false;
        Renderer rend = star.GetComponent<Renderer>();
        Vector3 startScale = star.transform.localScale;
        Color startColor = rend != null ? rend.material.color : Color.white;
        Vector3 startPos = star.transform.position;

        // Fase 1: Spaghettification muito exagerada
        float tdeDuration = 2.5f;
        float elapsed = 0f;

        while (elapsed < tdeDuration)
        {
            if (star == null) break;
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / tdeDuration);

            // Estica MUITO radialmente — visível mesmo no fundo do fosso
            float spaghetti = Mathf.Lerp(1f, 0.02f, t * t); // quase invisível lateralmente
            float stretch = Mathf.Lerp(1f, 12.0f, t * t); // 12x mais longo

            if (blackHole != null)
            {
                Vector3 radialDir = (blackHole.transform.position - star.transform.position).normalized;
                star.transform.rotation = radialDir != Vector3.zero
                    ? Quaternion.LookRotation(radialDir) : Quaternion.identity;

                // Move gradualmente para o buraco negro
                star.transform.position = Vector3.Lerp(startPos,
                    blackHole.transform.position, t * t * 0.8f);
            }

            star.transform.localScale = new Vector3(
                startScale.x * spaghetti,
                startScale.y * spaghetti,
                startScale.z * stretch);

            // Aquece intensamente — plasma a milhões de graus
            if (rend != null)
            {
                Color hot = Color.Lerp(startColor, new Color(0.6f, 0.85f, 1.0f), t);
                rend.material.color = hot;
                rend.material.SetColor("_EmissionColor", hot * Mathf.Lerp(6f, 40f, t));
                rend.material.EnableKeyword("_EMISSION");
            }
            yield return null;
        }

        if (star == null) { processingPairs.Remove(pairKey); yield break; }

        // Fase 2: Dispersão em espiral luminosa
        if (blackHole != null)
            StartCoroutine(TidalSpiral(star.transform.position,
                blackHole.transform.position, startColor, startScale.x));

        // Fase 3: Shockwave muito intensa
        if (gravitationalWaves != null && blackHole != null)
        {
            gravitationalWaves.SpawnShockwave(blackHole.transform.position, star.mass * 4f);
            StartCoroutine(DelayedShockwave(blackHole.transform.position, star.mass * 2f, 0.4f));
        }

        if (star != null) Destroy(star.gameObject);
        processingPairs.Remove(pairKey);
    }

    // Espiral de material da estrela destruída — partículas grandes e brilhantes
    IEnumerator TidalSpiral(Vector3 origin, Vector3 blackHolePos, Color starColor, float starRadius)
    {
        int particleCount = 12;
        float duration = 2.0f;
        float elapsed = 0f;

        // Partículas grandes — visíveis mesmo no fundo do fosso
        float particleSize = Mathf.Max(starRadius * tidalParticleScale, 0.3f);

        GameObject[] particles = new GameObject[particleCount];
        float[] angles = new float[particleCount];
        for (int i = 0; i < particleCount; i++)
        {
            angles[i] = (float)i / particleCount * 360f;
            particles[i] = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            particles[i].transform.position = origin;
            particles[i].transform.localScale = Vector3.one * particleSize;
            Destroy(particles[i].GetComponent<Collider>());
            Material mat = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
            Color bright = starColor * 3f;
            mat.color = bright;
            mat.SetColor("_BaseColor", bright);
            mat.EnableKeyword("_EMISSION");
            mat.SetColor("_EmissionColor", bright * 8f);
            particles[i].GetComponent<Renderer>().material = mat;
        }

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);

            for (int i = 0; i < particleCount; i++)
            {
                if (particles[i] == null) continue;

                // Espiral — acelera e raio diminui rapidamente
                angles[i] += Time.deltaTime * Mathf.Lerp(180f, 600f, t);
                float radius = Mathf.Lerp(starRadius * 3f, 0.3f, t * t);

                float rad = angles[i] * Mathf.Deg2Rad;
                Vector3 pos = blackHolePos + new Vector3(
                    Mathf.Cos(rad) * radius, 0f, Mathf.Sin(rad) * radius);

                if (grid != null)
                    pos.y = grid.GetGridHeightAt(pos.x, pos.z) + particleSize * 0.5f;

                particles[i].transform.position = pos;

                // Encolhe na segunda metade
                float scale = t < 0.5f ? particleSize : Mathf.Lerp(particleSize, 0f, (t - 0.5f) * 2f);
                particles[i].transform.localScale = Vector3.one * scale;

                // Aquece para branco
                Color c = Color.Lerp(starColor * 3f, Color.white * 6f, t);
                Renderer r = particles[i].GetComponent<Renderer>();
                if (r != null)
                {
                    r.material.color = c;
                    r.material.SetColor("_EmissionColor", c * Mathf.Lerp(8f, 0f, t * t));
                }
            }
            yield return null;
        }

        foreach (var p in particles)
            if (p != null) Destroy(p);
    }

    // Fusão estrela + estrela — Kilonova
    // Flash azul-branco + shockwave dupla + pulso de crescimento dramático
    IEnumerator MergeStars(RelativityBody smaller, RelativityBody larger, int pairKey)
    {
        if (smaller == null || larger == null)
        {
            processingPairs.Remove(pairKey);
            yield break;
        }

        smaller.enabled = false;
        float duration = 1.0f;
        float elapsed = 0f;
        Vector3 startScale = smaller.transform.localScale;
        Renderer rendSmall = smaller.GetComponent<Renderer>();
        Renderer rendLarge = larger.GetComponent<Renderer>();

        while (elapsed < duration)
        {
            if (smaller == null) break;
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            smaller.transform.position = Vector3.Lerp(
                smaller.transform.position, larger.transform.position, t * t);
            smaller.transform.localScale = Vector3.Lerp(startScale, Vector3.zero, t);
            // Ambas aqueceem ao aproximar-se
            if (rendSmall != null)
                rendSmall.material.SetColor("_EmissionColor",
                    Color.white * Mathf.Lerp(4f, 16f, t));
            if (rendLarge != null)
                rendLarge.material.SetColor("_EmissionColor",
                    Color.white * Mathf.Lerp(4f, 16f, t));
            yield return null;
        }

        if (larger == null) { processingPairs.Remove(pairKey); yield break; }

        // Kilonova — flash azul-branco muito brilhante
        StartCoroutine(KilonovaFlash(larger.transform.position,
            larger.transform.localScale.x * 4f));

        larger.mass += smaller.mass;
        StarComponent sc = larger.GetComponent<StarComponent>();
        if (sc != null) { sc.mass = larger.mass; sc.UpdateAppearance(); }

        // Shockwave dupla — onda principal + eco
        if (gravitationalWaves != null)
        {
            gravitationalWaves.SpawnShockwave(larger.transform.position, larger.mass * 2f);
            StartCoroutine(DelayedShockwave(larger.transform.position, larger.mass, 0.3f));
        }

        // Crescimento dramático — estrela fica visivelmente maior
        StartCoroutine(GrowPulse(larger, 1.7f));

        if (smaller != null) Destroy(smaller.gameObject);
        processingPairs.Remove(pairKey);
    }

    // Fusão planeta + planeta
    IEnumerator MergeLightBodies(RelativityBody smaller, RelativityBody larger, int pairKey)
    {
        if (smaller == null || larger == null)
        {
            processingPairs.Remove(pairKey);
            yield break;
        }

        smaller.enabled = false;
        float duration = 0.5f;
        float elapsed = 0f;
        Vector3 startScale = smaller.transform.localScale;

        while (elapsed < duration)
        {
            if (smaller == null) break;
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            smaller.transform.position = Vector3.Lerp(
                smaller.transform.position, larger.transform.position, t * t);
            smaller.transform.localScale = Vector3.Lerp(startScale, Vector3.zero, t);
            yield return null;
        }

        if (larger != null)
        {
            larger.mass += smaller.mass;
            StarComponent sc = larger.GetComponent<StarComponent>();
            if (sc != null) { sc.mass = larger.mass; sc.UpdateAppearance(); }
            if (gravitationalWaves != null)
                gravitationalWaves.SpawnWave(larger.transform.position, larger.mass);
            StartCoroutine(StarBrightnessPulse(larger, 1.3f, 0.4f));
        }

        if (smaller != null) Destroy(smaller.gameObject);
        processingPairs.Remove(pairKey);
    }

    // Efeitos visuais

    // Flash de kilonova — esfera azul-branca que expande e desaparece
    IEnumerator KilonovaFlash(Vector3 position, float maxRadius)
    {
        GameObject flash = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        flash.transform.position = position;
        Destroy(flash.GetComponent<Collider>());
        Material mat = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
        mat.color = new Color(0.7f, 0.9f, 1.0f);
        mat.SetColor("_BaseColor", new Color(0.7f, 0.9f, 1.0f));
        flash.GetComponent<Renderer>().material = mat;

        float duration = 1.2f;
        float elapsed  = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            float ease = 1f - Mathf.Pow(1f - t, 2f);
            flash.transform.localScale = Vector3.one * Mathf.Lerp(0f, maxRadius, ease);
            float alpha = t < 0.35f ? 1f : Mathf.Lerp(1f, 0f, (t - 0.35f) / 0.65f);
            Color c = new Color(0.7f, 0.9f, 1.0f) * (1f + (1f - t) * 4f);
            mat.color = new Color(c.r, c.g, c.b, alpha);
            mat.SetColor("_BaseColor", mat.color);
            yield return null;
        }
        Destroy(flash);
    }

    // Jatos relativísticos — dois LineRenderers perpendiculares ao plano
    IEnumerator SpawnJets(Vector3 origin, float width, float duration)
    {
        GameObject jetUp   = CreateJet(origin,  Vector3.up,  width);
        GameObject jetDown = CreateJet(origin, -Vector3.up,  width);
        float maxLength = width * 22f;
        float elapsed   = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            float ease = 1f - Mathf.Pow(1f - t, 2f);
            float length = Mathf.Lerp(0f, maxLength, ease);
            float alpha = t < 0.5f ? 1f : Mathf.Lerp(1f, 0f, (t - 0.5f) / 0.5f);
            UpdateJet(jetUp, origin, Vector3.up, length, alpha, width);
            UpdateJet(jetDown, origin, -Vector3.up, length, alpha, width);
            yield return null;
        }
        Destroy(jetUp);
        Destroy(jetDown);
    }

    GameObject CreateJet(Vector3 origin, Vector3 dir, float width)
    {
        GameObject go = new GameObject("Jet");
        LineRenderer lr = go.AddComponent<LineRenderer>();
        lr.positionCount = 2;
        lr.useWorldSpace = true;
        lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        lr.numCapVertices = 8;
        Material mat = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
        mat.color = new Color(0.5f, 0.8f, 1.0f);
        mat.SetColor("_BaseColor", mat.color);
        lr.material = mat;
        Gradient grad = new Gradient();
        grad.SetKeys(
            new GradientColorKey[] {
                new GradientColorKey(Color.white, 0.0f),
                new GradientColorKey(new Color(0.5f, 0.8f, 1.0f), 0.4f),
                new GradientColorKey(new Color(0.2f, 0.5f, 1.0f), 1.0f)
            },
            new GradientAlphaKey[] {
                new GradientAlphaKey(1.0f, 0.0f),
                new GradientAlphaKey(0.8f, 0.5f),
                new GradientAlphaKey(0.0f, 1.0f)
            });
        lr.colorGradient = grad;
        lr.SetPosition(0, origin);
        lr.SetPosition(1, origin);
        return go;
    }

    void UpdateJet(GameObject jet, Vector3 origin, Vector3 dir,
                   float length, float alpha, float width)
    {
        if (jet == null) return;
        LineRenderer lr = jet.GetComponent<LineRenderer>();
        if (lr == null) return;
        lr.SetPosition(0, origin);
        lr.SetPosition(1, origin + dir * length);
        lr.startWidth = width * alpha;
        lr.endWidth = width * 0.05f * alpha;
    }

    // Pulso de brilho na estrela
    IEnumerator StarBrightnessPulse(RelativityBody star, float intensity, float duration)
    {
        if (star == null) yield break;
        Renderer r = star.GetComponent<Renderer>();
        if (r == null) yield break;
        Color baseColor = r.material.color;
        float elapsed = 0f;
        while (elapsed < duration)
        {
            if (star == null) yield break;
            elapsed += Time.deltaTime;
            float t = Mathf.Sin((elapsed / duration) * Mathf.PI);
            r.material.SetColor("_EmissionColor", baseColor * Mathf.Lerp(3f, intensity * 8f, t));
            yield return null;
        }
        if (star != null) r.material.SetColor("_EmissionColor", baseColor * 3f);
    }

    // Pulso de crescimento da estrela
    IEnumerator GrowPulse(RelativityBody star, float peakMultiplier = 1.4f)
    {
        if (star == null) yield break;
        Vector3 baseScale = star.transform.localScale;
        float   duration  = 0.5f;
        float   elapsed   = 0f;
        while (elapsed < duration)
        {
            if (star == null) yield break;
            elapsed += Time.deltaTime;
            float ease = Mathf.Sin((elapsed / duration) * Mathf.PI);
            star.transform.localScale = Vector3.Lerp(baseScale, baseScale * peakMultiplier, ease);
            yield return null;
        }
        if (star != null) star.transform.localScale = baseScale;
    }

    // Shockwave com delay — eco gravitacional
    IEnumerator DelayedShockwave(Vector3 pos, float mass, float delay)
    {
        yield return new WaitForSeconds(delay);
        if (gravitationalWaves != null)
            gravitationalWaves.SpawnShockwave(pos, mass * 0.5f);
    }
}
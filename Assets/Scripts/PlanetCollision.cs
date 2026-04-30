using UnityEngine;
using System.Collections;
using System.Collections.Generic;
public class PlanetCollision : MonoBehaviour
{
    public enum CollisionMode { FragmentByMass, FragmentAll, Bounce }

    [Header("Modo de Colisão")]
    public CollisionMode mode = CollisionMode.FragmentByMass;

    [Header("Fragmentação")]
    [Tooltip("Rácio mínimo de massa (maior/menor) para fragmentar no modo FragmentByMass")]
    public float massRatioThreshold  = 2.0f;
    [Tooltip("Expoente de fragmentação β — controla quantos fragmentos se formam")]
    public float fragmentationBeta = 0.5f;
    [Tooltip("Número mínimo de fragmentos por colisão")]
    public int minFragments = 1; 
    [Tooltip("Número máximo de fragmentos por colisão")]
    public int maxFragments = 2; 
    [Tooltip("Fração da massa total que vai para os fragmentos")]
    public float fragmentMassFraction = 0.4f;

    [Header("Ricochete")]
    [Range(0f, 1f)]
    [Tooltip("Coeficiente de restituição — 1 = perfeitamente elástico, 0 = inelástico")]
    public float restitutionCoeff = 0.7f;

    [Header("Referências")]
    public StarSystemManager manager;

    // Prefab dos fragmentos — criado em runtime se não for atribuído
    public GameObject fragmentPrefab;

    // Cores dos fragmentos — detritos rochosos
    private static readonly Color[] fragmentColors =
    {
        new Color(0.45f, 0.38f, 0.32f), // castanho rochoso
        new Color(0.52f, 0.46f, 0.38f), // arenito
        new Color(0.38f, 0.35f, 0.32f), // cinzento escuro
        new Color(0.55f, 0.48f, 0.40f), // ocre
        new Color(0.42f, 0.40f, 0.36f), // basalto
    };

    // Processa uma colisão entre dois planetas
    // Chamado pelo StarSystemManager quando dois planetas se tocam
    public void ProcessCollision(StarComponent a, StarComponent b)
    {
        if (a == null || b == null) return;

        switch (mode)
        {
            case CollisionMode.FragmentByMass:
                HandleFragmentByMass(a, b);
                break;
            case CollisionMode.FragmentAll:
                HandleFragmentAll(a, b);
                break;
            case CollisionMode.Bounce:
                HandleBounce(a, b);
                break;
        }
    }

    // Fragmentação por massa
    // Só fragmenta se a diferença de massa for suficiente (massRatioThreshold)
    // O planeta menor parte-se; o maior continua com massa reduzida
    void HandleFragmentByMass(StarComponent a, StarComponent b)
    {
        StarComponent larger = a.mass >= b.mass ? a : b;
        StarComponent smaller = a.mass <  b.mass ? a : b;

        float massRatio = larger.mass / Mathf.Max(smaller.mass, 0.01f);

        if (massRatio >= massRatioThreshold)
        {
            // Fragmenta o planeta menor
            SpawnFragments(smaller, larger);

            // Planeta maior perde um pouco de massa (impacto) e atualiza a aparência
            larger.mass -= smaller.mass * 0.05f;
            larger.UpdateAppearance();

            // Regenera as texturas procedurais do planeta sobrevivente
            // (o PlanetAppearance precisa de ser notificado da mudança de massa)
            PlanetAppearance pa = larger.GetComponent<PlanetAppearance>();
            if (pa != null)
            {
                pa.enabled = false;
                pa.enabled = true; // força o Start() a correr novamente
            }

            // O menor já foi removido da lista pelo StarSystemManager
            // Só precisamos de destruir o GameObject
            if (smaller != null && smaller.gameObject != null)
                Destroy(smaller.gameObject);
        }
        else
        {
            // Massas parecidas — ricochete em vez de fragmentação
            HandleBounce(a, b);
        }
    }

    // Fragmentação total
    // Qualquer colisão entre planetas gera fragmentos de ambos
    void HandleFragmentAll(StarComponent a, StarComponent b)
    {
        Vector3 collisionPoint = (a.transform.position + b.transform.position) * 0.5f;

        // Ambos se fragmentam — o maior gera mais fragmentos
        SpawnFragments(a, b);
        SpawnFragments(b, a);

        RemovePlanet(a);
        RemovePlanet(b);
    }

    // Ricochete elástico
    // Conservação de momento e energia cinética com coeficiente de restituição
    void HandleBounce(StarComponent a, StarComponent b)
    {
        Vector3 normal = (a.transform.position - b.transform.position).normalized;
        Vector3 relVel = a.velocity - b.velocity;
        float vAlongN = Vector3.Dot(relVel, normal);

        // Só processa se os planetas estão a aproximar-se
        if (vAlongN > 0f) return;

        float totalMass = a.mass + b.mass;
        float impulse = -(1f + restitutionCoeff) * vAlongN / totalMass;

        // Aplica o impulso — conservação de momento linear
        a.velocity += impulse * b.mass * normal;
        b.velocity -= impulse * a.mass * normal;

        // Separa os planetas ligeiramente para evitar colisão repetida
        float overlap = (a.transform.localScale.x + b.transform.localScale.x) * 0.5f
                        - Vector3.Distance(a.transform.position, b.transform.position);
        if (overlap > 0f)
        {
            a.transform.position += normal * overlap * 0.51f;
            b.transform.position -= normal * overlap * 0.51f;
        }
    }

    // Geração de fragmentos
    // Calcula o número de fragmentos pela energia de impacto vs energia de ligação
    // N ∝ (E_impact / E_binding)^β  — modelo de fragmentação planetária
    void SpawnFragments(StarComponent fragmented, StarComponent impactor)
    {
        // LIMITADOR DE SEGURANÇA SEVERO:
        // Reduzido para 30 para o PC não lagar de todo durante colisões - Sim, tenho um computador fraco :(
        if (manager.GetStars().Count > 30) return;

        // Energia cinética do impacto (massa reduzida * v_relativa^2)
        float reducedMass = (fragmented.mass * impactor.mass)
                          / (fragmented.mass + impactor.mass);
        float relSpeed = (fragmented.velocity - impactor.velocity).magnitude;
        float eImpact = 0.5f * reducedMass * relSpeed * relSpeed;

        // Energia de ligação gravitacional: E_bind = G * M² / R
        float radius = fragmented.transform.localScale.x * 0.5f;
        float eBind = Mathf.Max(manager.G * fragmented.mass * fragmented.mass
                                   / Mathf.Max(radius, 0.1f), 0.001f);

        // Número de fragmentos — clamp entre min e max
        float ratio = eImpact / eBind;
        int nFragments  = Mathf.Clamp(
            Mathf.RoundToInt(minFragments * Mathf.Pow(Mathf.Max(ratio, 1f), fragmentationBeta)),
            minFragments, maxFragments);

        float fragmentMass = fragmented.mass * fragmentMassFraction / nFragments;
        fragmentMass = Mathf.Max(fragmentMass, 0.1f); // massa mínima

        Vector3 impactNormal = (fragmented.transform.position - impactor.transform.position).normalized;

        for (int i = 0; i < nFragments; i++)
        {
            // Direção aleatória no hemisfério oposto ao impactor
            Vector3 randDir = Random.onUnitSphere;
            if (Vector3.Dot(randDir, impactNormal) < 0f)
                randDir = -randDir;
            randDir = Vector3.Lerp(impactNormal, randDir, 0.7f).normalized;

            // Velocidade do fragmento: herda a velocidade do planeta + ejeção
            float ejectSpeed = Mathf.Sqrt(2f * eImpact / (fragmentMass * nFragments));
            ejectSpeed = Mathf.Clamp(ejectSpeed, 0.5f, 15f);
            Vector3 fragVel  = fragmented.velocity + randDir * ejectSpeed;

            // Posição: ligeiramente afastada do ponto de impacto
            Vector3 fragPos = fragmented.transform.position
                            + randDir * radius * Random.Range(1.0f, 1.5f);

            // Cria o fragmento como planeta pequeno
            SpawnSingleFragment(fragPos, fragVel, fragmentMass);
        }

        // Flash de impacto
        StartCoroutine(ImpactFlash(fragmented.transform.position));
    }

    // Cria um único fragmento na cena como planeta com física N-body
    void SpawnSingleFragment(Vector3 position, Vector3 velocity, float mass)
    {
        GameObject prefab = fragmentPrefab != null
            ? fragmentPrefab
            : manager.GetPlanetPrefab(); 

        if (prefab == null) return;

        GameObject obj = Instantiate(prefab, position, Random.rotation);
        obj.transform.parent = manager.transform;
        obj.name = "Fragment";

        // Remove o TrailRenderer dos fragmentos para ganhar FPS
        if (obj.TryGetComponent<TrailRenderer>(out var tr)) tr.enabled = false;

        StarComponent sc = obj.GetComponent<StarComponent>();
        if (sc != null)
        {
            sc.mass = mass;
            sc.velocity = velocity;
            sc.isPlanet = true;

            // Cor de detrito rochoso
            Color fragmentColor = fragmentColors[Random.Range(0, fragmentColors.Length)];

            // Escala fixa pequena
            float scale = 0.12f; 
            obj.transform.localScale = Vector3.one * scale;

            Renderer rend = obj.GetComponent<Renderer>();
            if (rend != null)
            {
                rend.material.color = fragmentColor;
                rend.material.SetColor("_BaseColor", fragmentColor);
                rend.material.DisableKeyword("_EMISSION");
            }

            // OTIMIZAÇÃO: Fragmentos não entram no StarSystemManager. List
            // Eles apenas movem-se via script simples ou FixedUpdate básico
        }

        // Destruição ultra-rápida (0.5 segundos)
        StartCoroutine(QuickDestroy(obj, 0.5f));
    }

    // Remove planeta da simulação sem animação (já vai ser substituído por fragmentos)
    void RemovePlanet(StarComponent planet)
    {
        manager.RemoveFromSimulation(planet);
        Destroy(planet.gameObject);
    }

    // Rotina de destruição sem cálculos pesados
    IEnumerator QuickDestroy(GameObject fragment, float delay)
    {
        yield return new WaitForSeconds(delay);
        if (fragment != null) Destroy(fragment);
    }

    // Flash visual simplificado
    IEnumerator ImpactFlash(Vector3 position)
    {
        GameObject flashGO = new GameObject("ImpactFlash");
        flashGO.transform.position = position;

        LineRenderer lr  = flashGO.AddComponent<LineRenderer>();
        int seg = 8; // Ainda mais reduzido
        lr.positionCount = seg + 1;
        lr.useWorldSpace = true;
        lr.loop = true;

        Material mat = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
        mat.color = new Color(1f, 0.8f, 0.4f, 0.5f);
        lr.material = mat;

        float duration = 0.15f;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float radius = 1.5f * (elapsed / duration);
            for (int i = 0; i <= seg; i++)
            {
                float a = (float)i / seg * Mathf.PI * 2f;
                lr.SetPosition(i, position + new Vector3(Mathf.Cos(a) * radius, 0f, Mathf.Sin(a) * radius));
            }
            yield return null;
        }
        Destroy(flashGO);
    }
}
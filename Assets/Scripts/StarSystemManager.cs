using UnityEngine;
using System.Collections.Generic;

public class StarSystemManager : MonoBehaviour
{
    [Header("Configurações")]
    public GameObject starPrefab;
    public float G = 100f; 
    public float timeScale = 1f;
    
    public int starCount = 100; 
    public float spawnRadius = 30f;

    [Header("Colisões")]
    [Tooltip("Ativar na cena Laboratorio_Manual. Desativar na cena Newton_Aleatorio.")]
    public bool enableMerging = false;
    public float mergeDistance = 1.5f; // distância mínima para fundir duas estrelas
    
    // Lista que guarda as estrelas para o cálculo da gravidade
    private List<StarComponent> stars = new List<StarComponent>();
    private bool isPaused = false;
    public void SetPaused(bool paused) => isPaused = paused;

    // Evento disparado sempre que um planeta entra ou sai da simulação
    // O SettingsPanel subscreve isto para atualizar o tooltip em tempo real
    public event System.Action OnStarListChanged;

    // Referência ao PlanetAbsorption — gere a sequência visual de absorção
    private PlanetAbsorption absorptionHandler;

    // Referência ao PlanetCollision — gere as colisões planeta-planeta
    private PlanetCollision collisionHandler;

    [Header("Prefabs")]
    [Tooltip("Prefab do planeta (Planet_Base) — usado para criar fragmentos em colisões")]
    public GameObject planetPrefab;

    // Contadores para nomes automáticos
    private int starCounter = 0;
    private int planetCounter = 0;

    void Start()
    {
        // Garante que os handlers de colisão existem no mesmo GameObject
        absorptionHandler = GetComponent<PlanetAbsorption>() ?? gameObject.AddComponent<PlanetAbsorption>();
        collisionHandler = GetComponent<PlanetCollision>() ?? gameObject.AddComponent<PlanetCollision>();
        if (collisionHandler != null) collisionHandler.manager = this;

        if (starCount > 0)
        {
            SpawnInitialGalaxy(starCount, spawnRadius);
        }
    }

    // Gera a galáxia inicial
    public void SpawnInitialGalaxy(int count, float radius)
    {
        for (int i = 0; i < count; i++)
        {
            Vector3 pos = Random.insideUnitSphere * radius;
            // pos.y *= 0.1f; 
            
            float randomMass = Random.Range(10f, 500f); // Massas variadas
            Vector3 vel = Vector3.Cross(pos, Vector3.up).normalized * 5f;

            CreateStar(pos, vel, randomMass);
        }
    }

    // Função para criar uma estrela (Modo User Control)
    public GameObject CreateStar(Vector3 position, Vector3 initialVelocity, float mass)
    {
        return CreateStarCustom(starPrefab, position, initialVelocity, mass);
    }

    // Permite criar qualquer prefab (Estrela ou Planeta) vindo do MouseInteraction
    public GameObject CreateStarCustom(GameObject prefab, Vector3 position, Vector3 initialVelocity, float mass)
    {
        if (prefab == null) return null;

        GameObject obj = Instantiate(prefab, position, Quaternion.identity);
        obj.transform.parent = this.transform;

        if (obj.TryGetComponent<TrailRenderer>(out var tr))
        {
            tr.enabled = true;
            tr.Clear(); // Limpa rastos residuais do momento da criação
        }

        StarComponent sc = obj.GetComponent<StarComponent>();
        if (sc != null)
        {
            sc.mass = mass;
            sc.velocity = initialVelocity;

            // Nome automático: Star #1, Planet #1, etc.
            bool isPlanet = sc.isPlanet;
            obj.name = isPlanet
                ? $"Planet #{++planetCounter}"
                : $"Star #{++starCounter}";

            // Atualiza a cor logo ao nascer baseado na massa
            sc.UpdateAppearance();

            stars.Add(sc);
            OnStarListChanged?.Invoke(); // notifica o SettingsPanel
        }

        return obj; // Retorna o objeto criado para o MouseInteraction guardar a referência
    }

    // Devolve todas as estrelas (não planetas) para a simulação da trajetória
    public List<StarComponent> GetStars()
    {
        stars.RemoveAll(s => s == null);
        return stars;
    }

    // Devolve a estrela (não planeta) mais próxima de uma posição, usado pelo MouseInteraction para calcular a velocidade orbital ideal.
    public StarComponent GetNearestStar(Vector3 position)
    {
        StarComponent nearest = null;
        float minDist = float.MaxValue;

        foreach (StarComponent sc in stars)
        {
            if (sc == null || sc.isPlanet) continue;
            float d = Vector3.Distance(position, sc.transform.position);
            if (d < minDist) { minDist = d; nearest = sc; }
        }

        return nearest;
    }

    public void ResetSimulation()
    {
        // Limpeza de segurança para evitar erros se objetos forem destruídos manualmente
        stars.RemoveAll(s => s == null);

        foreach (StarComponent sc in stars)
        {
            if (sc!= null)
            {
                if (sc.gameObject != null) Destroy(sc.gameObject);
            }
        }
        stars.Clear();
        starCounter = 0;
        planetCounter = 0;

        if (starCount > 0)
        {
            SpawnInitialGalaxy(starCount, spawnRadius);
        }
    }

    void FixedUpdate()
    {
        if (isPaused) return;

        float dt = Time.fixedDeltaTime * timeScale;

        // Limpeza de referências nulas (caso estrelas colidam e desapareçam)
        stars.RemoveAll(s => s == null);

        // Loop de gravidade N-Bodies
        for (int i = 0; i < stars.Count; i++)
        {
            if (stars[i] == null) continue;

            // Fragmentos usam física simplificada — só interagem com a estrela mais próxima
            // Evita o custo O(n^2) do N-body completo para detritos de curta vida
            if (stars[i].gameObject.CompareTag("Fragment"))
            {
                // Fragmentos agora não processam gravidade para aliviar o PC
                continue;
            }

            Vector3 acceleration = Vector3.zero;
            for (int j = 0; j < stars.Count; j++)
            {
                if (i == j) continue;
                if (stars[j] == null) continue;

                // Fragmentos não exercem força gravitacional sobre outros objetos
                if (stars[j].gameObject.CompareTag("Fragment")) continue;

                Vector3 dir = stars[j].transform.position - stars[i].transform.position;
                float distSq = Mathf.Max(dir.sqrMagnitude, 25f); // Evita erros de divisão por zero

                // Usa a massa real de cada estrela
                float force = (G * stars[j].mass) / distSq;
                acceleration += dir.normalized * force;
            }
            stars[i].velocity += acceleration * dt;
        }

        // Aplica o movimento
        foreach (StarComponent sc in stars)
        {
            if (sc != null)
                sc.transform.position += sc.velocity * dt;
        }

        // Fusão de estrelas (só ativa se enableMerging = true)
        if (enableMerging)
            ProcessMerges();

        ProcessPlanetPlanetCollisions();
        // Colisão planeta-estrela — planeta é destruído ao entrar no raio da estrela
        ProcessPlanetCollisions();
    }

    [Header("Colisão Planeta-Planeta")]
    [Tooltip("Distância mínima entre dois planetas para despoletar colisão (em game units)")]
    public float planetCollisionDistance = 1.5f;

    // Deteta colisões entre planetas e delega ao PlanetCollision
    void ProcessPlanetPlanetCollisions()
    {
        for (int i = stars.Count - 1; i >= 0; i--)
        {
            if (stars[i] == null || !stars[i].isPlanet) continue;

            for (int j = i - 1; j >= 0; j--)
            {
                if (stars[j] == null || !stars[j].isPlanet) continue;

                float dist = Vector3.Distance(stars[i].transform.position,
                                              stars[j].transform.position);

                if (dist > planetCollisionDistance) continue;

                StarComponent a = stars[i];
                StarComponent b = stars[j];

                PlanetCollision pc = GetComponent<PlanetCollision>();
                if (pc != null)
                {
                    pc.manager = this;

                    bool isBounce = pc.mode == PlanetCollision.CollisionMode.Bounce;

                    if (!isBounce)
                    {
                        if (pc.mode == PlanetCollision.CollisionMode.FragmentAll)
                        {
                            // Fragment All — remove ambos da lista
                            stars.RemoveAt(i);
                            int newJ = j < i ? j : j - 1;
                            if (newJ >= 0 && newJ < stars.Count) stars.RemoveAt(newJ);
                            OnStarListChanged?.Invoke();
                        }
                        else if (pc.mode == PlanetCollision.CollisionMode.FragmentByMass)
                        {
                            float largerMass  = Mathf.Max(a.mass, b.mass);
                            float smallerMass = Mathf.Min(a.mass, b.mass);
                            bool  willFrag = (largerMass / Mathf.Max(smallerMass, 0.01f)) >= pc.massRatioThreshold;

                            if (willFrag)
                            {
                                // Só remove o menor — o maior sobrevive na simulação com textura
                                StarComponent smaller = a.mass < b.mass ? a : b;
                                stars.Remove(smaller);
                                OnStarListChanged?.Invoke();
                            }
                            else
                            {
                                // Massas parecidas → vai para Bounce interno, não remove nada
                            }
                        }
                    }

                    pc.ProcessCollision(a, b);
                }
                else
                {
                    // Fallback — destrói ambos com animação se PlanetCollision não existir
                    stars.RemoveAt(i);
                    int newJ = j < i ? j : j - 1;
                    if (newJ >= 0 && newJ < stars.Count) stars.RemoveAt(newJ);
                    OnStarListChanged?.Invoke();
                    a.StartCoroutine(a.DestroyAnimation());
                    b.StartCoroutine(b.DestroyAnimation());
                }
                return; // OTIMIZAÇÃO: Apenas uma colisão processada por frame para evitar lag
            }
        }
    }

    void ProcessPlanetCollisions()
    {
        for (int i = stars.Count - 1; i >= 0; i--)
        {
            if (stars[i] == null || !stars[i].isPlanet) continue;

            for (int j = 0; j < stars.Count; j++)
            {
                if (stars[j] == null || stars[j].isPlanet) continue;

                // Raio de captura gravitacional — inicia a espiral antes do contacto visual
                float starRadius = stars[j].transform.localScale.x * 0.5f;
                float captureRadius = starRadius * 1.5f;
                float dist = Vector3.Distance(stars[i].transform.position,
                                              stars[j].transform.position);

                if (dist > captureRadius) continue;

                stars[j].mass += stars[i].mass * 0.1f; // absorve 10% da massa do planeta
                stars[j].UpdateAppearance();

                StarComponent planet = stars[i];
                StarComponent star = stars[j];
                stars.RemoveAt(i);
                OnStarListChanged?.Invoke(); // notifica o SettingsPanel

                if (absorptionHandler != null)
                    absorptionHandler.StartAbsorption(planet, star);
                else
                    planet.StartCoroutine(planet.DestroyAnimation()); // fallback

                break;
            }
        }
    }

    // Devolve o prefab do planeta — usado pelo PlanetCollision para criar fragmentos
    public GameObject GetPlanetPrefab() => planetPrefab;

    // Adiciona um objeto à simulação N-body completa
    public void AddToSimulation(StarComponent sc)
    {
        if (sc != null && !stars.Contains(sc))
            stars.Add(sc);
    }

    // Adiciona um fragmento com física simplificada — só interage com a estrela mais próxima
    // Muito mais eficiente que o N-body completo para detritos de curta vida
    public void AddFragmentToSimulation(StarComponent sc)
    {
        if (sc == null) return;

        // Marca o fragmento para física simplificada via tag
        sc.gameObject.tag = "Fragment";

        // Adiciona à lista normal — o FixedUpdate trata-o de forma diferente se for Fragment
        if (!stars.Contains(sc))
            stars.Add(sc);
    }

    // Remove um objeto da simulação N-body
    public void RemoveFromSimulation(StarComponent sc)
    {
        if (stars.Remove(sc))
            OnStarListChanged?.Invoke(); // notifica o SettingsPanel
    }

    // Conta quantos planetas têm esta estrela como a mais próxima deles
    // Usado pelo ObjectInspector para mostrar "Orbiting bodies: X" quando se clica numa estrela
    public int GetOrbitingCount(StarComponent star)
    {
        int count = 0;
        stars.RemoveAll(s => s == null);
        foreach (StarComponent sc in stars)
        {
            if (sc == null || !sc.isPlanet) continue;
            StarComponent nearest = GetNearestStar(sc.transform.position);
            if (nearest == star) count++;
        }
        return count;
    }

    void ProcessMerges()
    {
        for (int i = stars.Count - 1; i >= 0; i--)
        {
            if (stars[i] == null) continue;

            for (int j = i - 1; j >= 0; j--)
            {
                if (stars[j] == null) continue;

                float dist = Vector3.Distance(stars[i].transform.position,
                                              stars[j].transform.position);
                if (dist > mergeDistance) continue;

                // Nunca fundir planetas — só estrela com estrela
                if (stars[i].isPlanet || stars[j].isPlanet) continue;

                // Conservação de momento: p = m*v → v_final = (m1*v1 + m2*v2) / (m1+m2)
                float totalMass = stars[i].mass + stars[j].mass;
                Vector3 newVelocity = (stars[i].velocity * stars[i].mass
                                      + stars[j].velocity * stars[j].mass) / totalMass;
                Vector3 newPosition = (stars[i].transform.position * stars[i].mass
                                      + stars[j].transform.position * stars[j].mass) / totalMass;

                // Mantém a estrela mais massiva (i), absorve a mais pequena (j)
                stars[i].mass = totalMass;
                stars[i].velocity = newVelocity;
                stars[i].mergeCount += stars[j].mergeCount + 1;
                stars[i].transform.position = newPosition;
                stars[i].UpdateAppearance();

                Destroy(stars[j].gameObject);
                stars.RemoveAt(j);
                i--; // ajusta o índice após remoção
                break;
            }
        }
    }
}
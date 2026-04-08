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
    // Contadores para nomes automáticos
    private int starCounter = 0;
    private int planetCounter = 0;

    void Start()
    {
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
            Vector3 acceleration = Vector3.zero;
            for (int j = 0; j < stars.Count; j++)
            {
                if (i == j) continue;

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

        // Colisão planeta-estrela — planeta é destruído ao entrar no raio da estrela
        ProcessPlanetCollisions();
    }

    void ProcessPlanetCollisions()
    {
        for (int i = stars.Count - 1; i >= 0; i--)
        {
            if (stars[i] == null || !stars[i].isPlanet) continue;

            for (int j = 0; j < stars.Count; j++)
            {
                if (stars[j] == null || stars[j].isPlanet) continue;

                // Raio da estrela baseado na sua escala atual
                float starRadius = stars[j].transform.localScale.x * 0.5f;
                float dist = Vector3.Distance(stars[i].transform.position,
                                                    stars[j].transform.position);

                if (dist > starRadius) continue;

                // Estrela absorve a massa do planeta e pulsa
                stars[j].mass += stars[i].mass * 0.1f; // absorve 10% da massa do planeta
                stars[j].UpdateAppearance();
                stars[j].StartCoroutine(stars[j].AbsorptionPulse());

                // Planeta desaparece com animação
                StarComponent planet = stars[i];
                stars.RemoveAt(i);
                planet.StartCoroutine(planet.DestroyAnimation());
                break;
            }
        }
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
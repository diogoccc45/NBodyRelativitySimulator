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
    
    // Lista que guarda as estrelas para o cálculo da gravidade
    private List<StarComponent> stars = new List<StarComponent>();

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
    // Agora retorna GameObject para o sistema de foco
    public GameObject CreateStar(Vector3 position, Vector3 initialVelocity, float mass)
    {
        return CreateStarCustom(starPrefab, position, initialVelocity, mass);
    }

    // Permite criar qualquer prefab (Estrela ou Planeta) vindo do MouseInteraction
    // Mudado de 'public void' para 'public GameObject'
    public GameObject CreateStarCustom(GameObject prefab, Vector3 position, Vector3 initialVelocity, float mass)
    {
        if (prefab == null) return null;

        GameObject obj = Instantiate(prefab, position, Quaternion.identity);
        obj.transform.parent = this.transform;

        // --- GARANTE QUE O TRAIL APARECE E ESTÁ LIMPO ---
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
            
            // Atualiza a cor logo ao nascer baseado na massa
            sc.UpdateAppearance();

            stars.Add(sc);
        }

        return obj; // Retorna o objeto criado para o MouseInteraction guardar a referência
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

        // Agora usa as variáveis do inspector no reset também
        if (starCount > 0)
        {
            SpawnInitialGalaxy(starCount, spawnRadius);
        }
    }

    void FixedUpdate()
    {
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
                float distSq = Mathf.Max(dir.sqrMagnitude, 2f); // Evita erros de divisão por zero

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
    }
}
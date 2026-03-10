using UnityEngine;
using System.Collections.Generic;

public class StarSystemManager : MonoBehaviour
{
    [Header("Configurações")]
    public GameObject starPrefab;
    public float G = 100f; 
    public float timeScale = 1f;
    
    // Lista que guarda as estrelas para o cálculo da gravidade
    private List<StarComponent> stars = new List<StarComponent>();

    void Start()
    {
        //Spawn a um conjunto de estrelas: Teste
        SpawnInitialGalaxy(100, 30f);
    }

    // Gera a galáxia inicial
    public void SpawnInitialGalaxy(int count, float radius)
    {
        for (int i = 0; i < count; i++)
        {
            Vector3 pos = Random.insideUnitSphere * radius;
            pos.y *= 0.1f; 
            
            float randomMass = Random.Range(10f, 500f); // Massas variadas
            Vector3 vel = Vector3.Cross(pos, Vector3.up).normalized * 5f;

            CreateStar(pos, vel, randomMass);
        }
    }

    // Função para criar uma estrela (Modo User Control)
    public void CreateStar(Vector3 position, Vector3 initialVelocity, float mass)
    {
        GameObject obj = Instantiate(starPrefab, position, Quaternion.identity);
        obj.transform.parent = this.transform;

        StarComponent sc = obj.GetComponent<StarComponent>();
        sc.mass = mass;
        sc.velocity = initialVelocity;
        
        // Atualiza a cor logo ao nascer baseado na massa
        sc.UpdateAppearance();

        stars.Add(sc);
    }

    public void ResetSimulation()
    {
        foreach (StarComponent sc in stars)
        {
            if (sc!= null)
            {
                Destroy(sc.gameObject);
            }
        }
        stars.Clear();

        SpawnInitialGalaxy(100, 30f);
    }

    void FixedUpdate()
    {
        float dt = Time.fixedDeltaTime * timeScale;

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
            sc.transform.position += sc.velocity * dt;
        }
    }
}
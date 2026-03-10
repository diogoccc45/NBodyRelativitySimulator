using UnityEngine;
using System.Collections.Generic;
using UnityEngine.SceneManagement; 

public class StarSystemManager : MonoBehaviour
{
    [Header("Geração")]
    public GameObject starPrefab;
    public int numberOfStars = 100;
    public float spawnRadius = 30f;

    [Header("Física (Newton)")]
    public float G = 100f; 
    public float initialVelocityScale = 5f;
    public float timeScale = 1f; // Para acelerar ou abrandar a simulação

    private List<GameObject> stars = new List<GameObject>();
    private List<Vector3> velocities = new List<Vector3>();

    void Start()
    {
        SpawnGalaxy();
    }

    void SpawnGalaxy()
    {
        for (int i = 0; i < numberOfStars; i++)
        {
            // Posição em disco (Galáxia)
            Vector3 randomPos = Random.insideUnitSphere * spawnRadius;
            randomPos.y *= 0.1f; 

            GameObject star = Instantiate(starPrefab, randomPos, Quaternion.identity);
            star.transform.parent = this.transform;
            stars.Add(star);

            // Velocidade Orbital Inicial (Para não colapsarem logo)
            Vector3 perpendicular = Vector3.Cross(randomPos, Vector3.up).normalized;
            velocities.Add(perpendicular * initialVelocityScale);
            
            star.name = "Star_" + i;
        }
    }

    void FixedUpdate()
    {
        ApplyGravity();
    }

    void ApplyGravity()
    {
        float dt = Time.fixedDeltaTime * timeScale;

        for (int i = 0; i < stars.Count; i++)
        {
            Vector3 acceleration = Vector3.zero;

            for (int j = 0; j < stars.Count; j++)
            {
                if (i == j) continue;

                Vector3 direction = stars[j].transform.position - stars[i].transform.position;
                float distanceSq = direction.sqrMagnitude;

                // "Softening" - evita que a força seja infinita quando se cruzam
                if (distanceSq < 2f) continue; 

                // F = G / r^2
                float force = G / distanceSq;
                acceleration += direction.normalized * force;
            }

            velocities[i] += acceleration * dt;
            stars[i].transform.position += velocities[i] * dt;
        }
    }
    public void ResetSimulation()
    {
    // Opção A: Reiniciar a cena completa
    SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    
    /* Opção B: Apagar as estrelas sem recarregar a cena:
    foreach (GameObject star in stars) {
        Destroy(star);
    }
    stars.Clear();
    velocities.Clear();
    SpawnGalaxy();
    */
    }
}
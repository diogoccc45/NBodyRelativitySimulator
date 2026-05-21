using UnityEngine;
using System.Collections.Generic;

// Gera o fundo do menu principal proceduralmente:
//   - Campo de estrelas com paralaxe suave
//   - Nebulosa com partículas coloridas
//   - Tudo via código — sem assets externos
[RequireComponent(typeof(ParticleSystem))]
public class MainMenuBackground : MonoBehaviour
{
    [Header("Estrelas")]
    public int starCount       = 320;
    public float fieldRadius   = 12f;
    public float twinkleSpeed  = 0.8f;

    [Header("Nebulosa")]
    public int nebulaCount     = 60;
    public Color nebulaColorA  = new Color(0.10f, 0.18f, 0.55f, 0.18f);
    public Color nebulaColorB  = new Color(0.35f, 0.08f, 0.45f, 0.14f);
    public Color nebulaColorC  = new Color(0.05f, 0.30f, 0.50f, 0.12f);

    private ParticleSystem ps;
    private ParticleSystem.Particle[] stars;
    private float[] twinkleOffsets;
    private float[] twinkleSpeeds;

    void Start()
    {
        ps = GetComponent<ParticleSystem>();
        var main = ps.main;
        main.loop          = false;
        main.playOnAwake   = false;
        main.maxParticles  = starCount + nebulaCount;
        main.simulationSpace = ParticleSystemSimulationSpace.World;

        var emission = ps.emission; emission.enabled = false;
        var shape    = ps.shape;    shape.enabled    = false;

        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        stars          = new ParticleSystem.Particle[starCount + nebulaCount];
        twinkleOffsets = new float[starCount];
        twinkleSpeeds  = new float[starCount];

        // Estrelas
        for (int i = 0; i < starCount; i++)
        {
            float angle = Random.Range(0f, Mathf.PI * 2f);
            float r     = Mathf.Sqrt(Random.value) * fieldRadius;
            float depth = Random.Range(-2f, 2f);

            stars[i].position  = new Vector3(Mathf.Cos(angle) * r, Mathf.Sin(angle) * r, depth);
            stars[i].startSize = Random.Range(0.015f, 0.07f);

            float brightness = Random.Range(0.55f, 1f);
            // Mistura de brancos frios e quentes
            Color starColor = Random.value > 0.85f
                ? new Color(0.7f, 0.85f, 1.0f, brightness)   // azulado
                : Random.value > 0.7f
                    ? new Color(1.0f, 0.92f, 0.75f, brightness) // amarelado
                    : new Color(brightness, brightness, brightness, brightness);

            stars[i].startColor    = starColor;
            stars[i].remainingLifetime = 1e10f;
            stars[i].velocity      = Vector3.zero;

            twinkleOffsets[i] = Random.Range(0f, Mathf.PI * 2f);
            twinkleSpeeds[i]  = Random.Range(0.4f, 1.6f) * twinkleSpeed;
        }

        // Nébula — partículas grandes e transparentes
        Color[] nebulaColors = { nebulaColorA, nebulaColorB, nebulaColorC };
        for (int i = starCount; i < starCount + nebulaCount; i++)
        {
            float angle = Random.Range(0f, Mathf.PI * 2f);
            float r     = Mathf.Sqrt(Random.value) * fieldRadius * 0.85f;

            stars[i].position  = new Vector3(Mathf.Cos(angle) * r, Mathf.Sin(angle) * r, 3f);
            stars[i].startSize = Random.Range(1.8f, 4.5f);
            stars[i].startColor = nebulaColors[Random.Range(0, nebulaColors.Length)];
            stars[i].remainingLifetime = 1e10f;
            stars[i].velocity  = Vector3.zero;
        }

        ps.SetParticles(stars, stars.Length);
    }

    void Update()
    {
        float t = Time.time;

        // Scintilação das estrelas
        for (int i = 0; i < starCount; i++)
        {
            float flicker = 0.55f + 0.45f * Mathf.Sin(t * twinkleSpeeds[i] + twinkleOffsets[i]);
            Color c = stars[i].startColor;
            stars[i].startColor = new Color(c.r, c.g, c.b, flicker);
        }

        // Paralaxe subtil com o tempo
        float px = Mathf.Sin(t * 0.04f) * 0.08f;
        float py = Mathf.Cos(t * 0.03f) * 0.05f;
        transform.position = new Vector3(px, py, transform.position.z);

        ps.SetParticles(stars, stars.Length);
    }
}

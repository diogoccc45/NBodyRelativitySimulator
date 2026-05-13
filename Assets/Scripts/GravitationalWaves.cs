using UnityEngine;
using System.Collections.Generic;

// Gere ondas gravitacionais na grid de espaço-tempo
// Cada vez que uma massa é colocada ou movida, gera uma onda que se propaga
// a partir do ponto de origem — como uma pedra atirada numa poça de água
public class GravitationalWaves : MonoBehaviour
{
    [Header("Referências")]
    public SpacetimeGrid grid;

    [Header("Configuração das Ondas")]
    [Tooltip("Velocidade de propagação da onda em game units por segundo")]
    public float waveSpeed      = 18f;
    [Tooltip("Duração total da onda em segundos")]
    public float waveDuration   = 4f;
    [Tooltip("Amplitude máxima da onda — quanto deforma a grid")]
    public float waveAmplitude  = 3.5f;
    [Tooltip("Largura do anel de onda — mais largo = onda mais suave")]
    public float waveWidth      = 6f;
    [Tooltip("Número máximo de ondas simultâneas — limita para o PC")]
    public int   maxWaves       = 4;

    // Estrutura de uma onda em propagação
    private struct Wave
    {
        public Vector3 origin;
        public float startTime;
        public float mass;
        public bool isShockwave; // shockwaves são mais rápidas e agressivas
    }

    private List<Wave> activeWaves = new List<Wave>();

    // Chamado pelo RelativityManager quando uma massa é colocada ou largada após drag
    public void SpawnWave(Vector3 worldPos, float mass)
    {
        if (activeWaves.Count >= maxWaves)
            activeWaves.RemoveAt(0);

        activeWaves.Add(new Wave
        {
            origin = new Vector3(worldPos.x, 0f, worldPos.z),
            startTime = Time.time,
            mass = mass,
            isShockwave = false
        });
    }

    // Shockwave de absorção — muito mais rápida e agressiva que uma onda normal
    // Disparada no momento de absorção pelo buraco negro
    public void SpawnShockwave(Vector3 worldPos, float mass)
    {
        // Shockwaves ignoram o limite máximo — são eventos únicos importantes
        activeWaves.Add(new Wave
        {
            origin = new Vector3(worldPos.x, 0f, worldPos.z),
            startTime = Time.time,
            mass = mass * 5f, // amplitude muito maior
            isShockwave = true
        });
    }

    // Devolve a deformação adicional causada pelas ondas num ponto XZ
    public float GetWaveDeformAt(float worldX, float worldZ)
    {
        if (activeWaves.Count == 0) return 0f;

        float totalDeform = 0f;
        float currentTime = Time.time;

        for (int i = activeWaves.Count - 1; i >= 0; i--)
        {
            Wave w = activeWaves[i];
            float elapsed = currentTime - w.startTime;

            // Shockwaves são 3x mais rápidas e duram menos
            float duration = w.isShockwave ? waveDuration * 0.4f : waveDuration;
            float speed = w.isShockwave ? waveSpeed * 3f : waveSpeed;
            float width = w.isShockwave ? waveWidth * 0.5f : waveWidth;

            if (elapsed > duration)
            {
                activeWaves.RemoveAt(i);
                continue;
            }

            float waveRadius  = elapsed * speed;
            float dx = worldX - w.origin.x;
            float dz = worldZ - w.origin.z;
            float dist = Mathf.Sqrt(dx * dx + dz * dz);
            float distToFront = dist - waveRadius;

            if (Mathf.Abs(distToFront) > width) continue;

            float waveShape = Mathf.Exp(-(distToFront * distToFront) / (width * width * 0.5f));
            float timeFade = 1f - Mathf.Clamp01(elapsed / duration);
            timeFade = timeFade * timeFade;
            float distFade  = Mathf.Clamp01(1f - dist / (speed * duration));
            float massScale = Mathf.Clamp(w.mass / 200f, 0.2f, 6f);

            totalDeform += waveShape * timeFade * distFade * waveAmplitude * massScale;
        }

        return totalDeform;
    }

    // Devolve true se há ondas ativas — usado pelo SpacetimeGrid para saber se precisa recalcular
    public bool HasActiveWaves() => activeWaves.Count > 0;
}

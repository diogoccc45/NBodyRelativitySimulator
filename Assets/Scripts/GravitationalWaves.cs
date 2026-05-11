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
        public Vector3 origin;    // ponto de origem no plano XZ
        public float   startTime; // quando começou
        public float   mass;      // massa que gerou a onda — afeta a amplitude
    }

    private List<Wave> activeWaves = new List<Wave>();

    // Chamado pelo RelativityManager quando uma massa é colocada ou largada após drag
    public void SpawnWave(Vector3 worldPos, float mass)
    {
        // Limita o número de ondas simultâneas
        if (activeWaves.Count >= maxWaves)
            activeWaves.RemoveAt(0);

        activeWaves.Add(new Wave
        {
            origin    = new Vector3(worldPos.x, 0f, worldPos.z),
            startTime = Time.time,
            mass      = mass
        });
    }

    // Devolve a deformação adicional causada pelas ondas num ponto XZ
    // Chamado pelo SpacetimeGrid.DeformGrid() para cada vértice
    public float GetWaveDeformAt(float worldX, float worldZ)
    {
        if (activeWaves.Count == 0) return 0f;

        float totalDeform = 0f;
        float currentTime = Time.time;

        for (int i = activeWaves.Count - 1; i >= 0; i--)
        {
            Wave w       = activeWaves[i];
            float elapsed = currentTime - w.startTime;

            // Remove ondas expiradas
            if (elapsed > waveDuration)
            {
                activeWaves.RemoveAt(i);
                continue;
            }

            // Raio atual da frente de onda
            float waveRadius = elapsed * waveSpeed;

            // Distância deste vértice à origem da onda
            float dx   = worldX - w.origin.x;
            float dz   = worldZ - w.origin.z;
            float dist = Mathf.Sqrt(dx * dx + dz * dz);

            // Distância da frente de onda a este vértice
            float distToFront = dist - waveRadius;

            // Só afeta vértices perto da frente de onda
            if (Mathf.Abs(distToFront) > waveWidth) continue;

            // Forma da onda — gaussiana centrada na frente de onda
            float waveShape = Mathf.Exp(-(distToFront * distToFront) / (waveWidth * waveWidth * 0.5f));

            // Fade out ao longo do tempo — onda perde energia ao propagar
            float timeFade = 1f - Mathf.Clamp01(elapsed / waveDuration);
            timeFade = timeFade * timeFade; // quadrático — cai rapidamente no fim

            // Fade out com a distância — onda perde energia ao afastar
            float distFade = Mathf.Clamp01(1f - dist / (waveSpeed * waveDuration));

            // Amplitude modulada pela massa — massas maiores geram ondas maiores
            float massScale = Mathf.Clamp(w.mass / 200f, 0.2f, 3f);

            totalDeform += waveShape * timeFade * distFade * waveAmplitude * massScale;
        }

        return totalDeform;
    }

    // Devolve true se há ondas ativas — usado pelo SpacetimeGrid para saber se precisa recalcular
    public bool HasActiveWaves() => activeWaves.Count > 0;
}

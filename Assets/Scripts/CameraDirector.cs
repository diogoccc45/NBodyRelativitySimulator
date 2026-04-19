using UnityEngine;
using System.Collections.Generic;
public class DirectorCamera : MonoBehaviour
{
    [Header("Referências")]
    public StarSystemManager manager;

    [Header("Configurações de Score")]
    [Tooltip("Peso da massa no score — estrelas mais massivas são mais interessantes")]
    public float massWeight       = 0.1f;
    [Tooltip("Peso da velocidade — objetos rápidos são mais dinâmicos")]
    public float velocityWeight   = 2f;
    [Tooltip("Peso da proximidade a outro objeto — iminência de colisão/fusão")]
    public float proximityWeight  = 500f;
    [Tooltip("Boost de score por cada fusão sofrida — recompensa eventos recentes")]
    public float mergeWeight      = 50f;

    [Header("Configurações de Posição")]
    [Tooltip("Multiplicador do raio do objeto para calcular a distância de observação")]
    public float radiusMultiplier = 6f;
    public float minDistance      = 5f;
    public float maxDistance      = 150f;
    [Tooltip("Velocidade de movimento suave da câmara até ao novo alvo")]
    public float smoothSpeed      = 3f;

    [Header("Cooldown")]
    [Tooltip("Tempo mínimo (segundos) entre mudanças de alvo em condições normais")]
    public float cooldownNormal   = 5f;
    [Tooltip("Cooldown reduzido quando uma fusão ou colisão iminente é detetada")]
    public float cooldownUrgent   = 1.5f;
    [Tooltip("Distância abaixo da qual se considera iminente uma fusão/colisão")]
    public float urgentDistance   = 3f;

    private StarComponent currentTarget = null;
    private Vector3       desiredPosition;
    private float         cooldownTimer  = 0f;

    void OnEnable()
    {
        // Ao ativar a câmara, escolhe imediatamente o alvo mais dramático
        cooldownTimer = 0f;
        PickBestTarget();
        Debug.Log("[DirectorCamera] Câmara de diretor ativada.");
    }

    void Update()
    {
        cooldownTimer -= Time.deltaTime;

        // Verifica se há um evento urgente (fusão/colisão iminente) mesmo antes do cooldown
        bool urgentEvent = IsUrgentEventDetected();

        if (cooldownTimer <= 0f || urgentEvent)
        {
            PickBestTarget();
            cooldownTimer = urgentEvent ? cooldownUrgent : cooldownNormal;
        }

        if (currentTarget != null)
            UpdatePosition();
    }

    // Calcula o score de dramatismo para cada objeto e escolhe o mais alto
    void PickBestTarget()
    {
        List<StarComponent> stars = manager.GetStars();
        if (stars == null || stars.Count == 0) return;

        StarComponent bestTarget = null;
        float         bestScore  = float.MinValue;

        foreach (StarComponent sc in stars)
        {
            if (sc == null) continue;

            float score = CalcDramaScore(sc, stars);

            if (score > bestScore)
            {
                bestScore  = score;
                bestTarget = sc;
            }
        }

        if (bestTarget != null && bestTarget != currentTarget)
        {
            currentTarget = bestTarget;
            Debug.Log($"[DirectorCamera] Novo alvo: '{currentTarget.gameObject.name}' (score: {bestScore:F1})");
        }
    }

    // Score de dramatismo — quanto maior, mais interessante é o objeto para a câmara
    float CalcDramaScore(StarComponent sc, List<StarComponent> allStars)
    {
        float score = 0f;

        // Fator 1: Massa — estrelas massivas são visualmente imponentes
        score += sc.mass * massWeight;

        // Fator 2: Velocidade — objetos rápidos são mais dinâmicos de observar
        score += sc.velocity.magnitude * velocityWeight;

        // Fator 3: Proximidade ao vizinho mais próximo — iminência de evento
        // Usa 1/distância para que objetos muito próximos tenham score muito alto
        float nearestDist = GetNearestNeighborDistance(sc, allStars);
        if (nearestDist > 0.01f)
            score += (1f / nearestDist) * proximityWeight;

        // Fator 4: Fusões sofridas — estrelas que já absorveram outras são mais massivas
        // e têm historial de eventos, tornando-as alvos mais interessantes
        score += sc.mergeCount * mergeWeight;

        return score;
    }

    // Devolve a distância ao vizinho mais próximo (qualquer objeto exceto si próprio)
    float GetNearestNeighborDistance(StarComponent sc, List<StarComponent> allStars)
    {
        float minDist = float.MaxValue;

        foreach (StarComponent other in allStars)
        {
            if (other == null || other == sc) continue;
            float d = Vector3.Distance(sc.transform.position, other.transform.position);
            if (d < minDist) minDist = d;
        }

        return minDist == float.MaxValue ? 0f : minDist;
    }

    // Deteta se algum par de objetos está perigosamente perto — evento urgente
    bool IsUrgentEventDetected()
    {
        List<StarComponent> stars = manager.GetStars();
        if (stars == null) return false;

        for (int i = 0; i < stars.Count; i++)
        {
            if (stars[i] == null) continue;
            for (int j = i + 1; j < stars.Count; j++)
            {
                if (stars[j] == null) continue;
                float d = Vector3.Distance(stars[i].transform.position,
                                           stars[j].transform.position);
                if (d < urgentDistance) return true;
            }
        }
        return false;
    }

    // Move a câmara suavemente para trás e acima do alvo atual
    void UpdatePosition()
    {
        if (currentTarget == null) return;

        // Calcula posição desejada: atrás e acima do objeto, a uma distância proporcional ao seu raio
        float radius   = currentTarget.transform.localScale.x * 0.5f;
        float distance = Mathf.Clamp(radius * radiusMultiplier, minDistance, maxDistance);

        // Coloca a câmara ligeiramente acima e atrás da direção de movimento
        Vector3 behindDir = Vector3.back;
        if (currentTarget.velocity.sqrMagnitude > 0.01f)
            behindDir = -currentTarget.velocity.normalized;

        Quaternion moveRot = Quaternion.LookRotation(-behindDir);
        desiredPosition = currentTarget.transform.position
                        + moveRot * new Vector3(0f, distance * 0.3f, -distance);

        transform.position = Vector3.Lerp(transform.position, desiredPosition,
                                          smoothSpeed * Time.deltaTime);
        transform.LookAt(currentTarget.transform.position);
    }
}
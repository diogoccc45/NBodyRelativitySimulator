using UnityEngine;
using System.Collections.Generic;
public class TwoBodyCamera : MonoBehaviour
{
    [Header("Referências")]
    public StarSystemManager manager;

    [Header("Configurações de Enquadramento")]
    [Tooltip("Padding extra para garantir que os dois objetos cabem no ecrã com margem")]
    public float framePadding = 2.0f;
    public float minDistance = 10f;
    public float maxDistance = 600f;
    [Tooltip("Elevação acima do plano dos dois objetos")]
    public float elevationAngle = 25f;
    [Tooltip("Velocidade de suavização do movimento")]
    public float smoothSpeed = 3f;

    [Header("Rotação Manual")]
    [Tooltip("Sensibilidade do botão direito para orbitar manualmente")]
    public float sensitivity = 0.15f;

    [Header("Fallback")]
    [Tooltip("Se só existir um objeto, comporta-se como a BarycentreCamera com esta distância")]
    public float singleObjectDistance = 30f;

    private StarComponent primaryTarget = null; // objeto mais massivo
    private StarComponent secondaryTarget = null; // segundo mais massivo
    private float targetRefreshTimer = 0f;
    private const float TARGET_REFRESH_INTERVAL = 2f; // reavalia os alvos a cada 2 segundos

    // Ângulo de órbita manual — calculado a partir da posição atual para evitar saltos
    private float currentAngle;
    private float currentDistance;

    void OnEnable()
    {
        targetRefreshTimer = 0f;
        RefreshTargets();
        // Inicializa o ângulo e a distância a partir da posição atual da câmara para não haver salto brusco ao ativar
        Vector3 center = GetTargetCenter();
        Vector3 toCamera = transform.position - center;
        currentAngle = Mathf.Atan2(toCamera.x, toCamera.z) * Mathf.Rad2Deg;
        currentDistance = Mathf.Clamp(toCamera.magnitude, minDistance, maxDistance);

        Debug.Log("[TwoBodyCamera] Câmara de dois corpos ativada.");
    }

    void Update()
    {
        // Reavalia os dois alvos periodicamente (fusões podem mudar a hierarquia de massa)
        targetRefreshTimer -= Time.deltaTime;
        if (targetRefreshTimer <= 0f)
        {
            RefreshTargets();
            targetRefreshTimer = TARGET_REFRESH_INTERVAL;
        }
        if (primaryTarget == null) return;

        UpdatePosition();
    }

    // Encontra os dois objetos mais massivos da cena (apenas estrelas, não planetas)
    void RefreshTargets()
    {
        List<StarComponent> stars = manager.GetStars();
        if (stars == null || stars.Count == 0) return;

        StarComponent first = null;
        StarComponent second = null;
        float firstMass = float.MinValue;
        float secondMass = float.MinValue;

        foreach (StarComponent sc in stars)
        {
            // Só considera estrelas — planetas não são "corpos dominantes" do sistema
            if (sc == null || sc.isPlanet) continue;

            if (sc.mass > firstMass)
            {
                second = first;
                secondMass = firstMass;
                first = sc;
                firstMass = sc.mass;
            }
            else if (sc.mass > secondMass)
            {
                second = sc;
                secondMass = sc.mass;
            }
        }

        primaryTarget = first;
        secondaryTarget = second;

        // Debug para verificar quais os alvos encontrados. Ps: espero que não tenha feito asneira
        if (primaryTarget != null && secondaryTarget != null)
            Debug.Log($"[TwoBodyCamera] Alvos: '{primaryTarget.gameObject.name}' e '{secondaryTarget.gameObject.name}'");
        else if (primaryTarget != null)
            Debug.Log($"[TwoBodyCamera] Só um objeto encontrado: '{primaryTarget.gameObject.name}'");
    }

    void UpdatePosition()
    {
        Vector3 targetCenter = GetTargetCenter();
        float   requiredDistance = GetRequiredDistance();

        // Suaviza a distância ao longo do tempo
        currentDistance = Mathf.Lerp(currentDistance, requiredDistance, smoothSpeed * Time.deltaTime);

        // Botão direito — orbita manualmente à volta do ponto médio
        // Arrastar horizontalmente roda o ângulo; verticalmente ajusta a elevação
        if (UnityEngine.InputSystem.Mouse.current.rightButton.isPressed)
        {
            Vector2 delta = UnityEngine.InputSystem.Mouse.current.delta.ReadValue();
            currentAngle += delta.x * sensitivity;
            elevationAngle = Mathf.Clamp(elevationAngle - delta.y * sensitivity, 5f, 85f);
        }

        // Calcula a posição em coordenadas esféricas à volta do ponto médio
        float rad = currentAngle * Mathf.Deg2Rad;
        float elRad = elevationAngle * Mathf.Deg2Rad;
        float horizDist = currentDistance * Mathf.Cos(elRad);

        Vector3 offset = new Vector3(
            Mathf.Sin(rad) * horizDist,
            currentDistance * Mathf.Sin(elRad),
            Mathf.Cos(rad) * horizDist
        );

        Vector3 desiredPos = targetCenter + offset;

        transform.position = Vector3.Lerp(transform.position, desiredPos, smoothSpeed * Time.deltaTime);
        transform.LookAt(targetCenter);
    }

    // Ponto médio entre os dois alvos (ou posição do único alvo se só houver um)
    Vector3 GetTargetCenter()
    {
        if (primaryTarget == null) return Vector3.zero;
        if (secondaryTarget == null) return primaryTarget.transform.position;
        return (primaryTarget.transform.position + secondaryTarget.transform.position) * 0.5f;
    }

    // Distância necessária para enquadrar os dois objetos com padding
    float GetRequiredDistance()
    {
        if (primaryTarget == null) return singleObjectDistance;
        if (secondaryTarget == null) return singleObjectDistance;

        float separation = Vector3.Distance(primaryTarget.transform.position,
                                            secondaryTarget.transform.position);
        return Mathf.Clamp(separation * framePadding, minDistance, maxDistance);
    }
}
using UnityEngine;
using System.Collections.Generic;
public class BarycentreCamera : MonoBehaviour
{
    [Header("Referências")]
    public StarSystemManager manager;

    [Header("Configurações de Órbita")]
    [Tooltip("Velocidade de rotação automática em graus por segundo")]
    public float orbitSpeed = 10f;
    [Tooltip("Ângulo de elevação acima do plano do sistema (0 = lateral, 90 = top-down)")]
    public float elevationAngle = 35f;
    [Tooltip("Padding extra para garantir que todos os objetos cabem no ecrã")]
    public float zoomPadding = 1.6f;
    public float minDistance = 20f;
    public float maxDistance = 800f;
    [Tooltip("Velocidade de suavização do movimento e zoom")]
    public float smoothSpeed = 2f;

    [Header("Rotação Manual")]
    [Tooltip("Sensibilidade do botão direito para orbitar manualmente")]
    public float sensitivity = 0.15f;
    private float currentAngle;
    private Vector3 currentBarycenter;
    private float currentDistance;

    void OnEnable()
    {
        List<StarComponent> stars = manager.GetStars();
        if (stars == null || stars.Count == 0) return;

        // Inicializa diretamente sem Lerp para não começar em Vector3.zero
        currentBarycenter = CalculateBarycenter();
        currentDistance   = Mathf.Clamp(
            Vector3.Distance(transform.position, currentBarycenter),
            minDistance, maxDistance);

        // Calcula o ângulo inicial a partir da posição atual da câmara para não haver salto brusco ao ativar
        Vector3 toCamera = transform.position - currentBarycenter;
        currentAngle = Mathf.Atan2(toCamera.x, toCamera.z) * Mathf.Rad2Deg;

        Debug.Log("[BarycentreCamera] Câmara de baricentro ativada.");
    }

    void Update()
    {
        List<StarComponent> stars = manager.GetStars();
        if (stars == null || stars.Count == 0) return;

        Vector3 barycenter = CalculateBarycenter();
        float requiredDistance = CalculateRequiredDistance(stars, barycenter);

        // Suaviza o baricentro e a distância ao longo do tempo
        currentBarycenter = Vector3.Lerp(currentBarycenter, barycenter, smoothSpeed * Time.deltaTime);
        currentDistance = Mathf.Lerp(currentDistance, requiredDistance, smoothSpeed * Time.deltaTime);

        // Botão direito — pausa a rotação automática e deixa orbitar manualmente
        // Arrastar horizontalmente roda o ângulo; verticalmente ajusta a elevação
        if (UnityEngine.InputSystem.Mouse.current.rightButton.isPressed)
        {
            Vector2 delta   = UnityEngine.InputSystem.Mouse.current.delta.ReadValue();
            currentAngle   += delta.x * sensitivity;
            elevationAngle  = Mathf.Clamp(elevationAngle - delta.y * sensitivity, 5f, 85f);
        }
        else
        {
            // Retoma a rotação automática quando o botão direito é largado
            currentAngle += orbitSpeed * Time.deltaTime;
        }

        // Calcula a posição da câmara em coordenadas esféricas
        float rad = currentAngle * Mathf.Deg2Rad;
        float elRad = elevationAngle * Mathf.Deg2Rad;
        float horizDist = currentDistance * Mathf.Cos(elRad);

        Vector3 offset = new Vector3(
            Mathf.Sin(rad) * horizDist,
            currentDistance * Mathf.Sin(elRad),
            Mathf.Cos(rad) * horizDist
        );

        transform.position = Vector3.Lerp(transform.position,
                                          currentBarycenter + offset,
                                          smoothSpeed * Time.deltaTime);
        transform.LookAt(currentBarycenter);
    }

    // Calcula o centro de massa ponderado do sistema
    Vector3 CalculateBarycenter()
    {
        List<StarComponent> stars = manager.GetStars();
        if (stars == null || stars.Count == 0) return Vector3.zero;

        Vector3 weighted = Vector3.zero;
        float totalMass = 0f;

        foreach (StarComponent sc in stars)
        {
            if (sc == null) continue;
            weighted += sc.transform.position * sc.mass;
            totalMass += sc.mass;
        }

        return totalMass > 0f ? weighted / totalMass : Vector3.zero;
    }

    // Calcula a distância mínima para que todos os objetos caibam no ecrã
    float CalculateRequiredDistance(List<StarComponent> stars, Vector3 barycenter)
    {
        float maxDist = minDistance;

        foreach (StarComponent sc in stars)
        {
            if (sc == null) continue;
            float d = Vector3.Distance(sc.transform.position, barycenter);
            if (d > maxDist) maxDist = d;
        }

        return Mathf.Clamp(maxDist * zoomPadding, minDistance, maxDistance);
    }
}
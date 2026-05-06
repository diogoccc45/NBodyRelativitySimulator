using UnityEngine;
using UnityEngine.InputSystem;

// Câmara orbital para a cena de Relatividade Geral
// Roda automaticamente à volta do centro da grid mostrando a curvatura
// O utilizador pode orbitar manualmente com clique direito e fazer zoom com scroll
public class OrbitalCamera : MonoBehaviour
{
    [Header("Alvo")]
    [Tooltip("Ponto central à volta do qual a câmara orbita — normalmente o centro da grid")]
    public Transform target;
    public Vector3 targetOffset = Vector3.zero; // offset do ponto de foco

    [Header("Distância")]
    public float distance = 120f;
    public float minDistance = 40f;
    public float maxDistance = 250f;
    public float scrollSpeed = 15f;

    [Header("Ângulos")]
    [Tooltip("Ângulo horizontal inicial em graus")]
    public float yaw = 0f;
    [Tooltip("Ângulo vertical — 90 = diretamente de cima, 20 = quase de lado")]
    public float pitch = 55f;
    public float minPitch = 15f;
    public float maxPitch = 88f;

    [Header("Rotação Automática")]
    [Tooltip("Ativa a rotação automática quando o utilizador não interage")]
    public bool autoRotate = true;
    [Tooltip("Velocidade de rotação automática em graus por segundo")]
    public float autoRotateSpeed = 6f;
    [Tooltip("Segundos sem input antes de retomar a rotação automática")]
    public float autoRotateDelay = 3f;

    [Header("Input Manual")]
    public float sensitivity = 0.4f;
    public float smoothSpeed = 8f;

    // Estado interno
    private float currentYaw;
    private float currentPitch;
    private float currentDistance;
    private float targetYaw;
    private float targetPitch;
    private float targetDistance;
    private float lastInputTime = -999f;
    private bool isDragging = false;

    void Start()
    {
        currentYaw = yaw;
        currentPitch = pitch;
        currentDistance = distance;
        targetYaw = yaw;
        targetPitch = pitch;
        targetDistance = distance;

        // Se não tiver target definido, aponta para a origem
        if (target == null)
        {
            GameObject pivot = new GameObject("OrbitalPivot");
            pivot.transform.position = Vector3.zero;
            target = pivot.transform;
        }
    }

    void LateUpdate()
    {
        HandleInput();
        UpdateAutoRotate();
        ApplyCamera();
    }

    void HandleInput()
    {
        // Scroll — zoom
        float scroll = Mouse.current.scroll.ReadValue().y;
        if (Mathf.Abs(scroll) > 0.01f)
        {
            targetDistance -= scroll * scrollSpeed * Time.deltaTime * 60f;
            targetDistance = Mathf.Clamp(targetDistance, minDistance, maxDistance);
            lastInputTime = Time.time;
        }

        // Clique direito — orbita manualmente
        if (Mouse.current.rightButton.wasPressedThisFrame)
        {
            isDragging = true;
            lastInputTime = Time.time;
        }
        if (Mouse.current.rightButton.wasReleasedThisFrame)
            isDragging = false;

        if (isDragging)
        {
            Vector2 delta = Mouse.current.delta.ReadValue();
            targetYaw += delta.x * sensitivity;
            targetPitch -= delta.y * sensitivity;
            targetPitch = Mathf.Clamp(targetPitch, minPitch, maxPitch);
            lastInputTime = Time.time;
        }
    }

    void UpdateAutoRotate()
    {
        if (!autoRotate) return;

        // Só roda automaticamente se o utilizador não interagiu recentemente
        if (Time.time - lastInputTime > autoRotateDelay)
            targetYaw += autoRotateSpeed * Time.deltaTime;
    }

    void ApplyCamera()
    {
        // Suaviza os valores atuais em direção aos alvos
        currentYaw = Mathf.LerpAngle(currentYaw, targetYaw, smoothSpeed * Time.deltaTime);
        currentPitch = Mathf.Lerp(currentPitch, targetPitch, smoothSpeed * Time.deltaTime);
        currentDistance = Mathf.Lerp(currentDistance, targetDistance, smoothSpeed * Time.deltaTime);

        // Converte esférico para cartesiano
        float pitchRad = currentPitch * Mathf.Deg2Rad;
        float yawRad = currentYaw * Mathf.Deg2Rad;

        Vector3 offset = new Vector3(
            currentDistance * Mathf.Cos(pitchRad) * Mathf.Sin(yawRad),
            currentDistance * Mathf.Sin(pitchRad),
            currentDistance * Mathf.Cos(pitchRad) * Mathf.Cos(yawRad)
        );

        Vector3 focusPoint = target.position + targetOffset;
        transform.position = focusPoint + offset;
        transform.LookAt(focusPoint);
    }
}
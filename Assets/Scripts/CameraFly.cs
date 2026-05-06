using UnityEngine;
using UnityEngine.InputSystem;

public class CameraFly : MonoBehaviour
{
    public float speed = 100f;

    [Header("Configurações de Rotação")]
    public float sensitivity = 0.15f;

    [Header("Sistema de Foco")]
    public MouseInteraction mouseInteraction;
    public float focusDistance = 30f;
    public float focusSmoothSpeed = 5f;

    // Yaw e pitch guardados separadamente
    // O yaw controla o movimento — o pitch só afeta para onde a câmara olha
    private float yaw   = 0f;
    private float pitch = 0f;

    void Start()
    {
        // Inicializa yaw e pitch com a rotação atual da câmara
        yaw   = transform.eulerAngles.y;
        pitch = transform.eulerAngles.x;
    }

    void Update()
    {
        // Rotação — atualiza yaw e pitch separadamente
        if (Mouse.current.rightButton.isPressed)
        {
            Vector2 delta = Mouse.current.delta.ReadValue();
            yaw   += delta.x * sensitivity;
            pitch -= delta.y * sensitivity;
            pitch  = Mathf.Clamp(pitch, -89f, 89f); // evita gimbal lock

            transform.rotation = Quaternion.Euler(pitch, yaw, 0f);
        }

        // Inputs
        float moveForward = 0f;
        float moveSide    = 0f;

        if (Keyboard.current.wKey.isPressed) moveForward =  1f;
        if (Keyboard.current.sKey.isPressed) moveForward = -1f;
        if (Keyboard.current.dKey.isPressed) moveSide    =  1f;
        if (Keyboard.current.aKey.isPressed) moveSide    = -1f;

        // Movimento para onde a câmara aponta — incluindo cima/baixo (FPS puro)
        Vector3 direction = (transform.forward * moveForward + transform.right * moveSide).normalized;

        transform.position += direction * speed * Time.deltaTime;

        // Turbo
        if (Keyboard.current.leftShiftKey.isPressed) speed = 500f;
        else speed = 100f;

        // Lógica para voltar ao último objeto (Tecla F)
        if (Keyboard.current.fKey.wasPressedThisFrame)
        {
            if (mouseInteraction != null && mouseInteraction.lastCreatedObject != null)
            {
                StopAllCoroutines();
                StartCoroutine(FocusOnLastObject(mouseInteraction.lastCreatedObject.transform.position));
            }
        }
    }

    System.Collections.IEnumerator FocusOnLastObject(Vector3 targetPos)
    {
        float elapsed = 0;
        Vector3 startPos = transform.position;
        Quaternion startRot = transform.rotation;

        Vector3 directionToCam = (startPos - targetPos).normalized;
        if (directionToCam == Vector3.zero) directionToCam = -Vector3.forward;
        Vector3 endPos = targetPos + (directionToCam * focusDistance);

        Quaternion endRot = Quaternion.LookRotation(targetPos - endPos);

        while (elapsed < 1.0f)
        {
            elapsed += Time.deltaTime * focusSmoothSpeed;
            transform.position = Vector3.Lerp(startPos, endPos, elapsed);
            transform.rotation = Quaternion.Slerp(startRot, endRot, elapsed);
            yield return null;
        }

        // Sincroniza yaw e pitch com a rotação final do foco
        yaw   = transform.eulerAngles.y;
        pitch = transform.eulerAngles.x;
    }
}
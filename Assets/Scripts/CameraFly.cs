using UnityEngine;
using UnityEngine.InputSystem;

public class CameraFly : MonoBehaviour
{
    public float speed = 100f;

    [Header("Configurações de Rotação")]
    public float sensitivity = 0.15f;

    [Header("Sistema de Foco")]
    public MouseInteraction mouseInteraction; // Referência ao script que guarda o último objeto
    public float focusDistance = 30f;          // Distância ideal para observar o objeto
    public float focusSmoothSpeed = 5f;        // Velocidade da viagem de regresso

    void Update()
    {
        // Inputs
        float moveForward = 0f;
        float moveSide = 0f;

        if (Keyboard.current.wKey.isPressed) moveForward = 1f;
        if (Keyboard.current.sKey.isPressed) moveForward = -1f;
        if (Keyboard.current.dKey.isPressed) moveSide = 1f;
        if (Keyboard.current.aKey.isPressed) moveSide = -1f;

        Vector3 direction = (transform.forward * moveForward + transform.right * moveSide).normalized;

        // Forcei a posição a mudar em todos os eixos (X, Y, Z) baseada na visão
        transform.position += direction * speed * Time.deltaTime;

        // Rotação
        if (Mouse.current.rightButton.isPressed)
        {
            Vector2 delta = Mouse.current.delta.ReadValue();
            
            // Aplicamos a sensibilidade ao delta do rato
            float rotX = delta.x * sensitivity;
            float rotY = delta.y * sensitivity;

            transform.Rotate(Vector3.up, rotX, Space.World);
            transform.Rotate(Vector3.left, rotY, Space.Self);
        }

        // Turbo
        if (Keyboard.current.leftShiftKey.isPressed) speed = 500f; 
        else speed = 100f;

        // Lógica para voltar ao último objeto (Tecla F)
        if (Keyboard.current.fKey.wasPressedThisFrame)
        {
            if (mouseInteraction != null && mouseInteraction.lastCreatedObject != null)
            {
                StopAllCoroutines(); // Para qualquer movimento de foco anterior
                StartCoroutine(FocusOnLastObject(mouseInteraction.lastCreatedObject.transform.position));
            }
        }
    }

    // Coroutine para mover a câmara suavemente até ao último astro criado
    System.Collections.IEnumerator FocusOnLastObject(Vector3 targetPos)
    {
        float elapsed = 0;
        Vector3 startPos = transform.position;
        Quaternion startRot = transform.rotation;

        // Calculei onde a câmara deve parar
        Vector3 directionToCam = (startPos - targetPos).normalized;
        if (directionToCam == Vector3.zero) directionToCam = -Vector3.forward;
        Vector3 endPos = targetPos + (directionToCam * focusDistance);

        // Rotação final olhando diretamente para o objeto
        Quaternion endRot = Quaternion.LookRotation(targetPos - endPos);

        while (elapsed < 1.0f)
        {
            elapsed += Time.deltaTime * focusSmoothSpeed;
            
            // Move e roda ao mesmo tempo usando interpolação
            transform.position = Vector3.Lerp(startPos, endPos, elapsed);
            transform.rotation = Quaternion.Slerp(startRot, endRot, elapsed);
            
            yield return null;
        }
    }
}
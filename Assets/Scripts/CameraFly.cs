using UnityEngine;
using UnityEngine.InputSystem;

public class CameraFly : MonoBehaviour
{
    public float speed = 100f;

    [Header("Configurações de Rotação")]
    public float sensitivity = 0.15f; // Adicionada a caixa de sensitivity com valor base mais suave

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
    }
}
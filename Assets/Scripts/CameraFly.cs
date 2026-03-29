using UnityEngine;
using UnityEngine.InputSystem;

public class CameraFly : MonoBehaviour
{
    public float speed = 50f;
    public float sensitivity = 2f;

    void Update()
    {
        // Movimento WASD
        float moveForward = 0f;
        float moveSide = 0f;

        if (Keyboard.current.wKey.isPressed) moveForward = 1f;
        if (Keyboard.current.sKey.isPressed) moveForward = -1f;
        if (Keyboard.current.dKey.isPressed) moveSide = 1f;
        if (Keyboard.current.aKey.isPressed) moveSide = -1f;

        // Usamos o forward da câmara mas projetamos no plano horizontal de forma segura
        Vector3 camForward = transform.forward;
        Vector3 flatForward = Vector3.Cross(transform.right, Vector3.up).normalized; 

        Vector3 right = transform.right;
        right.y = 0;
        right.Normalize();

        // Aplicamos o movimento
        Vector3 moveDirection = (flatForward * moveForward + right * moveSide);
        transform.position += moveDirection * speed * Time.deltaTime;

        // Rotação com botão direito do rato
        if (Mouse.current.rightButton.isPressed)
        {
            Vector2 delta = Mouse.current.delta.ReadValue();
            transform.Rotate(0, delta.x * sensitivity, 0, Space.World);
            transform.Rotate(-delta.y * sensitivity, 0, 0, Space.Self);
        }
    }
}
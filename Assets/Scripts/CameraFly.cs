using UnityEngine;

public class CameraFly : MonoBehaviour 
{
    public float speed = 50f;
    public float sensitivity = 2f;

    void Update() 
    {
        // Movimento WASD manual
        float moveForward = 0;
        float moveSide = 0;

        if (Input.GetKey(KeyCode.W)) moveForward = 1;
        if (Input.GetKey(KeyCode.S)) moveForward = -1;
        if (Input.GetKey(KeyCode.D)) moveSide = 1;
        if (Input.GetKey(KeyCode.A)) moveSide = -1;

        Vector3 direction = new Vector3(moveSide, 0, moveForward);
        transform.Translate(direction * speed * Time.deltaTime);
        
        // Rotação com o Botão Direito do Rato
        if (Input.GetMouseButton(1)) 
        {
            float rotX = Input.GetAxis("Mouse X") * sensitivity;
            float rotY = -Input.GetAxis("Mouse Y") * sensitivity;
            transform.Rotate(0, rotX, 0, Space.World);
            transform.Rotate(rotY, 0, 0, Space.Self);
        }
    }
}
using UnityEngine;
using UnityEngine.InputSystem;
public class StarFollowCamera : MonoBehaviour
{
    [Header("Configurações de Follow")]
    public Vector3 followOffset = new Vector3(0f, 2f, -8f);
    public float followSmoothSpeed = 8f;
    public float sensitivity = 0.15f;
    
    [Header("Distância Automática + Scroll")]
    public float starRadiusMultiplier = 4f; // offset = raio * este valor
    public float minDistance = 2f; // distância mínima ao objeto
    public float maxDistance = 200f; // distância máxima ao objeto
    public float scrollSpeed = 3f; // velocidade do scroll

    [Header("Inspetor")]
    public ObjectInspector inspector;
    private bool inspectorVisible = true;
    private Transform followTarget = null;
    private StarComponent followStar = null;

    // Chamado pelo CameraManager quando este script é desativado (saída de follow)
    void OnDisable()
    {
        if (inspector != null) inspector.Hide();
        followTarget = null;
        followStar = null;
    }

    void Update()
    {
        if (followTarget == null) return;

        // Toggle do painel com I
        if (Keyboard.current.iKey.wasPressedThisFrame && inspector != null)
        {
            inspectorVisible = !inspectorVisible;
            if (inspectorVisible) inspector.Show(followStar);
            else inspector.Hide();
        }

        UpdateFollow();
    }

    void UpdateFollow()
    {
        if (followTarget == null) return;

        // Scroll — aproxima ou afasta a câmara
        float scroll = Mouse.current.scroll.ReadValue().y;
        if (Mathf.Abs(scroll) > 0.01f)
        {
            // Move o offset ao longo da sua direção (aproxima/afasta)
            float currentDist = followOffset.magnitude;
            float newDist = Mathf.Clamp(currentDist - scroll * scrollSpeed, minDistance, maxDistance);
            followOffset = followOffset.normalized * newDist;
        }

        // Rotação com botão direito — orbita à volta do objeto
        if (Mouse.current.rightButton.isPressed)
        {
            Vector2 delta = Mouse.current.delta.ReadValue();
            Quaternion yaw = Quaternion.AngleAxis( delta.x * sensitivity, Vector3.up);
            Quaternion pitch = Quaternion.AngleAxis(-delta.y * sensitivity, transform.right);
            followOffset = pitch * yaw * followOffset;
        }

        Vector3 behindDir = Vector3.back;
        if (followStar != null && followStar.velocity.sqrMagnitude > 0.01f)
            behindDir = -followStar.velocity.normalized;

        Quaternion moveRot = Quaternion.LookRotation(-behindDir);
        Vector3 desiredPos = followTarget.position + moveRot * followOffset;

        transform.position = Vector3.Lerp(transform.position, desiredPos,
                                          followSmoothSpeed * Time.deltaTime);
        transform.LookAt(followTarget.position);
    }

    // Chamado pelo CameraManager quando o Mouse3 acerta num objeto
    public void EnterFollow(StarComponent star)
    {
        followTarget = star.transform;
        followStar = star;

        // Calcula offset automático com base no raio da estrela
        float radius = star.transform.localScale.x * 0.5f;
        float distance = Mathf.Clamp(radius * starRadiusMultiplier, minDistance, maxDistance);
        followOffset = new Vector3(0f, distance * 0.3f, -distance);

        inspectorVisible = true;
        if (inspector != null) inspector.Show(star);
        Debug.Log($"[StarFollowCamera] A seguir '{star.gameObject.name}' — distância: {distance:F1}");
    }
}
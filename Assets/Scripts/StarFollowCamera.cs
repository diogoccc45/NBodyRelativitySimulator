using UnityEngine;
using UnityEngine.InputSystem;
public class StarFollowCamera : MonoBehaviour
{
    [Header("Configurações de Follow")]
    public Vector3 followOffset = new Vector3(0f, 2f, -8f);
    public float followSmoothSpeed = 8f;
    public float sensitivity = 0.15f;

    [Header("Inspetor")]
    public ObjectInspector inspector;

    private bool isFollowing = false;
    private bool inspectorVisible = true;
    private Transform followTarget = null;
    private StarComponent followStar = null;
    private CameraFly cameraFly = null;

    void Awake()
    {
        cameraFly = GetComponent<CameraFly>();
    }

    void Update()
    {
        // Deteta Mouse3 aqui com Raycast para entrar em modo Follow
        if (Mouse.current.middleButton.wasPressedThisFrame)
        {
            if (isFollowing)
            {
                ExitFollow();
            }
            else
            {
                // Lança um raio do rato para ver se acerta numa estrela/planeta
                Ray ray = Camera.main.ScreenPointToRay(Mouse.current.position.ReadValue());
                if (Physics.Raycast(ray, out RaycastHit hit))
                {
                    StarComponent star = hit.collider.GetComponent<StarComponent>();
                    if (star != null)
                        EnterFollow(star);
                }
            }
        }

        if (!isFollowing) return;

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
        if (followTarget == null) { ExitFollow(); return; }

        // Rotação com botão direito — orbita à volta do objeto
        if (Mouse.current.rightButton.isPressed)
        {
            Vector2 delta = Mouse.current.delta.ReadValue();
            // Roda o offset à volta do objeto em vez de rodar a câmara no lugar
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

    public void EnterFollow(StarComponent star)
    {
        isFollowing = true;
        followTarget = star.transform;
        followStar = star;
        if (cameraFly != null) cameraFly.enabled = false;
        inspectorVisible = true;
        if (inspector != null) inspector.Show(star);
        Debug.Log($"[StarFollowCamera] A seguir '{star.gameObject.name}'");
    }

    void ExitFollow()
    {
        isFollowing = false;
        followTarget = null;
        followStar = null;
        if (cameraFly != null) cameraFly.enabled = true;
        if (inspector != null) inspector.Hide();
        Debug.Log("[StarFollowCamera] Voo livre.");
    }
}
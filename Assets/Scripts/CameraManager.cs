using UnityEngine;
using UnityEngine.InputSystem;
public class CameraManager : MonoBehaviour
{
    [Header("Referências das Câmaras")]
    public CameraFly cameraFly;
    public StarFollowCamera starFollowCamera;
    public DirectorCamera directorCamera;
    public BarycentreCamera barycentreCamera;
    public TwoBodyCamera twoBodyCamera;
    public enum CameraMode { Fly, Follow, Director, Barycentre, TwoBody }
    private CameraMode currentMode  = CameraMode.Fly;
    private CameraMode previousMode = CameraMode.Fly; // guardado para voltar após sair do Follow

    // Ordem do ciclo da tecla C (Follow não entra aqui — só se acede com Mouse3)
    private readonly CameraMode[] cycleOrder = new CameraMode[]
    {
        CameraMode.Fly,
        CameraMode.Director,
        CameraMode.Barycentre,
        CameraMode.TwoBody
    };

    void Start()
    {
        // Começa sempre em voo livre
        SetMode(CameraMode.Fly);
    }

    void Update()
    {
        // Tecla C — cicla para o próximo modo (exceto Follow, que é só via Mouse3)
        if (Keyboard.current.cKey.wasPressedThisFrame)
            CycleCamera();

        // Mouse3 — entra em Follow se clicar numa estrela/planeta
        //        — sai do Follow e volta ao modo anterior se já estiver em Follow
        if (Mouse.current.middleButton.wasPressedThisFrame)
            HandleMiddleClick();
    }

    // Avança para o próximo modo na ordem definida em cycleOrder
    void CycleCamera()
    {
        // Se estiver em Follow, o C sai do Follow e vai para o próximo do ciclo a seguir ao previousMode
        CameraMode baseMode = currentMode == CameraMode.Follow ? previousMode : currentMode;

        int currentIndex = System.Array.IndexOf(cycleOrder, baseMode);
        int nextIndex = (currentIndex + 1) % cycleOrder.Length;

        SetMode(cycleOrder[nextIndex]);
        Debug.Log($"[CameraManager] Modo: {currentMode}");
    }

    // Mouse3 sobre uma estrela/planeta — entra em Follow guardando o modo atual
    // Mouse3 estando já em Follow — volta ao modo anterior
    void HandleMiddleClick()
    {
        if (currentMode == CameraMode.Follow)
        {
            // Sai do follow e volta ao modo em que estava antes
            SetMode(previousMode);
            return;
        }

        // Lança raio do rato para ver se acerta num objeto
        Ray ray = Camera.main.ScreenPointToRay(Mouse.current.position.ReadValue());
        if (Physics.Raycast(ray, out RaycastHit hit))
        {
            StarComponent star = hit.collider.GetComponent<StarComponent>();
            if (star != null)
            {
                previousMode = currentMode; // guarda o modo atual para poder voltar
                starFollowCamera.EnterFollow(star);
                SetMode(CameraMode.Follow);
            }
        }
    }

    // Ativa o modo pedido e desativa todos os outros
    public void SetMode(CameraMode mode)
    {
        currentMode = mode;

        // Desativa tudo primeiro
        if (cameraFly != null) cameraFly.enabled = false;
        if (starFollowCamera != null) starFollowCamera.enabled = false;
        if (directorCamera != null) directorCamera.enabled = false;
        if (barycentreCamera != null) barycentreCamera.enabled = false;
        if (twoBodyCamera != null) twoBodyCamera.enabled = false;

        // Ativa só o que precisamos
        switch (mode)
        {
            case CameraMode.Fly:
                if (cameraFly != null) cameraFly.enabled = true;  break;
            case CameraMode.Follow:
                if (starFollowCamera != null) starFollowCamera.enabled = true;  break;
            case CameraMode.Director:
                if (directorCamera != null) directorCamera.enabled = true;  break;
            case CameraMode.Barycentre:
                if (barycentreCamera != null) barycentreCamera.enabled = true;  break;
            case CameraMode.TwoBody:
                if (twoBodyCamera != null) twoBodyCamera.enabled = true;  break;
        }
    }

    // Para permitir que outros scripts saibam o modo atual
    public CameraMode CurrentMode => currentMode;
}
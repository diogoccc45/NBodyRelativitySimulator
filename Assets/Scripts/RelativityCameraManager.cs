using UnityEngine;
using UnityEngine.InputSystem;

// Gere os modos de câmara da cena de Relatividade Geral
// Dois modos: Orbital (padrão) e Fly (limitado à zona da grid)
// Tecla C alterna entre os dois — não afeta o CameraManager da outra cena
public class RelativityCameraManager : MonoBehaviour
{
    [Header("Câmaras")]
    public OrbitalCamera orbitalCamera;
    public CameraFly flyCamera;

    [Header("Limites do Modo Fly")]
    [Tooltip("Distância máxima do centro da grid no plano XZ")]
    public float maxFlyRadius = 160f;
    [Tooltip("Altura mínima da câmara em modo Fly — não pode ir abaixo da grid")]
    public float minFlyHeight = 5f;
    [Tooltip("Altura máxima da câmara em modo Fly")]
    public float maxFlyHeight = 200f;
    [Tooltip("Centro da grid — normalmente Vector3.zero")]
    public Vector3 gridCenter = Vector3.zero;

    [Header("UI")]
    public TMPro.TextMeshProUGUI cameraModeText;

    public enum CameraMode {Orbital, Fly}
    private CameraMode currentMode = CameraMode.Orbital;

    void Start()
    {
        SetMode(CameraMode.Orbital);
    }

    void Update()
    {
        // Tecla C — alterna entre Orbital e Fly
        if (Keyboard.current.cKey.wasPressedThisFrame)
            ToggleMode();

        // Aplica limites se estiver em modo Fly
        if (currentMode == CameraMode.Fly)
            EnforceFlyLimits();
    }

    void ToggleMode()
    {
        SetMode(currentMode == CameraMode.Orbital ? CameraMode.Fly : CameraMode.Orbital);
    }

    public void SetMode(CameraMode mode)
    {
        currentMode = mode;

        switch (mode)
        {
            case CameraMode.Orbital:
                if (orbitalCamera != null) orbitalCamera.enabled = true;
                if (flyCamera != null) flyCamera.enabled = false;
                break;

            case CameraMode.Fly:
                if (orbitalCamera != null) orbitalCamera.enabled = false;
                if (flyCamera != null)
                {
                    flyCamera.enabled = true;
                    // Posiciona o Fly a partir de onde a Orbital estava para a transição ser suave
                    flyCamera.transform.position = orbitalCamera != null
                        ? orbitalCamera.transform.position
                        : new Vector3(0f, 80f, -80f);
                }
                break;
        }

        UpdateModeUI();
    }

    // Limita a câmara Fly à zona da grid — só nesta cena
    void EnforceFlyLimits()
    {
        if (flyCamera == null) return;

        Vector3 pos = flyCamera.transform.position;
        bool clamped = false;

        // Limite de altura
        if (pos.y < minFlyHeight) { pos.y = minFlyHeight; clamped = true; }
        if (pos.y > maxFlyHeight) { pos.y = maxFlyHeight; clamped = true; }

        // Limite de caixa quadrada — bate certo com os limites reais da grid
        // maxFlyRadius define metade do lado da caixa
        float minX = gridCenter.x - maxFlyRadius;
        float maxX = gridCenter.x + maxFlyRadius;
        float minZ = gridCenter.z - maxFlyRadius;
        float maxZ = gridCenter.z + maxFlyRadius;

        if (pos.x < minX) { pos.x = minX; clamped = true; }
        if (pos.x > maxX) { pos.x = maxX; clamped = true; }
        if (pos.z < minZ) { pos.z = minZ; clamped = true; }
        if (pos.z > maxZ) { pos.z = maxZ; clamped = true; }

        if (clamped) flyCamera.transform.position = pos;
    }

    void UpdateModeUI()
    {
        if (cameraModeText == null) return;
        cameraModeText.text = currentMode == CameraMode.Orbital
            ? "Câmara: Orbital (C para Fly)"
            : "Câmara: Fly (C para Orbital)";
    }

    public CameraMode CurrentMode => currentMode;
}
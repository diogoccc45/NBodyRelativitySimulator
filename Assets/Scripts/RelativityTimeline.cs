using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;

// Pausa e retoma o movimento dos planetas leves na cena de Relatividade
// Segue o mesmo padrão do SimulationTimeline do Laboratório — tecla Espaço
public class RelativityTimeline : MonoBehaviour
{
    [Header("Referências")]
    public RelativityManager relativityManager;

    [Header("UI")]
    public Image playPauseIcon;
    public Sprite playSprite;
    public Sprite pauseSprite;

    private bool isPaused = false;

    // Propriedade pública para o RelativityBody verificar se está pausado
    public bool IsPaused => isPaused;

    void Update()
    {
        // Espaço — pausa/retoma (igual ao SimulationTimeline do laboratório)
        if (Keyboard.current.spaceKey.wasPressedThisFrame)
            SetPaused(!isPaused);
    }

    public void SetPaused(bool paused)
    {
        isPaused = paused;

        if (playPauseIcon != null)
            playPauseIcon.sprite = paused ? playSprite : pauseSprite;
    }

    // Chamado pelo botão de play/pause na UI
    public void TogglePause() => SetPaused(!isPaused);
}
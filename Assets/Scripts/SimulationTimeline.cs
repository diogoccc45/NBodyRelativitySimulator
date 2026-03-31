using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using System.Collections.Generic;
public class SimulationTimeline : MonoBehaviour
{
    [Header("Referências")]
    public StarSystemManager manager;

    [Header("UI")]
    public Slider timelineSlider;   // barra de progresso
    public Image playPauseIcon;    // ícone de play/pause
    public Sprite playSprite;
    public Sprite pauseSprite;

    [Header("Configurações")]
    public float recordInterval  = 0.05f;  // grava a cada 50ms (20 snapshots/s)
    public float maxHistoryTime  = 30f;    // 30 segundos de histórico
    public float rewindSpeed     = 0.5f;   // velocidade de J e L

    // Estrutura de snapshot
    struct ObjectSnapshot
    {
        public int instanceID;
        public Vector3 position;
        public Vector3 velocity;
        public float mass;
        public bool isPlanet;
        public bool exists;  // false = objeto foi destruído neste frame
    }
    struct FrameSnapshot
    {
        public float time;
        public List<ObjectSnapshot> objects;
    }

    // Estado interno
    List<FrameSnapshot> history = new List<FrameSnapshot>();
    float recordTimer = 0f;
    bool isPaused = false;
    bool isRewinding = false;
    int replayIndex = -1;        // -1 = live
    float rewindTimer = 0f;

    int maxFrames => Mathf.CeilToInt(maxHistoryTime / recordInterval);

    void Update()
    {
        // PAUSE / RESUME (Espaço)
        if (Keyboard.current.spaceKey.wasPressedThisFrame)
            SetPaused(!isPaused);

        // RECUAR (J)
        if (Keyboard.current.jKey.isPressed)
        {
            if (!isPaused) SetPaused(true);
            rewindTimer += Time.unscaledDeltaTime * rewindSpeed;
            if (rewindTimer >= recordInterval)
            {
                rewindTimer = 0f;
                StepReplay(-1);
            }
        }

        // AVANÇAR (L) 
        if (Keyboard.current.lKey.isPressed)
        {
            if (!isPaused) SetPaused(true);
            rewindTimer += Time.unscaledDeltaTime * rewindSpeed;
            if (rewindTimer >= recordInterval)
            {
                rewindTimer = 0f;
                StepReplay(1);
            }
        }

        // Ao soltar J ou L, para o rewind timer
        if (Keyboard.current.jKey.wasReleasedThisFrame ||
            Keyboard.current.lKey.wasReleasedThisFrame)
            rewindTimer = 0f;

        // GRAVAR (só quando live e não pausado) 
        if (!isPaused && replayIndex == -1)
        {
            recordTimer += Time.deltaTime;
            if (recordTimer >= recordInterval)
            {
                recordTimer = 0f;
                RecordSnapshot();
            }
        }

        // SLIDER 
        if (timelineSlider != null)
        {
            // Atualiza o slider para refletir a posição atual
            if (history.Count > 1)
            {
                int idx = replayIndex == -1 ? history.Count - 1 : replayIndex;
                timelineSlider.SetValueWithoutNotify((float)idx / (history.Count - 1));
            }
        }
    }

    // GRAVAR SNAPSHOT
    void RecordSnapshot()
    {
        List<StarComponent> stars = manager.GetStars();
        if (stars == null) return;

        FrameSnapshot frame = new FrameSnapshot
        {
            time    = Time.time,
            objects = new List<ObjectSnapshot>()
        };

        foreach (StarComponent sc in stars)
        {
            if (sc == null) continue;
            frame.objects.Add(new ObjectSnapshot
            {
                instanceID = sc.gameObject.GetInstanceID(),
                position = sc.transform.position,
                velocity = sc.velocity,
                mass = sc.mass,
                isPlanet = sc.isPlanet,
                exists = true
            });
        }

        history.Add(frame);

        // Remove frames antigos para não exceder o limite de memória
        if (history.Count > maxFrames)
            history.RemoveAt(0);
    }

    // AVANÇAR / RECUAR UM FRAME
    void StepReplay(int direction)
    {
        if (history.Count == 0) return;

        // Define o índice de início
        if (replayIndex == -1)
            replayIndex = history.Count - 1;

        replayIndex = Mathf.Clamp(replayIndex + direction, 0, history.Count - 1);

        ApplySnapshot(history[replayIndex]);

        // Se chegámos ao frame mais recente, voltamos ao live
        if (replayIndex == history.Count - 1)
            replayIndex = -1;
    }

    // APLICAR SNAPSHOT AOS OBJETOS DA CENA
    void ApplySnapshot(FrameSnapshot frame)
    {
        List<StarComponent> stars = manager.GetStars();
        if (stars == null) return;

        foreach (StarComponent sc in stars)
        {
            if (sc == null) continue;

            ObjectSnapshot snap = frame.objects.Find(
                o => o.instanceID == sc.gameObject.GetInstanceID());

            if (snap.exists)
            {
                sc.transform.position = snap.position;
                sc.velocity = snap.velocity;
                sc.mass = snap.mass;
                sc.UpdateAppearance();

                // Limpa o TrailRenderer durante o replay para evitar rastos invertidos
                TrailRenderer trail = sc.GetComponent<TrailRenderer>();
                if (trail != null) trail.Clear();
            }
        }
    }
    // Restaura os TrailRenderers ao voltar ao live
    void RestoreTrails()
    {
        List<StarComponent> stars = manager.GetStars();
        if (stars == null) return;
        foreach (StarComponent sc in stars)
        {
            if (sc == null) continue;
            TrailRenderer trail = sc.GetComponent<TrailRenderer>();
            if (trail != null) trail.Clear();
        }
    }

    // PAUSE / RESUME
    void SetPaused(bool paused)
    {
        isPaused = paused;
        manager.SetPaused(paused);

        if (playPauseIcon != null)
            playPauseIcon.sprite = paused ? playSprite : pauseSprite;

        // Ao resumir do replay, volta ao live
        if (!paused && replayIndex != -1)
        {
            history.RemoveRange(replayIndex + 1, history.Count - replayIndex - 1);
            replayIndex = -1;
        }

        // Limpa rastos ao pausar/resumir para evitar artefactos visuais
        RestoreTrails();

        Debug.Log($"[Timeline] {(paused ? "Pausado" : "A correr")}");
    }

    // Chamado pelo slider de UI
    public void OnSliderChanged(float value)
    {
        if (history.Count < 2) return;
        if (!isPaused) SetPaused(true);

        replayIndex = Mathf.RoundToInt(value * (history.Count - 1));
        replayIndex = Mathf.Clamp(replayIndex, 0, history.Count - 1);
        ApplySnapshot(history[replayIndex]);
    }
}
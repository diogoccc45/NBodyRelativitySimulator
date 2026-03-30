using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using TMPro;
using System;

public class MouseInteraction : MonoBehaviour
{
    public StarSystemManager manager;
    public Slider massSlider;
    public TextMeshProUGUI massText;

    [Header("Configuração de Prefabs")]
    public GameObject starPrefab;
    public GameObject planetPrefab;
    private GameObject currentPrefab;
    
    [Header("Configuração do Preview")]
    public GameObject previewStar;
    public Material ghostMaterial;
    private GameObject ghostInstance;
    private StarComponent ghostComponent;

    [Header("Configuração do Lançamento")]
    public LineRenderer dragLine;
    private Vector3 dragStartPos;
    private bool isDragging = false;
    public float launchForceMultiplier = 0.5f;
    public float dragThreshold = 0.3f; // Distância mínima para validar o lançamento

    [HideInInspector] 
    public GameObject lastCreatedObject; // Variável para a câmara saber onde voltar

    void Start()
    {
        currentPrefab = starPrefab;
        ResetGhost();

        // Configuração inicial da linha visual
        if (dragLine != null)
        {
            dragLine.positionCount = 2;
            dragLine.enabled = false;
        }
    }

    // Função para botões trocarem entre Estrela e Planeta
    public void MudarTipo(bool eEstrela)
    {
        // Escolhe o prefab
        currentPrefab = eEstrela ? starPrefab : planetPrefab;

        if (eEstrela)
        {
            massSlider.minValue = 10;
            massSlider.maxValue = 500;
            massSlider.value = 100;
        }
        else
        {
            massSlider.minValue = 0.1f;
            massSlider.maxValue = 10f;
            massSlider.value = 1;
        }

        // Reset ao fantasma para mudar de visual no ecrã
        ResetGhost();
    }

    void ResetGhost()
    {
        if (ghostInstance != null) Destroy(ghostInstance);
        
        if (currentPrefab != null)
        {
            ghostInstance = Instantiate(currentPrefab);
            // Removi o TrailRenderer e a Física do fantasma para não interferir
            if (ghostInstance.TryGetComponent<TrailRenderer>(out var tr)) Destroy(tr);
            if (ghostInstance.TryGetComponent<Collider>(out var col)) Destroy(col);

            // Efeito ghost para não atrapalhar o utilizador e consequentemente a visualização das estrelas aleatórias criadas
            Renderer rend = ghostInstance.GetComponent<Renderer>();
            if (rend != null && ghostMaterial != null)
            {
                rend.material = ghostMaterial;
            }

            ghostComponent = ghostInstance.GetComponent<StarComponent>();
        }
    }

    void Update()
    {
        // Atualiza o texto da massa
        if (massText != null && massSlider != null)
            massText.text = "Massa a criar: " + massSlider.value.ToString("F2");

        HandleInput();
    }

    void HandleInput()
    {
        if (ghostInstance == null) return;

        // Deteta o início do clique (Prepara o nascimento da estrela)
        if (Input.GetMouseButtonDown(0) && !UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject())
        {
            isDragging = true;
            dragStartPos = GetMouseWorldPos();
            
            if (dragLine != null) dragLine.enabled = true;
        }

        // Enquanto o botão está a ser pressionado (Calcula a trajetória/força)
        if (isDragging)
        {
            Vector3 currentMousePos = GetMouseWorldPos();
            
            // O Fantasma fica fixo no ponto onde clicamos (onde a estrela vai nascer)
            ghostInstance.transform.position = dragStartPos;
            UpdateGhostVisuals();

            // Desenha a linha
            if (dragLine != null)
            {
                dragLine.SetPosition(0, dragStartPos);
                dragLine.SetPosition(1, currentMousePos);
            }

            // Se soltar o botão, cria com velocidade ou modo estático
            if (Input.GetMouseButtonUp(0))
            {
                isDragging = false;
                if (dragLine != null) dragLine.enabled = false;

                Vector3 dragEndPos = GetMouseWorldPos();
                
                // Calculei a distância do arrasto para evitar cliques simples sem querer
                float dragDistance = Vector3.Distance(dragStartPos, dragEndPos);

                // O + clique esquerdo - órbita automática em relação à estrela mais próxima
                if (Keyboard.current.oKey.isPressed && currentPrefab == planetPrefab)
                {
                    Vector3 orbitalVel = CalcOrbitalVelocity(dragStartPos);
                    lastCreatedObject = manager.CreateStarCustom(currentPrefab, dragStartPos, orbitalVel, massSlider.value);
                }
                // SHIFT - cria parado
                else if (Keyboard.current.leftShiftKey.isPressed || Keyboard.current.rightShiftKey.isPressed)
                {
                    lastCreatedObject = manager.CreateStarCustom(currentPrefab, dragStartPos, Vector3.zero, massSlider.value);
                }
                // Estilingue normal
                else if (dragDistance > dragThreshold)
                {
                    Vector3 launchVelocity = (dragStartPos - dragEndPos) * launchForceMultiplier;
                    lastCreatedObject = manager.CreateStarCustom(currentPrefab, dragStartPos, launchVelocity, massSlider.value);
                }
            }
        }
        else
        {
            // Se não está a arrastar, o fantasma segue o rato normalmente para exploração
            MovePreview();
            if (dragLine != null) dragLine.enabled = false;
        }
    }

    void MovePreview()
    {
        if (ghostInstance == null || Camera.main == null) return;

        ghostInstance.transform.position = GetMouseWorldPos();
        UpdateGhostVisuals();
    }

    void UpdateGhostVisuals()
    {
        if (ghostInstance == null || ghostComponent == null || massSlider == null) return;

        ghostComponent.mass = massSlider.value;
        ghostComponent.UpdateAppearance();

        Renderer rend = ghostInstance.GetComponent<Renderer>();
        if (rend != null)
        {
            float t = Mathf.InverseLerp(massSlider.minValue, massSlider.maxValue, massSlider.value);
            Color targetColor;

            if (currentPrefab == starPrefab)
            {
                targetColor = Color.Lerp(Color.red, Color.cyan, t);
            }
            else
            {
                // Gradiente do Sistema Solar para Planetas
                // 0.0 (Mercúrio/Marte) -> 0.5 (Júpiter/Saturno) -> 1.0 (Neptuno)
                if (t < 0.5f)
                    targetColor = Color.Lerp(new Color(0.7f, 0.4f, 0.3f), new Color(0.9f, 0.7f, 0.5f), t * 2); // Castanho a Bege
                else
                    targetColor = Color.Lerp(new Color(0.9f, 0.7f, 0.5f), new Color(0.2f, 0.5f, 1.0f), (t - 0.5f) * 2); // Bege a Azul
            }

            targetColor.a = 0.4f;
            rend.material.color = targetColor;
            rend.material.SetColor("_BaseColor", targetColor);
        }
    }

    public float spawnDistance = 20f;

    Vector3 GetMouseWorldPos()
    {
        if (Camera.main == null) return Vector3.zero;
        Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
        Plane plane = new Plane(-Camera.main.transform.forward,
                                Camera.main.transform.position + Camera.main.transform.forward * spawnDistance);
        if (plane.Raycast(ray, out float distance))
            return ray.GetPoint(distance);
        return ray.GetPoint(spawnDistance);
    }

    // Calcula a velocidade orbital ideal em relação à estrela mais próxima de 'position'.
    // Usa a fórmula v = sqrt(G * M / r), perpendicular ao vetor posição-estrela.
    Vector3 CalcOrbitalVelocity(Vector3 position)
    {
        // Pede ao manager a lista de estrelas para encontrar a mais próxima
        StarComponent nearest = manager.GetNearestStar(position);
        if (nearest == null) return Vector3.zero;

        Vector3 toStar = nearest.transform.position - position;
        float   r      = toStar.magnitude;
        if (r < 0.01f) return Vector3.zero;

        float speed = Mathf.Sqrt(manager.G * nearest.mass / r);

        // Direção perpendicular ao vetor posição->estrela, no plano da câmara
        Vector3 perpendicular = Vector3.Cross(toStar.normalized, Camera.main.transform.forward).normalized;

        return perpendicular * speed;
    }
}
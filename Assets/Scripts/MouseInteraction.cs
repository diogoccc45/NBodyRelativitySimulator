using UnityEngine;
using UnityEngine.UI;
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
                
                // Calculamos a distância do arrasto para evitar cliques simples sem querer
                float dragDistance = Vector3.Distance(dragStartPos, dragEndPos);

                // SE segurar SHIFT, cria parado
                if (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift))
                {
                    // Guardamos a referência para o sistema de foco
                    lastCreatedObject = manager.CreateStarCustom(currentPrefab, dragStartPos, Vector3.zero, massSlider.value);
                }
                // SENÃO, verifica se o arrasto é suficiente para o lançamento
                else if (dragDistance > dragThreshold)
                {
                    // Vetor de lançamento: Ponto inicial menos ponto final (direção oposta ao arrasto)
                    Vector3 launchVelocity = (dragStartPos - dragEndPos) * launchForceMultiplier;
                    // Guardamos a referência para o sistema de foco
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

    // Distância fixa à câmara onde o ghost e as estrelas são criados.
    public float spawnDistance = 30f;

    Vector3 GetMouseWorldPos()
    {
    if (Camera.main == null) return Vector3.zero;

    // Criei um raio que sai do ponteiro do rato
    Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
    
    // Criei um plano que está sempre virado para a câmara a 20 metros de distância
    // Isto garante que o ghost acompanha o rato perfeitamente em 3D
    Plane plane = new Plane(-Camera.main.transform.forward, Camera.main.transform.position + Camera.main.transform.forward * 20f);
    
    if (plane.Raycast(ray, out float distance))
    {
        return ray.GetPoint(distance);
    }
    
    return ray.GetPoint(20f);
    }
}
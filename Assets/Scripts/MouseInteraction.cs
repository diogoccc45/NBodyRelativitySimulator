using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System;

public class MouseInteraction : MonoBehaviour
{
    public StarSystemManager manager;
    public Slider massSlider;
    public TextMeshProUGUI massText;
    
    [Header("Configuração do Preview")]
    public GameObject previewStar;
    public Material ghostMaterial;
    private GameObject ghostInstance;
    private StarComponent ghostComponent;

    [Header("Configuração do Lançamento")]
    public LineRenderer dragLine; // Arrastar o componente LineRenderer para aqui no Inspector
    private Vector3 dragStartPos;
    private bool isDragging = false;
    public float launchForceMultiplier = 0.5f; // Ajusta a sensibilidade do lançamento

    void Start()
    {
        // Criamos a instância do "Fantasma" no início
        if (previewStar != null)
        {
            ghostInstance = Instantiate(previewStar);
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

        // Configuração inicial da linha visual
        if (dragLine != null)
        {
            dragLine.positionCount = 2;
            dragLine.enabled = false;
        }
    }

    void Update()
    {
        // Atualiza o texto da massa
        if (massText != null && massSlider != null)
            massText.text = "Massa a criar: " + massSlider.value;

        HandleInput();
    }

    void HandleInput()
    {
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

            // Se soltar o botão, cria com velocidade
            if (Input.GetMouseButtonUp(0))
            {
                isDragging = false;
                if (dragLine != null) dragLine.enabled = false;

                Vector3 dragEndPos = GetMouseWorldPos();
                
                // Vetor de lançamento: Ponto inicial menos ponto final (direção oposta ao arrasto)
                Vector3 launchVelocity = (dragStartPos - dragEndPos) * launchForceMultiplier;

                // Cria a estrela real com a velocidade calculada (se não arrastou, a velocidade é 0)
                manager.CreateStar(dragStartPos, launchVelocity, massSlider.value);
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
        // Atualiza o aspeto do fantasma em tempo real conforme o Slider
        if (ghostComponent != null && massSlider != null)
        {
            ghostComponent.mass = massSlider.value;
            ghostComponent.UpdateAppearance();

            Renderer rend = ghostInstance.GetComponent<Renderer>();
            if (rend != null)
            {
                float t = Mathf.InverseLerp(10f, 500f, massSlider.value);
                Color targetColor = Color.Lerp(Color.red, Color.cyan, t);

                targetColor.a = 0.2f;

                rend.material.color = targetColor;
                rend.material.SetColor("_BaseColor", targetColor);
            }
        }
    }

    // Função auxiliar para converter posição do rato para o mundo 3D (plano Y=0)
    Vector3 GetMouseWorldPos()
    {
        Vector3 mousePos = Input.mousePosition;
        mousePos.z = Camera.main.transform.position.y;
        Vector3 worldPos = Camera.main.ScreenToWorldPoint(mousePos);
        worldPos.y = 0;
        return worldPos;
    }
}
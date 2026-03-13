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
    }

    void Update()
    {
        // Atualiza o texto da massa
        if (massText != null && massSlider != null)
            massText.text = "Massa a criar: " + massSlider.value;

        // Posiciona o Ghost Preview no Rato
        MovePreview();

        // Clique para Criar (Botão Esquerdo)
        // O !IsPointerOverGameObject impede de criar estrelas ao clicar no Slider
        if (Input.GetMouseButtonDown(0) && !UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject())
        {
            SpawnAtMouse();
        }
    }

    void MovePreview()
    {
        if (ghostInstance == null || Camera.main == null) return;

        Vector3 mousePos = Input.mousePosition;
        mousePos.z = Camera.main.transform.position.y;
        Vector3 worldPos = Camera.main.ScreenToWorldPoint(mousePos);
        worldPos.y = 0;

        ghostInstance.transform.position = worldPos;

        // Atualiza o aspeto do fantasma em tempo real conforme o Slider
        if (ghostComponent != null && massSlider != null)
        {
            ghostComponent.mass = massSlider.value;
            ghostComponent.UpdateAppearance();

            Renderer rend = ghostInstance.GetComponent<Renderer>();
            if (rend != null)
            {
                float t = Mathf.InverseLerp(10f,500f, massSlider.value);
                Color targetColor = Color.Lerp(Color.red, Color.cyan, t);

                targetColor.a = 0.2f;

                rend.material.color = targetColor;
                rend.material.SetColor("_BaseColor", targetColor);
            }
        }
    }

    void SpawnAtMouse()
    {
        if (ghostInstance == null) return;
        // Cria a estrela real na posição atual do fantasma com a massa do slider
        manager.CreateStar(ghostInstance.transform.position, Vector3.zero, massSlider.value);
    }
}
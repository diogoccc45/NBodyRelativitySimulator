using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class SidePanelController : MonoBehaviour
{
    private RectTransform rect;
    private bool isOpen = false;

    [Header("Configurações de Movimento")]
    public float speed = 10f;
    private Vector2 hiddenPos;
    private Vector2 visiblePos;

    [Header("Interface do Botão")]
    public TextMeshProUGUI buttonText;
    public string openSymbol = ">";
    public string closedSymbol = "<";

    void Start()
    {
        rect = GetComponent<RectTransform>();

        visiblePos = new Vector2(0, 0);
        
        // A posição escondida é a largura do painel
        hiddenPos = new Vector2(rect.rect.width, 0);

        // Começa fechado
        rect.anchoredPosition = hiddenPos;
        UpdateVisuals();
    }

    void Update()
    {
        Vector2 target = isOpen ? visiblePos : hiddenPos;
        // Interpolação suave
        rect.anchoredPosition = Vector2.Lerp(rect.anchoredPosition, target, Time.deltaTime * speed);
    }

    public void TogglePanel()
    {
        isOpen = !isOpen;
        UpdateVisuals();
    }

    void UpdateVisuals()
    {
        if (buttonText != null)
        {
            // Se o painel está aberto, a seta aponta para a direita (para fechar)
            // Se está fechado, aponta para a esquerda (para abrir)
            buttonText.text = isOpen ? openSymbol : closedSymbol;
        }
    }
}
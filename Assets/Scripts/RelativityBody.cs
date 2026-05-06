using UnityEngine;

// Representa uma massa na cena de Relatividade Geral
// Massas pesadas (deformsGrid = true) ficam fixas e deformam a grid
// Massas leves (deformsGrid = false) deslizam pela curvatura criada pelas massas pesadas
public class RelativityBody : MonoBehaviour
{
    [Header("Propriedades")]
    public float mass = 100f;
    [Tooltip("Se true, esta massa deforma a grid e fica fixa. Se false, desliza pela curvatura.")]
    public bool deformsGrid = true;

    [Header("Referências")]
    public SpacetimeGrid grid;
    // Referência ao timeline — preenchida automaticamente pelo RelativityManager
    [HideInInspector] public RelativityTimeline timeline;

    [Header("Movimento (só massas leves)")]
    [Tooltip("Velocidade inicial da massa leve")]
    public Vector3 initialVelocity = Vector3.zero;
    [Tooltip("Multiplicador da força de deslize — quanto mais alto, mais rápido desliza")]
    public float slideForce = 25f;
    [Tooltip("Amortecimento muito suave — 1.0 = sem fricção, 0.95 = fricção leve")]
    [Range(0.98f, 1.0f)]
    public float damping = 0.999f;

    private Vector3 velocity;
    private bool isDragging = false; // true quando o RelativityManager está a arrastar este corpo

    void Start()
    {
        velocity = initialVelocity;

        // Regista-se na grid ao nascer
        if (grid != null) grid.RegisterBody(this);

        // Coloca a massa na altura correta da grid imediatamente
        SnapToGrid();
    }

    void OnDestroy()
    {
        // Remove-se da grid ao ser destruído
        if (grid != null) grid.UnregisterBody(this);
    }

    void Update()
    {
        if (grid == null) return;

        // Massas pesadas ficam fixas — só precisam de se manter na altura da grid
        if (deformsGrid)
        {
            SnapToGrid();
            return;
        }

        // Massas leves — deslizam pela curvatura se não estão a ser arrastadas nem pausadas
        if (!isDragging && (timeline == null || !timeline.IsPaused))
            SlideAlongGrid();
        else
            SnapToGrid(); // enquanto arrasta ou pausado, mantém-se colada à grid
    }

    // Cola o objeto à superfície da grid no ponto XZ atual
    void SnapToGrid()
    {
        if (grid == null) return;
        float gridY = grid.GetGridHeightAt(transform.position.x, transform.position.z);
        // Senta o objeto ligeiramente acima da grid para não intersetar a mesh
        transform.position = new Vector3(transform.position.x, gridY + GetRadius(), transform.position.z);
    }

    // Física de deslize — segue o gradiente da grid (descida mais íngreme)
    void SlideAlongGrid()
    {
        // Gradiente aponta na direção da descida
        Vector3 gradient = grid.GetGridGradientAt(transform.position.x, transform.position.z);

        // Força proporcional à inclinação
        Vector3 force = gradient * slideForce;
        velocity += force * Time.deltaTime;

        // Amortecimento — simula fricção suave
        velocity *= Mathf.Pow(damping, Time.deltaTime * 60f);

        // Amortecimento progressivo nas bordas — evita que o planeta saia da grid
        // Quanto mais perto da borda, mais forte o amortecimento
        if (grid != null)
        {
            float gridHalf = grid.GridWorldSize * 0.5f;
            float edgeZone = gridHalf * 0.15f;

            float distFromEdgeX = gridHalf - Mathf.Abs(transform.position.x - grid.transform.position.x);
            float distFromEdgeZ = gridHalf - Mathf.Abs(transform.position.z - grid.transform.position.z);
            float minEdgeDist = Mathf.Min(distFromEdgeX, distFromEdgeZ);

            if (minEdgeDist < edgeZone)
            {
                float edgeFactor = 1f - Mathf.Clamp01(minEdgeDist / edgeZone);
                velocity *= 1f - edgeFactor * 0.15f;
            }
        }

        // Move no plano XZ
        Vector3 newPos = transform.position + new Vector3(velocity.x, 0f, velocity.z) * Time.deltaTime;

        // Mantém dentro dos limites da grid
        float half = grid.GridWorldSize * 0.5f;
        newPos.x = Mathf.Clamp(newPos.x, grid.transform.position.x - half, grid.transform.position.x + half);
        newPos.z = Mathf.Clamp(newPos.z, grid.transform.position.z - half, grid.transform.position.z + half);

        transform.position = newPos;
        SnapToGrid();
    }

    // Raio do objeto — usado para sentar o objeto acima da grid sem intersectar
    float GetRadius()
    {
        return transform.localScale.x * 0.5f;
    }

    // Chamado pelo RelativityManager ao iniciar o drag
    public void StartDrag()
    {
        isDragging = true;
        velocity = Vector3.zero; // reset da velocidade ao pegar
    }

    // Chamado pelo RelativityManager ao largar o drag
    public void EndDrag(Vector3 releaseVelocity)
    {
        isDragging = false;
        // Passa a velocidade do drag para o corpo — multiplicador aumentado para órbitas viáveis
        if (!deformsGrid) velocity = releaseVelocity * 0.8f;
    }

    // Move o corpo para uma posição XZ (chamado pelo RelativityManager durante o drag)
    public void MoveTo(Vector3 worldPos)
    {
        transform.position = new Vector3(worldPos.x, transform.position.y, worldPos.z);
        SnapToGrid();
    }
}
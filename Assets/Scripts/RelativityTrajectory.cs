using UnityEngine;

// Simula e desenha a trajetória prevista de uma massa leve na grid de espaço-tempo
// Chamado pelo RelativityManager durante o drag — amarelo = velocidade segura, vermelho = vai sair
[RequireComponent(typeof(LineRenderer))]
public class RelativityTrajectory : MonoBehaviour
{
    [Header("Referências")]
    public SpacetimeGrid grid;

    [Header("Simulação")]
    [Tooltip("Número de passos de simulação — mais passos = linha mais longa mas mais pesado")]
    public int   trajectorySteps = 80;
    [Tooltip("Tamanho de cada passo de tempo — maior = linha mais longa")]
    public float trajectoryStepSize = 0.05f;
    [Tooltip("slideForce usado na simulação — deve ser igual ao do RelativityBody")]
    public float slideForce = 25f;
    [Tooltip("damping usado na simulação — deve ser igual ao do RelativityBody")]
    public float damping = 0.999f;

    [Header("Cor")]
    [Tooltip("Velocidade abaixo da qual a linha é amarela (segura)")]
    public float safeSpeed   = 15f;
    [Tooltip("Velocidade acima da qual a linha é completamente vermelha (vai sair)")]
    public float dangerSpeed = 35f;
    public Color colorSafe = new Color(1f, 0.9f, 0f, 0.8f); // amarelo
    public Color colorDanger = new Color(1f, 0.1f, 0f, 0.8f); // vermelho

    private LineRenderer lr;

    void Awake()
    {
        lr = GetComponent<LineRenderer>();
        lr.useWorldSpace = true;
        lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        lr.receiveShadows = false;
        lr.enabled = false;
    }

    // Chamado pelo RelativityManager durante o drag com a posição e velocidade atuais
    public void DrawPreview(Vector3 startPos, Vector3 startVel)
    {
        if (grid == null) { lr.enabled = false; return; }

        // Cor binária — amarelo se fica dentro da grid, vermelho se sai
        bool willExit = false;
        Color color = colorSafe;

        lr.enabled = true;
        lr.positionCount = trajectorySteps;
        lr.startWidth = 0.4f;
        lr.endWidth = 0.05f;

        Vector3 pos = startPos;
        Vector3 vel = new Vector3(startVel.x, 0f, startVel.z); // só plano XZ
        float half = grid.GridWorldSize * 0.5f;

        for (int step = 0; step < trajectorySteps; step++)
        {
            // Altura da grid neste ponto
            float gridY = grid.GetGridHeightAt(pos.x, pos.z);
            Vector3 drawPos = new Vector3(pos.x, gridY + 0.5f, pos.z); // ligeiramente acima da grid
            lr.SetPosition(step, drawPos);

            // Simula um passo — mesmo cálculo do RelativityBody.SlideAlongGrid
            Vector3 gradient = grid.GetGridGradientAt(pos.x, pos.z);
            Vector3 force = gradient * slideForce;
            vel += force * trajectoryStepSize;
            vel *= Mathf.Pow(damping, trajectoryStepSize * 60f);

            Vector3 newPos = pos + vel * trajectoryStepSize;

            // Para a simulação se sair da grid — linha termina na borda
            if (Mathf.Abs(newPos.x - grid.transform.position.x) > half ||
                Mathf.Abs(newPos.z - grid.transform.position.z) > half)
            {
                willExit = true;
                // Preenche o resto da linha com o último ponto
                for (int rest = step + 1; rest < trajectorySteps; rest++)
                    lr.SetPosition(rest, drawPos);
                break;
            }

            pos = newPos;
        }

        // Aplica a cor binária depois de saber se sai ou não
        color = willExit ? colorDanger : colorSafe;
        lr.startColor = color;
        lr.endColor = new Color(color.r, color.g, color.b, 0f);
    }

    // Esconde a linha quando o drag termina
    public void Hide()
    {
        lr.enabled = false;
    }
}
using UnityEngine;
using System.Collections.Generic;

[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class SpacetimeGrid : MonoBehaviour
{
    [Header("Dimensões da Grid")]
    [Tooltip("Número de divisões por lado — 20 é um bom ponto de partida")]
    public int gridSize = 20;
    [Tooltip("Espaçamento entre vértices em game units")]
    public float cellSize = 3f;

    [Header("Deformação")]
    [Tooltip("Profundidade máxima de deformação — controla o quanto uma massa afunda a grid")]
    public float deformStrength = 15f;
    [Tooltip("Raio de influência de cada massa — quanto maior, mais suave e larga a depressão")]
    public float deformRadius = 18f;
    [Tooltip("Expoente da curva de deformação — valores mais altos criam depressões mais nítidas")]
    public float deformFalloff = 1.8f;
    [Tooltip("Massa de referência para normalizar a deformação — estrela com esta massa afunda deformStrength")]
    public float referenceMass = 200f;

    [Header("Visual")]
    public Material gridMaterial;

    [Header("Ondas Gravitacionais")]
    public GravitationalWaves waves;

    // Lista de massas que deformam a grid — gerida pelo RelativityManager
    private List<RelativityBody> bodies = new List<RelativityBody>();

    // Dados da mesh
    private Mesh mesh;
    private Vector3[] baseVertices;// posições originais (plano plano)
    private Vector3[] vertices; // posições deformadas (atualizadas em LateUpdate)

    // Dimensão total da grid em game units — usada pelo RelativityManager para colocar objetos
    public float GridWorldSize => gridSize * cellSize;

    void Start()
    {
        GenerateGrid();
    }

    // Gera a mesh plana inicial — chamado uma vez no Start
    void GenerateGrid()
    {
        mesh = new Mesh();
        mesh.name = "SpacetimeGrid";

        // Grid de (gridSize+1)^2 vértices
        int vCount = (gridSize + 1) * (gridSize + 1);
        baseVertices = new Vector3[vCount];
        vertices = new Vector3[vCount];
        Vector2[] uvs = new Vector2[vCount];

        float totalSize = gridSize * cellSize;
        float halfSize = totalSize * 0.5f;

        // Preenche os vértices no plano XZ centrado na origem
        for (int z = 0; z <= gridSize; z++)
        {
            for (int x = 0; x <= gridSize; x++)
            {
                int idx = z * (gridSize + 1) + x;
                float px = x * cellSize - halfSize;
                float pz = z * cellSize - halfSize;

                baseVertices[idx] = new Vector3(px, 0f, pz);
                vertices[idx] = baseVertices[idx];
                uvs[idx] = new Vector2((float)x / gridSize, (float)z / gridSize);
            }
        }

        // Triângulos — dois por célula
        int[] triangles = new int[gridSize * gridSize * 6];
        int t = 0;
        for (int z = 0; z < gridSize; z++)
        {
            for (int x = 0; x < gridSize; x++)
            {
                int v0 = z * (gridSize + 1) + x;
                int v1 = v0 + 1;
                int v2 = v0 + (gridSize + 1);
                int v3 = v2 + 1;

                // Triângulo 1
                triangles[t++] = v0;
                triangles[t++] = v2;
                triangles[t++] = v1;

                // Triângulo 2
                triangles[t++] = v1;
                triangles[t++] = v2;
                triangles[t++] = v3;
            }
        }

        mesh.vertices = vertices;
        mesh.triangles = triangles;
        mesh.uv = uvs;
        mesh.RecalculateNormals();

        GetComponent<MeshFilter>().mesh = mesh;

        MeshRenderer rend = GetComponent<MeshRenderer>();
        if (gridMaterial != null) rend.material = gridMaterial;
        rend.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        rend.receiveShadows = false;
    }

    // Chamado pelo RelativityManager quando um corpo é adicionado ou removido
    public void RegisterBody(RelativityBody body)
    {
        if (!bodies.Contains(body)) bodies.Add(body);
    }

    public void UnregisterBody(RelativityBody body)
    {
        bodies.Remove(body);
    }

    // MeshCollider atualizado com a deformação — permite raycast preciso contra a grid
    private MeshCollider meshCollider;

    void LateUpdate()
    {
        if (mesh == null) return;
        DeformGrid();
    }

    // Recalcula a deformação de todos os vértices com base nas massas registadas
    void DeformGrid()
    {
        // Remove referências nulas (corpos destruídos)
        bodies.RemoveAll(b => b == null);

        for (int i = 0; i < baseVertices.Length; i++)
        {
            Vector3 basePos    = baseVertices[i];
            float totalDeform  = 0f;

            foreach (RelativityBody body in bodies)
            {
                if (body == null) continue;

                // Massas leves podem deformar a grid ligeiramente se configurado
                bool canDeform = body.deformsGrid ||
                                 (!body.deformsGrid && body.lightBodyDeformsGrid);
                if (!canDeform) continue;

                float bRadius = body.overrideDeformation ? body.customDeformRadius : deformRadius;
                float bStrength= body.overrideDeformation ? body.customDeformStrength : deformStrength;
                float bFalloff = body.overrideDeformation ? body.customDeformFalloff : deformFalloff;

                // Massas leves usam uma fração da massa para não afundar a grid
                float effectiveMass = body.deformsGrid
                    ? body.mass
                    : body.mass * body.lightDeformMassFraction;

                float dx = basePos.x - body.transform.position.x;
                float dz = basePos.z - body.transform.position.z;
                float dist = Mathf.Sqrt(dx * dx + dz * dz);

                if (dist > bRadius) continue;

                float tVal = 1f - Mathf.Clamp01(dist / bRadius);
                float curve = Mathf.Pow(tVal, bFalloff);
                float massRatio = Mathf.Clamp(effectiveMass / referenceMass, 0f, 5f);
                totalDeform += curve * bStrength * massRatio;
            }

            // Aplica a deformação no eixo Y (para baixo)
            // Adiciona a deformação das ondas gravitacionais se existirem
            float waveDeform = (waves != null) ? waves.GetWaveDeformAt(basePos.x, basePos.z) : 0f;
            vertices[i] = new Vector3(basePos.x, -(totalDeform + waveDeform), basePos.z);
        }

        mesh.vertices = vertices;
        mesh.RecalculateNormals(); // necessário para o lighting reagir à curvatura

        // Atualiza o MeshCollider com a mesh deformada — permite raycast preciso
        if (meshCollider == null) meshCollider = GetComponent<MeshCollider>();
        if (meshCollider != null) meshCollider.sharedMesh = mesh;
    }

    // Devolve a posição Y da grid num ponto XZ qualquer — usado pelo RelativityBody para manter as massas leves "coladas" à superfície da grid
    public float GetGridHeightAt(float worldX, float worldZ)
    {
        float halfSize = GridWorldSize * 0.5f;

        // Converte coordenadas do mundo para índices da grid
        float localX = (worldX - transform.position.x + halfSize) / cellSize;
        float localZ = (worldZ - transform.position.z + halfSize) / cellSize;

        int x0 = Mathf.Clamp(Mathf.FloorToInt(localX), 0, gridSize - 1);
        int z0 = Mathf.Clamp(Mathf.FloorToInt(localZ), 0, gridSize - 1);
        int x1 = x0 + 1;
        int z1 = z0 + 1;

        // Interpolação bilinear entre os quatro vértices vizinhos
        float fx = localX - x0;
        float fz = localZ - z0;

        float h00 = vertices[z0 * (gridSize + 1) + x0].y;
        float h10 = vertices[z0 * (gridSize + 1) + x1].y;
        float h01 = vertices[z1 * (gridSize + 1) + x0].y;
        float h11 = vertices[z1 * (gridSize + 1) + x1].y;

        return Mathf.Lerp(
            Mathf.Lerp(h00, h10, fx),
            Mathf.Lerp(h01, h11, fx),
            fz
        );
    }

    // Devolve o gradiente (inclinação) da grid num ponto XZ
    // Usado pelo RelativityBody para calcular a direção em que a massa leve deve deslizar
    public Vector3 GetGridGradientAt(float worldX, float worldZ)
    {
        // Amostra a altura em 4 pontos vizinhos para calcular o gradiente numérico
        float sampleOffset = cellSize * 0.5f;

        float hRight = GetGridHeightAt(worldX + sampleOffset, worldZ);
        float hLeft = GetGridHeightAt(worldX - sampleOffset, worldZ);
        float hFront = GetGridHeightAt(worldX, worldZ + sampleOffset);
        float hBack = GetGridHeightAt(worldX, worldZ - sampleOffset);

        // Gradiente negado: aponta para baixo na rampa (direção da queda) sem o sinal negativo a força empurraria o planeta para cima da depressão
        float gx = -(hRight - hLeft) / (2f * sampleOffset);
        float gz = -(hFront - hBack) / (2f * sampleOffset);

        return new Vector3(gx, 0f, gz);
    }
}
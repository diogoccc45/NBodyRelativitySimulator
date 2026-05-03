using UnityEngine;

[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class ProceduralNebula : MonoBehaviour
{
    [Header("Configurações de Visual")]
    public Material nebulaMaterial;
    public int cloudCount = 80;
    public float radius = 1200f;

    [Header("Tamanho das Nuvens")]
    [Tooltip("Tamanho mínimo — nuvens pequenas e brilhantes em primeiro plano")]
    public float cloudSizeMin = 60f;
    [Tooltip("Tamanho máximo — nuvens grandes e difusas ao fundo")]
    public float cloudSizeMax = 280f;
    [Tooltip("Expoente da distribuição — valores > 1 favorecem nuvens pequenas (mais contraste)")]
    public float sizeDistributionPower = 2.2f;

    private Vector3[] cloudOffsets;    // offsets RELATIVOS à câmara — nunca mudam após Start()
    private float[]   cloudColorIndices; // índice de cor por quad (0-1) → shader _ColorIndex
    private float[]   cloudSizes;      // tamanho individual por quad
    private Mesh mesh;
    private Vector3[] vertices;

    void Start()
    {
        cloudOffsets      = new Vector3[cloudCount];
        cloudColorIndices = new float[cloudCount];
        cloudSizes        = new float[cloudCount];

        for (int i = 0; i < cloudCount; i++)
        {
            cloudOffsets[i]      = Random.insideUnitSphere * radius;
            cloudColorIndices[i] = Random.value;

            // Distribuição de tamanhos com power — a maioria das nuvens é pequena,
            // poucas são grandes. Dá profundidade e evita que tudo pareça igual.
            // t = 0 → cloudSizeMin (pequenas), t = 1 → cloudSizeMax (grandes)
            float t = Mathf.Pow(Random.value, sizeDistributionPower);
            cloudSizes[i] = Mathf.Lerp(cloudSizeMin, cloudSizeMax, t);
        }

        mesh = new Mesh();
        mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;

        vertices      = new Vector3[cloudCount * 4];
        Vector2[] uv  = new Vector2[cloudCount * 4];
        // UV2: x = índice de cor, y = tamanho normalizado (0-1) para o shader poder variar brilho por tamanho
        Vector2[] uv2 = new Vector2[cloudCount * 4];
        int[] tris    = new int[cloudCount * 6];

        for (int i = 0; i < cloudCount; i++)
        {
            int vIdx = i * 4;
            uv[vIdx + 0] = new Vector2(0, 0);
            uv[vIdx + 1] = new Vector2(1, 0);
            uv[vIdx + 2] = new Vector2(1, 1);
            uv[vIdx + 3] = new Vector2(0, 1);

            float ci        = cloudColorIndices[i];
            // y = tamanho normalizado — nuvens grandes ficam mais difusas (menos brilho no shader)
            float sizeNorm  = Mathf.InverseLerp(cloudSizeMin, cloudSizeMax, cloudSizes[i]);
            uv2[vIdx + 0] = new Vector2(ci, sizeNorm);
            uv2[vIdx + 1] = new Vector2(ci, sizeNorm);
            uv2[vIdx + 2] = new Vector2(ci, sizeNorm);
            uv2[vIdx + 3] = new Vector2(ci, sizeNorm);

            int tIdx = i * 6;
            tris[tIdx + 0] = vIdx + 0;
            tris[tIdx + 1] = vIdx + 2;
            tris[tIdx + 2] = vIdx + 1;
            tris[tIdx + 3] = vIdx + 0;
            tris[tIdx + 4] = vIdx + 3;
            tris[tIdx + 5] = vIdx + 2;
        }

        mesh.vertices  = vertices;
        mesh.uv        = uv;
        mesh.uv2       = uv2;
        mesh.triangles = tris;
        mesh.bounds    = new Bounds(Vector3.zero, Vector3.one * 100000f);

        GetComponent<MeshFilter>().mesh = mesh;

        MeshRenderer rend = GetComponent<MeshRenderer>();
        rend.material            = nebulaMaterial;
        rend.shadowCastingMode   = UnityEngine.Rendering.ShadowCastingMode.Off;
        rend.receiveShadows      = false;
    }

    void LateUpdate()
    {
        if (Camera.main == null || mesh == null) return;

        // Segue a câmara — os quads ficam sempre centrados nela
        transform.position = Camera.main.transform.position;
        transform.rotation = Quaternion.identity;

        Vector3 camRight = Camera.main.transform.right;
        Vector3 camUp    = Camera.main.transform.up;

        for (int i = 0; i < cloudCount; i++)
        {
            Vector3 c = cloudOffsets[i];
            float   s = cloudSizes[i] * 0.5f; // tamanho individual por quad

            int vIdx = i * 4;
            vertices[vIdx + 0] = c + (-camRight - camUp) * s;
            vertices[vIdx + 1] = c + ( camRight - camUp) * s;
            vertices[vIdx + 2] = c + ( camRight + camUp) * s;
            vertices[vIdx + 3] = c + (-camRight + camUp) * s;
        }

        mesh.vertices = vertices;
    }
}
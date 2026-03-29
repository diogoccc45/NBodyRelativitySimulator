using UnityEngine;

[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class ProceduralNebula : MonoBehaviour
{
    [Header("Configurações de Visual")]
    public Material nebulaMaterial;
    public int cloudCount = 150;
    public float radius = 1200f;
    public float cloudSize = 400f;

    private Vector3[] cloudCenters;
    private Mesh mesh;
    private Vector3[] vertices;

    void Start()
    {
        // Gera as posições aleatórias uma vez
        cloudCenters = new Vector3[cloudCount];
        for (int i = 0; i < cloudCount; i++)
            cloudCenters[i] = Random.insideUnitSphere * radius;

        // Cria a mesh vazia com o tamanho certo
        mesh = new Mesh();
        mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;

        vertices      = new Vector3[cloudCount * 4];
        Vector2[] uv  = new Vector2[cloudCount * 4];
        int[] tris    = new int[cloudCount * 6];

        for (int i = 0; i < cloudCount; i++)
        {
            int vIdx = i * 4;
            uv[vIdx + 0] = new Vector2(0, 0);
            uv[vIdx + 1] = new Vector2(1, 0);
            uv[vIdx + 2] = new Vector2(1, 1);
            uv[vIdx + 3] = new Vector2(0, 1);

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

        // Segue a câmara
        transform.position = Camera.main.transform.position;
        transform.rotation = Quaternion.identity;

        // Recalcula os vértices de cada quad para ficarem virados para a câmara (billboard)
        // A câmara está na origem do objeto (transform.position = Camera.main.transform.position)
        // por isso o vetor "para a câmara" é simplesmente -cloudCenter normalizado
        Vector3 camRight = Camera.main.transform.right;
        Vector3 camUp    = Camera.main.transform.up;
        float   s        = cloudSize * 0.5f;

        for (int i = 0; i < cloudCount; i++)
        {
            Vector3 c = cloudCenters[i];

            int vIdx = i * 4;
            vertices[vIdx + 0] = c + (-camRight - camUp) * s;
            vertices[vIdx + 1] = c + ( camRight - camUp) * s;
            vertices[vIdx + 2] = c + ( camRight + camUp) * s;
            vertices[vIdx + 3] = c + (-camRight + camUp) * s;
        }

        mesh.vertices = vertices;
    }
}
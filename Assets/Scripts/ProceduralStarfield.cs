using UnityEngine;
using System.Collections.Generic;

public class ProceduralStarfield : MonoBehaviour
{
    public Mesh starMesh;
    public Material starMaterial;

    [Header("Densidade e Visibilidade")]
    public int starCount = 20000;
    public float fieldRadius = 5000f;
    public float starSizeBase = 2f;

    private List<Matrix4x4[]> starBatches = new List<Matrix4x4[]>();
    private bool initialized = false;

    void Start()
    {
        transform.position = Vector3.zero;
        InitializeStars();
    }

    void InitializeStars()
    {
        starBatches.Clear();
        int remainingStars = starCount;

        while (remainingStars > 0)
        {
            int currentBatchSize = Mathf.Min(remainingStars, 1023);
            Matrix4x4[] batch = new Matrix4x4[currentBatchSize];

            for (int i = 0; i < currentBatchSize; i++)
            {
                // Criar uma esfera de estrelas à volta do utilizador
                Vector3 pos = Random.onUnitSphere * Random.Range(500f, fieldRadius);
                
                // Variar o tamanho: algumas mini, outras maiores
                float s = Random.Range(starSizeBase, starSizeBase * 3f);
                
                batch[i] = Matrix4x4.TRS(pos, Quaternion.identity, new Vector3(s, s, s));
            }
            starBatches.Add(batch);
            remainingStars -= currentBatchSize;
        }
        initialized = true;
    }

    void Update()
    {
        if (!initialized || starMaterial == null || starMesh == null) return;

        foreach (var batch in starBatches)
        {
            Graphics.DrawMeshInstanced(starMesh, 0, starMaterial, batch, batch.Length);
        }
    }
}
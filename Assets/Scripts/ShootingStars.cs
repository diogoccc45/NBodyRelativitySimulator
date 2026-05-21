using UnityEngine;
using UnityEngine.UI;
using System.Collections;

// ShootingStars v3 — trail sempre atrás da cabeça
[RequireComponent(typeof(RectTransform))]
public class ShootingStars : MonoBehaviour
{
    [Header("Spawn")]
    public float minInterval = 3.5f;
    public float maxInterval = 8.0f;
    public int   maxActive   = 3;

    [Header("Aparência")]
    [Range(60f,  400f)] public float minLength = 80f;
    [Range(100f, 600f)] public float maxLength = 200f;
    [Range(1f,   4f)]   public float minWidth  = 1.0f;
    [Range(2f,   6f)]   public float maxWidth  = 2.5f;

    [Header("Velocidade")]
    public float minSpeed = 500f;
    public float maxSpeed = 900f;

    [Header("Cor")]
    public Color headColor = new Color(1.00f, 1.00f, 1.00f, 1.00f);
    public Color tailColor = new Color(0.50f, 0.70f, 1.00f, 0.00f);

    float nextSpawn;
    int   activeCount;

    void Start()   => ScheduleNext();
    void Update()
    {
        if (Time.time >= nextSpawn && activeCount < maxActive)
        {
            StartCoroutine(SpawnStar());
            ScheduleNext();
        }
    }
    void ScheduleNext() => nextSpawn = Time.time + Random.Range(minInterval, maxInterval);

    IEnumerator SpawnStar()
    {
        activeCount++;

        const float W = 1920f, H = 1080f;

        // dir: vetor de movimento (direita-baixo, normalizado)
        float angleDeg = Random.Range(20f, 45f);
        float angleRad = angleDeg * Mathf.Deg2Rad;
        Vector2 dir = new Vector2(Mathf.Cos(angleRad), -Mathf.Sin(angleRad));

        // Ponto de spawn: a CABEÇA começa fora do ecrã
        Vector2 spawnHead;
        if (Random.value < 0.65f)
            spawnHead = new Vector2(Random.Range(0.05f, 0.85f) * W, H + Random.Range(20f, 80f));
        else
            spawnHead = new Vector2(-Random.Range(20f, 80f), Random.Range(0.5f, 1.0f) * H);

        float speed  = Random.Range(minSpeed, maxSpeed);
        float length = Random.Range(minLength, maxLength);
        float width  = Random.Range(minWidth,  maxWidth);

        // Textura: índice 0 = cauda (transparente/azul), índice w-1 = cabeça (branco)
        Texture2D trailTex = MakeGradientTex(64);

        var go = new GameObject("ShootingStar");
        go.transform.SetParent(transform, false);

        var ri     = go.AddComponent<RawImage>();
        ri.texture = trailTex;
        ri.color   = Color.white;

        var srt = go.GetComponent<RectTransform>();
        srt.anchorMin = srt.anchorMax = new Vector2(0.5f, 0.5f);
        srt.sizeDelta = new Vector2(length, width);

        // pivot = (1, 0.5): o ponto de referência é a CABEÇA (extremo direito do rect)
        // O rect cresce para -X local, que deve apontar para -dir (= atrás da cabeça)
        // Logo +X local deve apontar para dir
        // Rotação = ângulo de dir em relação a +X
        srt.pivot = new Vector2(1f, 0.5f);
        float rot = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
        srt.localEulerAngles = new Vector3(0f, 0f, rot);

        float maxDist    = Mathf.Sqrt(W * W + H * H) + length + 120f;
        float fadeInDur  = 0.10f;
        float fadeOutDur = 0.25f;
        float elapsed    = 0f;
        float dist       = 0f;

        while (dist < maxDist)
        {
            elapsed += Time.deltaTime;
            dist    += speed * Time.deltaTime;

            // anchoredPosition é a posição do pivot = cabeça
            Vector2 headPos = spawnHead + dir * dist;
            srt.anchoredPosition = new Vector2(headPos.x - W * 0.5f, headPos.y - H * 0.5f);

            float alpha = elapsed < fadeInDur ? elapsed / fadeInDur : 1f;
            float remaining = maxDist - dist;
            if (remaining < speed * fadeOutDur)
                alpha *= remaining / (speed * fadeOutDur);

            ri.color = new Color(1f, 1f, 1f, Mathf.Clamp01(alpha));
            yield return null;
        }

        Destroy(trailTex);
        Destroy(go);
        activeCount--;
    }

    // índice 0 = cauda (transparente), índice w-1 = cabeça (branco)
    Texture2D MakeGradientTex(int w)
    {
        var tex = new Texture2D(w, 1, TextureFormat.RGBA32, false);
        tex.wrapMode   = TextureWrapMode.Clamp;
        tex.filterMode = FilterMode.Bilinear;
        var pixels = new Color[w];
        for (int i = 0; i < w; i++)
        {
            float t = (float)i / (w - 1);   // 0=cauda, 1=cabeça
            pixels[i] = Color.Lerp(tailColor, headColor, t * t);
        }
        tex.SetPixels(pixels);
        tex.Apply();
        return tex;
    }
}
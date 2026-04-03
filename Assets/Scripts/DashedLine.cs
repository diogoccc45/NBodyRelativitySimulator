using UnityEngine;
using System.Collections.Generic;
public class DashedLine : MonoBehaviour
{
    [Header("Configurações")]
    public float dashLength = 0.8f;
    public float gapLength = 0.4f;
    public float lineWidth = 0.08f;
    public Color lineColor = new Color(0f, 1f, 0.8f, 0.8f);

    private List<LineRenderer> segments = new List<LineRenderer>();
    private Material lineMat;

    void Awake()
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Unlit")
                     ?? Shader.Find("Unlit/Color");
        lineMat = new Material(shader);
        lineMat.color = lineColor;
        lineMat.SetColor("_BaseColor", lineColor);
    }

    public void SetPoints(Vector3 from, Vector3 to)
    {
        float total = Vector3.Distance(from, to);
        Vector3 dir = (to - from).normalized;
        float segLen = dashLength + gapLength;
        int count = Mathf.Max(1, Mathf.FloorToInt(total / segLen));

        // Garante segmentos suficientes
        while (segments.Count < count)
            segments.Add(CreateSegment());

        // Esconde os excedentes
        for (int i = count; i < segments.Count; i++)
            segments[i].enabled = false;

        // Posiciona cada traço
        for (int i = 0; i < count; i++)
        {
            float startD = i * segLen;
            float endD = Mathf.Min(startD + dashLength, total);
            Vector3 a = from + dir * startD;
            Vector3 b = from + dir * endD;

            segments[i].enabled = true;
            segments[i].positionCount = 2;
            segments[i].SetPosition(0, a);
            segments[i].SetPosition(1, b);
        }
    }

    public void Hide()
    {
        foreach (var s in segments)
            if (s != null) s.enabled = false;
    }

    LineRenderer CreateSegment()
    {
        GameObject go = new GameObject("Dash");
        go.transform.SetParent(transform);

        LineRenderer lr = go.AddComponent<LineRenderer>();
        lr.material = lineMat;
        lr.startWidth = lineWidth;
        lr.endWidth = lineWidth;
        lr.useWorldSpace = true;
        lr.positionCount = 2;
        return lr;
    }
}
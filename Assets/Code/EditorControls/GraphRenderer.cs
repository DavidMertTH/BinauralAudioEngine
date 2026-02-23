using UnityEngine;
using UnityEngine.UI;

public class GraphRenderer : Graphic
{
    [Header("Shader")]
    public Shader graphShader;

    [Header("Visual Settings")]
    public Color lineColor     = Color.green;
    public float lineThickness = 0.005f;

    private Material      graphMat;
    private ComputeBuffer dataBuffer;
    private int           currentCapacity = 0;

    protected override void Awake()
    {
        base.Awake();
        if (graphShader != null)
        {
            graphMat = new Material(graphShader);
            material = graphMat;
        }
    }

    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();
        var r = GetPixelAdjustedRect();

        var v = new UIVertex();
        v.color = Color.white;

        v.position = new Vector3(r.xMin, r.yMin); v.uv0 = new Vector2(0, 0); vh.AddVert(v);
        v.position = new Vector3(r.xMax, r.yMin); v.uv0 = new Vector2(1, 0); vh.AddVert(v);
        v.position = new Vector3(r.xMax, r.yMax); v.uv0 = new Vector2(1, 1); vh.AddVert(v);
        v.position = new Vector3(r.xMin, r.yMax); v.uv0 = new Vector2(0, 1); vh.AddVert(v);

        vh.AddTriangle(0, 1, 2);
        vh.AddTriangle(2, 3, 0);
    }

    public void SetData(float[] data)
    {
        if (graphMat == null) return;

        int count = data.Length;

        if (dataBuffer == null || currentCapacity != count)
        {
            dataBuffer?.Release();
            dataBuffer      = new ComputeBuffer(count, sizeof(float));
            currentCapacity = count;
        }

        dataBuffer.SetData(data);

        // Symmetrisch um 0 – damit 0 immer in der Mitte ist
        float absMax = 0f;
        foreach (var v in data)
            if (Mathf.Abs(v) > absMax) absMax = Mathf.Abs(v);
        if (Mathf.Approximately(absMax, 0f)) absMax = 1f;

        graphMat.SetBuffer("_DataBuffer",    dataBuffer);
        graphMat.SetInt   ("_DataCount",     count);
        graphMat.SetFloat ("_MinValue",      -absMax);
        graphMat.SetFloat ("_MaxValue",       absMax);
        graphMat.SetColor ("_Color",          lineColor);
        graphMat.SetFloat ("_LineThickness",  lineThickness);
    }

    protected override void OnDestroy()
    {
        base.OnDestroy();
        dataBuffer?.Release();
        dataBuffer = null;
    }
}
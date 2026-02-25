using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class GraphRenderer : Graphic
{
    [Header("Shader")] public Shader graphShader;

    [Header("Visual Settings")] public Color lineColor = Color.green;
    public float lineThickness = 0.005f;
    public TMP_Text maxTime;
    public TMP_Text minTime;
    
    private Material graphMat;
    private ComputeBuffer dataBuffer;
    private int currentCapacity = 0;
   


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

        v.position = new Vector3(r.xMin, r.yMin);
        v.uv0 = new Vector2(0, 0);
        vh.AddVert(v);
        v.position = new Vector3(r.xMax, r.yMin);
        v.uv0 = new Vector2(1, 0);
        vh.AddVert(v);
        v.position = new Vector3(r.xMax, r.yMax);
        v.uv0 = new Vector2(1, 1);
        vh.AddVert(v);
        v.position = new Vector3(r.xMin, r.yMax);
        v.uv0 = new Vector2(0, 1);
        vh.AddVert(v);

        vh.AddTriangle(0, 1, 2);
        vh.AddTriangle(2, 3, 0);
    }

    public void SetData(float[] data)
    {
        if (data == null || data.Length == 0) return;
        if (graphMat == null) return;

        int lastNonZero = 0;
        for (int i = data.Length - 1; i >= 0; i--)
        {
            if (Mathf.Abs(data[i]) > 1e-10f)
            {
                lastNonZero = i;
                break;
            }
        }

        int displayCount = Mathf.Min((int)(lastNonZero * 1.5f), data.Length);
        displayCount = Mathf.Max(displayCount, 1);

        if (dataBuffer == null || currentCapacity != displayCount)
        {
            dataBuffer?.Release();
            dataBuffer = new ComputeBuffer(displayCount, sizeof(float));
            currentCapacity = displayCount;
        }

        var slice = new float[displayCount];
        Array.Copy(data, slice, displayCount);
        dataBuffer.SetData(slice);
        
        float absMax = 0f;
        for (int i = 0; i < displayCount; i++)
            if (Mathf.Abs(data[i]) > absMax)
                absMax = Mathf.Abs(data[i]);
        if (Mathf.Approximately(absMax, 0f)) absMax = 1f;
        maxTime.text = (displayCount/48000f) + " s";
        minTime.text = "0 s";
        graphMat.SetBuffer("_DataBuffer", dataBuffer);
        graphMat.SetInt("_DataCount", displayCount);
        graphMat.SetFloat("_MinValue", -absMax);
        graphMat.SetFloat("_MaxValue", absMax);
        graphMat.SetColor("_Color", lineColor);
        graphMat.SetFloat("_LineThickness", lineThickness);
    }

    protected override void OnDestroy()
    {
        base.OnDestroy();
        dataBuffer?.Release();
        dataBuffer = null;
    }
}
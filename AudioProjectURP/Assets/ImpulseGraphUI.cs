using System;
using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

[RequireComponent(typeof(LineRenderer))]
public class ImpulseGraphUI : MonoBehaviour
{
    public RectTransform graphArea; // UI-Bereich (z. B. ein Panel im Canvas)
    public float[] impulseResponse;
    public float lineWidth = 2f;
    public Color lineColor = Color.green;
    private LineRenderer lineRenderer;

    void Start()
    {
        SetupLineRenderer();
    }

    void SetupLineRenderer()
    {
        lineRenderer = GetComponent<LineRenderer>();
        lineRenderer.positionCount = 2001;
        lineRenderer.widthMultiplier = lineWidth;
        lineRenderer.startColor = lineColor;
        lineRenderer.endColor = lineColor;
        lineRenderer.useWorldSpace = false;
    }

    private void Update()
    {
        if(graphArea == null)return;
        DrawGraph();
    }

    void DrawGraph()
    {
        float width = graphArea.rect.width;
        float height = graphArea.rect.height;

        float maxAbs = 1f;
        

        for (int i = 0; i < impulseResponse.Length; i++)
        {
            float x = (i / (float)(impulseResponse.Length - 1)) * width;
            float y = (impulseResponse[i] / maxAbs) * height / 2f;

            // Position relativ zum Graph-Bereich
            Vector3 pos = new Vector3(x, y + height / 2f, 0);
            lineRenderer.SetPosition(i, pos);
        }

        // Optional: Position LineRenderer passend
        lineRenderer.transform.SetParent(graphArea, false);
        lineRenderer.transform.localPosition = Vector3.zero;
    }
}
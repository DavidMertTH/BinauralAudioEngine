
using System;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.UI.Extensions;
using System.Collections.Generic;

[RequireComponent(typeof(UILineRenderer))]
public class ImpulseGraphUI : MonoBehaviour
{
    public RectTransform graphArea; // UI Panel area
    public float[] impulseResponse;
    public float lineWidth = 4f;
    public Color lineColor = Color.green;
    [Range(0.1f, 8f)]
    public float heightScale = 3f; // Scale vertical height

    private UILineRenderer uiLineRenderer;

    void Start()
    {
        uiLineRenderer = GetComponent<UILineRenderer>();
        uiLineRenderer.LineThickness = lineWidth;
        uiLineRenderer.color = lineColor;
        uiLineRenderer.transform.SetParent(graphArea, false);
        uiLineRenderer.transform.localPosition = Vector3.zero;
    }

    void Update()
    {
        if (graphArea == null || impulseResponse == null || impulseResponse.Length == 0)
            return;

        DrawGraph();
    }

    void DrawGraph()
    {
        float width = graphArea.rect.width;
        float height = graphArea.rect.height;

        float maxAbs = 1f; // You can set this dynamically if needed

        var points = new List<Vector2>();

        for (int i = 0; i < impulseResponse.Length; i++)
        {
            float x = (i / (float)(impulseResponse.Length - 1)) * width;
            float y = (impulseResponse[i] / maxAbs) * height / 2f * heightScale;

            float baselineY = -height / 4f; // shifts it downward from center
            points.Add(new Vector2(x - width / 2f, y + baselineY));
        }

        uiLineRenderer.Points = points.ToArray();
        uiLineRenderer.SetAllDirty(); // Force redraw
    }
}


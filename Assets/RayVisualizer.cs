using System;
using Code.Renderer;
using Code.Simulation;
using UnityEngine;

public class RayVisualizer : MonoBehaviour
{
    public AudioSourceObject sourceObject;
    public LineRenderer lineRenderer;
    public GameObject targetObject;

    private void Update()
    {
        if (sourceObject == null) return;
        if (sourceObject.audioPaths == null) return;
        AudioPath path = sourceObject.audioPaths[0];
        lineRenderer.positionCount = 3;
        lineRenderer.SetPosition(0, sourceObject.transform.position);
        lineRenderer.SetPosition(1, path.Positions[1]);

        lineRenderer.SetPosition(2, targetObject.transform.position);
    }
}
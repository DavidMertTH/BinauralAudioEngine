using System;
using System.Collections.Generic;
using Code.Renderer;
using Code.Simulation;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Serialization;

public class RaysVisualizer : MonoBehaviour
{
    public AudioSourceObject sourceObject;
    public GameObject rayPrefab;
    public int numRays;
    private List<LineRenderer> _lineRenderer = new List<LineRenderer>();

    private void Update()
    {
        if(sourceObject.audioPaths == null)return;
        EnterNewRays(sourceObject.audioPaths,sourceObject.gameObject);
        for (int i = 0; i < numRays; i++)
        {
            _lineRenderer[i].material.color = sourceObject.color;
        }
    }

    private void Start()
    {
        for (int i = 0; i < numRays; i++)
        {
            GameObject go = Instantiate(rayPrefab);
            _lineRenderer.Add(go.GetComponent<LineRenderer>());
            _lineRenderer[i].transform.parent = transform;
            _lineRenderer[i].enabled = false;
            _lineRenderer[i].material.color = sourceObject.color;
        }
    }

    public void CleanPreviousRays()
    {
        for (int i = 0; i < numRays; i++)
        {
            _lineRenderer[i].positionCount = 0;
            _lineRenderer[i].enabled = false;
        }
    }

    public void EnterNewRays(List<AudioPath> paths, GameObject source)
    {
        CleanPreviousRays();
        int count = Mathf.Min(numRays, _lineRenderer.Count);

        for (int i = 0; i < count; i++)
        {
            if (i >= paths.Count || paths[i].Positions == null || paths[i].Positions.Length == 0 ||  !paths[i].IsValid)
            {
                _lineRenderer[i].enabled = false;
                continue;
            }

            var lr = _lineRenderer[i];
            lr.enabled = true;
            lr.useWorldSpace = true;

            int numCp = paths[i].Positions.Length;
            lr.positionCount = numCp;

            for (int j = 0; j < numCp; j++)
                lr.SetPosition(j, paths[i].Positions[j]);
        }
    }
}
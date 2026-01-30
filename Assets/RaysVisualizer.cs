using System;
using System.Collections.Generic;
using Code.Simulation;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Serialization;

public class RaysVisualizer : MonoBehaviour
{
    public GameObject rayPrefab;
    public int numRays;
    private List<LineRenderer> _lineRenderer = new List<LineRenderer>();

    private void Awake()
    {
        for (int i = 0; i < numRays; i++)
        {
            GameObject go = Instantiate(rayPrefab);
            _lineRenderer.Add(go.GetComponent<LineRenderer>());
            _lineRenderer[i].transform.parent = transform;
            _lineRenderer[i].enabled = false;
        }
    }

    public void CleanPreviousRays()
    {
        for (int i = 0; i < numRays; i++)
        {
            _lineRenderer[i].enabled = false;
        }
    }

    public void EnterNewRays(List<AudioPath> paths, GameObject source)
    {
        CleanPreviousRays();
        int count = Mathf.Min(numRays, _lineRenderer.Count);

        for (int i = 0; i < count; i++)
        {
            if (i >= paths.Count || paths[i].Positions == null || paths[i].Positions.Length == 0)
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
using System;
using System.Collections.Generic;
using DefaultNamespace;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

public class Listener3D : MonoBehaviour
{
    public AudioSource audioSource;
    private NativeArray<RaycastCommand> _raycastCommands;
    private NativeArray<RaycastCommand> _secondRaycastCommands;

    private NativeArray<RaycastHit> _raycastResults;
    private NativeArray<RaycastHit> _secondRaycastResults;

    private NativeArray<float3> _surroundingPoints;

    private FillRaycastCommandsParallel _fillJob;
    private FillSecondRaycastCommandsParallel _secondfillJob;

    private JobHandle _firstfillHandle;
    private JobHandle _secondfillHandle;

    private List<Vector3> _firstOrderHits;
    private List<Vector3> _secondOrderHits;


    void Start()
    {
        _firstOrderHits = new List<Vector3>();
        _secondOrderHits = new List<Vector3>();

        _surroundingPoints = Helper.GetFibonacciPoints(500);
    }

    

    private bool IsDirect()
    {
        RaycastHit hit;
        Vector3 direction = audioSource.transform.position - transform.position;
        if (Physics.Raycast(transform.position, direction, out hit, Mathf.Infinity))
        {
            Debug.DrawRay(transform.position, direction * hit.distance, Color.blue);
            return true;
        }

        return false;
    }

    private void Update()
    {
        if (AudioEnvironment.instance == null) return;

        //AudioEnvironment.instance.resolution = 100;
        //AudioEnvironment.instance.GetSurfacesAroundTarget(audioSource.transform.position,gameObject.transform.position);
    }

    private void FindPaths()
    {
        _firstfillHandle = StartFillJobFirstRay(this.gameObject.transform.position);
        _secondfillHandle = StartFillJobSecondRay(this.gameObject.transform.position);

        EvaluateHits();
    }

    private JobHandle StartFillJobFirstRay(Vector3 origin)
    {
        _raycastCommands = new NativeArray<RaycastCommand>(_surroundingPoints.Length, Allocator.TempJob);

        _fillJob = new FillRaycastCommandsParallel()
        {
            RaycastCommands = _raycastCommands,
            Origin = (float3)origin,
            SurroundingPoints = _surroundingPoints
        };

        return _fillJob.Schedule(_surroundingPoints.Length, 8);
    }

    private JobHandle StartFillJobSecondRay(Vector3 origin)
    {
        _firstfillHandle.Complete();

        _raycastCommands = _fillJob.RaycastCommands;
        _raycastResults = new NativeArray<RaycastHit>(_raycastCommands.Length, Allocator.TempJob);

        JobHandle handle = RaycastCommand.ScheduleBatch(_raycastCommands, _raycastResults, 1, 1);
        handle.Complete();

        _secondRaycastCommands = new NativeArray<RaycastCommand>(_surroundingPoints.Length, Allocator.TempJob);

        _secondfillJob = new FillSecondRaycastCommandsParallel()
        {
            RaycastCommands = _secondRaycastCommands,
            Target = audioSource.transform.position,
            PreviousHits = _raycastResults
        };

        return _secondfillJob.Schedule(_surroundingPoints.Length, 8);
    }


    [BurstCompile]
    private struct FillRaycastCommandsParallel : IJobParallelFor
    {
        public NativeArray<RaycastCommand> RaycastCommands;
        [ReadOnly] public NativeArray<float3> SurroundingPoints;
        [DeallocateOnJobCompletion] [ReadOnly] public float3 Origin;

        public void Execute(int index)
        {
            RaycastCommands[index] = new RaycastCommand(Origin, SurroundingPoints[index], QueryParameters.Default);
        }
    }

    [BurstCompile]
    private struct FillSecondRaycastCommandsParallel : IJobParallelFor
    {
        public NativeArray<RaycastCommand> RaycastCommands;

        [ReadOnly] public NativeArray<RaycastHit> PreviousHits;
        [DeallocateOnJobCompletion] [ReadOnly] public Vector3 Target;

        public void Execute(int index)
        {
            if (PreviousHits[index].distance == 0) return;

            Vector3 startPoint = PreviousHits[index].point + PreviousHits[index].normal * 0.01f;
            RaycastCommands[index] = new RaycastCommand(startPoint, (Target - PreviousHits[index].point).normalized,
                QueryParameters.Default);
        }
    }

    private void OnDrawGizmosSelected()
    {
        if (_firstOrderHits == null) return;

        for (int i = 0; i < _firstOrderHits.Count; i++)
        {
            if (_firstOrderHits[i].x > 0.001f || _firstOrderHits[i].y > 0.001f || _firstOrderHits[i].z > 0.001f)
            {
                Gizmos.color = Color.yellow;
                Gizmos.DrawSphere(_firstOrderHits[i], 0.01f);
                Gizmos.DrawRay(transform.position, _firstOrderHits[i] - transform.position);
            }

            if (_secondOrderHits[i].x > 0.001f || _secondOrderHits[i].y > 0.001f || _secondOrderHits[i].z > 0.001f)
            {
                Gizmos.color = Color.red;
                Gizmos.DrawSphere(_secondOrderHits[i], 0.01f);
                Gizmos.DrawRay(_secondOrderHits[i], _firstOrderHits[i] - _secondOrderHits[i]);
            }
        }
    }

    private void EvaluateHits()
    {
        _secondfillHandle.Complete();

        _secondRaycastCommands = _secondfillJob.RaycastCommands;
        _secondRaycastResults = new NativeArray<RaycastHit>(_raycastCommands.Length, Allocator.TempJob);
        JobHandle handle = RaycastCommand.ScheduleBatch(_secondRaycastCommands, _secondRaycastResults, 1, 1);

        handle.Complete();

        _firstOrderHits.Clear();
        _secondOrderHits.Clear();

        foreach (RaycastHit hit in _raycastResults)
        {
            _firstOrderHits.Add(hit.point);
        }

        foreach (RaycastHit hit in _secondRaycastResults)
        {
            if (hit.collider != null && hit.collider.gameObject.layer == 6)
            {
                _secondOrderHits.Add(hit.point);
            }
            else
            {
                _secondOrderHits.Add(new Vector3());
            }
        }

        _raycastResults.Dispose();
        _secondRaycastCommands.Dispose();
        _raycastCommands.Dispose();
    }

    [BurstCompile]
    public struct FirstOrderReflectionsEvaluation : IJobParallelFor
    {
        [DeallocateOnJobCompletion] [ReadOnly] public NativeArray<RaycastHit> Hits;

        public NativeArray<float> PathDistances;
        public NativeArray<float> Angle;
        public NativeArray<float> Strength;


        [BurstCompile]
        public void Execute(int index)
        {
            for (int i = 0; i < Hits.Length; ++i)
            {
                if (Hits[i].distance == 0)
                {
                    Strength[i] = 0f;
                    continue;
                }
            }
        }
    }
}
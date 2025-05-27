using System;
using System.Diagnostics;
using DefaultNamespace;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.Serialization;

public class AudioEnvironment : MonoBehaviour
{
    public static AudioEnvironment Instance;
    public int resolution;

    private NativeArray<float3> _surroundingPoints;


    private void Start()
    {
        Application.targetFrameRate = 60;

        if (Instance != null) Destroy(this);
        else Instance = this;
        _surroundingPoints = Helper.GetFibonacciPoints(resolution);
    }
    
    private void OnDestroy()
    {
        if(_surroundingPoints.IsCreated)_surroundingPoints.Dispose();
    }

    public NativeArray<RaycastHit> GetSurfacesAroundPosition(Vector3 posToCheck)
    {
        NativeArray<RaycastCommand> sourceRaycastCommands =
            new NativeArray<RaycastCommand>(_surroundingPoints.Length, Allocator.TempJob);

        FillRaycastCommandsParallel sourceFillJob = new FillRaycastCommandsParallel()
        {
            RaycastCommands = sourceRaycastCommands,
            Origin = (float3)posToCheck,
            SurroundingPoints = _surroundingPoints
        };

        JobHandle fillHandle = sourceFillJob.Schedule(_surroundingPoints.Length, 8);

        NativeArray<RaycastHit> raycastResults =
            new NativeArray<RaycastHit>(sourceRaycastCommands.Length, Allocator.TempJob);

        fillHandle.Complete();

        JobHandle sourceRayHandle = RaycastCommand.ScheduleBatch(sourceRaycastCommands, raycastResults, 1, 1);


        sourceRayHandle.Complete();

        NativeArray<RaycastHit> uniqueHits = RemoveDoubles(raycastResults);

        sourceRaycastCommands.Dispose();
        raycastResults.Dispose();
        
        return uniqueHits;
    }
    public NativeArray<RaycastCommand> GetRaycastsAroundPosition(Vector3 posToCheck)
    {
        NativeArray<RaycastCommand> sourceRaycastCommands =
            new NativeArray<RaycastCommand>(_surroundingPoints.Length, Allocator.TempJob);

        FillRaycastCommandsParallel sourceFillJob = new FillRaycastCommandsParallel()
        {
            RaycastCommands = sourceRaycastCommands,
            Origin = (float3)posToCheck,
            SurroundingPoints = _surroundingPoints
        };

        JobHandle fillHandle = sourceFillJob.Schedule(_surroundingPoints.Length, 8);
        fillHandle.Complete();
        
        return sourceRaycastCommands;
    }
    public NativeArray<RaycastHit> RemoveDoubles(NativeArray<RaycastHit> rawHits)
    {
        var uniqueHits = new NativeList<RaycastHit>(rawHits.Length, Allocator.TempJob);

        var job = new RemoveDuplicateHitsJob
        {
            RawHits = rawHits,
            UniqueHits = uniqueHits,
            NormalTolerance = 0.999f,
            PlaneEpsilon = 0.01f
        };

        job.Run();

        var result = new NativeArray<RaycastHit>(uniqueHits.Length, Allocator.TempJob);
        for (int i = 0; i < uniqueHits.Length; i++)
            result[i] = uniqueHits[i];

        uniqueHits.Dispose();
        return result;
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
    public struct RemoveDuplicateHitsJob : IJob
    {
        [ReadOnly] public NativeArray<RaycastHit> RawHits;
        public NativeList<RaycastHit> UniqueHits;

        public float NormalTolerance;
        public float PlaneEpsilon;

        public void Execute()
        {
            for (int i = 0; i < RawHits.Length; i++)
            {
                var hitA = RawHits[i];
                bool isDuplicate = false;

                for (int j = 0; j < UniqueHits.Length; j++)
                {
                    var hitB = UniqueHits[j];


                    var normA = math.normalize(hitA.normal);
                    var normB = math.normalize(hitB.normal);

                    if (math.dot(normA, normB) > NormalTolerance)
                    {
                        float distToPlane = math.abs(math.dot(hitA.point - hitB.point, normB));
                        if (distToPlane < PlaneEpsilon)
                        {
                            isDuplicate = true;
                            break;
                        }
                    }
                }

                if (!isDuplicate)
                {
                    UniqueHits.Add(hitA);
                }
            }
        }
    }
}
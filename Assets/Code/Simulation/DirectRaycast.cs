using Code.Renderer;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

namespace Code.Simulation
{
    [BurstCompile]
    public struct CreateDirectRaycastCommands : IJobParallelFor
    {
        public NativeArray<RaycastCommand> Commands;

        [ReadOnly] public NativeArray<float3> Sources;
        [ReadOnly] public float3 Listener;

        public void Execute(int index)
        {
            Commands[index] = new RaycastCommand(Sources[index], Listener, QueryParameters.Default);
        }
    }

    [BurstCompile]
    public struct EvaluateDirectRaycasts : IJobParallelFor
    {
        public NativeArray<RaycastHit> Hits;
        
        public void Execute(int index)
        {
            throw new System.NotImplementedException();
        }
    }
}
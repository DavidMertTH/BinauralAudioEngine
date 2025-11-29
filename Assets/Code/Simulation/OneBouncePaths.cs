using System;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

namespace Code.Simulation
{
    /// <summary>
    /// Simulate audio paths with one bounce using the image source method
    /// </summary>
    public class OneBouncePaths
    {
        private NativeArray<RaycastCommand> _commands;

        public JobHandle GetOneBouncePaths(float3 listener, NativeArray<float3> sources,
            NativeArray<RaycastHit> surroundingHits, int hitsPerListenerOrSource,
            JobHandle hitsReadyHandle, NativeArray<AudioPath> result)
        {
            var numRaycasts = result.Length * 2; // One bounce means two ray casts
            _commands = Helper.ReallocateIfNeeded(_commands, numRaycasts, Allocator.Persistent);
            throw new NotImplementedException();
        }

        [BurstCompile]
        private struct CreateOneBounceRaycastCommands : IJobFor
        {
            public NativeArray<RaycastCommand> Commands;

            [ReadOnly] public NativeArray<float3> Sources;
            [ReadOnly] public float3 Listener;
            [ReadOnly] public NativeArray<RaycastHit> SurroundingHits;
            [ReadOnly] public int SurroundingHitsStride;

            public void Execute(int index)
            {
                throw new NotImplementedException();
                //Commands[index] = new RaycastCommand(Sources[index], direction, QueryParameters.Default, distance);
            }
        }
    }
}
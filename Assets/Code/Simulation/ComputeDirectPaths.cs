using System;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

namespace Code.Simulation
{
    /// <summary>
    /// Direct rays from audio sources to listener
    /// </summary>
    public class ComputeDirectPaths : IDisposable
    {
        public JobHandle Schedule(float3 listener, NativeArray<float3>.ReadOnly sources, LayerMask rayMask,
            NativeArray<AudioPath> paths)
        {
            // This runs synchronously on the main thread because RaycastCommand.ScheduleBatch did not work correctly,
            // and I could not figure out why.
            for (var i = 0; i < sources.Length; i++)
            {
                var dist = math.distance(listener, sources[i]);
                var hit = Physics.Raycast(sources[i], listener - sources[i], dist, rayMask);
                if (hit)
                {
                    paths[i] = new AudioPath() { IsValid = false };
                    continue;
                }
                
                var path = new AudioPath()
                {
                    IsValid = true,
                    DistanceToImage = dist,
                    Energy = 1f,
                    ImagePosition = sources[i],
                    SourceIndex = i,
                };
                path.Positions.Clear();
                path.Positions.Add(listener);
                path.Positions.Add(sources[i]);
                paths[i] = path;
            }
            
            return new JobHandle();
        }

        public void Dispose()
        {
        }
    }
}
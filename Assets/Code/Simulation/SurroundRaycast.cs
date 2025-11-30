using System;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using static Unity.Mathematics.math;

namespace Code.Simulation
{
    /// <summary>
    /// Create raycasts in a regular pattern in all directions around a set of points to explore the surrounding environment.
    /// </summary>
    public class SurroundRaycast : IDisposable
    {
        private NativeArray<float3> _directions;
        private NativeArray<RaycastCommand> _commands;
        private NativeArray<RaycastHit> _hits;
        private readonly CoplanarHits _coplanarHits = new();
        
        public JobHandle CastRaysAroundOrigins(NativeArray<float3>.ReadOnly origins, AudioPathArrayLayout layout,
            out NativeArray<RaycastHit> hits, out int hitsStride, out NativeArray<bool> isHitCoplanar)
        {
            var numRaysPerOrigin = GetNumRaysPerOrigin(layout);
            hitsStride = numRaysPerOrigin;
            var getDirectionsHandle = GetDirections(numRaysPerOrigin, out var directions);
            var getCommandsHandle = GetCommands(getDirectionsHandle, origins, directions, out var commands);
            var raycastHandle = GetHits(getCommandsHandle, commands, out hits);
            var checkCoplanarHandle = _coplanarHits.FindCoplanarHits(raycastHandle, hits, hitsStride, out isHitCoplanar);
            return checkCoplanarHandle;
        }

        private int GetNumRaysPerOrigin(AudioPathArrayLayout layout)
        {
            var numSources = layout.DirectCount;
            // The number or ray casts around each listener and each source needed for the requested
            // number of paths with two bounces
            var a = sqrt(layout.TwoBouncesCount / (float)numSources);
            // The number of ray casts needed for the requested number of paths with one bounce
            var b = layout.OneBounceCount / (float)(2 * numSources);
            // The number used is the maximum of the two
            return (int)ceil(max(a, b));
        }

        private JobHandle GetDirections(int num, out NativeArray<float3> directions)
        {
            _directions =
                Helper.ReallocateIfNeeded(_directions, num, Allocator.Persistent, out var canUseCache);
            directions = _directions;
            if (!canUseCache)
                return new JobHandle(); // Return an already completed handle
            var job = new GetRaycastDirectionsJob
            {
                Directions = _directions,
                Phi = Mathf.PI * (3f - Mathf.Sqrt(5f))
            };
            return job.Schedule(num, 32);
        }

        [BurstCompile]
        private struct GetRaycastDirectionsJob : IJobParallelFor
        {
            public NativeArray<float3> Directions;
            public float Phi;

            public void Execute(int index)
            {
                var y = 1f - index / (float)(Directions.Length - 1) * 2f;
                var r = Mathf.Sqrt(1f - y * y);
                var theta = Phi * index;
                var x = Mathf.Cos(theta) * r;
                var z = Mathf.Sin(theta) * r;
                Directions[index] = new float3(x, y, z);
            }
        }

        private JobHandle GetCommands(JobHandle getDirectionsHandle, NativeArray<float3>.ReadOnly origins,
            NativeArray<float3> directions,
            out NativeArray<RaycastCommand> commands)
        {
            var raycastCount = origins.Length * directions.Length;
            _commands = Helper.ReallocateIfNeeded(_commands, raycastCount, Allocator.Persistent);
            commands = _commands;
            var job = new GetCommandsJob
            {
                RaycastCommands = commands,
                Origins = origins,
                Directions = directions,
            };
            return job.Schedule(raycastCount, 32, getDirectionsHandle);
        }

        [BurstCompile]
        private struct GetCommandsJob : IJobParallelFor
        {
            public NativeArray<RaycastCommand> RaycastCommands;
            [ReadOnly] public NativeArray<float3>.ReadOnly Origins;
            [ReadOnly] public NativeArray<float3> Directions;

            public void Execute(int index)
            {
                // Create one raycast command for every direction and every origin
                RaycastCommands[index] = new RaycastCommand(Origins[index / Origins.Length],
                    Directions[index % Directions.Length],
                    QueryParameters.Default);
            }
        }

        private JobHandle GetHits(JobHandle getCommandsHandle, NativeArray<RaycastCommand> commands,
            out NativeArray<RaycastHit> hits)
        {
            _hits = Helper.ReallocateIfNeeded(_hits, commands.Length, Allocator.Persistent);
            hits = _hits;
            return RaycastCommand.ScheduleBatch(commands, hits, 1, getCommandsHandle);
        }


        public void Dispose()
        {
            _directions.Dispose();
            _commands.Dispose();
            _hits.Dispose();
            _coplanarHits.Dispose();
        }
    }
}
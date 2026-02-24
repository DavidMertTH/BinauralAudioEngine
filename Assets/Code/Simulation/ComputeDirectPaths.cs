using System;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using static Unity.Mathematics.math;
using UnityEngine;
using Unity.Collections.LowLevel.Unsafe;
using UnityEngine.UI.Extensions;

namespace Code.Simulation
{
    /// <summary>
    /// Direct rays from audio sources to listener
    /// </summary>
    public class ComputeDirectPaths : IDisposable
    {
        private NativeArray<RaycastCommand> _commands;
        private NativeArray<RaycastHit> _hits;

        public JobHandle Schedule(float3 listener, NativeArray<float3>.ReadOnly sources, LayerMask rayMask,
            NativeArray<AudioPath> paths)
        {
            // Create raycast commands
            _commands = Helper.ReallocateIfNeeded(_commands, sources.Length, Allocator.Persistent);
            var createCommands = new CreateDirectRaycastCommands()
            {
                Commands = _commands,
                Sources = sources,
                Listener = listener,
                RayMask = rayMask
            };
            var createCommandsHandle = createCommands.ScheduleParallel(_commands.Length, 32, default);

            // Perform raycasts
            _hits = Helper.ReallocateIfNeeded(_hits, sources.Length, Allocator.Persistent);
            var execRaycastsHandle = RaycastCommand.ScheduleBatch(_commands, _hits, 1, dependsOn: createCommandsHandle);

            // Evaluate raycasts -> audio paths
            var evaluateRaycastsJob = new EvaluateRaycasts()
            {
                Paths = paths,
                Hits = _hits,
                Commands = _commands,
                Listener = listener,
                Sources = sources,
            };
            var evaluateRaycastsHandle =
                evaluateRaycastsJob.ScheduleParallel(sources.Length, 32, dependency: execRaycastsHandle);
            return evaluateRaycastsHandle;
        }

        [BurstCompile]
        private struct CreateDirectRaycastCommands : IJobFor
        {
            public NativeArray<RaycastCommand> Commands;

            [Unity.Collections.ReadOnly] public NativeArray<float3>.ReadOnly Sources;
            [Unity.Collections.ReadOnly] public float3 Listener;
            [Unity.Collections.ReadOnly] public LayerMask RayMask;

            public void Execute(int index)
            {
                var direction = Sources[index] - Listener;
                var distance = length(direction);
                Commands[index] = new RaycastCommand(Listener, direction, new QueryParameters(RayMask, hitBackfaces: true), distance);
            }
        }

        /// <summary>
        /// Create audio paths from the raycast results.
        /// </summary>
        [BurstCompile]
        private struct EvaluateRaycasts : IJobFor
        {
            // Disable safety checks to write to the <c>Paths</c> sub-array
            // while other jobs are writing to different sub-arrays of the same array.
            [NativeDisableContainerSafetyRestriction]
            public NativeArray<AudioPath> Paths;

            [Unity.Collections.ReadOnly] public NativeArray<RaycastHit> Hits;
            [Unity.Collections.ReadOnly] public NativeArray<RaycastCommand> Commands;
            [Unity.Collections.ReadOnly] public NativeArray<float3>.ReadOnly Sources;
            [Unity.Collections.ReadOnly] public float3 Listener;

            public void Execute(int index)
            {
                var path = Paths[index];
                path.IsValid = !Helper.DidHit(Hits[index]);
                if (path.IsValid)
                {
                    path.SourceIndex = index;
                    path.Reflections = 0;
                    path.DistanceToImage = Commands[index].distance;
                    path.Energy = 1f;
                    path.Positions.Clear();
                    path.Positions.Add(Listener);
                    path.Positions.Add(Sources[index]);
                }

                Paths[index] = path;
            }
        }

        public void Dispose()
        {
            _commands.Dispose();
            _hits.Dispose();
        }
    }
}
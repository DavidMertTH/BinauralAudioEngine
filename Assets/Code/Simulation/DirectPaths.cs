using System;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using static Unity.Mathematics.math;
using UnityEngine;
using Unity.Collections.LowLevel.Unsafe;

namespace Code.Simulation
{
    /// <summary>
    /// Direct rays from audio sources to listener
    /// </summary>
    public class DirectPaths : IDisposable
    {
        private NativeArray<RaycastCommand> _commands;
        private NativeArray<RaycastHit> _hits;

        public JobHandle GetDirectPaths(float3 listener, NativeArray<float3> sources, NativeArray<AudioPath> paths)
        {
            // Create raycast commands
            _commands = Helper.ReallocateIfNeeded(_commands, sources.Length, Allocator.Persistent);
            var createCommands = new CreateDirectRaycastCommands()
            {
                Commands = _commands,
                Sources = sources,
                Listener = listener,
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

            [ReadOnly] public NativeArray<float3> Sources;
            [ReadOnly] public float3 Listener;

            public void Execute(int index)
            {
                var direction = Sources[index] - Listener;
                var distance = length(direction);
                Commands[index] = new RaycastCommand(Listener, direction, QueryParameters.Default, distance);
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

            [ReadOnly] public NativeArray<RaycastHit> Hits;
            [ReadOnly] public NativeArray<RaycastCommand> Commands;
            [ReadOnly] public NativeArray<float3> Sources;
            [ReadOnly] public float3 Listener;

            public void Execute(int index)
            {
                var didHit = Hits[index].distance != 0f;
                var path = Paths[index];
                path.Reflections = 0;
                path.DistanceToImage = Commands[index].distance;
                path.IsValid = !didHit; // nothing blocks the ray
                path.Energy = 1f;
                path.Positions.Add(Listener);
                path.Positions.Add(Sources[index]);
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
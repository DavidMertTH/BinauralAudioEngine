using System;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using static Unity.Mathematics.math;
using UnityEngine;

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

            // Evaluate raycasts -> audio rays
            var evaluateRaycastsJob = new EvaluateRaycasts()
            {
                Paths = paths,
                Hits = _hits,
                Commands = _commands,
            };
            var evaluateRaycastsHandle = evaluateRaycastsJob.ScheduleParallel(paths.Length, 32, dependency: execRaycastsHandle);
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
                var direction = Listener - Sources[index];
                var distance = length(direction);
                Commands[index] = new RaycastCommand(Sources[index], direction, QueryParameters.Default, distance);
            }
        }

        [BurstCompile]
        private struct EvaluateRaycasts : IJobFor
        {
            public NativeArray<AudioPath> Paths;
            [ReadOnly] public NativeArray<RaycastHit> Hits;
            [ReadOnly] public NativeArray<RaycastCommand> Commands;

            public void Execute(int index)
            {
                var didHit = Hits[index].distance != 0f;
                var ray = Paths[index];
                ray.Reflections = 0;
                ray.DistanceToImage = Commands[index].distance;
                ray.IsValid = didHit;
                ray.Energy = 1f;
                Paths[index] = ray;
            }
        }

        public void Dispose()
        {
            _commands.Dispose();
            _hits.Dispose();
        }
    }
}
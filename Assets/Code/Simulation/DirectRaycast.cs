using System;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

namespace Code.Simulation
{
    public class DirectRaycast : IDisposable
    {
        private NativeArray<RaycastCommand> _commands;
        private NativeArray<RaycastHit> _hits;

        public JobHandle GetDirectRays(float3 listener, NativeArray<float3> sources, out NativeArray<RaycastHit> hits)
        {
            _commands = Helper.ReallocateIfNeeded(_commands, sources.Length, Allocator.Persistent);
            var createCommands = new CreateDirectRaycastCommands()
            {
                Commands = _commands,
                Sources = sources,
                Listener = listener,
            };
            var createCommandsHandle = createCommands.Schedule(_commands.Length, 32);
            _hits = Helper.ReallocateIfNeeded(_hits, sources.Length, Allocator.Persistent);
            hits = _hits;
            return RaycastCommand.ScheduleBatch(_commands, _hits, 1, dependsOn: createCommandsHandle);
        }

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

        public void Dispose()
        {
            _commands.Dispose();
            _hits.Dispose();
        }
    }
}
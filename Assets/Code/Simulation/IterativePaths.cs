using System;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

namespace Code.Simulation
{
    public class IterativePaths : IDisposable
    {
        private NativeArray<float3> _bounces;
        private NativeArray<float> _totalDistances;
        private NativeArray<RaycastCommand> _bounceCommands;
        private NativeArray<RaycastHit> _bounceHits;
        private NativeArray<RaycastCommand> _visibilityCommands;
        private NativeArray<RaycastHit> _visibilityHits;

        public JobHandle GetIterativePaths(float3 listener, NativeArray<float3>.ReadOnly sources,
            NativeArray<RaycastCommand>.ReadOnly rayCommandsAroundListener,
            NativeArray<RaycastHit>.ReadOnly hitsAroundListener, int numBounces,
            JobHandle hitsReadyHandle, NativeArray<AudioPath> result)
        {
            _bounces = Helper.ReallocateIfNeeded(_bounces, numBounces * hitsAroundListener.Length,
                Allocator.Persistent);
            _bounceHits = Helper.ReallocateIfNeeded(_bounceHits, hitsAroundListener.Length, Allocator.Persistent);
            var hitsCopyHandle = new Copy<RaycastHit>
            {
                Source = hitsAroundListener,
                Destination = _bounceHits,
            }.Schedule(hitsReadyHandle);
            _bounceCommands =
                Helper.ReallocateIfNeeded(_bounceCommands, hitsAroundListener.Length, Allocator.Persistent);
            var commandsCopyHandle = new Copy<RaycastCommand>
            {
                Source = rayCommandsAroundListener,
                Destination = _bounceCommands,
            }.Schedule(hitsReadyHandle);
            _totalDistances = Helper.ReallocateIfNeeded(_totalDistances, result.Length, Allocator.Persistent);

            var prevLoopHandle = JobHandle.CombineDependencies(hitsCopyHandle, commandsCopyHandle);
            for (var i = 0; i < numBounces; i++)
            {
                var reflectHandle = new Bounce
                {
                    Bounces = _bounces,
                    TotalDistances = _totalDistances,
                    ReflectCommands = _bounceCommands,
                    PreviousRayHits = _bounceHits,
                    BounceIndex = i,
                    MaxNumBounces = numBounces
                }.ScheduleParallel(hitsAroundListener.Length, 32, prevLoopHandle);

                var raycastHandle =
                    RaycastCommand.ScheduleBatch(_bounceCommands, _bounceHits, minCommandsPerJob: 1, reflectHandle);
                prevLoopHandle = raycastHandle;
            }

            _visibilityCommands = Helper.ReallocateIfNeeded(_visibilityCommands, result.Length, Allocator.Persistent);
            var visibilityCheckCommandHandle = new CheckVisibility
            {
                VisibilityCommands = _visibilityCommands,
                Bounces = _bounces,
                Sources = sources,
            }.ScheduleParallel(result.Length, 32, prevLoopHandle);

            _visibilityHits = Helper.ReallocateIfNeeded(_visibilityHits, result.Length, Allocator.Persistent);
            var visibilityCheckHandle = RaycastCommand.ScheduleBatch(
                _visibilityCommands, _visibilityHits, 1, visibilityCheckCommandHandle);

            var createPathsHandle = new CreatePaths
            {
                Paths = result,
                Bounces = _bounces,

                TotalDistances = _totalDistances,
                Sources = sources,
                Listener = listener,
                MaxNumBounces = numBounces,
                VisibilityHits = _visibilityHits,
            }.ScheduleParallel(result.Length, 32, visibilityCheckHandle);

            return createPathsHandle;
        }

        private struct Copy<T> : IJob where T : struct
        {
            [ReadOnly] public NativeArray<T>.ReadOnly Source;
            public NativeArray<T> Destination;

            public void Execute()
            {
                Source.CopyTo(Destination);
            }
        }

        private struct Bounce : IJobFor
        {
            [NativeDisableParallelForRestriction]
            public NativeSlice<float3> Bounces;
            public NativeArray<float> TotalDistances;
            public NativeArray<RaycastCommand> ReflectCommands;
            [ReadOnly] public NativeArray<RaycastHit> PreviousRayHits;
            [ReadOnly] public int BounceIndex;
            [ReadOnly] public int MaxNumBounces;

            public void Execute(int index)
            {
                if (!Helper.DidHit(PreviousRayHits[index]))
                {
                    ReflectCommands[index] = new RaycastCommand { queryParameters = new QueryParameters(layerMask: 0) };
                    Bounces[index] = float3.zero;
                    return;
                }

                Bounces[BounceIndex * MaxNumBounces + index] = PreviousRayHits[index].point;
                TotalDistances[index] = TotalDistances[index] + PreviousRayHits[index].distance;

                var reflectDir =
                    math.reflect(ReflectCommands[index].direction, PreviousRayHits[index].normal);
                var origin = PreviousRayHits[index].point;
                ReflectCommands[index] = new RaycastCommand(
                    origin, reflectDir, QueryParameters.Default);
            }
        }

        private struct CheckVisibility : IJobFor
        {
            public NativeArray<RaycastCommand> VisibilityCommands;

            [ReadOnly] public NativeArray<float3> Bounces;
            [ReadOnly] public NativeArray<float3>.ReadOnly Sources;

            public void Execute(int index)
            {
                var source = Sources[index / Bounces.Length];
                var bounce = Bounces[index % Bounces.Length];
                var direction = source - bounce;
                var distance = math.distance(source, bounce);
                VisibilityCommands[index] =
                    new RaycastCommand(bounce, direction, QueryParameters.Default, distance);
            }
        }

        private struct CreatePaths : IJobFor
        {
            [NativeDisableContainerSafetyRestriction]
            public NativeArray<AudioPath> Paths;

            [ReadOnly] public NativeArray<float3> Bounces;
            [ReadOnly] public NativeArray<float> TotalDistances;
            [ReadOnly] public NativeArray<float3>.ReadOnly Sources;
            [ReadOnly] public float3 Listener;
            [ReadOnly] public NativeArray<RaycastHit> VisibilityHits;
            [ReadOnly] public int MaxNumBounces;

            public void Execute(int index)
            {
                var bounceIndex = index % Bounces.Length;
                var lastBounce = Bounces[bounceIndex];
                var numBounces = bounceIndex % MaxNumBounces + 1;

                if (lastBounce is { x: 0, y: 0, z: 0 } || Helper.DidHit(VisibilityHits[index]) || numBounces < 3)
                {
                    Paths[index] = new AudioPath { IsValid = false };
                    return;
                }

                var sourceIndex = index / Bounces.Length;

                var path = new AudioPath
                {
                    IsValid = true,
                    ImagePosition = lastBounce,
                    DistanceToImage = TotalDistances[index],
                    Energy = math.pow(0.9f, numBounces),
                    Reflections = numBounces,
                    SourceIndex = sourceIndex,
                };

                path.Positions.Add(Listener);
                for (var i = bounceIndex - numBounces + 1; i <= bounceIndex; i++)
                    path.Positions.Add(Bounces[i]);
                var source = Sources[sourceIndex];
                path.Positions.Add(source);
                Paths[index] = path;
            }
        }

        public void Dispose()
        {
            _bounces.Dispose();
            _totalDistances.Dispose();
            _bounceCommands.Dispose();
            _bounceHits.Dispose();
            _visibilityCommands.Dispose();
            _visibilityHits.Dispose();
        }
    }
}
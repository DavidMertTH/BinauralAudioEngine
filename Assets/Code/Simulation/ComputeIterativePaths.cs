using System;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

namespace Code.Simulation
{
    public class ComputeIterativePaths : IDisposable
    {
        private NativeArray<RaycastCommand> _bounceCommands;
        private NativeArray<RaycastHit> _bounceHits;
        private NativeArray<RaycastCommand> _visibilityCommands;
        private NativeArray<RaycastHit> _visibilityHits;
        private NativeArray<float> _totalDistances;

        public JobHandle Schedule(float3 listener, NativeArray<float3>.ReadOnly sources,
            NativeArray<RaycastCommand>.ReadOnly rayCommandsAroundListener,
            NativeArray<RaycastHit>.ReadOnly hitsAroundListener, int numBouncesPerPath, LayerMask rayMask,
            float bounceAttenuation, JobHandle hitsReadyHandle, NativeArray<AudioPath> result, float maxReflectAngleDeg)
        {
            var numBouncesTotal = numBouncesPerPath * hitsAroundListener.Length;
            var bouncesStride = hitsAroundListener.Length;
            _totalDistances = Helper.ReallocateIfNeeded(_totalDistances, numBouncesTotal, Allocator.Persistent);
            _bounceHits = Helper.ReallocateIfNeeded(_bounceHits, numBouncesTotal, Allocator.Persistent);
            var hitsCopyHandle = new Copy<RaycastHit>
            {
                Source = hitsAroundListener,
                Destination = _bounceHits.GetSubArray(0, hitsAroundListener.Length)
            }.Schedule(hitsReadyHandle);
            _bounceCommands =
                Helper.ReallocateIfNeeded(_bounceCommands, hitsAroundListener.Length, Allocator.Persistent);
            var commandsCopyHandle = new Copy<RaycastCommand>
            {
                Source = rayCommandsAroundListener,
                Destination = _bounceCommands,
            }.Schedule(hitsReadyHandle);

            var distanceCopyHandle = new CopyDistance
            {
                CopyTo = _totalDistances.GetSubArray(0, hitsAroundListener.Length),
                Hits = hitsAroundListener,
            }.ScheduleParallel(hitsAroundListener.Length, 32, hitsReadyHandle);

            var prevLoopHandle = JobHandle.CombineDependencies(hitsCopyHandle, commandsCopyHandle, distanceCopyHandle);
            for (var i = 1; i < numBouncesPerPath; i++)
            {
                var reflectHandle = new Bounce
                {
                    ReflectCommands = _bounceCommands,
                    PreviousRayHits = _bounceHits.GetSubArray((i - 1) * bouncesStride, bouncesStride),
                    RayMask = rayMask,
                }.ScheduleParallel(hitsAroundListener.Length, 32, prevLoopHandle);

                var raycastHandle =
                    RaycastCommand.ScheduleBatch(_bounceCommands,
                        _bounceHits.GetSubArray(i * bouncesStride, bouncesStride), minCommandsPerJob: 1,
                        reflectHandle);

                var addDistanceHandle = new AddDistance
                {
                    AddTo = _totalDistances.GetSubArray((i - 1) * bouncesStride, bouncesStride * 2),
                    Hits = _bounceHits.GetSubArray(i * bouncesStride, bouncesStride).AsReadOnly(),
                }.ScheduleParallel(bouncesStride, 32, raycastHandle);
                
                prevLoopHandle = addDistanceHandle;
            }

            _visibilityCommands =
                Helper.ReallocateIfNeeded(_visibilityCommands, result.Length, Allocator.Persistent);
            var visibilityCheckCommandHandle = new CheckVisibility
            {
                VisibilityCommands = _visibilityCommands,
                BounceHits = _bounceHits,
                Sources = sources,
                RayMask = rayMask
            }.ScheduleParallel(result.Length, 32, prevLoopHandle);

            _visibilityHits = Helper.ReallocateIfNeeded(_visibilityHits, result.Length, Allocator.Persistent);
            var visibilityCheckHandle = RaycastCommand.ScheduleBatch(
                _visibilityCommands, _visibilityHits, 1, visibilityCheckCommandHandle);

            var createPathsHandle = new CreatePaths
            {
                Paths = result,
                BounceHits = _bounceHits,
                TotalDistances = _totalDistances,
                BouncesStride = bouncesStride,
                Sources = sources,
                Listener = listener,
                VisibilityHits = _visibilityHits,
                MinReflectDot = math.cos(math.radians(maxReflectAngleDeg)),
                BounceAttenuation = bounceAttenuation,
            }.ScheduleParallel(result.Length, 32, visibilityCheckHandle);

            return createPathsHandle;
        }

        // TODO: Burst?
        private struct Copy<T> : IJob where T : struct
        {
            [ReadOnly] public NativeArray<T>.ReadOnly Source;
            public NativeArray<T> Destination;

            public void Execute()
            {
                Source.CopyTo(Destination);
            }
        }

        private struct CopyDistance : IJobFor
        {
            public NativeArray<float> CopyTo;
            [ReadOnly] public NativeArray<RaycastHit>.ReadOnly Hits;

            public void Execute(int index)
            {
                CopyTo[index] = Hits[index].distance;
            }
        }

        private struct AddDistance : IJobFor
        {
            [NativeDisableParallelForRestriction]
            public NativeArray<float> AddTo;
            [ReadOnly] public NativeArray<RaycastHit>.ReadOnly Hits;

            public void Execute(int index)
            {
                AddTo[index + Hits.Length] = AddTo[index] + Hits[index].distance;
            }
        }

        private struct Bounce : IJobFor
        {
            public NativeArray<RaycastCommand> ReflectCommands;

            [NativeDisableParallelForRestriction] [ReadOnly]
            public NativeArray<RaycastHit> PreviousRayHits;

            [ReadOnly] public LayerMask RayMask;

            public void Execute(int index)
            {
                if (!Helper.DidHit(PreviousRayHits[index]))
                {
                    ReflectCommands[index] = new RaycastCommand { queryParameters = new QueryParameters(layerMask: 0) };
                    return;
                }

                var reflectDir =
                    math.reflect(ReflectCommands[index].direction, PreviousRayHits[index].normal);
                var origin = PreviousRayHits[index].point;
                ReflectCommands[index] = new RaycastCommand(
                    origin, reflectDir, new QueryParameters(RayMask));
            }
        }

        private struct CheckVisibility : IJobFor
        {
            public NativeArray<RaycastCommand> VisibilityCommands;

            [ReadOnly] public NativeArray<RaycastHit> BounceHits;
            [ReadOnly] public NativeArray<float3>.ReadOnly Sources;
            [ReadOnly] public LayerMask RayMask;

            public void Execute(int index)
            {
                var source = Sources[index / BounceHits.Length];
                var bounce = (float3)BounceHits[index % BounceHits.Length].point;
                var direction = bounce - source;
                VisibilityCommands[index] =
                    new RaycastCommand(source, direction, new QueryParameters(RayMask));
            }
        }

        private struct CreatePaths : IJobFor
        {
            [NativeDisableContainerSafetyRestriction]
            public NativeArray<AudioPath> Paths;

            [ReadOnly] public NativeArray<RaycastHit> BounceHits;
            [ReadOnly] public NativeArray<float> TotalDistances;
            [ReadOnly] public int BouncesStride;
            [ReadOnly] public NativeArray<float3>.ReadOnly Sources;
            [ReadOnly] public float3 Listener;
            [ReadOnly] public NativeArray<RaycastHit> VisibilityHits;
            [ReadOnly] public float MinReflectDot;
            [ReadOnly] public float BounceAttenuation;

            public void Execute(int index)
            {
                var bounceIndex = index % BounceHits.Length;
                var lastBounce = (float3)BounceHits[bounceIndex].point;
                var numBounces = bounceIndex / BouncesStride + 1;

                if (lastBounce is { x: 0, y: 0, z: 0 } || !Helper.DidRayHitPoint(VisibilityHits[index], lastBounce) ||
                    numBounces < 3)
                {
                    Paths[index] = new AudioPath { IsValid = false };
                    return;
                }

                var sourceIndex = index / BounceHits.Length;
                if (!IsLastBounceWithinDeviationLimit(bounceIndex, sourceIndex))
                {
                    Paths[index] = new AudioPath { IsValid = false };
                    return;
                }

                var sourceToLastBounce = Sources[sourceIndex] - lastBounce;
                var distToSource = math.length(sourceToLastBounce);
                var totalDist = TotalDistances[index] + distToSource;

                var path = new AudioPath
                {
                    IsValid = true,
                    ImagePosition = Sources[sourceIndex] + sourceToLastBounce / distToSource * totalDist,
                    DistanceToImage = totalDist,
                    Energy = math.pow(BounceAttenuation, numBounces),
                    Reflections = numBounces,
                    SourceIndex = sourceIndex,
                };

                path.Positions.Add(Listener);
                for (var i = numBounces - 1; i >= 0; i--)
                    path.Positions.Add(BounceHits[bounceIndex - i * BouncesStride].point);
                var source = Sources[sourceIndex];
                path.Positions.Add(source);
                Paths[index] = path;
            }

            private bool IsLastBounceWithinDeviationLimit(int bounceIndex, int sourceIndex)
            {
                var lastBounce = (float3)BounceHits[bounceIndex].point;
                var prevBounce = (float3)BounceHits[bounceIndex - BouncesStride].point;
                var toLastBounceDir = math.normalize(lastBounce - prevBounce);
                var lastBounceNormal = (float3)BounceHits[bounceIndex].normal;
                var perfectReflectionDir = math.reflect(toLastBounceDir, lastBounceNormal);
                var lastBounceToSourceDir = math.normalize(Sources[sourceIndex] - lastBounce);
                var reflectDot = math.dot(perfectReflectionDir, lastBounceToSourceDir);
                return reflectDot > MinReflectDot;
            }
        }

        public void Dispose()
        {
            _totalDistances.Dispose();
            _bounceCommands.Dispose();
            _bounceHits.Dispose();
            _visibilityCommands.Dispose();
            _visibilityHits.Dispose();
        }
    }
}
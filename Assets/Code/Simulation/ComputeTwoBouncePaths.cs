using System;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

namespace Code.Simulation
{
    /// <summary>
    /// Simulate audio paths with two bounces using the image source method to find specular reflections.
    /// </summary>
    public class ComputeTwoBouncePaths : IDisposable
    {
        private NativeArray<RaycastCommand> _visibilityChecks;
        private NativeArray<RaycastHit> _visibilityHits;

        public JobHandle Schedule(float3 listener, NativeArray<float3>.ReadOnly sources,
            NativeArray<RaycastHit>.ReadOnly hitsAroundListener, NativeArray<bool>.ReadOnly isHitAroundListenerCoplanar,
            NativeArray<RaycastHit>.ReadOnly hitsAroundSources, NativeArray<bool>.ReadOnly isHitAroundSourceCoplanar,
            int numHitsPerSource, LayerMask rayMask, JobHandle hitsReadyHandle, NativeArray<AudioPath> result)
        {
            _visibilityChecks = Helper.ReallocateIfNeeded(_visibilityChecks, result.Length * 3, Allocator.Persistent);
            var findPathsHandle = new FindPaths()
            {
                Listener = listener,
                Sources = sources,
                HitsAroundListener = hitsAroundListener,
                HitsAroundSources = hitsAroundSources,
                IsHitAroundListenerCoplanar = isHitAroundListenerCoplanar,
                IsHitAroundSourceCoplanar = isHitAroundSourceCoplanar,
                numHitsPerSource = numHitsPerSource,
                Paths = result,
                VisibilityChecks = _visibilityChecks,
                RayMask = rayMask
            }.ScheduleParallel(result.Length, 32, hitsReadyHandle);
            _visibilityHits =
                Helper.ReallocateIfNeeded(_visibilityHits, _visibilityChecks.Length, Allocator.Persistent);
            var visibilityCheckHandle =
                RaycastCommand.ScheduleBatch(_visibilityChecks, _visibilityHits, 1, findPathsHandle);
            var invalidateObstructedPathsHandle = new InvalidateObstructedPaths()
            {
                Paths = result,
                VisibilityHits = _visibilityHits,
            }.ScheduleParallel(result.Length, 32, visibilityCheckHandle);
            return invalidateObstructedPathsHandle;
        }

        [BurstCompile]
        private struct FindPaths : IJobFor
        {
            /// <summary>
            /// Job will create one path for every pair of hits from <c>HitsAroundListener</c> and
            /// <c>HitsAroundSources</c>
            /// </summary>
            [NativeDisableContainerSafetyRestriction]
            public NativeArray<AudioPath> Paths;

            /// <summary>
            /// Stride 3. Raycast commands to check whether the path is obstructed and therefore invalid.
            /// Three raycasts for every path: Listener -> First reflection point -> Second reflection point -> Source
            /// </summary>
            [NativeDisableParallelForRestriction] public NativeArray<RaycastCommand> VisibilityChecks;

            [ReadOnly] public float3 Listener;
            [ReadOnly] public NativeArray<float3>.ReadOnly Sources;
            [ReadOnly] public NativeArray<RaycastHit>.ReadOnly HitsAroundListener;
            [ReadOnly] public NativeArray<bool>.ReadOnly IsHitAroundListenerCoplanar;
            [ReadOnly] public NativeArray<RaycastHit>.ReadOnly HitsAroundSources;
            [ReadOnly] public NativeArray<bool>.ReadOnly IsHitAroundSourceCoplanar;
            [ReadOnly] public int numHitsPerSource;
            [ReadOnly] public LayerMask RayMask;

            public void Execute(int index)
            {
                var listenerHitIndex = index % HitsAroundListener.Length;
                var sourceHitIndex = index / HitsAroundListener.Length;
                var sourceIndex = sourceHitIndex / numHitsPerSource;
                var listenerHit = HitsAroundListener[listenerHitIndex];
                var sourceHit = HitsAroundSources[sourceHitIndex];
                if (IsHitAroundListenerCoplanar[listenerHitIndex]
                    || IsHitAroundSourceCoplanar[sourceHitIndex]
                    || Helper.CheckCoplanar(listenerHit, sourceHit))
                {
                    Paths[index] = new AudioPath { IsValid = false };
                    return;
                }

                var listenerMirrored = Helper.MirrorPointAcrossPlane(Listener, listenerHit.point, listenerHit.normal);
                var sourceMirrored =
                    Helper.MirrorPointAcrossPlane(Sources[sourceIndex], sourceHit.point, sourceHit.normal);
                if (!Helper.TryIntersectLineSegmentWithPlane(listenerMirrored, sourceMirrored, listenerHit.point,
                        listenerHit.normal, out var firstIntersection)
                    || !Helper.TryIntersectLineSegmentWithPlane(listenerMirrored, sourceMirrored, sourceHit.point,
                        sourceHit.normal, out var secondIntersection))
                {
                    Paths[index] = new AudioPath { IsValid = false };
                    return;
                }

                var listenerToFirstReflectionDistance = math.distance(Listener, firstIntersection);
                var firstToSecondReflectionDistance = math.distance(firstIntersection, secondIntersection);
                var secondReflectionToSourceDistance = math.distance(secondIntersection, Sources[sourceIndex]);
                var totalDistance = listenerToFirstReflectionDistance + firstToSecondReflectionDistance +
                                    secondReflectionToSourceDistance;
                var path = new AudioPath
                {
                    SourceIndex = sourceIndex,
                    DistanceToImage = totalDistance,
                    Energy = 1f, // TODO
                    ImagePosition = firstIntersection,
                    IsValid = true,
                    Reflections = 2,
                };
                path.Positions.Clear();
                path.Positions.Add(Listener);
                path.Positions.Add(firstIntersection);
                path.Positions.Add(secondIntersection);
                path.Positions.Add(Sources[sourceIndex]);
                Paths[index] = path;
                VisibilityChecks[index * 3] = new RaycastCommand(from: Listener,
                    direction: firstIntersection - Listener,
                    new QueryParameters(RayMask));
                var firstToSecondIntersectionNormal =
                    (secondIntersection - firstIntersection) / firstToSecondReflectionDistance;
                VisibilityChecks[index * 3 + 1] = new RaycastCommand(
                    from: firstIntersection - firstToSecondIntersectionNormal * 0.01f,
                    direction: firstToSecondIntersectionNormal, new QueryParameters(RayMask, hitBackfaces: true));
                VisibilityChecks[index * 3 + 2] = new RaycastCommand(from: Sources[sourceIndex],
                    direction: secondIntersection - Sources[sourceIndex], new QueryParameters(RayMask));
            }
        }

        [BurstCompile]
        private struct InvalidateObstructedPaths : IJobFor
        {
            /// <summary>
            /// Contains one path for every pair of hits from <c>HitsAroundListener</c> and <c>HitsAroundSources</c>.
            /// Job invalidates any paths that are obstructed according to <c>VisibilityHits</c>.
            /// </summary>
            public NativeArray<AudioPath> Paths;

            /// <summary>
            /// Stride 3. Raycast results to check whether the path is obstructed and therefore invalid.
            /// Three raycasts for every path: Listener -> First reflection point -> Second reflection point -> Source
            /// </summary>
            [ReadOnly] public NativeArray<RaycastHit> VisibilityHits;

            public void Execute(int index)
            {
                if (!Paths[index].IsValid)
                    return;
                var path = Paths[index];
                var firstBouncePoint = path.Positions[1];
                var secondBouncePoint = path.Positions[2];
                var isPathClear = Helper.DidRayHitPoint(VisibilityHits[index * 3], firstBouncePoint)
                                  && Helper.DidRayHitPoint(VisibilityHits[index * 3 + 1], secondBouncePoint)
                                  && Helper.DidRayHitPoint(VisibilityHits[index * 3 + 2], secondBouncePoint);
                path.IsValid = isPathClear;
                Paths[index] = path;
            }
        }

        public void Dispose()
        {
            _visibilityChecks.Dispose();
            _visibilityHits.Dispose();
        }
    }
}
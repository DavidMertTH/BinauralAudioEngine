using System;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using Unity.Collections.LowLevel.Unsafe;

namespace Code.Simulation
{
    /// <summary>
    /// Simulate audio paths with one bounce using the image source method
    /// </summary>
    public class ComputeOneBouncePaths : IDisposable
    {
        private NativeArray<RaycastCommand> _commands;
        private NativeArray<float3> _reflectionPoints;
        private NativeArray<RaycastHit> _visibilityHits;

        public JobHandle Schedule(float3 listener, NativeArray<float3>.ReadOnly sources,
            NativeArray<RaycastHit>.ReadOnly hitsAroundListener, NativeArray<bool>.ReadOnly isHitAroundListenerCoplanar,
            LayerMask rayMask, float bounceAttenuation, float throughWallAttenuation, int maxWallPenetrations,
            JobHandle hitsReadyHandle, NativeArray<AudioPath> result)
        {
            var numRaycasts = result.Length * 2; // Need to check visibility from listener and from source
            _commands = Helper.ReallocateIfNeeded(_commands, numRaycasts, Allocator.Persistent);
            _reflectionPoints =
                Helper.ReallocateIfNeeded(_reflectionPoints, result.Length, Allocator.Persistent);
            var findReflectionsHandle = new FindReflectionPoints()
            {
                Commands = _commands,
                ReflectionPoints = _reflectionPoints,
                Sources = sources,
                Listener = listener,
                HitsAroundListener = hitsAroundListener,
                IsHitAroundListenerCoplanar = isHitAroundListenerCoplanar,
                RayMask = rayMask
            }.ScheduleParallel(_reflectionPoints.Length, 32, hitsReadyHandle);
            _visibilityHits = Helper.ReallocateIfNeeded(_visibilityHits, numRaycasts * (maxWallPenetrations + 1),
                Allocator.Persistent);
            var doRaycastsHandle = RaycastCommand.ScheduleBatch(
                _commands, _visibilityHits, 1, maxWallPenetrations + 1, findReflectionsHandle);
            var createPathsHandle = new CreatePathsJob()
            {
                Paths = result,
                ReflectionPoints = _reflectionPoints,
                HitsAroundListener = hitsAroundListener,
                IsHitAroundListenerCoplanar = isHitAroundListenerCoplanar,
                VisibilityHits = _visibilityHits,
                SourcePositions = sources,
                ListenerPosition = listener,
                BounceAttenuation = bounceAttenuation,
                MaxWallPenetrations = maxWallPenetrations,
            }.ScheduleParallel(result.Length, 32, doRaycastsHandle);
            return createPathsHandle;
        }

        [BurstCompile]
        private struct FindReflectionPoints : IJobFor
        {
            [NativeDisableContainerSafetyRestriction]
            public NativeArray<RaycastCommand> Commands;

            public NativeArray<float3> ReflectionPoints;

            [ReadOnly] public NativeArray<float3>.ReadOnly Sources;
            [ReadOnly] public float3 Listener;
            [ReadOnly] public NativeArray<RaycastHit>.ReadOnly HitsAroundListener;
            [ReadOnly] public NativeArray<bool>.ReadOnly IsHitAroundListenerCoplanar;
            [ReadOnly] public LayerMask RayMask;

            public void Execute(int index)
            {
                var hitIndex = index % HitsAroundListener.Length;
                // Coplanar surfaces would produce duplicate reflection points
                if (IsHitAroundListenerCoplanar[hitIndex])
                    return;
                var sourceIndex = index / HitsAroundListener.Length;
                ReflectionPoints[index] = FindSpecularReflection(Sources[sourceIndex], Listener,
                    HitsAroundListener[hitIndex].normal, HitsAroundListener[hitIndex].point);
                Commands[index * 2] = new RaycastCommand(
                    from: Listener,
                    direction: ReflectionPoints[index] - Listener,
                    new QueryParameters(RayMask, hitMultipleFaces: true));
                Commands[index * 2 + 1] = new RaycastCommand(
                    from: Sources[sourceIndex],
                    direction: ReflectionPoints[index] - Sources[sourceIndex],
                    new QueryParameters(RayMask, hitMultipleFaces: true));
            }

            private float3 FindSpecularReflection(float3 a, float3 b, float3 planeNormal, float3 planePoint)
            {
                var aToPlaneDist = Helper.DistanceFromPlane(a, planePoint, planeNormal);
                var bToPlaneDist = Helper.DistanceFromPlane(b, planePoint, planeNormal);
                var aProj = a - planeNormal * aToPlaneDist;
                var bProj = b - planeNormal * bToPlaneDist;
                var t = aToPlaneDist / (aToPlaneDist + bToPlaneDist);
                return math.lerp(aProj, bProj, t);
            }
        }

        /// <summary>
        /// Create audio paths based on the raycasts results.
        /// </summary>
        [BurstCompile]
        private struct CreatePathsJob : IJobFor
        {
            [NativeDisableParallelForRestriction]
            public NativeArray<AudioPath> Paths;
            [NativeDisableParallelForRestriction]
            public NativeArray<RaycastHit> VisibilityHits;
            [ReadOnly] public NativeArray<RaycastHit>.ReadOnly HitsAroundListener;
            [ReadOnly] public NativeArray<bool>.ReadOnly IsHitAroundListenerCoplanar;
            [ReadOnly] public NativeArray<float3> ReflectionPoints;
            [ReadOnly] public NativeArray<float3>.ReadOnly SourcePositions;
            [ReadOnly] public float3 ListenerPosition;
            [ReadOnly] public float BounceAttenuation;
            [ReadOnly] public int MaxWallPenetrations;
            [ReadOnly] public float WallPenetrationAttenuation;

            public void Execute(int index)
            {
                var hitsBetweenListenerAndReflection =
                    VisibilityHits.GetSubArray(index * (MaxWallPenetrations + 1) * 2, MaxWallPenetrations + 1);
                var didHitReflectionFromListener = DidRayHitPoint(hitsBetweenListenerAndReflection.AsReadOnly(),
                    ReflectionPoints[index]);

                var hitsBetweenSourceAndReflection = VisibilityHits.GetSubArray(
                    index * (MaxWallPenetrations + 1) * 2 + MaxWallPenetrations + 1, MaxWallPenetrations + 1);
                var didHitReflectionFromSource = DidRayHitPoint(hitsBetweenSourceAndReflection.AsReadOnly(),
                    ReflectionPoints[index]);

                if (!IsHitAroundListenerCoplanar[index % HitsAroundListener.Length] && didHitReflectionFromListener &&
                    didHitReflectionFromSource)
                {
                    var sourceIndex = index / HitsAroundListener.Length;
                    var listenerToReflectionVec = ReflectionPoints[index] - ListenerPosition;
                    var listenerToReflectionDist = math.length(listenerToReflectionVec);
                    var reflectionToSourceDist = math.distance(ReflectionPoints[index], SourcePositions[sourceIndex]);
                    var imgDist = listenerToReflectionDist + reflectionToSourceDist;
                    var listenerToReflectionNormal = listenerToReflectionVec / listenerToReflectionDist;
                    var path = new AudioPath
                    {
                        SourceIndex = sourceIndex,
                        Reflections = 1,
                        ImagePosition = ReflectionPoints[index] + listenerToReflectionNormal * imgDist,
                        DistanceToImage = imgDist,
                        IsValid = true,
                    };
                    path.Positions.Clear();
                    path.Positions.Add(ListenerPosition);
                    AddPenetrationPoints(hitsBetweenListenerAndReflection, ref path.Positions,
                        out var numHitsFomListener);
                    path.Positions.Add(ReflectionPoints[index]);
                    AddPenetrationPoints(hitsBetweenSourceAndReflection, ref path.Positions, out var numHitsFromSource,
                        invertOrder: true);
                    path.Positions.Add(SourcePositions[sourceIndex]);
                    var numHitsAlongPath = numHitsFomListener + numHitsFromSource;
                    path.Energy = BounceAttenuation * math.pow(WallPenetrationAttenuation, numHitsAlongPath);
                    path.NumWallsPenetrated = numHitsAlongPath;
                    Paths[index] = path;
                }
                else
                {
                    Paths[index] = new AudioPath { IsValid = false };
                }
            }

            private static bool DidRayHitPoint(NativeArray<RaycastHit>.ReadOnly hits, float3 point)
            {
                foreach (var t in hits)
                {
                    if (Helper.DidRayHitPoint(t, point))
                    {
                        return true;
                    }
                }

                return false;
            }

            private void AddPenetrationPoints(NativeArray<RaycastHit> hits, ref FixedList512Bytes<float3> points,
                out int numPoints, bool invertOrder = false)
            {
                if (invertOrder)
                    hits.Sort(new Helper.RaycastHitDistanceDescComparer());
                else
                    hits.Sort(new Helper.RaycastHitDistanceAscComparer());

                numPoints = 0;

                foreach (var hit in hits)
                {
                    if (!Helper.DidHit(hit)) continue;
                    points.Add(hit.point);
                    numPoints++;
                }
            }
        }

        public void Dispose()
        {
            _commands.Dispose();
            _reflectionPoints.Dispose();
            _visibilityHits.Dispose();
        }
    }
}
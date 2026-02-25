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
            LayerMask rayMask, JobHandle hitsReadyHandle, NativeArray<AudioPath> result)
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
            _visibilityHits = Helper.ReallocateIfNeeded(_visibilityHits, numRaycasts, Allocator.Persistent);
            var doRaycastsHandle = RaycastCommand.ScheduleBatch(
                _commands, _visibilityHits, 1, findReflectionsHandle);
            var createPathsHandle = new CreatePathsJob()
            {
                Paths = result,
                ReflectionPoints = _reflectionPoints,
                HitsAroundListener = hitsAroundListener,
                IsHitAroundListenerCoplanar = isHitAroundListenerCoplanar,
                VisibilityHits = _visibilityHits,
                SourcePositions = sources,
                ListenerPosition = listener,
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
                    new QueryParameters(RayMask));
                Commands[index * 2 + 1] = new RaycastCommand(
                    from: Sources[sourceIndex],
                    direction: ReflectionPoints[index] - Sources[sourceIndex],
                    new QueryParameters(RayMask));
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
            [NativeDisableContainerSafetyRestriction]
            public NativeArray<AudioPath> Paths;

            [ReadOnly] public NativeArray<RaycastHit> VisibilityHits;
            [ReadOnly] public NativeArray<RaycastHit>.ReadOnly HitsAroundListener;
            [ReadOnly] public NativeArray<bool>.ReadOnly IsHitAroundListenerCoplanar;
            [ReadOnly] public NativeArray<float3> ReflectionPoints;
            [ReadOnly] public NativeArray<float3>.ReadOnly SourcePositions;
            [ReadOnly] public float3 ListenerPosition;
            

            public void Execute(int index)
            {
                var isPathClear =
                    Helper.DidRayHitPoint(VisibilityHits[index * 2], ReflectionPoints[index]) &&
                    Helper.DidRayHitPoint(VisibilityHits[index * 2 + 1], ReflectionPoints[index]);
                if (!IsHitAroundListenerCoplanar[index % HitsAroundListener.Length] && isPathClear)
                {
                    var path = new AudioPath()
                    {
                        SourceIndex = index / HitsAroundListener.Length,
                        Reflections = 1,
                        ImagePosition = ReflectionPoints[index],
                        DistanceToImage = VisibilityHits[index * 2 + 1].distance,
                        IsValid = true,
                        Energy = 0.9f
                    };
                    path.Positions.Clear();
                    path.Positions.Add(ListenerPosition);
                    path.Positions.Add(ReflectionPoints[index]);
                    path.Positions.Add(SourcePositions[index / HitsAroundListener.Length]);
                    Paths[index] = path;
                }
                else
                {
                    Paths[index] = new AudioPath() { IsValid = false };
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
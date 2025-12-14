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
    public class OneBouncePaths : IDisposable
    {
        private NativeArray<RaycastCommand> _commands;
        private NativeArray<float3> _reflectionPoints;
        private NativeArray<RaycastHit> _visibilityHits;

        public JobHandle GetOneBouncePaths(float3 listener, NativeArray<float3> sources,
            NativeArray<RaycastHit> hitsAroundListener, NativeArray<bool> isHitAroundListenerCoplanar,
            JobHandle hitsReadyHandle, NativeArray<AudioPath> result)
        {
            // Every hit around the listener is a surface with a possible reflection point
            var numReflectionPoints = hitsAroundListener.Length;
            var numRaycasts = numReflectionPoints * 2; // Need to check visibility from listener and from source
            _commands = Helper.ReallocateIfNeeded(_commands, numRaycasts, Allocator.Persistent);
            _reflectionPoints =
                Helper.ReallocateIfNeeded(_reflectionPoints, numReflectionPoints, Allocator.Persistent);
            var findReflectionsHandle = new FindReflectionPoints()
            {
                Commands = _commands,
                ReflectionPoints = _reflectionPoints,
                Sources = sources,
                Listener = listener,
                HitsAroundListener = hitsAroundListener,
                IsHitAroundListenerCoplanar = isHitAroundListenerCoplanar
            }.ScheduleParallel(numReflectionPoints, 32, hitsReadyHandle);
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
            }.ScheduleParallel(numReflectionPoints, 32, doRaycastsHandle);
            return createPathsHandle;
        }

        [BurstCompile]
        private struct FindReflectionPoints : IJobFor
        {
            [NativeDisableContainerSafetyRestriction]
            public NativeArray<RaycastCommand> Commands;
            public NativeArray<float3> ReflectionPoints;

            [ReadOnly] public NativeArray<float3> Sources;
            [ReadOnly] public float3 Listener;
            [ReadOnly] public NativeArray<RaycastHit> HitsAroundListener;
            [ReadOnly] public NativeArray<bool> IsHitAroundListenerCoplanar;

            public void Execute(int index)
            {
                var hitIndex = index % HitsAroundListener.Length;
                // Coplanar surfaces would produce duplicate reflection points
                if (IsHitAroundListenerCoplanar[hitIndex])
                    return;
                var sourceIndex = hitIndex / HitsAroundListener.Length;
                ReflectionPoints[index] = FindSpecularReflection(Sources[sourceIndex], Listener,
                    HitsAroundListener[hitIndex].normal, HitsAroundListener[hitIndex].point);
                Commands[index] = new RaycastCommand(
                    from: Listener,
                    direction: ReflectionPoints[index] - Listener,
                    QueryParameters.Default);
                Commands[index + 1] = new RaycastCommand(
                    from: Sources[sourceIndex],
                    direction: ReflectionPoints[index] - Sources[sourceIndex],
                    QueryParameters.Default);
            }

            private float3 FindSpecularReflection(float3 a, float3 b, float3 planeNormal, float3 planePoint)
            {
                var aToPlaneDist = Helper.DistanceFronPlane(a, planeNormal, planePoint);
                var bToPlaneDist = Helper.DistanceFronPlane(b, planeNormal, planePoint);
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
            [ReadOnly] public NativeArray<RaycastHit> HitsAroundListener;
            [ReadOnly] public NativeArray<bool> IsHitAroundListenerCoplanar;
            [ReadOnly] public NativeArray<float3> ReflectionPoints;
            [ReadOnly] public NativeArray<float3> SourcePositions;
            [ReadOnly] public float3 ListenerPosition;

            public void Execute(int index)
            {
                var isReflectionPointIsVisibleFromListener = DidRayHitReflectionPoint(
                    VisibilityHits[index * 2], HitsAroundListener[index].normal, ReflectionPoints[index]);
                var isReflectionPointIsVisibleFromSource = DidRayHitReflectionPoint(
                    VisibilityHits[index * 2 + 1], HitsAroundListener[index].normal, ReflectionPoints[index]);
                if (!IsHitAroundListenerCoplanar[index % HitsAroundListener.Length] && 
                    isReflectionPointIsVisibleFromListener &&
                    isReflectionPointIsVisibleFromSource)
                {
                    Paths[index] = new AudioPath()
                    {
                        Reflections = 1,
                        ImagePosition = ReflectionPoints[index],
                        DistanceToImage = VisibilityHits[index * 2 + 1].distance,
                        IsValid = true,
                        Energy = 1f, // TODO: Calc energy
                    };
                    Paths[index].Positions.Add(ListenerPosition);
                    Paths[index].Positions.Add(ReflectionPoints[index]);
                    Paths[index].Positions.Add(SourcePositions[index / HitsAroundListener.Length]);
                }
                else
                {
                    Paths[index] = new AudioPath() { IsValid = false };
                }
            }

            private bool DidRayHitReflectionPoint(RaycastHit hit, float3 reflectionNormal, float3 reflectionPoint)
            {
                var normalMatches = math.dot(hit.normal, reflectionNormal) > 0.99f;
                var hitPointMatches =
                    math.abs(hit.point.x - reflectionPoint.x) +
                    math.abs(hit.point.y - reflectionPoint.y) +
                    math.abs(hit.point.z - reflectionPoint.z) > 0.01f;
                return normalMatches && hitPointMatches;
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
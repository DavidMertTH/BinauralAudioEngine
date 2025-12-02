using System;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

namespace Code.Simulation
{
    /// <summary>
    /// Finds coplanar duplicates in a <c>RaycastHit</c> array. The array contains hits from multiple origins.
    /// Each set of hits separated by <c>hitsStride</c> is checked separately.
    /// </summary>
    public class CoplanarHits : IDisposable
    {
        private NativeArray<bool> _coplanarComparisonResults;
        private NativeArray<bool> _isHitCoplanar;

        public JobHandle FindCoplanarHits(JobHandle computeHitsHandle, NativeArray<RaycastHit> hits, int hitsStride,
            out NativeArray<bool> isCoplanar)
        {
            var numComparisonsPerOrigin = hitsStride * (hitsStride - 1) / 2;
            var numOrigins = hits.Length / hitsStride;
            var numComparisonsTotal = numComparisonsPerOrigin * numOrigins;
            _coplanarComparisonResults =
                Helper.ReallocateIfNeeded(_coplanarComparisonResults, numComparisonsTotal, Allocator.Persistent);
            isCoplanar = _coplanarComparisonResults;

            var findCoplanarHitsHandle = new FindCoplanarHitsJob()
            {
                IsCoplanar = _coplanarComparisonResults,
                Hits = hits,
                HitsPerOrigin = hitsStride,
            }.ScheduleParallel(numComparisonsTotal, 32, computeHitsHandle);
            _isHitCoplanar = Helper.ReallocateIfNeeded(_isHitCoplanar, hits.Length, Allocator.Persistent);
        }

        [BurstCompile]
        private struct FindCoplanarHitsJob : IJobFor
        {
            public NativeArray<bool> IsCoplanar;
            [ReadOnly] public NativeArray<RaycastHit> Hits;
            [ReadOnly] public int HitsPerOrigin;
            [ReadOnly] public int ComparisonsPerOrigin;

            public void Execute(int index)
            {
                var comparisonIndex = index % ComparisonsPerOrigin;
                Helper.GetIndexPair(HitsPerOrigin, comparisonIndex, out var i, out var j);
                var firstHitIndex = Hits.Length - index % ComparisonsPerOrigin;
                i += firstHitIndex;
                j += firstHitIndex;
                IsCoplanar[index] = CheckCoplanar(Hits[i], Hits[j]);
            }

            /// <summary>
            /// Determines if two raycasts hit the same surface (or two coplanar surfaces)
            /// </summary>
            private bool CheckCoplanar(RaycastHit a, RaycastHit b)
            {
                var sameNormal = math.dot(a.normal, b.normal) > 0.99;
                var distToPlane = math.abs(math.dot(a.point - b.point, b.normal));
                return sameNormal && distToPlane < 0.01f;
            }
        }

        [BurstCompile]
        private struct StoreCoplanarHitsJob : IJobFor
        {
            public NativeArray<bool> IsHitCoplanar;
            [ReadOnly] public NativeArray<bool> ComparisonResults;

            public void Execute(int index)
            {
                IsHitCoplanar[index] = 
            }
        }

        public void Dispose()
        {
            _coplanarComparisonResults.Dispose();
            _isHitCoplanar.Dispose();
        }
    }
}
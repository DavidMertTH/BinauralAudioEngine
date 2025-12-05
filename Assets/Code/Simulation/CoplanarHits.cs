using System;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

namespace Code.Simulation
{
    public class CoplanarHits : IDisposable
    {
        private NativeArray<bool> _coplanarComparisonResults;
        private NativeArray<bool> _isHitCoplanar;

        /// <summary>
        /// Finds raycast hits from the same origin that hit the same surface. This is important for the image source
        /// method where specular reflections are calculated, because only one such reflection is possible per surface.
        /// </summary>
        /// <param name="computeHitsHandle">Should complete when the hits array is ready.</param>
        /// <param name="hits">May contain hits from multiple origins, separated by <c>hitsStride</c>.</param>
        /// <param name="hitsStride">The number of hits per origin</param>
        /// <param name="isCoplanar">Each element corresponds to the hit in <c>hits</c> with the same index.
        /// Within each group of coplanar hits, one will be <c>false</c>, the others <c>true</c>, such that they can
        /// be skipped when coplanar hits are not relevant.</param>
        /// <returns></returns>
        public JobHandle FindCoplanarHits(JobHandle computeHitsHandle, NativeArray<RaycastHit> hits, int hitsStride,
            out NativeArray<bool> isCoplanar)
        {
            var numComparisonsPerOrigin = hitsStride * (hitsStride - 1) / 2;
            var numOrigins = hits.Length / hitsStride;
            var numComparisonsTotal = numComparisonsPerOrigin * numOrigins;
            _coplanarComparisonResults =
                Helper.ReallocateIfNeeded(_coplanarComparisonResults, numComparisonsTotal, Allocator.Persistent);

            var findCoplanarHitsHandle = new FindCoplanarHitsJob()
            {
                IsCoplanar = _coplanarComparisonResults,
                Hits = hits,
                HitsPerOrigin = hitsStride,
                ComparisonsPerOrigin = numComparisonsPerOrigin
            }.ScheduleParallel(numComparisonsTotal, 32, computeHitsHandle);
            _isHitCoplanar = Helper.ReallocateIfNeeded(_isHitCoplanar, hits.Length, Allocator.Persistent);
            isCoplanar = _isHitCoplanar;

            var storeCoplanarHitsHandle = new StoreCoplanarHitsJob()
            {
                IsHitCoplanar = _isHitCoplanar,
                ComparisonResults = _coplanarComparisonResults,
                HitsPerOrigin = hitsStride,
                ComparisonsPerOrigin = numComparisonsPerOrigin
            }.ScheduleParallel(_isHitCoplanar.Length, 32, findCoplanarHitsHandle);

            return storeCoplanarHitsHandle;
        }

        [BurstCompile]
        private struct FindCoplanarHitsJob : IJobFor
        {
            /// <summary>
            /// The output of the job. Every group of <c>ComparisonsPerOrigin</c> consecutive elements refers to the
            /// hits of one raycast origin from <c>Hits</c>. Every element specifies whether a pair of raycasts from
            /// that origin hit coplanar surfaces. 
            /// </summary>
            public NativeArray<bool> IsCoplanar;

            /// <summary>
            /// <c>Hits</c> is divided by a stride of <c>HitsPerOrigin</c> into groups of raycast hits from a common
            /// origin.
            /// </summary>
            [ReadOnly] public NativeArray<RaycastHit> Hits;

            /// <summary>
            /// The number of raycasts done per origin and subsequently the number of raycast hits per origin.
            /// </summary>
            [ReadOnly] public int HitsPerOrigin;

            /// <summary>
            /// The number of unique pairs of ray casts from the same origin.
            /// </summary>
            [ReadOnly] public int ComparisonsPerOrigin;

            public void Execute(int index)
            {
                var comparisonIndex = index % ComparisonsPerOrigin;
                Helper.GetIndexPair(HitsPerOrigin, comparisonIndex, out var i, out var j);
                var originIndex = index / ComparisonsPerOrigin;
                var firstHitIndex = originIndex * HitsPerOrigin;
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

        /// <summary>
        /// Combines the comparisons of every pair of two ray hits from one origin.
        /// </summary>
        [BurstCompile]
        private struct StoreCoplanarHitsJob : IJobFor
        {
            public NativeArray<bool> IsHitCoplanar;
            [ReadOnly] public NativeArray<bool> ComparisonResults;
            [ReadOnly] public int HitsPerOrigin;
            [ReadOnly] public int ComparisonsPerOrigin;

            public void Execute(int index)
            {
                var originsBeforeThis = index / HitsPerOrigin;
                var comparisonsBeforeThis = originsBeforeThis * ComparisonsPerOrigin;
                var indexPerOrigin = index % HitsPerOrigin; // The first hit from each origin will be 0
                var startIndex = comparisonsBeforeThis + (indexPerOrigin - 1) * (indexPerOrigin - 2) / 2;
                var endIndex = startIndex + HitsPerOrigin - indexPerOrigin - 1;
                for (var i = startIndex; i < endIndex; i++)
                {
                    if (ComparisonResults[i])
                    {
                        IsHitCoplanar[index] = true;
                        return;
                    }
                }

                IsHitCoplanar[index] = false;
            }
        }

        public void Dispose()
        {
            _coplanarComparisonResults.Dispose();
            _isHitCoplanar.Dispose();
        }
    }
}
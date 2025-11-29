using System;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

namespace Code.Simulation
{
    public class SortRaycastHits : IDisposable
    {
        private NativeArray<RaycastHitComparison> _comparisonResults;
        private NativeArray<int> _numUniqueHits;
        
        /// <summary>
        /// Sorts the <c>hits</c> array such that it starts with a 
        /// </summary>
        /// <param name="computeHitsHandle"></param>
        /// <param name="hits"></param>
        /// <param name="nonCoplanarHits"></param>
        /// <returns></returns>
        /// <exception cref="NotImplementedException"></exception>
        private JobHandle SortHitsCoplanar(JobHandle computeHitsHandle, NativeArray<RaycastHit> hits,
            out NativeArray<RaycastHit> nonCoplanarHits)
        {
            var numComparisons = hits.Length * (hits.Length - 1) / 2;
            _comparisonResults = Helper.ReallocateIfNeeded(_comparisonResults, numComparisons, Allocator.Persistent);
            var findCoplanarHitsJob = new FindCoplanarHitsJob()
            {
                ComparisonResults = _comparisonResults,
                Hits = hits
            };
            var findCoplanarHitsHandle = findCoplanarHitsJob.ScheduleParallel(numComparisons, 32, computeHitsHandle);
            var sortCoplanarHitsJob = new SortCoplanarHitsJob()
            {
                Hits = hits,
                UniqueCount = _numUniqueHits,
                ComparisonResults = _comparisonResults
            };
            var sortCoplanarHitsHandle = sortCoplanarHitsJob.Schedule(findCoplanarHitsHandle);
            
        }

        [BurstCompile]
        private struct FindCoplanarHitsJob : IJobFor
        {
            // These two arrays are NOT the same length. ComparisonResults has one entry for every possible pair in hits.
            public NativeArray<RaycastHitComparison> ComparisonResults;
            [ReadOnly] public NativeArray<RaycastHit> Hits;

            public void Execute(int index)
            {
                GetIndices(index, out var i, out var j);
                var comparison = ComparisonResults[index];
                comparison.i = i;
                comparison.j = j;
                comparison.coplanar = IsCoplanar(Hits[i], Hits[j]);
                ComparisonResults[index] = comparison;
            }

            /// <summary>
            /// Determines which pair of array elements should be compared.
            /// </summary>
            /// <param name="k">The index of the comparison</param>
            /// <param name="i">One of the two indices to compare</param>
            /// <param name="j">One of the two indices to compare</param>
            private void GetIndices(int k, out int i, out int j)
            {
                var n = Hits.Length;
                i = (int)math.floor((2 * n - 1 - math.sqrt(math.square(2 * n - 1) - 8 * k)) / 2);
                var rowStart = math.floor(i * n - i * (i + 1) / 2f);
                j = (int)(k - rowStart + i + 1);
            }

            /// <summary>
            /// Determines if two raycasts hit the same surface (or two coplanar surfaces)
            /// </summary>
            private bool IsCoplanar(RaycastHit a, RaycastHit b)
            {
                var sameNormal = math.dot(a.normal, b.normal) > 0.99;
                var distToPlane = math.abs(math.dot(a.point - b.point, b.normal));
                return sameNormal && distToPlane < 0.01f;
            }
        }

        /// <summary>
        /// Sort the <c>Hits</c> such that unique hits come first, followed by hits of coplanar surfaces
        /// </summary>
        [BurstCompile]
        private struct SortCoplanarHitsJob : IJob
        {
            public NativeArray<RaycastHit> Hits;
            public NativeArray<int> UniqueCount;
            [ReadOnly] public NativeArray<RaycastHitComparison> ComparisonResults;

            public void Execute()
            {
                using var coplanarIndices = GetCoplanarIndices();
                UniqueCount[0] = coplanarIndices.Length;
                SortHits(coplanarIndices);
            }

            /// <returns>An array with indices such that no coplanar hits would remain after their removal.</returns>
            private NativeArray<int> GetCoplanarIndices()
            {
                // Temp allocations are disposed automatically
                var coplanarIndices = new NativeArray<int>(Hits.Length - 1, Allocator.Temp);

                var coplanarCount = 0;
                for (var i = 0; i < ComparisonResults.Length; i++)
                {
                    var comparison = ComparisonResults[i];
                    if (comparison.coplanar)
                    {
                        var coplanarIndex = ComparisonResults[i].j;
                        coplanarIndices.Sort();
                        var wasFoundBefore = !coplanarIndices.GetSubArray(0, coplanarCount).Contains(coplanarIndex);
                        if (!wasFoundBefore)
                        {
                            coplanarIndices[coplanarCount] = coplanarIndex;
                            coplanarCount++;
                        }
                    }
                }

                return coplanarIndices.GetSubArray(0, coplanarCount);
            }

            private void SortHits(NativeArray<int> coplanarIndices)
            {
                var uniqueCount = Hits.Length - coplanarIndices.Length;
                var swaps = new NativeArray<IndexPair>(coplanarIndices.Length, Allocator.Temp);
                // The number of coplanar hits that need to be moved to the back of the array
                var coplanarMoveCounter = 0;
                // The number of unique hits that need go be moved to the front of the array
                var uniqueMoveCounter = 0;
                for (var i = 0; i < Hits.Length; i++)
                {
                    if (i < uniqueCount && coplanarIndices.Contains(i))
                    {
                        var swap = swaps[coplanarMoveCounter];
                        swap.i = i;
                        swaps[coplanarMoveCounter] = swap;
                        coplanarMoveCounter++;
                    }
                    else if (i >= uniqueCount && !coplanarIndices.Contains(i))
                    {
                        var swap = swaps[uniqueMoveCounter];
                        swap.j = i;
                        swaps[i] = swap;
                        uniqueMoveCounter++;
                    }
                }

#if UNITY_EDITOR
                if (coplanarMoveCounter != uniqueMoveCounter)
                    Debug.LogError("Number of coplanar raycast hits to be swapped does not match number of " +
                                   " unique raycast hits to be swapped.");
#endif
                foreach (var swap in swaps)
                {
                    (Hits[swap.i], Hits[swap.j]) = (Hits[swap.j], Hits[swap.i]);
                }
            }
        }

        [BurstCompile]
        private struct RaycastHitComparison
        {
            public int i;
            public int j;
            public bool coplanar;
        }

        [BurstCompile]
        private struct IndexPair
        {
            public int i, j;
        }

        public void Dispose()
        {
            _comparisonResults.Dispose();
        }
    }
}
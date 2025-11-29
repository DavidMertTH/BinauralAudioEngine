using System;
using System.Threading;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

namespace Code.Simulation
{
    /// <summary>
    /// Finds coplanar duplicates in a <c>RaycastHit</c> array.
    /// </summary>
    public class CoplanarHits : IDisposable
    {
        private NativeArray<bool> _isCoplanar;

        public JobHandle FindCoplanarHits(JobHandle computeHitsHandle, NativeArray<RaycastHit> hits,
            out NativeArray<bool> isCoplanar)
        {
            _isCoplanar = Helper.ReallocateIfNeeded(_isCoplanar, hits.Length, Allocator.Persistent);
            isCoplanar = _isCoplanar;
            var numComparisons = hits.Length * (hits.Length - 1) / 2;
            var findCoplanarHitsJob = new FindCoplanarHitsJob()
            {
                Hits = hits
            };
            return findCoplanarHitsJob.ScheduleParallel(numComparisons, 32, computeHitsHandle);
        }

        [BurstCompile]
        private struct FindCoplanarHitsJob : IJobFor
        {
            private int _lock;
            public NativeArray<bool> IsCoplanar;
            [ReadOnly] public NativeArray<RaycastHit> Hits;

            public void Execute(int index)
            {
                GetIndices(index, out var i, out var j);
                var isCoplanar = CheckCoplanar(Hits[i], Hits[j]);
                if (!isCoplanar) return;

                while (0 != Interlocked.Exchange(ref _lock, 1))
                {
                }

                IsCoplanar[j] = true;
                Interlocked.Exchange(ref _lock, 0);
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
            private bool CheckCoplanar(RaycastHit a, RaycastHit b)
            {
                var sameNormal = math.dot(a.normal, b.normal) > 0.99;
                var distToPlane = math.abs(math.dot(a.point - b.point, b.normal));
                return sameNormal && distToPlane < 0.01f;
            }
        }

        public void Dispose()
        {
            _isCoplanar.Dispose();
        }
    }
}
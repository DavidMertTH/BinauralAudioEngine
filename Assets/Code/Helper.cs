using System.Threading;
using System.Threading.Tasks;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

namespace Code
{
    public static class Helper
    {
        public static NativeArray<float3> GetFibonacciPoints(int samples)
        {
            NativeArray<float3> arr = new NativeArray<float3>(samples, Allocator.Persistent);
            float phi = Mathf.PI * (3f - Mathf.Sqrt(5f));

            for (int i = 0; i < samples; i++)
            {
                float y = 1f - (i / (float)(samples - 1)) * 2f;
                float r = Mathf.Sqrt(1f - y * y);
                float theta = phi * i;

                float x = Mathf.Cos(theta) * r;
                float z = Mathf.Sin(theta) * r;

                arr[i] = new float3(x, y, z);
            }

            return arr;
        }

        public static Vector3 GetMouseWorldPosition(Camera camera)
        {
            float z = -camera.transform.position.z;
            Vector3 mouseScreenPos = new Vector3(Input.mousePosition.x, Input.mousePosition.y, z);
            return camera.ScreenToWorldPoint(mouseScreenPos);
        }

        public static float[] ReallocateIfNeeded(float[] arr, int desiredSize)
        {
            return ReallocateIfNeeded(arr, desiredSize, out _);
        }

        public static float[] ReallocateIfNeeded(float[] arr, int desiredSize, out bool didReallocate)
        {
            if (arr == null || arr.Length != desiredSize)
            {
                didReallocate = true;
                return new float[desiredSize];
            }

            didReallocate = false;
            return arr;
        }

        public static NativeArray<T> ReallocateIfNeeded<T>(NativeArray<T> arr, int desiredSize, Allocator allocator)
            where T : struct
        {
            return ReallocateIfNeeded(arr, desiredSize, allocator, out _);
        }

        public static NativeArray<T> ReallocateIfNeeded<T>(NativeArray<T> arr, int desiredSize, Allocator allocator,
            out bool didReallocate) where T : struct
        {
            didReallocate = false;
            if (arr.IsCreated && arr.Length == desiredSize) return arr;
            if (arr.IsCreated) arr.Dispose();
            didReallocate = true;
            return new NativeArray<T>(desiredSize, allocator);
        }

        public static void DeinterlaceChannels(float[] interlaced, float[] channelA, float[] channelB)
        {
            for (int i = 0, j = 0; i < interlaced.Length; i += 2, j++)
            {
                channelA[j] = interlaced[i];
                channelB[j] = interlaced[i + 1];
            }
        }

        public static void InterlaceChannels(float[] interlaced, float[] channelA, float[] channelB)
        {
            for (int i = 0, j = 0; i < interlaced.Length; i += 2, j++)
            {
                interlaced[i] = channelA[j];
                interlaced[i + 1] = channelB[j];
            }
        }

        [BurstCompile]
        public static float3 MirrorPointAcrossPlane(float3 point, float3 planePoint, float3 planeNormal)
        {
            var distancePointToPlane = math.dot(point - planePoint, planeNormal);
            return point - 2 * distancePointToPlane * planeNormal;
        }

        [BurstCompile]
        public static float DistanceFromPlane(float3 point, float3 planePoint, float3 planeNormal)
        {
            return math.dot(planeNormal, point - planePoint);
        }

        public static async Task ToTask(this JobHandle jobHandle, CancellationToken ct = default)
        {
            while (!jobHandle.IsCompleted)
                await Awaitable.NextFrameAsync(ct);
            jobHandle.Complete();
        }

        /// <summary>
        /// Gets all possible pairs of two indices in an array of length <c>n</c> for every <c>k</c> between 0
        /// (inclusive) and <c>n * (n - 1) / 2</c> (exclusive)
        /// </summary>
        /// <param name="n">The total number of elements</param>
        /// <param name="k">The index of the pair</param>
        /// <param name="i">The row index</param>
        /// <param name="j">The column index</param>
        public static void GetIndexPair(int n, int k, out int i, out int j)
        {
            i = (int)math.floor((2 * n - 1 - math.sqrt(math.square(2 * n - 1) - 8 * k)) / 2);
            var rowStart = math.floor(i * n - i * (i + 1) / 2f);
            j = (int)(k - rowStart + i + 1);
        }

        public static bool DidHit(RaycastHit hit) => hit.distance != 0f;

        /// <summary>
        /// Determines if two raycasts hit the same surface (or two coplanar surfaces)
        /// </summary>
        public static bool CheckCoplanar(RaycastHit a, RaycastHit b)
        {
            var bothMissed = !DidHit(a) && !DidHit(b);
            if (bothMissed)
                return true;
            var oneHitOneMissed = DidHit(a) != DidHit(b);
            if (oneHitOneMissed)
                return false;
            var sameNormal = math.dot(a.normal, b.normal) > 0.99;
            var distToPlane = math.abs(math.dot(a.point - b.point, b.normal));
            if (sameNormal && distToPlane < 0.01f)
                return true;
            return false;
        }

        /// <summary>
        /// Intersect a line segment with a plane
        /// </summary>
        /// <returns>Whether an intersection was found</returns>
        public static bool TryIntersectLineSegmentWithPlane(float3 lineSegStart, float3 lineSegEnd, float3 planePoint,
            float3 planeNormal, out float3 intersection)
        {
            var startToEnd = lineSegEnd - lineSegStart;
            var d = math.dot(planeNormal, startToEnd);
            if (math.abs(d) < 0.001) // Parallel to plane
            {
                intersection = float3.zero;
                return false;
            }

            var t = math.dot(planeNormal, planePoint - lineSegStart) / d;
            if (t is < 0f or > 1f) // Intersection is past limits of line segment
            {
                intersection = float3.zero;
                return false;
            }

            intersection = lineSegStart + startToEnd * t;
            return true;
        }

        public static bool IsClose(float3 a, float3 b)
        {
            var diff = a - b;
            return math.abs(diff.x) < 0.01f && math.abs(diff.y) < 0.01f && math.abs(diff.z) < 0.01f;
        }
        
        public static bool DidRayHitPoint(RaycastHit ray, float3 point)
        {
            return DidHit(ray) && IsClose(ray.point, point);
        }
    }
}
using Unity.Collections;
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
            return new NativeArray<T>(arr, allocator);
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
    }
}
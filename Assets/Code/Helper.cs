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
            if (arr == null || arr.Length != desiredSize)
                return new float[desiredSize];
            return arr;
        }
        
        public static void DeinterlaceChannels(float[] interleaved, float[] left, float[] right)
        {
            for (int i = 0, j = 0; i < interleaved.Length; i += 2, j++)
            {
                left[j] = interleaved[i];
                right[j] = interleaved[i + 1];
            }
        }

        public static void InterlaceChannels(float[] interleaved, float[] left, float[] right)
        {
            for (int i = 0, j = 0; i < interleaved.Length; i += 2, j++)
            {
                interleaved[i] = left[j];
                interleaved[i + 1] = right[j];
            }
        }
    }
}
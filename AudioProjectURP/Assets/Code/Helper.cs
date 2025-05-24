using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;

namespace DefaultNamespace
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
    }
}
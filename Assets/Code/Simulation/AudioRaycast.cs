using System;
using System.Collections.Generic;
using System.Threading;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine; 

namespace Code.Simulation
{
    /// <summary>
    /// Casts audio rays into the scene using three methods.
    /// </summary>
    public class AudioRaycast
    {
        public static Awaitable CastDirectRays(float4 origin, NativeArray<float4> targets,
            out NativeArray<AudioPath> paths, CancellationToken ct)
        {
            throw new NotImplementedException();
        }

        public static Awaitable CastImageSourceRays(float4 origin, NativeArray<float4> targets,
            out NativeArray<AudioPath> paths, CancellationToken ct)
        {
            throw new NotImplementedException();
        }

        public static Awaitable CastIndirectRays(float4 origin, out NativeArray<float4> targets,
            out NativeArray<AudioPath> paths, CancellationToken ct)
        {
            throw new NotImplementedException();
            
        }
    }
}
using System;
using System.Collections.Generic;
using System.Threading;
using Code.Simulation.Raycasting;
using UnityEngine;

namespace Code.Simulation
{
    /// <summary>
    /// Casts audio rays into the scene using three methods.
    /// </summary>
    public class AudioRaycast
    {
        public static Awaitable<List<AudioRay>> CastDirectRay(Vector3 origin, Vector3 target)
        {
            throw new NotImplementedException();
        }

        public static Awaitable<List<AudioRay>> CastImageSourceRays(Vector3 origin, Vector3 target)
        {
            throw new NotImplementedException();
            
        }

        public static Awaitable<List<AudioRay>> CastUntargetedRays(Vector3 origin, CancellationToken ct)
        {
            throw new NotImplementedException();
        }
    }
}
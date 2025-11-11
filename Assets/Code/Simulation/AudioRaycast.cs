using System.Collections.Generic;
using System.Threading;
using UnityEngine;

namespace Code.Simulation
{
    /// <summary>
    /// Casts audio rays into the scene using three methods.
    /// </summary>
    public class AudioRaycast
    {
        public static AudioRay CastDirectRay(Vector3 origin, Vector3 target)
        {
        }

        public static Awaitable<List<AudioRay>> CastImageSourceRays(Vector3 origin, Vector3 target)
        {
        }

        public static Awaitable<List<AudioRay>> CastUntargetedRays(Vector3 origin, CancellationToken ct)
        {
        }
    }
}
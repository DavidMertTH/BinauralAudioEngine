using System.Threading;
using UnityEngine;
using Code.Simulation;

namespace Code
{
    public class BinauralAudioEngine : MonoBehaviour
    {
        private List<
        /// <summary>
        /// To be called by the GUI whenever the user makes changes to the scene that affect the impulse response.
        /// </summary>
        public async Awaitable UpdateImpulseResponses(CancellationToken ct)
        {
            var untargetedRays = await AudioRaycast.CastUntargetedRays(ct);
        }
    }
}
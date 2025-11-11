using System.Collections.Generic;
using System.Threading;
using UnityEngine;
using Code.Simulation;
using Code.Renderer;
using UnityEngine.Serialization;

namespace Code
{
    public class BinauralAudioEngine : MonoBehaviour
    {
        [SerializeField] private Transform listener;
        private List<BinauralAudioFilter> _audioFilters;

        /// <summary>
        /// To be called by the GUI whenever the user makes changes to the scene that affect the impulse response.
        /// </summary>
        public async Awaitable UpdateImpulseResponses(CancellationToken ct)
        {
            // Go from main thread to background thread
            await Awaitable.BackgroundThreadAsync();
            var untargetedRays = await AudioRaycast.CastUntargetedRays(listener.position, ct);
        }

        public void RegisterAudioFilter(BinauralAudioFilter filter)
        {
            if (_audioFilters == null)
                _audioFilters = new List<BinauralAudioFilter>();

            if (!_audioFilters.Contains(filter))
                _audioFilters.Add(filter);
        }

        public void UnregisterAudioFilter(BinauralAudioFilter filter)
        {
            if (_audioFilters != null && _audioFilters.Contains(filter))
                _audioFilters.Remove(filter);
        }
    }
}
using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;
using Code.Simulation;
using Code.Renderer;

namespace Code
{
    public class BinauralAudioEngine : MonoBehaviour
    {
        [SerializeField] private Transform listener;
        private List<BinauralAudioFilter> _audioFilters;

        /// <summary>
        /// To be called by the GUI whenever the user makes changes to the scene that affect the impulse response.
        /// </summary>
        public async Awaitable UpdateAllImpulseResponses(CancellationToken ct)
        {
            await ComputeAudioRays(ct);
            await ComputeImpulseResponses(ct);
        }
        
        public void NotifyAudioSourceMoved(BinauralAudioFilter audioFilter)
        {
            
        }

        private async Awaitable ComputeAudioRays(CancellationToken ct)
        {
            throw new NotImplementedException();
        }

        private async Awaitable ComputeImpulseResponses(CancellationToken ct)
        {
            throw new NotImplementedException();
        }

        public void RegisterAudioFilter(BinauralAudioFilter filter)
        {
            _audioFilters ??= new List<BinauralAudioFilter>();

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
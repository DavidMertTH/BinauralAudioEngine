using System;
using System.Collections.Generic;
using System.Threading;
using ArthurKehrwald.Singleton;
using Code.Renderer;
using UnityEngine;

namespace Code.Core
{
    public class BinauralAudioEngine : Singleton<BinauralAudioEngine, DoAutoCreate<BinauralAudioEngine>>
    {
        [SerializeField] private BinauralAudioSettings settings;
        public BinauralAudioSettings Settings => settings;
        private Transform _listener;

        private Transform Listener
        {
            get
            {
                _listener ??= FindFirstObjectByType<AudioListener>().transform;
                if (_listener == null)
                    throw new NullReferenceException("No AudioListener was found in the scene.");
                return _listener;
            }
        }

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
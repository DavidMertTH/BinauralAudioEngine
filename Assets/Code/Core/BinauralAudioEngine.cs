using System;
using System.Collections.Generic;
using System.Threading;
using ArthurKehrwald.Singleton;
using Code.Renderer;
using Code.Simulation;
using Unity.Collections;
using Unity.Jobs;
using UnityEngine;

namespace Code.Core
{
    public class BinauralAudioEngine : Singleton<BinauralAudioEngine, DoAutoCreate<BinauralAudioEngine>>
    {
        [SerializeField] private BinauralAudioSettings settings;
        public BinauralAudioSettings Settings => settings;
        private Transform _listener;
        private NativeArray<AudioRay> _audioRays;
        private readonly SurroundRaycast _surroundRaycast = new();
        private readonly GlobalSimulationData _globalSimulationData = new();
        private readonly DirectRaycast _directRaycast = new();

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

        private List<BinauralAudioFilter> _audioFilters = new();

        /// <summary>
        /// To be called by the GUI whenever the user makes changes to the scene that affect the impulse response.
        /// </summary>
        public async Awaitable UpdateAllImpulseResponses(CancellationToken ct)
        {
            var rayJob = ComputeAudioRays(ct);
            var impulseResponseJob = ComputeImpulseResponses(rayJob, ct);
            var combinedJob = JobHandle.CombineDependencies(impulseResponseJob, rayJob);
            while (!combinedJob.IsCompleted)
                await Awaitable.NextFrameAsync(ct);
        }

        private JobHandle ComputeAudioRays(CancellationToken ct)
        {
            var origins = _globalSimulationData.UpdateListenerAndSourcePositions(Listener, _audioFilters);
            var rayCounts = Settings.GetRayCounts(_audioFilters.Count);
            var directHitHandle = _directRaycast.GetDirectRays(_globalSimulationData.ListenerPosition,
                _globalSimulationData.SourcePositions, out var directHits);
            var surroundRaycastHandle =
                _surroundRaycast.CastRaysAroundOrigins(origins, rayCounts.AroundListenerAndSources, out var hits);
            throw new NotImplementedException();
        }

        private JobHandle ComputeImpulseResponses(JobHandle rayJob, CancellationToken ct)
        {
            throw new NotImplementedException();
        }

        public void RegisterAudioFilter(BinauralAudioFilter filter)
        {
            if (!_audioFilters.Contains(filter))
                _audioFilters.Add(filter);
        }

        public void UnregisterAudioFilter(BinauralAudioFilter filter)
        {
            if (_audioFilters.Contains(filter))
                _audioFilters.Remove(filter);
        }

        private void OnDestroy()
        {
            _surroundRaycast.Dispose();
            _globalSimulationData.Dispose();
            _directRaycast.Dispose();
        }
    }
}
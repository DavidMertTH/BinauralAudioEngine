using System;
using System.Collections.Generic;
using ArthurKehrwald.Singleton;
using Code.Renderer;
using Code.Simulation;
using Unity.Jobs;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace Code.Core
{
    public class BinauralAudioEngine : Singleton<BinauralAudioEngine, DoAutoCreate<BinauralAudioEngine>>
    {
        [SerializeField] private BinauralAudioSettings settings;

        // TODO: Remove
        public AudioPath[] AudioPaths;
        public bool IsReady { get; private set; }

        private readonly List<BinauralAudioFilter> _audioFilters = new();
        private Transform _listener;

        private Transform Listener
        {
            get
            {
                _listener ??= FindAnyObjectByType<AudioListener>(FindObjectsInactive.Include).transform;
                if (_listener == null)
                    throw new NullReferenceException("No AudioListener was found in the scene.");
                return _listener;
            }
        }

        private async void Update()
        {
            try
            {
                if (Input.GetKeyDown(KeyCode.Space))
                {
                    await UpdateAllImpulseResponses();
                    IsReady = true;
                }
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception e)
            {
                Debug.LogError(e);
            }
        }

        /// <summary>
        /// To be called by the GUI whenever the user makes changes to the scene that affect the impulse response.
        /// </summary>
        public async Awaitable UpdateAllImpulseResponses()
        {
            using var pathSim = new PathSimulation(Listener, _audioFilters, settings);
            var pathJob = pathSim.Schedule(out var paths);

            using var irRenderer = new ImpulseResponseRenderer(settings, Listener, _audioFilters.Count);
            var impulseResponseJob = irRenderer.Schedule(paths, pathJob, out _);

            var filtersCopy = new List<BinauralAudioFilter>(_audioFilters); // In case it changes during the job
            var combinedJob = JobHandle.CombineDependencies(impulseResponseJob, pathJob);
            await combinedJob.ToAwaitable();

            AudioPaths = paths.ToArray();
            for (var i = 0; i < filtersCopy.Count; i++)
            {
                var sourceIr = irRenderer.GetImpulseResponse(i);
                if (filtersCopy[i].TryGetComponent<AudioSourceObject>(out var sourceObject))
                {
                    sourceObject.EnterNewIr(sourceIr);
                }
            }
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
    }
}
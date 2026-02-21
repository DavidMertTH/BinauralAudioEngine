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
            await UpdateImpulseResponses(_audioFilters);
        }
        
        /// <summary>
        /// Call when a single audio source is repositioned.
        /// </summary>
        public async Awaitable UpdateImpulseResponse(BinauralAudioFilter filter)
        {
            await UpdateImpulseResponses(new List<BinauralAudioFilter> { filter });
        }
        
        public async Awaitable UpdateImpulseResponses(List<BinauralAudioFilter> filters)
        {
            var sourcePositions = filters.ConvertAll(f => f.transform.position);
            using var pathSim = new PathSimulation(Listener, sourcePositions, settings);
            var pathJob = pathSim.Schedule(out var paths);

            using var irRenderer = new ImpulseResponseRenderer(settings, Listener, filters.Count);
            var impulseResponseJob = irRenderer.Schedule(paths, pathJob, out _);

            var filtersCopy = new List<BinauralAudioFilter>(filters); // Defensive copy
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
using System;
using System.Collections.Generic;
using System.Linq;
using ArthurKehrwald.Singleton;
using Code.Renderer;
using Code.Simulation;
using Unity.Jobs;
using UnityEngine;
using UnityEngine.Events;
using Debug = UnityEngine.Debug;

namespace Code.Core
{
    public class BinauralAudioEngine : Singleton<BinauralAudioEngine, DoAutoCreate<BinauralAudioEngine>>
    {
        [SerializeField] private BinauralAudioSettings settings;

        // TODO: Remove
        public AudioPath[] AudioPaths;
        public bool IsReady { get; private set; }

        public readonly List<BinauralAudioFilter> audioFilters = new();
        private Transform _listener;
        public UnityEvent simulationDone;  // delegate
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
            await UpdateImpulseResponses(audioFilters);
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
            JobHandle pathJob = pathSim.Schedule(out var paths);
            pathJob.Complete();
            AudioPaths = paths.ToArray();

            
            
            // using var irRenderer = new ImpulseResponseRenderer(settings, Listener, filters.Count);
            // JobHandle impulseResponseJob = irRenderer.Schedule(paths, pathJob, out _);

            var filtersCopy = new List<BinauralAudioFilter>(filters); // Defensive copy
            // JobHandle combinedJob = JobHandle.CombineDependencies(impulseResponseJob, pathJob);
            // await combinedJob.ToAwaitable();
            for (int i = 0; i < filtersCopy.Count; i++)
            {
                var irs = RaysToIr.CreateBrirLeftAndRight(FilterPathForIndex(AudioPaths,i), 1024 * 4, _listener, 48000, 1);
                if (filtersCopy[i].TryGetComponent<AudioSourceObject>(out var sourceObject))
                {
                    sourceObject.EnterNewIr(irs.Item1, irs.Item2);
                }
            }
            
            for (var i = 0; i < filtersCopy.Count; i++)
            {
                // var sourceIr = irRenderer.GetImpulseResponse(i);
                // if (filtersCopy[i].TryGetComponent<AudioSourceObject>(out var sourceObject))
                // {
                //     sourceObject.EnterNewIr(sourceIr);
                // }
            }
            simulationDone?.Invoke();
        }

        public AudioPath[] FilterPathForIndex(AudioPath[] paths,int index)
        {
            return paths.Where(p => p.SourceIndex == index).ToArray();
        }
        public void RegisterAudioFilter(BinauralAudioFilter filter)
        {
            if (!audioFilters.Contains(filter))
                audioFilters.Add(filter);
        }

        public void UnregisterAudioFilter(BinauralAudioFilter filter)
        {
            if (audioFilters.Contains(filter))
                audioFilters.Remove(filter);
        }
    }
}
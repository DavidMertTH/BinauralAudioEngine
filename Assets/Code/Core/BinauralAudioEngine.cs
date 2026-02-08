using System;
using System.Collections.Generic;
using System.IO;
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
        private BinauralAudioSettings Settings => settings ??= new BinauralAudioSettings();

        // TODO: Remove
        public AudioPath[] AudioPaths;
        public bool IsReady { get; private set; }

        private readonly List<BinauralAudioFilter> _audioFilters = new List<BinauralAudioFilter>();
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
            var layout = Settings.GetAudioPathArrayLayout(_audioFilters.Count);
            using var simData = new SimulationData(Listener, _audioFilters, layout, settings.ImpulseResponseSamples,
                Settings.SofaFile);
            var pathJob = ComputeAudioPaths(simData);
            var impulseResponseJob = ComputeImpulseResponses(simData, pathJob);
            var combinedJob = JobHandle.CombineDependencies(impulseResponseJob, pathJob);
            await combinedJob.ToAwaitable();

            AudioPaths = simData.AllAudioPaths.ToArray();
            for (var i = 0; i < simData.Filters.Count; i++)
            {
                var sourceIr = simData.GetImpulseResponse(i);
                if (simData.Filters[i].TryGetComponent<AudioSourceObject>(out var sourceObject))
                {
                    sourceObject.EnterNewIr(sourceIr);
                }
            }
        }

        private JobHandle ComputeAudioPaths(SimulationData simData)
        {
            var directPathsHandle = simData.ComputeDirectPaths.Schedule(simData.ListenerPosition,
                simData.SourcePositions, simData.DirectPaths);
            var surroundRaycastHandle = simData.SurroundRaycast.CastRaysAroundOrigins(
                simData.ListenerAndSourcePositions, Settings.RaysAroundListenerAndEachSource, out var hits,
                out var hitsStride, out var isHitCoplanar, out var commands);
            var hitsAroundListener = hits.GetSubArray(0, hitsStride).AsReadOnly();
            var commandsAroundListener = commands.GetSubArray(0, hitsStride).AsReadOnly();
            var isHitAroundListenerCoplanar = isHitCoplanar.GetSubArray(0, hitsStride).AsReadOnly();
            var hitsAroundSources = hits.GetSubArray(hitsStride, hits.Length - hitsStride).AsReadOnly();
            var isHitAroundSourcesCoplanar =
                isHitCoplanar.GetSubArray(hitsStride, isHitCoplanar.Length - hitsStride).AsReadOnly();
            var oneBouncePathsHandle = simData.ComputeOneBouncePaths.Schedule(simData.ListenerPosition,
                simData.SourcePositions, hitsAroundListener,
                isHitAroundListenerCoplanar, surroundRaycastHandle,
                simData.OneBouncePaths);
            var twoBouncePathsHandle = simData.ComputeTwoBouncePaths.Schedule(simData.ListenerPosition,
                simData.SourcePositions, hitsAroundListener, isHitAroundListenerCoplanar, hitsAroundSources,
                isHitAroundSourcesCoplanar, hitsStride, surroundRaycastHandle, simData.TwoBouncePaths);
            var iterativePathsHandle = simData.ComputeIterativePaths.Schedule(simData.ListenerPosition,
                simData.SourcePositions, commandsAroundListener, hitsAroundListener, Settings.MaxIterativeBounces,
                surroundRaycastHandle, simData.HigherOrderPaths);
            return simData.CombinePathJobHandles(directPathsHandle, oneBouncePathsHandle, twoBouncePathsHandle,
                iterativePathsHandle);
        }

        private JobHandle ComputeImpulseResponses(SimulationData simData, JobHandle pathJob)
        {
            return ComputeImpulseResponse.Schedule(simData.AllAudioPaths, Listener,
                settings.ImpulseResponseSamples, settings.ImpulseResponseSamplesPerSecond, simData.Hrirs, pathJob,
                simData.AllImpulseResponses);
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
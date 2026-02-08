using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using ArthurKehrwald.Singleton;
using Code.Renderer;
using Code.Simulation;
using Unity.Collections;
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

        private Transform _listener;
        private List<BinauralAudioFilter> _audioFilters;
        private GlobalSimulationData _simulationData;
        private SurroundRaycast _surroundRaycast;
        private DirectPaths _directPaths;
        private OneBouncePaths _oneBouncePaths;
        private TwoBouncePaths _twoBouncePaths;
        private IterativePaths _iterativePaths;
        private NativeArray<JobHandle> _pathJobHandles;
        private SemaphoreSlim _semaphoreSlim;

        public Transform Listener
        {
            get
            {
                _listener ??= FindAnyObjectByType<AudioListener>(FindObjectsInactive.Include).transform;
                if (_listener == null)
                    throw new NullReferenceException("No AudioListener was found in the scene.");
                return _listener;
            }
        }

        private void Awake()
        {
            _audioFilters = new List<BinauralAudioFilter>();
            _simulationData = new GlobalSimulationData();
            _surroundRaycast = new SurroundRaycast();
            _directPaths = new DirectPaths();
            _oneBouncePaths = new OneBouncePaths();
            _twoBouncePaths = new TwoBouncePaths();
            _iterativePaths = new IterativePaths();
            _pathJobHandles = new NativeArray<JobHandle>(4, Allocator.Persistent);
            _semaphoreSlim = new SemaphoreSlim(1, 1);
        }

        private async void Update()
        {
            try
            {
                if (Input.GetKeyDown(KeyCode.Space))
                {
                    await UpdateAllImpulseResponses();
                    AudioPaths = _simulationData.AllAudioPaths.ToArray();
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
            await _semaphoreSlim.WaitAsync();
            try
            {
                var layout = Settings.GetAudioPathArrayLayout(_audioFilters.Count);
                _simulationData.Init(Listener, _audioFilters, layout, settings.ImpulseResponseSamples);
                var pathJob = ComputeAudioPaths();
                var impulseResponseJob = ComputeImpulseResponses(pathJob);
                var combinedJob = JobHandle.CombineDependencies(impulseResponseJob, pathJob);
                await combinedJob.ToAwaitable();
                for (var i = 0; i < _simulationData.Filters.Count; i++)
                {
                    var sourceIr = _simulationData.GetImpulseResponse(i);
                    if (_simulationData.Filters[i].TryGetComponent<AudioSourceObject>(out var sourceObject))
                    {
                        sourceObject.EnterNewIr(sourceIr);
                    }
                }
            }
            finally
            {
                _semaphoreSlim.Release();
            }
        }

        private JobHandle ComputeAudioPaths()
        {
            _pathJobHandles[0] = _directPaths.GetDirectPaths(_simulationData.ListenerPosition,
                _simulationData.SourcePositions, _simulationData.DirectPaths);
            var surroundRaycastHandle = _surroundRaycast.CastRaysAroundOrigins(
                _simulationData.ListenerAndSourcePositions, Settings.RaysAroundListenerAndEachSource, out var hits,
                out var hitsStride, out var isHitCoplanar, out var commands);
            var hitsAroundListener = hits.GetSubArray(0, hitsStride).AsReadOnly();
            var commandsAroundListener = commands.GetSubArray(0, hitsStride).AsReadOnly();
            var isHitAroundListenerCoplanar = isHitCoplanar.GetSubArray(0, hitsStride).AsReadOnly();
            var hitsAroundSources = hits.GetSubArray(hitsStride, hits.Length - hitsStride).AsReadOnly();
            var isHitAroundSourcesCoplanar =
                isHitCoplanar.GetSubArray(hitsStride, isHitCoplanar.Length - hitsStride).AsReadOnly();
            _pathJobHandles[1] = _oneBouncePaths.GetOneBouncePaths(_simulationData.ListenerPosition,
                _simulationData.SourcePositions, hitsAroundListener,
                isHitAroundListenerCoplanar, surroundRaycastHandle,
                _simulationData.OneBouncePaths);
            _pathJobHandles[2] = _twoBouncePaths.GetTwoBounceBaths(_simulationData.ListenerPosition,
                _simulationData.SourcePositions, hitsAroundListener, isHitAroundListenerCoplanar, hitsAroundSources,
                isHitAroundSourcesCoplanar, hitsStride, surroundRaycastHandle, _simulationData.TwoBouncePaths);
            _pathJobHandles[3] = _iterativePaths.GetIterativePaths(_simulationData.ListenerPosition,
                _simulationData.SourcePositions, commandsAroundListener, hitsAroundListener,
                Settings.MaxIterativeBounces,
                surroundRaycastHandle, _simulationData.HigherOrderPaths);
            return JobHandle.CombineDependencies(_pathJobHandles);
        }

        private JobHandle ComputeImpulseResponses(JobHandle pathJob)
        {
            // TODO: Don't hardcode path
            var sofaPath = Path.Combine(Application.streamingAssetsPath, "sofafiles/hrtf0.sofa");
            var hrirs = SofaReader.Read(sofaPath);
            return ComputeImpulseResponse.Schedule(_simulationData.AllAudioPaths, Listener,
                settings.ImpulseResponseSamples, settings.ImpulseResponseSamplesPerSecond, hrirs, pathJob,
                _simulationData.AllImpulseResponses);
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

        private async void OnDestroy()
        {
            try
            {
                await _semaphoreSlim.WaitAsync(timeout: TimeSpan.FromSeconds(5));
            }
            catch (Exception e)
            {
                // Log any exceptions because async void means they disappear otherwise
                Debug.LogError(e);
            }
            finally
            {
                // ...then dispose unmanaged resources
                _semaphoreSlim.Dispose();
                _surroundRaycast.Dispose();
                _simulationData.Dispose();
                _directPaths.Dispose();
                _twoBouncePaths.Dispose();
                _iterativePaths.Dispose();
                _pathJobHandles.Dispose();
            }
        }
    }
}
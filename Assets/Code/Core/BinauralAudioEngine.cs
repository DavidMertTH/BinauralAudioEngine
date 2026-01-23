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
        public BinauralAudioSettings Settings => settings ??= new BinauralAudioSettings();

        // TODO: Remove
        public AudioPath[] AudioPaths;
        public int NumSources => _audioFilters.Count;
        public RaycastHit[] SurroundingHits;
        public int HitsPerOrigin => _surroundRaycast.hitsPerOrigin;
        public bool[] IsCoplanar;
        public bool IsReady { get; private set; }

        private Transform _listener;
        private readonly List<BinauralAudioFilter> _audioFilters = new();
        private readonly GlobalSimulationData _simulationData = new();
        private readonly SurroundRaycast _surroundRaycast = new();
        private readonly DirectPaths _directPaths = new();
        private readonly OneBouncePaths _oneBouncePaths = new();
        private readonly TwoBouncePaths _twoBouncePaths = new();
        private readonly IterativePaths _iterativePaths = new();
        private NativeArray<JobHandle> _pathJobHandles = new(4, Allocator.Persistent);
        private readonly SemaphoreSlim _semaphoreSlim = new(1);
        private readonly CancellationTokenSource _onDestroyCts = new();

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

        private async void Start()
        {
            try
            {
                while (true)
                {
                    await UpdateAllImpulseResponses();
                    AudioPaths = _simulationData.AllAudioPaths.ToArray();
                    SurroundingHits = _surroundRaycast._hits.ToArray();
                    IsCoplanar = _surroundRaycast._coplanarHits._isHitCoplanar.ToArray();
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
        public async Awaitable UpdateAllImpulseResponses(CancellationToken ct = default)
        {
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(ct, _onDestroyCts.Token);
            await _semaphoreSlim.WaitAsync(linkedCts.Token);
            try
            {
                var rayJob = ComputeAudioPaths(linkedCts.Token);
                var impulseResponseJob = ComputeImpulseResponses(rayJob, linkedCts.Token);
                var combinedJob = JobHandle.CombineDependencies(impulseResponseJob, rayJob);
                await combinedJob.ToTask(linkedCts.Token);
            }
            finally
            {
                _semaphoreSlim.Release();
            }
        }

        private JobHandle ComputeAudioPaths(CancellationToken ct)
        {
            var layout = Settings.GetAudioPathArrayLayout(_audioFilters.Count);
            _simulationData.Init(Listener, _audioFilters, layout);
            _pathJobHandles[0] = _directPaths.GetDirectPaths(_simulationData.ListenerPosition,
                _simulationData.SourcePositions, _simulationData.DirectPaths);
            var surroundRaycastHandle = _surroundRaycast.CastRaysAroundOrigins(
                _simulationData.ListenerAndSourcePositions, Settings.RaysAroundListenerAndEachSource, out var hits,
                out var hitsStride, out var isHitCoplanar, out var commands);
            var hitsAroundListener = hits.GetSubArray(0, hitsStride).AsReadOnly();
            var commandsAroundListener = commands.GetSubArray(0, hitsStride).AsReadOnly();
            var isHitAroundListenerCoplanar = isHitCoplanar.GetSubArray(0, hitsStride).AsReadOnly();
            var hitsAroundSources = hits.GetSubArray(hitsStride, hits.Length - hitsStride).AsReadOnly();
            var isHitAroundSourcesCoplanar = isHitCoplanar.GetSubArray(hitsStride, isHitCoplanar.Length - hitsStride).AsReadOnly();
            _pathJobHandles[1] = _oneBouncePaths.GetOneBouncePaths(_simulationData.ListenerPosition,
                _simulationData.SourcePositions, hitsAroundListener,
                isHitAroundListenerCoplanar, surroundRaycastHandle,
                _simulationData.OneBouncePaths);
            _pathJobHandles[2] = _twoBouncePaths.GetTwoBounceBaths(_simulationData.ListenerPosition,
                _simulationData.SourcePositions, hitsAroundListener, isHitAroundListenerCoplanar, hitsAroundSources,
                isHitAroundSourcesCoplanar, hitsStride, surroundRaycastHandle, _simulationData.TwoBouncePaths);
            _pathJobHandles[3] = _iterativePaths.GetIterativePaths(_simulationData.ListenerPosition,
                _simulationData.SourcePositions, commandsAroundListener, hitsAroundListener, Settings.MaxIterativeBounces,
                surroundRaycastHandle, _simulationData.HigherOrderPaths);
            return JobHandle.CombineDependencies(_pathJobHandles);
        }

        private JobHandle ComputeImpulseResponses(JobHandle rayJob, CancellationToken ct)
        {
            return new JobHandle();
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
                // Cancel ongoing processing...
                _onDestroyCts.Cancel();
                await _semaphoreSlim.WaitAsync(millisecondsTimeout: 500);
            }
            catch (Exception e)
            {
                // Log any exceptions because async void means they disappear otherwise
                Debug.LogError(e);
            }
            finally
            {
                // ...then dispose unmanaged resources
                _onDestroyCts.Dispose();
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
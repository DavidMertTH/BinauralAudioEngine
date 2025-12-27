using System;
using System.Collections.Generic;
using System.Threading;
using ArthurKehrwald.Singleton;
using Code.Renderer;
using Code.Simulation;
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
        private readonly SemaphoreSlim _semaphoreSlim = new(1);
        private readonly CancellationTokenSource _onDestroyCts = new();

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
                var rayJob = ComputeAudioRays(linkedCts.Token);
                var impulseResponseJob = ComputeImpulseResponses(rayJob, linkedCts.Token);
                var combinedJob = JobHandle.CombineDependencies(impulseResponseJob, rayJob);
                await combinedJob.ToTask(linkedCts.Token);
            }
            finally
            {
                _semaphoreSlim.Release();
            }
        }

        private JobHandle ComputeAudioRays(CancellationToken ct)
        {
            var layout = Settings.GetAudioPathArrayLayout(_audioFilters.Count);
            _simulationData.Init(Listener, _audioFilters, layout);
            var directPathsHandle = _directPaths.GetDirectPaths(_simulationData.ListenerPosition,
                _simulationData.SourcePositions, _simulationData.DirectPaths);
            var surroundRaycastHandle = _surroundRaycast.CastRaysAroundOrigins(
                _simulationData.ListenerAndSourcePositions, Settings.RaysAroundListenerAndEachSource, out var hits,
                out var hitsStride, out var isHitCoplanar);
            var hitsAroundListener = hits.GetSubArray(0, hitsStride);
            var isHitAroundListenerCoplanar = isHitCoplanar.GetSubArray(0, hitsStride);
            var hitsAroundSources = hits.GetSubArray(hitsStride, hits.Length - hitsStride);
            var isHitAroundSourcesCoplanar = isHitCoplanar.GetSubArray(hitsStride, isHitCoplanar.Length - hitsStride);
            var oneBouncePathsHandle = _oneBouncePaths.GetOneBouncePaths(_simulationData.ListenerPosition,
                _simulationData.SourcePositions, hitsAroundListener.AsReadOnly(),
                isHitAroundListenerCoplanar.AsReadOnly(), surroundRaycastHandle,
                _simulationData.OneBouncePaths);
            var twoBouncePathsHandle = _twoBouncePaths.GetTwoBounceBaths(_simulationData.ListenerPosition,
                _simulationData.SourcePositions, hitsAroundListener, isHitAroundListenerCoplanar, hitsAroundSources,
                isHitAroundSourcesCoplanar, hitsStride, surroundRaycastHandle, _simulationData.TwoBouncePaths);
            return JobHandle.CombineDependencies(directPathsHandle, oneBouncePathsHandle, twoBouncePathsHandle);
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
            }
        }
    }
}
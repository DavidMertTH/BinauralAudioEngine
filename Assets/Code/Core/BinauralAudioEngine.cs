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
        public BinauralAudioSettings Settings => settings;
        private Transform _listener;
        private readonly List<BinauralAudioFilter> _audioFilters = new();
        private readonly GlobalSimulationData _simulationData = new();
        private readonly SurroundRaycast _surroundRaycast = new();
        private readonly DirectPaths _directPaths = new();
        private readonly OneBouncePaths _oneBouncePaths = new();
        private readonly SemaphoreSlim _semaphoreSlim = new(1);
        private readonly CancellationTokenSource _onDestroyCts = new();

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

        /// <summary>
        /// To be called by the GUI whenever the user makes changes to the scene that affect the impulse response.
        /// </summary>
        public async Awaitable UpdateAllImpulseResponses(CancellationToken ct)
        {
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(ct, _onDestroyCts.Token);
            await _semaphoreSlim.WaitAsync(linkedCts.Token);
            try
            {
                var rayJob = ComputeAudioRays(linkedCts.Token);
                var impulseResponseJob = ComputeImpulseResponses(rayJob, linkedCts.Token);
                var combinedJob = JobHandle.CombineDependencies(impulseResponseJob, rayJob);
                while (!combinedJob.IsCompleted)
                    await Awaitable.NextFrameAsync(linkedCts.Token);
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
                _simulationData.ListenerAndSourcePositions,
                layout, out var hits, out var hitsStride, out var isHitCoplanar);
            var oneBouncePathsHandle = _oneBouncePaths.GetOneBouncePaths(_simulationData.ListenerPosition,
                _simulationData.SourcePositions, hits, hitsStride, surroundRaycastHandle,
                _simulationData.OneBouncePaths);
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
            }
        }
    }
}
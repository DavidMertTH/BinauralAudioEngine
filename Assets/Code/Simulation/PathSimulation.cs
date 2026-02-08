using System;
using System.Collections.Generic;
using Code.Core;
using Code.Renderer;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

namespace Code.Simulation
{
    /// <summary>
    /// Simulates the paths of audio 'particles' in the scene.
    /// </summary>
    public class PathSimulation : IDisposable
    {
        /// <summary>
        /// The first element is the listener position, followed by source positions (World space)
        /// </summary>
        public NativeArray<float3>.ReadOnly ListenerAndSourcePositions => _listenerAndSourcePositions.AsReadOnly();

        /// <summary>
        /// World-space positions of all audio sources in the scene
        /// </summary>
        public NativeArray<float3>.ReadOnly SourcePositions =>
            _listenerAndSourcePositions.GetSubArray(1, _listenerAndSourcePositions.Length - 1).AsReadOnly();

        /// <summary>
        /// World-space position of the listener
        /// </summary>
        public float3 ListenerPosition => _listenerAndSourcePositions[0];

        /// <summary>
        /// Array containing direct rays, primary rays, secondary rays, and higher order rays, in that order.
        /// A fixed number of entries is allocated for each type according to the <c>RayCounts</c> struct passed to
        /// <c>Init</c>.
        /// </summary>
        public NativeArray<AudioPath>.ReadOnly AllAudioPaths => _allAudioPaths.AsReadOnly();

        /// <summary>
        /// Sub-array containing direct audio paths between the listener and an audio source
        /// </summary>
        public NativeArray<AudioPath> DirectPaths =>
            _allAudioPaths.GetSubArray(_audioPathArrayLayout.DirectPathsStartIndex,
                _audioPathArrayLayout.NumDirectPaths);

        /// <summary>
        /// Sub-array containing audio paths with one bounce
        /// </summary>
        public NativeArray<AudioPath> OneBouncePaths =>
            _allAudioPaths.GetSubArray(_audioPathArrayLayout.OneBouncePathsStartIndex,
                _audioPathArrayLayout.NumOneBouncePaths);

        /// <summary>
        /// Sub-array containing audio paths with two bounces
        /// </summary>
        public NativeArray<AudioPath> TwoBouncePaths =>
            _allAudioPaths.GetSubArray(_audioPathArrayLayout.TwoBouncesPathsStartIndex,
                _audioPathArrayLayout.NumTwoBouncePaths);

        /// <summary>
        /// Sub-array containing audio paths with more than two bounces
        /// </summary>
        public NativeArray<AudioPath> HigherOrderPaths =>
            _allAudioPaths.GetSubArray(_audioPathArrayLayout.ManyBouncePathsStartIndex,
                _audioPathArrayLayout.NumIterativePaths);


        private SurroundRaycast SurroundRaycast { get; }
        private ComputeDirectPaths ComputeDirectPaths { get; }

        private ComputeOneBouncePaths ComputeOneBouncePaths { get; }

        private ComputeTwoBouncePaths ComputeTwoBouncePaths { get; }

        private ComputeIterativePaths ComputeIterativePaths { get; }


        public JobHandle CombinePathJobHandles(JobHandle direct, JobHandle oneBounce, JobHandle twoBounce,
            JobHandle higherOrder)
        {
            _pathJobHandles[0] = direct;
            _pathJobHandles[1] = oneBounce;
            _pathJobHandles[2] = twoBounce;
            _pathJobHandles[3] = higherOrder;
            return JobHandle.CombineDependencies(_pathJobHandles);
        }

        private readonly AudioPathArrayLayout _audioPathArrayLayout;
        private NativeArray<float3> _listenerAndSourcePositions;
        private NativeArray<AudioPath> _allAudioPaths;
        private NativeArray<JobHandle> _pathJobHandles;
        private readonly int _numRaysAroundListenerAndEachSource;
        private readonly int _maxIterativeBounces;

        public PathSimulation(Transform listener, List<BinauralAudioFilter> sources, BinauralAudioSettings settings)
        {
            _numRaysAroundListenerAndEachSource = settings.RaysAroundListenerAndEachSource;
            _maxIterativeBounces = settings.MaxIterativeBounces;
            var originCount = sources.Count + 1;
            _listenerAndSourcePositions = new NativeArray<float3>(originCount, Allocator.Persistent);
            _listenerAndSourcePositions[0] = listener.position;
            for (var i = 0; i < sources.Count; i++)
                _listenerAndSourcePositions[i + 1] = sources[i].transform.position;
            _audioPathArrayLayout = settings.GetAudioPathArrayLayout(sources.Count);
            _allAudioPaths = new NativeArray<AudioPath>(_audioPathArrayLayout.NumTotalPaths, Allocator.Persistent);
            _pathJobHandles = new NativeArray<JobHandle>(4, Allocator.Persistent);
            ComputeDirectPaths = new ComputeDirectPaths();
            ComputeOneBouncePaths = new ComputeOneBouncePaths();
            ComputeTwoBouncePaths = new ComputeTwoBouncePaths();
            ComputeIterativePaths = new ComputeIterativePaths();
            SurroundRaycast = new SurroundRaycast();
        }

        public JobHandle Schedule(out NativeArray<AudioPath>.ReadOnly paths)
        {
            var directPathsHandle = ComputeDirectPaths.Schedule(ListenerPosition,
                SourcePositions, DirectPaths);
            var surroundRaycastHandle = SurroundRaycast.CastRaysAroundOrigins(
                ListenerAndSourcePositions, _numRaysAroundListenerAndEachSource, out var hits,
                out var hitsStride, out var isHitCoplanar, out var commands);
            var hitsAroundListener = hits.GetSubArray(0, hitsStride).AsReadOnly();
            var commandsAroundListener = commands.GetSubArray(0, hitsStride).AsReadOnly();
            var isHitAroundListenerCoplanar = isHitCoplanar.GetSubArray(0, hitsStride).AsReadOnly();
            var hitsAroundSources = hits.GetSubArray(hitsStride, hits.Length - hitsStride).AsReadOnly();
            var isHitAroundSourcesCoplanar =
                isHitCoplanar.GetSubArray(hitsStride, isHitCoplanar.Length - hitsStride).AsReadOnly();
            var oneBouncePathsHandle = ComputeOneBouncePaths.Schedule(ListenerPosition,
                SourcePositions, hitsAroundListener, isHitAroundListenerCoplanar, surroundRaycastHandle,
                OneBouncePaths);
            var twoBouncePathsHandle = ComputeTwoBouncePaths.Schedule(ListenerPosition,
                SourcePositions, hitsAroundListener, isHitAroundListenerCoplanar, hitsAroundSources,
                isHitAroundSourcesCoplanar, hitsStride, surroundRaycastHandle, TwoBouncePaths);
            var iterativePathsHandle = ComputeIterativePaths.Schedule(ListenerPosition,
                SourcePositions, commandsAroundListener, hitsAroundListener, _maxIterativeBounces,
                surroundRaycastHandle, HigherOrderPaths);
            paths = AllAudioPaths;
            return CombinePathJobHandles(directPathsHandle, oneBouncePathsHandle, twoBouncePathsHandle,
                iterativePathsHandle);
        }

        public void Dispose()
        {
            _listenerAndSourcePositions.Dispose();
            _allAudioPaths.Dispose();
            _pathJobHandles.Dispose();
            ComputeDirectPaths.Dispose();
            ComputeOneBouncePaths.Dispose();
            ComputeTwoBouncePaths.Dispose();
            ComputeIterativePaths.Dispose();
            SurroundRaycast.Dispose();
        }
    }
}
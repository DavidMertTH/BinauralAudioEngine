using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Code.Renderer;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;

namespace Code.Simulation
{
    /// <summary>
    /// Binaural impulse response of an individual audio source and the rays used to calculate it
    /// </summary>
    public class AudioSourceSimulationData : IDisposable
    {
        public NativeArray<AudioPath> audioPaths;
        public NativeArray<float> leftImpulseResponse;
        public NativeArray<float> rightImpulseResponse;

        public void Dispose()
        {
            audioPaths.Dispose();
            leftImpulseResponse.Dispose();
            rightImpulseResponse.Dispose();
        }
    }

    /// <summary>
    /// Burst compatible global scene information for ray casting
    /// </summary>
    public class GlobalSimulationData : IDisposable
    {
        /// <summary>
        /// The first element is the listener position, followed by source positions (World space)
        /// </summary>
        public NativeArray<float3>.ReadOnly ListenerAndSourcePositions => _listenerAndSourcePositions.AsReadOnly();

        /// <summary>
        /// World-space positions of all audio sources in the scene
        /// </summary>
        public NativeArray<float3> SourcePositions =>
            _listenerAndSourcePositions.GetSubArray(1, _listenerAndSourcePositions.Length - 1);

        /// <summary>
        /// World-space position of the listener
        /// </summary>
        public float3 ListenerPosition => _listenerAndSourcePositions[0];

        /// <summary>
        /// Array containing direct rays, primary rays, secondary rays, and higher order rays, in that order.
        /// A fixed number of entries is allocated for each type according to the <c>RayCounts</c> struct passed to
        /// <c>Init</c>.
        /// </summary>
        public NativeArray<AudioPath> AllAudioPaths;

        /// <summary>
        /// Sub-array containing direct audio paths between the listener and an audio source
        /// </summary>
        public NativeArray<AudioPath> DirectPaths =>
            AllAudioPaths.GetSubArray(_audioPathArrayLayout.DirectPathsStartIndex, _audioPathArrayLayout.DirectCount);

        /// <summary>
        /// Sub-array containing audio paths with one bounce
        /// </summary>
        public NativeArray<AudioPath> OneBouncePaths =>
            AllAudioPaths.GetSubArray(_audioPathArrayLayout.OneBouncePathsStartIndex,
                _audioPathArrayLayout.OneBounceCount);

        /// <summary>
        /// Sub-array containing audio paths with two bounces
        /// </summary>
        public NativeArray<AudioPath> TwoBouncePaths =>
            AllAudioPaths.GetSubArray(_audioPathArrayLayout.TwoBouncesPathsStartIndex,
                _audioPathArrayLayout.TwoBouncesCount);

        /// <summary>
        /// Sub-array containing audio paths with more than two bounces
        /// </summary>
        public NativeArray<AudioPath> HigherOrderPaths =>
            AllAudioPaths.GetSubArray(_audioPathArrayLayout.ManyBouncePathsStartIndex,
                _audioPathArrayLayout.ManyBouncesCount);

        private AudioPathArrayLayout _audioPathArrayLayout;
        private NativeArray<float3> _listenerAndSourcePositions;

        /// <summary>
        /// Initialize the simulation data according to settings and scene state. Call only before starting the
        /// simulation, never while simulating.
        /// </summary>
        public void Init(Transform listener, List<BinauralAudioFilter> sources,
            AudioPathArrayLayout layout)
        {
            var originCount = sources.Count + 1;
            _listenerAndSourcePositions = Helper.ReallocateIfNeeded(_listenerAndSourcePositions, originCount,
                Allocator.Persistent);
            _listenerAndSourcePositions[0] = listener.position;
            Parallel.For(0, sources.Count,
                i => { _listenerAndSourcePositions[i + 1] = sources[i].transform.position; });
            _audioPathArrayLayout = layout;
            AllAudioPaths = Helper.ReallocateIfNeeded(AllAudioPaths, layout.TotalCount, Allocator.Persistent);
        }

        public void Dispose()
        {
            _listenerAndSourcePositions.Dispose();
            AllAudioPaths.Dispose();
        }
    }
}
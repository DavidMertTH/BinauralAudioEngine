using System;
using System.Collections.Generic;
using Code.Renderer;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;

namespace Code.Simulation
{
    public class AudioSourceImpulseResponse
    {
        public readonly float[] Left;
        public readonly float[] Right;

        public AudioSourceImpulseResponse(float[] left, float[] right)
        {
            Left = left;
            Right = right;
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

        /// <summary>
        /// Contiguous array of all impulse responses, structured by audio source, side, and time, in that order.
        /// The length is determined by the number of sources and the impulse response length passed to <c>Init</c>.
        /// </summary>
        public NativeArray<float> AllImpulseResponses => _allImpulseResponses;

        public IReadOnlyList<BinauralAudioFilter> Filters => _filters.AsReadOnly();

        public HeadRelatedImpulseResponses Hrirs => _hrirs;

        /// <summary>
        /// Get the left and right impulse responses for a particular source
        /// </summary>
        /// <param name="sourceIndex">The index of the source in the list that was passed to <c>Init</c>.</param>
        /// <returns></returns>
        public AudioSourceImpulseResponse GetImpulseResponse(int sourceIndex)
        {
            var numSources = SourcePositions.Length;
            var stride = _allImpulseResponses.Length / numSources / 2;
            var startIndex = stride * sourceIndex * 2;
            return new AudioSourceImpulseResponse(
                _allImpulseResponses.GetSubArray(startIndex, stride).ToArray(),
                _allImpulseResponses.GetSubArray(startIndex + stride, stride).ToArray());
        }

        private AudioPathArrayLayout _audioPathArrayLayout;
        private NativeArray<float3> _listenerAndSourcePositions;
        private NativeArray<AudioPath> _allAudioPaths;
        private NativeArray<float> _allImpulseResponses;
        private HeadRelatedImpulseResponses _hrirs;
        private List<BinauralAudioFilter> _filters;

        /// <summary>
        /// Initialize the simulation data according to settings and scene state. Call only before starting the
        /// simulation, never while simulating.
        /// </summary>
        public void Init(Transform listener, List<BinauralAudioFilter> sources, AudioPathArrayLayout layout,
            int impulseResponseSamples, string sofaPath)
        {
            _hrirs.Dispose();
            _hrirs = SofaReader.Read(sofaPath);
            _filters = new List<BinauralAudioFilter>(sources);
            var originCount = sources.Count + 1;
            _listenerAndSourcePositions = Helper.ReallocateIfNeeded(_listenerAndSourcePositions, originCount,
                Allocator.Persistent);
            _listenerAndSourcePositions[0] = listener.position;
            for (var i = 0; i < sources.Count; i++)
                _listenerAndSourcePositions[i + 1] = sources[i].transform.position;
            _audioPathArrayLayout = layout;
            _allAudioPaths = Helper.ReallocateIfNeeded(_allAudioPaths, layout.NumTotalPaths, Allocator.Persistent);
            _allImpulseResponses = Helper.ReallocateIfNeeded(_allImpulseResponses,
                desiredSize: impulseResponseSamples * sources.Count * 2, Allocator.Persistent);
        }

        public void Dispose()
        {
            _listenerAndSourcePositions.Dispose();
            _allAudioPaths.Dispose();
            _hrirs.Dispose();
        }
    }
}
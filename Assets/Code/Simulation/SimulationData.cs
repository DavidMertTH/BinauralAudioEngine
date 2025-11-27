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
        /// Sub-array containing direct rays between the listener and an audio source
        /// </summary>
        public NativeArray<AudioPath> DirectPaths => AllAudioPaths.GetSubArray(0, _pathCounts.DirectCount);

        /// <summary>
        /// Sub-array containing primary and secondary image source rays
        /// </summary>
        public NativeArray<AudioPath> ImageSourcePaths =>
            AllAudioPaths.GetSubArray(_pathCounts.DirectCount, _pathCounts.ImageSourceTotalCount);

        /// <summary>
        /// Sub-array containing rays with more than two reflections
        /// </summary>
        public NativeArray<AudioPath> HigherOrderPaths =>
            AllAudioPaths.GetSubArray(_pathCounts.DirectCount + _pathCounts.ImageSourceTotalCount,
                _pathCounts.HigherOrderCount);

        private PathCounts _pathCounts;
        private NativeArray<float3> _listenerAndSourcePositions;

        /// <summary>
        /// Initialize the simulation data according to settings and scene state. Call only before starting the
        /// simulation, never while simulating.
        /// </summary>
        public void Init(Transform listener, List<BinauralAudioFilter> sources,
            PathCounts pathCounts)
        {
            var originCount = sources.Count + 1;
            _listenerAndSourcePositions = Helper.ReallocateIfNeeded(_listenerAndSourcePositions, originCount,
                Allocator.Persistent);
            _listenerAndSourcePositions[0] = listener.position;
            Parallel.For(0, sources.Count,
                i => { _listenerAndSourcePositions[i + 1] = sources[i].transform.position; });
            _pathCounts = pathCounts;
            AllAudioPaths = Helper.ReallocateIfNeeded(AllAudioPaths, pathCounts.TotalCount, Allocator.Persistent);
        }

        public void Dispose()
        {
            _listenerAndSourcePositions.Dispose();
            AllAudioPaths.Dispose();
        }
    }
}
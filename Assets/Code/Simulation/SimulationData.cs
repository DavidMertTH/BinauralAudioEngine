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
        public NativeArray<AudioRay> audioRays;
        public NativeArray<float> leftImpulseResponse;
        public NativeArray<float> rightImpulseResponse;

        public void Dispose()
        {
            audioRays.Dispose();
            leftImpulseResponse.Dispose();
            rightImpulseResponse.Dispose();
        }
    }

    /// <summary>
    /// Burst compatible scene information for ray casting
    /// </summary>
    public class GlobalSimulationData : IDisposable
    {
        /// <summary>
        /// The first element is the listener position, followed by source positions (World space)
        /// </summary>
        private NativeArray<float3> _listenerAndSourcePositions;

        public NativeArray<float3> ListenerAndSourcePositions => _listenerAndSourcePositions;

        public NativeArray<float3> SourcePositions =>
            _listenerAndSourcePositions.GetSubArray(1, _listenerAndSourcePositions.Length - 1);
        
        public float3 ListenerPosition => _listenerAndSourcePositions[0];

        public NativeArray<float3> UpdateListenerAndSourcePositions(Transform listener,
            List<BinauralAudioFilter> sources)
        {
            var originCount = sources.Count + 1;
            _listenerAndSourcePositions = Helper.ReallocateIfNeeded(_listenerAndSourcePositions, originCount,
                Allocator.Persistent);
            _listenerAndSourcePositions[0] = listener.position;
            Parallel.For(0, sources.Count,
                i => { _listenerAndSourcePositions[i + 1] = sources[i].transform.position; });
            return _listenerAndSourcePositions;
        }

        public void Dispose()
        {
            _listenerAndSourcePositions.Dispose();
        }
    }
}
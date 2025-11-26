using System;
using Unity.Collections;

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
}
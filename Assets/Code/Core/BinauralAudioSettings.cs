using System;
using Code.Simulation;
using UnityEngine;
using UnityEngine.Serialization;

namespace Code.Core
{
    [Serializable]
    public class BinauralAudioSettings
    {
        [SerializeField] [Range(0f, 10f)] private float gain = 1f;
        public float Gain => gain;

        [SerializeField] private bool enableHannFiltering;
        public bool EnableHannFiltering => enableHannFiltering;

        [SerializeField] [Range(1f, 100f)] private int raysAroundListenerAndEachSource = 10;

        [SerializeField] [Range(3f, 6f)] private int maxIterativeBounces = 5;

        [SerializeField] [Range(1f, 10f)] private float impulseResponseLengthSeconds = 7f;
        [SerializeField] private int impulseResponseSamplesPerSecond = 1024;

        public int RaysAroundListenerAndEachSource => raysAroundListenerAndEachSource;
        public int MaxIterativeBounces => maxIterativeBounces;
        public float ImpulseResponseLengthSeconds => impulseResponseLengthSeconds;
        public int ImpulseResponseSamplesPerSecond => impulseResponseSamplesPerSecond;
        public int ImpulseResponseSamples => (int) (impulseResponseSamplesPerSecond * impulseResponseLengthSeconds);

        public AudioPathArrayLayout GetAudioPathArrayLayout(int numSources) =>
            new(numSources, raysAroundListenerAndEachSource, RaysAroundListenerAndEachSource, maxIterativeBounces);
    }
}
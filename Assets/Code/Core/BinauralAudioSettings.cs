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

        [SerializeField] [Range(1f, 10000f)] private int raysAroundListenerAndEachSource = 1000;

        [SerializeField] [Range(3f, 6f)] private int maxIterativeBounces = 5;

        public int RaysAroundListenerAndEachSource => raysAroundListenerAndEachSource;
        public int MaxIterativeBounces => maxIterativeBounces;

        public AudioPathArrayLayout GetAudioPathArrayLayout(int numSources) =>
            new(numSources, raysAroundListenerAndEachSource, RaysAroundListenerAndEachSource, maxIterativeBounces);
    }
}
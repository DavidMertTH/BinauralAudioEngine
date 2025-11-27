using System;
using Code.Simulation;
using UnityEngine;

namespace Code.Core
{
    [Serializable]
    public class BinauralAudioSettings
    {
        [SerializeField] [Range(0f, 10f)] private float gain = 1f;
        public float Gain => gain;

        [SerializeField] private bool enableHannFiltering;
        public bool EnableHannFiltering => enableHannFiltering;

        [SerializeField] [Range(100f, 10000f)] private int oneBounceRayCount = 1000;
        [SerializeField] [Range(100f, 10000f)] private int twoBounceRayCount = 1000;
        [SerializeField] [Range(100f, 10000f)] private int manyBounceRayCount = 1000;

        public AudioPathArrayLayout GetAudioPathArrayLayout(int numSources) =>
            new(numSources, oneBounceRayCount, twoBounceRayCount, manyBounceRayCount);
    }
}
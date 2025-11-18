using System;
using UnityEngine;

namespace Code.Core
{
    [Serializable]
    public class BinauralAudioSettings
    {
        [SerializeField] [Range(0f, 10f)] private float gain;
        public float Gain => gain;

        [SerializeField] private bool enableHannFiltering;
        public bool EnableHannFiltering => enableHannFiltering;
    }
}
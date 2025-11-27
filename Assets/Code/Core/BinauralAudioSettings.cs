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

        [SerializeField] [Range(100f, 10000f)] private int rayCount = 1000;

        [Tooltip(
            "How the ray budget that remains after direct rays is distributed between image source and iterative " +
            "ray casting. 0 means all remaining rays are used for image source ray casting; 1 means all remaining " +
            "rays are used for iterative ray casting.")]
        [SerializeField]
        [Range(0f, 1f)]
        private float imageSourceToHigherOrderRayBudgetDistribution = 0.5f;

        public PathCounts GetRayCounts(int numSources) =>
            new(rayCount, numSources, imageSourceToHigherOrderRayBudgetDistribution);
    }
}
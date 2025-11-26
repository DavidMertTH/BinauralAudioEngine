using System;
using UnityEngine;
using static Unity.Mathematics.math;

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

        public RayCounts GetRayCounts(int numSources)
        {
            var total = rayCount;
            var imageSource =
                (int)round((total - numSources) / (1 - imageSourceToHigherOrderRayBudgetDistribution));
            // If x number of rays are cast around each listener and each source, there will be x^2 * numSources
            // secondary and 2x * numSources primary reflections. The sum must equal the total number of image source
            // rays. This results in a quadratic formula with the following solution:
            var x = (int)round(sqrt(1 + imageSource / (float)numSources) - 1);
            // Correct rounding error
            imageSource = square(x) * numSources + 2 * x * numSources;
            var higherOrder = total - imageSource;
            return new RayCounts(total, numSources, imageSource, x, higherOrder);
        }

        public struct RayCounts
        {
            public readonly int Total;
            public readonly int Direct;
            public readonly int ImageSource;
            public readonly int AroundListenerAndSources;
            public readonly int HigherOrder;

            public RayCounts(int total, int direct, int imageSource, int aroundListenerAndSources, int higherOrder) : this()
            {
                Total = total;
                Direct = direct;
                ImageSource = imageSource;
                AroundListenerAndSources = aroundListenerAndSources;
                HigherOrder = higherOrder;
            }
        }
    }
}
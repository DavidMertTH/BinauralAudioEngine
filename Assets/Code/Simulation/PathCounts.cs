using static Unity.Mathematics.math;

namespace Code.Simulation
{
    /// <summary>
    /// Splits the ray budget between various ray casting techniques based on the number of audio sources in the scene
    /// and additional parameters from <c>BinauralAudioSettings</c>.
    /// </summary>
    public readonly struct PathCounts
    {
        public readonly int TotalCount;
        public readonly int DirectCount;
        public readonly int ImageSourceTotalCount;
        public readonly int ImageSourcePrimaryCount;
        public readonly int ImageSourceSecondaryCount;
        public readonly int AroundListenerAndSourcesCount;
        public readonly int HigherOrderCount;

        public int DirectRaysStartIndex => 0;
        public int ImageSourceRaysStartIndex => DirectCount;
        public int HigherOrderStartIndex => ImageSourceRaysStartIndex + ImageSourceTotalCount;

        public PathCounts(int totalCount, int directCount, int imageSourceTotalCount, int imageSourcePrimaryCount,
            int imageSourceSecondaryCount, int aroundListenerAndSourcesCount,
            int higherOrderCount) : this()
        {
            TotalCount = totalCount;
            DirectCount = directCount;
            ImageSourceTotalCount = imageSourceTotalCount;
            ImageSourcePrimaryCount = imageSourcePrimaryCount;
            ImageSourceSecondaryCount = imageSourceSecondaryCount;
            AroundListenerAndSourcesCount = aroundListenerAndSourcesCount;
            HigherOrderCount = higherOrderCount;
        }

        public PathCounts(int totalCount, int numSources, float imageSourceToHigherOrderDistribution)
        {
            TotalCount = totalCount;
            DirectCount = numSources;
            var imageSourceFloat = (totalCount - numSources) / (1 - imageSourceToHigherOrderDistribution);
            // If x is the number of rays are cast around each listener and each source, there will be x^2 * numSources
            // secondary and 2x * numSources primary reflections. The sum must equal the total number of image source
            // rays. This results in a quadratic formula with the following solution:
            var x = (int)round(sqrt(1 + imageSourceFloat / (float)numSources) - 1);
            AroundListenerAndSourcesCount = x;
            // Correct rounding error
            ImageSourceSecondaryCount = square(x) * numSources;
            ImageSourcePrimaryCount = 2 * x * numSources;
            ImageSourceTotalCount = ImageSourceSecondaryCount + ImageSourcePrimaryCount;
            HigherOrderCount = totalCount - ImageSourceTotalCount;
        }
    }
}
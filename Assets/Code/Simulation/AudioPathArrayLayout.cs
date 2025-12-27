namespace Code.Simulation
{
    /// <summary>
    /// Describes the structure of the audio path array based on the number of audio sources in the scene
    /// and additional parameters from <c>BinauralAudioSettings</c>.
    /// </summary>
    public readonly struct AudioPathArrayLayout
    {
        public readonly int TotalCount;
        public readonly int DirectCount;
        public readonly int OneBounceCount;
        public readonly int TwoBouncesCount;
        public readonly int ManyBouncesCount;

        public readonly int DirectPathsStartIndex;
        public readonly int OneBouncePathsStartIndex;
        public readonly int TwoBouncesPathsStartIndex;
        public readonly int ManyBouncePathsStartIndex;

        public AudioPathArrayLayout(int numRaysAroundListener, int numRaysAroundEachSource, int numSources, int numIterativePaths)
        {
            DirectCount = numSources;
            OneBounceCount = numSources * numRaysAroundListener;
            TwoBouncesCount = numSources * numRaysAroundEachSource * numRaysAroundListener;
            ManyBouncesCount = numIterativePaths;
            TotalCount = DirectCount + OneBounceCount + TwoBouncesCount + ManyBouncesCount;
            DirectPathsStartIndex = 0;
            OneBouncePathsStartIndex = DirectCount;
            TwoBouncesPathsStartIndex = OneBouncePathsStartIndex + OneBounceCount;
            ManyBouncePathsStartIndex = TwoBouncesPathsStartIndex + TwoBouncesCount;
        }
    }
}
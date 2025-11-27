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

        public int DirectPathsStartIndex => 0;
        public int OneBouncePathsStartIndex => DirectCount;
        public int TwoBouncesPathsStartIndex => OneBouncePathsStartIndex + OneBounceCount;
        public int ManyBouncePathsStartIndex => TwoBouncesPathsStartIndex + TwoBouncesCount;

        public AudioPathArrayLayout(int numSources, int oneBounceCount, int twoBouncesCount, int manyBouncesCount)
        {
            DirectCount = numSources;
            OneBounceCount = oneBounceCount;
            TwoBouncesCount = twoBouncesCount;
            ManyBouncesCount = manyBouncesCount;
            TotalCount = DirectCount + OneBounceCount + TwoBouncesCount + ManyBouncesCount;
        }
    }
}
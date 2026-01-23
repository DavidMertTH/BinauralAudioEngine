namespace Code.Simulation
{
    /// <summary>
    /// Describes the structure of the audio path array based on the number of audio sources in the scene
    /// and additional parameters from <c>BinauralAudioSettings</c>.
    /// </summary>
    public readonly struct AudioPathArrayLayout
    {
        public readonly int NumTotalPaths;
        public readonly int NumDirectPaths;
        public readonly int NumOneBouncePaths;
        public readonly int NumTwoBouncePaths;
        public readonly int NumIterativePaths;

        public readonly int DirectPathsStartIndex;
        public readonly int OneBouncePathsStartIndex;
        public readonly int TwoBouncesPathsStartIndex;
        public readonly int ManyBouncePathsStartIndex;

        public AudioPathArrayLayout(int numRaysAroundListener, int numRaysAroundEachSource, int numSources, int maxIterativeBounces)
        {
            NumDirectPaths = numSources;
            NumOneBouncePaths = numSources * numRaysAroundListener;
            NumTwoBouncePaths = numSources * numRaysAroundEachSource * numRaysAroundListener;
            NumIterativePaths = numSources * maxIterativeBounces * numRaysAroundListener;
            NumTotalPaths = NumDirectPaths + NumOneBouncePaths + NumTwoBouncePaths + NumIterativePaths;
            DirectPathsStartIndex = 0;
            OneBouncePathsStartIndex = NumDirectPaths;
            TwoBouncesPathsStartIndex = OneBouncePathsStartIndex + NumOneBouncePaths;
            ManyBouncePathsStartIndex = TwoBouncesPathsStartIndex + NumTwoBouncePaths;
        }
    }
}
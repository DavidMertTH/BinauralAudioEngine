using System;
using System.Runtime.InteropServices;

namespace Code
{
    public class ConvolutionReverb
    {
        [DllImport("JuceUnityDLL", CallingConvention = CallingConvention.Cdecl)]
        public static extern void initReverb(double sampleRate, int blockSize, int numChannels);

        [DllImport("JuceUnityDLL", CallingConvention = CallingConvention.Cdecl)]
        public static extern void processReverb(
            float[] input, float[] output);

        [DllImport("JuceUnityDLL", CallingConvention = CallingConvention.Cdecl)]
        public static extern void shutdownReverb();
    }
}
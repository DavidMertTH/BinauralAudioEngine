using System;
using System.Diagnostics;
using System.Numerics;
using System.Threading.Tasks;
using MathNet.Numerics.IntegralTransforms;
using MathNet.Numerics.Providers.FourierTransform;
using UnityEngine;

namespace Code.Renderer
{
    public class LiveConvolutionReverb : MonoBehaviour
    {
        [Range(0, 1)] [SerializeField] public float wet;
        [HideInInspector] public int fullIrLength;

        private float[] _overlapBufferLeft;
        private float[] _overlapBufferRight;

        public enum Side
        {
            Left,
            Right
        }

        private void Awake()
        {
            fullIrLength = 1024 * 2;
            _overlapBufferLeft = new float[fullIrLength * 2];
            _overlapBufferRight = new float[fullIrLength * 2];
        }

        private void Start()
        {
            FourierTransformControl.TryUseNativeMKL();
        }

        public static int GetMaxZweierPotenz(int size)
        {
            int fftLen = 1;
            while (fftLen < size)
                fftLen <<= 1;
            return fftLen;
        }

        public Complex[] ToFreqDomain(float[] inTimeDomain, int length)
        {
            Complex[] complexIr = GetComplex(inTimeDomain, length);
            Fourier.Forward(complexIr, FourierOptions.Matlab);
            return complexIr;
        }


        public float[] ConvolveData(Complex[] irFreqDomain, Complex[] lastIrFreqDomain, Complex[] audioData,
            float[] dry, ref float[] overlapBuffer)
        {
            if (lastIrFreqDomain == null) lastIrFreqDomain = irFreqDomain;
            
            Complex[] oldAudioFolded = new Complex[irFreqDomain.Length];
            Complex[] newAudioFolded = new Complex[irFreqDomain.Length];

            for (int i = 0; i < irFreqDomain.Length; i++)
            {
                newAudioFolded[i] = audioData[i] * irFreqDomain[i];
                oldAudioFolded[i] = audioData[i] * lastIrFreqDomain[i];
            }

            Fourier.Inverse(newAudioFolded, FourierOptions.Matlab);
            Fourier.Inverse(oldAudioFolded, FourierOptions.Matlab);

            overlapBuffer = PullOverlapBuffer(overlapBuffer, dry.Length);
            for (int i = 0; i < newAudioFolded.Length; i++)
            {
                float crossover = i / (float)newAudioFolded.Length;
                float toAddAudioPart = (float)(crossover * (float)newAudioFolded[i].Real +
                                               (1 - crossover) * oldAudioFolded[i].Real);


                overlapBuffer[i] += toAddAudioPart;
            }

            return Mix(dry, overlapBuffer);
        }

        public float[] ProgressiveConvolve(float[] irTimeDomain, float[] audioData,
            float[] dry, Side side, int blockLength)
        {
            if (audioData.Length % blockLength != 0 || irTimeDomain.Length % blockLength != 0)
            {
                print("FAULTY BLOCKLENGTH");
                return null;
            }

            float[] overlapBuffer = Array.Empty<float>();
            Complex[][] lastIr = Array.Empty<Complex[]>();
            if (side == Side.Left)
            {
                overlapBuffer = _overlapBufferLeft;
            }

            if (side == Side.Right)
            {
                overlapBuffer = _overlapBufferRight;
            }

            int paddedLength = blockLength * 2;

            int irBlockAmount = irTimeDomain.Length / blockLength;

            Complex[] audioDataFreq = ToFreqDomain(audioData, paddedLength);

            float[][] irBlocks = new float[irBlockAmount][];
            Complex[][] irFreqBlocks = new Complex[irBlockAmount][];
            Complex[][] audioBlocksConvolvedNew = new Complex[irBlockAmount][];

            Parallel.For(0, irBlockAmount, j =>
            {
                float[] block = new float[paddedLength];
                int offset = j * blockLength;
                for (int i = 0; i < blockLength; i++)
                    block[i] = irTimeDomain[offset + i];
                irBlocks[j] = block;

                irFreqBlocks[j] = ToFreqDomain(block, paddedLength);

                var convNew = ConvolveFreqDomain(irFreqBlocks[j], audioDataFreq);
                Fourier.Inverse(convNew, FourierOptions.Matlab);
                audioBlocksConvolvedNew[j] = convNew;
            });
            Complex[] fullAudioFreqNew = AddOverlappingData(audioBlocksConvolvedNew);
            overlapBuffer = PullOverlapBuffer(overlapBuffer, dry.Length);

            for (int i = 0; i < fullAudioFreqNew.Length; i++)
            {
                overlapBuffer[i] += (float)fullAudioFreqNew[i].Real;
            }

            return Mix(dry, overlapBuffer);
        }

        private Complex[] AddOverlappingData(Complex[][] audioBlocks)
        {
            if (audioBlocks == null || audioBlocks.Length == 0) return null;
            int oneBlockWithoutPadding = audioBlocks[0].Length / 2;
            int fullLength = audioBlocks.Length * oneBlockWithoutPadding + oneBlockWithoutPadding;
            Complex[] result = new Complex[fullLength];

            for (int i = 0; i < audioBlocks.Length; i++)
            {
                int offset = i * oneBlockWithoutPadding;
                for (int j = 0; j < audioBlocks[0].Length; j++)
                {
                    result[offset + j] += audioBlocks[i][j];
                }
            }

            return result;
        }

        private Complex[] ConvolveFreqDomain(Complex[] irFreqDomain, Complex[] audioData)
        {
            Complex[] result = new Complex[irFreqDomain.Length];
            for (int i = 0; i < irFreqDomain.Length; i++)
            {
                result[i] = audioData[i] * irFreqDomain[i];
            }

            return result;
        }

        private float[] PullOverlapBuffer(float[] overlapBuffer, int pullLength)
        {
            for (int i = 0; i < overlapBuffer.Length - pullLength; i++)
            {
                overlapBuffer[i] = overlapBuffer[i + pullLength];
            }

            for (int i = overlapBuffer.Length - pullLength; i < overlapBuffer.Length; i++)
            {
                overlapBuffer[i] = 0;
            }

            return overlapBuffer;
        }

        private float[] Mix(float[] dry, float[] wet)
        {
            float[] result = new float[dry.Length];
            for (int i = 0; i < dry.Length; i++)
            {
                result[i] = dry[i] * (1 - this.wet) + wet[i] * (this.wet);
            }

            return result;
        }

        private Complex[] Normalize(Complex[] complexAudioInput)
        {
            for (int i = 0; i < complexAudioInput.Length; i++)
                complexAudioInput[i] /= 5;

            return complexAudioInput;
        }

        private Complex[] GetComplex(float[] buffer, int requiredLength)
        {
            Complex[] complexAudioData = new Complex[requiredLength];
            for (int i = 0; i < Math.Min(buffer.Length, requiredLength); i++)
            {
                Complex tmp = new Complex(buffer[i], 0);
                complexAudioData[i] = tmp;
            }

            return complexAudioData;
        }
    }
}
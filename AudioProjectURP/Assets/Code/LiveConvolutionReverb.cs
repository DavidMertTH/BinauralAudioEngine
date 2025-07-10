using System;
using System.Diagnostics;
using System.Numerics;
using System.Threading.Tasks;
using MathNet.Numerics.IntegralTransforms;
using MathNet.Numerics.Providers.FourierTransform;
using UnityEngine;

namespace Code
{
    public class LiveConvolutionReverb : MonoBehaviour
    {
        [Range(0, 1)] [SerializeField] public float wet;
        [Range(1, 1024 * 4)] [SerializeField] public int crossoverLength;
        public AudioClip clip;
        [Range(0, 1)] [SerializeField] public float hallReverb;
        [HideInInspector] public int fullIrLength;
        private Complex[] _reverbLayerFreq;
        public static readonly Stopwatch Clock = Stopwatch.StartNew();

        private void Awake()
        {
            fullIrLength = 1024 * 4;
        }

        private void Start()
        {
            FourierTransformControl.TryUseNativeMKL();
            AudioFileLoader audioFileLoader = new AudioFileLoader();
            audioFileLoader.Clip = clip;
            audioFileLoader.LoadClip();
            //float[] samples = SmoothEnds(audioFileLoader.Samples, fullIrLength, 128);
            //_reverbLayerFreq = ToFreqDomain(samples, fullIrLength);
        }

        public float[] SmoothEnds(float[] samples, int chopSize, int smoothRadius)
        {
            float[] choppedSamples = new float[chopSize];
            for (int i = 0; i < chopSize; i++)
            {
                if (i < chopSize - smoothRadius)
                {
                    choppedSamples[i] = samples[i];
                }
                else
                {
                    float koefficent = (i - ((float)chopSize - smoothRadius)) / smoothRadius;
                    choppedSamples[i] = samples[i] * (1 - koefficent);
                }
            }

            return choppedSamples;
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
            float[] dry, ref float[] overlapBuffer, int blockLength)
        {
            
            if (audioData.Length % blockLength != 0 || irTimeDomain.Length % blockLength != 0)
            {
                print("FAULTY BLOCKLENGTH");
                return null;
            }

            int paddedLength = blockLength * 2;
            
            int irBlockAmount = irTimeDomain.Length / blockLength;
            int audioBlockAmount = audioData.Length / blockLength;

            Complex[] audioDataFreq = ToFreqDomain(audioData, paddedLength);
            
            float[][] irBlocks = new float[irBlockAmount][];
            Complex[][] irFreqBlocks = new Complex[irBlockAmount][];
            Complex[][] audioBlocksConvolved = new Complex[irBlockAmount][];
            Parallel.For(0, irBlockAmount, j =>
            {
                float[] block = new float[paddedLength];
                int offset = j * blockLength;
                for (int i = 0; i < blockLength; i++)
                    block[i] = irTimeDomain[offset + i];
                irBlocks[j] = block;

                var irFreq = ToFreqDomain(block, paddedLength);
                irFreqBlocks[j] = irFreq;

                var convFreq = ConvolveFreqDomain(irFreq, audioDataFreq);
                Fourier.Inverse(convFreq, FourierOptions.Matlab);

                audioBlocksConvolved[j] = convFreq;
            });
            Complex[] fullAudioFreq =  AddOverlappingData(audioBlocksConvolved);

            overlapBuffer = PullOverlapBuffer(overlapBuffer, dry.Length);
            for (int i = 0; i < fullAudioFreq.Length; i++)
            {
                overlapBuffer[i] += (float)fullAudioFreq[i].Real;
            }
            return Mix(dry, overlapBuffer);
        }

        private Complex[] AddOverlappingData(Complex[][] audioBlocks)
        {
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
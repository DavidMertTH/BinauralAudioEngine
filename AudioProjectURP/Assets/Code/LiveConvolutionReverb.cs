using System.Collections.Generic;
using System.Linq;
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
        [Range(1, 128)] [SerializeField] public int crossoverLength;

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
        public Complex[] ToFreqDomain(float[] inTimeDomain, int length, int blocksize)
        {
            int blockAmount = blocksize / length;
            
            Complex[] complexIr = GetComplex(inTimeDomain, length);
            Fourier.Forward(complexIr, FourierOptions.Matlab);
            return complexIr;
        }

        public float[] ConvolveData(Complex[] irFreqDomain, Complex[] lastIrFreqDomain, Complex[] audioData,
            float[] dry, ref float[] overlapBuffer)
        {
            
            for (int i = 0; i < irFreqDomain.Length; i++)
            {
                float ratio = (float)i / crossoverLength;
                if (lastIrFreqDomain != null)
                {
                    if (ratio <= 1)
                    {
                        Complex oldIrPart = (1 - ratio) * audioData[i] * lastIrFreqDomain[i];
                        Complex newIrPart = (ratio) * audioData[i] * irFreqDomain[i];
                        audioData[i] = newIrPart + oldIrPart;
                    }
                    else
                    {
                        audioData[i] *= irFreqDomain[i];
                    }
                }
            }

            Fourier.Inverse(audioData, FourierOptions.Matlab);

            audioData = Normalize(audioData);
            float[] resultingSamples = new float[audioData.Length];

            overlapBuffer = PullOverlapBuffer(overlapBuffer, dry.Length);
            for (int i = 0; i < resultingSamples.Length; i++)
            {
                overlapBuffer[i] += (float)audioData[i].Real;
            }

            return Mix(dry, overlapBuffer);
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
            for (int i = 0; i < buffer.Length; i++)
            {
                Complex tmp = new Complex(buffer[i], 0);
                complexAudioData[i] = tmp;
            }

            return complexAudioData;
        }
    }
}
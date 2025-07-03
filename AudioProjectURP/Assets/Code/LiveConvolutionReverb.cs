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

        private float[] CutIR(int length, float[] ir)
        {
            float[] halfIr = new float[length];
            for (int i = 0; i < halfIr.Length; i++)
            {
                halfIr[i] = ir[i];
            }

            return halfIr;
        }

        float GetRMS(float[] ir)
        {
            float sum = 0f;
            foreach (var sample in ir) sum += sample * sample;
            return Mathf.Sqrt(sum / ir.Length);
        }

        public Complex[] ToFreqDomain(float[] inTimeDomain, int length)
        {
    /*        float preRms = GetRMS(inTimeDomain);
            for (int i = 0; i < previousInTimeDomain.Count; i++)
            {
                float[] prev = previousInTimeDomain[i];
                for (int j = 0; j < inTimeDomain.Length; j++)
                {
                    inTimeDomain[j] += prev[j];
                }
            }

            float postRms = GetRMS(inTimeDomain);
            float ampFaktor = preRms / postRms;
            for (int i = 0; i < inTimeDomain.Length; i++)
            {
                inTimeDomain[i] /= previousInTimeDomain.Count;
                inTimeDomain[i] *= ampFaktor;
            }*/
            Complex[] complexIr = GetComplex(inTimeDomain, length);
            Fourier.Forward(complexIr, FourierOptions.Matlab);
            return complexIr;
        }
        

        public float[] ConvolveData(Complex[] irFreqDomain, float[] audio, ref float[] overlapBuffer)
        {
            int requiredLength = irFreqDomain.Length;
            if (overlapBuffer == null) overlapBuffer = new float[requiredLength];
            Complex[] complexAudioInput = GetComplex(audio, requiredLength);

            Fourier.Forward(complexAudioInput, FourierOptions.Matlab);

            for (int i = 0; i < irFreqDomain.Length; i++)
            {
                complexAudioInput[i] *= irFreqDomain[i];
            }

            Fourier.Inverse(complexAudioInput, FourierOptions.Matlab);

            complexAudioInput = Normalize(complexAudioInput);
            float[] resultingSamples = new float[complexAudioInput.Length];

            overlapBuffer = PullOverlapBuffer(overlapBuffer, audio.Length);
            for (int i = 0; i < resultingSamples.Length; i++)
            {
                overlapBuffer[i] += (float)complexAudioInput[i].Real;
            }

            return Mix(audio, overlapBuffer);
        }
        public float[] ConvolveData(Complex[] irFreqDomain, Complex[] audioData, float[] dry, ref float[] overlapBuffer)
        {
            if (overlapBuffer == null) overlapBuffer = new float[audioData.Length];
            
            for (int i = 0; i < irFreqDomain.Length; i++)
            {
                audioData[i] *= irFreqDomain[i];
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
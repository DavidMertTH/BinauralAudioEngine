using System.Numerics;
using MathNet.Numerics.IntegralTransforms;
using MathNet.Numerics.Providers.FourierTransform;
using UnityEngine;

public class LiveConvolutionReverb : MonoBehaviour
{
    [Range(0, 1)] [SerializeField] public float wet;

    private void Start()
    {
        FourierTransformControl.TryUseNativeMKL();
    }

    private int GetMaxZweierPotenz(int size)
    {
        int fftLen = 1;
        while (fftLen < size)
            fftLen <<= 1;
        return fftLen;
    }

    private float[] CutIR(int length , float[] ir)
    {
        float[] halfIr = new float[length];
        for (int i = 0; i < halfIr.Length; i++)
        {
            halfIr[i] = ir[i];
        }

        return halfIr;
    }

    public float[] ConvolveData(float[] ir, float[] audio,ref float[] overlapBuffer)
    {
        int lengthSum = audio.Length + ir.Length;
        int requiredLength = GetMaxZweierPotenz(lengthSum);
        if (overlapBuffer == null)
        {
            overlapBuffer = new float[requiredLength];
        }

        Complex[] complexAudioInput = GetComplex(audio, requiredLength);

        Fourier.Forward(complexAudioInput, FourierOptions.Matlab);
        
        Complex[] complexIrInput = GetComplex(ir, requiredLength);
        Fourier.Forward(complexIrInput, FourierOptions.Matlab);
        
        for (int i = 0; i < complexIrInput.Length; i++)
        {
            complexAudioInput[i] *= complexIrInput[i];
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

    private float[] PullOverlapBuffer(float[] overlapBuffer,int pullLength)
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
            result[i] = dry[i] * (1 - this.wet)+ wet[i] * (this.wet);
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
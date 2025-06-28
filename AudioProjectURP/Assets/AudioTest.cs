using System;
using System.Runtime.InteropServices;
using Code;
using UnityEngine;
using UnityEngine.Serialization;

public class ConvolutionReverbNative : MonoBehaviour
{
    [DllImport("JuceUnityDLL", CallingConvention = CallingConvention.Cdecl)]
    static extern IntPtr createConvolver(
        float[] irData, UIntPtr irLength, double sampleRate, UIntPtr blockSize);

    [DllImport("JuceUnityDLL", CallingConvention = CallingConvention.Cdecl)]
    static extern void processConvolver(
        IntPtr convolverPtr, float[] input, float[] output);

    [DllImport("JuceUnityDLL", CallingConvention = CallingConvention.Cdecl)]
    static extern void destroyConvolver(IntPtr convolverPtr);

    [Header("Mono- oder Stereo-IR (Stereo: nur der linke Kanal wird genutzt)")]
    public AudioClip impulseResponseClip;

    public bool bypass;
    public BinauralAudioProcessor processor;
    
    private IntPtr _convLeft;
    private IntPtr _convRight;
    private int _blockSize;
    private float[] _irMono;
    private float[] _inBuf, _outBuf;

    void Start()
    {
        // 1) DSP-Blockgröße ermitteln (z.B. 1024)
        AudioSettings.GetDSPBufferSize(out _blockSize, out _);

        // 2) IR aus AudioClip holen
        int irChannels = impulseResponseClip.channels;
        int irSamples  = impulseResponseClip.samples;
        processor.CreatePrimitiveImpulseresponse();
        float[] ir = processor._impulseResponseLeft;

        // 3) Mono-IR extrahieren (Linker Kanal)
        _irMono = new float[irSamples];
        
        // 4) Convolver für beide Kanäle anlegen
        _convLeft  = createConvolver(ir, (UIntPtr)ir.Length,
                                     AudioSettings.outputSampleRate, (UIntPtr)_blockSize);
        _convRight = createConvolver(ir, (UIntPtr)ir.Length,
                                     AudioSettings.outputSampleRate, (UIntPtr)_blockSize);

        // 5) Buffers anlegen
        _inBuf  = new float[_blockSize];
        _outBuf = new float[_blockSize];
    }

    void OnAudioFilterRead(float[] data, int channels)
    {
        if(bypass)return;
       

        int frames = data.Length / channels;
        int count  = Math.Min(frames, _blockSize);

        // Stereo: zwei Kanäle getrennt verarbeiten
        for (int ch = 0; ch < channels; ++ch)
        {
            // Input spliten
            for (int i = 0; i < count; ++i)
                _inBuf[i] = data[i * channels + ch];

            // auf den richtigen Convolver callen
            IntPtr ctx = (ch == 0 ? _convLeft : _convRight);
            processConvolver(ctx, _inBuf, _outBuf);

            // Output mergen
            for (int i = 0; i < count; ++i)
                data[i * channels + ch] = _outBuf[i];
        }
    }

    void OnDestroy()
    {
        if (_convLeft != IntPtr.Zero)
            destroyConvolver(_convLeft);
        if (_convRight != IntPtr.Zero)
            destroyConvolver(_convRight);

        _convLeft  = IntPtr.Zero;
        _convRight = IntPtr.Zero;
    }
}

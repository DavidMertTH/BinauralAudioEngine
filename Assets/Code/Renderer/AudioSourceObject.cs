using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Threading.Tasks;
using Code.Core;
using Code.Simulation;
using MathNet.Numerics;
using MathNet.Numerics.IntegralTransforms;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Serialization;
using Vector3 = UnityEngine.Vector3;

namespace Code.Renderer
{
    public class AudioSourceObject : MonoBehaviour
    {
        public int audioChunkAmount;
        public bool reloadIr;
        public int irLenght = 1024 * 7;
        public string path;
        public bool openFile = false;
        public float volume;
        public bool isRunning;
        [HideInInspector] public float[] audioTrack;
        [HideInInspector] public AudioSource audioSource;

        private float[][] _audioChunks;
        private Complex[][] _spectralAudio;
        private float[] audioLeft;
        private float[] audioRight;
        
        private Complex[] _spectralIrLeft;
        private Complex[] _spectralIrRight;
        private int _dspBufferLength;
        public int _sampleRate;
        private int _fullBlockLength;
        public int currentPlayBackHead;
        private IEnumerator _convolutionCoroutine;
        public bool _coroutineRunning;
        private float[] _irLeft;
        private float[] _irRight;
        private bool _updateIrNextFrame;

        private void Start()
        {
            audioSource = GetComponent<AudioSource>();
            if (audioSource == null) audioSource = gameObject.AddComponent<AudioSource>();
            InitData(irLenght);
            audioSource.loop = true;
            audioSource.Play();
            CreateOfflineAudioBuffer();
            // CreateOfflineIrBuffer();
        }

        private void Update()
        {
            if (openFile)
            {
                openFile = false;
                LoadAudioTrackFromSource();
            }

            if (reloadIr)
            {
                var rays = BinauralAudioEngine.Instance.AudioPaths.ToList();
                var listener = BinauralAudioEngine.Instance.Listener;
                reloadIr = false;
                Debug.Log("found " + rays.Count + " Rays");
                (float[], float[]) ir = RaysToIr.CreateBrirLeftAndRight(rays, irLenght,
                    listener.gameObject, _sampleRate,
                    1);
                EnterNewIr(ir.Item1, ir.Item2);
            }

            if (_updateIrNextFrame)
            {
                if (!_coroutineRunning)
                {
                    _updateIrNextFrame = false;
                    
                    StartCoroutine(CreateConvolvedAudioBufferCoroutine(_irLeft, _irRight));
                }
            }
        }

        private void InitData(int irLength)
        {
            int dspNumBuffers;

            AudioSettings.GetDSPBufferSize(out _dspBufferLength, out dspNumBuffers);
            int channel = 2;

            _fullBlockLength = GetMaxZweierPotenz(_dspBufferLength + irLength - 1);
        }

        public void EnterNewIr(float[] irLeft, float[] irRight)
        {
            _irLeft = irLeft;
            _irRight = irRight;
            _updateIrNextFrame = true;
        }

        private IEnumerator CreateConvolvedAudioBufferCoroutine(float[] irLeft, float[] irRight)
        {
            _coroutineRunning = true;
            int chunkCount = audioChunkAmount;
            int fullLen = _fullBlockLength;
            int dspLen = _dspBufferLength;
            int audioLen = audioSource.clip.samples;
            int irLen = irLeft.Length;
            int convLen = dspLen + irLen - 1;

            Complex[][] spectralAudio = _spectralAudio;

            int startHead = currentPlayBackHead;

            audioLeft = new float[audioLen + irLen - 1];
            audioRight = new float[audioLen + irLen - 1];

            var task = Task.Run(() =>
            {
                Complex[] irSpecL = ToFreqDomain(irLeft, fullLen);
                Complex[] irSpecR = ToFreqDomain(irRight, fullLen);

                int stripeCount = Environment.ProcessorCount * 4;
                object[] locks = new object[stripeCount];
                for (int i = 0; i < stripeCount; i++)
                    locks[i] = new object();

                Parallel.For(0, chunkCount, k =>
                {
                    int block = (startHead + k) % chunkCount;

                    Complex[] src = spectralAudio[block];

                    Complex[] tempL = new Complex[fullLen];
                    Complex[] tempR = new Complex[fullLen];

                    for (int j = 0; j < fullLen; j++)
                    {
                        tempL[j] = irSpecL[j] * src[j];
                        tempR[j] = irSpecR[j] * src[j];
                    }

                    Fourier.Inverse(tempL, FourierOptions.Matlab);
                    Fourier.Inverse(tempR, FourierOptions.Matlab);

                    int baseIndex = block * dspLen;

                    for (int n = 0; n < convLen; n++)
                    {
                        int idx = baseIndex + n;
                        if (idx >= audioLeft.Length)
                            break;

                        float l = (float)tempL[n].Real;
                        float r = (float)tempR[n].Real;

                        int stripe = idx % stripeCount;
                        lock (locks[stripe])
                        {
                            audioLeft[idx] += l;
                            audioRight[idx] += r;
                        }
                    }
                });
            });


            while (!task.IsCompleted)
                yield return null;
            _coroutineRunning = false;
            if (task.Exception != null)
                Debug.LogError(task.Exception);

            Debug.Log($"Convolution ready (started at chunk {startHead})");
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

        public void CreateOfflineAudioBuffer()
        {
            _sampleRate = audioSource.clip.frequency;
            float[] stereoBuffer = new float[audioSource.clip.samples * audioSource.clip.channels];
            audioSource.clip.GetData(stereoBuffer, 0);

            float[] monoBuffer = new float[audioSource.clip.samples];

            for (int i = 0; i < monoBuffer.Length; i++)
            {
                monoBuffer[i] = stereoBuffer[i * 2] + stereoBuffer[i * 2 + 1];
            }

            int amountAudioBuffer = monoBuffer.Length / _dspBufferLength;

            float[][] segmentedMonoBuffer = new float[amountAudioBuffer][];
            _spectralAudio = new Complex[amountAudioBuffer][];

            //ZeroBuffering Audio
            for (int x = 0; x < amountAudioBuffer; x++)
            {
                segmentedMonoBuffer[x] = new float[_dspBufferLength];
                for (int y = 0; y < _dspBufferLength; y++)
                {
                    int currentPosition = (x * _dspBufferLength + y);
                    if (monoBuffer.Length < currentPosition) continue;

                    segmentedMonoBuffer[x][y] = monoBuffer[currentPosition];
                }
            }

            StartCoroutine(AudioToSpectrum(segmentedMonoBuffer, _fullBlockLength));

            _audioChunks = segmentedMonoBuffer;
            audioChunkAmount = amountAudioBuffer;


            Debug.Log("monoBuffer.Length: " + monoBuffer.Length);
            Debug.Log("dspBufferLength: " + _dspBufferLength);
            Debug.Log("fullLength: " + _fullBlockLength);
        }

        private IEnumerator AudioToSpectrum(float[][] audioData, int fullLength)
        {
            int chunkCount = audioData.Length;

            Debug.Log("Start FFT Parallel Conversion...");

            var task = Task.Run(() =>
            {
                Parallel.For(0, chunkCount, i => { _spectralAudio[i] = ToFreqDomain(audioData[i], fullLength); });
            });

            while (!task.IsCompleted)
                yield return null;

            if (task.Exception != null)
            {
                Debug.LogError(task.Exception);
                yield break;
            }

            Debug.Log("Fertig (FFT parallel)");
        }

        public int GetMaxZweierPotenz(int size)
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

        public string LoadAudioTrackFromSource()
        {
            string path = EditorUtility.OpenFilePanel("Wähle eine Datei", "", "wav");
            if (!string.IsNullOrEmpty(path))
            {
                Debug.Log("Ausgewählte Datei: " + path);
                audioTrack = WaveFileImporter.ReadWavFile(path);
                int channels = 2;
                AudioClip clip = AudioClip.Create("ImportedClip", audioTrack.Length / channels, channels, _sampleRate,
                    false);
                clip.SetData(audioTrack, 0);
                audioSource.clip = clip;
                Debug.Log($"AudioClip geladen: {clip.samples} Samples, {clip.channels} Kanäle, {clip.frequency} Hz");
                audioSource.Play();
                CreateOfflineAudioBuffer();
            }

            this.path = path;
            return path;
        }

        private void OnAudioFilterRead(float[] data, int channels)
        {
           if (audioLeft == null || audioRight == null || !isRunning)
            {
                for (int i = 0; i < data.Length; i++)
                {
                    data[i] = 0;
                }

                return;
            }

            int bufferCounter;
            for (int i = 0; i < data.Length; i++)
            {
                bufferCounter = currentPlayBackHead * _dspBufferLength + (i / 2);

                data[i] = i % 2 == 0 ? audioRight[bufferCounter] : audioLeft[bufferCounter];
                // data[i * 2] = audioLeft[_currentPlayBackHead * _dspBufferLength + i];
                // data[i * 2 + 1] = audioRight[_currentPlayBackHead * _dspBufferLength + i];
                data[i] *= volume;
            }

            currentPlayBackHead++;
            if (audioChunkAmount <= currentPlayBackHead) currentPlayBackHead = 0;
        }
    }
}
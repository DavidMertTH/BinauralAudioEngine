using System;
using System.Collections;
using System.Collections.Generic;
using System.Numerics;
using System.Threading.Tasks;
using Code.Simulation;
using MathNet.Numerics.IntegralTransforms;
using UnityEditor;
using UnityEngine;

namespace Code.Renderer
{
    [RequireComponent(typeof(BinauralAudioFilter))]
    public class AudioSourceObject : MonoBehaviour
    {
        public int audioChunkAmount;
        public bool reloadIr;
        public int irLength = 1024 * 7;
        public string path;
        public bool openFile;
        public bool isRunning;
        public bool coroutineRunning;
        public int sampleRate;
        public RaysVisualizer raysVisualizer;
        [HideInInspector] public float[] audioTrack;
        [HideInInspector] public AudioSource audioSource;
        public List<AudioPath> AudioPaths;

        private Complex[][] _spectralAudio;
        private float[] _audioLeft;
        private float[] _audioRight;
        private Complex[] _spectralIrLeft;
        private Complex[] _spectralIrRight;
        private int _dspBufferLength;
        private int _fullBlockLength;
        private IEnumerator _convolutionCoroutine;
        private float[] _irLeft;
        private float[] _irRight;
        private bool _updateIrNextFrame;


        private void Start()
        {
            audioSource = GetComponent<AudioSource>();
            if (audioSource == null) audioSource = gameObject.AddComponent<AudioSource>();
            InitData(irLength);
            audioSource.loop = true;
            audioSource.Play();
            CreateOfflineAudioBuffer();
        }

        private static List<AudioPath> GetValidPaths(List<AudioPath> unfilteredPaths)
        {
            var validPaths = new List<AudioPath>();
            for (var i = 0; i < unfilteredPaths.Count; i++)
                if (unfilteredPaths[i].IsValid)
                    validPaths.Add(unfilteredPaths[i]);

            return validPaths;
        }

        private void UpdateVisualization()
        {
            raysVisualizer.EnterNewRays(GetValidPaths(AudioPaths), gameObject);
        }

        private void Update()
        {
            if (openFile)
            {
                openFile = false;
                LoadAudioTrackFromSource();
            }

            if (_updateIrNextFrame)
                if (!coroutineRunning)
                {
                    _updateIrNextFrame = false;

                    StartCoroutine(CreateConvolvedAudioBufferCoroutine(_irLeft, _irRight));
                }
        }

        private void InitData(int irLength)
        {
            AudioSettings.GetDSPBufferSize(out _dspBufferLength, out _);
            _fullBlockLength = GetMaxZweierPotenz(_dspBufferLength + irLength - 1);
        }

        public void EnterNewIr(AudioSourceImpulseResponse ir)
        {
            _irLeft = ir.Left;
            _irRight = ir.Right;
            _updateIrNextFrame = true;
        }

        private IEnumerator CreateConvolvedAudioBufferCoroutine(float[] irLeft, float[] irRight)
        {
            coroutineRunning = true;
            var chunkCount = audioChunkAmount;
            var fullLen = _fullBlockLength;
            var dspLen = _dspBufferLength;
            var audioLen = audioSource.clip.samples;
            var irLen = irLeft.Length;
            var convLen = dspLen + irLen - 1;

            var spectralAudio = _spectralAudio;
            _audioLeft = new float[audioLen + irLen - 1];
            _audioRight = new float[audioLen + irLen - 1];

            var task = Task.Run(() =>
            {
                var irSpecL = ToFreqDomain(irLeft, fullLen);
                var irSpecR = ToFreqDomain(irRight, fullLen);

                var stripeCount = Environment.ProcessorCount * 4;
                var locks = new object[stripeCount];
                for (var i = 0; i < stripeCount; i++)
                    locks[i] = new object();

                Parallel.For(0, chunkCount, k =>
                {
                    var block = k % chunkCount;

                    var src = spectralAudio[block];

                    var tempL = new Complex[fullLen];
                    var tempR = new Complex[fullLen];

                    for (var j = 0; j < fullLen; j++)
                    {
                        tempL[j] = irSpecL[j] * src[j];
                        tempR[j] = irSpecR[j] * src[j];
                    }

                    Fourier.Inverse(tempL, FourierOptions.Matlab);
                    Fourier.Inverse(tempR, FourierOptions.Matlab);

                    var baseIndex = block * dspLen;

                    for (var n = 0; n < convLen; n++)
                    {
                        var idx = baseIndex + n;
                        if (idx >= _audioLeft.Length)
                            break;

                        var l = (float)tempL[n].Real;
                        var r = (float)tempR[n].Real;

                        var stripe = idx % stripeCount;
                        lock (locks[stripe])
                        {
                            _audioLeft[idx] += l;
                            _audioRight[idx] += r;
                        }
                    }
                });
            });


            while (!task.IsCompleted)
                yield return null;
            
            if (TryGetComponent<BinauralAudioFilter>(out var filter))
                filter.SetAudio( _audioLeft, _audioRight);
            coroutineRunning = false;
            if (task.Exception != null)
                Debug.LogError(task.Exception);

            Debug.Log($"Convolution ready.");
        }


        private void CreateOfflineAudioBuffer()
        {
            sampleRate = audioSource.clip.frequency;
            var stereoBuffer = new float[audioSource.clip.samples * audioSource.clip.channels];
            audioSource.clip.GetData(stereoBuffer, 0);

            var monoBuffer = new float[audioSource.clip.samples];

            for (var i = 0; i < monoBuffer.Length; i++) monoBuffer[i] = stereoBuffer[i * 2] + stereoBuffer[i * 2 + 1];

            var amountAudioBuffer = monoBuffer.Length / _dspBufferLength;

            var segmentedMonoBuffer = new float[amountAudioBuffer][];
            _spectralAudio = new Complex[amountAudioBuffer][];

            // ZeroBuffering Audio
            for (var x = 0; x < amountAudioBuffer; x++)
            {
                segmentedMonoBuffer[x] = new float[_dspBufferLength];
                for (var y = 0; y < _dspBufferLength; y++)
                {
                    var currentPosition = x * _dspBufferLength + y;
                    segmentedMonoBuffer[x][y] = monoBuffer[currentPosition];
                }
            }

            StartCoroutine(AudioToSpectrum(segmentedMonoBuffer, _fullBlockLength));

            audioChunkAmount = amountAudioBuffer;


            Debug.Log("monoBuffer.Length: " + monoBuffer.Length);
            Debug.Log("dspBufferLength: " + _dspBufferLength);
            Debug.Log("fullLength: " + _fullBlockLength);
        }

        private IEnumerator AudioToSpectrum(float[][] audioData, int fullLength)
        {
            var chunkCount = audioData.Length;

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

        private int GetMaxZweierPotenz(int size)
        {
            var fftLen = 1;
            while (fftLen < size)
                fftLen <<= 1;
            return fftLen;
        }

        private Complex[] ToFreqDomain(float[] inTimeDomain, int length)
        {
            var complexIr = GetComplex(inTimeDomain, length);
            Fourier.Forward(complexIr, FourierOptions.Matlab);
            return complexIr;
        }

        private Complex[] GetComplex(float[] buffer, int requiredLength)
        {
            var complexAudioData = new Complex[requiredLength];
            for (var i = 0; i < Math.Min(buffer.Length, requiredLength); i++)
            {
                var tmp = new Complex(buffer[i], 0);
                complexAudioData[i] = tmp;
            }

            return complexAudioData;
        }

        public string LoadAudioTrackFromSource()
        {
            var chosenPath = EditorUtility.OpenFilePanel("Wähle eine Datei", "", "wav");
            if (!string.IsNullOrEmpty(chosenPath))
            {
                Debug.Log("Ausgewählte Datei: " + chosenPath);
                audioTrack = WaveFileImporter.ReadWavFile(chosenPath);
                var channels = 2;
                var clip = AudioClip.Create("ImportedClip", audioTrack.Length / channels, channels, sampleRate,
                    false);
                clip.SetData(audioTrack, 0);
                audioSource.clip = clip;
                Debug.Log($"AudioClip geladen: {clip.samples} Samples, {clip.channels} Kanäle, {clip.frequency} Hz");
                audioSource.Play();
                CreateOfflineAudioBuffer();
            }

            path = chosenPath;
            return chosenPath;
        }
    }
}
using System;
using System.Collections;
using System.Collections.Generic;
using System.Numerics;
using System.Threading.Tasks;
using Code.Preprocessing;
using Code.Simulation;
using MathNet.Numerics.IntegralTransforms;
using Unity.Mathematics;
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
        public BinauralAudioFilter audioFilter;
        public Color color;
        
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
            audioFilter = GetComponent<BinauralAudioFilter>();
            color = SourceManager.NextColor();
            GetComponent<UnityEngine.Renderer>().material.color = color;
            SourceManager.Instance.Register(this);
        }

        private static List<AudioPath> GetValidPaths(List<AudioPath> unfilteredPaths)
        {
            var validPaths = new List<AudioPath>();
            for (var i = 0; i < unfilteredPaths.Count; i++)
                if (unfilteredPaths[i].IsValid)
                    validPaths.Add(unfilteredPaths[i]);

            return validPaths;
        }
        

        private void Update()
        {
            if (openFile)
            {
                openFile = false;
                LoadAudioTrackFromSource();
            }

            if (_updateIrNextFrame)
                if (!coroutineRunning && _spectralAudio != null)
                {
                    _updateIrNextFrame = false;

                    StartCoroutine(CreateConvolvedAudioBufferCoroutine(_irLeft, _irRight));
                }
        }

        private void InitData(int irLength)
        {
            AudioSettings.GetDSPBufferSize(out _dspBufferLength, out _);
            _fullBlockLength = CalcBlockSize(_dspBufferLength, irLength);
        }

        public void EnterNewIr(AudioSourceImpulseResponse ir)
        {
            _irLeft = ir.Left;
            _irRight = ir.Right;
            _updateIrNextFrame = true;
        }

        private IEnumerator CreateConvolvedAudioBufferCoroutine(float[] irLeft, float[] irRight)
        {
            var stopwatch = new System.Diagnostics.Stopwatch();
            stopwatch.Start();
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

            stopwatch.Stop();
            Debug.Log($"Convolution ready. Took {stopwatch.ElapsedMilliseconds} ms");
        }

        private void OnDestroy()
        {
            SourceManager.Instance.DeRegister(this);
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
        
        private static int CalcBlockSize(int dspBufferSize, int impulseResponseNumSamples)
        {
            var size = dspBufferSize + impulseResponseNumSamples - 1;
            return math.ceilpow2(size);
        }

        public string LoadAudioTrackFromSource()
        {
            var chosenPath = EditorUtility.OpenFilePanel("Wähle eine Datei", "", "wav");
            if (!string.IsNullOrEmpty(chosenPath))
            {
                Debug.Log("Ausgewählte Datei: " + chosenPath);
                var sw = System.Diagnostics.Stopwatch.StartNew();
                using var audioPreprocessor = new AudioPreprocessor(chosenPath, _dspBufferLength, _fullBlockLength);
                audioPreprocessor.Schedule(out var spectralAudioNative).Complete();
                var numBlocks = spectralAudioNative.Length / _fullBlockLength;
                _spectralAudio = new Complex[numBlocks][];
                for (var i = 0; i < numBlocks; i++)
                {
                    _spectralAudio[i] = new Complex[_fullBlockLength];
                    spectralAudioNative.GetSubArray(i * _fullBlockLength, _fullBlockLength).CopyTo(_spectralAudio[i]);
                }
                audioChunkAmount = numBlocks;

                sw.Stop();
                Debug.Log($"Audio preprocessing took {sw.ElapsedMilliseconds} ms");
            }

            path = chosenPath;
            return chosenPath;
        }
    }
}
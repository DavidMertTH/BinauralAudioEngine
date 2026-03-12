using System;
using System.Collections;
using System.Collections.Generic;
using System.Numerics;
using System.Threading.Tasks;
using Code.Core;
using Code.Preprocessing;
using Code.Simulation;
using MathNet.Numerics.IntegralTransforms;
using SFB;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;
using UnityEngine.Serialization;

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
        public bool coroutineRunning;
        public int sampleRate;
        public RaysVisualizer raysVisualizer;
        [HideInInspector] public float[] audioTrack;
        [HideInInspector] public AudioSource audioSource;
        public List<AudioPath> AudioPaths;
        public BinauralAudioFilter AudioFilter { get; private set; }
        public Color color;
        public float[] irLeft;
        public float[] irRight;

        private Complex[][] _spectralAudio;
        private float[] _audioLeft;
        private float[] _audioRight;
        private Complex[] _spectralIrLeft;
        private Complex[] _spectralIrRight;
        private int _dspBufferLength;
        private int _fullBlockLength;
        private IEnumerator _convolutionCoroutine;
        private bool _updateIrNextFrame;
        private int _numSamples;


        private void Awake()
        {
            AudioFilter = GetComponent<BinauralAudioFilter>();
        }

        private void Start()
        {
            audioSource = GetComponent<AudioSource>();
            if (audioSource == null) audioSource = gameObject.AddComponent<AudioSource>();
            InitData(irLength);
            audioSource.loop = true;
            audioSource.Play();
            color = SourceManager.NextColor();
            GetComponent<UnityEngine.Renderer>().material.color = color;
            SourceManager.Instance.Register(this);
            BinauralAudioEngine.Instance.UpdateAllImpulseResponses();
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

                    StartCoroutine(CreateConvolvedAudioBufferCoroutine(irLeft, irRight));
                }
        }

        private void InitData(int irLength)
        {
            AudioSettings.GetDSPBufferSize(out _dspBufferLength, out _);
            _fullBlockLength = CalcBlockSize(_dspBufferLength, irLength);
        }

        public void EnterNewIr(float[] irL, float[] irR)
        {
            irLeft = irL;
            irRight = irR;
            _updateIrNextFrame = true;
        }

        private IEnumerator CreateConvolvedAudioBufferCoroutine(float[] irLeft, float[] irRight)
        {
            var stopwatch = new System.Diagnostics.Stopwatch();
            stopwatch.Start();
            coroutineRunning = true;

            var spectralAudio = _spectralAudio;
            var chunkCount = Mathf.Min(audioChunkAmount, spectralAudio.Length);
            var fullLen = spectralAudio[0].Length;
            var dspLen = _dspBufferLength;
            var irLen = irLeft.Length;

            // IR in Segmente der Größe dspLen aufteilen
            var irSegmentCount = Mathf.CeilToInt((float)irLen / dspLen);

            // Output muss Audio + kompletten IR-Tail abdecken
            var totalOutputLength = chunkCount * dspLen + irLen - 1;
            _audioLeft = new float[totalOutputLength];
            _audioRight = new float[totalOutputLength];

            Debug.Log(
                $"fullLen={fullLen}, dspLen={dspLen}, irLen={irLen}, irSegments={irSegmentCount}, chunks={chunkCount}, outLen={totalOutputLength}");

            // IR-Segmente vorbereiten (außerhalb des Tasks, da ToFreqDomain nicht thread-safe sein muss)
            var irSegmentsL = new Complex[irSegmentCount][];
            var irSegmentsR = new Complex[irSegmentCount][];
            for (var s = 0; s < irSegmentCount; s++)
            {
                var offset = s * dspLen;
                var segLen = Mathf.Min(dspLen, irLen - offset);
                var segL = new float[dspLen];
                var segR = new float[dspLen];
                Array.Copy(irLeft, offset, segL, 0, segLen);
                Array.Copy(irRight, offset, segR, 0, segLen);
                irSegmentsL[s] = ToFreqDomain(segL, fullLen);
                irSegmentsR[s] = ToFreqDomain(segR, fullLen);
            }

            var audioLeft = _audioLeft;
            var audioRight = _audioRight;

            var task = Task.Run(() =>
            {
                try
                {
                    var stripeCount = Environment.ProcessorCount * 4;
                    var locks = new object[stripeCount];
                    for (var i = 0; i < stripeCount; i++)
                        locks[i] = new object();

                    // Für jeden Audio-Chunk und jedes IR-Segment
                    Parallel.For(0, chunkCount * irSegmentCount, idx =>
                    {
                        var k = idx / irSegmentCount; // Audio-Chunk Index
                        var s = idx % irSegmentCount; // IR-Segment Index

                        var src = spectralAudio[k];
                        var irSpecL = irSegmentsL[s];
                        var irSpecR = irSegmentsR[s];

                        var tempL = new Complex[fullLen];
                        var tempR = new Complex[fullLen];

                        for (var j = 0; j < fullLen; j++)
                        {
                            tempL[j] = irSpecL[j] * src[j];
                            tempR[j] = irSpecR[j] * src[j];
                        }

                        Fourier.Inverse(tempL, FourierOptions.Matlab);
                        Fourier.Inverse(tempR, FourierOptions.Matlab);

                        // Overlap-Add: Ergebnis an der richtigen Stelle addieren
                        // Audio-Chunk k startet bei k*dspLen, IR-Segment s startet bei s*dspLen
                        var baseIndex = k * dspLen + s * dspLen;
                        var outLen = dspLen + dspLen - 1; // Länge des IFFT-Ergebnisses das relevant ist

                        for (var n = 0; n < outLen; n++)
                        {
                            var outIdx = baseIndex + n;
                            if (outIdx >= audioLeft.Length)
                                break;

                            var l = (float)tempL[n].Real;
                            var r = (float)tempR[n].Real;

                            var stripe = outIdx % stripeCount;
                            lock (locks[stripe])
                            {
                                audioLeft[outIdx] += l;
                                audioRight[outIdx] += r;
                            }
                        }
                    });
                }
                catch (Exception e)
                {
                    Debug.LogError($"[Convolution Task] {e.GetType().Name}: {e.Message}\n{e.StackTrace}");
                    if (e is AggregateException ae)
                        foreach (var inner in ae.InnerExceptions)
                            Debug.LogError($"[Inner] {inner.GetType().Name}: {inner.Message}\n{inner.StackTrace}");
                    throw;
                }
            });

            while (!task.IsCompleted)
                yield return null;

            if (task.Exception != null)
            {
                foreach (var inner in task.Exception.Flatten().InnerExceptions)
                    Debug.LogError($"[Task Exception] {inner.GetType().Name}: {inner.Message}\n{inner.StackTrace}");
                coroutineRunning = false;
                yield break;
            }

            if (TryGetComponent<BinauralAudioFilter>(out var filter))
                filter.SetAudio(_audioLeft, _audioRight);

            coroutineRunning = false;
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
            var extensions = new[]
            {
                new ExtensionFilter("Audio", "wav")
            };

            var paths = StandaloneFileBrowser.OpenFilePanel("Wähle eine Datei", "", extensions, false);
            string chosenPath = new string("");
            if (paths.Length > 0 && !string.IsNullOrEmpty(paths[0]))
            {
                chosenPath = paths[0];
            }
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
                _numSamples = audioPreprocessor.numSamples;
                sw.Stop();
                Debug.Log($"Audio preprocessing took {sw.ElapsedMilliseconds} ms");
            }

            BinauralAudioEngine.Instance.UpdateAllImpulseResponses();
            path = chosenPath;
            return chosenPath;
        }
    }
}
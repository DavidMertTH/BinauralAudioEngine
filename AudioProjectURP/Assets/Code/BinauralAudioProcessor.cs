using System;
using System.Collections.Generic;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using MathNet.Numerics.IntegralTransforms;
using UnityEngine;
using UnityEngine.Serialization;
using Vector3 = UnityEngine.Vector3;

namespace Code
{
    [RequireComponent(typeof(AudioSource))]
    public class BinauralAudioProcessor : MonoBehaviour
    {
        public Transform sourceObject;
        public Transform targetObject;
        public AudioFileLoader audioFileLoader;
        public float earOffset = 0.1f; // Abstand der Ohren zur Mitte in Metern
        public bool bypass = false;
        public bool useDirect = false;
        public bool usePrimaryReflections = false;
        public bool useSecondaryReflections = false;
        public bool useHigherOrderReflections = false;
        public float Gain;
        public LiveConvolutionReverb reverb;
        public AudioRay DirectHit;
        public List<AudioRay> PrimaryReflections;
        public List<AudioRay> SecundaryReflections;
        public List<AudioRay> HigherOrderReflections;

        public float delaySmoothFactor = 0;

        public float[] _impulseResponseLeft;
        private float[] _impulseResponseRight;

        private float[] _delayBufferLeft;
        private float[] _delayBufferRight;
        private float _timeSinceLastImpulse;

        private int _bufferLength;
        private int _writeIndex;
        private int _sampleRate;

        private Vector3 _leftEar;
        private Vector3 _rightEar;

        private float prevLeftDelaySamples = 0f;
        private float prevRightDelaySamples = 0f;
        private bool _isSetup;
        private int _blockSize;
        private IntPtr _convLeft, _convRight;
        private ConvolutionReverbShap _reverb;
        private float[] _overlapBufferLeft;
        private float[] _overlapBufferRight;

        private void Awake()
        {
            _isSetup = false;
            AudioSettings.GetDSPBufferSize(out _blockSize, out _);
            _reverb = new ConvolutionReverbShap(2048, 1024);
        }

        private void Start()
        {
            _writeIndex = 0;
            _sampleRate = AudioSettings.outputSampleRate;
            _bufferLength = _sampleRate * 5;
            _delayBufferLeft = new float[_bufferLength];
            _delayBufferRight = new float[_bufferLength];
            
            _isSetup = true;
            ConvolutionReverb.initReverb(_sampleRate, _blockSize, 2);
        }

        private void OnDestroy()
        {
            ConvolutionReverb.shutdownReverb();
        }

        private void Update()
        {
            _leftEar = targetObject.transform.position - targetObject.transform.right * earOffset;
            _rightEar = targetObject.transform.position + targetObject.transform.right * earOffset;

            CreatePrimitiveImpulseresponse();
            _timeSinceLastImpulse += Time.deltaTime;
        }

        public void CreatePrimitiveImpulseresponse()
        {
            if (_timeSinceLastImpulse < 0.1) return;
            _timeSinceLastImpulse = 0;
            int irLength = 2024;
            // TOTO DAVID MARTIN KARG __ Diese Funktion sollte mit der HRTF Funktion ersetzt werden
            if (bypass || !_isSetup) return;
            _impulseResponseLeft = new float[irLength];
            _impulseResponseRight = new float[irLength];

            List<AudioRay> rays = GetAllSelectedRays();

            foreach (var ray in rays)
            {
                if (!ray.IsValid) continue;

                float leftDistance = ray.DistanceToImage + Vector3.Distance(ray.ImagePosition, _leftEar);
                float rightDistance = ray.DistanceToImage + Vector3.Distance(ray.ImagePosition, _rightEar);

                float leftDelaySec = leftDistance / 343f;
                float rightDelaySec = rightDistance / 343f;

                float targetLeftDelaySamples = _sampleRate * leftDelaySec;
                float targetRightDelaySamples = _sampleRate * rightDelaySec;

                float maxEarDist = Vector3.Distance(_rightEar, _leftEar);
                float binauralFactor = Mathf.Clamp((leftDistance - rightDistance) / (4 * maxEarDist), -1f, 1f);
                float averageDistance = (leftDistance + rightDistance) / 2;
                float DistanceAmplitude = 2 / averageDistance;

                if ((int)targetLeftDelaySamples >= irLength - 1 ||
                    (int)targetRightDelaySamples >= irLength - 1) continue;

                float leftAmplitude = DistanceAmplitude * (1 - binauralFactor) * ray.Absorbtion;
                float rightAmplitude = DistanceAmplitude * (1 + binauralFactor) * ray.Absorbtion;

                _impulseResponseLeft[(int)targetLeftDelaySamples] += leftAmplitude;
                _impulseResponseRight[(int)targetRightDelaySamples] += rightAmplitude;

                _impulseResponseLeft[(int)targetLeftDelaySamples + 1] += leftAmplitude / 3;
                _impulseResponseRight[(int)targetRightDelaySamples + 1] += rightAmplitude / 3;

                _impulseResponseLeft[(int)targetLeftDelaySamples - 1] += leftAmplitude / 3;
                _impulseResponseRight[(int)targetRightDelaySamples - 1] += rightAmplitude / 3;
            }
        }


        void OnAudioFilterRead(float[] data, int channels)
        {
            if (bypass || channels < 2 || !_isSetup) return;
            if (_impulseResponseLeft == null || _impulseResponseRight == null) return;

            float[] dataOutput = new float[data.Length];
            float[] dataLeft = new float[data.Length / 2];
            float[] dataRight = new float[data.Length / 2];


            for (int i = 0, j = 0; i < data.Length; i += 2, j++)
            {
                dataLeft[j] = data[i] * Gain;
                dataRight[j] = data[i + 1] * Gain;
            }


            dataLeft = reverb.ConvolveData(_impulseResponseLeft, dataLeft,ref _overlapBufferLeft);
            dataRight =reverb.ConvolveData(_impulseResponseRight, dataLeft,ref _overlapBufferRight);


            for (int i = 0, j = 0; i < data.Length; i += 2, j++)
            {
                data[i] = dataLeft[j];
                data[i + 1] = dataRight[j];
            } /*

            List<AudioRay> rays = GetAllSelectedRays();

            for (int i = 0; i < data.Length; i += 2)
            {
                float dry = (data[i] + data[i + 1]) * 0.5f;

                // Pufferbeschreiben durch alle Rays
                foreach (var ray in rays)
                {
                    if (!ray.IsValid) continue;

                    float leftDistance = ray.DistanceToImage + Vector3.Distance(ray.ImagePosition, _leftEar);
                    float rightDistance = ray.DistanceToImage + Vector3.Distance(ray.ImagePosition, _rightEar);

                    float leftDelaySec = leftDistance / 343f;
                    float rightDelaySec = rightDistance / 343f;

                    float targetLeftDelaySamples = _sampleRate * leftDelaySec;
                    float targetRightDelaySamples = _sampleRate * rightDelaySec;

                    prevLeftDelaySamples = Mathf.Lerp(prevLeftDelaySamples, targetLeftDelaySamples, delaySmoothFactor);
                    prevRightDelaySamples =
                        Mathf.Lerp(prevRightDelaySamples, targetRightDelaySamples, delaySmoothFactor);

                    int leftDelaySamples = Mathf.Clamp(Mathf.RoundToInt(prevLeftDelaySamples), 0, _bufferLength - 1);
                    int rightDelaySamples = Mathf.Clamp(Mathf.RoundToInt(prevRightDelaySamples), 0, _bufferLength - 1);

                    float maxEarDist = Vector3.Distance(_rightEar, _leftEar);
                    float binauralFactor = Mathf.Clamp((leftDistance - rightDistance) / (4 * maxEarDist), -1f, 1f);

                    int writeL = (_writeIndex + leftDelaySamples) % _bufferLength;
                    int writeR = (_writeIndex + rightDelaySamples) % _bufferLength;


                    float averageDistance = (leftDistance + rightDistance) / 2;

                    float DistanceAmplitude = 2 / averageDistance;

                    _delayBufferLeft[writeL] += Gain * DistanceAmplitude * dry * (1 - binauralFactor) * ray.Absorbtion;
                    _delayBufferRight[writeR] += Gain * DistanceAmplitude * dry * (1 + binauralFactor) * ray.Absorbtion;

                }

                // Lese Puffer & gib binaurales Signal aus
                float outL = _delayBufferLeft[_writeIndex];
                float outR = _delayBufferRight[_writeIndex];

                _delayBufferLeft[_writeIndex] = 0f;
                _delayBufferRight[_writeIndex] = 0f;

                data[i] = outL;
                data[i + 1] = outR;

                _writeIndex = (_writeIndex + 1) % _bufferLength;
            }*/
        }

        private void ConvolveIr(float[] data, float[] ir, float[] buffer, int writeIndex)
        {
            int dataLen = data.Length;
            int irLen = ir.Length;
            int bufferLen = buffer.Length;
            for (int n = 0; n < dataLen; ++n)
            {
                int idx = (writeIndex + n) % bufferLen;
                buffer[idx] = 0f;
            }

            Parallel.For<float[]>(0, dataLen,
                () => new float[bufferLen],
                (i, loopState, localBuf) =>
                {
                    float sample = data[i];
                    for (int j = 0; j < irLen; ++j)
                    {
                        int outIndex = (writeIndex + i + j) % bufferLen;
                        localBuf[outIndex] += sample * ir[j];
                    }

                    return localBuf;
                },
                localBuf =>
                {
                    lock (buffer)
                    {
                        for (int k = 0; k < bufferLen; ++k)
                            buffer[k] += localBuf[k];
                    }
                }
            );
        }


        private int GetOverlapBufferIndex(int bufferLength, int accessIndex)
        {
            if (accessIndex >= 0) return accessIndex % bufferLength;
            return bufferLength + accessIndex;
        }

        private List<AudioRay> GetAllSelectedRays()
        {
            List<AudioRay> rays = new List<AudioRay> { };
            if (useDirect && DirectHit.IsValid)
                rays.Add(DirectHit);
            if (usePrimaryReflections && PrimaryReflections != null && PrimaryReflections.Count > 0)
                rays.AddRange(PrimaryReflections);
            if (useSecondaryReflections && SecundaryReflections != null && SecundaryReflections.Count > 0)
                rays.AddRange(SecundaryReflections);
            if (useHigherOrderReflections && HigherOrderReflections != null && HigherOrderReflections.Count > 0)
                rays.AddRange(HigherOrderReflections);
            return rays;
        }
    }
}
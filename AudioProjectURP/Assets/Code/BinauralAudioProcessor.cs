using System.Collections.Generic;
using UnityEngine;

namespace Code
{
    [RequireComponent(typeof(AudioSource))]
    public class BinauralAudioProcessor : MonoBehaviour
    {
        public Transform sourceObject;
        public Transform targetObject;

        public float earOffset = 0.1f; // Abstand der Ohren zur Mitte in Metern
        public bool bypass = false;
        public bool UseDirect = false;
        public bool UsePrimaryReflections = false;
        public float Gain;

        public AudioRay DirectHit;
        public List<AudioRay> PrimaryReflections;
        public List<AudioRay> SecundaryReflections;

        public float delaySmoothFactor = 0;

        private float[] _delayBufferLeft;
        private float[] _delayBufferRight;
        private int _bufferLength;
        private int _writeIndex;
        private int _sampleRate;

        private Vector3 _leftEar;
        private Vector3 _rightEar;

        private float prevLeftDelaySamples = 0f;
        private float prevRightDelaySamples = 0f;

        private void Start()
        {
            _sampleRate = AudioSettings.outputSampleRate;
            _bufferLength = _sampleRate * 2; // 2 Sekunden Puffer
            _delayBufferLeft = new float[_bufferLength];
            _delayBufferRight = new float[_bufferLength];
        }

        private void Update()
        {
            _leftEar = targetObject.transform.position - targetObject.transform.right * earOffset;
            _rightEar = targetObject.transform.position + targetObject.transform.right * earOffset;
        }


        void OnAudioFilterRead(float[] data, int channels)
        {
            if (bypass || channels < 2) return;

            List<AudioRay> rays = new List<AudioRay> { };
            if (UseDirect)
                rays.Add(DirectHit);
            if (UsePrimaryReflections)
                rays.AddRange(PrimaryReflections);
            
            rays.AddRange(SecundaryReflections);

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
                    float binauralFactor = Mathf.Clamp((leftDistance - rightDistance) / (2 * maxEarDist), -1f, 1f);

                    int writeL = (_writeIndex + leftDelaySamples) % _bufferLength;
                    int writeR = (_writeIndex + rightDelaySamples) % _bufferLength;


                    float averageDistance = (leftDistance + rightDistance) / 2;

                    float DistanceAmplitude = 2 / averageDistance;

                    _delayBufferLeft[writeL] += Gain * DistanceAmplitude * dry * (1 - binauralFactor);
                    _delayBufferRight[writeR] += Gain * DistanceAmplitude * dry * (1 + binauralFactor);
                }

                // Lese Puffer & gib binaurales Signal aus
                float outL = _delayBufferLeft[_writeIndex];
                float outR = _delayBufferRight[_writeIndex];

                _delayBufferLeft[_writeIndex] = 0f;
                _delayBufferRight[_writeIndex] = 0f;

                data[i] = outL;
                data[i + 1] = outR;

                _writeIndex = (_writeIndex + 1) % _bufferLength;
            }
        }
    }
}
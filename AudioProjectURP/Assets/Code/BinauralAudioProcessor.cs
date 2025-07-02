using System.Collections.Generic;
using System.Numerics;
using System.Threading.Tasks;
using UnityEngine;
using Vector3 = UnityEngine.Vector3;

namespace Code
{
    [RequireComponent(typeof(AudioSource))]
    public class BinauralAudioProcessor : MonoBehaviour
    {
        public Transform targetObject;
        public AudioFileLoader audioFileLoader;
        
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
        
        private float[] _impulseResponseLeft;
        private float[] _impulseResponseRight;
        
        private List<float[]> _previousImpulseResponsesLeft;
        private List<float[]> _previousImpulseResponsesRight;

        private float _timeSinceLastImpulse;

        private int _bufferLength;
        private int _sampleRate;

        private Vector3 _leftEar;
        private Vector3 _rightEar;

        private bool _isSetup;
        
        private float[] _overlapBufferLeft;
        private float[] _overlapBufferRight;
        
        private Complex[] _freqDomainIrLeft;
        private Complex[] _freqDomainIrRight;
        private readonly float earOffset = 0.1f; // Abstand der Ohren zur Mitte in Metern

        private void Awake()
        {
            _isSetup = false;
        }

        private void Start()
        {
            _sampleRate = AudioSettings.outputSampleRate;
            _previousImpulseResponsesLeft = new List<float[]>();
            _previousImpulseResponsesRight = new List<float[]>();
            _isSetup = true;
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
        }

        public void CreatePrimitiveImpulseresponse()
        {
            int irLength = 2024 * 2;
            // TOTO DAVID MARTIN KARG __ Diese Funktion sollte mit der HRTF Funktion ersetzt werden
            if (bypass || !_isSetup) return;
            _impulseResponseLeft = new float[irLength];
            _impulseResponseRight = new float[irLength];

            List<AudioRay> rays = GetAllSelectedRays();
            int overshootLength = 20;
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
                float distanceAmplitude = 2 / averageDistance;

                if ((int)targetLeftDelaySamples >= irLength - 1 ||
                    (int)targetRightDelaySamples >= irLength - 1) continue;

                float leftAmplitude = distanceAmplitude * (1 - binauralFactor) * ray.Absorbtion *Gain;
                float rightAmplitude = distanceAmplitude * (1 + binauralFactor) * ray.Absorbtion * Gain;

                _impulseResponseLeft[(int)targetLeftDelaySamples] += leftAmplitude;
                _impulseResponseRight[(int)targetRightDelaySamples] += rightAmplitude;

                for (int i = 1; i < overshootLength; i++)
                {
                    if(targetLeftDelaySamples+i >= irLength - 1) break;
                    if(targetRightDelaySamples+i >= irLength - 1) break;

                    _impulseResponseLeft[(int)targetLeftDelaySamples + i] += leftAmplitude / i;
                    _impulseResponseRight[(int)targetRightDelaySamples + 1] += rightAmplitude / i;
                }
            }

            Task.Run(() => {_freqDomainIrLeft = reverb.ToFreqDomain(_impulseResponseLeft,_previousImpulseResponsesLeft); });
            Task.Run(() => {_freqDomainIrRight = reverb.ToFreqDomain(_impulseResponseRight,_previousImpulseResponsesRight); });

            if (_previousImpulseResponsesRight.Count > 20)
            {
                _previousImpulseResponsesRight.RemoveAt(0);
                _previousImpulseResponsesLeft.RemoveAt(0); 
            }
            
            _previousImpulseResponsesLeft.Add(_impulseResponseLeft);
            _previousImpulseResponsesRight.Add(_impulseResponseRight);
        }


        void OnAudioFilterRead(float[] data, int channels)
        {
            if (bypass || channels < 2 || !_isSetup) return;
            if (_freqDomainIrLeft == null || _freqDomainIrRight == null) return;

            float[] dataLeft = new float[data.Length / 2];
            float[] dataRight = new float[data.Length / 2];


            for (int i = 0, j = 0; i < data.Length; i += 2, j++)
            {
                dataLeft[j] = data[i] * Gain;
                dataRight[j] = data[i + 1] * Gain;
            }


            dataLeft = reverb.ConvolveData(_freqDomainIrLeft, dataLeft, ref _overlapBufferLeft);
            dataRight = reverb.ConvolveData(_freqDomainIrRight, dataRight, ref _overlapBufferRight);


            for (int i = 0, j = 0; i < data.Length; i += 2, j++)
            {
                data[i] = dataLeft[j];
                data[i + 1] = dataRight[j];
            } 
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
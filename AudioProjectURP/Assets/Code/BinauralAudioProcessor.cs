using System.Collections.Generic;
using System;
using System.Numerics;
using System.Threading.Tasks;
using UnityEngine;
using Vector3 = UnityEngine.Vector3;
using System.Runtime.InteropServices;
using UnityEngine.Serialization;

namespace Code
{
    [RequireComponent(typeof(AudioSource))]
    public class BinauralAudioProcessor : MonoBehaviour
    {
        [DllImport("hrtf_import")]
        private static extern IntPtr mysofa_load(string filename, out int err);


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
        public ImpulseGraphUI impulseGraphUI;
        public float[] impulseResponseLeft;
        public float[] impulseResponseRight;

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

        private MySofaHRIR sofaHRIR;
        public bool useFirstFunction = true;

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
            Application.targetFrameRate = -1;

            string filePath = Application.streamingAssetsPath + "/sofafiles/hrtf0.sofa";
            int errorCode;
            IntPtr hrtfPtr = DllDemoIntegration.mysofa_load(filePath, out errorCode);

            sofaHRIR = new MySofaHRIR(hrtfPtr);

            Debug.Log(sofaHRIR.radius);
        }


        private void Update()
        {
            _leftEar = targetObject.transform.position - targetObject.transform.right * earOffset;
            _rightEar = targetObject.transform.position + targetObject.transform.right * earOffset;

            if (Input.GetKeyDown(KeyCode.Space))
            {
                // Wechselt zwischen den beiden Funktionen
                useFirstFunction = !useFirstFunction;
            }

            if (Input.GetKeyDown(KeyCode.Keypad0))
            {
                string filePath = Application.streamingAssetsPath + "/sofafiles/hrtf0.sofa"; // Pfad zur Datei
                int errorCode;

                IntPtr hrtfPtr = mysofa_load(filePath, out errorCode);

                sofaHRIR = new MySofaHRIR(hrtfPtr);
            }
            else if (Input.GetKeyDown(KeyCode.Keypad1))
            {
                string filePath = Application.streamingAssetsPath + "/sofafiles/hrtf1.sofa";
                int errorCode;

                IntPtr hrtfPtr = mysofa_load(filePath, out errorCode);

                sofaHRIR = new MySofaHRIR(hrtfPtr);
            }
            else if (Input.GetKeyDown(KeyCode.Keypad2))
            {
                string filePath = Application.streamingAssetsPath + "/sofafiles/hrtf2.sofa";
                int errorCode;

                IntPtr hrtfPtr = mysofa_load(filePath, out errorCode);

                sofaHRIR = new MySofaHRIR(hrtfPtr);
            }
            else if (Input.GetKeyDown(KeyCode.Keypad3))
            {
                string filePath = Application.streamingAssetsPath + "/sofafiles/hrtf4.sofa";
                int errorCode;

                IntPtr hrtfPtr = mysofa_load(filePath, out errorCode);

                sofaHRIR = new MySofaHRIR(hrtfPtr);
            }

            // Ruft je nach Zustand die richtige Funktion auf
            if (useFirstFunction)
            {
                CreatePrimitiveImpulseresponse();
            }
            else
            {
                CreateHRTFImpulseresponse();
            }
        }

        public void CreatePrimitiveImpulseresponse()
        {
            int irLength = 2024 * 2;
            // TOTO DAVID MARTIN KARG __ Diese Funktion sollte mit der HRTF Funktion ersetzt werden
            if (bypass || !_isSetup) return;

            impulseResponseLeft = new float[irLength];
            impulseResponseRight = new float[irLength];

            List<AudioRay> rays = GetAllSelectedRays();
            int overshootLength = 20;

            float lengthDirectRay = DirectHit.DistanceToImage;
            foreach (var ray in rays)
            {
                if (!ray.IsValid) continue;

                if (ray.DistanceToImage < lengthDirectRay)
                {
                    print("error");
                }

                float imageToCenter = Vector3.Distance(targetObject.transform.position, ray.ImagePosition);

                float offsetLeft = imageToCenter - Vector3.Distance(_leftEar, ray.ImagePosition);
                float offsetRight = imageToCenter - Vector3.Distance(_rightEar, ray.ImagePosition);
                float leftDistance = ray.DistanceToImage - offsetLeft;
                float rightDistance = ray.DistanceToImage - offsetRight;

                float leftDelaySec = leftDistance / 343f;
                float rightDelaySec = rightDistance / 343f;

                float targetLeftDelaySamples = _sampleRate * leftDelaySec;
                float targetRightDelaySamples = _sampleRate * rightDelaySec;

                float maxEarDist = Vector3.Distance(_rightEar, _leftEar);
                float binauralFactor = Mathf.Clamp((leftDistance - rightDistance) / (4 * maxEarDist), -2f, 2f);
                float averageDistance = (leftDistance + rightDistance) / 2;
                float distanceAmplitude = 2 / averageDistance;

                if ((int)targetLeftDelaySamples >= irLength - 1 ||
                    (int)targetRightDelaySamples >= irLength - 1) continue;

                float leftAmplitude = distanceAmplitude * (1 - binauralFactor) * ray.Absorbtion * Gain;
                float rightAmplitude = distanceAmplitude * (1 + binauralFactor) * ray.Absorbtion * Gain;

                impulseResponseLeft[(int)targetLeftDelaySamples] += leftAmplitude;
                impulseResponseRight[(int)targetRightDelaySamples] += rightAmplitude;

                for (int i = 1; i < overshootLength; i++)
                {
                    if (targetLeftDelaySamples + i >= irLength - 1) break;
                    if (targetRightDelaySamples + i >= irLength - 1) break;

                    impulseResponseLeft[(int)targetLeftDelaySamples + i] += leftAmplitude / i;
                    impulseResponseRight[(int)targetRightDelaySamples + 1] += rightAmplitude / i;
                }
            }
            
            int lengthSum = 1024 + irLength;
            int requiredLength = LiveConvolutionReverb.GetMaxZweierPotenz(lengthSum);

            Task.Run(() => { _freqDomainIrLeft = reverb.ToFreqDomain(impulseResponseLeft, requiredLength); });
            Task.Run(() => { _freqDomainIrRight = reverb.ToFreqDomain(impulseResponseRight, requiredLength); });

            if (_previousImpulseResponsesRight.Count > 20)
            {
                _previousImpulseResponsesRight.RemoveAt(0);
                _previousImpulseResponsesLeft.RemoveAt(0);
            }

            _previousImpulseResponsesLeft.Add(impulseResponseLeft);
            _previousImpulseResponsesRight.Add(impulseResponseRight);
        }


        public void CreateHRTFImpulseresponse()
        {
            int irLength = 2024 * 2;

            if (bypass || !_isSetup) return;

            impulseResponseLeft = new float[irLength];
            impulseResponseRight = new float[irLength];

            List<AudioRay> rays = GetAllSelectedRays();

            float lengthDirectRay = DirectHit.DistanceToImage;
            foreach (var ray in rays)
            {
                if (!ray.IsValid) continue;

                Vector3 vecSourceListener = targetObject.transform.position - new Vector3(ray.ImagePosition.x, ray.ImagePosition.y, ray.ImagePosition.z);
                Vector3 listenerUp = targetObject.transform.up;
                Vector3 listenerForward = targetObject.transform.forward;

                float azimuth = Mathf.Atan2(
                    Vector3.Dot(Vector3.Cross(listenerUp, listenerForward), vecSourceListener.normalized),
                    Vector3.Dot(listenerForward, vecSourceListener)) * Mathf.Rad2Deg;
                float elevation = Mathf.Asin(Vector3.Dot(vecSourceListener, listenerUp)) * Mathf.Rad2Deg;

                (float[] leftEarResponse, float[] rightEarResponse) = sofaHRIR.FindBestHRIR(azimuth, elevation);

                if (leftEarResponse != null && rightEarResponse != null)
                {
                    float distanceToSource = ray.DistanceToImage +
                                             (Vector3.Distance(targetObject.transform.position, ray.ImagePosition) - sofaHRIR.radius * 2);
                    float propagationDelaySec = distanceToSource / 343f; // Schallgeschwindigkeit: 343 m/s
                    float propagationDelaySamples = _sampleRate * propagationDelaySec;
                    float distanceAmplitudeTwo = ray.Absorbtion * (8 / distanceToSource) * Gain;

                    for (int i = 0; i < sofaHRIR.hrtfData.N; i++)
                    {
                        if (i + propagationDelaySamples >= irLength - 1 || propagationDelaySamples < 0) break;

                        impulseResponseLeft[i + (int)propagationDelaySamples] +=
                            leftEarResponse[i] * distanceAmplitudeTwo;
                        impulseResponseRight[i + (int)propagationDelaySamples] +=
                            rightEarResponse[i] * distanceAmplitudeTwo;
                    }
                }
            }

            int lengthSum = 1024 + irLength;
            int requiredLength = LiveConvolutionReverb.GetMaxZweierPotenz(lengthSum);

            Task.Run(() => { _freqDomainIrLeft = reverb.ToFreqDomain(impulseResponseLeft, requiredLength); });
            Task.Run(() => { _freqDomainIrRight = reverb.ToFreqDomain(impulseResponseRight, requiredLength); });

            if (_previousImpulseResponsesRight.Count > 20)
            {
                _previousImpulseResponsesRight.RemoveAt(0);
                _previousImpulseResponsesLeft.RemoveAt(0);
            }

            _previousImpulseResponsesLeft.Add(impulseResponseLeft);
            _previousImpulseResponsesRight.Add(impulseResponseRight);
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

            Complex[] leftCompData;
            Complex[] rightCompData;


            int requiredLength =
                LiveConvolutionReverb.GetMaxZweierPotenz(impulseResponseLeft.Length + dataLeft.Length);

            var left = dataLeft;
            var leftTask = Task.Run(() => reverb.ToFreqDomain(left, _freqDomainIrLeft.Length));
            var right = dataRight;
            var rightTask = Task.Run(() => reverb.ToFreqDomain(right, _freqDomainIrLeft.Length));

            Task.WaitAll(leftTask, rightTask);

            leftCompData = leftTask.Result;
            rightCompData = rightTask.Result;

            dataLeft = reverb.ConvolveData(_freqDomainIrLeft,_freqDomainIrLeft, leftCompData, dataLeft, ref _overlapBufferLeft);
            dataRight = reverb.ConvolveData(_freqDomainIrRight,_freqDomainIrRight, rightCompData, dataRight, ref _overlapBufferRight);
/*
            dataLeft = reverb.ConvolveData(_freqDomainIrLeft, dataLeft, ref _overlapBufferLeft);
            dataRight = reverb.ConvolveData(_freqDomainIrRight, dataRight, ref _overlapBufferRight);
*/
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
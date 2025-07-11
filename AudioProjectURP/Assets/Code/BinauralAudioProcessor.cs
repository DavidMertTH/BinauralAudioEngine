using System;
using System.Collections.Generic;
using System;
using System.Numerics;
using System.Threading.Tasks;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
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
        public float alpha;
        public float Gain;
        public int blockSize = 128;
        public LiveConvolutionReverb reverb;

        public AudioRay DirectHit;
        public List<AudioRay> PrimaryReflections;
        public List<AudioRay> SecundaryReflections;
        public List<AudioRay> HigherOrderReflections;
        public ImpulseGraphUI impulseGraphUI;
        public float[] impulseResponseLeft;
        public float[] impulseResponseRight;
        [HideInInspector] public float[] leftData;

        private NativeArray<float> _nativeImpulseResponseLeft;
        private NativeArray<float> _nativeImpulseResponseRight;

        private float _timeSinceLastImpulse;

        private int _bufferLength;
        public int sampleRate;

        private Vector3 _leftEar;
        private Vector3 _rightEar;

        private bool _isSetup;

        private float[] _overlapBufferLeft;
        private float[] _overlapBufferRight;

        private Complex[] _freqDomainIrLeft;
        private Complex[] _freqDomainIrRight;
        private readonly float earOffset = 0.1f; // Abstand der Ohren zur Mitte in Metern
        private float[] _lastData;
        private Task<Complex[]> _rightTask;
        private Task<Complex[]> _leftTask;
        private Task leftTask;
        private Task rightTask;

        private MySofaHRIR sofaHRIR;

        [FormerlySerializedAs("useFirstFunction")]
        public bool useHRTFs = true;

        private FillImpulseResponseParallel _impulseJobLeft;
        private FillImpulseResponseParallel _impulseJobRight;
        private bool _jobIsRunning;

        private int _dataBufferLength;
        private JobHandle _leftJobHandle;
        private JobHandle _rightJobHandle;
        private int _irLength;
        private float[][] _leftBlocks;
        private float[][] _rightBlocks;
        private bool _applyHann = false;

        private void Awake()
        {
            _isSetup = false;
        }

        private void Start()
        {
            sampleRate = AudioSettings.outputSampleRate;
            _isSetup = true;
            Application.targetFrameRate = -1;
            int bufferLength;
            int numBuffers;
            AudioSettings.GetDSPBufferSize(out bufferLength, out numBuffers);
            _dataBufferLength = bufferLength;
            _lastData = new float[bufferLength * numBuffers];
            _jobIsRunning = false;
            _irLength = reverb.fullIrLength - bufferLength;
            _overlapBufferLeft = new float[_irLength * 2];
            _overlapBufferRight = new float[_irLength * 2];

            string filePath = Application.streamingAssetsPath + "/sofafiles/hrtf0.sofa";
            int errorCode;
            IntPtr hrtfPtr = DllDemoIntegration.mysofa_load(filePath, out errorCode);
            impulseResponseLeft = new float[_irLength];
            sofaHRIR = new MySofaHRIR(hrtfPtr);
            Debug.Log(sofaHRIR.radius);
            print(_dataBufferLength);
            int blockAmount = _dataBufferLength / blockSize;
            _leftBlocks = new float[blockAmount][];
            _rightBlocks = new float[blockAmount][];
        }


        private void Update()
        {
            GetUserInput();
            if (useHRTFs)
            {
                CreateHRTFImpulseresponse();
            }
            else
            {
                SavePrimitiveImpulseResponse();
                StartPrimitiveImpulseResponse();
            }

            _leftEar = targetObject.transform.position - targetObject.transform.right * earOffset;
            _rightEar = targetObject.transform.position + targetObject.transform.right * earOffset;
        }

        private void GetUserInput()
        {
            if (Input.GetKeyDown(KeyCode.B))
            {
                bypass = !bypass;
            }
            if (Input.GetKeyDown(KeyCode.H))
            {
                _applyHann = !_applyHann;
            }
            if (Input.GetKeyDown(KeyCode.Space))
            {
                useHRTFs = !useHRTFs;
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
        }

        public void SavePrimitiveImpulseResponse()
        {
            if (!_jobIsRunning) return;
            _jobIsRunning = false;
            _leftJobHandle.Complete();
            _rightJobHandle.Complete();
            impulseResponseLeft = new float[_irLength];
            impulseResponseRight = new float[_irLength];
            _impulseJobLeft.ImpulseResponse.CopyTo(impulseResponseLeft);
            _impulseJobRight.ImpulseResponse.CopyTo(impulseResponseRight);

            int lengthSum = (_dataBufferLength / 2) + _irLength;
            int requiredLength = LiveConvolutionReverb.GetMaxZweierPotenz(lengthSum);

            if (_overlapBufferLeft == null || _overlapBufferRight == null)
            {
                _overlapBufferLeft = new float[requiredLength];
                _overlapBufferRight = new float[requiredLength];
            }

            Task.Run(() => { _freqDomainIrLeft = reverb.ToFreqDomain(impulseResponseLeft, requiredLength); });
            Task.Run(() => { _freqDomainIrRight = reverb.ToFreqDomain(impulseResponseRight, requiredLength); });

            _nativeImpulseResponseLeft.Dispose();
            _nativeImpulseResponseRight.Dispose();
        }

        private void OnDestroy()
        {
            if (!_jobIsRunning) return;
            _leftJobHandle.Complete();
            _rightJobHandle.Complete();
            if (_nativeImpulseResponseLeft.IsCreated) _nativeImpulseResponseLeft.Dispose();
            if (_nativeImpulseResponseRight.IsCreated) _nativeImpulseResponseRight.Dispose();
        }

        public void StartPrimitiveImpulseResponse()
        {
            if (bypass || !_isSetup) return;


            List<AudioRay> rays = GetAllSelectedRays();

            _nativeImpulseResponseLeft = new NativeArray<float>(_irLength, Allocator.TempJob);
            _nativeImpulseResponseRight = new NativeArray<float>(_irLength, Allocator.TempJob);

            NativeArray<int> timeDelayLeft = new NativeArray<int>(rays.Count, Allocator.TempJob);
            NativeArray<int> timeDelayRight = new NativeArray<int>(rays.Count, Allocator.TempJob);

            NativeArray<float> amplitudeLeft = new NativeArray<float>(rays.Count, Allocator.TempJob);
            NativeArray<float> amplitudeRight = new NativeArray<float>(rays.Count, Allocator.TempJob);


            NativeArray<AudioRay> nativeAudioRays = new NativeArray<AudioRay>(rays.Count, Allocator.TempJob);
            nativeAudioRays.CopyFrom(rays.ToArray());


            GetRayToImpulseData dataJob = new GetRayToImpulseData()
            {
                AmplitudeLeft = amplitudeLeft,
                AmplitudeRight = amplitudeRight,
                TimeDelaySamplesLeft = timeDelayLeft,
                TimeDelaySamplesRight = timeDelayRight,

                Gain = Gain,
                IrLength = _irLength,
                Rays = nativeAudioRays,
                SampleRate = sampleRate,
                LeftEarPosition = _leftEar,
                RightEarPosition = _rightEar,
                TargetPosition = targetObject.transform.position
            };
            JobHandle dataHandle = dataJob.Schedule(amplitudeLeft.Length, 1);

            _impulseJobLeft = new FillImpulseResponseParallel()
            {
                ImpulseResponse = _nativeImpulseResponseLeft,
                Amplitude = amplitudeLeft,
                TimeDelaySamples = timeDelayLeft,
            };
            _impulseJobRight = new FillImpulseResponseParallel()
            {
                ImpulseResponse = _nativeImpulseResponseRight,
                Amplitude = amplitudeRight,
                TimeDelaySamples = timeDelayRight,
            };
            _leftJobHandle = _impulseJobLeft.Schedule(_nativeImpulseResponseLeft.Length, 1, dataHandle);
            _rightJobHandle = _impulseJobRight.Schedule(_nativeImpulseResponseRight.Length, 1, dataHandle);

            _jobIsRunning = true;
        }

        public void CreateHRTFImpulseresponse()
        {
            if (bypass || !_isSetup) return;

            impulseResponseLeft = new float[_irLength];
            impulseResponseRight = new float[_irLength];

            List<AudioRay> rays = GetAllSelectedRays();

            float lengthDirectRay = DirectHit.DistanceToImage;
            foreach (var ray in rays)
            {
                if (!ray.IsValid) continue;

                Vector3 vecSourceListener = targetObject.transform.position -
                                            new Vector3(ray.ImagePosition.x, ray.ImagePosition.y, ray.ImagePosition.z);
                Vector3 listenerUp = targetObject.transform.up;
                Vector3 listenerForward = targetObject.transform.forward;

                float azimuth = Mathf.Atan2(
                    Vector3.Dot(Vector3.Cross(listenerUp, listenerForward), vecSourceListener.normalized),
                    Vector3.Dot(listenerForward, vecSourceListener)) * Mathf.Rad2Deg;
                float elevation = Mathf.Asin(Vector3.Dot(vecSourceListener, listenerUp)) * Mathf.Rad2Deg;

                (float[] leftEarResponse, float[] rightEarResponse) = sofaHRIR.FindBestHRIR(azimuth, elevation);

                if (leftEarResponse != null && rightEarResponse != null)
                {
                    float distanceToSource = ray.DistanceToImage + (sofaHRIR.radius);
                    float propagationDelaySec = distanceToSource / 343f; // Schallgeschwindigkeit: 343 m/s
                    float propagationDelaySamples = sampleRate * propagationDelaySec;
                    float distanceAmplitudeTwo = ray.Absorbtion * (8 / distanceToSource) * Gain;

                    for (int i = 0; i < sofaHRIR.hrtfData.N; i++)
                    {
                        if (i + propagationDelaySamples >= _irLength - 1 || propagationDelaySamples < 0) break;

                        impulseResponseLeft[i + (int)propagationDelaySamples] +=
                            leftEarResponse[i] * distanceAmplitudeTwo;
                        impulseResponseRight[i + (int)propagationDelaySamples] +=
                            rightEarResponse[i] * distanceAmplitudeTwo;
                    }
                }
            }
        }

        void OnAudioFilterRead(float[] data, int channels)
        {
            if (bypass || channels < 2 || !_isSetup) return;
            if (impulseResponseLeft == null || impulseResponseRight == null || impulseResponseRight.Length == 0 ||
                impulseResponseLeft.Length == 0) return;
            int blockAmount = _dataBufferLength / blockSize;

            if (data.Length % blockSize != 0)
            {
                print("FAULTY BLOCKLENGTH (AUDIO)");
                return;
            }

            float[] dataLeft = new float[data.Length / 2];
            float[] dataRight = new float[data.Length / 2];

            for (int i = 0, j = 0; i < data.Length; i += 2, j++)
            {
                dataLeft[j] = data[i] * Gain;
                dataRight[j] = data[i + 1] * Gain;
            }

            if (_applyHann)
            {
                Hann.ApplyHann(dataLeft);
                Hann.ApplyHann(dataRight);
            }


            if (leftTask != null)
            {
                Task.WaitAll(leftTask);
                Task.WaitAll(rightTask);
            }

            leftTask = Task.Run(() =>
            {
                dataLeft = reverb.ProgressiveConvolve(impulseResponseLeft, dataLeft, dataLeft, ref _overlapBufferLeft,
                    dataLeft.Length);
                for (int i = 0, j = 0; i < data.Length; i += 2, j++)
                {
                    _lastData[i] = dataLeft[j];
                }

                leftData = dataLeft;
            });
            rightTask = Task.Run(() =>
            {
                dataRight = reverb.ProgressiveConvolve(impulseResponseRight, dataRight, dataRight,
                    ref _overlapBufferRight,
                    dataRight.Length);
                for (int i = 0, j = 0; i < data.Length; i += 2, j++)
                {
                    _lastData[i + 1] = dataRight[j];
                }
            });
            if (_lastData != null)
            {
                for (int i = 0; i < data.Length; i++)
                {
                    data[i] = _lastData[i];
                }
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

        [BurstCompile]
        private struct GetRayToImpulseData : IJobParallelFor
        {
            public NativeArray<int> TimeDelaySamplesLeft;
            public NativeArray<int> TimeDelaySamplesRight;

            public NativeArray<float> AmplitudeLeft;
            public NativeArray<float> AmplitudeRight;

            [DeallocateOnJobCompletion] [ReadOnly] public NativeArray<AudioRay> Rays;
            [ReadOnly] public Vector3 LeftEarPosition;
            [ReadOnly] public Vector3 RightEarPosition;
            [ReadOnly] public Vector3 TargetPosition;

            [ReadOnly] public float Gain;
            [ReadOnly] public float SampleRate;
            [ReadOnly] public int IrLength;

            public void Execute(int index)
            {
                AudioRay ray = Rays[index];

                if (!ray.IsValid)
                {
                    TimeDelaySamplesLeft[index] = -1;
                    TimeDelaySamplesRight[index] = -1;
                    return;
                }

                float imageToCenter = Vector3.Distance(TargetPosition, ray.ImagePosition);

                float offsetLeft = imageToCenter - Vector3.Distance(LeftEarPosition, ray.ImagePosition);
                float offsetRight = imageToCenter - Vector3.Distance(RightEarPosition, ray.ImagePosition);

                float leftDistance = ray.DistanceToImage - offsetLeft;
                float rightDistance = ray.DistanceToImage - offsetRight;

                float leftDelaySec = leftDistance / 343f;
                float rightDelaySec = rightDistance / 343f;

                float targetLeftDelaySamples = SampleRate * leftDelaySec;
                float targetRightDelaySamples = SampleRate * rightDelaySec;

                if ((int)targetLeftDelaySamples >= IrLength - 1 ||
                    (int)targetRightDelaySamples >= IrLength - 1)
                {
                    TimeDelaySamplesLeft[index] = -1;
                    TimeDelaySamplesRight[index] = -1;
                    return;
                }

                TimeDelaySamplesLeft[index] = (int)targetLeftDelaySamples;
                TimeDelaySamplesRight[index] = (int)targetRightDelaySamples;

                float maxEarDist = Vector3.Distance(RightEarPosition, LeftEarPosition);
                float binauralFactor = Mathf.Clamp((leftDistance - rightDistance) / (4 * maxEarDist), -2f, 2f);
                float averageDistance = (leftDistance + rightDistance) / 2;
                float distanceAmplitude = 3 / (averageDistance);

                float leftAmplitude = distanceAmplitude * (1 - binauralFactor) * ray.Absorbtion * Gain;
                float rightAmplitude = distanceAmplitude * (1 + binauralFactor) * ray.Absorbtion * Gain;

                leftAmplitude = Mathf.Min(leftAmplitude, 1);
                rightAmplitude = Mathf.Min(rightAmplitude, 1);

                AmplitudeLeft[index] = leftAmplitude;
                AmplitudeRight[index] = rightAmplitude;
            }
        }


        [BurstCompile]
        private struct FillImpulseResponseParallel : IJobParallelFor
        {
            public NativeArray<float> ImpulseResponse;

            [DeallocateOnJobCompletion] [ReadOnly] public NativeArray<int> TimeDelaySamples;
            [DeallocateOnJobCompletion] [ReadOnly] public NativeArray<float> Amplitude;

            public void Execute(int index)
            {
                for (int r = 0; r < TimeDelaySamples.Length; r++)
                {
                    if (TimeDelaySamples[r] != index) continue;
                    ImpulseResponse[index] += Amplitude[r];
                }
            }
        }
    }
}
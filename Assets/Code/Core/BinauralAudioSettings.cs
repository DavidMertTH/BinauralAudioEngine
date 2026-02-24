using System;
using System.IO;
using Code.Simulation;
using UnityEngine;

namespace Code.Core
{
    [Serializable]
    public class BinauralAudioSettings
    {
        [SerializeField] [Range(1f, 100f)] private int raysAroundListenerAndEachSource = 10;
        [SerializeField] [Range(3f, 6f)] private int maxIterativeBounces = 5;
        [SerializeField] [Range(0.001f, 2)] private float impulseResponseLengthSeconds = 0.1f;
        [SerializeField] private int impulseResponseSamplesPerSecond = 1024;
        [SerializeField] private string sofaFile = "hrtf0.sofa";
        [SerializeField] private LayerMask raycastMask = -1;
        [SerializeField] [Range(0, 180f)] private float iterativeReflectionDeviationAngleDeg = 30f;

        public int RaysAroundListenerAndEachSource => raysAroundListenerAndEachSource;
        public int MaxIterativeBounces => maxIterativeBounces;
        public float ImpulseResponseLengthSeconds => impulseResponseLengthSeconds;
        public int ImpulseResponseSamplesPerSecond => impulseResponseSamplesPerSecond;
        public int ImpulseResponseSamples => (int)(impulseResponseSamplesPerSecond * impulseResponseLengthSeconds);
        public string SofaFile => Path.Combine(Application.streamingAssetsPath, "sofafiles", sofaFile);
        public LayerMask RaycastMask => raycastMask;
        public float IterativeReflectionDeviationAngleDeg => iterativeReflectionDeviationAngleDeg;

        public AudioPathArrayLayout GetAudioPathArrayLayout(int numSources) =>
            new(numSources, raysAroundListenerAndEachSource, RaysAroundListenerAndEachSource, maxIterativeBounces);
    }
}
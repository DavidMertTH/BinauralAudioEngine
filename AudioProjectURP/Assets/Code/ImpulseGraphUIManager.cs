using System;
using UnityEngine;

namespace Code
{
    public class ImpulseGraphUIManager : MonoBehaviour
    {
        public static ImpulseGraphUIManager Instance;
        public ImpulseGraphUI irLeft;
        public ImpulseGraphUI irRight;
        public ImpulseGraphUI spectreLeft;
        public ImpulseGraphUI spectreRight;
        public BinauralAudioProcessor audioProcessor;

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
            }
            else
            {
                Destroy(this);
            }
        }

        private void Update()
        {
            if (audioProcessor == null) return;
            irLeft.floatBuffer = audioProcessor.impulseResponseLeft;
            irRight.floatBuffer = audioProcessor.impulseResponseRight;
            spectreLeft.floatBuffer = audioProcessor.spectre;
            spectreRight.floatBuffer = audioProcessor.impulseResponseRight;
        }
    }
}
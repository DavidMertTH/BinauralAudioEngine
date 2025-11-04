using System;
using System.Collections.Generic;
using System.Threading;
using Code.Simulation;
using UnityEngine;

namespace Code.Renderer
{
    [RequireComponent(typeof(AudioSource))]
    public class BinauralAudioFilter : MonoBehaviour
    {
        private BinauralImpulseResponse _impulseResponse;
        private BinauralAudioEngine _audioEngine;

        private void Init(BinauralAudioEngine audioEngine)
        {
            _audioEngine = audioEngine;
        }
        
        private void OnEnable()
        {
                
        }


        public async Awaitable<List<AudioRay>> SimulateTargetedRays(CancellationToken ct)
        {
            
        }
        
        public async Awaitable UpdateImpulseResponse(List<AudioRay> rays, CancellationToken ct)
        {
            
        }
        
        /// <summary>
        /// Convolves Unity audio output with our impulse response to create the final result.
        /// Called by Unity on the audio thread.
        /// https://docs.unity3d.com/ScriptReference/MonoBehaviour.OnAudioFilterRead.html
        /// </summary>
        /// <param name="data">The audio data to be output in the next ~20ms. Channels are interleaved.</param>
        /// <param name="channels">The number of audio channels encoded in the <c>data</c> parameter.</param>
        private void OnAudioFilterRead(float[] data, int channels)
        {
            
        }
    }
}
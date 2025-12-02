using System;
using System.Threading.Tasks;
using Code.Core;
using Code.Simulation;
using UnityEngine;

namespace Code.Renderer
{
    [RequireComponent(typeof(LiveConvolutionReverb))]
    [RequireComponent(typeof(AudioSource))]
    public class BinauralAudioFilter : MonoBehaviour
    {
        public AudioSourceSimulationData SimulationData { get; } = new();
        private float[] _leftData;
        private float[] _rightData;
        private float[] _swapBuffer;
        private Task _leftConvolution = Task.CompletedTask;
        private Task _rightConvolution = Task.CompletedTask;
        private LiveConvolutionReverb _reverb;

        private void Awake() => _reverb = GetComponent<LiveConvolutionReverb>();
        private void OnEnable() => BinauralAudioEngine.Instance.RegisterAudioFilter(this);
        private void OnDisable() => BinauralAudioEngine.Instance?.UnregisterAudioFilter(this);
        private void OnDestroy() => SimulationData.Dispose();
        
        /// <summary>
        /// Called by Unity on the audio thread.
        /// Convolves Unity audio output with our impulse response to create the final result.
        /// The results will be available next time this method is called, which introduces a small delay.
        /// https://docs.unity3d.com/ScriptReference/MonoBehaviour.OnAudioFilterRead.html
        /// </summary>
        /// <param name="data">The audio data to be output in the next ~20ms. Channels are interleaved.</param>
        /// <param name="channels">The number of audio channels encoded in the <c>data</c> parameter.</param>
        private void OnAudioFilterRead(float[] data, int channels)
        {
            if (channels != 2 || !SimulationData.HasValidImpulseResponse) return;
            
            // Make sure buffers are allocated correctly
            var channelLength = data.Length / 2;
            _leftData = Helper.ReallocateIfNeeded(_leftData, channelLength);
            _rightData = Helper.ReallocateIfNeeded(_rightData, channelLength);
            _swapBuffer = Helper.ReallocateIfNeeded(_swapBuffer, data.Length);

            // Swap buffers: data <-> _left/rightData
            Array.Copy(data, _swapBuffer, data.Length);
            Task.WaitAll(_leftConvolution, _rightConvolution);
            Helper.InterlaceChannels(data, _rightData, _leftData);
            Helper.DeinterlaceChannels(_swapBuffer, _leftData, _rightData);

            if (BinauralAudioEngine.Instance.Settings.EnableHannFiltering)
            {
                Hann.ApplyHann(_leftData);
                Hann.ApplyHann(_rightData);
            }
            
            // Convolve result for next method call
            _leftConvolution = Task.Run(() =>
            {
                _leftData = _reverb.ProgressiveConvolve(
                    SimulationData.leftImpulseResponse.ToArray(),
                    _leftData,
                    _leftData,
                    LiveConvolutionReverb.Side.Left,
                    _leftData.Length);
            });

            _rightConvolution = Task.Run(() =>
            {
                _rightData = _reverb.ProgressiveConvolve(
                    SimulationData.rightImpulseResponse.ToArray(),
                    _rightData,
                    _rightData,
                    LiveConvolutionReverb.Side.Right,
                    _rightData.Length);
            });
        }
    }
}
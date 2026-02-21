using Code.Core;
using UnityEngine;

namespace Code.Renderer
{
    [RequireComponent(typeof(AudioSource))]
    public class BinauralAudioFilter : MonoBehaviour
    {
        private AudioSource _audioSource;
        private float[] _audioLeft;
        private float[] _audioRight;
        private int _dspBufferLength;

        private int DSPBufferCount =>
            _audioLeft != null && _dspBufferLength > 0 ? _audioLeft.Length / _dspBufferLength : 0;

        private int _playbackIndex;

        public float PlaybackPosition01
        {
            get => _playbackIndex / (float)DSPBufferCount;
            set => _playbackIndex = (int)(DSPBufferCount * value);
        }
        
        private float _volume = 1f;
        public float Volume
        {
            get => _volume;
            set => _volume = Mathf.Max(0, value);
        }

        public void SetAudio(float[] audioLeft, float[] audioRight)
        {
            _audioLeft = audioLeft;
            _audioRight = audioRight;
        }

        private void Awake()
        {
            AudioSettings.GetDSPBufferSize(out _dspBufferLength, out _);
            _audioSource = gameObject.GetComponent<AudioSource>();
            _audioSource.loop = true;
        }

        private void OnEnable()
        {
            BinauralAudioEngine.Instance.RegisterAudioFilter(this);
            _audioSource.Play();
        }

        private void OnDisable()
        {
            BinauralAudioEngine.Instance?.UnregisterAudioFilter(this);
            _audioSource.Stop();
        }

        private void OnDestroy()
        {
            Destroy(_audioSource);
        }

        private void OnAudioFilterRead(float[] data, int channels)
        {
            if (_audioLeft == null || _audioRight == null)
            {
                for (var i = 0; i < data.Length; i++) data[i] = 0;
                return;
            }

            for (var i = 0; i < data.Length; i++)
            {
                var bufferCounter = _playbackIndex * _dspBufferLength + i / 2;

                data[i] = i % 2 == 0 ? _audioRight[bufferCounter] : _audioLeft[bufferCounter];
                data[i] *= _volume;
            }

            _playbackIndex++;
            var numSegments = _audioLeft.Length / _dspBufferLength;
            _playbackIndex %= numSegments;
        }
    }
}
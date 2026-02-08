using Code.Core;
using Code.Simulation;
using UnityEngine;

namespace Code.Renderer
{
    [RequireComponent(typeof(AudioSource))]
    public class BinauralAudioFilter : MonoBehaviour
    {
        private void OnEnable() => BinauralAudioEngine.Instance.RegisterAudioFilter(this);
        private void OnDisable() => BinauralAudioEngine.Instance?.UnregisterAudioFilter(this);
    }
}
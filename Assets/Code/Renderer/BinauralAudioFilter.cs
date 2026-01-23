using Code.Core;
using Code.Simulation;
using UnityEngine;

namespace Code.Renderer
{
    [RequireComponent(typeof(AudioSource))]
    public class BinauralAudioFilter : MonoBehaviour
    {
        public AudioSourceSimulationData SimulationData { get; } = new();

        private void OnEnable() => BinauralAudioEngine.Instance.RegisterAudioFilter(this);
        private void OnDisable() => BinauralAudioEngine.Instance?.UnregisterAudioFilter(this);
        private void OnDestroy() => SimulationData.Dispose();
    }
}
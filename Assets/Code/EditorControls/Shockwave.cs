using Code.Renderer;
using UnityEngine;

namespace Code.EditorControls
{
    [RequireComponent(typeof(UnityEngine.Renderer))]
    public class Shockwave : MonoBehaviour
    {
        public AudioSourceObject sourceObject;
        public float duration = 1.0f;
        public float maxRadius = 0.8f;
        public float startRadius = 0.0f;

        private Material _mat;
        private float _time;

        void Awake()
        {
            _mat = GetComponent<UnityEngine.Renderer>().material;
            _time = 0f;
            _mat.SetFloat("_Radius", startRadius);
        }

        void Update()
        {
            if (!sourceObject.audioFilter.enabled) _time = 0;

            _time += Time.deltaTime;
            float t = Mathf.Clamp01(_time / duration);

            float eased = t * t * (3f - 2f * t);

            float radius = Mathf.Lerp(startRadius, maxRadius, eased);
            _mat.SetFloat("_Radius", radius);
            if (_time >= duration)
            {
                _time = 0;
            }
        }
    }
}
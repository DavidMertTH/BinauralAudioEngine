using TMPro;
using UnityEngine;

namespace Code.UI
{
    public class FPSCounter : MonoBehaviour
    {
        public TextMeshProUGUI fpsText;

        private float deltaTime = 0.0f;

        void Update()
        {
            deltaTime += (Time.unscaledDeltaTime - deltaTime) * 0.1f;

            float fps = 1.0f / deltaTime;
            fpsText.text = $"FPS: {Mathf.CeilToInt(fps)}";
        }
    }
}
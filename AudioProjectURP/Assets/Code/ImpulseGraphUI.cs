using UnityEngine;
using UnityEngine.UI;

namespace Code
{
    [RequireComponent(typeof(RawImage))]
    public class ImpulseGraphUI : MonoBehaviour
    {
        [Header("Graph Settings")] 
        [Range(0f, 1f)] public float zeroLinePosition = 0.35f;
        [Range(0f, 2f)] public float amplitudeScale = 0.8f;

        [Header("Colors")] 
        public Color lineColor;

        [Header("UI Elements")] 
        public RawImage graphDisplayUI;

        [HideInInspector] public float[] floatBuffer;

        // intern
        private int _currentResolution = -1;
        private float[] _downsampled;               // Puffer für Downsample-Ergebnis
        private Texture2D _irTex;                   // RFloat-Textur

        private Material _mat;
        private const float thickness = 0.0002f;
        private const float zeroLineThickness = 0.0002f;

        void Start()
        {
            _mat = new Material(Shader.Find("UI/ImpulseGraphShader"));
            _mat.hideFlags = HideFlags.DontSave;

            if (graphDisplayUI == null)
                graphDisplayUI = GetComponent<RawImage>();

            graphDisplayUI.material = _mat;
        }

        void Update()
        {
            if (floatBuffer == null || floatBuffer.Length == 0)
                return;

            // Ziel-Auflösung basierend auf der UI-Breite
            int targetResolution = Mathf.RoundToInt(graphDisplayUI.rectTransform.rect.width);
            targetResolution = Mathf.Clamp(targetResolution, 16, 2048);

            // Nur neu anlegen, wenn sich die Auflösung geändert hat
            if (_currentResolution != targetResolution)
            {
                _currentResolution = targetResolution;
                _downsampled = new float[targetResolution];

                if (_irTex != null)
                    Destroy(_irTex);

                // RFloat-Texture für direkten Float-Upload
                _irTex = new Texture2D(targetResolution, 1, TextureFormat.RFloat, false);
                _irTex.wrapMode = TextureWrapMode.Clamp;
                graphDisplayUI.texture = _irTex;
            }

            zeroLinePosition = Mathf.Clamp01(zeroLinePosition);
            amplitudeScale = Mathf.Max(0.001f, amplitudeScale);

            // Downsample mit Max-Abs-Pooling (inline, ohne neue Puffer pro Frame)
            float samplesPerBucket = (float)floatBuffer.Length / _currentResolution;
            for (int i = 0; i < _currentResolution; i++)
            {
                int start = Mathf.FloorToInt(i * samplesPerBucket);
                int end   = Mathf.Min(Mathf.CeilToInt((i + 1) * samplesPerBucket), floatBuffer.Length);
                float maxVal = 0f;
                for (int j = start; j < end; j++)
                {
                    float candidate = floatBuffer[j];
                    if (Mathf.Abs(candidate) > Mathf.Abs(maxVal))
                        maxVal = candidate;
                }
                _downsampled[i] = maxVal * amplitudeScale;
            }

            // Direkter Upload der Float-Daten in die RFloat-Textur
            _irTex.SetPixelData(_downsampled, 0);
            _irTex.Apply(false, false);

            // Shader-Uniforms
            _mat.SetFloat("_ZeroLine", zeroLinePosition);
            _mat.SetFloat("_ZeroLineThickness", zeroLineThickness);
            _mat.SetFloat("_Scale", amplitudeScale);
            _mat.SetFloat("_Thickness", thickness);
            _mat.SetColor("_LineColor", lineColor);

            graphDisplayUI.SetMaterialDirty();
        }
    }
}

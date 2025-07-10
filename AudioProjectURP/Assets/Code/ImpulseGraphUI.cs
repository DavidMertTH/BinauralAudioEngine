using UnityEngine;
using UnityEngine.UI;
using System;
using UnityEngine.Serialization;

namespace Code
{
    [RequireComponent(typeof(RawImage))]
    public class ImpulseGraphUI : MonoBehaviour
    {
        [Header("Graph Settings")] [Range(0f, 1f)]
        public float zeroLinePosition = 0.35f;

        [Range(0f, 2f)] public float amplitudeScale = 0.8f;

        [Header("Colors")] public Color lineColor = Color.green;

        [Header("UI Elements")] public RawImage graphDisplayUI;

        [HideInInspector] public float[] floatBuffer;

        float thickness = 0.0002f;
        float zeroLineThickness = 0.0002f;

        Material _mat;
        Texture2D _irTex;

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


            int targetResolution = Mathf.RoundToInt(graphDisplayUI.rectTransform.rect.width);
            targetResolution = Mathf.Clamp(targetResolution, 16, 2048); // Avoid extremely small/large values

            // ── 1) Downsample to match visual resolution ──
            float[] downsampled = Downsample(floatBuffer, targetResolution);

            // ── 2) Rebuild the texture if needed ──
            if (_irTex == null || _irTex.width != downsampled.Length)
            {
                if (_irTex != null) Destroy(_irTex);
                _irTex = new Texture2D(downsampled.Length, 1, TextureFormat.RFloat, false);
                _irTex.wrapMode = TextureWrapMode.Clamp;
                graphDisplayUI.texture = _irTex;
            }

            zeroLinePosition = Mathf.Clamp01(zeroLinePosition);
            amplitudeScale = Mathf.Max(0.001f, amplitudeScale);

            // ── 3) Fill texture with downsampled values ──
            var cols = new Color[downsampled.Length];
            for (int i = 0; i < cols.Length; i++)
                cols[i] = new Color(downsampled[i] * amplitudeScale, 0, 0, 0);
            _irTex.SetPixels(cols);
            _irTex.Apply();

            // ── 4) Push uniforms into the shader ──
            _mat.SetFloat("_ZeroLine", zeroLinePosition);
            _mat.SetFloat("_ZeroLineThickness", zeroLineThickness);
            _mat.SetFloat("_Scale", amplitudeScale);
            _mat.SetFloat("_Thickness", thickness);
            _mat.SetColor("_LineColor", lineColor);

            graphDisplayUI.SetMaterialDirty();
        }

        /// <summary>
        /// Downsamples the source array to a smaller array using max-abs-pooling (preserves peaks).
        /// </summary>
        float[] Downsample(float[] source, int targetSize)
        {
            if (source.Length <= targetSize)
                return source;

            float[] result = new float[targetSize];
            float samplesPerBucket = (float)source.Length / targetSize;

            for (int i = 0; i < targetSize; i++)
            {
                int start = Mathf.FloorToInt(i * samplesPerBucket);
                int end = Mathf.Min(Mathf.CeilToInt((i + 1) * samplesPerBucket), source.Length);
                float maxVal = 0f;

                for (int j = start; j < end; j++)
                {
                    float candidate = source[j];
                    if (Mathf.Abs(candidate) > Mathf.Abs(maxVal))
                        maxVal = candidate; // preserve sign
                }

                result[i] = maxVal;
            }

            return result;
        }
    }
}
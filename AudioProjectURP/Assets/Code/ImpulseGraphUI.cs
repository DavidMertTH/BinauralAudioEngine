using UnityEngine;
using UnityEngine.UI;

namespace Code
{
    public class ImpulseGraphUI : MonoBehaviour
    {
        [Header("Graph Settings")]
        public float heightScale = 1f;

        [Range(0f, 1f)]
        public float zeroLinePosition = 0.35f; // Vertical offset for the 0-line

        [Header("Colors")]
        public Color backgroundColor = new Color(0.1f, 0.1f, 0.1f, 1f);
        public Color leftColor = Color.green;
        public Color rightColor = Color.red;

        [Header("UI Elements")]
        public RawImage graphDisplayUI; // Assign this in the inspector

        [Header("Texture Settings")]
        public int textureWidth = 512;
        public int textureHeight = 256;

        [HideInInspector] public float[] impulseResponseLeft;
        [HideInInspector] public float[] impulseResponseRight;

        private RenderTexture _renderTexture;
        private Material _lineMaterial;

        void Start()
        {
            SetupRenderTexture();
            SetupMaterial();

            if (graphDisplayUI != null)
                graphDisplayUI.texture = _renderTexture;
        }

        void SetupRenderTexture()
        {
            _renderTexture = new RenderTexture(textureWidth, textureHeight, 0, RenderTextureFormat.ARGB32);
            _renderTexture.Create();
        }

        void SetupMaterial()
        {
            Shader shader = Shader.Find("Hidden/Internal-Colored");
            _lineMaterial = new Material(shader)
            {
                hideFlags = HideFlags.HideAndDontSave
            };

            _lineMaterial.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            _lineMaterial.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            _lineMaterial.SetInt("_Cull", (int)UnityEngine.Rendering.CullMode.Off);
            _lineMaterial.SetInt("_ZWrite", 0);
        }

        void Update()
        {
            bool hasLeft = impulseResponseLeft != null && impulseResponseLeft.Length > 0;
            bool hasRight = impulseResponseRight != null && impulseResponseRight.Length > 0;

            if (!hasLeft && !hasRight) return;

            Graphics.SetRenderTarget(_renderTexture);
            GL.Clear(true, true, backgroundColor);

            _lineMaterial.SetPass(0);
            GL.PushMatrix();
            GL.LoadPixelMatrix(0, textureWidth, 0, textureHeight);

            if (hasLeft)
                DrawImpulseResponse(impulseResponseLeft, leftColor);

            if (hasRight)
                DrawImpulseResponse(impulseResponseRight, rightColor);

            GL.PopMatrix();
            Graphics.SetRenderTarget(null);
        }

        void DrawImpulseResponse(float[] data, Color color)
        {
            int len = data.Length;
            float zeroLineY = textureHeight * zeroLinePosition;

            GL.Begin(GL.LINE_STRIP);
            GL.Color(color);

            for (int i = 0; i < len; i++)
            {
                float x = (i / (float)(len - 1)) * textureWidth;
                float y = zeroLineY + data[i] * heightScale * textureHeight * 0.5f;
                GL.Vertex3(x, y, 0);
            }

            GL.End();
        }
    }
}

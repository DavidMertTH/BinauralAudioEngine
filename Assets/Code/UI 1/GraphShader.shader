Shader "Custom/FloatArrayGraphURP"
{
    Properties
    {
        _LineColor      ("Line Color", Color) = (0,1,0,1)
        _BackgroundColor("Background Color", Color) = (0,0,0,1)
        _Thickness      ("Line Thickness", Range(0.001,0.1)) = 0.01
    }

    SubShader
    {
        Tags
        {
            "RenderType"="Opaque"
            "Queue"="Geometry"
            "RenderPipeline"="UniversalPipeline"
        }

        Pass
        {
            Name "Graph"
            Tags { "LightMode"="UniversalForward" }

            ZWrite On
            Cull Back
            Blend Off

            HLSLPROGRAM

            #pragma target 4.5
            #pragma vertex   vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv         : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float2 uv          : TEXCOORD0;
            };

            // --- Properties ---
            half4 _LineColor;
            half4 _BackgroundColor;
            float _Thickness;

            // --- Daten aus C# ---
            StructuredBuffer<float> _Samples; // float-Array
            int _SampleCount;                 // Länge des Arrays

            Varyings vert (Attributes IN)
            {
                Varyings OUT;
                OUT.positionHCS = TransformObjectToHClip(IN.positionOS);
                OUT.uv          = IN.uv;
                return OUT;
            }

            // Hilfsfunktion: Wert aus Samples mit linearer Interpolation holen
            float SampleGraph(float xNorm)
            {
                // xNorm in [0,1] -> Index in [0, _SampleCount-1]
                float idx = xNorm * max(_SampleCount - 1, 1);
                int i0 = (int)floor(idx);
                int i1 = min(i0 + 1, _SampleCount - 1);
                float t = idx - i0;

                float v0 = _Samples[i0];
                float v1 = _Samples[i1];

                return lerp(v0, v1, t); // erwarteter Wert im Bereich 0..1
            }

            half4 frag (Varyings IN) : SV_Target
            {
                float2 uv = IN.uv;

                if (_SampleCount <= 0)
                {
                    return _BackgroundColor;
                }

                // X -> Index im Array
                float yGraph = SampleGraph(uv.x);

                // Abstand zur Linie
                float dist = abs(uv.y - yGraph);

                // Weiche Kante über Thickness
                float alphaLine = smoothstep(_Thickness, 0.0, dist);

                half4 col = lerp(_BackgroundColor, _LineColor, alphaLine);
                return col;
            }

            ENDHLSL
        }
    }
}

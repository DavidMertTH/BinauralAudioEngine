Shader "UI/ImpulseGraphShader"
{
    Properties
    {
        _MainTex   ("IR Texture",   2D)   = "white" {}
        _ZeroLine  ("Zero Line",    Range(0,1)) = 0.35
        _ZeroLineThickness ("Zero Line Thickness", Range(0,0.002)) = 0.001
        _Scale     ("Amplitude Scale", Float)    = 1.0
        _Thickness ("Thickness (UV)", Range(0,0.1)) = 0.03
        _LineColor ("Wave Color",   Color) = (0,1,0,1)
    }

    SubShader
    {
        Tags {
            "Queue"="Transparent" "IgnoreProjector"="True"
            "RenderType"="Transparent"
        }
        ZWrite Off
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            #include "UnityUI.cginc"

            struct appdata_t {
                float4 vertex   : POSITION;
                float4 color    : COLOR;
                float2 texcoord : TEXCOORD0;
            };

            struct v2f {
                float4 pos : SV_POSITION;
                float2 uv  : TEXCOORD0;
                float4 col : COLOR;
            };

            sampler2D _MainTex;
            float4 _MainTex_TexelSize; 
            float4 _MainTex_ST;
            float _ZeroLine;
            float _ZeroLineThickness;
            float _Scale;
            float _Thickness;
            float4 _LineColor;

            v2f vert(appdata_t IN)
            {
                v2f OUT;
                OUT.pos = UnityObjectToClipPos(IN.vertex);
                OUT.uv  = TRANSFORM_TEX(IN.texcoord, _MainTex);
                OUT.col = IN.color;
                return OUT;
            }

            fixed4 frag(v2f IN) : SV_Target
            {
                float2 uv = IN.uv;

                // Sample IR texture
                float sampleLeft = tex2D(_MainTex, float2(uv.x, 0.5)).r;
                float sampleRight = tex2D(_MainTex, float2(uv.x + _MainTex_TexelSize.x, 0.5)).r;

                float yLeft = _ZeroLine + sampleLeft;
                float yRight = _ZeroLine + sampleRight;

                float2 p = float2(uv.x, yLeft);
                float2 q = float2(uv.x + _MainTex_TexelSize.x, yRight);
                float2 fragPos = uv;

                float2 pq = q - p;
                float2 pf = fragPos - p;

                float t = clamp(dot(pf, pq) / dot(pq, pq), 0.0, 1.0);
                float2 closest = p + t * pq;

                float dist = distance(closest, fragPos);

                // Adaptive sharpness
                float pixelSize = fwidth(fragPos.y);
                float waveformAlpha = smoothstep(_Thickness + pixelSize, pixelSize, dist);

                // Zero line blending (same color, softer alpha)
                float zeroDist = abs(uv.y - _ZeroLine);
                float zeroAlpha = step(zeroDist, _ZeroLineThickness);

                float finalAlpha = waveformAlpha + (1.0 - waveformAlpha) * zeroAlpha;
                return float4(_LineColor.rgb, _LineColor.a * finalAlpha);
            }
            ENDCG
        }
    }
}

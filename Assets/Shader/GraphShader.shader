Shader "Custom/GraphShaderUI"
{
    Properties
    {
        _Color ("Line Color", Color) = (0, 1, 0, 1)
        _LineThickness ("Line Thickness", Float) = 0.005
        _StencilComp ("Stencil Comparison", Float) = 8
        _Stencil ("Stencil ID", Float) = 0
        _StencilOp ("Stencil Operation", Float) = 0
        _StencilWriteMask ("Stencil Write Mask", Float) = 255
        _StencilReadMask ("Stencil Read Mask", Float) = 255
        _ColorMask ("Color Mask", Float) = 15
    }
    SubShader
    {
        Tags
        {
            "Queue"="Transparent"
            "IgnoreProjector"="True"
            "RenderType"="Transparent"
            "PreviewType"="Plane"
            "CanUseSpriteAtlas"="True"
        }

        Stencil
        {
            Ref [_Stencil]
            Comp [_StencilComp]
            Pass [_StencilOp]
            ReadMask [_StencilReadMask]
            WriteMask [_StencilWriteMask]
        }

        Cull Off
        Lighting Off
        ZWrite Off
        ZTest [unity_GUIZTestMode]
        Blend SrcAlpha OneMinusSrcAlpha
        ColorMask [_ColorMask]

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 4.5
            #include "UnityCG.cginc"
            #include "UnityUI.cginc"

            StructuredBuffer<float> _DataBuffer;
            int   _DataCount;
            float _MinValue;
            float _MaxValue;
            float4 _Color;
            float  _LineThickness;
            float4 _ClipRect;

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv     : TEXCOORD0;
                float4 color  : COLOR;
            };

            struct v2f
            {
                float4 pos      : SV_POSITION;
                float2 uv       : TEXCOORD0;
                float4 color    : COLOR;
                float4 worldPos : TEXCOORD1;
            };

            v2f vert(appdata v)
            {
                v2f o;
                o.pos      = UnityObjectToClipPos(v.vertex);
                o.uv       = v.uv;
                o.color    = v.color;
                o.worldPos = v.vertex;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                if (!UnityGet2DClipping(i.worldPos.xy, _ClipRect))
                    discard;

                float idx    = i.uv.x * (_DataCount - 1);
                int   i0     = (int)idx;
                int   i1     = min(i0 + 1, _DataCount - 1);
                float t      = idx - i0;

                float v0     = (_DataBuffer[i0] - _MinValue) / (_MaxValue - _MinValue);
                float v1     = (_DataBuffer[i1] - _MinValue) / (_MaxValue - _MinValue);
                float graphY = lerp(v0, v1, t);

                // 0 liegt immer in der Mitte (da MinValue == -MaxValue)
                float zero   = (_MinValue * -1.0) / (_MaxValue - _MinValue);

                // Gefüllter Bereich zwischen 0 und graphY
                float filled = step(min(zero, graphY), i.uv.y)
                             * step(i.uv.y, max(zero, graphY));

                // Weiche Kante oben/unten an der Linie
                float dist  = abs(i.uv.y - graphY);
                float edge  = 1.0 - smoothstep(0.0, _LineThickness, dist);
                float alpha = saturate(filled + edge);

                return fixed4(_Color.rgb, _Color.a * alpha);
            }
            ENDCG
        }
    }
}
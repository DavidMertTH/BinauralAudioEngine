Shader "Custom/ShockwaveDistortionURP"
{
    Properties
    {
        _Radius   ("Radius",   Range(0,1)) = 0.0
        _Width    ("Width",    Range(0,1)) = 0.1
        _Strength ("Strength", Range(0,0.1)) = 0.02
        _Alpha    ("Alpha",    Range(0,1)) = 1.0
    }

    SubShader
    {
        Tags
        {
            "RenderType"="Transparent"
            "Queue"="Transparent"
            "RenderPipeline"="UniversalPipeline"
        }

        Pass
        {
            Name "Forward"
            Tags { "LightMode"="UniversalForward" }

            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off

            HLSLPROGRAM

            #pragma vertex   vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            // --- Properties ---
            float _Radius;
            float _Width;
            float _Strength;
            float _Alpha;

            // --- Textur mit der bereits gerenderten Szene ---
            TEXTURE2D(_CameraOpaqueTexture);
            SAMPLER(sampler_CameraOpaqueTexture);

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv         : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float2 uv          : TEXCOORD0;
                float4 screenPos   : TEXCOORD1;
            };

            Varyings vert (Attributes IN)
            {
                Varyings OUT;
                OUT.positionHCS = TransformObjectToHClip(IN.positionOS);
                OUT.uv         = IN.uv;
                OUT.screenPos  = ComputeScreenPos(OUT.positionHCS);
                return OUT;
            }

            float ringMask(float dist, float radius, float width)
            {
                // innen und außen des Ringes
                float inner = radius - width;
                float outer = radius + width;

                // weiche Kante nach innen
                float innerStep = smoothstep(inner, radius, dist);
                // weiche Kante nach außen
                float outerStep = smoothstep(radius, outer, dist);

                // Ring = innen an, außen wieder aus
                return saturate(innerStep * (1.0 - outerStep));
            }

            half4 frag (Varyings IN) : SV_Target
            {
                // Normalisierte Screen-UVs
                float2 screenUV = IN.screenPos.xy / IN.screenPos.w;

                // Mittelpunkt der Welle in der Mitte des Mesh-UVs
                float2 centeredUV = IN.uv - 0.5;
                float dist = length(centeredUV);

                // Ringprofil (Maske)
                float mask = ringMask(dist, _Radius, _Width);

                // Richtung von der Mitte nach außen
                float2 dir = (dist > 0.0001) ? centeredUV / dist : float2(0,0);

                // Offset abhängig von Maske und Stärke
                float2 offset = dir * _Strength * mask;

                float2 distortedUV = screenUV + offset;

                // Szene an der verzerrten Position abtasten
                half4 col = SAMPLE_TEXTURE2D(_CameraOpaqueTexture, sampler_CameraOpaqueTexture, distortedUV);

                // Alpha: nur im Ring sichtbar
                col.a = mask * _Alpha;

                return col;
            }

            ENDHLSL
        }
    }
}

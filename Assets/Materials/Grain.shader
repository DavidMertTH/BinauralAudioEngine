Shader "Custom/UnlitSaltPepperNoise"
{
    Properties
    {
        _Color ("Base Color", Color) = (1,1,1,1)
        _NoiseScale ("Noise Scale", Range(1, 4000)) = 20
        _NoiseAmount ("Noise Amount", Range(0, 1)) = 0.5
        _NoiseThreshold ("Noise Threshold", Range(0, 1)) = 0.5
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        LOD 100

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float2 uv : TEXCOORD0;
                float4 vertex : SV_POSITION;
            };

            float4 _Color;
            float _NoiseScale;
            float _NoiseAmount;
            float _NoiseThreshold;

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                return o;
            }

            // Simple hash function for noise
            float hash(float2 p)
            {
                p = frac(p * float2(123.34, 456.21));
                p += dot(p, p + 45.32);
                return frac(p.x * p.y);
            }

            fixed4 frag (v2f i) : SV_Target
            {
                // Scale UV coordinates for noise
                float2 noiseUV = i.uv * _NoiseScale;
                
                // Generate noise value
                float noise = hash(floor(noiseUV));
                
                // Create salt and pepper effect
                float saltPepper = 0;
                if (noise > _NoiseThreshold)
                {
                    saltPepper = 1; // White (salt)
                }
                else if (noise < (1 - _NoiseThreshold))
                {
                    saltPepper = -1; // Black (pepper)
                }
                
                // Mix base color with noise
                float4 col = _Color;
                col.rgb += saltPepper * _NoiseAmount;
                col.rgb = saturate(col.rgb);
                
                return col;
            }
            ENDCG
        }
    }
}
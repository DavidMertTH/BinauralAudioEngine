Shader "Custom/Shockwave"
{
    Properties
    {
        _DistortionStrength ("Distortion Strength", Range(0, 1)) = 0.1
        _WaveWidth ("Wave Width", Range(0, 1)) = 0.1
        _WaveSpeed ("Wave Speed", Float) = 1.0
        _Center ("Center (XY)", Vector) = (0.5, 0.5, 0, 0)
        _Radius ("Radius", Range(0, 2)) = 0.5
    }
    
    SubShader
    {
        Tags 
        { 
            "Queue"="Transparent" 
            "RenderType"="Transparent"
            "IgnoreProjector"="True"
        }
        
        GrabPass { "_GrabTexture" }
        
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
                float4 pos : SV_POSITION;
                float4 grabPos : TEXCOORD0;
                float2 uv : TEXCOORD1;
            };

            sampler2D _GrabTexture;
            float4 _GrabTexture_TexelSize;
            float _DistortionStrength;
            float _WaveWidth;
            float _WaveSpeed;
            float4 _Center;
            float _Radius;

            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.grabPos = ComputeGrabScreenPos(o.pos);
                o.uv = v.uv;
                return o;
            }

            half4 frag(v2f i) : SV_Target
            {
                float2 center = _Center.xy;
                float dist = distance(i.uv, center);
                float animatedRadius = frac(_Time.y * _WaveSpeed) * 2.0;
                float waveDist = abs(dist - animatedRadius);
                float waveMask = 1.0 - smoothstep(0, _WaveWidth, waveDist);
                float2 direction = normalize(i.uv - center);
                float2 distortion = direction * waveMask * _DistortionStrength;
                float2 grabUV = i.grabPos.xy / i.grabPos.w;
                grabUV += distortion;
                half4 col = tex2D(_GrabTexture, grabUV);
                col.rgb += waveMask * 0.2;
                
                return col;
            }
            ENDCG
        }
    }
    
    FallBack "Transparent/Diffuse"
}

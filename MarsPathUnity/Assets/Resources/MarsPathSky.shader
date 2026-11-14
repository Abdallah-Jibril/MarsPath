Shader "MarsPath/Sky"
{
    Properties
    {
        _ZenithColor ("Zenith", Color) = (0.58,0.46,0.39,1)
        _HorizonColor ("Dust Horizon", Color) = (0.85,0.72,0.60,1)
        _SunColor ("Sun Glow", Color) = (1.0,0.78,0.52,1)
    }
    SubShader
    {
        Tags { "Queue"="Background" "RenderType"="Background" "PreviewType"="Skybox" }
        Cull Off ZWrite Off
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            fixed4 _ZenithColor;
            fixed4 _HorizonColor;
            fixed4 _SunColor;
            struct appdata { float4 vertex : POSITION; };
            struct v2f { float4 position : SV_POSITION; float3 direction : TEXCOORD0; };

            v2f vert(appdata input)
            {
                v2f output;
                output.position = UnityObjectToClipPos(input.vertex);
                output.direction = input.vertex.xyz;
                return output;
            }

            fixed4 frag(v2f input) : SV_Target
            {
                float3 direction = normalize(input.direction);
                float horizon = pow(saturate(1.0 - abs(direction.y)), 5.0);
                float lowerDust = saturate(-direction.y * 3.0 + 0.65);
                float3 color = lerp(_ZenithColor.rgb, _HorizonColor.rgb, horizon);
                color = lerp(color, _HorizonColor.rgb * 0.82, lowerDust * 0.35);
                float3 sunDirection = normalize(float3(-0.42, 0.56, 0.71));
                float sun = pow(saturate(dot(direction, sunDirection)), 420.0);
                float glow = pow(saturate(dot(direction, sunDirection)), 18.0) * 0.18;
                color += _SunColor.rgb * (sun * 1.8 + glow);
                return fixed4(color, 1);
            }
            ENDCG
        }
    }
}

Shader "MarsPath/Global Mars"
{
    Properties
    {
        _MainTex ("NASA Viking global mosaic", 2D) = "white" {}
        _Grid ("Coordinate grid", Float) = 1
        _LimbColor ("Limb color", Vector) = (0.21, 0.09, 0.045, 1)
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0
            #include "UnityCG.cginc"
            sampler2D _MainTex;
            float _Grid;
            float4 _LimbColor;
            struct appdata { float4 vertex : POSITION; float3 normal : NORMAL; float2 uv : TEXCOORD0; };
            struct v2f { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; float3 normal : TEXCOORD1; float3 worldPos : TEXCOORD2; };
            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                o.normal = UnityObjectToWorldNormal(v.normal);
                o.worldPos = mul(unity_ObjectToWorld, v.vertex).xyz;
                return o;
            }
            fixed4 frag(v2f i) : SV_Target
            {
                float3 n = normalize(i.normal);
                float3 viewDirection = normalize(_WorldSpaceCameraPos - i.worldPos);
                // Broad illumination keeps surface features readable on both hemispheres.
                float3 lightDirection = normalize(viewDirection + float3(-0.55, 0.70, 0.12));
                float diffuse = saturate(dot(n, lightDirection));
                float3 surface = tex2Dbias(_MainTex, float4(i.uv, 0, -.20)).rgb;
                float3 color = surface * (0.30 + diffuse * 1.25);
                float limb = pow(1 - saturate(dot(n, viewDirection)), 3.5);
                color += _LimbColor.rgb * limb * diffuse;
                float2 gridUV = i.uv * float2(12, 6);
                float2 cell = abs(frac(gridUV + 0.5) - 0.5);
                float2 width = max(fwidth(gridUV), float2(0.0001, 0.0001));
                float2 lines = 1 - smoothstep(width * 0.35, width * 1.15, cell);
                float gridStrength = max(lines.x, lines.y) * _Grid * 0.16;
                color = lerp(color, float3(0.38, 0.67, 0.69), gridStrength);
                return fixed4(color, 1);
            }
            ENDCG
        }
    }
}

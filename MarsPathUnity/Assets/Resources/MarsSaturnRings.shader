Shader "MarsPath/Saturn Rings"
{
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" }
        Cull Off
        ZWrite Off
        Blend SrcAlpha OneMinusSrcAlpha
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            struct appdata { float4 vertex : POSITION; float2 uv : TEXCOORD0; };
            struct v2f { float4 position : SV_POSITION; float x : TEXCOORD0; };
            v2f vert(appdata v)
            {
                v2f o;
                o.position = UnityObjectToClipPos(v.vertex);
                o.x = v.uv.x;
                return o;
            }
            fixed4 frag(v2f i) : SV_Target
            {
                float inner = smoothstep(.02, .2, i.x);
                float division = smoothstep(.016, .045, abs(i.x - .68));
                float edge = 1 - smoothstep(.93, .99, i.x);
                float bands = .74 + .11 * sin(i.x * 99) + .08 * sin(i.x * 226);
                return fixed4(lerp(float3(.47,.35,.24),float3(.79,.68,.50),inner), inner * division * edge * bands);
            }
            ENDCG
        }
    }
}

Shader "MarsPath/Rock"
{
    Properties
    {
        _BaseColor ("Basalt Color", Color) = (0.22,0.10,0.06,1)
        _DustColor ("Mars Dust", Color) = (0.58,0.30,0.16,1)
        _Variation ("Weathering", Range(0,1)) = 0.6
        _Seed ("Rock Seed", Float) = 0
        _GrainTex ("NASA MAHLI surface grain", 2D) = "gray" {}
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Geometry" }
        LOD 300
        CGPROGRAM
        #pragma surface surf Standard fullforwardshadows addshadow
        #pragma target 3.0

        fixed4 _BaseColor;
        fixed4 _DustColor;
        half _Variation;
        float _Seed;
        sampler2D _GrainTex;

        struct Input
        {
            float3 worldPos;
            float3 worldNormal;
            fixed4 color : COLOR;
        };

        float Hash31(float3 p)
        {
            p = frac(p * 0.1031);
            p += dot(p, p.yzx + 33.33);
            return frac((p.x + p.y) * p.z);
        }

        float Noise3(float3 p)
        {
            float3 cell = floor(p);
            float3 local = frac(p);
            local = local * local * (3.0 - 2.0 * local);
            float n000 = Hash31(cell + float3(0, 0, 0));
            float n100 = Hash31(cell + float3(1, 0, 0));
            float n010 = Hash31(cell + float3(0, 1, 0));
            float n110 = Hash31(cell + float3(1, 1, 0));
            float n001 = Hash31(cell + float3(0, 0, 1));
            float n101 = Hash31(cell + float3(1, 0, 1));
            float n011 = Hash31(cell + float3(0, 1, 1));
            float n111 = Hash31(cell + float3(1, 1, 1));
            float nx00 = lerp(n000, n100, local.x);
            float nx10 = lerp(n010, n110, local.x);
            float nx01 = lerp(n001, n101, local.x);
            float nx11 = lerp(n011, n111, local.x);
            return lerp(lerp(nx00, nx10, local.y), lerp(nx01, nx11, local.y), local.z);
        }

        void surf(Input input, inout SurfaceOutputStandard output)
        {
            float3 samplePosition = input.worldPos + float3(_Seed * 0.73, _Seed * 0.19, _Seed * 0.41);
            float macro = Noise3(samplePosition * 0.72);
            float grain = Noise3(samplePosition * 3.6 + 8.2);
            float fine = Noise3(samplePosition * 11.0 - 3.7);
            float upward = saturate(normalize(input.worldNormal).y * 0.9 + 0.1);
            float dustMask = smoothstep(0.38, 0.92, upward + (macro - 0.5) * 0.42);
            dustMask *= lerp(0.30, 0.78, _Variation) * lerp(0.72, 1.0, grain);

            fixed3 basalt = _BaseColor.rgb * lerp(0.88, 1.20, macro);
            fixed3 dusty = _DustColor.rgb * lerp(0.90, 1.16, grain);
            fixed3 albedo = lerp(basalt, dusty, saturate(dustMask));
            albedo *= lerp(fixed3(1, 1, 1), input.color.rgb, 0.72) * lerp(0.94, 1.08, fine);
            float2 stoneUv=input.worldPos.xz*1.7+input.worldPos.yy*.31;
            stoneUv=1-abs(frac(stoneUv*.5)*2-1);
            float grainLuma=dot(tex2D(_GrainTex,stoneUv).rgb,fixed3(.3,.59,.11));
            float meanLuma=dot(tex2Dlod(_GrainTex,float4(stoneUv,0,5)).rgb,fixed3(.3,.59,.11));
            albedo*=clamp(1+(grainLuma-meanLuma)*2.8,.65,1.3);
            // A tiny diffuse fill represents light scattered by the dusty Martian
            // atmosphere and keeps shaded boulders readable without looking emissive.
            fixed3 atmosphericFill = lerp(_BaseColor.rgb, _DustColor.rgb, 0.30) * 0.14;

            output.Albedo = albedo;
            output.Emission = atmosphericFill;
            output.Metallic = 0;
            output.Smoothness = lerp(0.018, 0.065, fine) * (1.0 - dustMask * 0.45);
            output.Occlusion = lerp(0.94, 1.0, macro);
            output.Alpha = 1;
        }
        ENDCG
    }
    FallBack "Diffuse"
}

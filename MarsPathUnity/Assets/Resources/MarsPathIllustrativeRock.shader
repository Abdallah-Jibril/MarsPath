Shader "MarsPath/Illustrative Rock"
{
    Properties
    {
        _BaseColor ("Stone", Color) = (0.34,0.27,0.23,1)
        _DustColor ("Dust coating", Color) = (0.66,0.47,0.34,1)
        _Variation ("Weathering", Range(0,1)) = 0.6
        _Bedding ("Visible bedding", Range(0,1)) = 1
        _RoverGrain ("Jezero rock photo", Range(0,1)) = 0
        _MicroRelief ("Modeled surface relief", Range(0,1)) = 1
        _GrainTex ("NASA rover rock grain", 2D) = "gray" {}
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Geometry" }
        LOD 250
        CGPROGRAM
        #pragma surface surf Standard fullforwardshadows addshadow vertex:vert
        #pragma target 3.0
        #pragma multi_compile_instancing

        fixed4 _BaseColor, _DustColor;
        half _Variation, _Bedding, _RoverGrain, _MicroRelief;
        sampler2D _GrainTex;
        struct Input
        {
            float3 rockPosition;
            float3 rockNormal;
            float3 worldPos;
            float3 worldNormal;
            fixed4 color : COLOR;
            INTERNAL_DATA
        };
        void vert(inout appdata_full vertex, out Input output)
        {
            UNITY_INITIALIZE_OUTPUT(Input, output);
            output.rockPosition = vertex.vertex.xyz;
            output.rockNormal = vertex.normal;
        }

        float2 GrainUv(float2 uv)
        {
            // Reflect the rectangular WATSON crop at its edges so it tiles
            // across a face without a visible photographic seam.
            float2 reflected = 1 - abs(frac(uv * .5) * 2 - 1);
            return lerp(uv, .035 + .93 * reflected, _RoverGrain);
        }

        void surf(Input input, inout SurfaceOutputStandard output)
        {
            // Object coordinates keep grain attached to a stone through origin
            // rebases. The WATSON photo contributes surface grain and mottling.
            float phase = input.color.a * _RoverGrain;
            float3 p = input.rockPosition * lerp(3.6, 5.7, _RoverGrain) + phase * float3(1.13, 2.27, 1.71);
            float3 weights = pow(abs(normalize(input.rockNormal)), 4);
            weights /= max(0.001, weights.x + weights.y + weights.z);
            half3 textureColor = tex2D(_GrainTex, GrainUv(p.yz)).rgb * weights.x
                + tex2D(_GrainTex, GrainUv(p.xz)).rgb * weights.y
                + tex2D(_GrainTex, GrainUv(p.xy)).rgb * weights.z;
            half luminance = dot(textureColor, half3(.30,.59,.11));
            float grain = lerp(luminance, saturate(luminance * 2.55 + .055), _RoverGrain);
            float3 bed = input.rockPosition * 3.6;
            float strata = bed.y * 5.9 + bed.x * 1.3 + bed.z * .7 + grain * 1.1 + phase;
            float layers = sin(strata + sin(strata * .43) * .68) * .5 + .5;
            float3 normal = normalize(WorldNormalVector(input, float3(0, 0, 1)));
            float upward = saturate(normal.y);
            float dust = smoothstep(.45, .96, upward + (grain - .5) * .38) * lerp(.26, .69, _Variation);
            half3 tint = 1 + (textureColor - luminance) * (2.0 * _RoverGrain);
            half3 stone = _BaseColor.rgb * lerp(.59, 1.36, grain) * lerp(1, lerp(.76, 1.13, layers), _Bedding) * tint;
            half3 albedo = lerp(stone, _DustColor.rgb * lerp(.80, 1.14, grain), dust);
            output.Albedo = albedo * lerp(half3(1,1,1), input.color.rgb, .78);
            // Interpret photographic contrast as shallow material relief. It is
            // applied only to lighting, never to model or terrain elevation.
            float relief = (grain * .0050 + layers * _Bedding * .0027) * _RoverGrain * _MicroRelief;
            float3 dx = ddx(input.worldPos), dy = ddy(input.worldPos);
            float3 across = cross(dy, normal), along = cross(normal, dx);
            float area = dot(dx, across);
            float3 bumped = normalize(max(abs(area), 1e-10) * normal
                - sign(area) * (ddx(relief) * across + ddy(relief) * along));
            output.Normal = float3(dot(bumped, normalize(WorldNormalVector(input, float3(1, 0, 0)))),
                dot(bumped, normalize(WorldNormalVector(input, float3(0, 1, 0)))), dot(bumped, normal));
            output.Emission = _BaseColor.rgb * lerp(.032, .017, _RoverGrain);
            output.Metallic = 0;
            output.Smoothness = lerp(0.018, 0.055, grain);
            output.Occlusion = lerp(.77, 1, saturate(.48 * grain + input.color.r - .38));
            output.Alpha = 1;
        }
        ENDCG
    }
    FallBack "Diffuse"
}

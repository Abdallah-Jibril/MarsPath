Shader "MarsPath/Surface"
{
    Properties
    {
        _Color ("Tint", Color) = (1,1,1,1)
        [HideInInspector] _PlanningDetailRect("Close-up bounds",Vector)=(0,0,0,0)
        [HideInInspector] _DetailCutoutEnabled("Close-up replaces overview",Float)=0
        _MainTex ("Surface Texture", 2D) = "white" {}
        _BumpMap ("Surface Normal", 2D) = "bump" {}
        _BumpScale ("Normal Strength", Range(0,2)) = 0
        _Smoothness ("Smoothness", Range(0,1)) = 0.08
        _RegionalBlend ("Regional Context Blend", Range(0,1)) = 0
        _DetailMode ("Regional surface detail", Range(0,1)) = 0
        _GroundTex ("Visual soil detail", 2D) = "gray" {}
        _OrthoBlend ("Geographic CTX image detail", Range(0,1)) = 0
        _PlanetaryRadius ("Regional planetary radius", Float) = 0
        _PlanetaryCutout ("Regional detail opening", Float) = 0
        _DepthOffsetUnits ("Regional seam depth offset", Float) = 0
        _OrbitalTex ("Selected NASA orbital imagery", 2D) = "black" {}
        _OrbitalRect ("Geographic image UV rectangle", Vector) = (0,0,0,0)
        [HideInInspector] _OrbitalPrecise("Local native image registration",Float)=0
        [HideInInspector] _OrbitalUvAnchor("Local image UV and angular scale",Vector)=(0,0,0,0)
        [HideInInspector] _OrbitalGeoFrame("Origin latitude sine cosine and angular scale",Vector)=(0,1,0,0)
        _OrbitalBlend ("Selected imagery coverage", Range(0,1)) = 0
        _OrbitalMono ("Colorize grayscale CTX with Viking chroma", Float) = 0
        _MapLayer ("Map imagery / shaded relief / sampled slope", Float) = 0
        _MapReliefScale ("Map vertical display multiplier", Float) = 1
        _GeodesicUv ("Exact per-pixel geographic map image coordinates",Float)=0
        _GeoFrame ("Latitude longitude radians and angular scale",Vector)=(0,0,0,0)
        _SourceImageSpacing("Legacy image metres per pixel",Float)=200
        _CtxOverviewTex("NASA CTX photographic overview",2D)="black"{}
        _CtxOverviewRect("CTX overview geographic rectangle",Vector)=(0,0,0,0)
        _CtxOverviewBlend("CTX overview coverage",Float)=0
        _CtxOverviewSpacing("CTX overview metres per pixel",Float)=1000
        _CtxViewTex("NASA CTX visible view photograph",2D)="black"{}
        _CtxViewRect("CTX visible view geographic rectangle",Vector)=(0,0,0,0)
        _CtxViewBlend("CTX visible view coverage",Float)=0
        _CtxViewSpacing("CTX visible view metres per pixel",Float)=1000
        _CtxDetailTex("NASA CTX photographic detail",2D)="black"{}
        _CtxDetailRect("CTX detail geographic rectangle",Vector)=(0,0,0,0)
        _CtxDetailBlend("CTX detail coverage",Float)=0
        _CtxDetailSpacing("CTX detail metres per pixel",Float)=1000
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Geometry" }
        LOD 300
        Offset 0, [_DepthOffsetUnits]
        CGPROGRAM
        #pragma surface surf Standard fullforwardshadows addshadow
        #pragma target 3.0
        #include "UnityCG.cginc"
        #include "MarsCtxAtlas.cginc"

        sampler2D _MainTex;
        sampler2D _BumpMap;
        sampler2D _GroundTex;
        sampler2D _OrbitalTex;
        float4 _OrbitalRect;
        float _OrbitalPrecise;
        float4 _OrbitalUvAnchor,_OrbitalGeoFrame;
        float _OrbitalBlend,_OrbitalMono;
        float _MapLayer,_MapReliefScale;
        float _GeodesicUv;float4 _GeoFrame;
        float _SourceImageSpacing;
        fixed4 _Color;
        float4 _PlanningDetailRect;
        float _DetailCutoutEnabled;
        half _BumpScale;
        half _Smoothness;
        half _RegionalBlend;
        half _DetailMode;
        half _OrthoBlend;
        float _PlanetaryRadius,_PlanetaryCutout;

        struct Input
        {
            float2 uv_MainTex;
            float2 uv_BumpMap;
            float2 uv2_OrbitalTex;
            float3 worldPos;
            float3 worldNormal;
            INTERNAL_DATA
            fixed4 color : COLOR;
        };

        float Hash21(float2 samplePoint)
        {
            samplePoint = frac(samplePoint * float2(123.34, 456.21));
            samplePoint += dot(samplePoint, samplePoint + 45.32);
            return frac(samplePoint.x * samplePoint.y);
        }

        float Noise2(float2 samplePoint)
        {
            float2 cell = floor(samplePoint);
            float2 local = frac(samplePoint);
            local = local * local * (3.0 - 2.0 * local);
            float a = Hash21(cell);
            float b = Hash21(cell + float2(1, 0));
            float c = Hash21(cell + float2(0, 1));
            float d = Hash21(cell + float2(1, 1));
            return lerp(lerp(a, b, local.x), lerp(c, d, local.x), local.y);
        }

        float2 PreciseOrbitalUv(float2 position,float2 fallback)
        {
            if(_OrbitalPrecise<.5)return fallback;
            float radius=length(position),angle=radius*_OrbitalGeoFrame.z;
            float2 displacement=sin(angle)*position/max(radius,1e-7);
            float co=cos(angle),sp=_OrbitalGeoFrame.x,cp=_OrbitalGeoFrame.y;
            float latNorth=cp*co-sp*displacement.y;
            if(latNorth<=.0001)return fallback;
            float cosLatitude=sqrt(latNorth*latNorth+displacement.x*displacement.x);
            float correction=displacement.x*displacement.x/max(1e-7,cosLatitude+latNorth);
            float latitudeDelta=atan2(displacement.y-sp*correction,co+cp*correction);
            float longitudeDelta=atan2(displacement.x,latNorth);
            return _OrbitalUvAnchor.xy+float2(longitudeDelta,latitudeDelta)*_OrbitalUvAnchor.zw;
        }

        void surf(Input input, inout SurfaceOutputStandard output)
        {
            if(_DetailCutoutEnabled>.5)
            {
                float2 p=input.worldPos.xz-_PlanningDetailRect.xy;
                if(p.x>0&&p.y>0&&p.x<_PlanningDetailRect.z&&p.y<_PlanningDetailRect.w)clip(-1);
            }
            if(_PlanetaryCutout>0)
            {
                float3 radial=input.worldPos+float3(0,_PlanetaryRadius+1.5,0);
                float horizontal=length(radial.xz);
                float angle=atan2(horizontal,radial.y);
                float2 projected=radial.xz/max(0.00001,horizontal)*angle*_PlanetaryRadius;
                clip(max(abs(projected.x),abs(projected.y))-_PlanetaryCutout);
            }
            float2 imageUv=input.uv_MainTex,geoUv=input.uv2_OrbitalTex;
            if(_GeodesicUv>.5)
            {
                // Wide context rings need true map projection per fragment.
                // Interpolated geographic UVs stretched images into radial strips.
                float radius=length(input.worldPos.xz),angle=radius*_GeoFrame.z;
                float2 bearing=input.worldPos.xz/max(radius,1e-7);
                float cp=cos(_GeoFrame.x),sp=sin(_GeoFrame.x),cl=cos(_GeoFrame.y),sl=sin(_GeoFrame.y);
                float3 centre=float3(cp*cl,cp*sl,sp),north=float3(-sp*cl,-sp*sl,cp),east=float3(-sl,cl,0);
                float3 globe=centre*cos(angle)+(north*bearing.y+east*bearing.x)*sin(angle);
                float latitude=atan2(globe.z,length(globe.xy)),longitude=atan2(globe.y,globe.x);
                geoUv=float2(longitude/6.28318530718+.5,latitude/3.14159265359+.5);imageUv=geoUv;
            }
            fixed4 surface = tex2D(_MainTex, imageUv) * _Color * input.color;
            float orbitalCoverage=0,selectedLuma=0.18;
            if(_OrbitalBlend>0&&_OrbitalRect.z>0&&_OrbitalRect.w>0)
            {
                float2 geographic=geoUv;
                geographic.x+=round(_OrbitalRect.x+_OrbitalRect.z*0.5-geographic.x);
                float2 regionalUv=(geographic-_OrbitalRect.xy)/_OrbitalRect.zw;
                regionalUv=PreciseOrbitalUv(input.worldPos.xz,regionalUv);
                float2 edge=min(regionalUv,1-regionalUv);
                orbitalCoverage=_OrbitalBlend*smoothstep(0,0.025,min(edge.x,edge.y));
                // Missing source pixels carry zero alpha. Normalize mip-filtered
                // colour by coverage so blank tiles cannot create dark borders.
                fixed4 selectedSample=tex2D(_OrbitalTex,regionalUv);
                fixed3 selected=selectedSample.rgb/max(0.025,selectedSample.a);
                orbitalCoverage*=smoothstep(0.02,0.60,selectedSample.a);
                selectedLuma=dot(selected,fixed3(0.299,0.587,0.114));
                // Source alpha carries nodata. Dark shadows and bright real
                // terrain retain coverage instead of exposing fallback sand.
                // Display-stretched monochrome retains its registered image
                // contrast; only presentation hue comes from the shared palette.
                fixed3 colorized=MarsFerricDisplayAlbedo(selectedLuma);
                surface.rgb=lerp(surface.rgb,lerp(selected,colorized,_OrbitalMono),orbitalCoverage);
            }
            float edgeDistance = max(abs(input.worldPos.x), abs(input.worldPos.z));
            float regionalMask = lerp(1.0, 1.0 - smoothstep(48.0, 72.0, edgeDistance), _RegionalBlend);
            float2 contextPosition = input.worldPos.xz;
            float broad = Noise2(contextPosition * 0.018 + 8.3);
            float stone = Noise2(contextPosition * 0.072 - 4.7);
            float fine = Noise2(contextPosition * 0.29 + 17.1);
            float exposedRock = smoothstep(0.62, 0.89, broad * 0.68 + stone * 0.32);
            fixed3 dustySoil = lerp(fixed3(0.47, 0.225, 0.135), fixed3(0.57, 0.305, 0.185), broad * 0.55);
            fixed3 bedrock = lerp(fixed3(0.31, 0.235, 0.205), fixed3(0.42, 0.295, 0.225), stone);
            fixed3 context = lerp(dustySoil, bedrock, exposedRock * 0.46) * _Color.rgb * input.color.rgb;
            context *= lerp(0.95, 1.055, fine);
            float gravel = Noise2(contextPosition * 2.6 + 51.2);
            float mineral = Noise2(contextPosition * 0.58 - 12.3);
            fixed3 regional = lerp(fixed3(0.39,0.32,0.26), fixed3(0.57,0.46,0.35), broad);
            regional = lerp(regional, fixed3(0.25,0.245,0.23), exposedRock * 0.70);
            regional *= lerp(0.85,1.13, mineral) * lerp(0.94,1.06,gravel) * input.color.rgb;
            float2 detailUV = contextPosition * 0.95;
            float2 rotatedUV = float2(contextPosition.x * 0.71 + contextPosition.y * 0.43,
                contextPosition.y * 0.71 - contextPosition.x * 0.43) + 0.37;
            // Photographic crops are not periodic. Mirrored sampling joins the
            // same edge pixels instead of exposing a rectangular tile seam.
            float2 joinedDetailUV = 1.0 - abs(frac(detailUV * 0.5) * 2.0 - 1.0);
            float2 joinedRotatedUV = 1.0 - abs(frac(rotatedUV * 0.5) * 2.0 - 1.0);
            fixed3 photoA = tex2D(_GroundTex, joinedDetailUV).rgb;
            fixed3 photoB = tex2D(_GroundTex, joinedRotatedUV).rgb;
            // Remove panorama-scale silhouettes from the detail texture. Only
            // high-frequency photographic soil grain belongs at human scale.
            fixed3 photoMeanA=tex2Dlod(_GroundTex,float4(joinedDetailUV,0,2)).rgb;
            fixed3 photoMeanB=tex2Dlod(_GroundTex,float4(joinedRotatedUV,0,2)).rgb;
            float photoGrain=dot(lerp(photoA-photoMeanA,photoB-photoMeanB,0.42),fixed3(0.30,0.59,0.11));
            // A surface photograph is not a geographically registered material.
            // Its mirrored cracks were producing repeated track-like shapes.
            // Keep unresolved grain restrained and fade it before the horizon.
            float grainFade=(1-smoothstep(25,150,distance(input.worldPos.xz,_WorldSpaceCameraPos.xz)))*(1-smoothstep(.1,.5,max(fwidth(contextPosition.x),fwidth(contextPosition.y))));
            regional=fixed3(.65,.45,.32)*lerp(.96,1.04,broad)*lerp(1,lerp(.94,1.06,gravel),grainFade);
            regional*=clamp(1+photoGrain*.9*grainFade,.9,1.1);
            float plate=smoothstep(.40,.64,stone);
            float dunes=(sin(contextPosition.x*4.1+contextPosition.y*1.7+broad*5)*.5+.5);
            #ifndef UNITY_COLORSPACE_GAMMA
            regional=GammaToLinearSpace(regional);
            #endif
            float orthoMask=step(0.0,input.uv_MainTex.x)*step(input.uv_MainTex.x,1.0)*step(0.0,input.uv_MainTex.y)*step(input.uv_MainTex.y,1.0);
            float orbital=dot(tex2D(_MainTex,input.uv_MainTex).rgb,fixed3(0.3,0.59,0.11));
            orthoMask*=smoothstep(0.015,0.055,orbital);
            regional*=lerp(1.0,lerp(0.60,1.65,saturate(orbital*2.15)),_OrthoBlend*orthoMask);
            // Orbital mosaics contain acquisition seams and baked sun shadows.
            // Keep their ground-albedo influence gentle; lighting comes from
            // the measured 3D relief, not dark stripes copied from an orbiter.
            regional*=lerp(1.0,clamp(selectedLuma/0.18,0.85,1.15),orbitalCoverage*0.40);
            // With matching stereo ortho coverage, retain the actual mapped
            // outcrops and soil contrasts. Fine shading remains albedo only.
            fixed3 measuredAlbedo=MarsFerricDisplayAlbedo(selectedLuma);
            regional=lerp(regional,measuredAlbedo,orbitalCoverage*.95);
            output.Albedo = lerp(context, surface.rgb, regionalMask);
            output.Albedo = lerp(output.Albedo, regional, _DetailMode);
            float3 ctx=MarsCtxPhotograph(geoUv);
            float ctxCoverage=MarsCtxPriority(ctx,orbitalCoverage,_SourceImageSpacing)*(1-step(.5,_DetailMode));
            // Retain colour context where it is actually available. Only the
            // monochrome CTX/HiRISE photograph receives presentation colour.
            float3 ctxPhotograph=MarsFerricDisplayAlbedo(ctx.x);
            output.Albedo=lerp(output.Albedo,ctxPhotograph,ctxCoverage);
            // Keep the original restrained fill on broad CTX context only.
            output.Emission=ctxPhotograph*.08*ctxCoverage;
            if(_DetailMode<.5&&_GeodesicUv>.5&&_MapLayer<.5)
            {
                // Smooth measured mesh normals provide a restrained raked-light
                // cue for shallow real rims. No triangle derivatives, noise or
                // displacement are used to invent relief in a genuinely flat cell.
                float3 measuredNormal=normalize(WorldNormalVector(input,float3(0,0,1)));
                float3 sunDirection=normalize(UnityWorldSpaceLightDir(input.worldPos));
                float reliefCue=clamp(1+(dot(measuredNormal,sunDirection)-sunDirection.y)*1.1,.70,1.22);
                output.Albedo*=reliefCue;
                output.Emission*=sqrt(reliefCue);
            }
            if(_DetailMode<.5&&_MapLayer>.5)
            {
                // Diagnostic layers use geometric relief, independent of orbital
                // acquisition seams. Correct the displayed gradient by the
                // explicitly chosen vertical multiplier before classifying it.
                fixed3 relief=fixed3(.46,.30,.22);
                if(_MapLayer>1.5)
                {
                    float3 n=cross(ddx(input.worldPos),ddy(input.worldPos));
                    float degrees=atan(length(n.xz)/max(abs(n.y),1e-10)/max(_MapReliefScale,1))*57.29578;
                    relief=lerp(fixed3(.12,.40,.33),fixed3(.90,.64,.19),saturate(degrees/15));
                    relief=lerp(relief,fixed3(.78,.17,.12),saturate((degrees-15)/15));
                }
                #ifndef UNITY_COLORSPACE_GAMMA
                relief=GammaToLinearSpace(relief);
                #endif
                if(_MapLayer<1.5)relief=lerp(relief,surface.rgb,.55);
                output.Albedo=relief;
                if(_MapLayer>1.5)output.Emission=0;
            }
            fixed3 mappedNormal = UnpackScaleNormal(tex2D(_BumpMap, input.uv_BumpMap), _BumpScale);
            output.Normal = normalize(lerp(fixed3(0, 0, 1), mappedNormal, regionalMask));
            float2 edge = min(joinedDetailUV, 1.0 - joinedDetailUV);
            float normalFade = smoothstep(0.0, 0.09, min(edge.x, edge.y));
            float2 grainPoint=contextPosition*5.5;
            float gx=Noise2(grainPoint+float2(0.09,0))-Noise2(grainPoint-float2(0.09,0));
            float gy=Noise2(grainPoint+float2(0,0.09))-Noise2(grainPoint-float2(0,0.09));
            float pdx=dot(tex2D(_GroundTex,joinedDetailUV+float2(.0015,0)).rgb-tex2D(_GroundTex,joinedDetailUV-float2(.0015,0)).rgb,fixed3(.3,.59,.11));
            float pdy=dot(tex2D(_GroundTex,joinedDetailUV+float2(0,.0015)).rgb-tex2D(_GroundTex,joinedDetailUV-float2(0,.0015)).rgb,fixed3(.3,.59,.11));
            fixed3 detailNormal=normalize(fixed3(-gx*.08*grainFade,-gy*.08*grainFade,1));
            output.Normal = normalize(lerp(output.Normal, detailNormal, _DetailMode));
            output.Metallic = 0;
            output.Smoothness = _Smoothness;
            output.Occlusion = 0.94;
            output.Alpha = 1;
        }
        ENDCG
    }
    FallBack "Diffuse"
}

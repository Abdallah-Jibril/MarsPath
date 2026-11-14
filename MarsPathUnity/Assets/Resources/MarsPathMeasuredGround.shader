Shader "MarsPath/Measured Ground"
{
    Properties
    {
        _Color("Dust albedo",Color)=(0.65,0.45,0.32,1)
        _OrbitalTex("Registered orbital image",2D)="black"{}
        _GlobalTex("NASA Viking geographic context",2D)="gray"{}
        _OrbitalRect("Geographic UV rectangle",Vector)=(0,0,0,0)
        [HideInInspector] _OrbitalPrecise("Local native image registration",Float)=0
        [HideInInspector] _OrbitalUvAnchor("Local image UV and angular scale",Vector)=(0,0,0,0)
        [HideInInspector] _OrbitalGeoFrame("Origin latitude sine cosine and angular scale",Vector)=(0,1,0,0)
        _OrbitalBlend("Source coverage",Float)=0
        _OrbitalMono("Monochrome source",Float)=1
        _SourceImageSpacing("Image sample spacing",Float)=200
        _SoilTex("NASA photographic material grain / illustrative",2D)="gray"{}
        _GrainNormalStrength("Illustrative sub-grid material roughness",Range(0,.35))=.22
        _MaterialOrigin("Geographically anchored material phase",Vector)=(0,0,0,0)
        _MaterialEast("East material derivative",Vector)=(1,0,0,0)
        _MaterialNorth("North material derivative",Vector)=(0,1,0,0)
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
        Tags {"RenderType"="Opaque"}
        CGPROGRAM
        #pragma surface surf Standard fullforwardshadows addshadow finalcolor:DisplayDither
        #pragma target 3.0
        #include "MarsCtxAtlas.cginc"
        sampler2D _OrbitalTex,_SoilTex,_GlobalTex;
        float4 _OrbitalRect;
        float _OrbitalPrecise;
        float4 _OrbitalUvAnchor,_OrbitalGeoFrame;
        fixed4 _Color;
        float _OrbitalBlend,_OrbitalMono,_SourceImageSpacing,_GrainNormalStrength;
        float4 _MaterialOrigin,_MaterialEast,_MaterialNorth;
        struct Input{float3 worldPos;float2 uv2_OrbitalTex;float4 screenPos;};
        float H(float2 p){p=frac(p*float2(123.34,456.21));p+=dot(p,p+45.32);return frac(p.x*p.y);}
        float HP(float2 p,float period){return H(p-floor(p/period)*period);}
        float NP(float2 p,float period){float2 i=floor(p),f=frac(p);f=f*f*(3-2*f);return lerp(lerp(HP(i,period),HP(i+float2(1,0),period),f.x),lerp(HP(i+float2(0,1),period),HP(i+1,period),f.x),f.y);}
        float2 PreciseOrbitalUv(float2 position,float2 fallback)
        {
            if(_OrbitalPrecise<.5)return fallback;
            float radius=length(position),angle=radius*_OrbitalGeoFrame.z;
            float2 displacement=sin(angle)*position/max(radius,1e-7);
            float co=cos(angle),sp=_OrbitalGeoFrame.x,cp=_OrbitalGeoFrame.y;
            float latNorth=cp*co-sp*displacement.y;
            if(latNorth<=.0001)return fallback;
            float cosLatitude=sqrt(latNorth*latNorth+displacement.x*displacement.x);
            // Rationalize cos(latitude)-latNorth. This avoids subtracting two
            // nearly equal global angles, preserving native sub-metre pixels.
            float correction=displacement.x*displacement.x/max(1e-7,cosLatitude+latNorth);
            float latitudeDelta=atan2(displacement.y-sp*correction,co+cp*correction);
            float longitudeDelta=atan2(displacement.x,latNorth);
            return _OrbitalUvAnchor.xy+float2(longitudeDelta,latitudeDelta)*_OrbitalUvAnchor.zw;
        }
        void surf(Input input,inout SurfaceOutputStandard output)
        {
            // A double-precision CPU origin plus local tangent derivatives keeps
            // the material fixed on Mars when the moving surface recentres.
            float2 p=_MaterialOrigin.xy+input.worldPos.x*_MaterialEast.xy+input.worldPos.z*_MaterialNorth.xy;
            float aa=1-smoothstep(.04,.30,max(fwidth(p.x),fwidth(p.y)));
            float grain=(NP(p*7.0,28672)-.5)*.13*aa;
            float soil=NP(p*.1875,768);
            half3 albedo=_Color.rgb*(.85+.29*soil+grain);
            // Patches of fine iron-coloured dust over darker granular ground.
            // This is optical, geographically anchored sub-grid variation.
            albedo*=lerp(half3(.97,.98,1.03),half3(1.04,1,.90),soil);
            // Beyond the metre-scale foreground, preserve genuine broad mapped
            // colour/albedo variation rather than colouring every hillside one
            // flat beige. This is low-influence orbital context, not invented
            // ground texture or a claim of pixel-level hazard measurements.
            fixed3 mappedContext=tex2D(_GlobalTex,input.uv2_OrbitalTex).rgb;
            float contextFade=smoothstep(300,6000,distance(input.worldPos.xz,_WorldSpaceCameraPos.xz));
            albedo*=lerp(fixed3(1,1,1),clamp(mappedContext/.22,.65,1.55),contextFade*.32);
            // Only sub-grid photographic grain, with its large stones/shadows
            // removed by high-pass filtering. This never displaces the DEM or
            // creates mapped obstacles; actual geography stays in the ortho.
            float2 detail=1-abs(frac(p*.4375)*2-1);
            float2 detail2=1-abs(frac(float2(p.x*.3125+p.y*.21875,p.y*.3125-p.x*.21875)+.375)*2-1);
            float3 photo=tex2D(_SoilTex,detail).rgb-tex2Dlod(_SoilTex,float4(detail,0,4)).rgb;
            float3 photo2=tex2D(_SoilTex,detail2).rgb-tex2Dlod(_SoilTex,float4(detail2,0,4)).rgb;
            float highpass=dot(lerp(photo,photo2,.45),float3(.3,.59,.11));
            float fade=(1-smoothstep(8,65,distance(input.worldPos.xz,_WorldSpaceCameraPos.xz)))*aa;
            albedo*=clamp(1+highpass*lerp(5.8,3.6,soil)*fade,.59,1.45);
            float legacyCoverage=0;
            float3 materialResponse=clamp(albedo/max(_Color.rgb,float3(.001,.001,.001)),.65,1.45);
            if(_OrbitalBlend>.5&&_OrbitalRect.z>0&&_OrbitalRect.w>0)
            {
                float2 uv=input.uv2_OrbitalTex;uv.x+=round(_OrbitalRect.x+_OrbitalRect.z*.5-uv.x);uv=(uv-_OrbitalRect.xy)/_OrbitalRect.zw;
                uv=PreciseOrbitalUv(input.worldPos.xz,uv);
                float edge=min(min(uv.x,1-uv.x),min(uv.y,1-uv.y));
                fixed4 source=tex2D(_OrbitalTex,uv);
                float valid=smoothstep(0,.035,edge)*smoothstep(.1,.8,source.a);
                legacyCoverage=valid;
                // Alpha marks missing pixels; true dark rock and shadows are
                // valid measurements and must not turn into procedural sand.
                float luma=dot(source.rgb/max(.05,source.a),fixed3(.299,.587,.114));
                // Orbital posts do not resolve centimetre-scale material grain.
                // Retain the explicitly illustrative foreground roughness under
                // registered imagery rather than replacing it with a flat fill.
                float influence=lerp(.22,1,1-smoothstep(2,40,_SourceImageSpacing));
                float3 matched=lerp(source.rgb/max(.05,source.a),MarsFerricDisplayAlbedo(luma),_OrbitalMono);
                albedo=lerp(albedo,matched*materialResponse,valid*influence);
            }
            float3 ctx=MarsCtxPhotograph(input.uv2_OrbitalTex);
            float ctxCoverage=MarsCtxPriority(ctx,legacyCoverage,_SourceImageSpacing);
            // The registered photograph supplies real mapped outcrops/contrasts.
            // Its native black-and-white source receives a gentle dust tint;
            // centimetre material grain is optical and never displaces terrain.
            float3 photographicGround=MarsFerricDisplayAlbedo(ctx.x)*materialResponse;
            albedo=lerp(albedo,photographicGround,ctxCoverage);
            // Unity supplies Color properties in the project's linear space.
            // Only centimetre-scale optical roughness: this changes lighting,
            // never the measured mesh/collider, route slopes or terrain heights.
            // Fade it with footprint and distance so the horizon cannot shimmer
            // or acquire invented ridges from a repeating photograph.
            float2 q=p*7.0;
            float gx=NP(q+float2(.12,0),28672)-NP(q-float2(.12,0),28672);
            float gz=NP(q+float2(0,.12),28672)-NP(q-float2(0,.12),28672);
            float2 gradient=float2(gx,gz);
            output.Normal=normalize(float3(-dot(gradient,_MaterialEast.xy)*_GrainNormalStrength*fade,-dot(gradient,_MaterialNorth.xy)*_GrainNormalStrength*fade,1));
            output.Albedo=albedo;output.Metallic=0;output.Smoothness=.025;
            output.Occlusion=lerp(.89,1,saturate(.55+highpass*2.6))*fade+(1-fade);output.Alpha=1;
        }
        void DisplayDither(Input input,SurfaceOutputStandard surface,inout fixed4 colour)
        {
            // Sub-LSB display dithering avoids visible 8-bit tonal terraces on
            // broad, gently illuminated slopes. It changes no geometry/normals.
            float2 pixel=floor(input.screenPos.xy/max(input.screenPos.w,1e-6)*_ScreenParams.xy);
            colour.rgb+=(H(pixel)-.5)/255.0;
        }
        ENDCG
    }
    FallBack "Diffuse"
}

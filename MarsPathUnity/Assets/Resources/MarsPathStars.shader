Shader "MarsPath/OrbitalStars"
{
    Properties
    {
        _SpaceColor ("Deep space", Color) = (0.004,0.007,0.014,1)
    }
    SubShader
    {
        Tags { "Queue"="Background" "RenderType"="Background" "PreviewType"="Skybox" }
        Cull Off ZWrite Off
        Pass
        {
            CGPROGRAM
            #pragma target 3.0
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            fixed4 _SpaceColor;
            struct appdata { float4 vertex:POSITION; };
            struct v2f { float4 position:SV_POSITION;float3 direction:TEXCOORD0; };
            v2f vert(appdata input)
            {
                v2f output;output.position=UnityObjectToClipPos(input.vertex);
                output.direction=input.vertex.xyz;return output;
            }
            float hash(float3 p)
            {
                p=frac(p*float3(0.1031,0.11369,0.13787));
                p+=dot(p,p.yzx+19.19);return frac((p.x+p.y)*p.z);
            }
            fixed4 frag(v2f input):SV_Target
            {
                float3 d=normalize(input.direction),a=abs(d);float2 uv;float face;
                if(a.x>=a.y&&a.x>=a.z){uv=d.yz/a.x;face=d.x>0?0:1;}
                else if(a.y>=a.z){uv=d.xz/a.y;face=d.y>0?2:3;}
                else{uv=d.xy/a.z;face=d.z>0?4:5;}
                float2 p=(uv*0.5+0.5)*150.0;
                float2 cell=floor(p),local=frac(p);
                float choice=hash(float3(cell,face*31.7+3));
                float2 centre=float2(hash(float3(cell+7.31,face+12.7)),hash(float3(cell+19.2,face+5.5)));
                centre=lerp(0.15,0.85,centre);
                float2 offset=local-centre;
                float radius=lerp(0.020,0.045,hash(float3(cell,face+81.0)));
                float pixel=clamp(length(fwidth(p)),0.001,0.35);
                float core=1.0-smoothstep(radius,radius+pixel*0.65,length(offset));
                float brightness=lerp(0.25,0.85,pow(choice,8));
                float present=step(0.972,choice);
                float3 tint=lerp(float3(0.82,0.89,1),float3(1,0.95,0.86),hash(float3(cell,face+47)));
                return fixed4(_SpaceColor.rgb+tint*core*brightness*present,1);
            }
            ENDCG
        }
    }
}

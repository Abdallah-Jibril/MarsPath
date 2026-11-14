#ifndef MARSPATH_CTX_ATLAS_INCLUDED
#define MARSPATH_CTX_ATLAS_INCLUDED
sampler2D _CtxOverviewTex,_CtxViewTex,_CtxDetailTex;
float4 _CtxOverviewRect,_CtxViewRect,_CtxDetailRect;
float _CtxOverviewBlend,_CtxViewBlend,_CtxDetailBlend,_CtxOverviewSpacing,_CtxViewSpacing,_CtxDetailSpacing;

float3 MarsFerricDisplayAlbedo(float monochrome)
{
    // Presentation colour for monochrome CTX/HiRISE, not a true-colour product.
    // Preserve source luminance so the baked relief and fine ripples remain
    // readable after tinting. Dark bedrock is less saturated than warm dust.
    float gray=max(0,monochrome);
    float3 tint=lerp(float3(.43,.35,.31),float3(.70,.47,.32),smoothstep(.012,.20,gray));
    #ifndef UNITY_COLORSPACE_GAMMA
    tint=GammaToLinearSpace(tint);
    #endif
    tint/=max(.0001,dot(tint,float3(.2126,.7152,.0722)));
    // Pale sunlit rock approaches neutral smoothly, retaining bright source
    // detail without clipping a fixed red multiplier into flat white patches.
    tint=lerp(tint,float3(1,1,1),smoothstep(.35,1,gray));
    return gray*tint;
}

float2 MarsCtxRead(sampler2D image,float4 rectangle,float blend,float2 geographic)
{
    if(blend<=0||rectangle.z<=0||rectangle.w<=0)return float2(0,0);
    geographic.x+=round(rectangle.x+rectangle.z*.5-geographic.x);
    float2 uv=(geographic-rectangle.xy)/rectangle.zw;
    float edge=min(min(uv.x,1-uv.x),min(uv.y,1-uv.y));
    float4 source=tex2D(image,uv);
    // Valid texels have alpha1; absent tiles have RGB0/alpha0. Restore the
    // mip-filtered premultiplied value before blending atlas boundaries.
    float gray=dot(source.rgb/max(.015,source.a),float3(.299,.587,.114));
    float coverage=blend*smoothstep(0,.025,edge)*smoothstep(.015,.85,source.a);
    return float2(gray,coverage);
}

float3 MarsCtxPhotograph(float2 geographic)
{
    float2 overview=MarsCtxRead(_CtxOverviewTex,_CtxOverviewRect,_CtxOverviewBlend,geographic);
    float2 view=MarsCtxRead(_CtxViewTex,_CtxViewRect,_CtxViewBlend,geographic);
    float2 detail=MarsCtxRead(_CtxDetailTex,_CtxDetailRect,_CtxDetailBlend,geographic);
    // The full region, visible camera footprint, and finest ground window have
    // independent coverage. Sort by actual pixel spacing, not window name, so
    // a smaller but coarser requested window cannot blur an existing source.
    float3 a=float3(overview,_CtxOverviewSpacing),b=float3(view,_CtxViewSpacing),c=float3(detail,_CtxDetailSpacing),swap;
    if(a.z<b.z){swap=a;a=b;b=swap;}
    if(b.z<c.z){swap=b;b=c;c=swap;}
    if(a.z<b.z){swap=a;a=b;b=swap;}
    float wa=a.y*(1-b.y)*(1-c.y),wb=b.y*(1-c.y),wc=c.y,total=wa+wb+wc;
    if(total<.0001)return float3(0,0,1e9);
    float gray=(a.x*wa+b.x*wb+c.x*wc)/total;
    float spacing=(a.z*wa+b.z*wb+c.z*wc)/total;
    return float3(gray,total,spacing);
}

float MarsCtxPriority(float3 ctx,float legacyCoverage,float legacySpacing)
{
    // A matching1mHiRISE image remains above5mCTX. A valid5mCTX detail atlas
    // replaces a20m ortho or coarse overview without a global texture swap.
    float legacyIsFiner=1-smoothstep(ctx.z,max(ctx.z*1.35,ctx.z+.1),legacySpacing);
    return ctx.y*(1-legacyCoverage*legacyIsFiner);
}
#endif

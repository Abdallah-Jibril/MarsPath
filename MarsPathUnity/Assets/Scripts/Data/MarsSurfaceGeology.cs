using System;
using UnityEngine;

namespace MarsPath
{
    /// <summary>Visual sub-grid geology, not measured obstacles. Spatially anchored
    /// so recentering a human-scale terrain window does not regenerate the ground.
    /// Measured DEM elevations remain the macro terrain and planning source.</summary>
    public static class MarsSurfaceGeology
    {
        private struct Point{public double x,y;}
        private static Point Coordinates(float x,float z,GeoPoint origin)
        {
            double lat=origin.latitude*Math.PI/180,lon=origin.longitude*Math.PI/180;
            // A fixed oblique projection of the planet, plus local east/north.
            // No origin-dependent random seed or frame-local Perlin coordinates.
            double r=MarsRegionalTerrain.RadiusMeters;
            double px=r*Math.Cos(lat)*Math.Cos(lon)-x*Math.Sin(lon)-z*Math.Sin(lat)*Math.Cos(lon);
            double py=r*Math.Sin(lat)+z*Math.Cos(lat);
            double pz=r*Math.Cos(lat)*Math.Sin(lon)+x*Math.Cos(lon)-z*Math.Sin(lat)*Math.Sin(lon);
            return new Point{x=px*.67+py*.31+pz*.54,y=py*.71-px*.42+pz*.37};
        }
        private static float Hash(int x,int y)
        {unchecked{uint h=(uint)x*374761393u+(uint)y*668265263u;h=(h^(h>>13))*1274126177u;return (h^(h>>16))/(float)uint.MaxValue;}}
        private static float Noise(Point p,float scale,float offset)
        {
            double px=p.x*scale+offset,py=p.y*scale-offset;int x=(int)Math.Floor(px),y=(int)Math.Floor(py);
            float u=(float)(px-x),v=(float)(py-y);u=u*u*(3-2*u);v=v*v*(3-2*v);
            return Mathf.Lerp(Mathf.Lerp(Hash(x,y),Hash(x+1,y),u),Mathf.Lerp(Hash(x,y+1),Hash(x+1,y+1),u),v);
        }
        public static float Height(float x,float z,GeoPoint origin)
        {
            Point p=Coordinates(x,z,origin);
            float terrain=Noise(p,.009f,31),bedrock=Noise(p,.027f,79);
            float shelves=Mathf.SmoothStep(.28f,.73f,bedrock);
            float broken=Noise(p,.085f,47)-.5f;
            float grain=Noise(p,.38f,53)-.5f;
            float rippleMask=1-Mathf.SmoothStep(.40f,.64f,bedrock);
            // Decimetre aeolian bedforms and broken outcrops, not arbitrary
            // kilometre-high mountains substituted for missing scientific data.
            float ripples=(float)Math.Sin(p.x*.72+p.y*.23+terrain*2)*.11f*rippleMask;
            return (terrain-.5f)*7.5f+(shelves-.5f)*3.4f+broken*.58f+grain*.15f+ripples;
        }
    }
}

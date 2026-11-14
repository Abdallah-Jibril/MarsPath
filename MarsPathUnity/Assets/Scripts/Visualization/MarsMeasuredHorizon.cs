using System;
using UnityEngine;

namespace MarsPath
{
    /// <summary>Projects measured far terrain onto Mars' spherical horizon.
    /// Near walking geometry stays in its metre-scale tangent frame. This is
    /// planetary curvature, never an elevation exaggeration or invented relief.</summary>
    public static class MarsMeasuredHorizon
    {
        public const float RadiusMetres=60000;
        public const float PlanarJoinStartMetres=180;
        public const float FullCurvatureMetres=500;

        public static Vector3 Project(float x,float z,float measuredLocalHeight,float originElevation)
        {
            double range=Math.Sqrt((double)x*x+(double)z*z);
            if(range<.00001)return new Vector3(x,measuredLocalHeight,z);
            float edge=Mathf.Max(Mathf.Abs(x),Mathf.Abs(z));
            float join=Mathf.SmoothStep(0,1,Mathf.InverseLerp(PlanarJoinStartMetres,FullCurvatureMetres,edge));
            if(join<=0)return new Vector3(x,measuredLocalHeight,z);
            double angle=range/MarsRegionalTerrain.RadiusMeters;
            double horizontal=Math.Sin(angle)/angle;
            // SampleVisualHeight has the fixed -1.5 m scene datum. Remove it
            // only while reconstructing the physical radius of this DEM sample.
            double elevation=originElevation+measuredLocalHeight+1.5;
            double drop=(MarsRegionalTerrain.RadiusMeters+elevation)*(Math.Cos(angle)-1);
            return new Vector3(x*Mathf.Lerp(1,(float)horizontal,join),measuredLocalHeight+(float)drop*join,z*Mathf.Lerp(1,(float)horizontal,join));
        }
    }
}

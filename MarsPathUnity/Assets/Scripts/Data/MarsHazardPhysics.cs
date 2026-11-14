using System;
using UnityEngine;

namespace MarsPath
{
    /// <summary>Measured DEM geometry and explicit demonstration alert policies.
    /// Thresholds are not certified traction or astronaut safety limits.</summary>
    public static class MarsHazardPhysics
    {
        public const float SlopeWarningDegrees=10;
        public const float SlopeLookAheadMetres=8;
        public const float RockLookAheadMetres=16;
        public const float MinimumRockHeightMetres=.10f;
        public const float MinimumRockRadiusMetres=.12f;
        public const int MinimumFieldCount=4;
        public const float MinimumFieldFootprintSquareMetres=1.2f;

        public static double GradientDegrees(double eastGradient,double northGradient) =>
            Math.Atan(Math.Sqrt(eastGradient*eastGradient+northGradient*northGradient))*180/Math.PI;

        public static float MeasuredSlope(float x,float z,Vector3 forward,out float signedTrackGrade)
        {
            GeoPoint point=MarsRegionalTerrain.ToGeo(x,z);
            // Support spans two source posts; oversampling cannot create finer relief.
            double half=Math.Max(1,MarsRegionalTerrain.SourceSpacingMeters(point));
            float east=MarsRegionalTerrain.MeasuredElevation(MarsGeospatial.DestinationPrecise(point,90,half/1000));
            float west=MarsRegionalTerrain.MeasuredElevation(MarsGeospatial.DestinationPrecise(point,270,half/1000));
            float north=MarsRegionalTerrain.MeasuredElevation(MarsGeospatial.DestinationPrecise(point,0,half/1000));
            float south=MarsRegionalTerrain.MeasuredElevation(MarsGeospatial.DestinationPrecise(point,180,half/1000));
            double gx=(east-west)/(2*half),gz=(north-south)/(2*half);
            Vector2 heading=new Vector2(forward.x,forward.z).normalized;
            signedTrackGrade=(float)(Math.Atan(gx*heading.x+gz*heading.y)*180/Math.PI);
            return (float)GradientDegrees(gx,gz);
        }

        /// <summary>Horizontal footprint test in world units; clearance returned in metres.</summary>
        public static bool InRockCorridor(Vector3 position,Vector3 forward,Vector3 center,float radius,
            float metresPerUnit,out float clearance,out float lateralSquared)
        {
            clearance=float.MaxValue;lateralSquared=float.MaxValue;
            forward.y=0;
            if(metresPerUnit<=0||radius<0||forward.sqrMagnitude<.0001f)return false;
            forward.Normalize();Vector3 offset=center-position;offset.y=0;
            float along=Vector3.Dot(offset,forward);
            if(along<=0||along>RockLookAheadMetres/metresPerUnit+radius)return false;
            lateralSquared=Mathf.Max(0,offset.sqrMagnitude-along*along);
            float corridor=radius+Mathf.Clamp(along*.53f,.9f/metresPerUnit,4.8f/metresPerUnit);
            if(lateralSquared>corridor*corridor)return false;
            clearance=Mathf.Max(0,offset.magnitude-radius)*metresPerUnit;
            return true;
        }

        public static bool IsRockField(int count,float summedFootprintSquareMetres,float nearestMetres) =>
            count>=MinimumFieldCount&&summedFootprintSquareMetres>=MinimumFieldFootprintSquareMetres&&nearestMetres<=12;
    }
}

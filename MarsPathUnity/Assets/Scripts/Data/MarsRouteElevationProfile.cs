using UnityEngine;

namespace MarsPath
{
    /// <summary>Source-derived geographic route profile; no visual microrelief.</summary>
    public sealed class MarsRouteElevationProfile
    {
        public readonly float[] heights;
        public readonly GeoPoint[] locations;
        public readonly float[] sourceSpacings;
        public readonly string[] sourceNames;
        public readonly float lengthKm;
        public readonly int sourceRevision;
        public float minimum=float.MaxValue,maximum=float.MinValue,ascent,descent,maxGrade,meanAbsoluteGrade;
        public float StepMeters=>lengthKm*1000/(heights.Length-1);
        public MarsRouteElevationProfile(MissionData mission,RouteData route)
        {
            // Constructing a route track needs the planning frame. A details panel
            // opened while walking must not leave that frame active on the surface.
            MissionData previous=MarsRegionalTerrain.Current;
            MarsGeographicTrack track;
            try{track=new MarsGeographicTrack(mission,route);}
            finally{if(previous!=null&&previous!=mission)MarsRegionalTerrain.Configure(previous);}
            lengthKm=(float)track.LengthKm;sourceRevision=MarsRouteFacts.SourceRevision;
            float finest=Mathf.Min(MarsRegionalTerrain.SourceSpacingMeters(track.At(0)),MarsRegionalTerrain.SourceSpacingMeters(track.At(.5f)),MarsRegionalTerrain.SourceSpacingMeters(track.At(1)));
            int count=Mathf.Clamp(Mathf.CeilToInt(lengthKm*1000/Mathf.Max(1,finest))+1,257,1025);
            heights=new float[count];locations=new GeoPoint[count];sourceSpacings=new float[count];sourceNames=new string[count];
            for(int i=0;i<heights.Length;i++)
            {
                locations[i]=track.At(i/(float)(heights.Length-1));float h=heights[i]=MarsRegionalTerrain.MeasuredElevation(locations[i]);
                sourceSpacings[i]=MarsRegionalTerrain.SourceSpacingMeters(locations[i]);sourceNames[i]=MarsRegionalTerrain.SourceTag(locations[i]);
                minimum=Mathf.Min(minimum,h);maximum=Mathf.Max(maximum,h);
                if(i>0){float rise=h-heights[i-1];ascent+=Mathf.Max(0,rise);descent+=Mathf.Max(0,-rise);float grade=Mathf.Atan2(Mathf.Abs(rise),StepMeters)*Mathf.Rad2Deg;maxGrade=Mathf.Max(maxGrade,grade);meanAbsoluteGrade+=grade;}
            }
            meanAbsoluteGrade/=heights.Length-1;
        }
        public float HeightAt(float fraction)
        {float x=Mathf.Clamp01(fraction)*(heights.Length-1);int i=Mathf.Min(heights.Length-2,(int)x);return Mathf.Lerp(heights[i],heights[i+1],x-i);}
    }
}

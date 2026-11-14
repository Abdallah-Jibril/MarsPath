using System;
using System.Collections;
using UnityEngine;

namespace MarsPath
{
    public partial class MarsPathDemoBootstrap
    {
        // Stable native-200m neighbourhoods work at arbitrary coordinates, not
        // just at the named landmarks or the bundled reference windows. Small
        // windows avoid hundreds of row requests for a continent-sized crop.
        internal static GeoPoint GlobalBaselineFocus(GeoPoint point)
        {
            double latitude=Math.Max(-89.94,Math.Min(89.94,Math.Round(point.latitude/.08)*.08));
            double step=.08/Math.Max(.02,Math.Cos(latitude*Math.PI/180));
            return new GeoPoint{latitude=(float)latitude,longitude=MarsGeospatial.NormalizeLongitude((float)(Math.Round(point.longitude/step)*step))};
        }
        private IEnumerator RefineGlobalPlanningBaseline(MissionData original,Func<bool> cancelled,Action finished)
        {
            Func<bool> stop=()=>cancelled()||surfaceActive||helmetEntryLoading;
            try
            {
                var track=new MarsGeographicTrack(original,original.routes[selectedRoute]);
                foreach(float fraction in new[]{0f,1f,.5f,.25f,.75f})
                {
                    if(stop())yield break;
                    GeoPoint point=track.At(fraction);
                    if(MarsRegionalTerrain.SourceSpacingMeters(point)<=200)continue;
                    yield return PrepareGlobalBaselineAt(point,stop,18);
                }
            }
            finally{finished();}
        }
        private IEnumerator PrepareGlobalBaselineAt(GeoPoint point,Func<bool> cancelled,float budget)
        {
            while(surfaceFallbackBusy&&!cancelled())yield return null;
            if(cancelled()||MarsRegionalTerrain.SourceSpacingMeters(point)<=200)yield break;
            surfaceFallbackBusy=true;
            try
            {
                GeoPoint center=GlobalBaselineFocus(point);
                yield return MarsRegionSourceCache.PrepareRegion(center,center,null,cancelled,true,false,budget);
            }
            finally{surfaceFallbackBusy=false;}
        }
    }
}

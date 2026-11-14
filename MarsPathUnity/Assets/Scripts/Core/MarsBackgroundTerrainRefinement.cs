using System;
using System.Collections;
using UnityEngine;
namespace MarsPath
{
    public partial class MarsPathDemoBootstrap
    {
        private int refinementTicket;
        public bool IsRefiningTerrain{get;private set;}
        public bool RefinementEnabled{get;set;}=true;
        private void CancelTerrainRefinement(){refinementTicket++;IsRefiningTerrain=false;}
        private void BeginTerrainRefinement()
        {
            if(MarsMissionZone.IsMission(mission)){MarsOfficialTerrain.ActivateCachedImagery(mission.regionCenter);return;}
            if(!RefinementEnabled||Application.isBatchMode)return;
            StartCoroutine(RefineSelectedTerrain());
            StartCoroutine(PreparePlanningPhotographs(mission,refinementTicket));
        }
        private IEnumerator PreparePlanningPhotographs(MissionData original,int ticket)
        {
            Func<bool> stop=()=>ticket!=refinementTicket||GlobalView.IsVisible;
            // Native CTX for the overview is independent of elevation downloads
            // and of the currently active foreground image.
            yield return MarsCtxImageryCache.PrepareWindow(original.regionCenter,Mathf.Max(8,original.regionSpanKm*1.25f),14,stop,false);
            if(!stop())
            {
                MarsCtxImageryCache.ActivateAt(original.regionCenter,false);
                if(terrainRoot!=null)terrainRoot.GetComponent<MarsTerrainGenerator>()?.RefreshSourceImagery();
            }
        }
        private IEnumerator RefineSelectedTerrain()
        {
            int ticket=++refinementTicket;MissionData original=mission;
            IsRefiningTerrain=true;float began=Time.realtimeSinceStartup;
            Func<bool> stop=()=>ticket!=refinementTicket||mission!=original||GlobalView.IsVisible||Time.realtimeSinceStartup-began>55;
            int before=MarsRouteFacts.SourceRevision,active=0,next=0;
            bool globalFinished=false;
            StartCoroutine(RefineGlobalPlanningBaseline(original,stop,()=>globalFinished=true));
            var track=new MarsGeographicTrack(original,original.routes[selectedRoute]);
            GeoPoint[] points={original.currentLocation,track.At(.5f),original.destination,track.At(.25f),track.At(.75f)};
            while(!stop()&&(next<points.Length||active>0||!globalFinished))
            {
                while(next<points.Length&&active<2&&!stop())
                {
                    GeoPoint point=points[next++];active++;
                    StartCoroutine(RefineGeographicSource(point,stop,()=>active--));
                }
                yield return null;
            }
            if(ticket!=refinementTicket)yield break;
            if(mission==original&&!GlobalView.IsVisible)
            {
                if(before!=MarsRouteFacts.SourceRevision)
                {
                    if(!surfaceActive&&!helmetEntryLoading)
                    {
                        var view=cameraRig.CapturePlanningView();int chosen=selectedRoute;MissionData updated=null;
                        try{updated=MarsRoutePlanner.Generate(original.currentLocation,original.destination,original.routingOptions);}
                        catch(Exception e){Debug.LogWarning("Native source refinement retained current plan: "+e.Message);}
                        if(updated!=null){LoadRegionalMission(updated);if(updated.routes[chosen].available)SelectRoute(chosen);cameraRig.RestorePlanningView(view);}
                    }
                    else planningTerrainDirty=true;
                }
                terrainRoot?.GetComponent<MarsTerrainGenerator>()?.RefreshSourceImagery();
            }
            IsRefiningTerrain=false;
        }
        private IEnumerator RefineGeographicSource(GeoPoint point,Func<bool> stop,Action finished)
        {
            try
            {
                if(!stop())yield return MarsOfficialTerrain.Prepare(point,null,stop,false);
                if(!stop()&&MarsRegionalTerrain.SourceSpacingMeters(point)>25)yield return MarsHrscStereoTerrain.Prepare(point,stop);
            }
            finally{finished();}
        }
    }
}

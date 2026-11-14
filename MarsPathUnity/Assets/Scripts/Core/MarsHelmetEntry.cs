using System.Collections;
using UnityEngine;

namespace MarsPath
{
    public partial class MarsPathDemoBootstrap
    {
        private bool helmetEntryLoading,helmetPreview,previewDemoChoice;
        public bool IsTerrainPreparing=>helmetEntryLoading;
        private bool planningTerrainDirty;
        private float helmetEntryPercent,inspectProgress;
        private MarsRouteElevationProfile elevationProfile;
        private int profileRoute=-1;
        private MissionData profileMission;
        private MarsRouteElevationProfile SelectedElevationProfile
        {
            get
            {
                int index = PlanningRouteIndex;
                if(elevationProfile==null||profileRoute!=index||profileMission!=mission||elevationProfile.sourceRevision!=MarsRouteFacts.SourceRevision)
                {elevationProfile=new MarsRouteElevationProfile(mission,mission.routes[index]);profileRoute=index;profileMission=mission;}
                return elevationProfile;
            }
        }
        private void BeginHelmetEntry(bool preview)
        {if(!helmetEntryLoading&&!desktopBrowsingRoutes)StartCoroutine(PrepareHelmetEntry(preview));}
        private IEnumerator PrepareHelmetEntry(bool preview, bool dispatched = false)
        {
            helmetEntryLoading=true;helmetEntryPercent=0;
            // Native geometry preparation can finish during walking; a demo
            // shot is not the lifetime of a geographic source request.
            MarsRegionSourceCache.Cancel();
            MissionData selectedMission=mission;int selected=selectedRoute;
            var track=new MarsGeographicTrack(mission,mission.routes[selected]);GeoPoint focus=track.At(preview?inspectProgress:0);
            var terrain = PrepareMissionTerrain(track,preview?inspectProgress:0,demoPresentation&&!preview,dispatched);
            while (terrain.MoveNext()) yield return terrain.Current;
            if(GlobalView.IsVisible&&!dispatched){helmetEntryLoading=false;yield break;}
            if(MarsRegionalTerrain.SourceSpacingMeters(focus)>200)
            {
                SurfaceSourceEndpoints(focus,out GeoPoint a,out GeoPoint b);
                MarsRegionSourceCache.TryLoadCachedElevation(a,b);
                planningTerrainDirty=true;
            }
            if(!MarsRegionSourceCache.CoversImagery(focus)||MarsRegionSourceCache.ImageryDisplayMetersPerPixel>12)
            {
                GeoPoint imageA=MarsGeospatial.Destination(focus,270,1.3f),imageB=MarsGeospatial.Destination(focus,90,1.3f);
                MarsRegionSourceCache.TryLoadCachedImagery(imageA,imageB);
                planningTerrainDirty=true;
            }
            MarsOfficialTerrain.RestoreCachedGeometry(focus);MarsOfficialTerrain.ActivateCachedImagery(focus);
            helmetEntryLoading=false;
            if(mission!=selectedMission||selectedRoute!=selected||GlobalView.IsVisible&&!dispatched)yield break;
            if (dispatched) GlobalView.OpenJezeroMission();
            previewDemoChoice=demoPresentation;StartMission();
            if (dispatched)
            {
                PauseQuestTraverse();
                questDispatchedMission = true;
            }
            if(preview){SeekFullJourney(inspectProgress);paused=true;helmetPreview=true;}
            cameraRig.SetHelmetView(true);cameraRig.SnapToMission(astronaut.transform);
        }
        private void StartFromHelmetPreview()
        {demoPresentation=previewDemoChoice;helmetPreview=false;StartMission();}
    }
}

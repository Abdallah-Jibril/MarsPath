using System.Collections;
using UnityEngine;

namespace MarsPath
{
    public partial class MarsPathDemoBootstrap
    {
        private IEnumerator BuildSelectedRegion(GeoPoint start,GeoPoint end)
        {
            if(GlobalView.IsBusy)yield break;
            journeyOverview=null;
            GlobalView.IsBusy=true;GlobalView.SetStatus("Sampling elevation and comparing route options...");
            yield return null;
            // Foreground transitions are local-only. Network refinement owns a
            // separate cancellable coroutine after the region is already usable.
            CancelTerrainRefinement();
            RestoreLocalRegionSources(start, end);
            if(GlobalView==null||!GlobalView.IsVisible){if(GlobalView!=null)GlobalView.IsBusy=false;yield break;}
            // A continental overview can exceed the bounded DEM crop budget.
            // Still acquire a real 200 m local window around the mission start.
            if(MarsRegionalTerrain.SourceSpacingMeters(start)>200)
            {
                SurfaceSourceEndpoints(start,out GeoPoint a,out GeoPoint b);
                MarsRegionSourceCache.TryLoadCachedElevation(a,b);
            }
            if(GlobalView==null||!GlobalView.IsVisible){if(GlobalView!=null)GlobalView.IsBusy=false;yield break;}
            GlobalView.SetStatus("Building measured terrain and five route profiles...");yield return null;
            MissionData computed=null;string error=null;
            try{computed=MarsRoutePlanner.Generate(start,end,MarsMissionZone.RequiredForSelection?new MarsRoutingOptions{MissionZoneId=MarsMissionZone.Id}:null);}catch(System.Exception exception){error=exception.Message;Debug.LogException(exception);}
            if(computed==null){GlobalView.SetError(error??"Unable to construct this regional plan.");yield break;}
            LoadRegionalMission(computed);GlobalView.IsBusy=false;GlobalView.OpenJezeroMission();
            BeginTerrainRefinement();
        }

        private static void RestoreLocalRegionSources(GeoPoint start, GeoPoint end)
        {
            MarsRegionSourceCache.TryLoadCachedRegion(start, end);
            GeoPoint midpoint = MarsRegionalTerrain.Midpoint(start, end);
            MarsOfficialTerrain.RestoreCachedGeometry(start);
            MarsOfficialTerrain.RestoreCachedGeometry(end);
            MarsOfficialTerrain.RestoreCachedGeometry(midpoint);
            MarsOfficialTerrain.ActivateCachedImagery(midpoint);
        }

        public void LoadRegionalMission(MissionData source)
        {
            StopFullSurface();
            if(terrainRoot!=null){terrainRoot.SetActive(false);Destroy(terrainRoot);}
            if(routesRoot!=null){routesRoot.SetActive(false);Destroy(routesRoot);}
            mission=source;selectedRoute=0;missionStarted=false;paused=false;arrived=false;routeProgress=0;nearbyHazard=null;
            elevationProfile=null;planningTerrainDirty=false;helmetPreview=false;
            if(MarsRegionSourceCache.CoversImagery(mission.regionCenter))mission.dataNotice+="\n"+MarsRegionSourceCache.ImageryStatus+". Source image detail is separate from elevation resolution.";
            terrainRoot=new GameObject("Selected Regional Terrain");terrainRoot.transform.SetParent(transform,false);terrainRoot.AddComponent<MarsTerrainGenerator>().Build(mission);
            routesRoot=new GameObject("Computed Regional Routes");routesRoot.transform.SetParent(transform,false);routeView=routesRoot.AddComponent<MarsPathRouteView>();routeView.PlanningCamera=cameraRig.PlanningCamera;routeView.Build(mission);
            for(int i=0;i<mission.routes.Length;i++)if(mission.routes[i].available){selectedRoute=i;break;}
            routeView.Select(selectedRoute);routeView.RefreshPlanningGeometry();RebuildPathDistances();UpdateAstronautPosition(0f);SetAstronautWalking(false);
            cameraRig.SetMissionMode(false,astronaut.transform);
            cameraRig.FrameRoutes(mission);
            MarsPlanningDetailStream stream=GetComponent<MarsPlanningDetailStream>();if(stream==null)stream=gameObject.AddComponent<MarsPlanningDetailStream>();
            stream.Initialize(this,cameraRig,()=>{if(terrainRoot!=null)terrainRoot.GetComponent<MarsTerrainGenerator>()?.RefreshSourceImagery();});
            terrainRoot.AddComponent<MarsPlanningFocusTile>().Initialize(this,cameraRig,()=>{if(routeView!=null)routeView.RefreshPlanningGeometry();});
            evaSimulation.Reset(mission.telemetry);UpdateLifeSupport(mission.routes[selectedRoute]);reserveStop=false;rockAhead=false;
            oxygen=mission.telemetry.oxygenPercent;power=mission.telemetry.suitPowerPercent;missionElapsedMinutes=0;
        }

        private void RebuildPathDistances()
        {
            Vector3[] points=routeView.SelectedWorldPoints;if(points==null)return;
            pathDistances=new float[points.Length];pathLength=0;
            for(int i=1;i<points.Length;i++){pathLength+=Vector3.Distance(points[i-1],points[i]);pathDistances[i]=pathLength;}
        }

        public void PrepareRegionalCapture(int siteIndex,bool eva)
        {
            GeoPoint start=MarsSiteCatalog.Sites[siteIndex].Point,end=MarsGeospatial.Destination(start,48,2.4f);
            end.label=start.label+" / Local target";GlobalView.SetEndpoints(start,end);
            LoadRegionalMission(MarsRoutePlanner.Generate(start,end));GlobalView.OpenJezeroMission();
            if(eva){demoPresentation=false;StartMission();SeekFullJourney(0.27f);}
        }

        public void PlanLocalEvaSection()
        {
            MissionData overview=mission;
            GeoPoint target=MarsGeospatial.Destination(overview.currentLocation,MarsGeospatial.InitialBearing(overview.currentLocation,overview.destination),2.4f);
            target.label="2.4 km EVA endpoint toward "+overview.destination.label;
            MissionData local=MarsRoutePlanner.Generate(overview.currentLocation,target,overview.routingOptions);
            local.region=MarsSiteCatalog.Nearest(overview.currentLocation).name+" / Local EVA section";
            journeyOverview=overview;LoadRegionalMission(local);
        }
        public void SetPlaybackSpeed(float speed){playbackSpeed=speed<15?1:speed<40?30:50;}
        public void PrepareJourneyCapture(bool localSection)
        {
            MissionData journey=MarsRoutePlanner.Generate(new GeoPoint{label="Jezero",latitude=18.4447f,longitude=77.451f},MarsSiteCatalog.Sites[6].Point);
            LoadRegionalMission(journey);GlobalView.OpenJezeroMission();
            if(localSection)PlanLocalEvaSection();
        }
        private void ReturnToJourneyOverview()
        {if(journeyOverview==null)return;MissionData previous=journeyOverview;journeyOverview=null;LoadRegionalMission(previous);}
    }
}

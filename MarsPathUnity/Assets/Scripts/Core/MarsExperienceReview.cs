using System;
using System.Collections;
using UnityEngine;

namespace MarsPath
{
    public sealed class MarsExperienceReview:MonoBehaviour
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {foreach(string arg in Environment.GetCommandLineArgs())if(arg=="--mars-experience-review")new GameObject("Terrain and data experience acceptance").AddComponent<MarsExperienceReview>();}
        private IEnumerator Start(){yield return null;yield return FindAnyObjectByType<MarsPathDemoBootstrap>().ReviewExperience();}
    }
    public partial class MarsPathDemoBootstrap
    {
        public IEnumerator ReviewExperience()
        {
            RefinementEnabled=false;bool okay=true;ReturnToGlobalMap();GlobalView.UseHiRiseTerrainExample();
            float began=Time.realtimeSinceStartup;
            yield return BuildSelectedRegion(GlobalView.StartLocation,GlobalView.EndLocation);
            Debug.Log($"EXPERIENCE_PLAN: {Time.realtimeSinceStartup-began:0.000} s");
            MarsRouteFacts baseline=MarsRouteFacts.For(mission,mission.routes[selectedRoute],SelectedElevationProfile);
            yield return CaptureHelmetReview("MarsPathExperiencePlanner.png");
            showPlanningDetails=true;yield return CaptureHelmetReview("MarsPathExperienceRouteFacts.png");showPlanningDetails=false;
            if(mission.routes[3].available)
            {
                SelectRoute(3);var science=MarsRouteFacts.For(mission,mission.routes[3],SelectedElevationProfile);
                okay&=science.routeId!=baseline.routeId&&science.routeLengthKm==mission.routes[3].distanceKm;
                Debug.Log($"EXPERIENCE_ROUTE_CHANGE: balanced {baseline.routeLengthKm:0.000} km / {baseline.ascentMeters:0.0} m up; science {science.routeLengthKm:0.000} km / {science.ascentMeters:0.0} m up");
            }
            SelectRoute(0);demoPresentation=true;began=Time.realtimeSinceStartup;
            yield return PrepareHelmetEntry(false);paused=true;
            Debug.Log($"EXPERIENCE_ENTRY: {Time.realtimeSinceStartup-began:0.000} s");
            MissionData frame=MarsRegionalTerrain.Current;float measured=MarsRegionalTerrain.SampleVisualHeight(22,11);
            showMissionDetails=true;var details=MarsRouteFacts.For(mission,mission.routes[selectedRoute],SelectedElevationProfile);
            okay&=MarsRegionalTerrain.Current==frame&&MarsRegionalTerrain.HumanScale&&Mathf.Abs(measured-MarsRegionalTerrain.SampleVisualHeight(22,11))<.0001f;
            showMissionDetails=false;yield return CaptureHelmetReview("MarsPathExperienceVisor.png");
            showMissionDetails=true;showMissionMenu=true;yield return CaptureHelmetReview("MarsPathExperienceLiveFacts.png");ResetVisorPanels();
            for(int shot=1;shot<8;shot++)
            {
                AdvancePresentation(15,mission.routes[selectedRoute]);
                okay&=OfficialMeshMatches(surfaceRoot)&&VisibleSurfaceArrows>0&&MarsSurfaceRockScatter.SurfaceRockCount>0;
                if(shot==3){cameraRig.SnapToMission(astronaut.transform);yield return CaptureHelmetReview("MarsPathExperienceRidge.png");}
            }
            UseTimedPlayback(30);SeekFullJourney(.63f);paused=true;
            okay&=OfficialMeshMatches(surfaceRoot)&&oxygen>20&&power>15;
            showMissionDetails=true;yield return CaptureHelmetReview("MarsPathExperienceFullFacts.png");ResetVisorPanels();
            ReturnToGlobalMap();
            var start=new GeoPoint{latitude=19.37447f,longitude=67.33276f,label="Reported region"};
            var end=new GeoPoint{latitude=24.83584f,longitude=64.00996f,label="Reported destination"};
            GlobalView.SetEndpoints(start,end);yield return BuildSelectedRegion(start,end);
            var different=MarsRouteFacts.For(mission,mission.routes[selectedRoute],SelectedElevationProfile);
            okay&=Mathf.Abs(different.routeLengthKm-baseline.routeLengthKm)>100&&different.coarsestGridMeters>baseline.coarsestGridMeters;
            demoPresentation=true;yield return PrepareHelmetEntry(false);paused=true;
            okay&=OfficialMeshMatches(surfaceRoot)&&MarsSurfaceRockScatter.SurfaceRockCount>0;
            yield return CaptureHelmetReview("MarsPathExperienceRegionalVisor.png");
            Debug.Log(okay?"EXPERIENCE_PASSED: measured mesh, route-specific statistics, details frame isolation, all demo stations, full mode and decorative rocks.":"EXPERIENCE_FAILED");
            Application.Quit(okay?0:2);
        }
    }
}

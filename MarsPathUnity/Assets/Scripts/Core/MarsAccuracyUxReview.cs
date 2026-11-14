using System;
using System.Collections;
using UnityEngine;

namespace MarsPath
{
    public sealed class MarsAccuracyUxReview:MonoBehaviour
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {foreach(string arg in Environment.GetCommandLineArgs())if(arg=="--mars-accuracy-ux-review")new GameObject("Planetary and visor acceptance").AddComponent<MarsAccuracyUxReview>();}
        private IEnumerator Start(){yield return null;yield return FindAnyObjectByType<MarsPathDemoBootstrap>().ReviewAccuracyUx();}
    }
    public partial class MarsPathDemoBootstrap
    {
        public IEnumerator ReviewAccuracyUx()
        {
            RefinementEnabled=false;bool okay=true;
            float[,] endpoints={{0,-89.5f,0,89.5f},{10,-135,10,45},{5,179.5f,5,-179.5f}};
            for(int test=0;test<3;test++)
            {
                ReturnToGlobalMap();var a=new GeoPoint{latitude=endpoints[test,0],longitude=endpoints[test,1],label="West endpoint"};var b=new GeoPoint{latitude=endpoints[test,2],longitude=endpoints[test,3],label="East endpoint"};
                GlobalView.SetEndpoints(a,b);long bytes=MarsGlobalMola64.BytesRead;float began=Time.realtimeSinceStartup;
                yield return BuildSelectedRegion(a,b);yield return null;float seconds=Time.realtimeSinceStartup-began;long read=MarsGlobalMola64.BytesRead-bytes;
                okay&=seconds<8&&read<64L*1024*1024&&cameraRig.RoutesInsideViewport(mission)&&mission.routes[selectedRoute].available;
                foreach(RouteData r in mission.routes)if(r.available)
                {
                    RoutePoint first=r.waypoints[0],last=r.waypoints[r.waypoints.Length-1];
                    okay&=MarsGeospatial.AngularDistanceDegrees(MarsRegionalTerrain.ToGeo(first.x*60,first.z*60),a)<.0001f&&MarsGeospatial.AngularDistanceDegrees(MarsRegionalTerrain.ToGeo(last.x*60,last.z*60),b)<.0001f;
                }
                float separation=MarsGeospatial.AngularDistanceDegrees(a,b)*Mathf.Deg2Rad*(float)MarsRegionalTerrain.RadiusMeters/1000;
                okay&=mission.routes[2].distanceKm<separation*1.6f;
                Debug.Log($"PLANETARY_BOUNDED: case {test} / {mission.routes[selectedRoute].distanceKm:0.0} km / {seconds:0.000} s / {read} raster bytes / {MarsGlobalMola64.CachedSegments} segments");
                if(test==0)yield return CaptureHelmetReview("MarsPathEastWestOverview.png");
                demoPresentation=true;yield return PrepareHelmetEntry(false);paused=true;SeekFullJourney(1);
                okay&=MarsGeospatial.AngularDistanceDegrees(JourneyLocation,b)<.00002f&&OfficialMeshMatches(surfaceRoot);
            }
            ReturnToGlobalMap();GlobalView.UseHiRiseTerrainExample();yield return BuildSelectedRegion(GlobalView.StartLocation,GlobalView.EndLocation);
            demoPresentation=true;yield return PrepareHelmetEntry(false);paused=true;
            okay&=OfficialMeshMatches(surfaceRoot)&&MarsRegionalTerrain.SourceSpacingMeters(JourneyLocation)<2;
            double length=fullTrack.LengthKm*1000;GeoPoint physical=JourneyLocation;float h=MarsRegionalTerrain.MeasuredElevation(physical);
            ResetVisorPanels();yield return CaptureHelmetReview("MarsPathCleanVisor.png");
            bool priorPause=paused,priorDemo=demoPresentation;float progress=routeProgress;
            showMissionMenu=true;yield return CaptureHelmetReview("MarsPathVisorMissionMenu.png");
            okay&=priorPause==paused&&priorDemo==demoPresentation&&Mathf.Abs(progress-routeProgress)<.00001f;
            showMissionDetails=true;showPlaybackControls=true;yield return CaptureHelmetReview("MarsPathVisorDataPanels.png");
            ResetVisorPanels();UseTimedPlayback(30);SeekFullJourney(.63f);okay&=TimedControlsVisible&&OfficialMeshMatches(surfaceRoot);
            ResetVisorPanels();okay&=!showMissionMenu&&!showMissionDetails&&!showPlaybackControls;
            GeoPoint reported=new GeoPoint{latitude=22.64165f,longitude=65.79308f};
            Debug.Log($"ACCURACY_COVERAGE: reported point {MarsRegionalTerrain.SourceTag(reported)}; native HiRISE example {h:0.000} m / route {length:0.0} m; no displacement added to measured geometry");
            Debug.Log(okay?"ACCURACY_UX_PASSED: bounded near-antipodal/east-west/dateline planning, exact route endpoints, measured metre-scale mesh, clean visor/menu/timeline controls.":"ACCURACY_UX_FAILED: review planetary or visor invariants");Application.Quit(okay?0:2);
        }
    }
}

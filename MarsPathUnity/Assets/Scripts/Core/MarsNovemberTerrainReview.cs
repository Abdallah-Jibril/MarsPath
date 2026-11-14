using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace MarsPath
{
    public sealed class MarsNovemberTerrainReview:MonoBehaviour
    {
        private bool jezeroRockFieldOnly;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            foreach(string arg in Environment.GetCommandLineArgs())
            {
                if(arg!="--mars-nov3-review"&&arg!="--mars-jezero-rock-review")continue;
                var review=new GameObject("Surface rock acceptance").AddComponent<MarsNovemberTerrainReview>();
                review.jezeroRockFieldOnly=arg=="--mars-jezero-rock-review";
                break;
            }
        }
        private IEnumerator Start()
        {
            yield return null;
            var demo=FindAnyObjectByType<MarsPathDemoBootstrap>();
            yield return jezeroRockFieldOnly?demo.ReviewJezeroRockField():demo.ReviewNovemberTerrain();
        }
    }
    public partial class MarsPathDemoBootstrap
    {
        public IEnumerator ReviewJezeroRockField()
        {
            RefinementEnabled=false;
            yield return PrepareJezeroShowcase();
            if(!GlobalView.IsVisible)yield return PrepareHelmetEntry(false);
            paused=true;
            if(surfaceActive)
            {
                UseTimedPlayback(1);
                for(int i=0;i<32;i++)
                {
                    SeekFullJourney(i/32f);
                    UpdateHazardProximity(mission.routes[selectedRoute]);
                    if(rockAhead&&rockFieldCount>=4&&rockAheadDistance>=2f)
                    {
                        Debug.Log($"JEZERO_ROCK_FIELD_PASSED: {rockFieldCount} exposed rendered rocks / {rockAheadDistance:0.0} m / {routeProgress:P0} of route / {JourneyLocation.latitude:R},{JourneyLocation.longitude:R}");
                        yield return CaptureHelmetReview("MarsPathJezeroRockField.png");
                        Application.Quit(0);
                        yield break;
                    }
                    yield return null;
                }
            }
            Debug.LogError("JEZERO_ROCK_FIELD_FAILED: no visible rock field from the sampled route stations.");
            Application.Quit(2);
        }

        public IEnumerator ReviewNovemberTerrain()
        {
            RefinementEnabled=false;GeoPoint a=new GeoPoint{latitude=25.33859f,longitude=78.30515f,label="Selected start"},b=new GeoPoint{latitude=18.4672f,longitude=77.4298f,label="Jezero"};
            GlobalView.SetEndpoints(a,b);float began=Time.realtimeSinceStartup;yield return BuildSelectedRegion(a,b);yield return null;
            float planner=Time.realtimeSinceStartup-began;
            bool valid=cameraRig.RoutesInsideViewport(mission)&&terrainRoot.transform.Find("Regional map edge")==null&&terrainRoot.transform.Find("Continuous measured map surroundings")!=null;
            Debug.Log($"NOVEMBER_PLANNER: {planner:0.000} s / projected surroundings cover viewport / {MarsRegionalTerrain.SourceTag(a)}");
            yield return CaptureHelmetReview("MarsPathNovemberPlanner.png");
            began=Time.realtimeSinceStartup;demoPresentation=true;yield return PrepareHelmetEntry(false);paused=true;float entry=Time.realtimeSinceStartup-began;
            valid&=OfficialMeshMatches(surfaceRoot)&&MarsSurfaceRockScatter.SurfaceRockCount>50&&MarsSurfaceRockScatter.RockSensorSelfCheck();
            var rocks=new Dictionary<ulong,Vector3>();foreach(var r in MarsSurfaceRockScatter.VisibleRocks)rocks[r.GeographicId]=r.Center;
            MarsSurfaceRockScatter.SetFocus(astronaut.transform.position+Vector3.right*10);int common=0;
            foreach(var r in MarsSurfaceRockScatter.VisibleRocks)if(rocks.TryGetValue(r.GeographicId,out Vector3 prior)){common++;valid&=Vector3.Distance(r.Center,prior)<.005f;}
            valid&=common>35;Debug.Log($"NOVEMBER_GROUND: {entry:0.000} s / {MarsSurfaceRockScatter.SurfaceRockCount} visible illustrative rocks / {common} stable overlapping positions / source {DataTag}");
            yield return CaptureHelmetReview("MarsPathNovemberVisor.png");
            // Use the actual rendered model sensor, with the camera behind one
            // rock, to verify the icon/proximity alert in a native screenshot.
            foreach(var rock in MarsSurfaceRockScatter.VisibleRocks)if(rock.HeightMetres>.20f)
            {
                Vector3 p=rock.Center-astronaut.transform.forward*(rock.Radius+3);p.y=MarsTerrainGenerator.SampleHeight(p.x,p.z)+.76f;astronaut.transform.position=p;
                rockAhead=MarsSurfaceRockScatter.TryRockAhead(p,astronaut.transform.forward,out rockAheadDistance);cameraRig.SnapToMission(astronaut.transform);break;
            }
            valid&=rockAhead;yield return CaptureHelmetReview("MarsPathNovemberRockAlert.png");
            SeekFullJourney(0);cameraRig.SetHelmetView(false);cameraRig.SnapToMission(astronaut.transform);yield return CaptureHelmetReview("MarsPathNovemberChase.png");cameraRig.SetHelmetView(true);
            for(int shot=1;shot<8;shot++){AdvancePresentation(15,mission.routes[selectedRoute]);valid&=OfficialMeshMatches(surfaceRoot)&&VisibleSurfaceArrows>0&&MarsSurfaceRockScatter.SurfaceRockCount>0;}
            AdvancePresentation(15,mission.routes[selectedRoute]);valid&=DemoEnded&&MarsGeospatial.AngularDistanceDegrees(JourneyLocation,DemoPreviewEnd)<.00002f;
            UseTimedPlayback(30);foreach(float station in new[]{.25f,.65f,.98f,1f}){SeekFullJourney(station);valid&=OfficialMeshMatches(surfaceRoot)&&MarsSurfaceRockScatter.RockSensorSelfCheck();}
            // Also retain the genuinely metre-scale ridge as a separate example.
            ReturnToGlobalMap();GlobalView.UseHiRiseTerrainExample();yield return BuildSelectedRegion(GlobalView.StartLocation,GlobalView.EndLocation);demoPresentation=true;yield return PrepareHelmetEntry(false);paused=true;
            valid&=MarsRegionalTerrain.SourceSpacingMeters(JourneyLocation)<2&&OfficialMeshMatches(surfaceRoot);
            yield return CaptureHelmetReview("MarsPathNovemberHiRise.png");
            valid&=planner<6&&entry<3;
            Debug.Log(valid?"NOVEMBER_TERRAIN_PASSED: continuous planner, measured terrain, anchored visible rocks and sensor, compact HUD, full + demo original endpoint; HiRISE retained.":"NOVEMBER_TERRAIN_FAILED: terrain or route invariant");Application.Quit(valid?0:2);
        }
    }
}

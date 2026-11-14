using System;
using System.Collections;
using System.IO;
using UnityEngine;

namespace MarsPath
{
    public partial class MarsPathDemoBootstrap
    {
        public static GeoPoint ReviewStart=>new GeoPoint{latitude=24.20234f,longitude=-56.43297f,label="Selected start"};
        public static GeoPoint ReviewEnd(bool far)=>far?new GeoPoint{latitude=18.4672f,longitude=77.4298f,label="Jezero"}:new GeoPoint{latitude=30.16405f,longitude=-56.51999f,label="Selected destination"};
        public void PrepareReportedRegionalScene(int view)
        {
            GeoPoint start=ReviewStart,end=ReviewEnd(view==1);
            if(view==3){start=new GeoPoint{latitude=18.455f,longitude=77.441f,label="Jezero"};end=MarsGeospatial.Destination(start,48,2.4f);}
            LoadRegionalMission(MarsRoutePlanner.Generate(start,end));GlobalView.SetEndpoints(start,end);GlobalView.OpenJezeroMission();
            if(view==2||view==4){demoPresentation=true;StartMission();AdvancePresentation(view==4?0:7,mission.routes[selectedRoute]);paused=view!=4;SetAstronautWalking(view==4);cameraRig.SnapToMission(astronaut.transform);}
        }
        public void CheckReportedRegionalScenes()
        {
            MarsPathCameraRig rig=FindAnyObjectByType<MarsPathCameraRig>();
            foreach(bool far in new[]{false,true})
            {
                PrepareReportedRegionalScene(far?1:0);
                if(MarsRegionalTerrain.PlanetaryView||!rig.RoutesInsideViewport(mission))throw new Exception("Reported regional routes not fitted to projected relief map");
                if(terrainRoot.transform.Find("Continuous planetary context")!=null)throw new Exception("Planner unexpectedly contains a globe");
                if(MarsAdaptivePlanningTerrain.VertexCount>0)
                {
                    // Adaptive terrain spends samples on actual curvature and
                    // route corridors. The regular mesh is boundary metadata.
                    if(MarsAdaptivePlanningTerrain.VertexCount<4096||MarsAdaptivePlanningTerrain.VertexCount>70000||MarsAdaptivePlanningTerrain.SampleCount>100000)
                        throw new Exception("Adaptive planning relief outside geometry budgets");
                }
                else if(terrainRoot.GetComponent<MeshFilter>().sharedMesh.vertexCount<37000)throw new Exception("Planning relief mesh undersampled");
            }
            // Check late presentation frames of a 7,000 km plan. The continuous
            // preview advances 1.6 m/s without percentage-precision stalls.
            RestartTwoMinuteDemo();StartMission();AdvancePresentation(90,mission.routes[selectedRoute]);
            Vector3 previous=astronaut.transform.position;float min=float.MaxValue,max=0;
            for(int i=0;i<120;i++)
            {
                AdvancePresentation(1f/60,mission.routes[selectedRoute]);Vector3 p=astronaut.transform.position;
                float step=Vector2.Distance(new Vector2(p.x,p.z),new Vector2(previous.x,previous.z));previous=p;
                min=Mathf.Min(min,step);max=Mathf.Max(max,step);
                if(VisibleSurfaceArrows<3)throw new Exception("Sparse surface guidance disappeared near end of continental route");
            }
            if(min<.024f||max>.030f)throw new Exception($"Late route frame steps stutter: min {min}, max {max}");
            UseTimedPlayback(1);SeekFullJourney(.86f);previous=astronaut.transform.position;
            float timedMin=float.MaxValue,timedMax=0;
            for(int i=0;i<120;i++)
            {
                AdvanceJourney(1f/60,mission.routes[selectedRoute]);UpdateTimedJourneyPosition(mission.routes[selectedRoute]);
                Vector3 p=astronaut.transform.position;float step=Vector2.Distance(new Vector2(p.x,p.z),new Vector2(previous.x,previous.z));previous=p;
                timedMin=Mathf.Min(timedMin,step);timedMax=Mathf.Max(timedMax,step);
            }
            if(timedMin<.005f||timedMax>.04f)throw new Exception($"Late full-mission steps stutter: {timedMin}, {timedMax}");
            // Same physical point across a 100 m terrain rebase must retain relief.
            GeoPoint a=JourneyLocation,b=MarsGeospatial.Destination(a,90,.1f);
            var p0=new MissionData{generatedRegion=true,regionSpanKm=.12f,regionCenter=a,currentLocation=a};MarsRegionalTerrain.Configure(p0);
            float before=MarsRegionalTerrain.MeasuredHeight(100,0);
            p0.regionCenter=b;p0.currentLocation=b;MarsRegionalTerrain.Configure(p0);
            float after=MarsRegionalTerrain.MeasuredHeight(0,0);
            if(Mathf.Abs(before-after)>1f)throw new Exception("Measured elevation changes during origin rebase: "+Mathf.Abs(before-after));
            float low=float.MaxValue,high=float.MinValue;
            for(int z=-8;z<=8;z++)for(int x=-8;x<=8;x++){float h=MarsTerrainGenerator.SampleHeight(x*8,z*8);low=Mathf.Min(low,h);high=Mathf.Max(high,h);}
            if(!float.IsFinite(high-low)||high-low>150)throw new Exception("Measured ground relief invalid: "+(high-low));
            paused=true;
            Debug.Log($"MARSPATH_REGIONAL_VISUAL_PASSED: 361/7157 km projected regional plans; late-route 60 Hz demo steps {min:0.00000}–{max:0.00000} m; full mission steps {timedMin:0.00000}–{timedMax:0.00000} m; anchored relief delta {Mathf.Abs(before-after):0.0000} m; near-ground range {high-low:0.00} m.");
        }
    }

    public sealed class MarsRegionalReviewCapture:MonoBehaviour
    {
        private static int view=-1;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            foreach(string arg in Environment.GetCommandLineArgs())if(arg.StartsWith("--mars-regional-review="))
            {if(int.TryParse(arg.Substring("--mars-regional-review=".Length),out view))new GameObject("Regional visual review").AddComponent<MarsRegionalReviewCapture>();}
        }
        private IEnumerator Start()
        {
            yield return null;var demo=FindAnyObjectByType<MarsPathDemoBootstrap>();
            if(view!=2&&view!=4)
            {
                GeoPoint start=MarsPathDemoBootstrap.ReviewStart,end=MarsPathDemoBootstrap.ReviewEnd(view==1);
                if(view==3){start=new GeoPoint{latitude=18.455f,longitude=77.441f};end=MarsGeospatial.Destination(start,48,2.4f);}
                yield return MarsRegionSourceCache.PrepareRegion(start,end,null);
            }
            if(view==2||view==4)
            {
                MarsPathDemoBootstrap.SurfaceSourceEndpoints(MarsPathDemoBootstrap.ReviewStart,out GeoPoint a,out GeoPoint b);
                yield return MarsRegionSourceCache.PrepareRegion(a,b,(p,message)=>{if(p==1)Debug.Log("MARSPATH_REVIEW_SOURCE: "+message);},null,true,false);
            }
            demo.PrepareReportedRegionalScene(view);
            if(view==4)
            {
                double sum=0;int count=0;float until=Time.realtimeSinceStartup+7;float peak=0;
                while(Time.realtimeSinceStartup<until)
                {yield return null;if(count>8)peak=Mathf.Max(peak,Time.unscaledDeltaTime);sum+=Time.unscaledDeltaTime;count++;}
                Debug.Log($"MARSPATH_NATIVE_WALK: {count/Math.Max(.001,sum):0.0} mean fps; peak frame {peak*1000:0.0} ms; source {MarsRegionalTerrain.SourceTag(demo.JourneyLocation)}");
            }
            yield return new WaitForSeconds(2);
            string path=Path.Combine(Application.persistentDataPath,"MarsPathRegionalReview"+view+".png");ScreenCapture.CaptureScreenshot(path);
            yield return new WaitForSeconds(2);Debug.Log("MARSPATH_REGIONAL_CAPTURE: "+path);Application.Quit(0);
        }
    }
}

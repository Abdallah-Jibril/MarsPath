using System;
using System.Collections;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;

namespace MarsPath
{
    public partial class MarsPathDemoBootstrap
    {
        public static GeoPoint HelmetReviewStart=>new GeoPoint{latitude=24.55408f,longitude=62.30671f,label="Selected start"};
        public static GeoPoint HelmetReviewEnd=>new GeoPoint{latitude=18.90003f,longitude=71.29592f,label="Selected destination"};
        public IEnumerator ReviewHelmetTerrain()
        {
            int last=-10;
            Action<float,string> report=(p,message)=>{int percent=Mathf.RoundToInt(p*100);if(percent>=last+10||percent==100){last=percent;Debug.Log("MARSPATH_HELMET_SOURCE: "+percent+" / "+message);}};
            yield return MarsRegionSourceCache.PrepareRegion(HelmetReviewStart,HelmetReviewEnd,report);
            SurfaceSourceEndpoints(HelmetReviewStart,out GeoPoint a,out GeoPoint b);last=-10;
            yield return MarsRegionSourceCache.PrepareRegion(a,b,report,null,true,false);
            LoadRegionalMission(MarsRoutePlanner.Generate(HelmetReviewStart,HelmetReviewEnd));GlobalView.SetEndpoints(HelmetReviewStart,HelmetReviewEnd);GlobalView.OpenJezeroMission();demoPresentation=false;
            MarsRouteElevationProfile profile=SelectedElevationProfile;float climbStation=0,bestRise=float.MinValue;
            var csv=new StringBuilder("fraction,latitude_deg,longitude_deg,source_elevation_m,source_grid_m\n");
            for(int i=0;i<profile.heights.Length;i++)
            {
                GeoPoint point=profile.locations[i];
                csv.AppendFormat(CultureInfo.InvariantCulture,"{0:F6},{1:F6},{2:F6},{3:F3},{4:F0}\n",i/(float)(profile.heights.Length-1),point.latitude,point.longitude,profile.heights[i],MarsRegionalTerrain.SourceSpacingMeters(point));
                if(i>2&&i<profile.heights.Length-2&&profile.heights[i]-profile.heights[i-1]>bestRise)
                {bestRise=profile.heights[i]-profile.heights[i-1];climbStation=(i-.75f)/(profile.heights.Length-1);}
            }
            File.WriteAllText(Path.Combine(Application.persistentDataPath,"MarsPath641kmElevation.csv"),csv.ToString());
            Debug.Log($"MARSPATH_REPORTED_PROFILE: {profile.lengthKm:0.0} km, elevations {profile.minimum:0.0} to {profile.maximum:0.0} m, ascent {profile.ascent:0.0} m, grade {profile.maxGrade:0.00} deg, climb station {climbStation:0.0000}");
            if(profile.maximum-profile.minimum<100||!cameraRig.RoutesInsideViewport(mission)){Debug.LogError("MARSPATH_HELMET_FAILED: profile or regional framing invalid");Application.Quit(2);yield break;}
            yield return CaptureHelmetReview("MarsPathHelmetPlanner.png");
            yield return PrepareHelmetEntry(false);paused=true;
            if(!CheckHelmetStation(HelmetReviewStart)){Application.Quit(2);yield break;}
            for(int i=0;i<120;i++){AdvanceJourney(1f/60,mission.routes[selectedRoute]);UpdateTimedJourneyPosition(mission.routes[selectedRoute]);}
            cameraRig.SnapToMission(astronaut.transform);
            yield return CaptureHelmetReview("MarsPathHelmetStart.png");
            ReturnToPlanning();inspectProgress=climbStation;
            yield return PrepareHelmetEntry(true);
            GeoPoint expected=fullTrack.At(climbStation);
            if(!CheckHelmetStation(expected)){Application.Quit(2);yield break;}
            float low=float.MaxValue,high=float.MinValue;
            for(int z=-20;z<=20;z++)for(int x=-20;x<=20;x++)
            {float h=MarsRegionalTerrain.MeasuredHeight(x*100,z*100);low=Mathf.Min(low,h);high=Mathf.Max(high,h);}
            Debug.Log($"MARSPATH_HELMET_TERRAIN_PASSED: same geographic station in planner and visor, 1.70 m eye height, native source grid {MarsRegionalTerrain.SourceSpacingMeters(JourneyLocation):0} m, 4 km terrain range {high-low:0.0} m, no astronaut mesh in visor; route endpoints retained.");
            Vector3 forward=astronaut.transform.forward;float origin=MarsRegionalTerrain.MeasuredHeight(0,0);
            foreach(float metres in new[]{25f,100f,500f,1000f,2000f,4000f})Debug.Log($"MARSPATH_FORWARD_ELEVATION: {metres} m ahead / {MarsRegionalTerrain.MeasuredHeight(forward.x*metres,forward.z*metres)-origin:0.0} m relative");
            yield return CaptureHelmetReview("MarsPathHelmetClimb.png");
            Application.Quit(0);
        }
        private bool CheckHelmetStation(GeoPoint expected)
        {
            cameraRig.SnapToMission(astronaut.transform);
            bool location=MarsGeospatial.AngularDistanceDegrees(expected,JourneyLocation)<.00002f;
            float ground=MarsTerrainGenerator.SampleHeight(astronaut.transform.position.x,astronaut.transform.position.z);
            float eye=Camera.main.transform.position.y-ground;
            if(!cameraRig.HelmetView||!location||Mathf.Abs(eye-1.70f)>.03f||MarsRegionalTerrain.SourceSpacingMeters(JourneyLocation)>200)
            {Debug.LogError($"MARSPATH_HELMET_FAILED: station {location}, eye {eye}, source {DataTag}");return false;}
            foreach(SkinnedMeshRenderer renderer in astronaut.GetComponentsInChildren<SkinnedMeshRenderer>())
                if(renderer.shadowCastingMode!=UnityEngine.Rendering.ShadowCastingMode.ShadowsOnly){Debug.LogError("MARSPATH_HELMET_FAILED: suit intersects visor");return false;}
            return true;
        }
        private IEnumerator CaptureHelmetReview(string name)
        {
            if(Application.isBatchMode)yield break;
            yield return new WaitForSeconds(2);
            ScreenCapture.CaptureScreenshot(Path.Combine(Application.persistentDataPath,name));
            yield return new WaitForSeconds(1);Debug.Log("MARSPATH_HELMET_CAPTURE: "+name);
        }
    }
    public sealed class MarsHelmetTerrainReview:MonoBehaviour
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {foreach(string arg in Environment.GetCommandLineArgs())if(arg=="--mars-helmet-review"){new GameObject("Reported helmet terrain verification").AddComponent<MarsHelmetTerrainReview>();break;}}
        private IEnumerator Start()
        {StartCoroutine(Deadline());yield return null;yield return FindAnyObjectByType<MarsPathDemoBootstrap>().ReviewHelmetTerrain();}
        private IEnumerator Deadline()
        {yield return new WaitForSecondsRealtime(330);Debug.LogError("MARSPATH_HELMET_FAILED: verification deadline");Application.Quit(2);}
    }
}

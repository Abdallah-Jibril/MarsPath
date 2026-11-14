using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace MarsPath
{
    public sealed class MarsFastTerrainReview:MonoBehaviour
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {foreach(string arg in Environment.GetCommandLineArgs())if(arg=="--mars-fast-terrain-review")new GameObject("Offline transition measurements").AddComponent<MarsFastTerrainReview>();}
        private IEnumerator Start()
        {StartCoroutine(Deadline());yield return null;yield return FindAnyObjectByType<MarsPathDemoBootstrap>().ReviewFastTerrain();}
        private IEnumerator Deadline(){yield return new WaitForSecondsRealtime(160);Debug.LogError("FAST_TERRAIN_FAILED: deadline");Application.Quit(2);}
    }
    public partial class MarsPathDemoBootstrap
    {
        [Serializable]private sealed class TransitionResult
        {public string scenario,source;public float plannerSeconds,helmetSeconds,maxStationSeconds,localReliefMeters;public int arrows;}
        [Serializable]private sealed class TransitionReport{public bool passed;public List<TransitionResult> transitions=new List<TransitionResult>();}
        private static bool FastTerrainCheck(bool condition,string message)
        {if(!condition)Debug.LogError("FAST_TERRAIN_ASSERT: "+message);return condition;}
        public IEnumerator ReviewFastTerrain()
        {
            RefinementEnabled=false;MarsTerrainCachePolicy.ReadPersistentCache=false;
            bool valid=FastTerrainCheck(MarsGlobalMola64.Available,"Offline MOLA64 files present");
            float[,] probes={{19.37447f,67.33276f,501.35676f},{-42.43f,70.5f,-6032.05005f},{18.65f,-133.8f,20023.20105f},{-30,-100,6851.25f},{0,0,-1517.75f},{0,180,-2562},{5,179.999f,-3118.12891f},{5,-179.999f,-3117.87109f},{90,0,-1983.5f},{-90,359.99f,3823.14063f},{24.83584f,64.00996f,1607.33166f}};
            for(int i=0;i<probes.GetLength(0);i++)
            {bool found=MarsGlobalMola64.TryElevation(probes[i,0],probes[i,1],out float h);valid&=found&&Mathf.Abs(h-probes[i,2])<.01f;Debug.Log($"MOLA64_GOLDEN: {probes[i,0]}, {probes[i,1]} / {h:R} m");}
            var report=new TransitionReport();
            GeoPoint reportedStart=new GeoPoint{latitude=19.37447f,longitude=67.33276f,label="Reported start"},reportedEnd=new GeoPoint{latitude=24.83584f,longitude=64.00996f,label="Reported destination"};
            for(int scenario=0;scenario<4;scenario++)
            {
                ReturnToGlobalMap();
                if(scenario==1)MarsTerrainCachePolicy.ReadPersistentCache=true;
                GeoPoint start=reportedStart,end=reportedEnd;
                if(scenario==2)
                {start=new GeoPoint{latitude=-14.01f,longitude=-58.59f,label="Valles Marineris"};end=MarsGeospatial.Destination(start,52,12);}
                if(scenario==3){GlobalView.UseHiRiseTerrainExample();start=GlobalView.StartLocation;end=GlobalView.EndLocation;}
                else GlobalView.SetEndpoints(start,end);
                var result=new TransitionResult{scenario=new[]{"Reported route / persistent cache ignored","Reported route / warm","Valles / other region","Jezero / bundled HiRISE 1 m"}[scenario]};
                float began=Time.realtimeSinceStartup;
                yield return BuildSelectedRegion(start,end);yield return null;
                result.plannerSeconds=Time.realtimeSinceStartup-began;
                valid&=FastTerrainCheck(!GlobalView.IsVisible&&mission.routes[selectedRoute].available&&cameraRig.RoutesInsideViewport(mission),result.scenario+" / route framing");
                if(scenario==0||scenario==3)yield return CaptureHelmetReview(scenario==0?"MarsPathFastPlanner400.png":"MarsPathFastHiRisePlanner.png");
                demoPresentation=true;began=Time.realtimeSinceStartup;
                yield return PrepareHelmetEntry(false);yield return null;paused=true;
                result.helmetSeconds=Time.realtimeSinceStartup-began;result.source=DataTag;result.arrows=VisibleSurfaceArrows;
                valid&=FastTerrainCheck(surfaceActive&&cameraRig.HelmetView&&result.arrows>=3&&OfficialMeshMatches(surfaceRoot),result.scenario+" / visor geometry and initial arrows");
                float low=float.MaxValue,high=float.MinValue;
                for(int z=-9;z<=9;z++)for(int x=-9;x<=9;x++){float h=MarsRegionalTerrain.MeasuredHeight(x*20,z*20);low=Mathf.Min(low,h);high=Mathf.Max(high,h);}
                result.localReliefMeters=high-low;
                if(scenario==0)yield return CaptureHelmetReview("MarsPathFastVisor400.png");
                if(scenario==3)
                {
                    valid&=MarsRegionalTerrain.SourceSpacingMeters(JourneyLocation)<2&&result.localReliefMeters>25;
                    yield return CaptureHelmetReview("MarsPathFastHiRiseVisor.png");
                    cameraRig.SetHelmetView(false);cameraRig.SnapToMission(astronaut.transform);yield return CaptureHelmetReview("MarsPathFastHiRiseChase.png");cameraRig.SetHelmetView(true);
                }
                for(int shot=1;shot<8;shot++)
                {
                    began=Time.realtimeSinceStartup;AdvancePresentation(15,mission.routes[selectedRoute]);
                    result.maxStationSeconds=Mathf.Max(result.maxStationSeconds,Time.realtimeSinceStartup-began);
                    valid&=FastTerrainCheck(OfficialMeshMatches(surfaceRoot)&&VisibleSurfaceArrows>0&&VisibleSurfaceArrows<=6,result.scenario+" / station "+shot+" / arrows "+VisibleSurfaceArrows);
                }
                AdvancePresentation(15,mission.routes[selectedRoute]);valid&=FastTerrainCheck(DemoEnded&&MarsGeospatial.AngularDistanceDegrees(JourneyLocation,DemoPreviewEnd)<.00002f,result.scenario+" / continuous preview end");
                // Thresholds catch accidental network calls and pathological
                // mesh rebuilds; exact measurements are recorded separately.
                valid&=result.plannerSeconds<6&&result.helmetSeconds<3&&result.maxStationSeconds<2;
                Debug.Log("FAST_TERRAIN_MEASURE: "+JsonUtility.ToJson(result));report.transitions.Add(result);
            }
            if(!Application.isBatchMode)
            {
                // Also test the real network-enabled path. It must cancel its
                // background jobs immediately when the user enters the visor.
                ReturnToGlobalMap();RefinementEnabled=true;GlobalView.SetEndpoints(reportedStart,reportedEnd);
                yield return BuildSelectedRegion(reportedStart,reportedEnd);yield return new WaitForSecondsRealtime(.5f);
                float began=Time.realtimeSinceStartup;demoPresentation=true;yield return PrepareHelmetEntry(false);paused=true;
                float seconds=Time.realtimeSinceStartup-began;
                valid&=FastTerrainCheck(seconds<3&&!IsRefiningTerrain&&surfaceActive,"Network-enabled refinement does not block visor entry");
                Debug.Log("FAST_TERRAIN_BACKGROUND_ENTRY: "+seconds.ToString("0.000")+" s");
                yield return new WaitForSecondsRealtime(10);
                valid&=FastTerrainCheck(OfficialMeshMatches(surfaceRoot)&&MarsGeospatial.AngularDistanceDegrees(JourneyLocation,reportedStart)<.001f,"Background source update retains actual location and measured mesh");
                ReturnToGlobalMap();yield return new WaitForSecondsRealtime(.3f);
            }
            report.passed=valid;File.WriteAllText(Path.Combine(Application.persistentDataPath,"MarsPathFastTerrainMetrics.json"),JsonUtility.ToJson(report,true));
            Debug.Log(valid?"FAST_TERRAIN_PASSED: local-only production transitions, quadrant/seam MOLA goldens, four regions, real relief, all eight demo stations and full endpoints.":"FAST_TERRAIN_FAILED: inspect measurements or source invariants");Application.Quit(valid?0:2);
        }
    }
}

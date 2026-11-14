using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace MarsPath
{
    public sealed class MarsNasaQualityReview:MonoBehaviour
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {foreach(string arg in Environment.GetCommandLineArgs())if(arg=="--mars-nasa-quality-review"||arg=="--mars-nasa-quality-export")new GameObject("NASA CTX quality acceptance").AddComponent<MarsNasaQualityReview>();}
        private IEnumerator Start(){yield return null;yield return FindAnyObjectByType<MarsPathDemoBootstrap>().ReviewNasaQuality();}
    }
    public partial class MarsPathDemoBootstrap
    {
        public IEnumerator ReviewNasaQuality()
        {
            RefinementEnabled=false;bool okay=true;
            byte[] sourceTile=Resources.Load<TextAsset>("NASA/CtxV01Tiles/6_20_83").bytes;
            okay&=MarsCtxImageIntegrity.IsCompleteImage(sourceTile);
            var truncated=new byte[sourceTile.Length-7];Array.Copy(sourceTile,truncated,truncated.Length);
            okay&=!MarsCtxImageIntegrity.IsCompleteImage(truncated);
            var a=new GeoPoint{latitude=24.67645f,longitude=58.36556f,label="Reported new start"};
            var b=new GeoPoint{latitude=27.68045f,longitude=53.42641f,label="Reported new destination"};
            ReturnToGlobalMap();GlobalView.SetEndpoints(a,b);float began=Time.realtimeSinceStartup;
            yield return BuildSelectedRegion(a,b);float planSeconds=Time.realtimeSinceStartup-began;
            okay&=MarsRegionalTerrain.SourceSpacingMeters(a)<=75.01f&&MarsRegionalTerrain.SourceSpacingMeters(b)<=75.01f;
            okay&=Mathf.Abs(MarsRegionalTerrain.MeasuredElevation(a)-1210.0515f)<.2f&&Mathf.Abs(MarsRegionalTerrain.MeasuredElevation(b)+53.1357f)<.2f;
            var keys=new SortedSet<string>();
            Action<GeoPoint,float,bool> add=(p,size,detail)=>{foreach(string key in MarsCtxImageryCache.TileKeysForWindow(p,size,detail))keys.Add(key);};
            add(mission.regionCenter,mission.regionSpanKm*1.25f,false);
            foreach(RouteData route in mission.routes)if(route.available)
            {
                var t=new MarsGeographicTrack(mission,route);
                for(int i=0;i<MarsDemoDirector.TerrainStationCount;i++)add(t.At(MarsDemoDirector.StationProgress(i,t.LengthKm)),3.2f,true);
            }
            var reference=new GeoPoint{latitude=21.010789f,longitude=-22.531535f,label="NASA Trek reference crater"};
            add(reference,38,false);add(reference,3.2f,true);
            File.WriteAllLines(Path.Combine(Application.persistentDataPath,"MarsNasaCtxTileKeys.txt"),keys);
            Debug.Log($"NASA_QUALITY_EXPORT: {keys.Count} original tile keys / plan {planSeconds:0.000}s / team catalog {MarsHrscStereoTerrain.TeamCatalogCount}");
            if(Application.isBatchMode){Application.Quit(0);yield break;}

            yield return MarsCtxImageryCache.PrepareWindow(mission.regionCenter,mission.regionSpanKm*1.25f,35,null,false);
            okay&=MarsCtxImageryCache.ActivateAt(mission.regionCenter,false);
            terrainRoot.GetComponent<MarsTerrainGenerator>().RefreshSourceImagery();
            yield return CaptureHelmetReview("MarsPathNasa336Planner.png");
            RouteData selected=mission.routes[selectedRoute];var track=new MarsGeographicTrack(mission,selected);
            float minRelief=float.MaxValue,maxRelief=0;
            for(int i=0;i<8;i++)
            {
                GeoPoint point=track.At(MarsDemoDirector.StationProgress(i,track.LengthKm));
                yield return MarsCtxImageryCache.PrepareWindow(point,3.2f,18,null,true);
                okay&=MarsCtxImageryCache.Covers(point,12);
                Debug.Log($"NASA_QUALITY_IMAGE: station {i} / {point.latitude:0.00000},{point.longitude:0.00000} / image {MarsCtxImageryCache.SpacingAt(point):0.00}m / elevation {MarsRegionalTerrain.SourceSpacingMeters(point):0.00}m");
            }
            var view=cameraRig.CapturePlanningView();GeoPoint close=track.At(MarsDemoDirector.StationProgress(3,track.LengthKm));Vector2 local=MarsRegionalTerrain.ToWorld(close);
            var detailView=view;detailView.target=MarsRegionalTerrain.RenderPoint(local.x,local.y);detailView.distance=1.2f;detailView.pitch=48;
            cameraRig.RestorePlanningView(detailView);terrainRoot.GetComponent<MarsPlanningFocusTile>().RebuildNow();
            yield return MarsCtxImageryCache.PrepareWindow(close,12,25,null,true);MarsCtxImageryCache.ActivateViewAt(close,12);MarsCtxImageryCache.ActivateAt(close);terrainRoot.GetComponent<MarsTerrainGenerator>().RefreshSourceImagery();routeView.RefreshPlanningGeometry();
            okay&=routeView.VisiblePlanningArrowCount>0;
            yield return CaptureHelmetReview("MarsPathNasa336Zoom.png");cameraRig.RestorePlanningView(view);
            demoPresentation=true;began=Time.realtimeSinceStartup;yield return PrepareHelmetEntry(false);paused=true;
            Debug.Log($"NASA_QUALITY_ENTRY: {Time.realtimeSinceStartup-began:0.000}s");
            for(int shot=0;shot<8;shot++)
            {
                if(shot>0)AdvancePresentation(15,selected);
                MarsCtxImageryCache.ActivateAt(JourneyLocation);surfaceRoot.GetComponentInChildren<MarsTerrainGenerator>().RefreshSourceImagery();cameraRig.SnapToMission(astronaut.transform);
                okay&=OfficialMeshMatches(surfaceRoot)&&VisibleSurfaceArrows>0;
                var gravel=surfaceRoot.GetComponentInChildren<MarsSurfaceGravel>();okay&=gravel!=null&&gravel.InstanceCount>1500&&gravel.DrawBatchCount<=6;
                float lo=float.MaxValue,hi=float.MinValue;for(int z=-3;z<=3;z++)for(int x=-3;x<=3;x++){float h=MarsRegionalTerrain.MeasuredHeight(x*50,z*50);lo=Mathf.Min(lo,h);hi=Mathf.Max(hi,h);}
                minRelief=Mathf.Min(minRelief,hi-lo);maxRelief=Mathf.Max(maxRelief,hi-lo);
                if(shot==0||shot==2||shot==4||shot==6)yield return CaptureHelmetReview("MarsPathNasa336Visor"+shot+".png");
                Debug.Log($"NASA_QUALITY_GROUND: {shot} / relief {hi-lo:0.00}m / {DataTag} / gravel {gravel?.InstanceCount} / image {MarsCtxImageryCache.SpacingAt(JourneyLocation):0.00}m");
            }
            AdvancePresentation(15,selected);okay&=DemoEnded&&MarsGeospatial.AngularDistanceDegrees(JourneyLocation,DemoPreviewEnd)<.00002f;
            UseTimedPlayback(30);SeekFullJourney(.52f);okay&=OfficialMeshMatches(surfaceRoot);
            ReturnToGlobalMap();GeoPoint rEnd=MarsGeospatial.Destination(reference,42,20);GlobalView.SetEndpoints(reference,rEnd);yield return BuildSelectedRegion(reference,rEnd);
            yield return MarsCtxImageryCache.PrepareWindow(reference,38,30,null,false);MarsCtxImageryCache.ActivateAt(reference,false);terrainRoot.GetComponent<MarsTerrainGenerator>().RefreshSourceImagery();
            okay&=MarsRegionalTerrain.SourceSpacingMeters(reference)<20.1f&&Mathf.Abs(MarsRegionalTerrain.MeasuredElevation(reference)+3750.063f)<.2f;
            var refView=cameraRig.CapturePlanningView();Vector2 r=MarsRegionalTerrain.ToWorld(reference);refView.target=MarsRegionalTerrain.RenderPoint(r.x,r.y);refView.distance=66;refView.pitch=78;refView.yaw=0;cameraRig.RestorePlanningView(refView);
            yield return CaptureHelmetReview("MarsPathNasaTrekCrater.png");
            yield return MarsCtxImageryCache.PrepareWindow(reference,3.2f,18,null,true);MarsCtxImageryCache.ActivateAt(reference);
            demoPresentation=false;yield return PrepareHelmetEntry(false);paused=true;cameraRig.SnapToMission(astronaut.transform);
            okay&=OfficialMeshMatches(surfaceRoot)&&VisibleSurfaceArrows>0;
            yield return CaptureHelmetReview("MarsPathNasaTrekCraterVisor.png");
            ReturnToGlobalMap();
            foreach(GeoPoint probe in new[]{new GeoPoint{latitude=30,longitude=58.6f},new GeoPoint{latitude=31,longitude=59}})
            {
                float beforeGrid=MarsRegionalTerrain.SourceSpacingMeters(probe),fetchBegan=Time.realtimeSinceStartup;
                yield return MarsHrscStereoTerrain.PrepareNativeWindowIfAvailable(probe,()=>Time.realtimeSinceStartup-fetchBegan>24);
                float expected=probe.latitude==30?-49.815733f:-215.5388f;
                okay&=MarsRegionalTerrain.SourceSpacingMeters(probe)<=75.01f&&Mathf.Abs(MarsRegionalTerrain.MeasuredElevation(probe)-expected)<.2f;
                Debug.Log($"NASA_QUALITY_LIVE_HRSC: {probe.latitude:0.0},{probe.longitude:0.0} / before {beforeGrid:0}m / after {MarsRegionalTerrain.SourceSpacingMeters(probe):0}m / {MarsRegionalTerrain.MeasuredElevation(probe):0.000}m / {Time.realtimeSinceStartup-fetchBegan:0.00}s");
            }
            Debug.Log(okay?$"NASA_QUALITY_PASSED: photographic CTX overview/local images, 8 geographic stations, exact measured mesh, instanced gravel, demo/full endpoints; near relief {minRelief:0.0}–{maxRelief:0.0}m.":"NASA_QUALITY_FAILED");
            Application.Quit(okay?0:2);
        }
    }
}

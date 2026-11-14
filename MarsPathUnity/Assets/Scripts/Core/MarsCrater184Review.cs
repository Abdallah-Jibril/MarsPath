using System;
using System.Collections;
using UnityEngine;

namespace MarsPath
{
    public sealed class MarsCrater184Review:MonoBehaviour
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {foreach(string arg in Environment.GetCommandLineArgs())if(arg=="--mars-crater184-review")new GameObject("Reported crater / physical-scale acceptance").AddComponent<MarsCrater184Review>();}
        private IEnumerator Start(){yield return null;yield return FindAnyObjectByType<MarsPathDemoBootstrap>().ReviewCrater184();}
    }
    public partial class MarsPathDemoBootstrap
    {
        public IEnumerator ReviewCrater184()
        {
            RefinementEnabled=false;MarsTerrainCachePolicy.ReadPersistentCache=false;bool okay=MarsOfficialTerrain.ImageFootprintSelfCheck();
            GeoPoint start=new GeoPoint{latitude=24.15337f,longitude=83.02737f,label="Reported start"},end=new GeoPoint{latitude=26.60797f,longitude=84.74411f,label="Reported destination"};
            ReturnToGlobalMap();GlobalView.SetEndpoints(start,end);float began=Time.realtimeSinceStartup;
            yield return BuildSelectedRegion(start,end);SelectRoute(2);
            float plannerTime=Time.realtimeSinceStartup-began;
            okay&=MarsRegionalTerrain.DisplayReliefScale==1&&OfficialMeshMatches(terrainRoot)&&cameraRig.RoutesInsideViewport(mission);
            okay&=MarsRegionalTerrain.SourceSpacingMeters(start)<=100.01f;
            GeoPoint gold=new GeoPoint{latitude=25.2272575f,longitude=83.77844375f};
            okay&=MarsRegionalTerrain.SourceSpacingMeters(gold)<21&&Mathf.Abs(MarsRegionalTerrain.MeasuredElevation(gold)+3462.782f)<.05f;
            Debug.Log($"CRATER184_SOURCES: start {MarsRegionalTerrain.SourceTag(start)} / {MarsRegionalTerrain.MeasuredElevation(start):0.000} m; middle {MarsRegionalTerrain.SourceTag(gold)} / {MarsRegionalTerrain.MeasuredElevation(gold):0.000} m; planner {plannerTime:0.000} s");
            yield return CaptureHelmetReview("MarsPathCrater184PhysicalPlanner.png");
            var track=new MarsGeographicTrack(mission,mission.routes[selectedRoute]);
            var overview=cameraRig.CapturePlanningView();GeoPoint focus=track.At(.36f);Vector2 local=MarsRegionalTerrain.ToWorld(focus);
            MarsOfficialTerrain.ActivateCachedImagery(focus);terrainRoot.GetComponent<MarsTerrainGenerator>().RefreshSourceImagery();
            foreach(float zoom in new[]{12f,3f,.5f})
            {
                var view=overview;view.target=MarsRegionalTerrain.RenderPoint(local.x,local.y);view.distance=zoom;view.pitch=48;cameraRig.RestorePlanningView(view);
                terrainRoot.GetComponent<MarsPlanningFocusTile>().RebuildNow();routeView.RefreshPlanningGeometry();routeView.RefreshPlanningGuidance(Camera.main);
                okay&=routeView.VisiblePlanningArrowCount>0&&routeView.PlanningSelectedLinePixels>=3&&MarsPlanningFocusTile.VisibleVertexCount>0;
                Debug.Log($"CRATER184_ZOOM: {zoom:0.0} / {routeView.VisiblePlanningArrowCount} arrows / {MarsPlanningFocusTile.VisibleVertexCount} tile vertices / {MarsPlanningFocusTile.DisplayCellMeters:0.00} m display cells");
                if(zoom==3)yield return CaptureHelmetReview("MarsPathCrater184DetailZoom.png");
                if(zoom==.5f)
                {
                    if(!Application.isBatchMode)yield return new WaitForSecondsRealtime(12);
                    yield return CaptureHelmetReview("MarsPathCrater184CloseZoom.png");
                }
            }
            cameraRig.RestorePlanningView(overview);yield return null;
            demoPresentation=false;began=Time.realtimeSinceStartup;yield return PrepareHelmetEntry(false);paused=true;
            okay&=OfficialMeshMatches(surfaceRoot)&&VisibleSurfaceArrows>=3&&MarsRegionalTerrain.DisplayReliefScale==1;
            Debug.Log($"CRATER184_ENTRY: {Time.realtimeSinceStartup-began:0.000} s / {VisibleSurfaceArrows} surface arrows");
            yield return CaptureHelmetReview("MarsPathCrater184ActualStart.png");
            foreach(float station in new[]{.22f,.36f,.48f,.65f,.85f,1f})
            {
                SeekFullJourney(station);paused=true;okay&=OfficialMeshMatches(surfaceRoot)&&MarsRegionalTerrain.DisplayReliefScale==1;
                float low=float.MaxValue,high=float.MinValue;
                for(int z=-4;z<=4;z++)for(int x=-4;x<=4;x++){float h=MarsRegionalTerrain.MeasuredHeight(x*40,z*40);low=Mathf.Min(low,h);high=Mathf.Max(high,h);}
                Debug.Log($"CRATER184_STATION: {station:0.00} / {currentLatitude:0.000000},{currentLongitude:0.000000} / {DataTag} / near relief {high-low:0.00} m / grade {currentDemSlopeDegrees:0.00} deg");
                if(station==.36f||station==.48f)yield return CaptureHelmetReview(station==.36f?"MarsPathCrater184Rim.png":"MarsPathCrater184Inside.png");
                if(station==.36f)
                {
                    LightShadows originalShadows=RenderSettings.sun.shadows;RenderSettings.sun.shadows=LightShadows.None;
                    yield return CaptureHelmetReview("MarsPathCrater184NoShadows.png");RenderSettings.sun.shadows=originalShadows;
                    float oldBias=RenderSettings.sun.shadowBias,oldNormalBias=RenderSettings.sun.shadowNormalBias;
                    RenderSettings.sun.shadowBias=.2f;RenderSettings.sun.shadowNormalBias=.7f;
                    yield return CaptureHelmetReview("MarsPathCrater184ShadowBias.png");
                    RenderSettings.sun.shadowBias=oldBias;RenderSettings.sun.shadowNormalBias=oldNormalBias;
                    foreach(MeshRenderer renderer in surfaceRoot.GetComponentsInChildren<MeshRenderer>())foreach(Material material in renderer.sharedMaterials)if(material!=null&&material.HasProperty("_OrbitalBlend"))material.SetFloat("_OrbitalBlend",0);
                    yield return CaptureHelmetReview("MarsPathCrater184NoOrtho.png");surfaceRoot.GetComponentInChildren<MarsTerrainGenerator>().RefreshSourceImagery();
                    float largest=0;
                    for(int direction=0;direction<4;direction++)
                    {
                        Vector2 axis=new Vector2(Mathf.Cos(direction*Mathf.PI/4),Mathf.Sin(direction*Mathf.PI/4));
                        float prior=MarsRegionalTerrain.MeasuredHeight(-axis.x*200,-axis.y*200);
                        for(int i=1;i<=2000;i++)
                        {float d=-200+i*.2f,h=MarsRegionalTerrain.MeasuredHeight(axis.x*d,axis.y*d);largest=Mathf.Max(largest,Mathf.Abs(h-prior));prior=h;}
                    }
                    okay&=largest<.75f;Debug.Log($"CRATER184_CONTINUITY: largest .2 m walking height step {largest:0.0000} m");
                }
            }
            okay&=MarsGeospatial.AngularDistanceDegrees(JourneyLocation,end)<.00002f;
            Debug.Log(okay?"CRATER184_PASSED: exact reported route, actual offline HRSC/CTX heights, physical scale, view-centred measured zoom, adaptive arrows and full-route ground.":"CRATER184_FAILED");
            Application.Quit(okay?0:2);
        }
    }
}

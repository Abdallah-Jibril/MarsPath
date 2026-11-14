using System;
using System.Collections;
using System.IO;
using UnityEngine;

namespace MarsPath
{
    public sealed class MarsGlobalTerrainReview:MonoBehaviour
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            foreach(string arg in Environment.GetCommandLineArgs())
                if(arg=="--mars-global-terrain-review")
                {new GameObject("Global measured terrain acceptance").AddComponent<MarsGlobalTerrainReview>();break;}
        }
        private IEnumerator Start()
        {
            MarsTerrainCachePolicy.ReadPersistentCache=false;
            StartCoroutine(Deadline());yield return null;
            yield return FindAnyObjectByType<MarsPathDemoBootstrap>().ReviewGlobalTerrain();
        }
        private IEnumerator Deadline()
        {yield return new WaitForSecondsRealtime(180);Debug.LogError("GLOBAL_TERRAIN_FAILED: 180 second review deadline");Application.Quit(2);}
    }

    public partial class MarsPathDemoBootstrap
    {
        public IEnumerator ReviewGlobalTerrain()
        {
            RefinementEnabled=false;CancelTerrainRefinement();MarsTerrainCachePolicy.ReadPersistentCache=false;
            bool okay=MarsGlobalMola64.Available;
            GeoPoint[] sites={
                new GeoPoint{label="Gale rim",latitude=-4.70f,longitude=137.40f},
                new GeoPoint{label="Valles Marineris",latitude=-13.90f,longitude=-59.20f},
                new GeoPoint{label="Arbitrary Arabia",latitude=24.80f,longitude=14.20f},
                new GeoPoint{label="Olympus Mons",latitude=18.65f,longitude=-133.80f},
                new GeoPoint{label="Elysium Planitia",latitude=4.50f,longitude=135.90f},
                new GeoPoint{label="Utopia Planitia",latitude=46.70f,longitude=117.50f},
                new GeoPoint{label="Dateline",latitude=10,longitude=179.99f}
            };
            MissionData previous=MarsRegionalTerrain.Current;
            foreach(GeoPoint site in sites)
            {
                var sampleFrame=new MissionData{generatedRegion=true,regionCenter=site,currentLocation=site,regionSpanKm=1800};
                MarsRegionalTerrain.Configure(sampleFrame);
                float origin=MarsRegionalTerrain.MeasuredElevation(site),maximumError=0,minimum=float.MaxValue,maximum=float.MinValue,maxOldDifference=0;
                for(int z=-2;z<=2;z++)for(int x=-2;x<=2;x++)
                {
                    float px=x*15000/MarsRegionalTerrain.MetersPerUnit,pz=z*15000/MarsRegionalTerrain.MetersPerUnit;
                    GeoPoint p=MarsRegionalTerrain.ToGeo(px,pz);
                    float measured=MarsRegionalTerrain.MeasuredElevation(p);
                    float displayed=(MarsRegionalTerrain.SampleContextHeight(px,pz)+1.5f)*MarsRegionalTerrain.MetersPerUnit+origin;
                    maximumError=Mathf.Max(maximumError,Mathf.Abs(displayed-measured));
                    minimum=Mathf.Min(minimum,measured);maximum=Mathf.Max(maximum,measured);
                    maxOldDifference=Mathf.Max(maxOldDifference,Mathf.Abs(measured-MarsRegionalTerrain.Mola16Elevation(p.latitude,p.longitude)));
                }
                bool finite=!float.IsNaN(maximumError)&&!float.IsInfinity(maximumError);
                okay&=finite&&maximumError<.1f&&MarsRegionalTerrain.DisplayReliefScale==1;
                Debug.Log($"GLOBAL_TERRAIN_SOURCE: {site.label} / {site.latitude:0.00000},{site.longitude:0.00000} / {MarsRegionalTerrain.SourceTag(site)} / context error {maximumError:0.0000}m / measured relief {maximum-minimum:0.0}m / old 16ppd difference {maxOldDifference:0.0}m");
                yield return null;
            }
            if(previous!=null)MarsRegionalTerrain.Configure(previous);

            ReturnToGlobalMap();
            var coincident=new GeoPoint{label="Nearby endpoints",latitude=-26.13901f,longitude=48.54480f};
            GlobalView.SetEndpoints(coincident,MarsGeospatial.Destination(coincident,30,.05f));GlobalView.FocusLandmark(4);
            foreach(Vector2 anchor in new[]{new Vector2(430,142),new Vector2(1560,142),new Vector2(430,585),new Vector2(1560,585),new Vector2(950,370)})
            {
                var labels=MarsGlobalView.ArrangeEndpointLabels(anchor,true,anchor+Vector2.one,true);
                Rect bounds=MarsGlobalView.EndpointLabelBounds;
                okay&=!labels.startRect.Overlaps(labels.endRect)&&labels.startRect.width<130&&labels.endRect.width<130&&
                    bounds.Contains(labels.startRect.min)&&bounds.Contains(labels.startRect.max-Vector2.one*.01f)&&
                    bounds.Contains(labels.endRect.min)&&bounds.Contains(labels.endRect.max-Vector2.one*.01f);
            }
            yield return CaptureHelmetReview("MarsPathGlobalEndpointLayout.png");

            float[,] extremes={{0,-89.5f,0,89.5f},{10,179.5f,10,-179.5f}};
            for(int test=0;test<2;test++)
            {
                ReturnToGlobalMap();var a=new GeoPoint{latitude=extremes[test,0],longitude=extremes[test,1],label="Start"};
                var b=new GeoPoint{latitude=extremes[test,2],longitude=extremes[test,3],label="Destination"};
                GlobalView.SetEndpoints(a,b);long before=MarsGlobalMola64.BytesRead;float began=Time.realtimeSinceStartup;
                yield return BuildSelectedRegion(a,b);yield return null;
                float seconds=Time.realtimeSinceStartup-began;long bytes=MarsGlobalMola64.BytesRead-before;
                bool framed=cameraRig.RoutesInsideViewport(mission);
                okay&=seconds<8&&bytes<64L*1024*1024&&framed&&mission.routes[selectedRoute].available;
                Debug.Log($"GLOBAL_TERRAIN_BOUNDED: {test} / {seconds:0.000}s / {bytes} MOLA bytes / framed {framed} / {mission.routes[selectedRoute].distanceKm:0.0}km");
            }

            for(int scene=0;scene<3;scene++)
            {
                ReturnToGlobalMap();GeoPoint a=sites[scene],b=MarsGeospatial.Destination(a,scene==0?145:105,32);
                b.label=a.label+" target";GlobalView.SetEndpoints(a,b);float began=Time.realtimeSinceStartup;
                yield return BuildSelectedRegion(a,b);
                // These are diagnostic scenes, not user endpoints. Preserve
                // the production slope constraint and explicitly choose a
                // nearby admissible test traverse if the canyon wall blocks
                // the first one; never force a route through the cliff.
                for(int attempt=0;attempt<8&&!mission.routes[selectedRoute].available;attempt++)
                {
                    ReturnToGlobalMap();a=MarsGeospatial.Destination(sites[scene],attempt*45,2.4f);a.label=sites[scene].label+" / nearby diagnostic";
                    b=MarsGeospatial.Destination(a,attempt*45+90,3.2f);b.label=a.label+" target";
                    GlobalView.SetEndpoints(a,b);yield return BuildSelectedRegion(a,b);
                    Debug.Log($"GLOBAL_TERRAIN_DIAGNOSTIC_SELECTION: {scene} / {a.latitude:R},{a.longitude:R} -> {b.latitude:R},{b.longitude:R} / admissible {mission.routes[selectedRoute].available}");
                }
                if(!mission.routes[selectedRoute].available){okay=false;Debug.LogError("GLOBAL_TERRAIN_NO_TEST_TRAVERSE: "+sites[scene].label);continue;}
                okay&=cameraRig.RoutesInsideViewport(mission)&&mission.routes[selectedRoute].available&&MarsRegionalTerrain.DisplayReliefScale==1;
                float plannedElevation=MarsRegionalTerrain.MeasuredElevation(a);
                Debug.Log($"GLOBAL_TERRAIN_PLANNER: {a.label} / {Time.realtimeSinceStartup-began:0.000}s / {MarsRegionalTerrain.SourceTag(a)}");
                yield return CaptureHelmetReview("MarsPathGlobalTerrainPlanner"+scene+".png");
                demoPresentation=false;began=Time.realtimeSinceStartup;yield return PrepareHelmetEntry(false);paused=true;
                ResetVisorPanels();cameraRig.SetHelmetView(true);cameraRig.SnapToMission(astronaut.transform);
                float error=0;int count=0;
                bool surfaceOkay=surfaceRoot!=null&&OfficialMeshMatches(surfaceRoot)&&GlobalHorizonSourceMatches(out error,out count);
                okay&=surfaceOkay&&VisibleSurfaceArrows>0&&MarsRegionalTerrain.HumanScale&&Mathf.Abs(MarsRegionalTerrain.MetersPerUnit-1)<.001f&&
                    Mathf.Abs(MarsRegionalTerrain.MeasuredElevation(JourneyLocation)-plannedElevation)<.2f&&!showMissionDetails;
                Debug.Log($"GLOBAL_TERRAIN_VISOR: {a.label} / entry {Time.realtimeSinceStartup-began:0.000}s / {MarsRegionalTerrain.SourceTag(JourneyLocation)} / horizon {count} samples, max {error:0.000}m / same planner elevation {plannedElevation:0.000}m / geometry {surfaceOkay}");
                yield return CaptureHelmetReview("MarsPathGlobalTerrainVisor"+scene+".png");
            }

            bool live=Array.IndexOf(Environment.GetCommandLineArgs(),"--mars-global-terrain-live")>=0;
            if(live)
            {
                ReturnToGlobalMap();MarsTerrainCachePolicy.ReadPersistentCache=true;
                GeoPoint point=GlobalBaselineFocus(sites[2]);float before=MarsRegionalTerrain.SourceSpacingMeters(point),began=Time.realtimeSinceStartup;
                int initialHits=MarsRegionSourceCache.DemRangeCacheHits,initialWrites=MarsRegionSourceCache.DemRangeCacheWrites;
                yield return MarsRegionSourceCache.PrepareRegion(point,point,null,()=>Time.realtimeSinceStartup-began>5,true,false,5);
                bool firstReady=MarsRegionSourceCache.GridSpacingAt(sites[2])>0&&MarsRegionSourceCache.GridSpacingAt(sites[2])<=200.1f;
                began=Time.realtimeSinceStartup;
                yield return MarsRegionSourceCache.PrepareRegion(point,point,null,()=>Time.realtimeSinceStartup-began>18,true,false,18);
                float after=MarsRegionalTerrain.SourceSpacingMeters(point);
                int hits=MarsRegionSourceCache.DemRangeCacheHits-initialHits,writes=MarsRegionSourceCache.DemRangeCacheWrites-initialWrites;
                // Independent original 2x2 Int16 source cells, checked by GDAL
                // at the actual float coordinates 24.8f,14.2f. The request is
                // snapped for cache reuse; the assertion remains unsnapped.
                bool exact=MarsRegionSourceCache.TryElevation(sites[2],out float native)&&Mathf.Abs(native-(-2090.7139304122175f))<.01f;
                okay&=after<=200.1f&&exact&&(hits>0||firstReady)&&(writes>0||firstReady);
                Debug.Log($"GLOBAL_TERRAIN_LIVE: request {point.latitude:0.00000},{point.longitude:0.00000} / {before:0.0} -> {after:0.0}m grid / native Arabia {native:0.00000}m / exact {exact} / range hits {hits}, writes {writes}, ready after first budget {firstReady} / {MarsRegionSourceCache.LastError}");
            }
            MarsTerrainCachePolicy.ReadPersistentCache=true;
            Debug.Log(okay?"GLOBAL_TERRAIN_PASSED: seven off-Jezero measured providers, physical context and horizon, three planner/visor scenes, compact endpoints and bounded dateline/near-antipodal planning.":"GLOBAL_TERRAIN_FAILED: inspect source, physical geometry or performance assertions");
            Application.Quit(okay?0:2);
        }

        private bool GlobalHorizonSourceMatches(out float maximumError,out int count)
        {
            maximumError=0;count=0;float origin=MarsRegionalTerrain.MeasuredElevation(surfaceCenter);
            foreach(MeshFilter filter in surfaceRoot.GetComponentsInChildren<MeshFilter>())
            {
                if(filter.gameObject.name!="Measured terrain horizon")continue;
                Vector3[] vertices=filter.sharedMesh.vertices;
                for(int i=0;i<vertices.Length;i+=Mathf.Max(1,vertices.Length/800))
                {
                    Vector3 vertex=vertices[i];double horizontal=Math.Sqrt((double)vertex.x*vertex.x+(double)vertex.z*vertex.z);
                    if(horizontal<510)continue;
                    double angle=Math.Asin(Math.Min(1,horizontal/MarsRegionalTerrain.RadiusMeters));
                    double range=angle*MarsRegionalTerrain.RadiusMeters;
                    float factor=(float)(range/horizontal),x=vertex.x*factor,z=vertex.z*factor;
                    GeoPoint point=MarsRegionalTerrain.ToGeo(x,z);float measured=MarsRegionalTerrain.MeasuredElevation(point);
                    // Independent source height plus physical spherical drop;
                    // no call to the mesh's SampleHeight/Project implementation.
                    double expected=measured-origin-1.5+(MarsRegionalTerrain.RadiusMeters+measured)*(Math.Cos(angle)-1);
                    maximumError=Mathf.Max(maximumError,(float)Math.Abs(vertex.y-expected));count++;
                }
            }
            return count>20&&maximumError<.4f;
        }
    }
}

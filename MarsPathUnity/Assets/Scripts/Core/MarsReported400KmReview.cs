using System;
using System.Collections;
using UnityEngine;

namespace MarsPath
{
    public sealed class MarsReported400KmReview:MonoBehaviour
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {foreach(string arg in Environment.GetCommandLineArgs())if(arg=="--mars-reported-400km")new GameObject("Reported 400 km terrain and visor review").AddComponent<MarsReported400KmReview>();}
        private IEnumerator Start(){StartCoroutine(Deadline());yield return null;yield return FindAnyObjectByType<MarsPathDemoBootstrap>().ReviewReported400Km();}
        private IEnumerator Deadline(){yield return new WaitForSecondsRealtime(800);Debug.LogError("REPORTED400_FAILED: deadline");Application.Quit(2);}
    }
    public partial class MarsPathDemoBootstrap
    {
        public IEnumerator ReviewReported400Km()
        {
            GeoPoint start=new GeoPoint{latitude=19.37447f,longitude=67.33276f,label="Selected start"},end=new GeoPoint{latitude=24.83584f,longitude=64.00996f,label="Selected destination"};
            bool raw=MarsHrscStereoTerrain.TryElevation(start,out float actual,out float blend)&&Mathf.Abs(actual-471.77017f)<.02f&&blend>.99f;
            if(!raw){Debug.LogError("REPORTED400_FAILED: native HRSC value / "+actual);Application.Quit(2);yield break;}
            Debug.Log("REPORTED400_HRSC_GOLDEN: "+actual.ToString("R")+" m / original 75 m sinusoidal areoid strip / independent GDAL verified");
            yield return MarsRegionSourceCache.PrepareRegion(start,end,(p,s)=>{if(p==1)Debug.Log("REPORTED400_MAP_SOURCE: "+s);});
            LoadRegionalMission(MarsRoutePlanner.Generate(start,end));GlobalView.SetEndpoints(start,end);GlobalView.OpenJezeroMission();
            if(!cameraRig.RoutesInsideViewport(mission)||MarsRegionalTerrain.DisplayReliefScale!=1||!OfficialMeshMatches(terrainRoot)){Debug.LogError("REPORTED400_FAILED: physical relief or route framing");Application.Quit(2);yield break;}
            yield return CaptureHelmetReview("MarsPath400KmPlanner.png");
            MarsTerrainGenerator.PlanningLayer=2;terrainRoot.GetComponent<MarsTerrainGenerator>().RefreshMapLayer();
            yield return CaptureHelmetReview("MarsPath400KmSlope.png");
            MarsTerrainGenerator.PlanningLayer=1;terrainRoot.GetComponent<MarsTerrainGenerator>().RefreshMapLayer();
            RouteData route=mission.routes[selectedRoute];float sourceHeight=MarsRegionalTerrain.MeasuredElevation(start);string waypoints=JsonUtility.ToJson(route);
            ChangeMapRelief(1);
            bool scale=JsonUtility.ToJson(mission.routes[selectedRoute])==waypoints&&Mathf.Abs(sourceHeight-MarsRegionalTerrain.MeasuredElevation(start))<.001f;
            ChangeMapRelief(6);
            if(!scale){Debug.LogError("REPORTED400_FAILED: display scale changed physical route");Application.Quit(2);yield break;}
            demoPresentation=true;yield return PrepareHelmetEntry(false);paused=true;
            bool okay=cameraRig.HelmetView&&MarsRegionalTerrain.DisplayReliefScale==1&&MarsRegionalTerrain.SourceSpacingMeters(JourneyLocation)<=75.1f&&OfficialMeshMatches(surfaceRoot);
            Rect safe=HelmetHudSafeRect;okay&=safe.xMin>=100&&safe.xMax<=1500&&safe.yMin>=60&&safe.yMax<=840;
            if(!okay){Debug.LogError("REPORTED400_FAILED: visor preparation / "+DataTag);Application.Quit(2);yield break;}
            yield return CaptureHelmetReview("MarsPath400KmVisor.png");
            for(int shot=0;shot<8;shot++)
            {
                if(shot>0)AdvancePresentation(15,mission.routes[selectedRoute]);
                okay&=OfficialMeshMatches(surfaceRoot)&&MarsRegionalTerrain.DisplayReliefScale==1;
                Debug.Log($"REPORTED400_DEMO: station {shot}, fraction {routeProgress:0.00000}, {DataTag}, surface {currentSlopeDegrees:0.00} deg");
                if(shot==3)
                {
                    cameraRig.SnapToMission(astronaut.transform);yield return CaptureHelmetReview("MarsPath400KmTerrainHighlight.png");
                    foreach(MeshRenderer renderer in surfaceRoot.GetComponentsInChildren<MeshRenderer>())foreach(Material material in renderer.sharedMaterials)if(material!=null&&material.HasProperty("_OrbitalBlend"))material.SetFloat("_OrbitalBlend",0);
                    yield return CaptureHelmetReview("MarsPath400KmGeometryOnly.png");
                    LightShadows shadows=RenderSettings.sun.shadows;RenderSettings.sun.shadows=LightShadows.None;
                    yield return CaptureHelmetReview("MarsPath400KmUnshadowed.png");RenderSettings.sun.shadows=shadows;
                    surfaceRoot.GetComponentInChildren<MarsTerrainGenerator>()?.RefreshSourceImagery();
                }
            }
            AdvancePresentation(15,mission.routes[selectedRoute]);okay&=DemoEnded&&MarsGeospatial.AngularDistanceDegrees(JourneyLocation,DemoPreviewEnd)<.00002f;
            UseTimedPlayback(50);SeekFullJourney(.35f);cameraRig.SetHelmetView(false);cameraRig.SnapToMission(astronaut.transform);
            yield return CaptureHelmetReview("MarsPath400KmChase.png");
            foreach(float p in new[]{0f,.25f,.5f,.75f,1f}){SeekFullJourney(p);okay&=OfficialMeshMatches(surfaceRoot)&&MarsRegionalTerrain.DisplayReliefScale==1;}
            Debug.Log(okay?"REPORTED400_PASSED: exact reported endpoints; 75 m PDS source; labelled map relief; 1:1 ground; all 8 terrain highlights and full route; visor-contained HUD with full-screen chase HUD.":"REPORTED400_FAILED: geometry continuity");
            Application.Quit(okay?0:2);
        }
    }
}

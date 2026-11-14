using System;
using System.Collections;
using UnityEngine;

namespace MarsPath
{
    public sealed class MarsSurfaceReliefReview : MonoBehaviour
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            foreach(string argument in Environment.GetCommandLineArgs())
                if(argument=="--mars-surface-relief-review")
                    new GameObject("Measured surface relief acceptance").AddComponent<MarsSurfaceReliefReview>();
        }

        private IEnumerator Start()
        {
            yield return null;
            yield return FindAnyObjectByType<MarsPathDemoBootstrap>().ReviewSurfaceRelief();
        }
    }

    public partial class MarsPathDemoBootstrap
    {
        public IEnumerator ReviewSurfaceRelief()
        {
            bool passed=true;
            GeoPoint[] starts={
                new GeoPoint{latitude=21.010789f,longitude=-22.531535f,label="Trek crater centre"},
                new GeoPoint{latitude=24.67645f,longitude=58.36556f,label="Selected route start"}
            };
            GeoPoint[] ends={
                MarsGeospatial.Destination(starts[0],42,20),
                new GeoPoint{latitude=27.68045f,longitude=53.42641f,label="Selected route destination"}
            };
            for(int scenario=0;scenario<starts.Length;scenario++)
            {
                ReturnToGlobalMap();
                RefinementEnabled=false;
                GlobalView.SetEndpoints(starts[scenario],ends[scenario]);
                yield return BuildSelectedRegion(starts[scenario],ends[scenario]);
                if(!mission.routes[selectedRoute].available)
                {
                    Debug.LogError("SURFACE_RELIEF_FAILED: no route in scenario "+scenario);
                    Application.Quit(2);
                    yield break;
                }
                if(scenario==0)
                {
                    showPlanningDetails=true;
                    showRouteLimits=false;
                    yield return CaptureHelmetReview("MarsPathTeamRouteData.png");
                    RouteData baseline=mission.routes[0];
                    var limits=new MarsRoutingOptions{
                        ConstraintsEnabled=true,MaxRisk=baseline.totalRisk*1.4f,
                        MaxTimeMinutes=baseline.etaMinutes*1.4f,
                        MaxEnergyKwh=baseline.estimatedEnergyKwh*1.4f
                    };
                    yield return ApplyRouteBudgets(limits);
                    passed&=mission.routes[0].available&&mission.routes[0].withinBudgets;
                    passed&=mission.routes[0].totalRisk<=limits.MaxRisk&&mission.routes[0].etaMinutes<=limits.MaxTimeMinutes&&mission.routes[0].estimatedEnergyKwh<=limits.MaxEnergyKwh;
                    yield return CaptureHelmetReview("MarsPathTeamRouteLimits.png");
                    showPlanningDetails=false;
                    showRouteLimits=false;
                }
                RefinementEnabled=true;
                demoPresentation=scenario==1;
                float began=Time.realtimeSinceStartup;
                yield return PrepareHelmetEntry(false);
                paused=true;
                float entrySeconds=Time.realtimeSinceStartup-began;
                passed&=entrySeconds<8&&OfficialMeshMatches(surfaceRoot);
                float waitUntil=Time.realtimeSinceStartup+18;
                while(Time.realtimeSinceStartup<waitUntil)
                {
                    passed&=surfaceGeometryJobs<=2&&surfaceImageJobs<=2;
                    if(MarsCtxImageryCache.View!=null&&MarsCtxImageryCache.ActivateViewAt(JourneyLocation,30)&&MarsCtxImageryCache.View.spacingMeters<=81.5f)break;
                    yield return null;
                }
                yield return CaptureHelmetReview("MarsPathRelief"+scenario+"Entry.png");
                if(scenario==0)
                {
                    SeekFullJourney(.4f);
                    float settle=Time.realtimeSinceStartup+3;
                    while(Time.realtimeSinceStartup<settle)yield return null;
                }
                else AdvancePresentation(45,mission.routes[selectedRoute]);
                paused=true;
                if(scenario==0)ExportSurfaceSourceComparison();
                cameraRig.SetHelmetView(true);
                cameraRig.SnapToMission(astronaut.transform);
                passed&=OfficialMeshMatches(surfaceRoot)&&VisibleSurfaceArrows>0&&SurfaceHorizonMatches();
                yield return CaptureHelmetReview("MarsPathRelief"+scenario+"Visor.png");
                cameraRig.SetHelmetView(false);
                cameraRig.SnapToMission(astronaut.transform);
                yield return CaptureHelmetReview("MarsPathRelief"+scenario+"Chase.png");
                float low=float.MaxValue,high=float.MinValue;
                foreach(MeshFilter filter in surfaceRoot.GetComponentsInChildren<MeshFilter>())
                {
                    if(filter.gameObject.name!="Measured terrain horizon")continue;
                    foreach(Vector3 vertex in filter.sharedMesh.vertices){low=Mathf.Min(low,vertex.y);high=Mathf.Max(high,vertex.y);}
                }
                Debug.Log($"SURFACE_RELIEF_CASE: {scenario} / {JourneyLocation.latitude:0.000000},{JourneyLocation.longitude:0.000000} / entry {entrySeconds:0.000}s / source {DataTag} / horizon range {high-low:0.0}m / active jobs {surfaceGeometryJobs},{surfaceImageJobs}");
                UseTimedPlayback(50);
                SeekFullJourney(1);
                passed&=MarsGeospatial.AngularDistanceDegrees(JourneyLocation,ends[scenario])<.00002f;
            }
            ReturnToGlobalMap();
            Debug.Log(passed?"SURFACE_RELIEF_PASSED: normal streaming entry, measured horizon, both cameras, sparse arrows and original endpoints.":"SURFACE_RELIEF_FAILED: inspect source/mesh/entry assertions.");
            Application.Quit(passed?0:2);
        }

        private void ExportSurfaceSourceComparison()
        {
            var csv=new System.Text.StringBuilder("x,z,latitude,longitude,measured,stereo,stereoBlend,regional,hrsc,hrscBlend\n");
            for(int z=-8000;z<=8000;z+=200)for(int x=-8000;x<=8000;x+=200)
            {
                GeoPoint point=MarsRegionalTerrain.ToGeo(x,z);
                bool stereo=MarsOfficialTerrain.TryMeasured(point,out float height,out float blend);
                bool regional=MarsRegionalDem.TryElevation(point,out float baseHeight);
                bool hrsc=MarsHrscStereoTerrain.TryElevation(point,out float hrscHeight,out float hrscBlend);
                csv.AppendFormat(System.Globalization.CultureInfo.InvariantCulture,"{0},{1},{2:R},{3:R},{4:R},{5:R},{6:R},{7:R},{8:R},{9:R}\n",x,z,point.latitude,point.longitude,
                    MarsRegionalTerrain.MeasuredElevation(point),stereo?height:float.NaN,blend,regional?baseHeight:float.NaN,hrsc?hrscHeight:float.NaN,hrscBlend);
            }
            System.IO.File.WriteAllText(System.IO.Path.Combine(Application.persistentDataPath,"MarsPathSurfaceSources.csv"),csv.ToString());
        }

        private bool SurfaceHorizonMatches()
        {
            float originElevation=MarsRegionalTerrain.MeasuredElevation(surfaceCenter);
            foreach(MeshFilter filter in surfaceRoot.GetComponentsInChildren<MeshFilter>())
            {
                if(filter.gameObject.name!="Measured terrain horizon")continue;
                Vector3[] vertices=filter.sharedMesh.vertices;
                for(int i=0;i<vertices.Length;i+=Mathf.Max(1,vertices.Length/1200))
                {
                    Vector3 actual=vertices[i];
                    double horizontal=Math.Sqrt((double)actual.x*actual.x+(double)actual.z*actual.z);
                    // Beyond the 500 m seam, invert only Mars' spherical map.
                    if(horizontal<510)continue;
                    double range=Math.Asin(Math.Min(1,horizontal/MarsRegionalTerrain.RadiusMeters))*MarsRegionalTerrain.RadiusMeters;
                    float factor=horizontal>0?(float)(range/horizontal):1;
                    float x=actual.x*factor,z=actual.z*factor;
                    Vector3 expected=MarsMeasuredHorizon.Project(x,z,MarsTerrainGenerator.SampleHeight(x,z),originElevation);
                    if(Mathf.Abs(actual.y-expected.y)>.25f)
                    {
                        Debug.LogError($"SURFACE_HORIZON_MISMATCH: {actual} vs {expected}");
                        return false;
                    }
                }
                return true;
            }
            return false;
        }
    }
}

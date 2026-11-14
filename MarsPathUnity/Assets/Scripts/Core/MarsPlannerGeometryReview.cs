using System;
using System.Collections;
using UnityEngine;

namespace MarsPath
{
    public sealed class MarsPlannerGeometryReview : MonoBehaviour
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            foreach(string argument in Environment.GetCommandLineArgs())
                if(argument=="--mars-planner-geometry-review")
                    new GameObject("Endpoint layout and physical planning review").AddComponent<MarsPlannerGeometryReview>();
        }
        private IEnumerator Start()
        {
            yield return null;
            yield return FindAnyObjectByType<MarsPathDemoBootstrap>().ReviewPlannerGeometry();
        }
    }

    public partial class MarsPathDemoBootstrap
    {
        public IEnumerator ReviewPlannerGeometry()
        {
            RefinementEnabled=false;
            var start=new GeoPoint{latitude=-26.13901f,longitude=48.54480f,label="Reported start"};
            var end=new GeoPoint{latitude=-22.25368f,longitude=51.56192f,label="Reported destination"};
            ReturnToGlobalMap();GlobalView.SetEndpoints(start,end);GlobalView.FocusLandmark(4);
            yield return new WaitForSecondsRealtime(.8f);
            bool passed=true;
            var labels=GlobalView.GetEndpointLabelLayout();
            passed&=labels.startVisible&&labels.endVisible&&!labels.startRect.Overlaps(labels.endRect);
            foreach(Vector2 edge in new[]{new Vector2(428,142),new Vector2(1559,142),new Vector2(428,587),new Vector2(1559,587),new Vector2(950,370)})
            {
                var same=MarsGlobalView.ArrangeEndpointLabels(edge,true,edge+Vector2.one,true);
                Rect bounds=MarsGlobalView.EndpointLabelBounds;
                passed&=!same.startRect.Overlaps(same.endRect)&&same.startRect.xMin>=bounds.xMin&&same.startRect.yMin>=bounds.yMin&&same.startRect.xMax<=bounds.xMax&&same.startRect.yMax<=bounds.yMax&&
                    same.endRect.xMin>=bounds.xMin&&same.endRect.yMin>=bounds.yMin&&same.endRect.xMax<=bounds.xMax&&same.endRect.yMax<=bounds.yMax;
            }
            yield return CaptureHelmetReview("MarsPathEndpointLabelsFixed.png");
            float began=Time.realtimeSinceStartup;
            yield return BuildSelectedRegion(start,end);
            passed&=cameraRig.RoutesInsideViewport(mission)&&MarsRegionalTerrain.DisplayReliefScale==1;
            yield return PreparePlanningPhotographs(mission,refinementTicket);
            var overview=cameraRig.CapturePlanningView();
            terrainRoot.GetComponent<MarsPlanningFocusTile>().RebuildNow();
            yield return CaptureHelmetReview("MarsPathPhysicalPlannerOverview.png");
            Debug.Log($"PLANNER_GEOMETRY_OVERVIEW: {Time.realtimeSinceStartup-began:0.000}s / map {mission.regionSpanKm:0.000}km / main mesh {MarsTerrainGenerator.PlanningOverviewCellMeters:0.00}m / detail {MarsPlanningFocusTile.DisplayCellMeters:0.00}m");

            // Independent original Float32 cells, from the source audit. Each
            // has at least 18 valid support cells, outside feathered boundaries.
            GeoPoint[] probes={
                new GeoPoint{latitude=-24.2884898511f,longitude=50.2063930947f},
                new GeoPoint{latitude=-24.0811879601f,longitude=50.2196103294f},
                new GeoPoint{latitude=-24.3274459111f,longitude=50.1479590046f}
            };
            float[] original={-834.888427734f,-68.040542603f,-318.367156982f};
            float center=MarsRegionalTerrain.MeasuredElevation(mission.regionCenter);
            for(int i=0;i<probes.Length;i++)
            {
                MarsOfficialTerrain.RestoreCachedGeometry(probes[i]);
                Vector2 local=MarsRegionalTerrain.ToWorld(probes[i]);
                cameraRig.InspectPlanningAt(MarsRegionalTerrain.RenderPoint(local.x,local.y));
                terrainRoot.GetComponent<MarsPlanningFocusTile>().RebuildNow();
                float measured=MarsRegionalTerrain.MeasuredElevation(probes[i]);
                bool found=MarsPlanningFocusTile.TryRenderedHeight(local.x,local.y,out float rendered);
                float renderedMeters=(rendered+1.5f)*MarsRegionalTerrain.MetersPerUnit+center;
                passed&=found&&Mathf.Abs(measured-original[i])<.2f&&Mathf.Abs(renderedMeters-original[i])<8;
                passed&=MarsRegionalTerrain.DisplayReliefScale==1&&cameraRig.PlanningInspectionActive;
                Debug.Log($"PLANNER_GEOMETRY_PROBE: {i} / raw {original[i]:0.000}m / measured {measured:0.000}m / mesh {renderedMeters:0.000}m / display {MarsPlanningFocusTile.DisplayCellMeters:0.00}m");
                if(i==0)
                {
                    yield return MarsCtxImageryCache.PrepareWindow(probes[i],22,12,()=>false,false);
                    MarsCtxImageryCache.ActivateAt(probes[i]);
                    terrainRoot.GetComponent<MarsTerrainGenerator>().RefreshSourceImagery();
                    terrainRoot.GetComponent<MarsPlanningFocusTile>().RebuildNow();
                    yield return CaptureHelmetReview("MarsPathPhysicalCraterDetail.png");
                }
            }
            cameraRig.TogglePlanningInspection();
            passed&=!cameraRig.PlanningInspectionActive&&(cameraRig.PlanningTarget-overview.target).sqrMagnitude<.001f&&Mathf.Abs(cameraRig.PlanningDistance-overview.distance)<.001f;
            passed&=cameraRig.RoutesInsideViewport(mission);
            Debug.Log(passed?"PLANNER_GEOMETRY_PASSED: separate endpoint labels, original native heights, physical 1:1 mesh, finer local inspection and overview restoration.":"PLANNER_GEOMETRY_FAILED: inspect endpoint/mesh/source assertions.");
            Application.Quit(passed?0:2);
        }
    }
}

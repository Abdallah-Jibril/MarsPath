using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

namespace MarsPath
{
    public sealed class MarsMissionZoneReview:MonoBehaviour
    {
        private bool reportedRoute;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            foreach(string argument in Environment.GetCommandLineArgs())
            {
                bool reported=argument=="--mars-jezero-reported-route-review";
                if(!reported&&argument!="--mars-mission-zone-review")continue;
                new GameObject("Measured mission zone acceptance").AddComponent<MarsMissionZoneReview>().reportedRoute=reported;
                break;
            }
        }
        private IEnumerator Start()
        {
            MarsTerrainCachePolicy.ReadPersistentCache=false;
            StartCoroutine(Deadline());yield return null;
            var demo=FindAnyObjectByType<MarsPathDemoBootstrap>();
            yield return reportedRoute?demo.ReviewReportedJezeroRoute():demo.ReviewMissionZone();
        }
        private IEnumerator Deadline()
        {yield return new WaitForSecondsRealtime(180);Debug.LogError("MISSION_ZONE_FAILED: acceptance deadline");Application.Quit(2);}
    }

    public partial class MarsPathDemoBootstrap
    {
        [Serializable]private sealed class ZoneReviewReference{public string source;public ZoneReviewReferencePoint[] points;}
        [Serializable]private sealed class ZoneReviewReferencePoint{public float latitude,longitude;public double heightMeters;}
        [Serializable]private sealed class ZoneReviewSource{public string dtmSource,elevationReferenceResource;}
        private struct ZoneReviewPoint{public GeoPoint point;public Vector2 uv;}
        private static ZoneReviewPoint ClosestZoneReviewPoint(List<ZoneReviewPoint> points,Vector2 target)
        {
            ZoneReviewPoint best=points[0];float distance=float.MaxValue;
            foreach(ZoneReviewPoint candidate in points)
            {float found=(candidate.uv-target).sqrMagnitude;if(found<distance){best=candidate;distance=found;}}
            return best;
        }
        private static bool ZoneReviewSamePoint(GeoPoint a,GeoPoint b)
        {return a!=null&&b!=null&&Mathf.Abs(a.latitude-b.latitude)<.000002f&&Mathf.Abs(MarsGeospatial.NormalizeLongitude(a.longitude-b.longitude))<.000002f;}
        private static bool ZoneReviewNativePoint(GeoPoint point,out float maximumError)
        {
            maximumError=float.PositiveInfinity;
            if(!MarsMissionZone.Contains(point)||MarsOfficialTerrain.IsEstimated(point)||MarsRegionalTerrain.SourceSpacingMeters(point)>1.1f)return false;
            if(!MarsOfficialTerrain.TryMeasured(point,out float measured,out float coverage)||coverage<.9999f)return false;
            float source=MarsMissionZone.Elevation(point);
            maximumError=Mathf.Max(Mathf.Abs(source-measured),Mathf.Abs(source-MarsRegionalTerrain.MeasuredElevation(point)));
            return !float.IsNaN(maximumError)&&maximumError<.08f;
        }
        private static bool ZoneReviewOriginalSource()
        {
            TextAsset definition=Resources.Load<TextAsset>("NASA/MissionZone/JezeroZone");
            if(definition==null){Debug.LogError("MISSION_ZONE_ORACLE_FAILED: zone metadata unavailable");return false;}
            var source=JsonUtility.FromJson<ZoneReviewSource>(definition.text);
            string resource=string.IsNullOrEmpty(source?.elevationReferenceResource)?"NASA/MissionZone/JezeroZoneElevationReference":source.elevationReferenceResource;
            TextAsset original=Resources.Load<TextAsset>(resource)??Resources.Load<TextAsset>("NASA/MissionZone/Jezero4kmElevationReference");
            if(original==null){Debug.LogError("MISSION_ZONE_ORACLE_FAILED: independent source samples unavailable");return false;}
            var reference=JsonUtility.FromJson<ZoneReviewReference>(original.text);
            if(reference==null||reference.points==null||reference.source!=source?.dtmSource)
            {Debug.LogError("MISSION_ZONE_ORACLE_FAILED: reference does not match this zone's original DTM");return false;}
            int checkedPoints=0;double maximumError=0;
            foreach(ZoneReviewReferencePoint sample in reference.points)
            {
                GeoPoint point=new GeoPoint{latitude=sample.latitude,longitude=sample.longitude};
                if(!MarsMissionZone.Contains(point))continue;
                double error=Math.Abs(MarsMissionZone.Elevation(point)-sample.heightMeters);
                maximumError=Math.Max(maximumError,error);checkedPoints++;
            }
            Debug.Log($"MISSION_ZONE_ORACLE: {checkedPoints} independently audited original GDAL samples / max error {maximumError:0.000000}m");
            return checkedPoints>=5&&maximumError<.02;
        }
        internal IEnumerator ReviewReportedJezeroRoute()
        {
            RefinementEnabled=false;
            var start=new GeoPoint{latitude=18.45925f,longitude=77.38559f,label="Start"};
            var destination=new GeoPoint{latitude=18.49730f,longitude=77.37571f,label="Destination"};
            GlobalView.OpenMissionZone();
            GlobalView.SetEndpoints(start,destination);
            yield return BuildSelectedRegion(start,destination);
            if(GlobalView.IsVisible||!MarsMissionZone.IsMission(mission)||!ZoneReviewSamePoint(mission.currentLocation,start)||
                !ZoneReviewSamePoint(mission.destination,destination)||!mission.routes[selectedRoute].available)
            {
                Debug.LogError("JEZERO_REPORTED_ROUTE_FAILED: reported endpoints did not yield a measured route");
                Application.Quit(2);
                yield break;
            }

            cameraRig.ResetPlanningView();
            cameraRig.ZoomPlanning(Mathf.Log(1.4f)/.12f);
            yield return CaptureHelmetReview("MarsPathJezeroReportedRoutePlanner.png");
            Vector3 target=cameraRig.PlanningTarget;
            GeoPoint focus=MarsRegionalTerrain.ToGeo(target.x,target.z);
            bool hasImage=MarsOfficialTerrain.TryActiveMatchingImage(focus,out string imageKind,out float spacing);
            Debug.Log($"JEZERO_REPORTED_ROUTE: {mission.routes[selectedRoute].distanceKm:0.00} km / zoom {cameraRig.PlanningZoom:0.00}x / mesh {PlanningMeshCellMeters:0.00} m / measured height {MarsRegionalTerrain.SourceSpacingMeters(focus):0.00} m / {(hasImage?$"{imageKind} {spacing:0.00} m":"context image")}");
            yield return PrepareHelmetEntry(false);
            paused=true;
            cameraRig.SnapToMission(astronaut.transform);
            yield return CaptureHelmetReview("MarsPathJezeroReportedRouteVisor.png");
            bool passed=surfaceActive&&Mathf.Abs(MarsRegionalTerrain.MeasuredElevation(JourneyLocation)-MarsMissionZone.Elevation(JourneyLocation))<.08f;
            Debug.Log(passed?"JEZERO_REPORTED_ROUTE_PASSED":"JEZERO_REPORTED_ROUTE_FAILED: measured ground mismatch");
            Application.Quit(passed?0:2);
        }

        public IEnumerator ReviewMissionZone()
        {
            RefinementEnabled=false;CancelTerrainRefinement();MarsTerrainCachePolicy.ReadPersistentCache=false;
            MarsMissionZone.RequiredForSelection=true;
            if(!MarsMissionZone.Available||MarsMissionZone.Image==null)
            {Debug.LogError("MISSION_ZONE_FAILED: offline native data or registered overview unavailable");Application.Quit(2);yield break;}
            bool passed=MarsMissionZone.SpacingMeters>0&&MarsMissionZone.SpacingMeters<=1.1f&&ZoneReviewOriginalSource();
            int deltaImageSamples=0;
            for(int i=0;i<=8;i++)
            {
                var point=new GeoPoint { latitude=18.44470f+.02250f*i/8, longitude=77.45100f-.02120f*i/8 };
                if(MarsOfficialTerrain.ActivateCachedImagery(point)&&
                    MarsOfficialTerrain.TryActiveMatchingImage(point,out _,out float spacing)&&Mathf.Approximately(spacing,.25f))
                    deltaImageSamples++;
                yield return null;
            }
            Debug.Log($"MISSION_ZONE_DELTA_IMAGE: {deltaImageSamples}/9 stations at 0.25 m on the western delta corridor.");
            passed&=deltaImageSamples==9;
            var valid=new List<ZoneReviewPoint>();
            for(int z=0;z<30;z++)for(int x=0;x<30;x++)
            {
                Vector2 uv=new Vector2(.07f+.86f*x/29,.07f+.86f*z/29);
                GeoPoint point=MarsMissionZone.AtNormalized(uv.x,uv.y);
                if(ZoneReviewNativePoint(point,out _))valid.Add(new ZoneReviewPoint{point=point,uv=uv});
            }
            if(valid.Count<9)
            {Debug.LogError("MISSION_ZONE_FAILED: insufficient fully supported real 1m interior samples / "+valid.Count);Application.Quit(2);yield break;}
            Vector2[] targets={new Vector2(.25f,.25f),new Vector2(.75f,.25f),new Vector2(.5f,.5f),new Vector2(.25f,.75f),new Vector2(.75f,.75f)};
            float largestError=0,minimum=float.MaxValue,maximum=float.MinValue;
            foreach(Vector2 target in targets)
            {
                ZoneReviewPoint sample=ClosestZoneReviewPoint(valid,target);
                passed&=ZoneReviewNativePoint(sample.point,out float error);largestError=Mathf.Max(largestError,error);
                float height=MarsMissionZone.Elevation(sample.point);minimum=Mathf.Min(minimum,height);maximum=Mathf.Max(maximum,height);
                passed&=(MarsMissionZone.Normalized(sample.point)-sample.uv).magnitude<.001f;
                Debug.Log($"MISSION_ZONE_NATIVE: {sample.point.latitude:R},{sample.point.longitude:R} / raw {height:0.000}m / composed error {error:0.00000}m / {MarsRegionalTerrain.SourceTag(sample.point)}");
            }
            Debug.Log($"MISSION_ZONE_DATA: {MarsMissionZone.Name} / {MarsMissionZone.WidthKm:0.000}x{MarsMissionZone.HeightKm:0.000}km / grid {MarsMissionZone.SpacingMeters:0.000}m / {valid.Count}/900 real interior points / five-point relief {maximum-minimum:0.0}m / maximum source disagreement {largestError:0.00000}m");

            ReturnToGlobalMap();GlobalView.OpenMissionZone();
            passed&=GlobalView.LocalZoneVisible;
            ZoneReviewPoint uiPoint=ClosestZoneReviewPoint(valid,new Vector2(.4f,.6f));
            passed&=GlobalView.TryZoneMapPick(uiPoint.uv,out GeoPoint picked)&&ZoneReviewSamePoint(picked,uiPoint.point);
            passed&=GlobalView.TryZoneScreenPosition(uiPoint.point,out Vector2 canvas)&&GlobalView.TryZoneCanvasPick(canvas,out GeoPoint canvasPick)&&ZoneReviewSamePoint(canvasPick,uiPoint.point);
            passed&=!GlobalView.TryZoneMapPick(new Vector2(-.01f,.5f),out _)&&!GlobalView.TryZoneMapPick(new Vector2(.5f,1.01f),out _);
            GlobalView.SetEndpoints(valid[0].point,valid[valid.Count-1].point);
            GeoPoint retained=GlobalView.StartLocation;
            var outside=new GeoPoint{latitude=(float)(MarsMissionZone.North+.02),longitude=MarsMissionZone.Center.longitude};
            passed&=!MarsMissionZone.Contains(outside)&&!GlobalView.ApplyManualCoordinates(false,outside.latitude.ToString("R",CultureInfo.InvariantCulture),outside.longitude.ToString("R",CultureInfo.InvariantCulture))&&ZoneReviewSamePoint(GlobalView.StartLocation,retained);
            passed&=GlobalView.ApplyManualCoordinates(false,retained.latitude.ToString("R",CultureInfo.InvariantCulture),retained.longitude.ToString("R",CultureInfo.InvariantCulture));
            yield return CaptureHelmetReview("MarsPathMissionZoneSelector.png");

            // Exercise the expanded sector end-to-end before nearby traverses.
            // Alternate wide candidates remain diagnostic choices under the
            // same 25-degree limit if a real scarp makes one pair infeasible.
            Vector2[] starts={new Vector2(.16f,.20f),new Vector2(.18f,.78f),new Vector2(.16f,.52f),new Vector2(.50f,.17f),new Vector2(.20f,.22f),new Vector2(.65f,.64f),new Vector2(.22f,.68f),new Vector2(.65f,.24f),new Vector2(.40f,.40f),new Vector2(.42f,.72f),new Vector2(.28f,.45f),new Vector2(.70f,.47f)};
            Vector2[] ends={new Vector2(.84f,.78f),new Vector2(.82f,.22f),new Vector2(.84f,.48f),new Vector2(.55f,.83f),new Vector2(.39f,.34f),new Vector2(.81f,.76f),new Vector2(.42f,.75f),new Vector2(.77f,.42f),new Vector2(.57f,.48f),new Vector2(.59f,.80f),new Vector2(.45f,.59f),new Vector2(.83f,.58f)};
            float wideMinimum=.5f*Mathf.Min(MarsMissionZone.WidthKm,MarsMissionZone.HeightKm);
            int accepted=0,acceptedWide=0,acceptedShort=0;GeoPoint firstStart=null,firstEnd=null;
            for(int attempt=0;attempt<starts.Length&&accepted<2;attempt++)
            {
                GeoPoint a=ClosestZoneReviewPoint(valid,starts[attempt]).point,b=ClosestZoneReviewPoint(valid,ends[attempt]).point;
                if(ZoneReviewSamePoint(a,b)||accepted>0&&ZoneReviewSamePoint(a,firstStart)&&ZoneReviewSamePoint(b,firstEnd))continue;
                float separation=MarsGeospatial.AngularDistanceDegrees(a,b)*Mathf.Deg2Rad*(float)MarsRegionalTerrain.RadiusMeters/1000;
                bool wide=separation>=wideMinimum;
                if(wide&&acceptedWide>0||!wide&&acceptedShort>0)continue;
                MissionData probe=null;string rejection=null;
                try{probe=MarsRoutePlanner.Generate(a,b,new MarsRoutingOptions{MissionZoneId=MarsMissionZone.Id,MaxSlopeDegrees=25});}
                catch(Exception e){rejection=e.Message;}
                int available=-1;
                if(probe?.routes!=null)for(int i=0;i<probe.routes.Length;i++)if(probe.routes[i].available){available=i;break;}
                Debug.Log($"MISSION_ZONE_DIAGNOSTIC_PAIR: {attempt} / {a.latitude:R},{a.longitude:R} -> {b.latitude:R},{b.longitude:R} / separation {separation:0.000}km, wide {wide} (minimum {wideMinimum:0.000}km) / admissible {available>=0} / unchanged 25 degree limit / {rejection}");
                if(available<0){yield return null;continue;}
                if(accepted==0){firstStart=a;firstEnd=b;}
                ReturnToGlobalMap();GlobalView.OpenMissionZone();GlobalView.SetEndpoints(a,b);
                float began=Time.realtimeSinceStartup;yield return BuildSelectedRegion(a,b);
                if(mission?.routes==null||!ZoneReviewSamePoint(mission.currentLocation,a)||!ZoneReviewSamePoint(mission.destination,b)||!MarsMissionZone.IsMission(mission))
                {passed=false;Debug.LogError("MISSION_ZONE_ENDPOINT_FAILED: original coordinates or zone identity changed");continue;}
                available=-1;for(int i=0;i<mission.routes.Length;i++)if(mission.routes[i].available){available=i;break;}
                if(available<0){passed=false;Debug.LogError("MISSION_ZONE_FAILED: UI flow disagrees with admissible diagnostic plan");continue;}
                SelectRoute(available);
                passed&=mission.routingOptions!=null&&Mathf.Abs(mission.routingOptions.MaxSlopeDegrees-25)<.001f&&cameraRig.RoutesInsideViewport(mission);
                int checkedPoints=0;
                foreach(RouteData route in mission.routes)if(route.available)
                {
                    var checkedTrack=new MarsGeographicTrack(mission,route);
                    foreach(RoutePoint waypoint in route.waypoints)
                    {passed&=MarsMissionZone.Contains(MarsRegionalTerrain.ToGeo(waypoint.x*60,waypoint.z*60));checkedPoints++;}
                    int intervals=Mathf.Clamp(Mathf.CeilToInt((float)checkedTrack.LengthKm*1000/3),2,4000);
                    for(int i=0;i<=intervals;i++){passed&=MarsMissionZone.Contains(checkedTrack.At(i/(float)intervals));checkedPoints++;}
                    passed&=ZoneReviewSamePoint(checkedTrack.At(0),a)&&ZoneReviewSamePoint(checkedTrack.At(1),b)&&route.maxSlopeDegrees<=25.05f;
                }
                var frozenTrack=new MarsGeographicTrack(mission,mission.routes[selectedRoute]);
                GeoPoint station=frozenTrack.At(.45f);float stationHeight=MarsMissionZone.Elevation(station);
                Debug.Log($"MISSION_ZONE_PLAN: {accepted} / {Time.realtimeSinceStartup-began:0.000}s / {checkedPoints} waypoint/intermediate containment checks / {mission.routes[selectedRoute].distanceKm:0.000}km");
                yield return CaptureHelmetReview("MarsPathMissionZonePlanner"+accepted+".png");
                demoPresentation=false;yield return PrepareHelmetEntry(false);paused=true;
                if(surfaceRoot==null){passed=false;Debug.LogError("MISSION_ZONE_FAILED: no physical surface");continue;}
                UseTimedPlayback(1);double initialMetres=surfaceMetres;AdvanceJourney(6,mission.routes[selectedRoute]);UpdateTimedJourneyPosition(mission.routes[selectedRoute]);
                bool moved=surfaceMetres>initialMetres+1;
                passed&=moved&&MarsMissionZone.Contains(JourneyLocation)&&MarsRegionalTerrain.HumanScale&&Mathf.Abs(MarsRegionalTerrain.MetersPerUnit-1)<.001f;
                SeekFullJourney(.45f);paused=true;ResetVisorPanels();cameraRig.SetHelmetView(true);cameraRig.SnapToMission(astronaut.transform);
                float physical=MarsRegionalTerrain.MeasuredElevation(JourneyLocation);
                passed&=ZoneReviewSamePoint(JourneyLocation,station)&&Mathf.Abs(physical-stationHeight)<.08f&&ZoneReviewNativePoint(JourneyLocation,out _)&&OfficialMeshMatches(surfaceRoot)&&VisibleSurfaceArrows>0;
                Debug.Log($"MISSION_ZONE_VISOR: {accepted} / same original station {JourneyLocation.latitude:R},{JourneyLocation.longitude:R} / zone {stationHeight:0.000}m, physical {physical:0.000}m / arrows {VisibleSurfaceArrows} / movement {moved}");
                yield return CaptureHelmetReview("MarsPathMissionZoneVisor"+accepted+".png");
                RestartTwoMinuteDemo();
                RouteData previewRoute=mission.routes[selectedRoute];
                AdvancePresentation(14.95f,previewRoute);
                double before=surfaceMetres;
                AdvancePresentation(.1f,previewRoute);
                passed&=surfaceMetres>before&&surfaceMetres-before<.25;
                AdvancePresentation(19.95f,previewRoute);paused=true;
                passed&=DemoMode&&MarsMissionZone.Contains(JourneyLocation)&&ZoneReviewSamePoint(JourneyLocation,fullTrack.At(JourneyProgress))&&OfficialMeshMatches(surfaceRoot)&&VisibleSurfaceArrows>0;
                cameraRig.SnapToMission(astronaut.transform);
                if(accepted==0)yield return CaptureHelmetReview("MarsPathMissionZoneDemo.png");
                if(wide)
                {
                    AdvancePresentation(85,previewRoute);
                    double atEnd=surfaceMetres;
                    passed&=DemoEnded&&paused&&!arrived&&System.Math.Abs(atEnd-192)<.01;
                    ContinueAfterDemo();
                    passed&=!DemoMode&&System.Math.Abs(surfaceMetres-atEnd)<.02;
                    AdvanceJourney(.1f,previewRoute);UpdateTimedJourneyPosition(previewRoute);
                    passed&=surfaceMetres>atEnd&&surfaceMetres-atEnd<1;
                }
                if(wide)acceptedWide++;else acceptedShort++;
                accepted++;
            }
            passed&=accepted==2&&acceptedWide>=1&&acceptedShort>=1;
            MarsTerrainCachePolicy.ReadPersistentCache=true;
            Debug.Log(passed?"MISSION_ZONE_PASSED: metadata-driven native sector, independent source checks, inside/outside guards, one wide end-to-end and one short constrained plan, bounded routes, original endpoints, matching physical full/demo views and motion.":$"MISSION_ZONE_FAILED: inspect source, selection, containment or physical view checks; admissible pairs {accepted}, wide {acceptedWide}, short {acceptedShort}");
            Application.Quit(passed?0:2);
        }
    }
}

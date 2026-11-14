using System.Collections;
using UnityEngine;

namespace MarsPath
{
    public class MarsPathRuntimeSmokeTest : MonoBehaviour
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void RunWhenHeadless()
        {
            if (!Application.isBatchMode) return;
            foreach(string argument in System.Environment.GetCommandLineArgs())if(argument=="--mars-jezero-showcase-review")return;
            foreach(string argument in System.Environment.GetCommandLineArgs())if(argument=="--mars-mission-zone-review")return;
            foreach(string argument in System.Environment.GetCommandLineArgs())if(argument=="--mars-global-terrain-review")return;
            foreach(string argument in System.Environment.GetCommandLineArgs())if(argument=="--mars-hirise-native-review")return;
            foreach(string argument in System.Environment.GetCommandLineArgs())if(argument=="--mars-reported199-review"||argument=="--mars-planner-geometry-review"||argument=="--mars-surface-relief-review"||argument=="--mars-team-routing-check")return;
            foreach(string argument in System.Environment.GetCommandLineArgs())if(argument=="--mars-accuracy-ux-review"||argument=="--mars-route-facts-check"||argument=="--mars-experience-review"||argument=="--mars-crater184-review"||argument=="--mars-nasa-quality-review"||argument=="--mars-nasa-quality-export")return;
            foreach(string argument in System.Environment.GetCommandLineArgs())if(argument=="--mars-source-check"||argument=="--mars-helmet-review"||argument=="--mars-official-terrain-check"||argument=="--mars-reported-400km"||argument=="--mars-fast-terrain-review"||argument=="--mars-nov3-review")return;
            new GameObject("MarsPath Runtime Checks").AddComponent<MarsPathRuntimeSmokeTest>();
        }

        private IEnumerator Start()
        {
            yield return null;
            try { CheckGlobalFlow(); }
            catch (System.Exception exception)
            {
                Debug.LogError("MARSPATH_RUNTIME_TEST_FAILED: " + exception);
                Application.Quit(2);
                yield break;
            }
            Application.Quit(0);
        }

        private static void CheckGlobalFlow()
        {

            MissionData mission = MarsPathDataLoader.LoadMockMission();
            TextAsset terrainHeight = Resources.Load<TextAsset>("NASA/JezeroHeight16");
            Vector3 globalStart = mission != null && mission.currentLocation != null
                ? MarsGeospatial.ToUnitSphere(mission.currentLocation.latitude, mission.currentLocation.longitude)
                : Vector3.zero;
            bool valid = mission != null && mission.routes != null && mission.routes.Length == 5
                && mission.currentLocation != null && mission.destination != null
                && Mathf.Abs(globalStart.magnitude - 1f) < 0.001f
                && terrainHeight != null && terrainHeight.bytes.Length == MarsTerrainGenerator.HeightDataResolution * MarsTerrainGenerator.HeightDataResolution * 2;
            if (!valid)
            {
                throw new System.Exception("Mission, planetary geolocation, route count, or Jezero height data is invalid.");
            }

            GeoPoint source = new GeoPoint { latitude = -23.1f, longitude = 179.9f };
            GeoPoint restored = MarsGeospatial.FromUnitSphere(MarsGeospatial.ToUnitSphere(source.latitude, source.longitude));
            Check(Mathf.Abs(source.latitude - restored.latitude) < 0.0001f, "Latitude round trip");
            Check(Mathf.Abs(MarsGeospatial.NormalizeLongitude(source.longitude - restored.longitude)) < 0.0001f, "Longitude round trip");
            Check(Vector3.Distance(MarsGeospatial.ToUnitSphere(0f, 90f), Vector3.left) < 0.0001f, "East-positive longitude, north-up outside view");
            Check(Vector3.Distance(MarsGeospatial.ToUnitSphere(90f, 0f), Vector3.up) < 0.0001f, "North pole");
            Check(Mathf.Abs(MarsGeospatial.AngularDistanceDegrees(new GeoPoint { longitude = 179f }, new GeoPoint { longitude = -179f }) - 2f) < 0.0001f, "Antimeridian crossing");
            Check(Mathf.Abs(MarsGeospatial.AngularDistanceDegrees(new GeoPoint(), new GeoPoint { longitude = 180f }) - 180f) < 0.0001f, "Antipodal distance");
            Check(MarsGeospatial.NormalizeLongitude(77.451f) == 77.451f, "Manual longitude retains float precision");
            float localAngle = MarsGeospatial.AngularDistanceDegrees(mission.currentLocation, mission.destination);
            Check(localAngle > 0.01f && localAngle < 0.1f, "Nearby EVA endpoint precision");

            MarsPathDemoBootstrap demo = Object.FindAnyObjectByType<MarsPathDemoBootstrap>();
            Check(demo != null && demo.GlobalView != null, "Global view initialization");
            MarsGlobalView globe = demo.GlobalView;
            Check(globe.IsVisible && globe.HasBasemap, "Global startup and NASA texture");
            globe.SelectLocation(source);
            globe.SelectLocation(new GeoPoint { latitude = 12f, longitude = -80f });
            Check(Mathf.Abs(globe.StartLocation.latitude - source.latitude) < 0.0001f, "Start selection");
            Check(Mathf.Abs(globe.EndLocation.longitude + 80f) < 0.0001f, "Destination selection");
            demo.PreparePlanningCapture();
            Check(!globe.IsVisible, "Open regional planning");
            demo.PrepareMissionCapture();
            Check(!globe.IsVisible, "Enter EVA");
            demo.ReturnToGlobalMap();
            Check(globe.IsVisible && Mathf.Abs(globe.StartLocation.latitude - source.latitude) < 0.0001f, "Return preserves bookmarks");
            globe.UseJezeroSample();
            Check(Mathf.Abs(globe.StartLocation.latitude - mission.currentLocation.latitude) < 0.0001f, "Restore Jezero sample");
            CheckRegionalPlanning(demo,globe,mission);
            Debug.Log("MARSPATH_RUNTIME_TEST_PASSED: Global/manual/landmark selection, MOLA elevation, CTX crop, five computed route objectives, geolocation round trips, dateline and polar regions, planner rejection, articulated suit rig, global -> regional planning -> EVA -> global.");
        }

        private static void CheckRegionalPlanning(MarsPathDemoBootstrap demo,MarsGlobalView globe,MissionData sample)
        {
            Check(MarsSiteCatalog.Sites.Length>=12,"Expanded site catalog");
            Check(!globe.ApplyManualCoordinates(false,"91","0"),"Reject latitude out of range");
            Check(!globe.ApplyManualCoordinates(false,"NaN","20"),"Reject non-finite coordinates");
            Check(!globe.ApplyManualCoordinates(false,"20E","30"),"Reject wrong hemisphere axis");
            Check(globe.ApplyManualCoordinates(false,"14.01S","58.59W"),"Hemisphere coordinate input");
            Check(Mathf.Abs(globe.StartLocation.latitude+14.01f)<0.0001f&&Mathf.Abs(globe.StartLocation.longitude+58.59f)<0.0001f,"Signed hemisphere values");
            Check(globe.ApplyManualCoordinates(true,"١٨٫٦٥","226.2"),"Arabic numerals and 0-360 longitude");
            Check(Mathf.Abs(globe.EndLocation.longitude+133.8f)<0.0001f,"Normalize east longitude");
            globe.UseSite(2,false);Check(globe.StartLocation.label=="Olympus Mons","Landmark endpoint selection");
            MarsRegionalTerrain.LoadData();Check(MarsRegionalTerrain.HasMola,"MOLA binary dimensions");
            Check(MarsRegionalTerrain.MolaElevation(18.65f,-133.8f)>14000,"Olympus positive MOLA elevation");
            Check(MarsRegionalTerrain.MolaElevation(-42.43f,70.5f)<-4000,"Hellas negative MOLA elevation");
            Check(Mathf.Abs(MarsRegionalTerrain.MolaElevation(5,180)-MarsRegionalTerrain.MolaElevation(5,-180))<0.01f,"MOLA dateline wrap");
            bool rejected=false;try{MarsRoutePlanner.Generate(sample.currentLocation,sample.currentLocation);}catch(System.ArgumentException){rejected=true;}Check(rejected,"Reject coincident endpoints");

            MissionData ctx=MarsRoutePlanner.Generate(sample.currentLocation,sample.destination);
            Check(ctx.usesJezeroDem,"Select high-resolution CTX inside Jezero crop");ValidateRoutes(ctx);
            globe.LocalTraverseAtSite(3);MissionData regional=MarsRoutePlanner.Generate(globe.StartLocation,globe.EndLocation);
            Check(!regional.usesJezeroDem&&regional.region.Contains("Valles"),"Regional plan is not reused Jezero geometry");ValidateRoutes(regional);
            Check(!SamePath(regional.routes[0],regional.routes[3]),"Science profile changes modeled route geometry");
            CheckFrameRoundTrip(globe.StartLocation);CheckFrameRoundTrip(globe.EndLocation);
            demo.LoadRegionalMission(regional);globe.OpenJezeroMission();demo.PrepareMissionCapture();demo.ReturnToGlobalMap();
            Check(demo.ActiveMission.region.Contains("Valles")&&globe.IsVisible,"New region EVA and return flow");
            MarsArticulatedAstronaut actor=Object.FindAnyObjectByType<MarsArticulatedAstronaut>();Check(actor!=null&&actor.HasArticulatedRig,"13-joint NASA suit rig");

            GeoPoint datelineStart=new GeoPoint{latitude=5,longitude=179.95f},datelineEnd=new GeoPoint{latitude=5,longitude=-179.95f};
            MissionData dateline=MarsRoutePlanner.Generate(datelineStart,datelineEnd);ValidateRoutes(dateline);CheckFrameRoundTrip(datelineStart);CheckFrameRoundTrip(datelineEnd);
            GeoPoint polar=new GeoPoint{latitude=89.8f,longitude=120};GeoPoint polarEnd=MarsGeospatial.Destination(polar,45,2.4f);
            MissionData polarMission=MarsRoutePlanner.Generate(polar,polarEnd);ValidateRoutes(polarMission);CheckFrameRoundTrip(polar);CheckFrameRoundTrip(polarEnd);
            MissionData longMission=MarsRoutePlanner.Generate(MarsSiteCatalog.Sites[2].Point,MarsSiteCatalog.Sites[1].Point);ValidateRoutes(longMission);
            Check(longMission.routes[0].etaMinutes>480,"Long traverses do not use sample EVA times");
            CheckEvaRegressions(demo,actor,sample);
            CheckFullJourney(demo,sample);
            CheckPresentationFlow(demo);
            demo.CheckReportedWalking();
            demo.CheckReportedRegionalScenes();
            MarsRegionalTerrain.Configure(regional);globe.UseJezeroSample();
        }
        private static void CheckEvaRegressions(MarsPathDemoBootstrap demo,MarsArticulatedAstronaut actor,MissionData sample)
        {
            MissionData reported=MarsRoutePlanner.Generate(sample.currentLocation,MarsSiteCatalog.Sites[6].Point);
            float lowest=float.MaxValue,highest=float.MinValue;
            for(int z=-5;z<=5;z++)for(int x=-5;x<=5;x++)
            {float y=MarsRegionalTerrain.SampleVisualHeight(x*12,z*12);lowest=Mathf.Min(lowest,y);highest=Mathf.Max(highest,y);}
            Check((highest-lowest)/MarsRegionalTerrain.DisplayReliefScale<2f,"Continental physical elevation is scaled without mountain needles");
            demo.LoadRegionalMission(reported);Check(!demo.CanStartSelectedEva,"Continental journey cannot enter suit EVA");
            demo.PlanLocalEvaSection();MissionData local=demo.ActiveMission;
            Check(demo.CanStartSelectedEva,"Explicit local section is suitable for EVA");
            float length=MarsGeospatial.AngularDistanceDegrees(local.currentLocation,local.destination)*Mathf.Deg2Rad*(float)MarsRegionalTerrain.RadiusMeters/1000;
            Check(Mathf.Abs(length-2.4f)<0.015f,"Local section is 2.4 km and preserves geographic scale");
            Check(MarsGeospatial.AngularDistanceDegrees(local.currentLocation,sample.currentLocation)<0.00001f,"Local section preserves original start");
            demo.StartFullRouteForTest();
            Check(MarsTerrainGenerator.SurfaceRockCount>0&&MarsTerrainGenerator.SurfaceRockCount<=MarsSurfaceRockScatter.MaximumRockCount&&MarsTerrainGenerator.RockSensorSelfCheck(),"Bounded illustrative rock field and visible-rock sensor are active on unchanged measured ground");
            Check(demo.CurrentSurfaceSlope>0.001f,"Rendered surface slope is initialized and not a zero placeholder");
            Check(actor.RigidBackpackVertices>100,"Rear life-support pack has rigid torso weights");
            MarsPathCameraRig rig=Object.FindAnyObjectByType<MarsPathCameraRig>();
            rig.ZoomPlanning(-100);Check(rig.PlanningDistance==520,"Planning camera can zoom beyond old 150-unit limit");
            rig.ZoomPlanning(100);Check(Mathf.Approximately(rig.PlanningDistance,rig.MinimumPlanningDistance),"Planning camera permits inspection at a geographic-scale-aware distance");rig.ResetPlanningView();

            MarsEvaSimulation suit=new MarsEvaSimulation();suit.Reset(local.telemetry);
            Check(Mathf.Abs(suit.MetabolicWatts-139)<.01f&&suit.OxygenGramsPerHour>34&&suit.OxygenGramsPerHour<36,
                "Initial stationary demand retains the nominal awake reference before walking");
            suit.Advance(1,0.7f,5,90);
            float walked=local.routes[0].distanceKm*1000/(local.routes[0].etaMinutes*60);
            Check(walked>0.1f&&walked<1.5f,"One real second advances a walking distance, not a continental jump");
            Check(suit.OxygenPercent>95.9f&&suit.OxygenPercent<96&&suit.ElapsedSeconds==1,"Oxygen uses elapsed seconds");
            float before=suit.OxygenPercent;suit.Advance(60,0.7f,18,70);float after=suit.OxygenPercent;
            suit.Advance(60,0.7f,0,95);Check(suit.OxygenPercent<after&&after<before,"Changing slope never restores consumed oxygen");
            suit.Advance(7*86400,0.7f,10,85);
            Check(suit.ReserveReached&&suit.OxygenPercent>=19.99f&&suit.PowerPercent>=14.99f&&suit.ElapsedSeconds<86400,"Reserve stop prevents empty-suit traversal");
            Debug.Log("MARSPATH_EVA_REGRESSIONS_PASSED: reported Jezero/Elysium journey, local section, terrain scale, initialized telemetry, elapsed-time consumption, reserve stop, slope, rock sensor, rigid backpack and expanded zoom.");
        }
        private static void CheckFrameRoundTrip(GeoPoint point)
        {
            Vector2 local=MarsRegionalTerrain.ToWorld(point);GeoPoint recovered=MarsRegionalTerrain.ToGeo(local.x,local.y);
            Check(MarsGeospatial.AngularDistanceDegrees(point,recovered)<0.001f,"Regional coordinate projection round trip");
        }
        private static void CheckFullJourney(MarsPathDemoBootstrap demo,MissionData sample)
        {
            Check(MarsRegionalTerrain.HasFullJezero,"Full CTX mosaic is bundled");
            Check(MarsRegionalTerrain.TryFullJezero(new GeoPoint{latitude=18.47f,longitude=77.31f},out _),"CTX coverage extends to western delta beyond old crop");
            MissionData full=MarsRoutePlanner.Generate(sample.currentLocation,MarsSiteCatalog.Sites[6].Point);
            MarsGeographicTrack track=new MarsGeographicTrack(full,full.routes[0]);
            Check(track.LengthKm>4000,"Full route retained across thousands of kilometers");
            Check(MarsGeospatial.AngularDistanceDegrees(track.At(1),full.destination)<0.00001f,"Track reaches original selected endpoint");
            demo.LoadRegionalMission(full);demo.GlobalView.OpenJezeroMission();demo.StartFullRouteForTest();
            Check(demo.SurfaceActive,"Full route opens moving surface terrain without truncating endpoints");
            demo.SeekFullJourney(0.63f);Check(Mathf.Abs(demo.JourneyProgress-0.63f)<0.00001f&&demo.ResupplyLeg>1,"Mid-route seek beyond initial local section");
            Check(MarsGeospatial.AngularDistanceDegrees(demo.JourneyLocation,track.At(0.63f))<0.001f,"Local surface position matches complete geographic track at 63 percent");
            Check(demo.CurrentOxygen>20,"Expedition resupply is explicit and supports a multi-day modeled journey");
            demo.SeekFullJourney(0.98f);Check(Mathf.Abs(demo.JourneyProgress-0.98f)<0.00001f,"Final portion of full route is accessible");
            Check(MarsGeospatial.AngularDistanceDegrees(demo.ActiveMission.destination,full.destination)<0.00001f,"Moving terrain never replaces the final destination");
            demo.SeekFullJourney(1);Check(MarsGeospatial.AngularDistanceDegrees(demo.JourneyLocation,full.destination)<0.00001f,"Surface reaches the complete original route destination");
            demo.SetPlaybackSpeed(50);demo.ReturnToGlobalMap();
            Check(!demo.SurfaceActive&&demo.GlobalView.IsVisible,"Return restores full planning frame");
            Debug.Log("MARSPATH_FULL_JOURNEY_PASSED: full Jezero CTX mosaic, original geographic endpoints, moving terrain at 63% and 98%, resupply legs, 50x playback, return to complete planning route.");
        }
        private static bool SamePath(RouteData a,RouteData b)
        {
            if(a.waypoints.Length!=b.waypoints.Length)return false;
            for(int i=0;i<a.waypoints.Length;i++)if(Mathf.Abs(a.waypoints[i].x-b.waypoints[i].x)>0.0001f||Mathf.Abs(a.waypoints[i].z-b.waypoints[i].z)>0.0001f)return false;
            return true;
        }
        private static void CheckPresentationFlow(MarsPathDemoBootstrap demo)
        {
            MarsDemoDirector director=new MarsDemoDirector();director.Reset();float previous=0;
            for(int i=0;i<239;i++){director.Advance(0.5f,404);Check(director.Progress>=previous&&director.Progress<1,"Demo progresses monotonically without ending early");previous=director.Progress;}
            director.Advance(0.5f,404);Check(director.Complete&&director.Elapsed==120&&
                System.Math.Abs(director.DistanceMeters-192)<.001&&Mathf.Abs(director.Progress-192f/404000)<.000001f,
                "A long-route presentation completes its continuous 192 m preview in 120 real seconds");
            MarsPathCameraRig rig=Object.FindAnyObjectByType<MarsPathCameraRig>();rig.ZoomPlanning(-100);
            demo.PrepareDemoRegionCapture(false);
            Check(rig.PlanningDistance<180&&rig.RoutesInsideViewport(demo.ActiveMission),"New selected region refits all five routes after extreme previous zoom");
            demo.RestartTwoMinuteDemo();demo.StartFullRouteForTest();
            Check(demo.DemoMode&&demo.VisibleSurfaceArrows>0&&demo.VisibleSurfaceArrows<=6,"Presentation defaults to demo and forward guidance stays sparse");
            Check(!demo.TimedControlsVisible,"Two-minute presentation hides mission-time speed controls");
            Check(Mathf.Abs(MarsRegionalTerrain.MetersPerUnit-1)<0.00001f,"Suit, camera and streamed surface use the same metre scale");
            Check(MarsRegionalDem.CropCount>=4,"Original offline HRSC/MOLA context crops remain bundled alongside added coverage");
            foreach(int site in new[]{1,2,3})Check(MarsRegionalDem.TryElevation(MarsSiteCatalog.Sites[site].Point,out float h)&&h>-9000&&h<24000,"200 m measured terrain covers landmark "+site);
            for(int i=0;i<8;i++)demo.AdvancePresentation(15,demo.ActiveMission.routes[0]);
            Check(demo.DemoElapsed==120&&demo.JourneyProgress>0&&demo.JourneyProgress<1,"Long-route presentation previews a continuous walk");
            Check(MarsGeospatial.AngularDistanceDegrees(demo.JourneyLocation,demo.DemoPreviewEnd)<0.00001f,"Demo reaches the measured preview endpoint");
            demo.UseTimedPlayback(30);Check(demo.TimedControlsVisible,"Full mission exposes its time controls");
            demo.ReturnToGlobalMap();Check(rig.RoutesInsideViewport(demo.ActiveMission),"Returning to planner refits selected region");
            Debug.Log("MARSPATH_PRESENTATION_PASSED: continuous 120-second preview, sparse arrows, viewport route fit and return-to-planning frame.");
        }
        private static void ValidateRoutes(MissionData mission)
        {
            Check(mission.routes.Length==5,"Five computed cost profiles");int available=0;
            foreach(RouteData route in mission.routes)
            {
                if(!route.available)continue;available++;
                Check(route.distanceKm>0&&!float.IsNaN(route.distanceKm)&&route.etaMinutes>0,"Finite route metrics");
                Check(route.safetyScore>=0&&route.safetyScore<=100&&route.energyScore>=0&&route.energyScore<=100,"Bounded model scores");
                RoutePoint first=route.waypoints[0],last=route.waypoints[route.waypoints.Length-1];
                GeoPoint start=MarsRegionalTerrain.ToGeo(first.x*60,first.z*60),end=MarsRegionalTerrain.ToGeo(last.x*60,last.z*60);
                Check(MarsGeospatial.AngularDistanceDegrees(start,mission.currentLocation)<0.001f&&MarsGeospatial.AngularDistanceDegrees(end,mission.destination)<0.001f,"Routes honor selected endpoints");
                foreach(RoutePoint p in route.waypoints)Check(Mathf.Abs(p.x)<=1.001f&&Mathf.Abs(p.z)<=1.001f,"Regional waypoint bounds");
            }
            Check(available>0,"At least one feasible modeled corridor");
        }

        private static void Check(bool condition, string label)
        {
            if (!condition) throw new System.Exception(label);
        }
    }
}

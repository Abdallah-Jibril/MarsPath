using UnityEngine;

namespace MarsPath
{
    public partial class MarsPathDemoBootstrap
    {
        private MarsGeographicTrack fullTrack;
        private GameObject surfaceRoot;
        private MarsPathRouteView surfaceRoute;
        private GeoPoint surfaceCenter;
        private double surfaceAnchorMetres,surfaceMetres;
        private int builtStereoRevision,builtElevationRevision;
        private int builtSourceFingerprint;
        internal static int SurfaceSourceFingerprint(GeoPoint point)
        {
            int hash = FingerprintSample(17, point);
            // This is evaluated on source revisions and window construction.
            // A new crater-rim window can affect the visible horizon while the
            // centre and the old 2 km-only sample ring remain unchanged.
            foreach (float radiusKm in new[] { .25f, .5f, 1f, 2f, 4f, 8f, 16f, 32f, 55f })
            {
                for (int bearing = 0; bearing < 360; bearing += 45)
                {
                    GeoPoint sample = MarsGeospatial.Destination(point, bearing, radiusKm);
                    hash = FingerprintSample(hash, sample);
                }
            }
            return hash;
        }

        private static int FingerprintSample(int hash, GeoPoint point)
        {
            unchecked
            {
                hash = hash * 31 + Mathf.RoundToInt(MarsRegionalTerrain.MeasuredElevation(point) * 100);
                return hash * 31 + Mathf.RoundToInt(MarsRegionalTerrain.SourceSpacingMeters(point) * 10);
            }
        }
        private float windowStart,windowEnd;
        private bool surfaceActive,expeditionMode;
        private double totalElapsedSeconds;
        private float pendingSeek=-1;
        private int resupplyLeg;
        public float JourneyProgress=>routeProgress;
        public bool SurfaceActive=>surfaceActive;
        public int ResupplyLeg=>resupplyLeg;
        public GeoPoint JourneyLocation=>new GeoPoint{latitude=currentLatitude,longitude=currentLongitude};
        public int VisibleSurfaceArrows=>surfaceRoute!=null?surfaceRoute.VisibleArrowCount:0;

        private void StartFullSurface()
        {
            if(!mission.generatedRegion)return;
            fullTrack=new MarsGeographicTrack(mission,mission.routes[selectedRoute]);
            terrainRoot.SetActive(false);routesRoot.SetActive(false);surfaceActive=true;
            double anchor = demoPresentation ? MarsDemoDirector.PreviewMetres(fullTrack.LengthKm * 1000) / 2 : 0;
            BuildSurfaceWindowMeters(anchor);
            UpdateSurfacePosition(routeProgress);
        }
        private void BuildSurfaceWindow(float progress)
        {BuildSurfaceWindowMeters((double)progress*fullTrack.LengthKm*1000);}
        private void BuildSurfaceWindowMeters(double metres)
        {
            if(surfaceRoot!=null){surfaceRoot.SetActive(false);Destroy(surfaceRoot);}
            surfaceAnchorMetres=metres;surfaceMetres=metres;surfaceCenter=fullTrack.AtMeters(metres);
            bool measuredZone = MarsMissionZone.IsMission(mission);
            double sourcesBegan = Time.realtimeSinceStartupAsDouble;
            if (!measuredZone) MarsCtxImageryCache.ActivateAt(surfaceCenter);
            MarsOfficialTerrain.RestoreCachedGeometry(surfaceCenter);
            if(!MarsOfficialTerrain.ActivateCachedImagery(surfaceCenter))
            {
                if (measuredZone) MarsCtxImageryCache.ActivateAt(surfaceCenter);
                GeoPoint ia = MarsGeospatial.Destination(surfaceCenter, 270, 1.3f);
                GeoPoint ib = MarsGeospatial.Destination(surfaceCenter, 90, 1.3f);
                MarsRegionSourceCache.TryLoadCachedImagery(ia, ib);
            }
            if (measuredZone) Debug.Log($"MARSPATH_SURFACE: local sources={Time.realtimeSinceStartupAsDouble - sourcesBegan:F2}s");
            MissionData patch=new MissionData{generatedRegion=true,regionCenter=surfaceCenter,currentLocation=surfaceCenter,destination=fullTrack.AtMeters(metres+100),
                regionSpanKm=0.12f,usesJezeroDem=MarsRegionalTerrain.InJezeroCrop(surfaceCenter),terrainProfile=mission.terrainProfile,region=mission.region,telemetry=mission.telemetry,routingOptions=mission.routingOptions};
            MarsRegionalTerrain.Configure(patch);
            RouteData selected=mission.routes[selectedRoute];RoutePoint[] local=fullTrack.LocalWindowMeters(metres);
            patch.routes=new[]{new RouteData{available=true,type=selected.type,displayName=selected.displayName,colorHex=selected.colorHex,waypoints=local}};
            patch.sciencePoints=new SciencePointData[0];patch.hazards=new HazardData[0];
            surfaceRoot=new GameObject("Moving Surface / Full Selected Journey");surfaceRoot.transform.SetParent(transform,false);
            GameObject ground=new GameObject("Continuous Surface Terrain");ground.transform.SetParent(surfaceRoot.transform,false);ground.AddComponent<MarsTerrainGenerator>().Build(patch);
            builtStereoRevision=MarsOfficialTerrain.Revision+MarsHrscStereoTerrain.Revision;builtElevationRevision=MarsRegionSourceCache.ElevationRevision;
            builtSourceFingerprint=SurfaceSourceFingerprint(surfaceCenter);
            GameObject nav=new GameObject("Selected Full Route / Local Surface Guidance");nav.transform.SetParent(surfaceRoot.transform,false);surfaceRoute=nav.AddComponent<MarsPathRouteView>();surfaceRoute.Build(patch);surfaceRoute.SetMissionMode(true);
            windowStart=(float)(Mathf.Max(0,(float)metres-100)/(fullTrack.LengthKm*1000));windowEnd=(float)(System.Math.Min(fullTrack.LengthKm*1000,metres+400)/(fullTrack.LengthKm*1000));
        }
        private void UpdateSurfacePosition(float progress)
        {UpdateSurfaceDistance((double)progress*fullTrack.LengthKm*1000);}
        private void UpdateSurfaceDistance(double metres)
        {
            GeoPoint location=fullTrack.AtMeters(metres);
            Vector2 p=fullTrack.OffsetMeters(metres,surfaceAnchorMetres);
            bool rebased=p.magnitude>100;Vector3 previous=astronaut.transform.position;
            if(rebased){BuildSurfaceWindowMeters(metres);p=Vector2.zero;}
            surfaceMetres=metres;
            astronaut.transform.position=new Vector3(p.x,MarsTerrainGenerator.SampleHeight(p.x,p.y)+0.76f,p.y);
            MarsSurfaceRockScatter.SetFocus(astronaut.transform.position);
            if(rebased)cameraRig.RebaseMission(astronaut.transform.position-previous);
            Vector2 tangent=fullTrack.OffsetMeters(System.Math.Min(fullTrack.LengthKm*1000,metres+2),surfaceAnchorMetres)-fullTrack.OffsetMeters(System.Math.Max(0,metres-2),surfaceAnchorMetres);
            currentHeadingDegrees=Mathf.Repeat(Mathf.Atan2(tangent.x,tangent.y)*Mathf.Rad2Deg,360);
            Quaternion heading=Quaternion.Euler(0,currentHeadingDegrees,0);
            astronaut.transform.rotation=metres<.001||rebased?heading:Quaternion.Slerp(astronaut.transform.rotation,heading,1-Mathf.Exp(-Time.deltaTime*7));
            currentLatitude=location.latitude;currentLongitude=location.longitude;currentSlopeDegrees=MarsTerrainGenerator.SurfaceSlope(p.x,p.y);
            MarsHazardPhysics.MeasuredSlope(p.x,p.y,heading*Vector3.forward,out currentSignedDemGradeDegrees);
            currentDemSlopeDegrees=Mathf.Abs(currentSignedDemGradeDegrees);
            surfaceRoute.SetMissionPosition(astronaut.transform.position);
            MarsMissionEvidence.Save(mission,mission.routes[selectedRoute],demoPresentation,location,metres,fullTrack.LengthKm*1000,rebased);
        }
        private void StopFullSurface()
        {
            surfaceSourceRevision++;surfaceSourceLoading=false;
            if(!surfaceActive)return;
            surfaceActive=false;if(surfaceRoot!=null){surfaceRoot.SetActive(false);Destroy(surfaceRoot);}surfaceRoot=null;
            MarsRegionalTerrain.Configure(mission);terrainRoot.SetActive(true);routesRoot.SetActive(true);
        }
        private void AdvanceJourney(float seconds,RouteData route)
        {
            float duration=Mathf.Max(1,route.etaMinutes*60);
            float remaining=(float)System.Math.Min(seconds,duration-totalElapsedSeconds);
            while(remaining>0.00001f)
            {
                float untilResupply=expeditionMode?14400-evaSimulation.ElapsedSeconds:remaining;
                float step=Mathf.Min(5,Mathf.Min(remaining,Mathf.Max(0.001f,untilResupply)));
                double previous=evaSimulation.ElapsedSecondsExact;
                JourneyGradeAt(route,duration,totalElapsedSeconds+step*.5,out float grade,out float elevation);
                evaSimulation.AdvancePhysical(step,route.distanceKm*1000/duration,grade,elevation);
                double used=evaSimulation.ElapsedSecondsExact-previous;totalElapsedSeconds+=used;remaining-=step;
                if(evaSimulation.ReserveReached)break;
                if(expeditionMode&&evaSimulation.ElapsedSeconds>=14399.99f)
                {resupplyLeg++;evaSimulation.Reset(mission.telemetry);}
            }
            routeProgress=Mathf.Clamp01((float)(totalElapsedSeconds/duration));
        }
        private void UpdateTimedJourneyPosition(RouteData route)
        {
            if(surfaceActive)UpdateSurfaceDistance(totalElapsedSeconds/(route.etaMinutes*60.0)*fullTrack.LengthKm*1000);
            else UpdateAstronautPosition(routeProgress);
        }
        public void SeekFullJourney(float progress)
        {
            if(!missionStarted)return;
            evaAudio?.ResetMotion();
            bool wasPaused=paused;
            progress=Mathf.Clamp01(progress);RouteData route=mission.routes[selectedRoute];
            totalElapsedSeconds=route.etaMinutes*60.0*progress;
            resupplyLeg=expeditionMode?(int)System.Math.Floor(totalElapsedSeconds/14400):0;
            float legSeconds=(float)(expeditionMode?totalElapsedSeconds-resupplyLeg*14400:totalElapsedSeconds);
            ReconstructSeekResources(route,legSeconds);
            routeProgress=progress;reserveStop=ReserveBlocksTraversal;arrived=progress>=1;paused=wasPaused||reserveStop||arrived;
            UpdateAstronautPosition(progress);
            // Refresh live effort at the new station rather than retaining the
            // previous station's slope after a seek.
            evaSimulation.AdvancePhysical(0,route.distanceKm*1000/Mathf.Max(1,route.etaMinutes*60),currentSignedDemGradeDegrees,
                mission.generatedRegion?MarsRegionalTerrain.MeasuredElevation(JourneyLocation):0);
            UpdateLifeSupport(route);UpdateHazardProximity(route);
            astronaut.transform.rotation=Quaternion.Euler(0,currentHeadingDegrees,0);
            cameraRig.SnapToMission(astronaut.transform);
        }
        private void ReconstructSeekResources(RouteData route,float legSeconds)
        {
            evaSimulation.Reset(mission.telemetry);
            if(legSeconds<=0)return;
            // Bounded integration of the traversed portion of the current suit
            // leg. Rates are a model; elevations are sampled on the actual path.
            var track=fullTrack;
            MissionData previous=MarsRegionalTerrain.Current;
            if(track==null)
            {
                try{track=new MarsGeographicTrack(mission,route);}
                finally{if(previous!=null)MarsRegionalTerrain.Configure(previous);}
            }
            double duration=System.Math.Max(1,route.etaMinutes*60.0),began=System.Math.Max(0,totalElapsedSeconds-legSeconds);
            float speed=route.distanceKm*1000/(float)duration;
            int count=Mathf.Clamp(Mathf.CeilToInt(legSeconds/5),1,4096);
            for(int i=0;i<count&&!evaSimulation.ReserveReached;i++)
            {
                double metres=(began+legSeconds*(i+.5)/count)/duration*track.LengthKm*1000;
                GeoPoint focus=track.AtMeters(metres);
                double baseline=System.Math.Max(2,MarsRegionalTerrain.SourceSpacingMeters(focus));
                double a=System.Math.Max(0,metres-baseline),b=System.Math.Min(track.LengthKm*1000,metres+baseline);
                float rise=MarsRegionalTerrain.MeasuredElevation(track.AtMeters(b))-MarsRegionalTerrain.MeasuredElevation(track.AtMeters(a));
                float grade=(float)MarsEvaPhysics.SignedGradeDegrees(rise,b-a);
                evaSimulation.AdvancePhysical(legSeconds/count,speed,grade,MarsRegionalTerrain.MeasuredElevation(focus));
            }
        }
        private void JourneyGradeAt(RouteData route,double duration,double elapsed,out float grade,out float elevation)
        {
            grade=currentSignedDemGradeDegrees;elevation=0;
            if(fullTrack==null||!mission.generatedRegion)return;
            double metres=elapsed/duration*fullTrack.LengthKm*1000;
            GeoPoint focus=fullTrack.AtMeters(metres);
            double support=System.Math.Max(1,MarsRegionalTerrain.SourceSpacingMeters(focus));
            double a=System.Math.Max(0,metres-support),b=System.Math.Min(fullTrack.LengthKm*1000,metres+support);
            float rise=MarsRegionalTerrain.MeasuredElevation(fullTrack.AtMeters(b))-MarsRegionalTerrain.MeasuredElevation(fullTrack.AtMeters(a));
            grade=(float)MarsEvaPhysics.SignedGradeDegrees(rise,b-a);
            elevation=MarsRegionalTerrain.MeasuredElevation(focus);
        }
        public void PrepareFullJourneyCapture(float progress)
        {demoPresentation=false;PrepareJourneyCapture(false);StartMission();SeekFullJourney(progress);paused=true;}
        public void PrepareJezeroWideCapture()
        {
            GeoPoint start=new GeoPoint{label="Jezero landing region",latitude=18.455f,longitude=77.441f};
            GeoPoint end=new GeoPoint{label="Western Jezero delta",latitude=18.47f,longitude=77.31f};
            LoadRegionalMission(MarsRoutePlanner.Generate(start,end));GlobalView.SetEndpoints(start,end);GlobalView.OpenJezeroMission();cameraRig.SetPlanningView(25,42,250);
        }
        public void StartFullRouteForTest(){StartMission();}
    }
}

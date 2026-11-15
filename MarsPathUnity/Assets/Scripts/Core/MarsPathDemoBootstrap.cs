using UnityEngine;

namespace MarsPath
{
    public partial class MarsPathDemoBootstrap : MonoBehaviour
    {
        private MissionData mission;
        private MarsPathRouteView routeView;
        private MarsPathCameraRig cameraRig;
        public MarsGlobalView GlobalView { get; private set; }
        private Camera mainCamera;
        private GameObject terrainRoot, routesRoot;
        private float[] pathDistances;
        private float pathLength;
        public MissionData ActiveMission => mission;
        public bool IsSurfaceMissionStarted=>missionStarted;
        private GameObject astronaut;
        private MarsAstronautVisual astronautVisual;
        private MarsArticulatedAstronaut nasaAstronautMotion;
        private HazardData nearbyHazard;
        private float nearbyHazardDistance;
        private int selectedRoute;
        private bool missionStarted;
        private bool paused;
        private bool arrived;
        private float routeProgress;
        private float oxygen;
        private float power;
        private float missionElapsedMinutes;
        private float oxygenFlowGramsPerHour;
        private float metabolicWatts;
        private float electricalWatts;
        private float heartRateBpm;
        private float currentSlopeDegrees;
        private float currentDemSlopeDegrees;
        private float currentSignedDemGradeDegrees;
        private readonly MarsEvaSimulation evaSimulation=new MarsEvaSimulation();
        private float playbackSpeed=1f;
        private bool reserveStop;
        private bool rockAhead;
        private float rockAheadDistance;
        private int rockFieldCount;
        private float upcomingSurfaceSlope;
        private MissionData journeyOverview;
        public bool CanStartSelectedEva=>MarsEvaSimulation.CanWalk(mission.routes[selectedRoute]);
        public float CurrentOxygen=>oxygen;
        public float CurrentSurfaceSlope=>currentSlopeDegrees;
        private float currentLatitude;
        private float currentLongitude;
        private float currentHeadingDegrees;
        private GUIStyle titleStyle;
        private GUIStyle labelStyle;
        private GUIStyle smallStyle;
        private GUIStyle cardStyle;
        private GUIStyle selectedCardStyle;
        private GUIStyle buttonStyle;
        private GUIStyle warningStyle;
        private GUIStyle hazardTitleStyle;
        private GUIStyle hazardBodyStyle;
        private Texture2D darkTexture;
        private Texture2D selectedTexture;
        private Texture2D hazardTexture;
        private Texture2D arrivalTexture;
        private Vector2Int requestedFullscreenSize;
        private float nextFullscreenCheck;
        private MarsEvaSoundscape evaAudio;

        private void Awake()
        {
            UpdateFullscreenResolution();
            Application.targetFrameRate = 72;
            Application.runInBackground = true;
            mission = MarsPathDataLoader.LoadMockMission();
            if (mission == null) return;

            BuildEnvironment();
            BuildAstronaut();
            oxygen = mission.telemetry.oxygenPercent;
            power = mission.telemetry.suitPowerPercent;
            GlobalView = gameObject.AddComponent<MarsGlobalView>();
            if (Application.platform == RuntimePlatform.Android)
            {
                questMissionControl = true;
                RefinementEnabled = false;
            }
            GlobalView.Initialize(mainCamera, cameraRig, mission);
            GlobalView.RoutesRequested += (start,end) => StartCoroutine(BuildSelectedRegion(start,end));
            GlobalView.JezeroDemoRequested += () => StartCoroutine(PrepareJezeroShowcase());
        }

        private void BuildEnvironment()
        {
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.38f,0.35f,0.32f);
            RenderSettings.ambientEquatorColor = new Color(0.25f,0.22f,0.20f);
            RenderSettings.ambientGroundColor = new Color(0.16f,0.13f,0.11f);
            RenderSettings.fog = false;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogColor = new Color(0.69f, 0.51f, 0.39f);
            RenderSettings.fogDensity = 0.0017f;

            GameObject terrainObject = new GameObject("Mars Terrain - Prototype Only");
            terrainRoot = terrainObject;
            terrainObject.transform.SetParent(transform, false);
            terrainObject.AddComponent<MarsTerrainGenerator>().Build(mission);

            GameObject sun = new GameObject("Mars Sun");
            Light light = sun.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.12f;
            light.color = new Color(1f, 0.96f, 0.89f);
            light.shadows = LightShadows.Soft;
            light.shadowStrength = 0.78f;
            light.shadowBias = 0.035f;
            sun.transform.rotation = Quaternion.Euler(24f, -55f, 0f);
            RenderSettings.sun = light;
            QualitySettings.shadowDistance = 6000f;
            QualitySettings.shadowCascades = 4;
            QualitySettings.shadowCascade4Split = new Vector3(.04f,.15f,.4f);

            Shader skyShader = Resources.Load<Shader>("MarsPathSky");
            if (skyShader == null) skyShader = Shader.Find("MarsPath/Sky");
            if (skyShader != null) RenderSettings.skybox = new Material(skyShader) { name = "Mars Dust Atmosphere" };

            GameObject routes = new GameObject("MarsPath Routes and Markers");
            routesRoot = routes;
            routes.transform.SetParent(transform, false);
            routeView = routes.AddComponent<MarsPathRouteView>();
            routeView.Build(mission);

            GameObject cameraObject = new GameObject("MarsPath Camera");
            cameraObject.tag = "MainCamera";
            mainCamera = cameraObject.AddComponent<Camera>();
            mainCamera.clearFlags = RenderSettings.skybox != null ? CameraClearFlags.Skybox : CameraClearFlags.SolidColor;
            mainCamera.backgroundColor = new Color(0.39f, 0.22f, 0.13f);
            if (Application.platform != RuntimePlatform.Android) mainCamera.fieldOfView = 58f;
            cameraObject.AddComponent<AudioListener>();
            cameraRig = gameObject.AddComponent<MarsPathCameraRig>();
            cameraRig.Initialize(mainCamera);
            cameraRig.FrameRoutes(mission);
        }

        private void BuildAstronaut()
        {
            GameObject nasaAstronautPrefab = Resources.Load<GameObject>("NASA/NasaAstronaut");
            if (nasaAstronautPrefab != null)
            {
                astronaut = Instantiate(nasaAstronautPrefab);
                astronaut.name = "NASA EVA Astronaut (Prototype Motion)";
                nasaAstronautMotion = astronaut.GetComponent<MarsArticulatedAstronaut>();
            }
            else
            {
                astronaut = new GameObject("MarsPath EVA Astronaut");
                astronautVisual = astronaut.AddComponent<MarsAstronautVisual>();
                astronautVisual.Build();
            }
            UpdateAstronautPosition(0f);
            RebuildPathDistances();
        }

        private void Update()
        {
            UpdateFullscreenResolution();
            if (GlobalView != null && GlobalView.IsVisible) return;
            if (desktopBrowsingRoutes)
            {
                routeView.SetPlanningVisualScale(cameraRig.PlanningDistance / 80f);
                return;
            }
            if(surfaceActive&&(builtStereoRevision!=MarsOfficialTerrain.Revision+MarsHrscStereoTerrain.Revision||builtElevationRevision!=MarsRegionSourceCache.ElevationRevision))
            {
                if(builtSourceFingerprint!=SurfaceSourceFingerprint(surfaceCenter))
                {Vector3 previous=astronaut.transform.position;double at=surfaceMetres;BuildSurfaceWindowMeters(at);UpdateSurfaceDistance(at);cameraRig.RebaseMission(astronaut.transform.position-previous);planningTerrainDirty=true;}
                else{builtStereoRevision=MarsOfficialTerrain.Revision+MarsHrscStereoTerrain.Revision;builtElevationRevision=MarsRegionSourceCache.ElevationRevision;}
            }
            if(!missionStarted&&routeView!=null&&cameraRig!=null)routeView.SetPlanningVisualScale(cameraRig.PlanningDistance/80f);
            if (desktopMissionMirror && missionStarted)
            {
                UpdateDesktopMirror();
                return;
            }
            if(pendingSeek>=0){float seek=pendingSeek;pendingSeek=-1;SeekFullJourney(seek);}
            if (missionStarted && Application.platform == RuntimePlatform.Android)
            {
                UpdateQuestFacing();
                SetQuestWalkInput(MarsQuestControls.RightTriggerPressed(), cameraRig.QuestWalkTrackingReady);
            }
            if (!missionStarted || arrived || reserveStop || routeView.SelectedWorldPoints == null)
            {
                SetAstronautWalking(false);
                return;
            }

            RouteData route = mission.routes[selectedRoute];
            bool wasPaused = paused;
            if (demoPresentation && !helmetPreview) TickTwoMinuteDemo(route, Time.unscaledTimeAsDouble);
            if (wasPaused)
            {
                SetAstronautWalking(false);
                return;
            }
            if (!demoPresentation)
            {
                AdvanceJourney(Mathf.Min(Time.deltaTime, .25f) * playbackSpeed, route);
                UpdateTimedJourneyPosition(route);
            }
            UpdateLifeSupport(route);
            UpdateHazardProximity(route);
            if (ReserveBlocksTraversal) { paused = true; reserveStop = true; }
            if (arrived || !demoPresentation && routeProgress >= 1f)
            {
                arrived = true;
                nearbyHazard = null;
                SetAstronautWalking(false);
            }
            else SetAstronautWalking(!paused);
        }

        private void UpdateFullscreenResolution()
        {
            if (Application.platform != RuntimePlatform.WindowsPlayer || Application.isBatchMode ||
                Time.unscaledTime < nextFullscreenCheck) return;
            nextFullscreenCheck = Time.unscaledTime + .25f;
            if (Screen.fullScreenMode != FullScreenMode.FullScreenWindow)
            {
                requestedFullscreenSize = default;
                return;
            }

            DisplayInfo display = Screen.mainWindowDisplayInfo;
            if (display.width <= 0 || display.height <= 0) return;
            int renderWidth = Display.main.renderingWidth, renderHeight = Display.main.renderingHeight;
            if (Screen.width == display.width && Screen.height == display.height &&
                (renderWidth <= 0 || renderWidth == display.width) && (renderHeight <= 0 || renderHeight == display.height))
            {
                requestedFullscreenSize = default;
                return;
            }

            // A borderless window fills the monitor even when the saved render
            // size has a different aspect. Unity applies this at frame end.
            var nativeSize = new Vector2Int(display.width, display.height);
            if (nativeSize == requestedFullscreenSize) return;
            requestedFullscreenSize = nativeSize;
            Debug.Log($"MARSPATH_FULLSCREEN: {Screen.width}x{Screen.height} window, {renderWidth}x{renderHeight} render -> {nativeSize.x}x{nativeSize.y}");
            Screen.SetResolution(nativeSize.x,nativeSize.y,FullScreenMode.FullScreenWindow);
        }

        private void LateUpdate()
        {
            bool missionOpen = missionStarted && !helmetEntryLoading && GlobalView != null && !GlobalView.IsVisible;
            bool outside = missionOpen && !desktopBrowsingRoutes && Application.isFocused && !Application.isBatchMode;
            UpdateMissionRadio(missionOpen && !helmetPreview && !Application.isBatchMode);
            if (outside && evaAudio == null) evaAudio = mainCamera.gameObject.AddComponent<MarsEvaSoundscape>();
            if (evaAudio == null) return;

            bool walking = outside && !paused && !arrived && !helmetPreview && (demoPresentation || playbackSpeed <= 1);
            EvaHazard hazard = reserveStop ? EvaHazard.Reserve : rockAhead ? EvaHazard.Rock
                : nearbyHazard != null || upcomingSurfaceSlope > 10 ? EvaHazard.Terrain : EvaHazard.None;
            evaAudio.Mix(outside, walking, arrived, surfaceActive ? surfaceMetres : routeProgress * pathLength,
                hazard, MissionRadioReceiving || QuestRadioTalking);
        }

        private void SetAstronautWalking(bool value)
        {
            if (astronautVisual != null) astronautVisual.SetWalking(value);
            if (nasaAstronautMotion != null) nasaAstronautMotion.SetWalking(value);
        }

        private void UpdateAstronautPosition(float progress)
        {
            if(surfaceActive){UpdateSurfacePosition(progress);return;}
            Vector3[] points = routeView.SelectedWorldPoints;
            if (points == null || points.Length < 2) return;
            if (pathDistances == null || pathDistances.Length != points.Length) RebuildPathDistances();
            float traveled = progress * pathLength;
            int segment = System.Array.BinarySearch(pathDistances, traveled);
            if(segment<0)segment=~segment-1;
            segment=Mathf.Clamp(segment,0,points.Length-2);
            float segmentT=Mathf.InverseLerp(pathDistances[segment],pathDistances[segment+1],traveled);
            Vector3 position = Vector3.Lerp(points[segment], points[segment + 1], segmentT);
            Vector3 direction = (points[segment + 1] - points[segment]).normalized;
            float horizontal = new Vector2(direction.x, direction.z).magnitude;
            currentSlopeDegrees = Mathf.Abs(Mathf.Atan2(direction.y, Mathf.Max(0.001f, horizontal)) * Mathf.Rad2Deg);
            currentSignedDemGradeDegrees=Mathf.Atan2(direction.y,Mathf.Max(.001f,horizontal))*Mathf.Rad2Deg;
            currentDemSlopeDegrees=Mathf.Abs(currentSignedDemGradeDegrees);
            if(mission.generatedRegion)
            {
                MarsHazardPhysics.MeasuredSlope(position.x,position.z,direction,out currentSignedDemGradeDegrees);
                currentDemSlopeDegrees=Mathf.Abs(currentSignedDemGradeDegrees);
            }
            float sample=0.5f;
            float hx=MarsTerrainGenerator.SampleHeight(position.x+sample,position.z)-MarsTerrainGenerator.SampleHeight(position.x-sample,position.z);
            float hz=MarsTerrainGenerator.SampleHeight(position.x,position.z+sample)-MarsTerrainGenerator.SampleHeight(position.x,position.z-sample);
            currentSlopeDegrees=Mathf.Atan2(Mathf.Sqrt(hx*hx+hz*hz),2*sample)*Mathf.Rad2Deg;
            currentHeadingDegrees = Mathf.Repeat(Mathf.Atan2(direction.x, direction.z) * Mathf.Rad2Deg, 360f);
            Vector3 groundPosition = position;
            position += Vector3.up * 0.76f;
            astronaut.transform.position = position;
            Vector3 heading=new Vector3(direction.x,0f,direction.z);
            if(heading.sqrMagnitude>0.01f)
            {
                Quaternion rotation=Quaternion.LookRotation(heading);
                astronaut.transform.rotation=progress<=0.0001f?rotation:Quaternion.Slerp(astronaut.transform.rotation,rotation,1f-Mathf.Exp(-Time.deltaTime*6f));
            }
            UpdateGeographicPosition(groundPosition);
            routeView.SetMissionProgress(progress);
        }

        private void UpdateGeographicPosition(Vector3 worldPosition)
        {
            if(mission.generatedRegion)
            {
                GeoPoint point=MarsRegionalTerrain.ToGeo(worldPosition.x,worldPosition.z);
                currentLatitude=point.latitude;currentLongitude=point.longitude;return;
            }
            RoutePoint[] routePoints = mission.routes[selectedRoute].waypoints;
            if (routePoints == null || routePoints.Length < 2)
            {
                currentLatitude = mission.currentLocation.latitude;
                currentLongitude = mission.currentLocation.longitude;
                return;
            }

            RoutePoint first = routePoints[0];
            RoutePoint last = routePoints[routePoints.Length - 1];
            float normalizedX = worldPosition.x / (MarsTerrainGenerator.TerrainSize * 0.5f);
            float normalizedZ = worldPosition.z / (MarsTerrainGenerator.TerrainSize * 0.5f);
            float longitudeProgress = Mathf.InverseLerp(first.x, last.x, normalizedX);
            float latitudeProgress = Mathf.InverseLerp(first.z, last.z, normalizedZ);
            currentLongitude = Mathf.Lerp(mission.currentLocation.longitude, mission.destination.longitude, longitudeProgress);
            currentLatitude = Mathf.Lerp(mission.currentLocation.latitude, mission.destination.latitude, latitudeProgress);
        }

        private void UpdateLifeSupport(RouteData route)
        {
            missionElapsedMinutes=(float)(totalElapsedSeconds/60);
            oxygen=evaSimulation.OxygenPercent; power=evaSimulation.PowerPercent;
            metabolicWatts=evaSimulation.MetabolicWatts; oxygenFlowGramsPerHour=evaSimulation.OxygenGramsPerHour;
            electricalWatts=evaSimulation.ElectricalWatts;
            heartRateBpm=evaSimulation.HeartRate;
        }

        private void UpdateHazardProximity(RouteData route)
        {
            nearbyHazard = null;
            nearbyHazardDistance = float.MaxValue;
            rockAhead=MarsTerrainGenerator.TryRockAhead(astronaut.transform.position,astronaut.transform.forward,out rockAheadDistance,out rockFieldCount);
            bool measured=mission.generatedRegion;
            upcomingSurfaceSlope=measured ? MarsHazardPhysics.MeasuredSlope(astronaut.transform.position.x,
                astronaut.transform.position.z,astronaut.transform.forward,out _) : currentSlopeDegrees;
            float units=measured?MarsRegionalTerrain.MetersPerUnit:1;
            for(int i=1;i<=8;i++)
            {
                Vector3 sample=astronaut.transform.position+astronaut.transform.forward*(i/units);
                float slope=measured?MarsHazardPhysics.MeasuredSlope(sample.x,sample.z,astronaut.transform.forward,out _)
                    : MarsTerrainGenerator.SurfaceSlope(sample.x,sample.z);
                upcomingSurfaceSlope=Mathf.Max(upcomingSurfaceSlope,slope);
            }
            if (surfaceActive||mission.hazards == null) return;
            foreach (HazardData hazard in mission.hazards)
            {
                Vector3 hazardPosition = new Vector3(
                    hazard.x * MarsTerrainGenerator.TerrainSize * 0.5f,
                    0f,
                    hazard.z * MarsTerrainGenerator.TerrainSize * 0.5f);
                Vector3 astronautPosition = astronaut.transform.position;
                astronautPosition.y = 0f;
                float distance = Vector3.Distance(astronautPosition, hazardPosition);
                if (distance < 14f && distance < nearbyHazardDistance)
                {
                    nearbyHazard = hazard;
                    nearbyHazardDistance = distance;
                }
            }
        }

        private void SelectRoute(int index)
        {
            selectedRoute = index;
            routeProgress = 0f;
            arrived = false;
            nearbyHazard = null;
            if (planningSceneDeferred) return;
            routeView.Select(index);
            RebuildPathDistances();
            routeView.SetMissionMode(false);
            UpdateAstronautPosition(0f);
        }

        private void StartMission()
        {
            if(!mission.routes[selectedRoute].available)return;
            helmetPreview=false;
            missionStarted = true;
            paused = Application.platform == RuntimePlatform.Android || desktopMissionMirror;
            questWaitForTriggerRelease = paused;
            questRouteFacing.Reset();
            arrived = false;
            routeProgress = 0f;
            reserveStop=false;evaSimulation.Reset(mission.telemetry);playbackSpeed=1;
            ResetDemo();ResetVisorPanels();
            totalElapsedSeconds=0;resupplyLeg=0;expeditionMode=demoPresentation||!CanStartSelectedEva;
            oxygen = mission.telemetry.oxygenPercent;
            power = mission.telemetry.suitPowerPercent;
            missionElapsedMinutes = 0f;
            heartRateBpm = mission.telemetry.heartRateBpm;
            if (!planningSceneDeferred)
            {
                routeView.SetMissionMode(true);
                routeView.SetMissionProgress(0f);
            }
            cameraRig.SetHelmetView(true);
            cameraRig.SetMissionMode(true, astronaut.transform);
            StartFullSurface();
            UpdateAstronautPosition(0);UpdateLifeSupport(mission.routes[selectedRoute]);UpdateHazardProximity(mission.routes[selectedRoute]);
            cameraRig.SnapToMission(astronaut.transform);
            ResetVisorPanels();
            BeginSurfaceSourceRefinement();
        }

        public void PrepareMissionCapture()
        {
            demoPresentation=false;
            GlobalView.OpenJezeroMission();
            SelectRoute(0);
            StartMission();
            SeekFullJourney(0.58f);
        }

        public void PrepareArrivalCapture()
        {
            demoPresentation=false;
            GlobalView.OpenJezeroMission();
            SelectRoute(0);
            StartMission();
            SeekFullJourney(1f);
            arrived = true;
            nearbyHazard = null;
            SetAstronautWalking(false);
        }

        public void PreparePlanningCapture()
        {
            GlobalView.OpenJezeroMission();
            ReturnToPlanning();
        }

        public void ReturnToGlobalMap()
        {
            CancelTerrainRefinement();MarsRegionSourceCache.Cancel();
            ReturnToPlanning(returningToGlobe: true);
            SetAstronautWalking(false);
            GlobalView.Show();
        }

        private void ReturnToPlanning(bool returningToGlobe = false)
        {
            EndDesktopMirror();
            questRouteFacing.Reset();
            ResetVisorPanels();
            if(helmetPreview){demoPresentation=previewDemoChoice;helmetPreview=false;}
            elevationProfile=null;
            StopFullSurface();
            missionStarted = false;
            paused = false;
            arrived = false;
            nearbyHazard = null;
            if (planningSceneDeferred && returningToGlobe)
            {
                terrainRoot.SetActive(false);
                routesRoot.SetActive(false);
                return;
            }
            if(planningTerrainDirty || planningSceneDeferred)
            {
                int chosen=selectedRoute;planningTerrainDirty=false;
                MarsRegionSourceCache.TryLoadCachedImagery(mission.currentLocation,mission.destination);
                LoadRegionalMission(mission);SelectRoute(chosen);return;
            }
            routeView.SetMissionMode(false);
            cameraRig.SetMissionMode(false, astronaut.transform);
            cameraRig.FrameRoutes(mission);
            UpdateAstronautPosition(0f);
            if(mission.generatedRegion&&MarsRegionSourceCache.TryLoadCachedImagery(mission.currentLocation,mission.destination))
                terrainRoot.GetComponent<MarsTerrainGenerator>()?.RefreshSourceImagery();
        }

        private void DrawLegacyInterface()
        {
            if (mission == null) return;
            if (GlobalView != null && GlobalView.IsVisible) return;
            EnsureStyles();
            if (missionStarted) DrawMissionHud();
            else DrawPlanningInterface();
        }

        private void DrawPlanningInterface()
        {
            GUI.Box(new Rect(22, 20, 385, Screen.height - 40), GUIContent.none, cardStyle);
            GUI.Label(new Rect(44, 38, 320, 34), "MARSPATH", titleStyle);
            GUI.Label(new Rect(44, 75, 330, 22), "EVA ROUTE DECISION SUPPORT", smallStyle);
            GUI.Label(new Rect(44, 108, 330, 25), mission.region, labelStyle);
            GUI.Label(new Rect(44, 136, 330, 40), "Select a mission route generated by the MarsPath core.", smallStyle);

            float y = 186f;
            for (int i = 0; i < mission.routes.Length; i++)
            {
                RouteData route = mission.routes[i];
                bool selected = i == selectedRoute;
                Rect card = new Rect(40, y, 348, 72);
                if (GUI.Button(card, GUIContent.none, selected ? selectedCardStyle : cardStyle)) SelectRoute(i);
                Color routeColor = MarsPathRouteView.ParseColor(route.colorHex, Color.white);
                Color previous = GUI.color;
                GUI.color = routeColor;
                GUI.DrawTexture(new Rect(50, y + 11, 5, 50), Texture2D.whiteTexture);
                GUI.color = previous;
                GUI.Label(new Rect(66, y + 8, 208, 24), route.displayName + (route.recommended ? "  ★" : ""), labelStyle);
                GUI.Label(new Rect(66, y + 35, 265, 22), $"{route.distanceKm:0.0} km   •   {route.etaMinutes:0} min   •   Safety {route.safetyScore:0}", smallStyle);
                y += 80f;
            }

            RouteData current = mission.routes[selectedRoute];
            float footerY = Screen.height - 137f;
            GUI.Label(new Rect(44, footerY, 330, 42), current.summary, smallStyle);
            if (GUI.Button(new Rect(44, footerY + 54, 330, 48), "START MISSION", buttonStyle)) StartMission();

            GUI.Box(new Rect(Screen.width - 370, 20, 348, 139), GUIContent.none, cardStyle);
            GUI.Label(new Rect(Screen.width - 348, 38, 310, 28), "PROTOTYPE DATA NOTICE", warningStyle);
            GUI.Label(new Rect(Screen.width - 348, 69, 300, 76), mission.dataNotice, smallStyle);
            if (GUI.Button(new Rect(Screen.width - 370, 170, 348, 38), "<  GLOBAL MARS MAP", buttonStyle)) ReturnToGlobalMap();
            GUI.Label(new Rect(Screen.width - 370, Screen.height - 38, 340, 22), "RMB: orbit camera  •  Mouse wheel: zoom", smallStyle);
        }

        private void DrawMissionHud()
        {
            RouteData route = mission.routes[selectedRoute];
            float remainingKm = route.distanceKm * (1f - routeProgress);
            float remainingMinutes = route.etaMinutes * (1f - routeProgress);

            GUI.Box(new Rect(18, 18, 320, 150), GUIContent.none, cardStyle);
            GUI.Label(new Rect(38, 34, 270, 28), route.type.ToUpperInvariant() + " ROUTE", titleStyle);
            GUI.Label(new Rect(38, 72, 260, 23), $"DISTANCE  {remainingKm:0.00} km", labelStyle);
            GUI.Label(new Rect(38, 100, 260, 23), $"ETA       {remainingMinutes:0} min", labelStyle);
            GUI.Label(new Rect(38, 128, 260, 23), $"SLOPE     {currentSlopeDegrees:0.0}° current", labelStyle);

            GUI.Box(new Rect(Screen.width - 312, 18, 294, 222), GUIContent.none, cardStyle);
            GUI.Label(new Rect(Screen.width - 290, 35, 250, 24), "EVA LIFE SUPPORT MODEL", warningStyle);
            DrawBar(new Rect(Screen.width - 290, 76, 244, 16), oxygen / 100f, new Color(0.2f, 0.78f, 1f));
            GUI.Label(new Rect(Screen.width - 290, 56, 244, 20), $"OXYGEN  {oxygen:0.0}%   ({oxygenFlowGramsPerHour:0} g/h)", smallStyle);
            DrawBar(new Rect(Screen.width - 290, 125, 244, 16), power / 100f, new Color(0.96f, 0.72f, 0.16f));
            GUI.Label(new Rect(Screen.width - 290, 105, 244, 20), $"SUIT POWER  {power:0.0}%", smallStyle);
            GUI.Label(new Rect(Screen.width - 290, 153, 244, 20), $"METABOLIC LOAD  {metabolicWatts:0} W", smallStyle);
            GUI.Label(new Rect(Screen.width - 290, 177, 244, 20), $"HEART RATE      {heartRateBpm:0} bpm", smallStyle);
            GUI.Label(new Rect(Screen.width - 290, 201, 244, 20), $"MISSION CLOCK   {missionElapsedMinutes:0.0} min", smallStyle);

            GUI.Box(new Rect(18, Screen.height - 118, 420, 100), GUIContent.none, cardStyle);
            string alert = arrived
                ? $"MISSION COMPLETE: Arrived at {mission.destination.label}."
                : route.hazardIds != null && route.hazardIds.Length > 0
                ? "CAUTION: Route contains mapped terrain hazards. Follow the highlighted corridor."
                : "Route corridor clear. Continue toward the next waypoint.";
            GUI.Label(new Rect(38, Screen.height - 98, 380, 42), alert, warningStyle);
            if (GUI.Button(new Rect(38, Screen.height - 54, 126, 28), paused ? "RESUME" : "PAUSE", buttonStyle)) paused = !paused;
            if (GUI.Button(new Rect(174, Screen.height - 54, 244, 28), "RETURN TO ROUTE PLANNING", buttonStyle)) ReturnToPlanning();

            GUI.Label(new Rect(Screen.width * 0.5f - 120, 24, 240, 24), $"WAYPOINT PROGRESS  {routeProgress * 100f:0}%", labelStyle);
            DrawNavigationFix();
            if (nearbyHazard != null) DrawHazardWarning();
            if (arrived) DrawArrivalMessage();
            DrawCrosshair();
        }

        private void DrawNavigationFix()
        {
            Rect panel = new Rect(Screen.width - 312, Screen.height - 136, 294, 118);
            GUI.Box(panel, GUIContent.none, cardStyle);
            GUI.Label(new Rect(panel.x + 20, panel.y + 12, panel.width - 40, 20), "MARS LOCALIZATION / LIVE", warningStyle);
            GUI.Label(new Rect(panel.x + 20, panel.y + 38, panel.width - 40, 20), FormatCoordinate("LAT", currentLatitude, 'N', 'S'), labelStyle);
            GUI.Label(new Rect(panel.x + 20, panel.y + 63, panel.width - 40, 20), FormatCoordinate("LON", currentLongitude, 'E', 'W'), labelStyle);
            GUI.Label(new Rect(panel.x + 20, panel.y + 88, panel.width - 40, 20), $"HDG   {currentHeadingDegrees:000}°  {HeadingCardinal(currentHeadingDegrees)}", labelStyle);
        }

        private static string FormatCoordinate(string label, float value, char positive, char negative)
        {
            char hemisphere = value >= 0f ? positive : negative;
            return $"{label}   {Mathf.Abs(value):0.000000}° {hemisphere}";
        }

        private static string HeadingCardinal(float heading) => MarsCompass.Cardinal(heading);

        private void DrawArrivalMessage()
        {
            float width = Mathf.Min(620f, Screen.width - 40f);
            Rect panel = new Rect((Screen.width - width) * 0.5f, Screen.height * 0.5f - 88f, width, 176f);
            GUI.Box(panel, GUIContent.none, new GUIStyle(cardStyle) { normal = { background = arrivalTexture } });
            GUI.Label(new Rect(panel.x + 25f, panel.y + 24f, panel.width - 50f, 38f), "DESTINATION REACHED", new GUIStyle(titleStyle) { alignment = TextAnchor.MiddleCenter, fontSize = 28 });
            GUI.Label(new Rect(panel.x + 25f, panel.y + 69f, panel.width - 50f, 26f), mission.destination.label, new GUIStyle(labelStyle) { alignment = TextAnchor.MiddleCenter });
            GUI.Label(new Rect(panel.x + 25f, panel.y + 101f, panel.width - 50f, 22f), $"EVA time {missionElapsedMinutes:0.0} min  •  Oxygen {oxygen:0.0}%  •  Suit power {power:0.0}%", new GUIStyle(smallStyle) { alignment = TextAnchor.MiddleCenter });
            if (GUI.Button(new Rect(panel.x + panel.width * 0.5f - 120f, panel.y + 137f, 240f, 28f), "RETURN TO ROUTE PLANNING", buttonStyle)) ReturnToPlanning();
        }

        private void DrawHazardWarning()
        {
            float width = Mathf.Min(600f, Screen.width - 40f);
            Rect panel = new Rect((Screen.width - width) * 0.5f, 66f, width, 112f);
            GUI.Box(panel, GUIContent.none, new GUIStyle(cardStyle) { normal = { background = hazardTexture } });
            string prefix = nearbyHazard.severity == "HIGH" ? "!! HIGH-RISK TERRAIN" : "!! TERRAIN CAUTION";
            GUI.Label(new Rect(panel.x + 22f, panel.y + 14f, panel.width - 44f, 25f), prefix, hazardTitleStyle);
            GUI.Label(new Rect(panel.x + 22f, panel.y + 43f, panel.width - 44f, 24f), nearbyHazard.warning.ToUpperInvariant(), hazardBodyStyle);
            GUI.Label(new Rect(panel.x + 22f, panel.y + 70f, panel.width - 44f, 30f), nearbyHazard.action, smallStyle);
        }

        private void DrawCrosshair()
        {
            Color old = GUI.color;
            GUI.color = new Color(0.3f, 0.95f, 1f, 0.75f);
            GUI.DrawTexture(new Rect(Screen.width * 0.5f - 22, Screen.height * 0.5f, 44, 2), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(Screen.width * 0.5f, Screen.height * 0.5f - 22, 2, 44), Texture2D.whiteTexture);
            GUI.color = old;
        }

        private static void DrawBar(Rect rect, float value, Color color)
        {
            GUI.color = new Color(1f, 1f, 1f, 0.16f);
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
            GUI.color = color;
            GUI.DrawTexture(new Rect(rect.x, rect.y, rect.width * Mathf.Clamp01(value), rect.height), Texture2D.whiteTexture);
            GUI.color = Color.white;
        }

        private void EnsureStyles()
        {
            if (titleStyle != null) return;
            darkTexture = MakeTexture(new Color(0.025f, 0.045f, 0.065f, 0.91f));
            selectedTexture = MakeTexture(new Color(0.03f, 0.18f, 0.22f, 0.96f));
            hazardTexture = MakeTexture(new Color(0.35f, 0.045f, 0.025f, 0.96f));
            arrivalTexture = MakeTexture(new Color(0.025f, 0.24f, 0.18f, 0.97f));
            titleStyle = new GUIStyle(GUI.skin.label) { fontSize = 24, fontStyle = FontStyle.Bold, normal = { textColor = new Color(0.34f, 0.92f, 1f) } };
            labelStyle = new GUIStyle(GUI.skin.label) { fontSize = 15, fontStyle = FontStyle.Bold, wordWrap=true, normal = { textColor = Color.white } };
            smallStyle = new GUIStyle(GUI.skin.label) { fontSize = 12, wordWrap = true, normal = { textColor = new Color(0.75f, 0.83f, 0.86f) } };
            warningStyle = new GUIStyle(GUI.skin.label) { fontSize = 12, fontStyle = FontStyle.Bold, wordWrap = true, normal = { textColor = new Color(1f, 0.68f, 0.24f) } };
            hazardTitleStyle = new GUIStyle(GUI.skin.label) { fontSize = 17, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter, normal = { textColor = new Color(1f, 0.42f, 0.18f) } };
            hazardBodyStyle = new GUIStyle(GUI.skin.label) { fontSize = 14, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter, normal = { textColor = Color.white } };
            cardStyle = new GUIStyle(GUI.skin.box) { normal = { background = darkTexture }, border = new RectOffset(6, 6, 6, 6) };
            selectedCardStyle = new GUIStyle(cardStyle) { normal = { background = selectedTexture } };
            buttonStyle = new GUIStyle(GUI.skin.button) { fontSize = 12, fontStyle = FontStyle.Bold, normal = { textColor = Color.white }, hover = { textColor = new Color(0.35f, 0.95f, 1f) } };
        }

        private static Texture2D MakeTexture(Color color)
        {
            Texture2D texture = new Texture2D(1, 1);
            texture.SetPixel(0, 0, color);
            texture.Apply();
            return texture;
        }
    }
}

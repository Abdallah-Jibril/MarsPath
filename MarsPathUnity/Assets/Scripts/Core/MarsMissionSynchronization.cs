using System;
using System.Text;
using UnityEngine;

namespace MarsPath
{
    [Serializable]
    internal sealed class MarsMissionLiveState
    {
        public string session;
        public long sequence;
        public double clock, missionSeconds, travelledMetres;
        public float progress, demoSeconds, oxygen, power, metabolicWatts, electricalWatts, oxygenFlow;
        public float latitude, longitude, heading, surfaceSlope, trackGrade;
        public int resupplyLeg, warningSymbol;
        public bool inMission, moving, arrived, reserveStop, demo, demoComplete;
        public bool hasHeadPose;
        public bool hasLookHeading;
        public float lookHeading;
        public bool facingBlocked;
        public float facingError;
        public Vector3 headOffset;
        public Quaternion headRotation;
        public string warning, warningDetail;

        internal byte[] Encode() => Encoding.UTF8.GetBytes(JsonUtility.ToJson(this));

        internal static MarsMissionLiveState Decode(byte[] payload)
        {
            var state = JsonUtility.FromJson<MarsMissionLiveState>(Encoding.UTF8.GetString(payload));
            if (state == null || !Guid.TryParse(state.session, out _) || state.sequence < 0 ||
                !Finite(state.clock) || !Finite(state.missionSeconds) || !Finite(state.travelledMetres) ||
                state.clock < 0 || state.missionSeconds < 0 || state.travelledMetres < 0 ||
                !Fraction(state.progress, 1) || !Fraction(state.oxygen, 100) || !Fraction(state.power, 100) ||
                !Fraction(state.demoSeconds, MarsDemoDirector.DurationSeconds) ||
                !Finite(state.latitude) || Math.Abs(state.latitude) > 90 ||
                !Finite(state.longitude) || state.longitude < -180 || state.longitude > 360 ||
                !Finite(state.heading) || !Finite(state.surfaceSlope) || !Finite(state.trackGrade) ||
                state.hasLookHeading && (!Finite(state.lookHeading) || state.lookHeading < 0 || state.lookHeading >= 360) ||
                !Finite(state.facingError) || Mathf.Abs(state.facingError) > 180 ||
                !Fraction(state.metabolicWatts, 100000) || !Fraction(state.electricalWatts, 100000) ||
                !Fraction(state.oxygenFlow, 100000) || state.resupplyLeg < 0 ||
                (state.warning?.Length ?? 0) > 96 || (state.warningDetail?.Length ?? 0) > 160)
                throw new ArgumentException("Invalid live mission state.");
            if (state.hasHeadPose && (!Finite(state.headOffset.x) || !Finite(state.headOffset.y) ||
                !Finite(state.headOffset.z) || state.headOffset.sqrMagnitude > 25 ||
                !Finite(state.headRotation.x) || !Finite(state.headRotation.y) ||
                !Finite(state.headRotation.z) || !Finite(state.headRotation.w) ||
                Math.Abs(Quaternion.Dot(state.headRotation, state.headRotation) - 1) > .05f))
                throw new ArgumentException("Invalid live head pose.");
            return state;
        }

        private static bool Fraction(double value, double maximum) => Finite(value) && value >= 0 && value <= maximum;
        private static bool Finite(double value) => !double.IsNaN(value) && !double.IsInfinity(value);
    }

    public partial class MarsPathDemoBootstrap
    {
        private bool desktopMissionMirror;
        private bool desktopBrowsingRoutes;
        private int comparedRoute;
        private MissionData desktopSurfaceFrame;
        private float? mirroredLookHeading;
        private Guid mirroredSession, questLiveSession;
        private MarsMissionLiveState previousLiveState, currentLiveState;
        private float nextLivePublication, nextLiveLog, mirrorDemoSeconds;
        private long liveSequence;
        private double liveReceivedAt = -10;

        internal bool DesktopMissionMirror => desktopMissionMirror;
        private int PlanningRouteIndex => desktopBrowsingRoutes ? comparedRoute : selectedRoute;
        private bool LiveMissionFresh => missionRadio?.Connected == true &&
            currentLiveState != null && Time.unscaledTimeAsDouble - liveReceivedAt < 1;

        private void BeginDesktopMirror(Guid session)
        {
            desktopMissionMirror = true;
            mirroredSession = session;
            currentLiveState = previousLiveState = null;
            mirroredLookHeading = null;
            mirrorDemoSeconds = 0;
            liveReceivedAt = -10;
            CancelTerrainRefinement();
            StartCoroutine(PrepareHelmetEntry(false));
        }

        private void EndDesktopMirror()
        {
            if (!desktopMissionMirror) return;
            ExitDesktopRouteBrowser();
            missionRadio?.SetMirrorReady(mirroredSession, false);
            desktopMissionMirror = false;
            currentLiveState = previousLiveState = null;
            mirroredLookHeading = null;
        }

        private void BrowseLiveRoutes()
        {
            if (!desktopMissionMirror || !missionStarted || helmetEntryLoading || desktopBrowsingRoutes) return;

            desktopBrowsingRoutes = true;
            comparedRoute = selectedRoute;
            inspectProgress = 0;
            inspectedMission = null;
            ResetVisorPanels();
            if (surfaceActive)
            {
                // Keep the live surface resident. Its metre-based coordinates
                // must not be evaluated in the regional planner's frame.
                surfaceSourceRevision++;
                surfaceSourceLoading = false;
                desktopSurfaceFrame = MarsRegionalTerrain.Current;
                surfaceRoot.SetActive(false);
                MarsRegionalTerrain.Configure(mission);
                terrainRoot.SetActive(true);
                routesRoot.SetActive(true);
            }
            astronaut.SetActive(false);
            routeView.SetMissionMode(false);
            routeView.Select(comparedRoute);
            cameraRig.SetMissionMode(false, astronaut.transform, keepRemotePose: true);
            cameraRig.ResetPlanningView();
        }

        private void SelectPlanningRoute(int index)
        {
            inspectProgress = 0;
            if (!desktopBrowsingRoutes)
            {
                SelectRoute(index);
                return;
            }
            comparedRoute = index;
            routeView.Select(index);
        }

        private void ReturnToLiveMission()
        {
            if (!desktopBrowsingRoutes) return;
            ExitDesktopRouteBrowser();
            cameraRig.SetMissionMode(true, astronaut.transform);
            previousLiveState = currentLiveState;
            UpdateDesktopMirror(snap: true);
            cameraRig.SnapToMission(astronaut.transform);
            BeginSurfaceSourceRefinement();
        }

        private void ExitDesktopRouteBrowser()
        {
            if (!desktopBrowsingRoutes) return;
            desktopBrowsingRoutes = false;
            showPlanningDetails = false;
            routeView.Select(selectedRoute);
            routeView.SetMissionMode(true);
            if (surfaceActive)
            {
                terrainRoot.SetActive(false);
                routesRoot.SetActive(false);
                MarsRegionalTerrain.Configure(desktopSurfaceFrame);
                surfaceRoot.SetActive(true);
            }
            desktopSurfaceFrame = null;
            astronaut.SetActive(true);
        }

        private void TickMissionSynchronization(bool quest)
        {
            if (quest)
            {
                if (questDispatchedMission && missionStarted && Time.unscaledTime >= nextLivePublication)
                    PublishQuestState(true);
                return;
            }
            if (!desktopMissionMirror) return;
            missionRadio.SetMirrorReady(mirroredSession, missionStarted && !helmetEntryLoading);
            if (missionRadio.QuestWaiting && !helmetEntryLoading)
            {
                ReturnToPlanning();
                return;
            }
            byte[] payload = missionRadio.TakeLiveState();
            if (payload == null) return;
            MarsMissionLiveState state;
            try { state = MarsMissionLiveState.Decode(payload); }
            catch (ArgumentException) { return; }
            if (state.session != mirroredSession.ToString("N") ||
                currentLiveState != null && state.sequence <= currentLiveState.sequence) return;
            previousLiveState = currentLiveState ?? state;
            currentLiveState = state;
            liveReceivedAt = Time.unscaledTimeAsDouble;
            if (!state.inMission && !helmetEntryLoading)
            {
                ReturnToPlanning();
                return;
            }
            if (Time.unscaledTime >= nextLiveLog)
            {
                Debug.Log($"MARSPATH_LIVE: received {mission.routes[selectedRoute].id}, " +
                    $"progress={state.progress:P2}, moving={state.moving}, session={mirroredSession:N}");
                nextLiveLog = Time.unscaledTime + 5;
            }
        }

        private void PublishQuestState(bool inMission)
        {
            if (missionRadio == null || questLiveSession == Guid.Empty) return;
            TryGetCompactWarning(out string warning, out string detail, out MarsHazardIcons.Symbol symbol);
            bool hasHeadPose = cameraRig.TryCaptureQuestView(astronaut.transform, out Vector3 eyeOffset, out Quaternion eyeRotation);
            float? lookHeading = cameraRig.QuestLookHeading;
            var state = new MarsMissionLiveState
            {
                session = questLiveSession.ToString("N"), sequence = ++liveSequence,
                clock = Time.unscaledTimeAsDouble, missionSeconds = totalElapsedSeconds,
                travelledMetres = surfaceActive ? surfaceMetres : routeProgress * pathLength,
                progress = routeProgress, demoSeconds = DemoElapsed, oxygen = oxygen, power = power,
                metabolicWatts = metabolicWatts, electricalWatts = electricalWatts, oxygenFlow = oxygenFlowGramsPerHour,
                latitude = currentLatitude, longitude = currentLongitude, heading = currentHeadingDegrees,
                surfaceSlope = currentSlopeDegrees, trackGrade = currentSignedDemGradeDegrees,
                resupplyLeg = resupplyLeg, inMission = inMission, moving = !paused && !arrived && !DemoEnded && !reserveStop,
                arrived = arrived, reserveStop = reserveStop, demo = demoPresentation, demoComplete = DemoEnded,
                warning = warning, warningDetail = detail, warningSymbol = (int)symbol,
                hasHeadPose = hasHeadPose, headOffset = eyeOffset, headRotation = eyeRotation,
                hasLookHeading = lookHeading.HasValue, lookHeading = lookHeading.GetValueOrDefault(),
                facingBlocked = QuestFacingBlocked, facingError = questRouteFacing.SignedError
            };
            missionRadio.SendLiveState(state.Encode());
            nextLivePublication = Time.unscaledTime + 1f / 30;
        }

        private void UpdateDesktopMirror(bool snap = false)
        {
            bool fresh = LiveMissionFresh;
            if (currentLiveState == null || !currentLiveState.inMission || !fresh && !snap)
            {
                paused = true;
                SetAstronautWalking(false);
                return;
            }
            MarsMissionLiveState state = currentLiveState, prior = previousLiveState ?? state;
            double sampleTime = state.clock + (Time.unscaledTimeAsDouble - liveReceivedAt) - .06;
            float poseBlend = snap ? 1 : Mathf.Clamp01((float)((sampleTime - prior.clock) / Math.Max(.001, state.clock - prior.clock)));
            float blend = state.moving && prior.moving && state.progress >= prior.progress ? poseBlend : 1;
            routeProgress = Mathf.Lerp(prior.progress, state.progress, blend);
            totalElapsedSeconds = prior.missionSeconds + (state.missionSeconds - prior.missionSeconds) * blend;
            mirrorDemoSeconds = Mathf.Lerp(prior.demoSeconds, state.demoSeconds, blend);
            demoPresentation = state.demo;
            if (surfaceActive)
                UpdateSurfaceDistance(prior.travelledMetres + (state.travelledMetres - prior.travelledMetres) * blend);
            else UpdateAstronautPosition(routeProgress);
            mirroredLookHeading = state.hasLookHeading ? (prior.hasLookHeading
                ? Mathf.Repeat(Mathf.LerpAngle(prior.lookHeading, state.lookHeading, poseBlend), 360)
                : state.lookHeading) : (float?)null;
            if (state.hasHeadPose)
                cameraRig.SetRemoteHeadPose(prior.hasHeadPose ? Vector3.Lerp(prior.headOffset, state.headOffset, poseBlend) : state.headOffset,
                    prior.hasHeadPose ? Quaternion.Slerp(prior.headRotation, state.headRotation, poseBlend) : state.headRotation,
                    mirroredLookHeading);
            oxygen = state.oxygen; power = state.power;
            metabolicWatts = state.metabolicWatts; electricalWatts = state.electricalWatts;
            oxygenFlowGramsPerHour = state.oxygenFlow;
            currentLatitude = state.latitude; currentLongitude = state.longitude;
            currentHeadingDegrees = state.heading; currentSlopeDegrees = state.surfaceSlope;
            currentSignedDemGradeDegrees = state.trackGrade; currentDemSlopeDegrees = Mathf.Abs(state.trackGrade);
            missionElapsedMinutes = (float)(totalElapsedSeconds / 60);
            resupplyLeg = state.resupplyLeg;
            paused = !fresh || !state.moving; arrived = state.arrived; reserveStop = state.reserveStop;
            SetAstronautWalking(!paused);
        }

        private void DrawLiveMissionProgress(Color accent)
        {
            Rect card = new Rect(431, 22, 738, 101);
            VisorPanel(card, LiveMissionFresh ? accent : UiAmber);
            string status = currentLiveState == null ? "WAITING FOR QUEST"
                : !LiveMissionFresh ? "RECONNECTING · HOLDING POSITION"
                : arrived ? "DESTINATION REACHED" : reserveStop ? "RESERVE STOP"
                : DemoEnded ? "DEMO COMPLETE" : currentLiveState.facingBlocked ? "FACING AWAY · WALKING LOCKED"
                : paused ? "ASTRONAUT STOPPED" : "ASTRONAUT WALKING";
            GUI.Label(new Rect(449, 32, 371, 23), "MISSION CONTROL  /  LIVE QUEST", visorHeading);
            GUI.Label(new Rect(831, 29, 171, 31), $"{routeProgress * 100:0.0} / 100%", visorLivePercent);
            GUI.Label(new Rect(449, 60, 371, 23), status, visorSmall);
            float travelled = mission.routes[selectedRoute].distanceKm * 1000 * routeProgress;
            GUI.Label(new Rect(831, 60, 171, 23), $"{travelled:0} / {mission.routes[selectedRoute].distanceKm * 1000:0} m",
                visorRouteRight);
            UiBar(new Rect(449, 94, 553, 5), routeProgress, accent);
            UiFill(new Rect(1021, 35, 1, 68), new Color(.35f, .68f, .72f, .33f));
            GUI.Label(new Rect(1032, 37, 127, 22), "LOOK HEADING", visorCenteredSmall);
            GUI.Label(new Rect(1032, 62, 127, 31), mirroredLookHeading is float bearing ? MarsCompass.Format(bearing) : "—",
                visorCompassHeading);
        }
    }
}

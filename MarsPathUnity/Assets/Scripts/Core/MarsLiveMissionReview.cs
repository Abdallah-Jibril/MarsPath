using System;
using System.Collections;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;
using UnityEngine;

namespace MarsPath
{
    public partial class MarsPathDemoBootstrap
    {
        private IEnumerator ReviewLiveMission()
        {
            bool captureCompass = Array.IndexOf(Environment.GetCommandLineArgs(), "--capture-live-compass") >= 0;
            RefinementEnabled = false;
            yield return PrepareJezeroShowcase();
            SelectRoute(1);
            yield return PrepareHelmetEntry(false);
            PauseQuestTraverse();
            questRouteFacing.Update(true, 179);
            SetQuestWalkInput(false, true);
            Quaternion beforePress = mainCamera.transform.rotation;
            SetQuestWalkInput(true, true);
            float lockedProgress = routeProgress;
            yield return new WaitForSeconds(.12f);
            RequireLive(paused && !QuestWalking && routeProgress == lockedProgress &&
                TryGetCompactWarning(out string lockedTitle, out _, out _) && lockedTitle == "ROUTE BEHIND YOU",
                "Opposite heading blocks a held right trigger and shows the warning");
            RequireLive(Quaternion.Angle(beforePress, mainCamera.transform.rotation) < .01f,
                "A blocked trigger cannot rotate the camera automatically");
            questRouteFacing.Update(true, 65);
            SetQuestWalkInput(true, true);
            RequireLive(paused, "Partial recovery cannot restart walking");
            questRouteFacing.Update(true, 25);
            SetQuestWalkInput(true, true);
            yield return new WaitForSeconds(.08f);
            RequireLive(!paused && routeProgress > lockedProgress, "Facing the route permits the held trigger to move again");
            SetQuestWalkInput(false, true);
            float releasedAt = routeProgress;
            float releasedSeconds = DemoElapsed;
            yield return new WaitForSecondsRealtime(.15f);
            RequireLive(paused && routeProgress == releasedAt && DemoElapsed == releasedSeconds,
                "Releasing the right trigger freezes the countdown and route position");
            RouteData demoRoute = mission.routes[selectedRoute];
            SetQuestWalkInput(true, true);
            TickTwoMinuteDemo(demoRoute, 1000);
            RequireLive(DemoElapsed == releasedSeconds && routeProgress == releasedAt,
                "Resuming does not count the stopped interval");
            TickTwoMinuteDemo(demoRoute, 1001);
            RequireLive(!paused && Mathf.Abs(DemoElapsed - (releasedSeconds + 1)) < .001f && routeProgress > releasedAt,
                "Resuming advances the countdown and route from the same point");
            TickTwoMinuteDemo(demoRoute, 1000 + MarsDemoDirector.DurationSeconds);
            RequireLive(DemoEnded && arrived && paused && routeProgress == 1 &&
                DemoElapsed == MarsDemoDirector.DurationSeconds && DemoCountdown == "0:00" &&
                MarsGeospatial.AngularDistanceDegrees(JourneyLocation, mission.destination) < .00001,
                "A Jezero route longer than the earlier preview reaches its destination at 120 active seconds");
            ReturnToPlanning();
            const int payloadSize = 1936;
            var listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            var laptop = new TcpClient();
            laptop.Connect(IPAddress.Loopback, ((IPEndPoint)listener.LocalEndpoint).Port);
            TcpClient quest = listener.AcceptTcpClient();
            listener.Stop();
            byte[] key = new byte[32];
            Task<MarsRadioPairing> questAuth = Task.Run(() => MarsRadioPairing.Authenticate(quest, key, true, payloadSize));
            MarsRadioPairing desktopAuth = MarsRadioPairing.Authenticate(laptop, key, false, payloadSize);
            while (!questAuth.IsCompleted) yield return null;
            var desktopRoot = new GameObject("Live review desktop link");
            var questRoot = new GameObject("Live review source link");
            missionRadio = desktopRoot.AddComponent<MarsMissionRadio>();
            var source = questRoot.AddComponent<MarsMissionRadio>();
            missionRadio.Initialize(false);
            source.Initialize(true);
            EnterHelmetFromPlanner();
            RequireLive(!missionStarted && !desktopMissionMirror && PlanningDispatchPending,
                "A disconnected desktop launch queues the route without starting a local mission");
            Guid session = missionRadio.DispatchId;
            SelectRoute(2); // Simulate a changed local selection after the frozen dispatch.
            missionRadio.OpenTransport(laptop, desktopAuth, false);
            source.OpenTransport(quest, questAuth.Result, false);
            bool passed = false;
            try
            {
                source.SetRouteWaiting(true);
                Guid receivedId = Guid.Empty;
                byte[] receivedPlan = null;
                yield return PumpUntil(() => source.TryTakeRoute(out receivedId, out receivedPlan));
                RequireLive(receivedId == session && DispatchedPlan.Decode(receivedPlan).selectedRoute == 1 && !missionStarted,
                    "Quest receives the exact frozen route before the desktop enters");
                source.ReportRoute(session, MarsMissionRadio.RouteDelivery.Preparing);
                yield return PumpUntil(() => missionRadio.DispatchStatus == MarsMissionRadio.RouteDelivery.Preparing);
                RequireLive(!missionStarted && !desktopMissionMirror, "Quest preparation keeps the desktop in the planner");
                source.ReportRoute(session, MarsMissionRadio.RouteDelivery.Started);
                yield return PumpUntil(() => missionStarted && !helmetEntryLoading);
                RequireLive(selectedRoute == 1 && routeProgress == 0 && DemoElapsed == 0 && paused,
                    "Selected route enters at zero and waits");
                var state = new MarsMissionLiveState
                {
                    session = session.ToString("N"), sequence = 1, clock = 1,
                    inMission = true, demo = true, oxygen = 94, power = 92,
                    hasHeadPose = true, headOffset = new Vector3(.12f, .94f, -.07f), headRotation = Quaternion.identity,
                    metabolicWatts = 330, electricalWatts = 79, oxygenFlow = 83,
                    warning = "STEEP SLOPE", warningDetail = "18° DEM · SLOW DOWN"
                };
                yield return Deliver(state);
                yield return new WaitForSeconds(.15f);
                RequireLive(routeProgress == 0 && paused, "Waiting time cannot advance the desktop route");
                state.sequence++; state.clock++;
                state.progress = .37f; state.moving = true;
                yield return Deliver(state);
                RequireLive(Mathf.Abs(routeProgress - .37f) < .0001f && !paused, "Desktop follows source movement");
                state.sequence++; state.clock++;
                state.progress = .42f; state.moving = false;
                yield return Deliver(state);
                float stopped = routeProgress;
                float stoppedDemoTime = DemoElapsed;
                state.sequence++; state.clock++;
                yield return Deliver(state);
                yield return new WaitForSeconds(.15f);
                RequireLive(routeProgress == stopped && paused && oxygen == 94 && power == 92 &&
                    DemoElapsed == stoppedDemoTime,
                    "The mirror preserves the stopped countdown, progress, and reserves");
                GeoPoint expected = fullTrack.At(.42f);
                RequireLive(MarsGeospatial.AngularDistanceDegrees(JourneyLocation, expected) < .00001 &&
                    TryGetCompactWarning(out string title, out _, out _) && title == "STEEP SLOPE",
                    "Geographic location and hazard match the source");

                // Looking around must still update when progress and walking are stopped.
                state.sequence++; state.clock++;
                state.headRotation = Quaternion.Euler(-22, 171, 8);
                state.facingBlocked = true; state.facingError = -171;
                state.warning = "ROUTE BEHIND YOU"; state.warningDetail = "TURN AROUND · WALK LOCKED";
                state.warningSymbol = (int)MarsHazardIcons.Symbol.Direction;
                yield return Deliver(state);
                yield return new WaitForSeconds(.14f);
                yield return new WaitForEndOfFrame();
                RequireLive(Quaternion.Angle(mainCamera.transform.rotation,
                    astronaut.transform.rotation * state.headRotation) < .25f &&
                    Vector3.Distance(mainCamera.transform.position,
                        astronaut.transform.TransformPoint(state.headOffset)) < .001f,
                    "Stopped astronaut head yaw, pitch, roll and eye offset synchronize");
                RequireLive(mirroredLookHeading is float poseOnlyHeading &&
                    MarsCompass.TryGetHeading(mainCamera.transform.forward, out float renderedHeading) &&
                    Mathf.Abs(Mathf.DeltaAngle(poseOnlyHeading, renderedHeading)) < .25f,
                    "The desktop compass stays available when Quest sends only its tracked pose");
                RequireLive(currentLiveState.facingBlocked && paused &&
                    TryGetCompactWarning(out string facingTitle, out _, out _) && facingTitle == "ROUTE BEHIND YOU",
                    "The live desktop shows the headset direction lock and warning");
                cameraRig.SetHelmetView(false);
                yield return new WaitForEndOfFrame();
                RequireLive(!cameraRig.HelmetView && routeProgress == stopped,
                    "Mission Control keeps its chase view while progress remains source controlled");
                cameraRig.SetHelmetView(true);
                state.facingBlocked = false; state.facingError = 0;
                state.headRotation = Quaternion.identity;
                state.warning = "STEEP SLOPE";

                state.sequence++; state.clock++; state.progress = .9f; state.session = Guid.NewGuid().ToString("N");
                source.SendLiveState(state.Encode());
                yield return new WaitForSeconds(.03f);
                missionRadio.Receive(); TickMissionSynchronization(false);
                yield return null;
                RequireLive(routeProgress == stopped, "A different mission session cannot move the desktop");
                state.session = session.ToString("N");
                state.sequence -= 2;
                source.SendLiveState(state.Encode());
                yield return new WaitForSeconds(.03f);
                missionRadio.Receive(); TickMissionSynchronization(false);
                yield return null;
                RequireLive(routeProgress == stopped, "An old state cannot move the desktop");

                yield return PumpUntil(() => source.RemoteMirrorReady);
                BrowseLiveRoutes();
                GameObject retainedSurface = surfaceRoot;
                RequireLive(desktopBrowsingRoutes && desktopMissionMirror && missionStarted && terrainRoot.activeSelf &&
                    !retainedSurface.activeSelf && MarsRegionalTerrain.Current == mission,
                    "Browsing uses the regional planner while keeping the live mission and surface resident");
                SelectPlanningRoute(2);
                RequireLive(PlanningRouteIndex == 2 && selectedRoute == 1 && missionRadio.DispatchId == session &&
                    SelectedElevationProfile == elevationProfile && profileRoute == 2,
                    "Comparing another route and its elevation profile does not change the live selection");
                EnterHelmetFromPlanner();
                BeginHelmetEntry(true);
                RequireLive(!PlanningDispatchPending && !helmetEntryLoading,
                    "The live planner cannot dispatch or start a separate helmet preview");

                state.sequence += 3; state.clock++;
                state.progress = .58f; state.moving = true;
                state.hasLookHeading = true; state.lookHeading = 90f;
                yield return Deliver(state);
                yield return PumpUntil(() => source.RemoteMirrorReady);
                RequireLive(currentLiveState.progress == .58f && routeProgress == stopped && selectedRoute == 1,
                    "Quest progress and readiness continue while laptop terrain stays in the comparison frame");
                if (captureCompass) yield return CaptureCompassFrame("MarsPathBrowseHeadingReview.png");
                ReturnToLiveMission();
                RequireLive(!desktopBrowsingRoutes && selectedRoute == 1 && Mathf.Abs(routeProgress - .58f) < .0001f &&
                    !paused && retainedSurface.activeSelf && MarsRegionalTerrain.Current != mission &&
                    mirroredLookHeading == 90f && MarsCompass.Format(mirroredLookHeading.Value) == "E  090°" &&
                    MarsCompass.TryGetHeading(mainCamera.transform.forward, out float visorBearing) && Mathf.Abs(visorBearing - 90) < .01f,
                    "Back to Live resumes the same mission at the latest progress and eastward gaze");
                if (captureCompass) yield return CaptureCompassFrame("MarsPathLiveCompassReview.png");
                state.sequence++; state.clock++;
                state.moving = false; state.lookHeading = 270f;
                yield return Deliver(state);
                yield return new WaitForSeconds(.14f);
                RequireLive(mirroredLookHeading == 270f && paused && Mathf.Abs(routeProgress - .58f) < .0001f,
                    "The live compass follows a turn to the west while the route is stopped");

                BrowseLiveRoutes();
                state.sequence++; state.clock++;
                state.progress = .68f; state.moving = true;
                yield return Deliver(state);
                liveReceivedAt = Time.unscaledTimeAsDouble - 2;
                ReturnToLiveMission();
                RequireLive(paused && Mathf.Abs(routeProgress - .68f) < .0001f,
                    "Returning during a link gap holds the latest confirmed position rather than the old view");

                double receivedAt = liveReceivedAt;
                liveReceivedAt = Time.unscaledTimeAsDouble - 2;
                float beforeStale = routeProgress;
                float timeBeforeStale = DemoElapsed;
                yield return new WaitForSeconds(.1f);
                RequireLive(paused && routeProgress == beforeStale && DemoElapsed == timeBeforeStale,
                    "A stale connection freezes the last confirmed location and countdown");
                liveReceivedAt = receivedAt;
                state.sequence++; state.clock++; state.progress = 1; state.moving = false; state.arrived = true;
                state.demoComplete = true;
                yield return Deliver(state);
                RequireLive(arrived && routeProgress == 1 && paused && DemoEnded && DemoCountdown == "0:00",
                    "The laptop mirrors arrival at 100 percent and 0:00");
                BrowseLiveRoutes();
                state.sequence++; state.clock++; state.inMission = false;
                yield return Deliver(state);
                RequireLive(!missionStarted && !desktopMissionMirror && !desktopBrowsingRoutes && astronaut.activeSelf &&
                    MarsRegionalTerrain.Current == mission, "Headset exit clears browse mode and restores standard planning");
                passed = true;
            }
            finally
            {
                EndDesktopMirror();
                Destroy(desktopRoot);
                Destroy(questRoot);
            }
            Debug.Log(passed ? "MARSPATH_LIVE_REVIEW_PASSED: selected route, zero start, source movement, stop, " +
                "Quest acknowledgement, facing gate, reserves, hazards, head pose, compass, route browsing and latest live return, stale link, paused countdown and end-to-end demo arrival."
                : "MARSPATH_LIVE_REVIEW_FAILED");
            Application.Quit(passed ? 0 : 2);

            IEnumerator PumpUntil(Func<bool> done)
            {
                var flush = typeof(MarsMissionRadio).GetMethod("SendControlFrames",
                    System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
                float deadline = Time.realtimeSinceStartup + 12;
                while (Time.realtimeSinceStartup < deadline)
                {
                    flush.Invoke(source, null); flush.Invoke(missionRadio, null);
                    source.Receive(); missionRadio.Receive();
                    TickDesktopDispatch();
                    if (done()) yield break;
                    yield return null;
                }
                RequireLive(false, "Dispatch handshake timed out");
            }

            IEnumerator Deliver(MarsMissionLiveState sample)
            {
                sample.missionSeconds = sample.progress * mission.routes[selectedRoute].etaMinutes * 60;
                sample.travelledMetres = sample.progress * fullTrack.LengthKm * 1000;
                sample.demoSeconds = sample.demoComplete
                    ? MarsDemoDirector.DurationSeconds : sample.progress * MarsDemoDirector.DurationSeconds;
                GeoPoint point = fullTrack.At(sample.progress);
                sample.latitude = point.latitude; sample.longitude = point.longitude;
                source.SendLiveState(sample.Encode());
                float deadline = Time.realtimeSinceStartup + 3;
                while (Time.realtimeSinceStartup < deadline)
                {
                    missionRadio.Receive();
                    TickMissionSynchronization(false);
                    if (currentLiveState?.sequence == sample.sequence || !desktopMissionMirror) break;
                    yield return null;
                }
                RequireLive(currentLiveState?.sequence == sample.sequence || !sample.inMission,
                    "Live state must arrive");
                yield return null;
            }

            IEnumerator CaptureCompassFrame(string name)
            {
                yield return new WaitForEndOfFrame();
                Texture2D frame = ScreenCapture.CaptureScreenshotAsTexture();
                try
                {
                    string path = System.IO.Path.Combine(Application.persistentDataPath, name);
                    System.IO.File.WriteAllBytes(path, frame.EncodeToPNG());
                    Debug.Log("MARSPATH_COMPASS_CAPTURE: " + path);
                }
                finally { Destroy(frame); }
            }
        }

        private static void RequireLive(bool condition, string message)
        {
            if (!condition)
            {
                Application.Quit(2);
                throw new InvalidOperationException("MARSPATH_LIVE_REVIEW_FAILED: " + message);
            }
            Debug.Log("MARSPATH_LIVE_ASSERT: " + message);
        }
    }
}

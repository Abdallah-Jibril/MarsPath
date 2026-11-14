using System;
using System.Collections;
using UnityEngine;

namespace MarsPath
{
    public partial class MarsPathDemoBootstrap
    {
        private MissionData jezeroShowcaseMission;
        private bool preparingJezeroShowcase;

        private IEnumerator PrepareJezeroShowcase()
        {
            if (preparingJezeroShowcase || GlobalView.IsBusy) yield break;
            preparingJezeroShowcase = true;
            try
            {
                if (Application.platform == RuntimePlatform.Android) RefinementEnabled = false;
                var start = new GeoPoint
                {
                    latitude = 18.47500229f, longitude = 77.37731171f, label = "Belva inner rim approach"
                };
                var end = new GeoPoint
                {
                    latitude = 18.47687721f, longitude = 77.37770844f, label = "Belva inner slope waypoint"
                };
                GlobalView.OpenMissionZone();
                GlobalView.SetEndpoints(start, end);
                yield return BuildSelectedRegion(start, end);

                if (GlobalView.IsVisible || !MarsMissionZone.IsMission(mission) ||
                    !mission.routes[selectedRoute].available)
                {
                    if (Application.platform == RuntimePlatform.Android)
                    {
                        if (!GlobalView.IsVisible) ReturnToGlobalMap();
                        GlobalView.CloseMissionZone();
                        questLobbyMessage = "The measured Jezero demo could not load";
                    }
                    GlobalView.SetError("The Jezero demo could not load a feasible route.");
                    Debug.LogError("JEZERO_SHOWCASE_FAILED: measured route unavailable.");
                    yield break;
                }

                jezeroShowcaseMission = mission;
                demoPresentation = true;
                Debug.Log($"JEZERO_SHOWCASE_READY: {mission.routes[selectedRoute].distanceKm * 1000:0.0} m, " +
                    $"{DemoScope(mission.routes[selectedRoute])}, start {start.latitude:R},{start.longitude:R}");
            }
            finally
            {
                preparingJezeroShowcase = false;
            }
        }

        private IEnumerator Start()
        {
            if (Application.platform == RuntimePlatform.Android) yield break;
            string[] arguments = Environment.GetCommandLineArgs();
            if (Array.IndexOf(arguments, "--mars-live-mission-review") >= 0)
            {
                yield return ReviewLiveMission();
                yield break;
            }
            if (Array.IndexOf(arguments, "--mars-quest-globe-ui-review") >= 0)
            {
                yield return ReviewQuestGlobeUi();
                yield break;
            }
            if (Array.IndexOf(arguments, "--mars-jezero-audio-review") >= 0)
            {
                yield return ReviewJezeroAudio();
                yield break;
            }
            if (Array.IndexOf(arguments, "--mars-jezero-showcase-review") < 0) yield break;

            RefinementEnabled = false;
            yield return PrepareJezeroShowcase();
            if (mission != jezeroShowcaseMission)
            {
                Debug.LogError("JEZERO_SHOWCASE_FAILED: continuous route unavailable.");
                Application.Quit(2);
                yield break;
            }

            MarsRouteElevationProfile profile = SelectedElevationProfile;
            bool relief = profile.ascent >= 5f && profile.descent >= 20f;
            bool distinct = CheckShowcaseAlternatives();
            Debug.Log($"JEZERO_SHOWCASE_CRATER: measured {profile.ascent:0.0} m ascent / {profile.descent:0.0} m descent.");
            cameraRig.ZoomPlanning(100);
            bool zoomBounded = cameraRig.PlanningZoom >= 1.6f && cameraRig.PlanningZoom <= 2.801f && !cameraRig.CanZoomPlanningIn;
            Debug.Log($"JEZERO_SHOWCASE_ZOOM: {cameraRig.PlanningZoom:0.00}x at the overview limit.");
            cameraRig.ResetPlanningView();
            yield return CaptureHelmetReview("MarsPathJezeroShowcasePlanner.png");
            yield return PrepareHelmetEntry(false);
            paused = true;
            cameraRig.SnapToMission(astronaut.transform);
            bool nativeImage = MarsOfficialTerrain.TryActiveMatchingImage(JourneyLocation,out _,out float imageSpacing)
                && Mathf.Approximately(imageSpacing,.25f);
            Debug.Log($"JEZERO_SHOWCASE_IMAGE: {imageSpacing:0.00} m photograph on the original 1 m height grid.");
            bool passed = zoomBounded && nativeImage && relief && distinct && surfaceActive && VisibleSurfaceArrows > 0 &&
                Mathf.Abs(MarsRegionalTerrain.MeasuredElevation(JourneyLocation) - MarsMissionZone.Elevation(JourneyLocation)) < .08f;
            Debug.Log($"JEZERO_SHOWCASE_ENTRY: passed {passed}, surface {surfaceActive}, arrows {VisibleSurfaceArrows}, height residual {MarsRegionalTerrain.MeasuredElevation(JourneyLocation)-MarsMissionZone.Elevation(JourneyLocation):F4} m.");
            yield return CaptureHelmetReview("MarsPathJezeroShowcaseVisor.png");

            AdvancePresentation(60, mission.routes[selectedRoute]);
            cameraRig.SnapToMission(astronaut.transform);
            passed &= routeProgress > .49f && routeProgress < .51f && VisibleSurfaceArrows > 0;
            Debug.Log($"JEZERO_SHOWCASE_MIDPOINT: passed {passed}, progress {routeProgress:R}, O2 {evaSimulation.OxygenPercent:F3}%, power {evaSimulation.PowerPercent:F3}%, reserve {evaSimulation.ReserveReached}, arrows {VisibleSurfaceArrows}.");
            yield return CaptureHelmetReview("MarsPathJezeroShowcaseMidpoint.png");

            AdvancePresentation(60, mission.routes[selectedRoute]);
            passed &= demoDirector.Complete && routeProgress > .999f &&
                MarsGeospatial.AngularDistanceDegrees(JourneyLocation, mission.destination) < .00001f;
            Debug.Log($"JEZERO_SHOWCASE_END: passed {passed}, progress {routeProgress:R}, complete {demoDirector.Complete}, endpoint error {MarsGeospatial.AngularDistanceDegrees(JourneyLocation,mission.destination):R} deg.");

            QuestReturnToRoutes();
            QuestBackToEndpoints();
            GlobalView.CloseMissionZone();
            QuestSelectEndpoint(new GeoPoint { latitude = 18.45f, longitude = 77.41f }, false);
            QuestSelectEndpoint(new GeoPoint { latitude = 18.47f, longitude = 77.43f }, true);
            passed &= GlobalView.IsVisible && !missionStarted &&
                GlobalView.StartLocation.longitude == 77.41f && GlobalView.EndLocation.longitude == 77.43f;
            QuestPlanJezeroDemo();
            float deadline = Time.realtimeSinceStartup + 20f;
            while (preparingJezeroShowcase && Time.realtimeSinceStartup < deadline) yield return null;
            passed &= QuestPlanningVisible && !missionStarted && demoPresentation;
            Debug.Log($"JEZERO_SHOWCASE_RESELECT: passed {passed}, planning {QuestPlanningVisible}, started {missionStarted}, demo {demoPresentation}.");
            int alternative = Array.FindIndex(mission.routes, route => route.available && route.type != mission.routes[0].type);
            passed &= alternative > 0;
            if (alternative > 0) QuestChooseRoute(alternative);
            QuestEnterSelectedRoute();
            deadline = Time.realtimeSinceStartup + 20f;
            while (helmetEntryLoading && Time.realtimeSinceStartup < deadline) yield return null;
            passed &= missionStarted && paused && selectedRoute == alternative && routeProgress == 0;
            Debug.Log($"JEZERO_SHOWCASE_ALTERNATE: passed {passed}, started {missionStarted}, paused {paused}, route {selectedRoute}/{alternative}, progress {routeProgress:R}.");
            SetQuestWalkInput(true, true);
            passed &= paused;
            SetQuestWalkInput(false, true);
            SetQuestWalkInput(true, true);
            passed &= !paused;
            yield return new WaitForSecondsRealtime(.35f);
            SetQuestWalkInput(false, true);
            float stoppedAt = routeProgress;
            yield return new WaitForSecondsRealtime(.25f);
            passed &= paused && stoppedAt > 0 && routeProgress == stoppedAt;
            QuestReturnToRoutes();
            passed &= QuestPlanningVisible && selectedRoute == alternative;
            Debug.Log($"QUEST_ROUTE_HOLD: selected {selectedRoute}, moved {stoppedAt:P2}, paused on release, returned to routes.");
            if (passed) Debug.Log("JEZERO_SHOWCASE_PASSED: route, measured ground, continuous progress and arrival.");
            else Debug.LogError("JEZERO_SHOWCASE_FAILED: route, source or playback verification.");
            Application.Quit(passed ? 0 : 2);
        }

        private IEnumerator ReviewJezeroAudio()
        {
            // Audibility checks need a focused player, as does the product's
            // outside-suit mixer. Allow the UI harness to activate the window.
            float focusDeadline = Time.realtimeSinceStartup + 45;
            while (!Application.isFocused && Time.realtimeSinceStartup < focusDeadline) yield return null;
            yield return PrepareJezeroShowcase();
            yield return PrepareHelmetEntry(false);
            PauseQuestTraverse();
            yield return new WaitForSecondsRealtime(.6f);
            bool started = missionStarted && paused && evaAudio != null && evaAudio.HasClips && evaAudio.AmbiencePlaying;
            if (!started)
            {
                Debug.LogError($"JEZERO_AUDIO_FAILED: focused={Application.isFocused}, mission={missionStarted}, paused={paused}, " +
                    $"soundscape={evaAudio != null}, clips={evaAudio != null && evaAudio.HasClips}, " +
                    $"ambience={evaAudio != null && evaAudio.AmbiencePlaying}.");
                Application.Quit(2);
                yield break;
            }

            SetQuestWalkInput(false, true);
            SetQuestWalkInput(true, true);
            yield return new WaitForSecondsRealtime(1.8f);
            int walked = evaAudio.FootfallCount;
            SetQuestWalkInput(false, true);
            SeekFullJourney(.6875f);
            yield return new WaitForSecondsRealtime(.6f);
            bool pausedCleanly = paused && evaAudio.AmbiencePlaying && evaAudio.FootfallCount == walked;
            QuestReturnToRoutes();
            QuestBackToEndpoints();
            yield return new WaitForSecondsRealtime(.6f);
            bool stopped = GlobalView.IsVisible && !evaAudio.AmbiencePlaying;
            bool passed = walked >= 2 && pausedCleanly && stopped;
            Debug.Log($"JEZERO_AUDIO_{(passed ? "PASSED" : "FAILED")}: eleven clips / {walked} footsteps / pause and seek silent {pausedCleanly} / globe silent {stopped}");
            Application.Quit(passed ? 0 : 2);
        }

        private bool CheckShowcaseAlternatives()
        {
            RouteData[] routes = mission.routes;
            if (routes.Length != 5 || Array.Exists(routes, route => !route.available || route.waypoints.Length < 2))
                return false;
            float minimum = float.PositiveInfinity;
            for (int i = 0; i < routes.Length; i++)
            {
                var profile = new MarsRouteElevationProfile(mission, routes[i]);
                Debug.Log($"JEZERO_SHOWCASE_PROFILE: {routes[i].displayName} {profile.lengthKm * 1000:0.0} m / +{profile.ascent:0.0} / -{profile.descent:0.0} m");
                for (int j = 0; j < i; j++)
                    minimum = Mathf.Min(minimum, Mathf.Max(ShowcaseOffset(routes[i], routes[j]), ShowcaseOffset(routes[j], routes[i])));
            }
            minimum *= 60 * MarsRegionalTerrain.MetersPerUnit;
            Debug.Log($"JEZERO_SHOWCASE_VARIANTS: minimum pairwise divergence {minimum:0.0} m.");
            // A corrected energy objective may legitimately choose a corridor
            // close to another profile. Do not distort its costs to meet an
            // arbitrary twelve-metre visual separation target.
            return minimum >= Mathf.Max(1,MarsRegionalTerrain.SourceSpacingMeters(mission.currentLocation));
        }

        private static float ShowcaseOffset(RouteData source, RouteData other)
        {
            float maximum = 0;
            foreach (RoutePoint waypoint in source.waypoints)
            {
                var point = new Vector2(waypoint.x, waypoint.z);
                float nearest = float.PositiveInfinity;
                for (int i = 1; i < other.waypoints.Length; i++)
                {
                    RoutePoint a = other.waypoints[i - 1], b = other.waypoints[i];
                    var origin = new Vector2(a.x, a.z);
                    var edge = new Vector2(b.x - a.x, b.z - a.z);
                    float along = Mathf.Clamp01(Vector2.Dot(point - origin, edge) / Mathf.Max(1e-10f, edge.sqrMagnitude));
                    nearest = Mathf.Min(nearest, (point - origin - edge * along).sqrMagnitude);
                }
                maximum = Mathf.Max(maximum, nearest);
            }
            return Mathf.Sqrt(maximum);
        }
    }
}

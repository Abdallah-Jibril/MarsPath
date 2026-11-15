using System.Collections;
using UnityEngine;

namespace MarsPath
{
    public partial class MarsPathDemoBootstrap
    {
        private string questLobbyMessage;
        private bool questWaitForTriggerRelease;
        private readonly MarsRouteFacing questRouteFacing = new MarsRouteFacing();
        internal bool QuestFacingBlocked => questRouteFacing.Blocked;

        internal string QuestLobbyStatus => GlobalView.IsBusy || preparingJezeroShowcase || helmetEntryLoading
            ? "Loading measured terrain"
            : questLobbyMessage;
        internal bool QuestPlanningVisible => GlobalView != null && !GlobalView.IsVisible && !missionStarted;
        internal bool QuestCanReplayOrContinue => arrived || DemoEnded && !reserveStop;
        internal int QuestRouteIndex => selectedRoute;
        internal bool QuestDemoMode => demoPresentation;
        internal bool QuestWalking => missionStarted && !paused && !arrived && !DemoEnded && !reserveStop;
        internal string QuestDemoScope => DemoScope(mission.routes[selectedRoute]);
        internal float QuestPlanningMesh => PlanningMeshCellMeters;
        internal string QuestPlanningStatus => helmetEntryLoading
            ? $"PREPARING TERRAIN   {helmetEntryPercent:P0}"
            : null;

        internal void QuestSelectEndpoint(GeoPoint point, bool destination)
        {
            if (!GlobalView.IsVisible || GlobalView.IsBusy || preparingJezeroShowcase) return;
            if (GlobalView.LocalZoneVisible && !MarsMissionZone.Contains(point)) return;
            point.label = destination ? "Destination" : "Start";
            GlobalView.SetEndpoints(destination ? GlobalView.StartLocation : point,
                destination ? point : GlobalView.EndLocation);
            questLobbyMessage = $"{point.label} set  {point.latitude:0.00}°, {point.longitude:0.00}°";
        }

        internal void QuestPlanSelectedRoute()
        {
            if (!GlobalView.IsVisible || GlobalView.IsBusy || preparingJezeroShowcase) return;
            GeoPoint start = GlobalView.StartLocation;
            GeoPoint end = GlobalView.EndLocation;
            if (GlobalView.LocalZoneVisible && (!MarsMissionZone.Contains(start) || !MarsMissionZone.Contains(end)))
            {
                questLobbyMessage = "Place both pins inside the measured zone";
                return;
            }
            float distance = MarsGeospatial.AngularDistanceDegrees(start, end) * Mathf.Deg2Rad * (float)MarsRegionalTerrain.RadiusMeters;
            if (distance < 80f)
            {
                questLobbyMessage = "Place the two pins at least 80 m apart";
                return;
            }
            questLobbyMessage = null;
            StartCoroutine(PrepareQuestSelectedRoute(start, end));
        }

        private IEnumerator PrepareQuestSelectedRoute(GeoPoint start, GeoPoint end)
        {
            yield return BuildSelectedRegion(start, end);
            if (GlobalView.IsVisible || !mission.routes[selectedRoute].available)
            {
                if (!GlobalView.IsVisible) ReturnToGlobalMap();
                questLobbyMessage = "No route found. Try another pair of pins";
                yield break;
            }
            demoPresentation = true;
        }

        internal void QuestPlanJezeroDemo()
        {
            if (GlobalView.IsVisible && !GlobalView.IsBusy && !preparingJezeroShowcase)
            {
                questLobbyMessage = null;
                StartCoroutine(PrepareJezeroShowcase());
            }
        }

        internal void QuestChooseRoute(int index)
        {
            if (!QuestPlanningVisible || helmetEntryLoading || index == selectedRoute || index < 0 ||
                index >= mission.routes.Length || !mission.routes[index].available) return;
            SelectRoute(index);
            inspectProgress = 0;
            if (Application.platform == RuntimePlatform.Android)
                Debug.Log($"MARSPATH_XR: selected route {mission.routes[index].id}");
        }

        internal MarsRouteFacts QuestRouteFacts() => MarsRouteFacts.For(mission, mission.routes[selectedRoute], SelectedElevationProfile);

        internal void QuestSetDemoMode(bool demo)
        {
            if (QuestPlanningVisible && !helmetEntryLoading) demoPresentation = demo;
        }

        internal void QuestEnterSelectedRoute()
        {
            if (QuestPlanningVisible && !helmetEntryLoading && mission.routes[selectedRoute].available)
                StartCoroutine(EnterQuestHelmet());
        }

        private IEnumerator EnterQuestHelmet()
        {
            yield return PrepareHelmetEntry(false);
            if (missionStarted)
            {
                PauseQuestTraverse();
                if (Application.platform == RuntimePlatform.Android)
                    Debug.Log($"MARSPATH_XR: entered {mission.routes[selectedRoute].id}, hold trigger to walk");
            }
        }

        internal void QuestBackToEndpoints()
        {
            if (!QuestPlanningVisible || helmetEntryLoading) return;
            ReturnToGlobalMap();
            questLobbyMessage = null;
        }

        internal void QuestReturnToRoutes()
        {
            if (!missionStarted || helmetEntryLoading) return;
            if (questDispatchedMission)
            {
                PublishQuestState(false);
                questDispatchedMission = false;
                ReturnToGlobalMap();
                if (GlobalView.LocalZoneVisible) GlobalView.CloseMissionZone();
            }
            else ReturnToPlanning();
        }

        internal void QuestReplay()
        {
            if (!missionStarted || !QuestCanReplayOrContinue) return;
            if (!arrived)
            {
                ContinueAfterDemo();
                return;
            }
            StartMission();
            PauseQuestTraverse();
        }

        internal string QuestVisorStatus => !missionStarted ? "LOADING" : arrived ? "ARRIVED" : FormatRouteLength(DistanceRemainingKm);
        internal string QuestVisorRoute => mission.routes[selectedRoute].displayName.ToUpperInvariant();
        internal string QuestVisorEta => demoPresentation ? "TO DESTINATION"
            : $"{TimeLabel(mission.routes[selectedRoute].etaMinutes * (1 - routeProgress))} EST. LEFT";
        internal string QuestVisorProgress => demoPresentation
            ? $"DEMO {DemoCountdown}{(DemoEnded ? "" : " LEFT")}   ·   {routeProgress:P1} OF ROUTE"
            : $"{routeProgress:P1} OF ROUTE  ·  {currentSlopeDegrees:0.0}° SURFACE";
        internal string QuestVisorReserves => $"O₂  {oxygen:0.0}%   ·   PWR  {power:0.0}%";
        internal string QuestVisorEffort => $"{electricalWatts:0} W SUIT   ·   {oxygenFlowGramsPerHour:0} g O₂/h";
        internal string QuestVisorAction => arrived ? "A   REPLAY" : reserveStop ? "RESERVE STOP"
            : DemoEnded ? "A   CONTINUE ROUTE" : QuestFacingBlocked ? "FACE ROUTE TO WALK"
            : paused ? "RT   HOLD TO WALK" : "RT   RELEASE TO STOP";
        internal string QuestVisorWarning => TryGetCompactWarning(out string title, out string detail, out _)
            ? $"{title}\n{detail}" : string.Empty;

        internal string QuestLiveDetails()
        {
            GeoPoint point = JourneyLocation;
            MarsRouteFacts facts = QuestRouteFacts();
            float grid = mission.generatedRegion ? MarsRegionalTerrain.SourceSpacingMeters(point) : 20f;
            string image = MarsOfficialTerrain.TryActiveMatchingImage(point, out _, out float spacing)
                ? $"   ·   IMAGE {spacing:0.##} m" : string.Empty;
            return $"{CoordinateShort(point)}\n{DataTag}\n" +
                   $"HEIGHT {MarsRegionalTerrain.MeasuredElevation(point):0} m   ·   GRID {grid:0.#} m\n" +
                   $"GRADE {currentDemSlopeDegrees:0.0}°   ·   MAX {facts.maxGradeDegrees:0.0}°{image}\n" +
                   $"ROUTE ASCENT {facts.ascentMeters:0} m   ·   DESCENT {facts.descentMeters:0} m\n" +
                   $"BODY {metabolicWatts:0} W / SUIT {electricalWatts:0} W\nCONCEPT SUIT · 0.8 kg O₂ · 447 Wh";
        }

        private void UpdateQuestFacing()
        {
            if (!missionStarted || arrived || DemoEnded || reserveStop || helmetPreview)
            {
                questRouteFacing.Reset();
                return;
            }
            bool hasHeading = cameraRig.TryQuestRouteFacing(out float error);
            questRouteFacing.Update(hasHeading, error);
        }

        internal void SetQuestWalkInput(bool held, bool trackingReady)
        {
            if (!missionStarted) return;
            if (!trackingReady || questDispatchedMission && missionRadio?.RemoteMirrorReady != true)
            {
                PauseQuestTraverse();
                return;
            }
            if (!held) questWaitForTriggerRelease = false;
            bool shouldPause = !held || questWaitForTriggerRelease || arrived || DemoEnded || reserveStop || helmetPreview ||
                QuestRadioSetupVisible || QuestFacingBlocked;
            if (paused == shouldPause) return;
            paused = shouldPause;
            if (Application.platform == RuntimePlatform.Android)
                Debug.Log($"MARSPATH_XR: traverse {(paused ? "paused" : "started")} at {routeProgress:P1}");
        }

        internal void PauseQuestTraverse()
        {
            if (!missionStarted) return;
            questWaitForTriggerRelease = true;
            demoLastTick = null;
            paused = true;
        }

        private float DistanceRemainingKm => mission.routes[selectedRoute].distanceKm * (1 - routeProgress);
    }
}

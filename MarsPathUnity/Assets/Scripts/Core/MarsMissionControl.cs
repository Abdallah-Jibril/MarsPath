using System;
using System.Collections;
using System.Text;
using UnityEngine;
using static MarsPath.MarsMissionRadio.RouteDelivery;

namespace MarsPath
{
    public partial class MarsPathDemoBootstrap
    {
        [Serializable]
        internal sealed class DispatchedPlan
        {
            public MissionData mission;
            public int selectedRoute;
            public bool demo;

            internal byte[] Encode() => Encoding.UTF8.GetBytes(JsonUtility.ToJson(this));

            internal static DispatchedPlan Decode(byte[] payload)
            {
                var plan = JsonUtility.FromJson<DispatchedPlan>(Encoding.UTF8.GetString(payload));
                if (plan == null || !plan.IsValid())
                    throw new ArgumentException("The received mission plan is invalid.");
                return plan;
            }

            private bool IsValid()
            {
                if (mission?.routes == null || selectedRoute < 0 || selectedRoute >= mission.routes.Length ||
                    mission.routes.Length > 5 || mission.routes[selectedRoute]?.available != true ||
                    !ValidCoordinates(mission.currentLocation) || !ValidCoordinates(mission.destination) ||
                    mission.regionCenter != null && !ValidCoordinates(mission.regionCenter) ||
                    !Finite(mission.regionSpanKm) || mission.regionSpanKm < 0 ||
                    mission.generatedRegion && mission.regionSpanKm == 0 ||
                    mission.telemetry == null || !Finite(mission.telemetry.oxygenPercent) ||
                    !Finite(mission.telemetry.suitPowerPercent) || !Finite(mission.telemetry.heartRateBpm) ||
                    mission.hazards == null || mission.sciencePoints == null)
                    return false;
                foreach (RouteData route in mission.routes)
                {
                    if (route?.waypoints == null || route.waypoints.Length < 2 ||
                        !Finite(route.distanceKm) || !Finite(route.etaMinutes) ||
                        route.available && (route.distanceKm <= 0 || route.etaMinutes <= 0)) return false;
                    foreach (RoutePoint point in route.waypoints)
                        if (point == null || !Finite(point.x) || !Finite(point.z) ||
                            Mathf.Abs(point.x) > 1.01f || Mathf.Abs(point.z) > 1.01f) return false;
                }
                return true;
            }

            private static bool ValidCoordinates(GeoPoint point) => point != null && Finite(point.latitude) &&
                Finite(point.longitude) && Mathf.Abs(point.latitude) <= 90 && point.longitude >= -180 && point.longitude <= 360;
            private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
        }

        private bool questMissionControl, questDispatchLoading, questDispatchedMission;
        private string questDispatchError, dispatchRouteName, dispatchError;
        private GUIStyle plannerRadioActivity;
        private DispatchedPlan pendingDesktopPlan;
        private MissionData pendingDesktopSource;
        private Guid pendingDesktopSession;
        private double dispatchBegan;
        private bool dispatchReceiptLogged;

        internal bool QuestMissionControlMode => questMissionControl;
        internal bool QuestDispatchLoading => questDispatchLoading;
        internal bool QuestDispatchedMission => questDispatchedMission;
        internal string QuestWaitingDetail => questDispatchLoading ? (helmetEntryLoading
            ? $"PREPARING TERRAIN  {helmetEntryPercent:P0}" : "LOADING THE SELECTED ROUTE")
            : questDispatchError ?? MissionRadioActivity ?? QuestRadioMicStatus ?? QuestRadioStatus;

        private bool DispatchFromPlanner => Application.platform != RuntimePlatform.Android;
        private bool PlanningDispatchPending => pendingDesktopPlan != null || missionRadio?.DispatchPending == true;

        private string PlannerDispatchDetail => dispatchError ?? (missionRadio?.DispatchStatus switch
        {
            Waiting => "WAITING FOR QUEST · OPEN MARSPATH",
            Sending => $"SENDING {dispatchRouteName}",
            Preparing => $"QUEST IS PREPARING {dispatchRouteName}",
            Started => $"QUEST STARTED {dispatchRouteName}",
            Busy => "QUEST IS BUSY · RETURN TO MISSION CONTROL",
            Failed => "QUEST COULD NOT LOAD ROUTE · TRY AGAIN",
            TimedOut => "QUEST DID NOT CONFIRM · CHECK THE HEADSET",
            _ => null
        });

        internal void SetQuestMissionControl(bool enabled)
        {
            if (questDispatchLoading || GlobalView.IsBusy || !GlobalView.IsVisible) return;
            if (enabled && GlobalView.LocalZoneVisible) GlobalView.CloseMissionZone();
            questDispatchError = null;
            questMissionControl = enabled;
        }

        private void EnterHelmetFromPlanner()
        {
            if (desktopMissionMirror) return;
            if (!DispatchFromPlanner)
            {
                BeginHelmetEntry(false);
                return;
            }
            if (missionRadio == null)
            {
                dispatchError = "STARTING THE QUEST LINK · TRY AGAIN";
                return;
            }
            if (PlanningDispatchPending || helmetEntryLoading || !mission.routes[selectedRoute].available) return;
            if (missionRadio.QuestReadyKnown && !missionRadio.QuestWaiting)
            {
                dispatchError = "ON QUEST, OPEN MISSION CONTROL TO RECEIVE";
                return;
            }
            dispatchError = null;
            double began = Time.realtimeSinceStartupAsDouble;
            var plan = new DispatchedPlan { mission = mission, selectedRoute = selectedRoute, demo = demoPresentation };
            byte[] encoded = plan.Encode();
            dispatchRouteName = mission.routes[selectedRoute].displayName.ToUpperInvariant();
            if (missionRadio.QueueRoute(encoded))
            {
                pendingDesktopPlan = DispatchedPlan.Decode(encoded);
                pendingDesktopSource = mission;
                pendingDesktopSession = missionRadio.DispatchId;
                dispatchBegan = began;
                dispatchReceiptLogged = false;
                Debug.Log($"MARSPATH_CONTROL: dispatch {mission.routes[selectedRoute].id}, demo={demoPresentation}, {encoded.Length} bytes");
            }
            else dispatchError = "COULD NOT SEND ROUTE · TRY AGAIN";
        }

        private void TickDesktopDispatch()
        {
            if (pendingDesktopPlan == null || missionRadio == null) return;
            if (missionRadio.DispatchId != pendingDesktopSession)
            {
                pendingDesktopPlan = null;
                pendingDesktopSource = null;
                dispatchError = "DISPATCH CHANGED · SELECT THE ROUTE AGAIN";
                return;
            }
            if (missionRadio.DispatchStatus != Started)
            {
                if (!dispatchReceiptLogged && missionRadio.DispatchStatus == Preparing)
                {
                    dispatchReceiptLogged = true;
                    Debug.Log($"MARSPATH_DISPATCH: Quest received plan in {Time.realtimeSinceStartupAsDouble - dispatchBegan:F2}s round trip");
                }
                if (!missionRadio.DispatchPending)
                {
                    pendingDesktopPlan = null;
                    pendingDesktopSource = null;
                }
                return;
            }
            DispatchedPlan plan = pendingDesktopPlan;
            Guid session = pendingDesktopSession;
            bool reusePlanner = MarsMissionZone.IsMission(plan.mission) &&
                ReferenceEquals(mission, pendingDesktopSource) && !missionStarted &&
                !planningSceneDeferred && terrainRoot != null && routesRoot != null;
            pendingDesktopPlan = null;
            pendingDesktopSource = null;
            try
            {
                // The measured Jezero plan is already displayed and cannot refine
                // in the background. Replay the frozen snapshot if the plan changed.
                if (!reusePlanner) LoadRegionalMission(plan.mission);
                SelectRoute(plan.selectedRoute);
                demoPresentation = plan.demo;
                Debug.Log($"MARSPATH_DISPATCH: Quest acknowledged in {Time.realtimeSinceStartupAsDouble - dispatchBegan:F2}s, reused planner={reusePlanner}");
                BeginDesktopMirror(session);
            }
            catch (Exception exception)
            {
                dispatchError = "LOCAL VIEW COULD NOT PREPARE · TRY AGAIN";
                Debug.LogWarning("MARSPATH_CONTROL: desktop preparation failed: " + exception.Message);
            }
        }

        private void DrawPlanningRadio()
        {
            if (missionRadio == null) return;
            string connection = missionRadio.QuestWaiting ? "QUEST WAITING"
                : missionRadio.Connected ? "QUEST CONNECTED" : missionRadio.CanReachQuest ? "CONNECTING QUEST" : "WAITING FOR QUEST";
            Color color = MissionRadioAccent;
            UiFill(new Rect(1317, 576, 240, 50), new Color(.025f, .055f, .070f, .96f));
            UiFill(new Rect(1317, 576, 2, 50), color);
            GUI.Label(new Rect(1329, 581, 216, 17), connection, planCaption);
            plannerRadioActivity ??= new GUIStyle(planLabel) { fontSize = 15 };
            plannerRadioActivity.normal.textColor = color;
            GUI.Label(new Rect(1329, 597, 216, 22), MissionRadioActivity ??
                missionRadio.MicrophoneStatus ?? (missionRadio.Connected ? "Y  HOLD TO TALK" : missionRadio.LinkStatus),
                plannerRadioActivity);
        }

        private void AdvertiseQuestWaiting(bool focused)
        {
            missionRadio.SetRouteWaiting(focused && questMissionControl && GlobalView.IsVisible &&
                !questDispatchLoading && !GlobalView.IsBusy);
        }

        private IEnumerator LoadDispatchedPlan(Guid id, byte[] payload)
        {
            double loadBegan = Time.realtimeSinceStartupAsDouble;
            questDispatchLoading = true;
            questLiveSession = id;
            liveSequence = 0;
            questDispatchError = null;
            missionRadio.ReportRoute(id, Preparing);
            bool loaded = false;
            try
            {
                yield return null;
                try
                {
                    DispatchedPlan plan = DispatchedPlan.Decode(payload);
                    CancelTerrainRefinement();
                    bool measuredZone = MarsMissionZone.IsMission(plan.mission);
                    if (measuredZone && !MarsMissionZone.Available)
                        throw new InvalidOperationException("The measured Jezero dataset is missing.");
                    RestoreLocalRegionSources(plan.mission.currentLocation, plan.mission.destination, terrainOnly: measuredZone);
                    GlobalView.SetEndpoints(plan.mission.currentLocation, plan.mission.destination);
                    LoadRegionalMissionData(plan.mission, measuredZone && plan.mission.generatedRegion);
                    SelectRoute(plan.selectedRoute);
                    demoPresentation = plan.demo;
                    inspectProgress = 0;
                    if (measuredZone)
                        Debug.Log($"MARSPATH_DISPATCH: Quest plan initialized in {Time.realtimeSinceStartupAsDouble - loadBegan:F2}s");
                    loaded = true;
                }
                catch (Exception exception)
                {
                    Debug.LogWarning("MARSPATH_CONTROL: route preparation failed: " + exception.Message);
                }

                if (loaded)
                {
                    IEnumerator entry = PrepareHelmetEntry(false, true);
                    while (true)
                    {
                        object wait;
                        try
                        {
                            if (!entry.MoveNext()) break;
                            wait = entry.Current;
                        }
                        catch (Exception exception)
                        {
                            Debug.LogWarning("MARSPATH_CONTROL: helmet entry failed: " + exception.Message);
                            break;
                        }
                        yield return wait;
                    }
                }
            }
            finally
            {
                questDispatchLoading = false;
                helmetEntryLoading = false;
            }

            if (questDispatchedMission && missionStarted && !GlobalView.IsVisible)
            {
                PauseQuestTraverse();
                missionRadio.ReportRoute(id, Started);
                Debug.Log($"MARSPATH_DISPATCH: Quest terrain ready in {Time.realtimeSinceStartupAsDouble - loadBegan:F2}s, deferred planner={planningSceneDeferred}");
                Debug.Log($"MARSPATH_CONTROL: Quest entered {mission.routes[selectedRoute].id}, demo={demoPresentation}, " +
                    $"points={mission.routes[selectedRoute].waypoints.Length}, progress={routeProgress:P1}");
            }
            else
            {
                missionRadio.ReportRoute(id, Failed);
                questDispatchError = "ROUTE COULD NOT LOAD · ASK CONTROL TO RETRY";
                if (!GlobalView.IsVisible) ReturnToGlobalMap();
                if (GlobalView.LocalZoneVisible) GlobalView.CloseMissionZone();
            }
        }
    }
}

using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.InputSystem.XR;
using UnityEngine.UI;
using UnityEngine.XR;
using XRDevice = UnityEngine.XR.InputDevice;
using XRUsages = UnityEngine.XR.CommonUsages;

namespace MarsPath
{
    public sealed class MarsQuestHeadTracking : MonoBehaviour
    {
        private const float TrackingGraceSeconds = .75f;
        private const float StandingEyeHeight = 1.84f;
        private const float MaximumHeadDrop = .30f;
        private MarsPathDemoBootstrap mission;
        private MarsQuestPlanner planner;
        private GameObject visor, radioSetup;
        private Text distance, routeName, eta, heading, progress, reserves, effort, warning, traverseAction, details, radio;
        private Text radioStatus;
        private Image warningPlate, warningStripe, detailsPlate, radioPlate;
        private bool showDetails;
        private Vector3 anchorPosition;
        private Quaternion initialWorldHeading;
        private readonly MarsQuestPose pose = new MarsQuestPose();
        private bool hasMissionAnchor;
        private bool aHeld, backHeld, xHeld, yHeld;
        private float lastTrackedAt;
        private float nextStatusUpdate;

        internal Camera PlanningCamera => planner.MapCamera;
        internal bool WalkTrackingReady => Application.isFocused && hasMissionAnchor && pose.IsCalibrated &&
            Time.unscaledTime - lastTrackedAt <= TrackingGraceSeconds;
        internal void HidePlanner() => planner.SetVisible(false);

        public void Initialize(MarsPathDemoBootstrap owner, MarsPathCameraRig rig)
        {
            mission = owner;
            Camera headset = GetComponent<Camera>();
            planner = new MarsQuestPlanner(owner, rig, headset);
            var panel = new MarsQuestPanel(headset, "Quest visor HUD", gameObject.layer);
            visor = panel.Root;
            panel.Card("Distance card", new Rect(-410, 351, 286, 121), MarsQuestPanel.Background);
            panel.Plate("Distance accent", new Rect(-546, 351, 2, 100), MarsQuestPanel.Cyan);
            routeName = panel.Label("Selected route", new Rect(-410, 386, 264, 25), string.Empty, 19, MarsQuestPanel.Cyan, true);
            distance = panel.Label("Distance", new Rect(-410, 350, 264, 48), string.Empty, 35, Color.white, true);
            eta = panel.Label("Estimated time", new Rect(-410, 314, 264, 25), string.Empty, 18, MarsQuestPanel.Muted);
            panel.Card("Compass card", new Rect(0, 369, 332, 85), MarsQuestPanel.Background);
            heading = panel.Label("Route heading", new Rect(0, 387, 311, 34), string.Empty, 26, Color.white, true);
            progress = panel.Label("Route progress", new Rect(0, 351, 311, 26), string.Empty, 19, MarsQuestPanel.Muted);
            panel.Card("Suit card", new Rect(410, 351, 286, 121), MarsQuestPanel.Background);
            panel.Label("Suit telemetry caption", new Rect(410, 387, 264, 24), "SUIT  /  MODELED", 18, MarsQuestPanel.Cyan);
            reserves = panel.Label("Suit reserves", new Rect(410, 351, 264, 38), string.Empty, 23, Color.white, true);
            effort = panel.Label("Suit effort", new Rect(410, 316, 264, 24), string.Empty, 18, MarsQuestPanel.Muted);

            Color red = MarsPathDemoBootstrap.EvaWarningRed;
            warningPlate = panel.Card("Terrain warning card", new Rect(410, 234, 286, 77), new Color(.084f, .020f, .028f, .86f));
            warningStripe = panel.Plate("Terrain warning accent", new Rect(275, 234, 3, 56), red);
            warning = panel.Label("Terrain warning", new Rect(410, 234, 268, 69), string.Empty, 20, red, true);
            detailsPlate = panel.Card("Live data card", new Rect(-411, 108, 400, 200), MarsQuestPanel.Background);
            details = panel.Label("Live mission data", new Rect(-411, 108, 376, 184), string.Empty, 18, MarsQuestPanel.Muted);
            panel.Card("Traverse card", new Rect(0, -370, 286, 57), MarsQuestPanel.Button);
            traverseAction = panel.Label("Traverse action", new Rect(0, -370, 274, 42), string.Empty, 23, Color.white, true);
            panel.Card("Route planner card", new Rect(255, -370, 198, 57), MarsQuestPanel.Background);
            panel.Label("Return action", new Rect(255, -370, 186, 42), "B    ROUTES", 22, MarsQuestPanel.Muted, true);
            panel.Card("Live details card", new Rect(-255, -370, 198, 57), MarsQuestPanel.Background);
            panel.Label("Details action", new Rect(-255, -370, 186, 42), "X    DETAILS", 22, MarsQuestPanel.Muted, true);
            panel.Label("Radio action", new Rect(-255, -419, 198, 31), "Y    RADIO", 19, MarsQuestPanel.Muted, true);
            radioPlate = panel.Card("Radio indicator card", new Rect(0, -316, 432, 36), MarsQuestPanel.Background);
            radio = panel.Label("Mission Control radio status", new Rect(0, -316, 406, 32), string.Empty, 20, MarsQuestPanel.Cyan, true);
            radioPlate.enabled = false;

            var link = new MarsQuestPanel(headset, "Quest radio status", gameObject.layer);
            radioSetup = link.Root;
            link.Card("Radio card", new Rect(0, 0, 574, 212), MarsQuestPanel.Background);
            link.Label("Radio title", new Rect(0, 65, 536, 39), "MISSION CONTROL RADIO", 26, MarsQuestPanel.Cyan, true);
            radioStatus = link.Label("Connection status", new Rect(0, 17, 536, 34), string.Empty, 24, Color.white, true);
            link.Label("Radio instructions", new Rect(0, -28, 536, 31), "LEFT TRIGGER  ·  HOLD TO TALK", 21, MarsQuestPanel.Muted);
            link.Label("Close radio", new Rect(0, -68, 536, 34), "Y OR B    CLOSE RADIO", 19, MarsQuestPanel.Cyan, true);
            radioSetup.SetActive(false);
            visor.SetActive(false);
        }

        public void ClearAnchor()
        {
            hasMissionAnchor = false;
            pose.Reset();
            visor.SetActive(false);
            radioSetup.SetActive(false);
        }

        internal void SetGroundAnchor(Vector3 ground, Quaternion heading)
        {
            planner.SetVisible(false);
            anchorPosition = ground + Vector3.up * StandingEyeHeight;
            if (!hasMissionAnchor)
            {
                initialWorldHeading = heading;
                hasMissionAnchor = true;
                lastTrackedAt = Time.unscaledTime;
                nextStatusUpdate = 0;
                showDetails = false;
                visor.SetActive(true);
            }
            ApplyHeadPose();
        }

        private void OnEnable() => Application.onBeforeRender += ApplyHeadPose;
        private void OnDisable() => Application.onBeforeRender -= ApplyHeadPose;
        private void OnDestroy() => planner?.Dispose();

        private void OnApplicationFocus(bool focused)
        {
            if (focused || mission == null) return;
            pose.ResetPositionOrigin();
            mission.PauseQuestTraverse();
            mission.PauseQuestRadio();
        }

        private void OnApplicationPause(bool paused)
        {
            if (paused) OnApplicationFocus(false);
        }

        private void Update()
        {
            if (mission == null) return;
            bool a = MarsQuestControls.IsPressed(false, XRUsages.primaryButton);
            bool back = MarsQuestControls.IsPressed(false, XRUsages.secondaryButton);
            bool x = MarsQuestControls.IsPressed(true, XRUsages.primaryButton);
            bool y = MarsQuestControls.IsPressed(true, XRUsages.secondaryButton);
            bool replayPressed = a && !aHeld;
            bool backPressed = back && !backHeld;
            bool detailsPressed = x && !xHeld;
            bool radioPressed = y && !yHeld;
            aHeld = a;
            backHeld = back;
            xHeld = x;
            yHeld = y;
            planner.SetVisible(mission.QuestPlanningVisible);
            if (planner.Visible) planner.Update();
            if (!hasMissionAnchor) return;

            if (Application.isFocused)
            {
                if (radioPressed || backPressed && mission.QuestRadioSetupVisible)
                {
                    mission.ToggleQuestRadioSetup();
                    mission.PauseQuestTraverse();
                    nextStatusUpdate = 0;
                }
                else if (backPressed)
                {
                    Debug.Log("MARSPATH_XR: B pressed in visor");
                    mission.QuestReturnToRoutes();
                    return;
                }
                if (replayPressed && mission.QuestCanReplayOrContinue) mission.QuestReplay();
                if (detailsPressed)
                {
                    showDetails = !showDetails;
                    nextStatusUpdate = 0;
                }
            }
            UpdateRadioStatus();
            Color warningColor = MarsPathDemoBootstrap.EvaWarningRed;
            warningColor.a = MarsPathDemoBootstrap.EvaWarningOpacity(Time.unscaledTime);
            warning.color = warningStripe.color = warningColor;
            warningPlate.color = new Color(.084f, .020f, .028f, .86f * warningColor.a);
            if (Time.unscaledTime < nextStatusUpdate) return;
            distance.text = mission.QuestVisorStatus;
            routeName.text = mission.QuestVisorRoute;
            eta.text = mission.QuestVisorEta;
            heading.text = mission.QuestVisorHeading;
            progress.text = mission.QuestVisorProgress;
            reserves.text = mission.QuestVisorReserves;
            effort.text = mission.QuestVisorEffort;
            warning.text = mission.QuestVisorWarning;
            bool showWarning = !string.IsNullOrEmpty(warning.text);
            warningPlate.gameObject.SetActive(showWarning);
            warningStripe.gameObject.SetActive(showWarning);
            warning.gameObject.SetActive(showWarning);
            detailsPlate.gameObject.SetActive(showDetails);
            details.gameObject.SetActive(showDetails);
            if (showDetails) details.text = mission.QuestLiveDetails();
            traverseAction.text = WalkTrackingReady ? mission.QuestVisorAction : "WAITING FOR HEADSET";
            nextStatusUpdate = Time.unscaledTime + .2f;
        }

        private void UpdateRadioStatus()
        {
            string activity = mission.MissionRadioActivity;
            Color accent = mission.MissionRadioAccent;
            bool showingRadio = mission.QuestRadioSetupVisible;
            radio.text = activity != null ? activity + (mission.QuestRadioTalking ? "  ·  RELEASE TO STOP"
                : mission.MissionRadioReceiving ? "  ·  MISSION CONTROL" : string.Empty)
                : mission.QuestRadioMicStatus ?? (mission.QuestRadioConnected ? "LT  ·  HOLD TO TALK" : string.Empty);
            radio.color = accent;
            radio.enabled = radioPlate.enabled = !showingRadio && radio.text.Length > 0;
            radioSetup.SetActive(showingRadio);
            if (showingRadio)
            {
                radioStatus.text = activity ?? mission.QuestRadioMicStatus ?? mission.QuestRadioStatus;
                radioStatus.color = accent;
            }
        }

        [BeforeRenderOrder(100)]
        private void ApplyHeadPose()
        {
            if (planner != null && planner.Visible) planner.ApplyPose();
            if (!hasMissionAnchor || !Application.isFocused) return;
            if (pose.Apply(transform, anchorPosition, initialWorldHeading, anchorPosition.y - MaximumHeadDrop))
                lastTrackedAt = Time.unscaledTime;
            else if (!pose.IsCalibrated) transform.SetPositionAndRotation(anchorPosition, initialWorldHeading);
        }
    }

    internal sealed class MarsQuestPose
    {
        private Vector3 initialHeadPosition;
        private Vector3 lastHeadOffset;
        private Quaternion headsetToWorld;
        private bool hasPositionOrigin;
        public bool IsCalibrated { get; private set; }

        public void Reset()
        {
            IsCalibrated = false;
            ResetPositionOrigin();
        }

        public void ResetPositionOrigin()
        {
            hasPositionOrigin = false;
            lastHeadOffset = Vector3.zero;
        }

        public bool Apply(Transform target, Vector3 anchor, Quaternion heading, float minimumEyeY = float.NegativeInfinity)
        {
            if (!TryReadHead(out Quaternion rotation, out Vector3 position, out bool hasPosition)) return false;
            if (!IsCalibrated)
            {
                Quaternion initialHeadYaw = Quaternion.Euler(0, rotation.eulerAngles.y, 0);
                headsetToWorld = heading * Quaternion.Inverse(initialHeadYaw);
                IsCalibrated = true;
            }
            if (hasPosition)
            {
                if (!hasPositionOrigin)
                {
                    initialHeadPosition = position;
                    hasPositionOrigin = true;
                }
                lastHeadOffset = headsetToWorld * (position - initialHeadPosition);
            }
            Vector3 eyePosition = anchor + lastHeadOffset;
            eyePosition.y = Mathf.Max(eyePosition.y, minimumEyeY);
            target.SetPositionAndRotation(eyePosition, headsetToWorld * rotation);
            return true;
        }

        public bool TryRightHandRay(Vector3 anchor, out Ray ray)
        {
            if (hasPositionOrigin && MarsQuestControls.TryReadRightPointer(out Vector3 position, out Quaternion rotation))
            {
                ray = new Ray(anchor + headsetToWorld * (position - initialHeadPosition),
                    headsetToWorld * (rotation * Vector3.forward));
                return true;
            }
            ray = default;
            return false;
        }

        private static bool TryReadHead(out Quaternion rotation, out Vector3 position, out bool hasPosition)
        {
            var device = InputDevices.GetDeviceAtXRNode(XRNode.CenterEye);
            if (!device.isValid) device = InputDevices.GetDeviceAtXRNode(XRNode.Head);
            bool tracked = !device.TryGetFeatureValue(XRUsages.isTracked, out bool isTracked) || isTracked;
            if (tracked && (device.TryGetFeatureValue(XRUsages.centerEyeRotation, out rotation) ||
                device.TryGetFeatureValue(XRUsages.deviceRotation, out rotation)))
            {
                hasPosition = device.TryGetFeatureValue(XRUsages.centerEyePosition, out position) ||
                    device.TryGetFeatureValue(XRUsages.devicePosition, out position);
                return true;
            }

            var headset = InputSystem.GetDevice<XRHMD>();
            if (headset != null && headset.isTracked.isPressed)
            {
                var tracking = (InputTrackingState)headset.trackingState.ReadValue();
                if ((tracking & InputTrackingState.Rotation) != 0)
                {
                    rotation = headset.centerEyeRotation.ReadValue();
                    position = headset.centerEyePosition.ReadValue();
                    hasPosition = (tracking & InputTrackingState.Position) != 0;
                    return true;
                }
            }

            rotation = Quaternion.identity;
            position = Vector3.zero;
            hasPosition = false;
            return false;
        }
    }

    internal static class MarsQuestControls
    {
        private static XRDevice Controller(bool left) => InputDevices.GetDeviceAtXRNode(left ? XRNode.LeftHand : XRNode.RightHand);
        private static XRController InputSystemController(bool left) => left ? XRController.leftHand : XRController.rightHand;

        public static string ControllerLayout(bool left) => InputSystemController(left)?.layout ?? (Controller(left).isValid ? "XR" : "none");

        public static bool IsPressed(bool left, InputFeatureUsage<bool> feature, string binding = null)
        {
            var control = InputSystemController(left)?.TryGetChildControl(binding ?? feature.name) as ButtonControl;
            return control != null ? control.isPressed : Controller(left).TryGetFeatureValue(feature, out bool pressed) && pressed;
        }

        public static bool RightTriggerPressed() => TriggerPressed(false);
        public static bool LeftTriggerPressed() => TriggerPressed(true);

        private static bool TriggerPressed(bool left) =>
            IsPressed(left, XRUsages.triggerButton, "triggerPressed") ||
            (InputSystemController(left)?.TryGetChildControl("trigger") as AxisControl)?.ReadValue() > .65f ||
            Controller(left).TryGetFeatureValue(XRUsages.trigger, out float pressure) && pressure > .65f;

        public static Vector2 Stick(bool left)
        {
            var stick = InputSystemController(left)?.TryGetChildControl("thumbstick") as Vector2Control;
            if (stick != null) return stick.ReadValue();
            return Controller(left).TryGetFeatureValue(XRUsages.primary2DAxis, out Vector2 value) ? value : Vector2.zero;
        }

        public static bool TryReadRightPointer(out Vector3 position, out Quaternion rotation)
        {
            // OpenXR and Input System can supply different PoseControl types.
            // Their position, rotation and tracking child controls are shared.
            var pointer = InputSystemController(false)?.TryGetChildControl("pointer");
            const InputTrackingState required = InputTrackingState.Position | InputTrackingState.Rotation;
            if (pointer?.TryGetChildControl("isTracked") is ButtonControl tracked && tracked.isPressed &&
                pointer.TryGetChildControl("trackingState") is IntegerControl state &&
                ((InputTrackingState)state.ReadValue() & required) == required &&
                pointer.TryGetChildControl("position") is Vector3Control origin &&
                pointer.TryGetChildControl("rotation") is QuaternionControl direction)
            {
                position = origin.ReadValue();
                rotation = direction.ReadValue();
                return true;
            }
            position = default;
            rotation = Quaternion.identity;
            return false;
        }
    }
}

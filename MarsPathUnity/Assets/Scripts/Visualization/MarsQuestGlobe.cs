using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR;

namespace MarsPath
{
    public partial class MarsGlobalView
    {
        private MarsQuestGlobe questGlobe;

        private void InitializeQuestGlobe(Transform planet, Transform arc)
        {
            spaceCamera.enabled = false;
            questGlobe = globeCamera.gameObject.AddComponent<MarsQuestGlobe>();
            questGlobe.Initialize(this, GetComponent<MarsPathDemoBootstrap>(), globeCamera, planet, arc);
        }
    }

    public sealed class MarsQuestGlobe : MonoBehaviour
    {
        private const float SurfaceDistance = 2.4f;

        private readonly MarsQuestPose pose = new MarsQuestPose();
        private MarsGlobalView globe;
        private MarsPathDemoBootstrap mission;
        private Transform sphere;
        private Transform startMarker;
        private Transform endMarker;
        private Text title;
        private Text aim;
        private Text guidance;
        private Text startCoordinates;
        private Text endCoordinates;
        private Text status;
        private Image demoButton;
        private Image planButton;
        private Image zoneButton;
        private Image startCard, endCard;
        private Text zoneButtonLabel, zoneStart, zoneEnd;
        private RawImage zoneMap;
        private Text zoneAim;
        private GameObject plannerOverlay, waitingOverlay;
        private Text waitingTitle, waitingStatus, waitingHint;
        private Rect zoneView = new Rect(0, 0, 1, 1);
        private float zoneZoom = 1f;
        private Vector2 zoneCenter = new Vector2(.5f, .5f);
        private bool showedZone;
        private MarsQuestPointer pointer;
        private LineRenderer previewLine;
        private float latitude;
        private float longitude;
        private float radius = 1f;
        private float nextTextUpdate;
        private bool selectingDestination;
        private bool triggerHeld;
        private bool aHeld;
        private bool xHeld;
        private bool yHeld;
        private bool bHeld;
        private string leftController;
        private string rightController;

        public void Initialize(MarsGlobalView owner, MarsPathDemoBootstrap bootstrap, Camera camera, Transform planet, Transform arc)
        {
            globe = owner;
            mission = bootstrap;
            sphere = new GameObject("Quest globe navigation").transform;
            sphere.SetParent(planet.parent, false);
            planet.SetParent(sphere, false);
            arc.SetParent(sphere, false);
            previewLine = arc.GetComponent<LineRenderer>();

            camera.rect = new Rect(0, 0, 1, 1);
            camera.farClipPlane = 2000;
            camera.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            int layer = planet.gameObject.layer;
            startMarker = CreateMarker("Start", layer, MarsQuestPanel.Cyan);
            endMarker = CreateMarker("Destination", layer, MarsQuestPanel.Amber);
            CreateOverlay(camera, layer);

            pointer = new MarsQuestPointer(sphere.parent, layer);
            ApplySphereTransform();
        }

        private void CreateOverlay(Camera camera, int layer)
        {
            var panel = new MarsQuestPanel(camera, "Quest globe HUD", layer, foreground: true);
            plannerOverlay = panel.Root;
            title = panel.Label("Title", new Rect(0, 486, 950, 56), "MARSPATH  /  GLOBAL MARS", 37, Color.white, true);
            startCard = panel.Card("Start card", new Rect(-217, 413, 410, 62), MarsQuestPanel.Background);
            endCard = panel.Card("Destination card", new Rect(217, 413, 410, 62), MarsQuestPanel.Background);
            panel.Label("Start caption", new Rect(-217, 427, 380, 22), "START", 19, MarsQuestPanel.Cyan, true);
            panel.Label("Destination caption", new Rect(217, 427, 380, 22), "DESTINATION", 19, MarsQuestPanel.Amber, true);
            startCoordinates = panel.Label("Start coordinates", new Rect(-217, 401, 380, 27), string.Empty, 23, Color.white);
            endCoordinates = panel.Label("Destination coordinates", new Rect(217, 401, 380, 27), string.Empty, 23, Color.white);
            zoneMap = panel.Map("Measured Jezero zone map", new Rect(0, 0, 720, 720), null);
            zoneMap.color = MarsMissionZone.MapTint;
            zoneStart = panel.Label("Zone start pin", new Rect(0, 0, 44, 38), "● S", 24, MarsQuestPanel.Cyan, true);
            zoneEnd = panel.Label("Zone destination pin", new Rect(0, 0, 44, 38), "● D", 24, MarsQuestPanel.Amber, true);
            zoneMap.gameObject.SetActive(false);
            zoneStart.enabled = zoneEnd.enabled = false;
            aim = panel.Label("Gaze reticle", new Rect(0, 0, 56, 56), "+", 35, MarsQuestPanel.Cyan, true);
            zoneAim = panel.Label("Zone endpoint preview", new Rect(0, 0, 56, 56), "+", 40, MarsQuestPanel.Cyan, true);
            zoneAim.enabled = false;
            guidance = panel.Label("Set endpoint", new Rect(0, -428, 740, 37), string.Empty, 24, MarsQuestPanel.Cyan, true);

            zoneButton = panel.Card("Jezero zone button", new Rect(-319, -484, 302, 63), MarsQuestPanel.Button);
            zoneButtonLabel = panel.Label("Jezero zone label", new Rect(-319, -484, 288, 41), "JEZERO 10 × 10 KM", 22, Color.white, true);
            demoButton = panel.Card("Jezero demo button", new Rect(0, -484, 302, 63), MarsQuestPanel.Amber);
            panel.Label("Jezero demo shortcut", new Rect(0, -484, 288, 41), "Y    JEZERO DEMO", 22, MarsQuestPanel.DarkInk, true);
            planButton = panel.Card("Plan route button", new Rect(319, -484, 302, 63), MarsQuestPanel.Button);
            panel.Label("Plan route shortcut", new Rect(319, -484, 288, 41), "X    VIEW ROUTES", 22, Color.white, true);
            status = panel.Label("Globe controls and status", new Rect(0, -548, 960, 39), string.Empty, 22, MarsQuestPanel.Muted);

            var waiting = new MarsQuestPanel(camera, "Quest Mission Control lobby", layer, foreground: true);
            waitingOverlay = waiting.Root;
            waiting.Label("Mission Control lobby title", new Rect(0, 464, 900, 56),
                "MARSPATH  /  ASTRONAUT", 36, Color.white, true);
            waiting.Card("Dispatch status card", new Rect(0, -455, 660, 169), MarsQuestPanel.Background);
            waitingTitle = waiting.Label("Dispatch status", new Rect(0, -404, 620, 36),
                "WAITING FOR MISSION CONTROL", 28, MarsQuestPanel.Cyan, true);
            waitingStatus = waiting.Label("Link status", new Rect(0, -454, 620, 37),
                string.Empty, 22, MarsQuestPanel.Muted);
            waitingHint = waiting.Label("Lobby controls", new Rect(0, -505, 620, 35),
                "", 20, Color.white);
            bool waitingForControl = mission != null && mission.QuestMissionControlMode;
            plannerOverlay.SetActive(!waitingForControl);
            waitingOverlay.SetActive(waitingForControl);
        }

        public void SetFocus(float latitudeDegrees, float longitudeDegrees)
        {
            latitude = latitudeDegrees;
            longitude = longitudeDegrees;
            ApplySphereTransform();
        }

        private void OnEnable()
        {
            Application.onBeforeRender += UpdatePose;
            ReadButtons(out aHeld, out triggerHeld, out xHeld, out yHeld, out bHeld);
        }
        private void OnDisable()
        {
            Application.onBeforeRender -= UpdatePose;
            pose.Reset();
        }

        [BeforeRenderOrder(100)]
        private void UpdatePose() => pose.Apply(transform, Vector3.zero, Quaternion.identity);

        public void UpdateNavigation()
        {
            bool tracked = pose.Apply(transform, Vector3.zero, Quaternion.identity);
            bool inZone = globe.LocalZoneVisible;
            bool ready = Application.isFocused && !globe.IsBusy;
            ReadButtons(out bool a, out bool trigger, out bool x, out bool y, out bool b);
            bool demoPressed = y && !yHeld;
            bool planPressed = x && !xHeld;
            bool switchPressed = a && !aHeld;
            bool triggerPressed = trigger && !triggerHeld;
            bool backPressed = b && !bHeld;
            aHeld = a;
            xHeld = x;
            yHeld = y;
            triggerHeld = trigger;
            bHeld = b;

            bool waitingForControl = mission.QuestMissionControlMode;
            plannerOverlay.SetActive(!waitingForControl);
            waitingOverlay.SetActive(waitingForControl);
            if (waitingForControl)
            {
                sphere.gameObject.SetActive(true);
                startMarker.gameObject.SetActive(false);
                endMarker.gameObject.SetActive(false);
                previewLine.enabled = false;
                pointer.Hide();
                waitingTitle.text = mission.QuestDispatchLoading ? "ROUTE RECEIVED"
                    : "WAITING FOR MISSION CONTROL";
                waitingStatus.text = mission.QuestWaitingDetail;
                waitingStatus.color = mission.MissionRadioAccent;
                waitingHint.text = mission.QuestDispatchLoading ? "OPENING THE HELMET AT THE ROUTE START"
                    : mission.QuestRadioConnected ? "LT  RADIO     ·     A  PLAN ON QUEST" : "A  PLAN ON QUEST";
                if (ready && switchPressed) mission.SetQuestMissionControl(false);
                return;
            }
            previewLine.enabled = true;
            if (ready && backPressed && !inZone)
            {
                mission.SetQuestMissionControl(true);
                return;
            }

            if (inZone != showedZone)
            {
                showedZone = inZone;
                sphere.gameObject.SetActive(!inZone);
                zoneMap.gameObject.SetActive(inZone);
                title.text = inZone ? "JEZERO 10 × 10 KM MISSION ZONE" : "MARSPATH  /  GLOBAL MARS";
                zoneButtonLabel.text = inZone ? "B    GLOBAL MARS" : "JEZERO 10 × 10 KM";
                if (inZone)
                {
                    zoneMap.texture = MarsMissionZone.Image;
                    zoneCenter = new Vector2(.5f, .5f);
                    zoneZoom = 1f;
                    MoveZoneMap(Vector2.zero, 0, 0);
                    selectingDestination = MarsMissionZone.BoundsContain(globe.StartLocation) && !MarsMissionZone.BoundsContain(globe.EndLocation);
                }
            }

            if (ready)
            {
                Vector2 rotation = MarsQuestControls.Stick(true);
                Vector2 zoom = MarsQuestControls.Stick(false);
                float delta = Mathf.Min(Time.unscaledDeltaTime, .05f);
                if (inZone) MoveZoneMap(rotation, zoom.y, delta);
                else if (rotation.sqrMagnitude > .01f || Mathf.Abs(zoom.y) > .1f)
                {
                    latitude = Mathf.Clamp(latitude + rotation.y * 85f * delta / radius, -89.9f, 89.9f);
                    longitude = MarsGeospatial.NormalizeLongitude(longitude + rotation.x * 105f * delta / radius);
                    radius = Mathf.Clamp(radius * Mathf.Exp(zoom.y * 2.1f * delta), 1f, 480f);
                    ApplySphereTransform();
                }
            }

            bool handRay = pose.TryRightHandRay(Vector3.zero, out Ray ray);
            if (!handRay) ray = new Ray(transform.position, transform.forward);
            float hitDistance = 0;
            bool canAim = ready && (handRay || tracked);
            bool overZone = canAim && MarsQuestPanel.Intersects(zoneButton, ray, out hitDistance);
            bool overDemo = canAim && !overZone && MarsQuestPanel.Intersects(demoButton, ray, out hitDistance);
            bool overPlan = canAim && !overZone && !overDemo && MarsQuestPanel.Intersects(planButton, ray, out hitDistance);
            Vector3 selected = default;
            Vector2 zonePick = default;
            bool overSurface = canAim && !overZone && !overDemo && !overPlan &&
                (inZone ? MarsQuestPanel.Intersects(zoneMap, ray, out hitDistance, out zonePick) : TryPick(ray, out selected, out hitDistance));
            zoneButton.color = overZone ? MarsQuestPanel.Hover : MarsQuestPanel.Button;
            demoButton.color = overDemo ? new Color(1f, .81f, .54f) : MarsQuestPanel.Amber;
            planButton.color = overPlan ? MarsQuestPanel.Hover : MarsQuestPanel.Button;
            // The foreground map hides the world-space laser. Draw its exact hit
            // in the same canvas, above the map, for controller and gaze aiming.
            zoneAim.enabled = inZone && overSurface;
            if (zoneAim.enabled)
            {
                zoneAim.rectTransform.anchoredPosition = zoneMap.rectTransform.anchoredPosition +
                    Vector2.Scale(zonePick - Vector2.one * .5f, zoneMap.rectTransform.sizeDelta);
                zoneAim.color = selectingDestination ? MarsQuestPanel.Amber : MarsQuestPanel.Cyan;
            }
            aim.enabled = !handRay && !(inZone && overSurface) && (overSurface || overZone || overDemo || overPlan);
            if (ready && handRay) pointer.Show(ray, hitDistance, overZone || overDemo || overPlan);
            else pointer.Hide();

            if (ready && (inZone && backPressed || triggerPressed && overZone))
            {
                if (inZone) globe.CloseMissionZone();
                else globe.OpenMissionZone();
            }
            else if (ready && (demoPressed || triggerPressed && overDemo))
            {
                Debug.Log("MARSPATH_XR: Jezero demo selected");
                mission.QuestPlanJezeroDemo();
            }
            else if (ready && (planPressed || triggerPressed && overPlan))
            {
                Debug.Log("MARSPATH_XR: plan route selected");
                mission.QuestPlanSelectedRoute();
            }
            else if (ready && switchPressed) selectingDestination = !selectingDestination;
            else if (triggerPressed && overSurface)
            {
                GeoPoint point;
                if (inZone)
                {
                    Vector2 sectorUv = Rect.NormalizedToPoint(zoneView, zonePick);
                    globe.TryZoneMapPick(sectorUv, out point);
                }
                else point = MarsGeospatial.FromUnitSphere(selected);
                if (point != null)
                {
                    Debug.Log(selectingDestination ? "MARSPATH_XR: destination pin placed" : "MARSPATH_XR: start pin placed");
                    mission.QuestSelectEndpoint(point, selectingDestination);
                    selectingDestination = !selectingDestination;
                }
            }

            UpdateMarker(startMarker, globe.StartLocation);
            UpdateMarker(endMarker, globe.EndLocation);
            PlaceZoneMarker(zoneStart, globe.StartLocation, inZone);
            PlaceZoneMarker(zoneEnd, globe.EndLocation, inZone);
            if (Time.unscaledTime < nextTextUpdate) return;
            nextTextUpdate = Time.unscaledTime + .13f;
            guidance.color = aim.color = selectingDestination ? MarsQuestPanel.Amber : MarsQuestPanel.Cyan;
            guidance.text = selectingDestination ? "RT  SET DESTINATION" : "RT  SET START";
            startCard.color = selectingDestination ? MarsQuestPanel.Background : MarsQuestPanel.Hover;
            endCard.color = selectingDestination ? MarsQuestPanel.Hover : MarsQuestPanel.Background;
            startCoordinates.text = Coordinates(globe.StartLocation);
            endCoordinates.text = Coordinates(globe.EndLocation);
            status.text = mission.QuestLobbyStatus ??
                (inZone ? "L STICK  PAN  ·  R STICK  ZOOM  ·  A  SWITCH PIN"
                    : "L STICK  ROTATE  ·  R STICK  ZOOM  ·  A  SWITCH PIN  ·  B  MISSION CONTROL");
            ReportControllerChange();
        }

        private void MoveZoneMap(Vector2 pan, float zoom, float delta)
        {
            zoneZoom = Mathf.Clamp(zoneZoom * Mathf.Exp(zoom * delta * 1.7f), 1f, 6f);
            float half = .5f / zoneZoom;
            zoneCenter += pan * (.65f * delta / zoneZoom);
            zoneCenter.x = Mathf.Clamp(zoneCenter.x, half, 1f - half);
            zoneCenter.y = Mathf.Clamp(zoneCenter.y, half, 1f - half);
            zoneView = new Rect(zoneCenter - Vector2.one * half, Vector2.one * (2 * half));
            Rect source = MarsMissionZone.TextureUV;
            zoneMap.uvRect = new Rect(Rect.NormalizedToPoint(source, zoneView.min), Vector2.Scale(zoneView.size, source.size));
        }

        private void PlaceZoneMarker(Text label, GeoPoint point, bool inZone)
        {
            Vector2 normalized = MarsMissionZone.Normalized(point);
            label.enabled = inZone && MarsMissionZone.BoundsContain(point) && zoneView.Contains(normalized);
            if (label.enabled)
                label.rectTransform.anchoredPosition = Vector2.Scale(Rect.PointToNormalized(zoneView, normalized) -
                    Vector2.one * .5f, zoneMap.rectTransform.sizeDelta);
        }

        private static void ReadButtons(out bool a, out bool trigger, out bool x, out bool y, out bool b)
        {
            a = MarsQuestControls.IsPressed(false, CommonUsages.primaryButton);
            trigger = MarsQuestControls.RightTriggerPressed();
            x = MarsQuestControls.IsPressed(true, CommonUsages.primaryButton);
            y = MarsQuestControls.IsPressed(true, CommonUsages.secondaryButton);
            b = MarsQuestControls.IsPressed(false, CommonUsages.secondaryButton);
        }

        private void ReportControllerChange()
        {
            string left = MarsQuestControls.ControllerLayout(true);
            string right = MarsQuestControls.ControllerLayout(false);
            if (left == leftController && right == rightController) return;
            leftController = left;
            rightController = right;
            Debug.Log($"MARSPATH_XR: controllers L={left} R={right}");
        }

        private void ApplySphereTransform()
        {
            Vector3 front = MarsGeospatial.ToUnitSphere(latitude, longitude);
            sphere.localPosition = new Vector3(0, 0, radius + SurfaceDistance);
            sphere.localRotation = Quaternion.Inverse(Quaternion.LookRotation(-front, Vector3.up));
            sphere.localScale = Vector3.one * radius;
            UpdateArcHeight();
        }

        public void UpdateArcHeight()
        {
            previewLine.widthMultiplier = .008f;
            for (int index = 0; index < previewLine.positionCount; index++)
            {
                Vector3 sample = previewLine.GetPosition(index);
                previewLine.SetPosition(index, sample.normalized * (1 + .008f / radius));
            }
        }

        private bool TryPick(Ray ray, out Vector3 point, out float hitDistance)
        {
            // Intersect the displayed unit sphere in its own geographic coordinates.
            Vector3 origin = sphere.InverseTransformPoint(ray.origin);
            Vector3 direction = sphere.InverseTransformDirection(ray.direction);
            float b = Vector3.Dot(origin, direction);
            float discriminant = b * b - origin.sqrMagnitude + 1f;
            float distance = discriminant >= 0 ? -b - Mathf.Sqrt(discriminant) : -1f;
            point = origin + direction * distance;
            hitDistance = distance * radius;
            return distance > 0;
        }

        private Transform CreateMarker(string name, int layer, Color color)
        {
            var marker = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            marker.name = name + " pin";
            marker.layer = layer;
            marker.transform.SetParent(sphere, false);
            Destroy(marker.GetComponent<Collider>());
            Mesh mesh = marker.GetComponent<MeshFilter>().mesh;
            var vertexColors = new Color32[mesh.vertexCount];
            for (int index = 0; index < vertexColors.Length; index++) vertexColors[index] = Color.white;
            mesh.colors32 = vertexColors;
            marker.GetComponent<MeshRenderer>().sharedMaterial = MarsPathMaterialFactory.CreateRoute(color, marker.name);
            return marker.transform;
        }

        private void UpdateMarker(Transform marker, GeoPoint point)
        {
            marker.gameObject.SetActive(point != null);
            if (point == null) return;
            marker.localPosition = MarsGeospatial.ToUnitSphere(point.latitude, point.longitude) * (1 + .017f / radius);
            marker.localScale = Vector3.one * (.038f / radius);
        }

        private static string Coordinates(GeoPoint point) => point == null ? "--" : $"{point.latitude:0.000}°, {point.longitude:0.000}°";
    }
}

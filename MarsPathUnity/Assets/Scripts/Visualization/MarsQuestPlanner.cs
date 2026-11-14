using System;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR;

namespace MarsPath
{
    internal sealed class MarsQuestPlanner
    {
        private const int ConsoleLayer = 29;
        private const int GlobeLayer = 30;
        private const int RouteSlots = 5;
        private static readonly Color Frame = new Color(.020f, .044f, .059f);
        private static readonly Color Selected = new Color(.045f, .233f, .271f);
        private static readonly Color Inactive = new Color(.030f, .069f, .086f);

        private readonly MarsPathDemoBootstrap mission;
        private readonly MarsPathCameraRig rig;
        private readonly Camera headset;
        private readonly MarsQuestPanel panel;
        private readonly RenderTexture mapTexture;
        private readonly MarsQuestPose pose = new MarsQuestPose();
        private readonly MarsQuestPointer pointer;
        private readonly Image[] routeCards = new Image[RouteSlots];
        private readonly Image[] routeStripes = new Image[RouteSlots];
        private readonly Text[] routeNames = new Text[RouteSlots];
        private readonly Text[] routeEstimates = new Text[RouteSlots];
        private readonly Text region, source, endpoints, brief, message, detailsText, startLabel, endLabel;
        private readonly Image detailsCard, start, back, details, inspect, demo, full;
        private readonly RawImage map;
        private readonly Texture2D profile;
        private readonly Graphic[] actions;
        private readonly Graphic[] evidence;
        private MarsRouteFacts drawnFacts;
        private float refreshAt;
        private bool showDetails, triggerHeld, aHeld, bHeld, xHeld;
        private int savedCullingMask;
        private CameraClearFlags savedClearFlags;
        private Color savedBackground;

        public Camera MapCamera { get; }
        public bool Visible => panel.Root.activeSelf;

        public MarsQuestPlanner(MarsPathDemoBootstrap owner, MarsPathCameraRig rig, Camera headset)
        {
            mission = owner;
            this.rig = rig;
            this.headset = headset;
            mapTexture = new RenderTexture(1440, 810, 24, RenderTextureFormat.ARGB32)
            {
                name = "Quest regional terrain view",
                antiAliasing = 2,
                filterMode = FilterMode.Bilinear
            };
            MapCamera = new GameObject("Quest route map camera").AddComponent<Camera>();
            MapCamera.CopyFrom(headset);
            MapCamera.enabled = false;
            MapCamera.stereoTargetEye = StereoTargetEyeMask.None;
            MapCamera.targetTexture = mapTexture;
            MapCamera.aspect = (float)mapTexture.width / mapTexture.height;
            MapCamera.cullingMask &= ~((1 << ConsoleLayer) | (1 << GlobeLayer));
            MapCamera.fieldOfView = 58f;
            MarsCameraBackground.Apply(MapCamera, true);

            panel = new MarsQuestPanel(headset, "Quest regional route planner", ConsoleLayer);
            pointer = new MarsQuestPointer(panel.Root.transform, ConsoleLayer);
            panel.Plate("Planner frame", new Rect(0, 0, 1440, 1000), Frame);
            panel.Label("Mission control", new Rect(-519, 443, 338, 42), "MARSPATH", 36,
                Color.white, true, TextAnchor.MiddleLeft);
            region = panel.Label("Selected region", new Rect(-519, 390, 338, 55), string.Empty, 22,
                MarsQuestPanel.Muted, false, TextAnchor.MiddleLeft);
            region.resizeTextForBestFit = true;
            region.resizeTextMinSize = 18;
            region.resizeTextMaxSize = 22;
            panel.Label("Route comparison", new Rect(194, 445, 984, 43), "COMPARE YOUR ROUTES", 36, Color.white, true);
            source = panel.Label("Elevation and imagery", new Rect(194, 405, 984, 34), string.Empty, 21, MarsQuestPanel.Cyan);
            panel.Plate("Planner divider", new Rect(-322, 87, 2, 738), new Color(.11f, .22f, .26f));

            for (int i = 0; i < RouteSlots; i++)
            {
                float y = 312 - i * 87;
                routeCards[i] = panel.Card("Route card " + i, new Rect(-520, y, 342, 74), Inactive);
                routeStripes[i] = panel.Plate("Route color " + i, new Rect(-682, y, 4, 46), MarsQuestPanel.Cyan);
                routeNames[i] = panel.Label("Route name " + i, new Rect(-511, y + 14, 310, 30), string.Empty, 25,
                    Color.white, true, TextAnchor.MiddleLeft);
                routeEstimates[i] = panel.Label("Route estimate " + i, new Rect(-511, y - 16, 310, 26), string.Empty, 20,
                    MarsQuestPanel.Muted, false, TextAnchor.MiddleLeft);
            }

            map = panel.Map("Measured route map", new Rect(194, 99, 984, 553.5f), mapTexture);
            startLabel = panel.Label("Map start pin", new Rect(0, 0, 48, 29), "S", 22, MarsQuestPanel.Cyan, true);
            endLabel = panel.Label("Map destination pin", new Rect(0, 0, 48, 29), "D", 22, MarsQuestPanel.Amber, true);
            endpoints = panel.Label("Coordinates", new Rect(-520, -163, 332, 101), string.Empty, 20, MarsQuestPanel.Muted);
            panel.Card("Selected route summary card", new Rect(194, -219, 984, 66), Inactive);
            brief = panel.Label("Selected route facts", new Rect(194, -219, 962, 60), string.Empty, 25, Color.white, true);
            profile = new Texture2D(384, 72, TextureFormat.RGBA32, false) { name = "Selected route elevation profile" };
            panel.Map("Measured elevation profile", new Rect(194, -305, 984, 89), profile);
            message = panel.Label("Planner status", new Rect(198, -377, 975, 30), string.Empty, 20, MarsQuestPanel.Muted);
            back = Action("Back to endpoints", new Rect(-520, -438, 342, 60), "B    ENDPOINTS");
            details = Action("Route details", new Rect(-223, -438, 148, 60), "X   DETAILS");
            inspect = Action("Inspect terrain", new Rect(-62, -438, 150, 60), "INSPECT");
            demo = Action("Two minute demo", new Rect(106, -438, 160, 60), "2 MIN DEMO");
            full = Action("Full mission", new Rect(280, -438, 162, 60), "FULL MISSION");
            start = Action("Enter helmet", new Rect(518, -438, 284, 60), "A    ENTER HELMET", true);
            actions = new Graphic[] { back, details, inspect, demo, full, start };

            detailsCard = panel.Plate("Route details sheet", new Rect(194, 99, 984, 553.5f), Frame);
            var detailsTitle = panel.Label("Route data title", new Rect(194, 321, 865, 39), "ROUTE DATA", 31, MarsQuestPanel.Cyan, true);
            detailsText = panel.Label("Route data", new Rect(194, 80, 864, 407), string.Empty, 24, Color.white, false, TextAnchor.UpperLeft);
            detailsText.lineSpacing = 1.25f;
            evidence = new Graphic[] { detailsCard, detailsTitle, detailsText };
            panel.Root.SetActive(false);
        }

        private Image Action(string name, Rect layout, string label, bool primary = false)
        {
            Image card = panel.Card(name, layout, primary ? MarsQuestPanel.Amber : MarsQuestPanel.Button);
            panel.Label(name + " label", layout, label, 22, primary ? MarsQuestPanel.DarkInk : Color.white, true);
            return card;
        }

        public void SetVisible(bool visible)
        {
            if (visible == Visible) return;
            panel.Root.SetActive(visible);
            MapCamera.enabled = visible;
            pose.Reset();
            if (visible)
            {
                savedCullingMask = headset.cullingMask;
                savedClearFlags = headset.clearFlags;
                savedBackground = headset.backgroundColor;
                headset.cullingMask = 1 << ConsoleLayer;
                headset.clearFlags = CameraClearFlags.SolidColor;
                headset.backgroundColor = new Color(.008f, .016f, .024f);
                headset.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
                ReadButtons(out triggerHeld, out aHeld, out bHeld, out xHeld);
                refreshAt = 0;
                showDetails = false;
                ApplyPose();
                Refresh();
                Debug.Log("MARSPATH_XR: route comparison displayed");
            }
            else
            {
                headset.cullingMask = savedCullingMask;
                headset.clearFlags = savedClearFlags;
                headset.backgroundColor = savedBackground;
                pointer.Hide();
            }
        }

        public void ApplyPose() => pose.Apply(headset.transform, Vector3.zero, Quaternion.identity);

        public void Update()
        {
            ApplyPose();
            ReadButtons(out bool trigger, out bool a, out bool b, out bool x);
            bool activate = trigger && !triggerHeld, enter = a && !aHeld, leave = b && !bHeld, toggleDetails = x && !xHeld;
            triggerHeld = trigger;
            aHeld = a;
            bHeld = b;
            xHeld = x;
            int routeHit = -1, actionHit = -1;
            float hitDistance = 0;
            bool ready = Application.isFocused && mission.QuestPlanningStatus == null;
            bool hand = pose.TryRightHandRay(Vector3.zero, out Ray ray);
            if (!hand) ray = new Ray(headset.transform.position, headset.transform.forward);
            if (ready)
            {
                rig.MoveQuestPlanningView(MarsQuestControls.Stick(true), MarsQuestControls.Stick(false));
                RouteData[] routes = mission.ActiveMission.routes;
                for (int i = 0; i < Mathf.Min(RouteSlots, routes.Length); i++)
                {
                    if (!routes[i].available || !MarsQuestPanel.Intersects(routeCards[i], ray, out hitDistance)) continue;
                    routeHit = i;
                    break;
                }
                if (routeHit < 0)
                    for (int i = 0; i < actions.Length; i++)
                    {
                        if (!MarsQuestPanel.Intersects(actions[i], ray, out hitDistance)) continue;
                        actionHit = i;
                        break;
                    }
                bool hasHit = routeHit >= 0 || actionHit >= 0;
                if (hand || hasHit) pointer.Show(ray, hasHit ? hitDistance : 0, hasHit);
                else pointer.Hide();

                if (leave || activate && actionHit == 0) mission.QuestBackToEndpoints();
                else if (enter || activate && actionHit == 5) mission.QuestEnterSelectedRoute();
                else if (toggleDetails || activate && actionHit == 1) showDetails = !showDetails;
                else if (activate && actionHit == 2) rig.TogglePlanningInspection();
                else if (activate && actionHit == 3) mission.QuestSetDemoMode(true);
                else if (activate && actionHit == 4) mission.QuestSetDemoMode(false);
                else if (activate && routeHit >= 0) mission.QuestChooseRoute(routeHit);
                if (leave || enter || toggleDetails || activate) refreshAt = 0;
            }
            else pointer.Hide();

            if (!mission.QuestPlanningVisible) return;
            for (int i = 0; i < RouteSlots; i++)
                routeCards[i].color = i == routeHit ? MarsQuestPanel.Hover : i == mission.QuestRouteIndex ? Selected : Inactive;
            for (int i = 0; i < actions.Length; i++)
            {
                bool selected = i == 1 && showDetails || i == 2 && rig.PlanningInspectionActive ||
                    i == 3 && mission.QuestDemoMode || i == 4 && !mission.QuestDemoMode;
                actions[i].color = i == 5
                    ? i == actionHit ? new Color(1f, .81f, .54f) : MarsQuestPanel.Amber
                    : i == actionHit ? MarsQuestPanel.Hover : selected ? Selected : MarsQuestPanel.Button;
            }
            if (Time.unscaledTime >= refreshAt) Refresh();
            RefreshPins();
        }

        private void Refresh()
        {
            refreshAt = Time.unscaledTime + .20f;
            MissionData plan = mission.ActiveMission;
            RouteData selected = plan.routes[mission.QuestRouteIndex];
            region.text = plan.region;
            endpoints.text = $"START\n{Coordinate(plan.currentLocation)}\nDESTINATION\n{Coordinate(plan.destination)}";
            for (int i = 0; i < RouteSlots; i++)
            {
                RouteData route = i < plan.routes.Length ? plan.routes[i] : null;
                routeCards[i].gameObject.SetActive(route != null);
                routeStripes[i].gameObject.SetActive(route != null);
                routeNames[i].text = route == null ? string.Empty : route.displayName;
                routeEstimates[i].text = route == null ? string.Empty : route.available
                    ? $"{Distance(route.distanceKm)}  ·  {Minutes(route.etaMinutes)} est.{(route.recommended ? "  ·  Recommended" : "")}"
                    : "No feasible corridor";
                routeStripes[i].color = route == null ? Color.clear : MarsPathRouteView.ParseColor(route.colorHex, MarsQuestPanel.Cyan);
            }
            GeoPoint focus = MarsRegionalTerrain.ToGeo(rig.PlanningTarget.x, rig.PlanningTarget.z);
            float image = MarsOfficialTerrain.TryActiveMatchingImage(focus, out _, out float spacing) ? spacing : float.MaxValue;
            float height = MarsRegionalTerrain.SourceSpacingMeters(focus);
            string imageLabel = image < float.MaxValue ? $"IMAGE {image:0.##} m  ·  " : string.Empty;
            source.text = $"{imageLabel}HEIGHT GRID {height:0.#} m  ·  MESH {mission.QuestPlanningMesh:0.#} m";
            message.text = mission.QuestPlanningStatus ?? "L STICK  ORBIT  ·  R STICK  PAN / ZOOM  ·  RT  SELECT";
            foreach (Graphic element in evidence) element.gameObject.SetActive(showDetails);
            if (!selected.available)
            {
                brief.text = "NO FEASIBLE CORRIDOR";
                detailsText.text = string.Empty;
                return;
            }
            MarsRouteFacts facts = mission.QuestRouteFacts();
            brief.text = $"{Distance(facts.routeLengthKm)}  ·  {Minutes(facts.etaMinutes)} EST.\n" +
                $"+{facts.ascentMeters:0} / −{facts.descentMeters:0} m  ·  {facts.maxGradeDegrees:0.0}° MAX GRADE";
            if (mission.QuestDemoMode && mission.QuestPlanningStatus == null) message.text = mission.QuestDemoScope;
            if (drawnFacts != facts)
            {
                drawnFacts = facts;
                Plot(facts.profile.heights, facts.minimumElevationMeters, facts.maximumElevationMeters,
                    MarsPathRouteView.ParseColor(selected.colorHex, MarsQuestPanel.Cyan));
            }
            if (showDetails)
            {
                string dataSource = facts.sources.Length == 0 ? "Height source unavailable" :
                    $"{facts.sources[0].source}   ·   {facts.sources[0].percent:0}% of route";
                detailsText.text = $"{selected.displayName.ToUpperInvariant()}\n{selected.summary}\n\n" +
                    $"HEIGHT {facts.minimumElevationMeters:0} to {facts.maximumElevationMeters:0} m\n" +
                    $"SOURCE GRID {facts.finestGridMeters:0.##}–{facts.coarsestGridMeters:0.##} m   ·   {facts.stereoCoveragePercent:0}% stereo\n{dataSource}\n\n" +
                    $"TRAVERSABILITY {selected.safetyScore:0}   ·   ENERGY {selected.energyScore:0} / 100\n" +
                    $"MODELED O₂ {facts.estimatedOxygenGrams:0} g   ·   ENERGY {facts.estimatedEnergyKwh:0.00} kWh\n" +
                    $"CANDIDATE SCIENCE {facts.modeledScienceCandidates}   ·   HAZARD ZONES {facts.modeledHazardZones}";
            }
        }

        private void RefreshPins()
        {
            RoutePoint[] points = mission.ActiveMission.routes[mission.QuestRouteIndex].waypoints;
            PlacePin(startLabel, points != null && points.Length > 1 ? points[0] : null);
            PlacePin(endLabel, points != null && points.Length > 1 ? points[points.Length - 1] : null);
        }

        private void PlacePin(Text label, RoutePoint point)
        {
            if (point == null || showDetails) { label.enabled = false; return; }
            Vector3 viewport = MapCamera.WorldToViewportPoint(MarsTerrainGenerator.SurfacePoint(point.x * 60f, point.z * 60f, .15f));
            label.enabled = viewport.z > 0 && viewport.x > .02f && viewport.x < .98f && viewport.y > .02f && viewport.y < .96f;
            if (label.enabled)
                label.rectTransform.anchoredPosition = map.rectTransform.anchoredPosition +
                    Vector2.Scale(new Vector2(viewport.x - .5f, viewport.y - .5f), map.rectTransform.sizeDelta) + Vector2.up * 10;
        }

        private void Plot(float[] heights, float minimum, float maximum, Color color)
        {
            int width = profile.width, height = profile.height;
            var pixels = new Color32[width * height];
            Color32 background = new Color(.034f, .085f, .103f), fill = Color.Lerp(background, color, .19f), line = color;
            float relief = Mathf.Max(.01f, maximum - minimum);
            int previous = 4;
            for (int x = 0; x < width; x++)
            {
                float sample = x * (heights.Length - 1f) / (width - 1f);
                int index = Mathf.FloorToInt(sample);
                float value = Mathf.Lerp(heights[index], heights[Mathf.Min(index + 1, heights.Length - 1)], sample - index);
                int y = Mathf.Clamp(4 + Mathf.RoundToInt((value - minimum) / relief * (height - 9)), 0, height - 1);
                for (int row = 0; row < height; row++)
                    pixels[row * width + x] = row <= y ? fill : background;
                if (x == 0) previous = y;
                for (int row = Mathf.Min(previous, y); row <= Mathf.Max(previous, y); row++)
                    pixels[row * width + x] = line;
                previous = y;
            }
            profile.SetPixels32(pixels);
            profile.Apply();
        }

        public void Dispose()
        {
            UnityEngine.Object.Destroy(MapCamera.gameObject);
            UnityEngine.Object.Destroy(mapTexture);
            UnityEngine.Object.Destroy(profile);
        }

        private static void ReadButtons(out bool trigger, out bool a, out bool b, out bool x)
        {
            trigger = MarsQuestControls.RightTriggerPressed();
            a = MarsQuestControls.IsPressed(false, CommonUsages.primaryButton);
            b = MarsQuestControls.IsPressed(false, CommonUsages.secondaryButton);
            x = MarsQuestControls.IsPressed(true, CommonUsages.primaryButton);
        }

        private static string Coordinate(GeoPoint p) => $"{p.latitude:0.00000}°, {p.longitude:0.00000}°";
        private static string Distance(float km) => km < 1 ? $"{km * 1000:0} m" : $"{km:0.00} km";
        private static string Minutes(float minutes) => minutes < 60 ? $"{minutes:0} min" :
            minutes < 1440 ? $"{minutes / 60:0.0} h" : $"{minutes / 1440:0.0} days";
    }
}

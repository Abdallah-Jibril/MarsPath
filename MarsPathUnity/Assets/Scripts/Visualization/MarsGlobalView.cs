using UnityEngine;

namespace MarsPath
{
    /// <summary>Global location selection, kept separate from the local EVA simulation.</summary>
    public partial class MarsGlobalView : MonoBehaviour
    {
        private const int GlobeLayer = 30;
        private const float CanvasWidth = 1600f;
        private const float CanvasHeight = 900f;
        private static readonly Rect MapRect = new Rect(415, 105, 1160, 498);
        private static readonly Color Cyan = new Color(0.25f, 0.88f, 0.91f);
        private static readonly Color Muted = new Color(0.68f, 0.77f, 0.80f);
        private static readonly Color Orange = new Color(1f, 0.65f, 0.38f);

        private sealed class Landmark
        {
            public string name;
            public float lat;
            public float lon;
            public string description;
            public Landmark(string name, float lat, float lon, string description)
            { this.name = name; this.lat = lat; this.lon = lon; this.description = description; }
        }

        private static readonly Landmark[] Landmarks = BuildLandmarks();
        private static Landmark[] BuildLandmarks()
        {
            Landmark[] result = new Landmark[MarsSiteCatalog.Sites.Length];
            for (int i=0;i<result.Length;i++) { MarsSite site=MarsSiteCatalog.Sites[i]; result[i]=new Landmark(site.name,site.latitude,site.longitude,site.description); }
            return result;
        }

        public bool IsVisible { get; private set; }
        public GeoPoint StartLocation { get; private set; }
        public GeoPoint EndLocation { get; private set; }
        public bool HasBasemap => globeBasemap != null;
        private Camera localCamera;
        private MarsPathCameraRig localRig;
        private Camera globeCamera;
        private Camera spaceCamera;
        private GameObject globeRoot;
        private GameObject solarBackdrop;
        private Material globeMaterial;
        private Texture2D globeBasemap;
        private Material arcMaterial;
        private LineRenderer previewArc;
        private Mesh sphereMesh;
        private MissionData mission;
        private float latitude = 17f;
        private float longitude = 44f;
        private float targetLatitude = 17f;
        private float targetLongitude = 44f;
        private float distance = 3.5f;
        private int selectedLandmark;
        private bool selectEnd;
        private bool showGrid = true;
        private bool dragging;
        private Vector3 mouseDown;
        private float dragTravel;
        private string selectionMessage = "Select a landmark or place mission bookmarks on the globe.";
        private GUIStyle title, heading, body, small, badge, button, activeButton, panel;
        private Texture2D panelTexture, buttonTexture, activeTexture;
        private GUIStyle endpointLabel;
        private Texture2D endpointPillTexture,endpointPinTexture;

        public void Initialize(Camera camera, MarsPathCameraRig rig, MissionData sourceMission)
        {
            localCamera = camera;
            localRig = rig;
            mission = sourceMission;
            localCamera.cullingMask &= ~(1 << GlobeLayer);
            globeRoot = new GameObject("Global Mars Navigation");
            globeRoot.transform.SetParent(transform, false);
            GameObject planet = new GameObject("NASA Viking Mars Globe");
            planet.layer = GlobeLayer;
            planet.transform.SetParent(globeRoot.transform, false);
            sphereMesh = CreateSphere();
            planet.AddComponent<MeshFilter>().sharedMesh = sphereMesh;
            Shader shader = Resources.Load<Shader>("MarsPathGlobe");
            globeMaterial = new Material(shader) { name = "NASA Viking Global Basemap" };
            globeBasemap = Resources.Load<Texture2D>("NASA/MarsMdimColor8k")??Resources.Load<Texture2D>("NASA/MarsGlobalViking");
            globeMaterial.mainTexture = globeBasemap;
            planet.AddComponent<MeshRenderer>().sharedMaterial = globeMaterial;
            solarBackdrop = new GameObject("Solar System background") { layer = GlobeLayer };
            solarBackdrop.transform.SetParent(globeRoot.transform, false);
            solarBackdrop.AddComponent<MarsSolarSystemBackdrop>().Populate(sphereMesh, shader, GlobeLayer,
                Application.platform == RuntimePlatform.Android);

            GameObject cameraObject = new GameObject("Planet Navigation Camera");
            cameraObject.transform.SetParent(globeRoot.transform, false);
            globeCamera = cameraObject.AddComponent<Camera>();
            globeCamera.cullingMask = 1 << GlobeLayer;
            MarsCameraBackground.Apply(globeCamera,true);
            globeCamera.fieldOfView = 36f;
            globeCamera.nearClipPlane = 0.05f;
            globeCamera.farClipPlane = 70f;
            globeCamera.depth = 2;

            GameObject spaceObject=new GameObject("Full-page orbital star background");
            spaceObject.transform.SetParent(globeRoot.transform,false);
            spaceCamera=spaceObject.AddComponent<Camera>();
            spaceCamera.cullingMask=0;spaceCamera.depth=1;spaceCamera.fieldOfView=36;
            spaceCamera.nearClipPlane=0.05f;spaceCamera.farClipPlane=20;
            MarsCameraBackground.Apply(spaceCamera,true);

            GameObject arc = new GameObject("Geodesic Location Preview");
            arc.layer = GlobeLayer;
            arc.transform.SetParent(globeRoot.transform, false);
            previewArc = arc.AddComponent<LineRenderer>();
            arcMaterial = MarsPathMaterialFactory.CreateRoute(Cyan, "Global Location Preview");
            previewArc.sharedMaterial = arcMaterial;
            previewArc.widthMultiplier = 0.004f;
            previewArc.useWorldSpace = false;
            if (Application.platform == RuntimePlatform.Android)
                InitializeQuestGlobe(planet.transform, arc.transform);
            UseJezeroSample();
            Show();
        }

        public void Show()
        {
            IsVisible = true;
            LocalZoneVisible=resumeMissionZone;
            MarsMissionZone.RequiredForSelection=LocalZoneVisible;
            RenderSettings.fog=false;
            MarsCameraBackground.Apply(globeCamera,true);
            globeRoot.SetActive(!LocalZoneVisible || questGlobe != null);
            localCamera.enabled = false;
            localRig.enabled = false;
            UpdateCamera();
        }

        public void OpenJezeroMission()
        {
            IsVisible = false;
            LocalZoneVisible=false;
            // The planner captures this mode before hiding the selection page.
            // Preserve it while a mission from the bounded zone is open.
            MarsMissionZone.RequiredForSelection=resumeMissionZone;
            dragging = false;
            globeRoot.SetActive(false);
            localCamera.enabled = true;
            localRig.enabled = true;
        }

        public void UseJezeroSample()
        {
            if(IsBusy)return;
            StartLocation = CopyPoint(mission.currentLocation);
            EndLocation = CopyPoint(mission.destination);
            selectEnd = false;
            distance = 3.5f;
            selectedLandmark = 0;
            FocusLandmark(0);
            selectionMessage = "Jezero endpoints ready. VIEW ROUTES computes the selected traverse.";
            UpdateArc();
            SyncCoordinateFields();
            inputError=false;
        }

        public void FocusLandmark(int index)
        {
            if(IsBusy)return;
            selectedLandmark = Mathf.Clamp(index, 0, Landmarks.Length - 1);
            Landmark landmark = Landmarks[selectedLandmark];
            targetLatitude = landmark.lat;
            targetLongitude = landmark.lon;
        }

        public void SelectLocation(GeoPoint point)
        {
            if(IsBusy)return;
            if(LocalZoneVisible&&!MarsMissionZone.Contains(point))
            {SetError("Choose a point inside the outlined measured mission zone.");return;}
            GeoPoint selected = CopyPoint(point);
            if(string.IsNullOrEmpty(selected.label)||selected.label=="Selected location")
                selected.label=selectEnd?"Destination map selection":"Start map selection";
            selected.latitude = Mathf.Clamp(selected.latitude, -90f, 90f);
            selected.longitude = MarsGeospatial.NormalizeLongitude(selected.longitude);
            if (selectEnd) EndLocation = selected;
            else StartLocation = selected;
            selectEnd = !selectEnd;
            selectionMessage = "Endpoints updated. Select VIEW ROUTES to build the regional plan.";
            if(LocalZoneVisible)selectionMessage=MissionZoneSelectionMessage();
            UpdateArc();
            SyncCoordinateFields();
            inputError=false;
        }

        public void PrepareGlobalCapture(bool selectionPreview)
        {
            Show();
            if (selectionPreview)
            {
                selectEnd = false;
                SelectLocation(new GeoPoint { latitude = 18.65f, longitude = -133.8f });
                SelectLocation(new GeoPoint { latitude = -14.01f, longitude = -58.59f });
                targetLatitude = 12f;
                targetLongitude = -91f;
                selectedLandmark = 3;
            }
            latitude = targetLatitude;
            longitude = targetLongitude;
            UpdateCamera();
        }

        private static GeoPoint CopyPoint(GeoPoint point) => new GeoPoint
        { label = point.label, latitude = point.latitude, longitude = point.longitude };

        private void Update()
        {
            if (questGlobe != null)
            {
                if (IsVisible) questGlobe.UpdateNavigation();
                return;
            }
            if (!IsVisible||LocalZoneVisible) return;
            latitude = Mathf.Lerp(latitude, targetLatitude, 1f - Mathf.Exp(-Time.deltaTime * 8f));
            longitude = Mathf.LerpAngle(longitude, targetLongitude, 1f - Mathf.Exp(-Time.deltaTime * 8f));
            UpdateCamera();
        }

        private void UpdateCamera()
        {
            solarBackdrop.SetActive(!LocalZoneVisible);
            if (questGlobe != null)
            {
                questGlobe.SetFocus(targetLatitude, targetLongitude);
                return;
            }
            globeCamera.rect = new Rect(MapRect.x / CanvasWidth, 1f - MapRect.yMax / CanvasHeight, MapRect.width / CanvasWidth, MapRect.height / CanvasHeight);
            globeCamera.transform.position = MarsGeospatial.ToUnitSphere(latitude, longitude) * distance;
            globeCamera.transform.LookAt(Vector3.zero);
            if(spaceCamera!=null)spaceCamera.transform.rotation=globeCamera.transform.rotation;
            globeMaterial.SetFloat("_Grid", showGrid ? 1f : 0f);
        }

        private void PickLocation(Vector3 mouse)
        {
            Ray ray = globeCamera.ScreenPointToRay(mouse);
            float b = Vector3.Dot(ray.origin, ray.direction);
            float c = ray.origin.sqrMagnitude - 1f;
            float discriminant = b * b - c;
            if (discriminant < 0f) return;
            float alongRay = -b - Mathf.Sqrt(discriminant);
            if (alongRay <= 0f) return;
            SelectLocation(MarsGeospatial.FromUnitSphere(ray.GetPoint(alongRay)));
        }

        private void UpdateArc()
        {
            if (previewArc == null || StartLocation == null || EndLocation == null) return;
            Vector3 start = MarsGeospatial.ToUnitSphere(StartLocation.latitude, StartLocation.longitude);
            Vector3 end = MarsGeospatial.ToUnitSphere(EndLocation.latitude, EndLocation.longitude);
            float angle = MarsGeospatial.AngularDistanceDegrees(StartLocation, EndLocation);
            previewArc.positionCount = angle < 0.1f ? 0 : 97;
            Vector3 tangent = end - Vector3.Dot(start, end) * start;
            if (tangent.sqrMagnitude < 0.000001f) tangent = Vector3.Cross(start, Mathf.Abs(start.y) < 0.9f ? Vector3.up : Vector3.right);
            tangent.Normalize();
            for (int i = 0; i < previewArc.positionCount; i++)
            {
                float radians = angle * Mathf.Deg2Rad * i / 96f;
                previewArc.SetPosition(i, (start * Mathf.Cos(radians) + tangent * Mathf.Sin(radians)) * 1.006f);
            }
            questGlobe?.UpdateArcHeight();
        }

        private void DrawLegacyGlobalInterface()
        {
            if (!IsVisible) return;
            EnsureStyles();
            Matrix4x4 previous = GUI.matrix;
            GUI.matrix = Matrix4x4.TRS(Vector3.zero, Quaternion.identity, new Vector3(Screen.width / CanvasWidth, Screen.height / CanvasHeight, 1));
            // The camera occupies only the map viewport; opaque frame panels also
            // cover the old local camera buffer on every frame and resolution.
            Fill(new Rect(0, 0, 425, 900), new Color(0.018f, 0.030f, 0.046f));
            Fill(new Rect(425, 0, 1175, 118), new Color(0.018f, 0.030f, 0.046f));
            Fill(new Rect(425, 783, 1175, 117), new Color(0.018f, 0.030f, 0.046f));
            Fill(new Rect(1565, 118, 35, 665), new Color(0.018f, 0.030f, 0.046f));
            GUI.Label(new Rect(34, 27, 330, 38), "MARSPATH", title);
            GUI.Label(new Rect(36, 70, 330, 22), "LAGRANGE V  /  MISSION NAVIGATION", small);
            Fill(new Rect(35, 112, 352, 1), new Color(0.16f, 0.23f, 0.28f));
            GUI.Label(new Rect(35, 139, 350, 28), "01   CHOOSE YOUR REGION", heading);
            GUI.Label(new Rect(35, 176, 345, 40), "Explore Mars, inspect locations and open an available surface mission.", body);
            for (int i = 0; i < Landmarks.Length; i++)
            {
                Rect rect = new Rect(35, 237 + i * 66, 352, 56);
                if (GUI.Button(rect, GUIContent.none, selectedLandmark == i ? activeButton : button)) FocusLandmark(i);
                GUI.Label(new Rect(51, rect.y + 8, 220, 22), Landmarks[i].name, heading);
                GUI.Label(new Rect(51, rect.y + 32, 260, 17), i == 0 ? "LOCAL MISSION AVAILABLE" : "GLOBAL IMAGERY", small);
                Fill(new Rect(369, rect.y + 14, 4, 27), i == 0 ? Cyan : new Color(0.27f, 0.34f, 0.38f));
            }
            GUI.Label(new Rect(35, 519, 345, 28), Landmarks[selectedLandmark].name.ToUpperInvariant(), heading);
            GUI.Label(new Rect(35, 555, 345, 64), Landmarks[selectedLandmark].description, body);
            GUI.Label(new Rect(35, 643, 345, 19), "AVAILABLE NOW", small);
            GUI.Label(new Rect(35, 672, 345, 48), "Jezero local terrain\n5 supplied routes  /  EVA simulation", body);
            if (GUI.Button(new Rect(35, 740, 352, 46), "OPEN JEZERO DEMO  >", activeButton)) OpenJezeroMission();
            GUI.Label(new Rect(35, 803, 352, 59), "Globe picks are location bookmarks. The demo opens its own supplied Jezero mission endpoints.", small);

            GUI.Label(new Rect(449, 29, 800, 40), "A PLANET OF POSSIBILITIES", title);
            GUI.Label(new Rect(451, 78, 760, 23), "GLOBAL MARS    /    02 REGIONAL PLANNING    /    03 EVA", small);
            if (GUI.Button(new Rect(1385, 54, 165, 32), showGrid ? "GRID  ON" : "GRID  OFF", button)) showGrid = !showGrid;

            DrawLandmarkLabels();
            bool samePoint = MarsGeospatial.AngularDistanceDegrees(StartLocation, EndLocation) < 0.1f;
            bool sampleEndpoints = Mathf.Abs(StartLocation.latitude - mission.currentLocation.latitude) < 0.00001f
                && Mathf.Abs(StartLocation.longitude - mission.currentLocation.longitude) < 0.00001f
                && Mathf.Abs(EndLocation.latitude - mission.destination.latitude) < 0.00001f
                && Mathf.Abs(EndLocation.longitude - mission.destination.longitude) < 0.00001f;
            DrawPointLabel(StartLocation, samePoint ? (sampleEndpoints ? "S / E  JEZERO SAMPLE" : "S / E  BOOKMARKS") : "S  START", Cyan, -32f);
            if (!samePoint) DrawPointLabel(EndLocation, "E  DESTINATION", Orange, 6f);

            GUI.Box(new Rect(455, 663, 447, 112), GUIContent.none, panel);
            if (GUI.Button(new Rect(467, 674, 211, 28), selectEnd ? "SET START" : "SET START  [ACTIVE]", selectEnd ? button : activeButton)) selectEnd = false;
            if (GUI.Button(new Rect(685, 674, 205, 28), selectEnd ? "SET END  [ACTIVE]" : "SET END", selectEnd ? activeButton : button)) selectEnd = true;
            GUI.Label(new Rect(469, 709, 417, 23), "S   " + Coordinate(StartLocation), body);
            GUI.Label(new Rect(469, 737, 417, 23), "E   " + Coordinate(EndLocation), body);
            GUI.Box(new Rect(1110, 675, 430, 100), GUIContent.none, panel);
            GUI.Label(new Rect(1126, 689, 400, 22), "GEODESIC PREVIEW  /  " + MarsGeospatial.AngularDistanceDegrees(StartLocation, EndLocation).ToString("0.000") + " deg", heading);
            GUI.Label(new Rect(1126, 724, 398, 38), "Geographic separation only. Surface safety and walkability are not evaluated here.", small);

            GUI.Label(new Rect(455, 796, 810, 38), selectionMessage, body);
            if (GUI.Button(new Rect(1330, 797, 210, 32), "USE JEZERO SAMPLE", button)) UseJezeroSample();
            GUI.Label(new Rect(455, 857, 800, 19), "Drag: rotate   |   Scroll: zoom   |   Click: place start / destination", small);
            GUI.Label(new Rect(455, 879, 1090, 18), "BASEMAP: NASA / JPL-CALTECH / USGS VIKING IMAGERY    •    GLOBAL COLOR CONTEXT, NOT AN ELEVATION OR HAZARD MAP", small);
            // UI controls consume the mouse event before globe selection. This
            // avoids changing bookmarks when clicking a label or map control.
            HandleMapPointer(Event.current);
            GUI.matrix = previous;
        }

        private void HandleMapPointer(Event current)
        {
            if(IsBusy){dragging=false;return;}
            // Scroll views temporarily change IMGUI's event coordinate space.
            // Use screen input as a second boundary so scrolling the site list
            // or editing an endpoint cannot operate the globe behind the UI.
            Vector2 physicalMouse = new Vector2(Input.mousePosition.x / Screen.width * CanvasWidth,
                (1f - Input.mousePosition.y / Screen.height) * CanvasHeight);
            bool onMap = MapRect.Contains(physicalMouse) && physicalMouse.y > 138f && physicalMouse.y < 600f;
            if (current.type == EventType.MouseDown && onMap && (current.button == 0 || current.button == 1))
            {
                dragging = true;
                dragTravel = 0f;
                mouseDown = current.mousePosition;
                current.Use();
            }
            else if (current.type == EventType.MouseDrag && dragging)
            {
                dragTravel += current.delta.magnitude;
                if (dragTravel > 5f)
                {
                    targetLongitude -= current.delta.x * 0.24f;
                    targetLatitude = Mathf.Clamp(targetLatitude + current.delta.y * 0.21f, -85f, 85f);
                }
                current.Use();
            }
            else if (current.type == EventType.MouseUp && dragging)
            {
                if (current.button == 0 && onMap && dragTravel <= 5f
                    && (new Vector3(current.mousePosition.x, current.mousePosition.y) - mouseDown).sqrMagnitude <= 25f)
                {
                    Vector2 mouse = current.mousePosition;
                    PickLocation(new Vector3(mouse.x / CanvasWidth * Screen.width, (1f - mouse.y / CanvasHeight) * Screen.height));
                }
                dragging = false;
                current.Use();
            }
            else if (current.type == EventType.ScrollWheel && onMap)
            {
                distance = Mathf.Clamp(distance + current.delta.y * 0.10f, 2.45f, 5.5f);
                current.Use();
            }
        }

        private void DrawLandmarkLabels()
        {
            for (int i = 0; i < Landmarks.Length; i++)
            {
                Landmark site = Landmarks[i];
                Vector3 unit = MarsGeospatial.ToUnitSphere(site.lat, site.lon);
                if (!VisiblePoint(unit, out Vector2 pixel)) continue;
                Rect labelRect = new Rect(Mathf.Clamp(pixel.x + 12, MapRect.x + 8, MapRect.xMax - 160), Mathf.Max(MapRect.y + 8, pixel.y - 10), 148, 24);
                if (OverlapsPointLabel(labelRect, StartLocation, -32f) || OverlapsPointLabel(labelRect, EndLocation, 6f)) continue;
                Color saved = GUI.color;
                GUI.color = i == 0 ? Cyan : new Color(0.81f, 0.81f, 0.72f);
                GUI.Label(new Rect(pixel.x - 5, pixel.y - 10, 20, 22), "+", heading);
                GUI.color = saved;
                if (GUI.Button(labelRect, site.name.ToUpperInvariant(), button)) FocusLandmark(i);
            }
        }

        private bool OverlapsPointLabel(Rect rectangle, GeoPoint point, float offset)
        {
            if (!VisiblePoint(MarsGeospatial.ToUnitSphere(point.latitude, point.longitude), out Vector2 pixel)) return false;
            Rect pointLabel = PointLabelRect(pixel, offset);
            pointLabel.xMin -= 8;
            pointLabel.yMin -= 8;
            pointLabel.width += 8;
            pointLabel.height += 8;
            return pointLabel.Overlaps(rectangle);
        }

        private static Rect PointLabelRect(Vector2 pixel, float offset)
        {
            return new Rect(Mathf.Clamp(pixel.x + 13, MapRect.x + 8, MapRect.xMax - 230),
                Mathf.Clamp(pixel.y + offset, MapRect.y + 8, 625f), 215, 26);
        }

        private void DrawPointLabel(GeoPoint point, string text, Color color, float offset)
        {
            Vector3 unit = MarsGeospatial.ToUnitSphere(point.latitude, point.longitude);
            if (!VisiblePoint(unit, out Vector2 pixel)) return;
            Fill(new Rect(pixel.x - 4, pixel.y - 4, 8, 8), color);
            badge.normal.textColor = color;
            GUI.Label(PointLabelRect(pixel, offset), text, badge);
        }

        private bool VisiblePoint(Vector3 unit, out Vector2 pixel)
        {
            pixel = Vector2.zero;
            if (Vector3.Dot(unit, globeCamera.transform.position.normalized) <= 1f / distance + 0.01f) return false;
            Vector3 screen = globeCamera.WorldToScreenPoint(unit * 1.008f);
            pixel = new Vector2(screen.x / Screen.width * CanvasWidth, (Screen.height - screen.y) / Screen.height * CanvasHeight);
            return MapRect.Contains(pixel) && pixel.y < 650f;
        }

        private static string Coordinate(GeoPoint point)
        {
            return $"{Mathf.Abs(point.latitude):0.00000}°{(point.latitude >= 0 ? "N" : "S")}    {Mathf.Abs(point.longitude):0.00000}°{(point.longitude >= 0 ? "E" : "W")}";
        }

        private static void Fill(Rect rect, Color color)
        { Color saved = GUI.color; GUI.color = color; GUI.DrawTexture(rect, Texture2D.whiteTexture); GUI.color = saved; }

        private void EnsureStyles()
        {
            if (title != null) return;
            panelTexture = SolidTexture(new Color(0.025f, 0.05f, 0.066f, 1f));
            buttonTexture = SolidTexture(new Color(0.045f, 0.075f, 0.094f));
            activeTexture = SolidTexture(new Color(0.035f, 0.23f, 0.26f));
            title = new GUIStyle(GUI.skin.label) { fontSize = 27, fontStyle = FontStyle.Bold, normal = { textColor = Color.white } };
            heading = new GUIStyle(GUI.skin.label) { fontSize = 14, fontStyle = FontStyle.Bold, normal = { textColor = Color.white } };
            body = new GUIStyle(GUI.skin.label) { fontSize = 13, wordWrap = true, normal = { textColor = new Color(0.73f, 0.80f, 0.81f) } };
            small = new GUIStyle(body) { fontSize = 11, normal = { textColor = Muted } };
            panel = new GUIStyle(GUI.skin.box) { normal = { background = panelTexture } };
            button = new GUIStyle(GUI.skin.button) { fontSize = 11, fontStyle = FontStyle.Bold, normal = { background = buttonTexture, textColor = Color.white }, hover = { background = activeTexture, textColor = Cyan }, active = { background = activeTexture, textColor = Color.white } };
            activeButton = new GUIStyle(button) { normal = { background = activeTexture, textColor = Cyan } };
            badge = new GUIStyle(heading) { fontSize = 12, normal = { background = panelTexture, textColor = Cyan }, padding = new RectOffset(8, 6, 4, 4) };
            endpointPillTexture=EndpointPillTexture();endpointPinTexture=Resources.Load<Texture2D>("NASA/MapPinEmoji");
            endpointLabel=new GUIStyle(heading){fontSize=11,alignment=TextAnchor.MiddleCenter,
                normal={background=endpointPillTexture,textColor=Cyan},border=new RectOffset(10,10,10,10),
                padding=new RectOffset(10,10,2,2),clipping=TextClipping.Clip,wordWrap=false};
        }

        private static Texture2D EndpointPillTexture()
        {
            const int width=48,height=24;
            var texture=new Texture2D(width,height,TextureFormat.RGBA32,false){name="Compact endpoint label",wrapMode=TextureWrapMode.Clamp,filterMode=FilterMode.Bilinear};
            for(int y=0;y<height;y++)for(int x=0;x<width;x++)
            {
                Vector2 q=new Vector2(Mathf.Abs(x+.5f-width*.5f)-(width*.5f-10),Mathf.Abs(y+.5f-height*.5f)-(height*.5f-10));
                float edge=new Vector2(Mathf.Max(q.x,0),Mathf.Max(q.y,0)).magnitude+Mathf.Min(Mathf.Max(q.x,q.y),0)-10;
                texture.SetPixel(x,y,new Color(.018f,.035f,.043f,.78f*Mathf.Clamp01(.5f-edge)));
            }
            texture.Apply(false,true);return texture;
        }

        private static Texture2D SolidTexture(Color color)
        { Texture2D texture = new Texture2D(1, 1); texture.SetPixel(0, 0, color); texture.Apply(); return texture; }

        private static Mesh CreateSphere()
        {
            const int columns = 192, rows = 96;
            Vector3[] vertices = new Vector3[(columns + 1) * (rows + 1)];
            Vector2[] uv = new Vector2[vertices.Length];
            int[] indices = new int[columns * rows * 6];
            int next = 0;
            for (int row = 0; row <= rows; row++)
            for (int column = 0; column <= columns; column++)
            {
                int index = row * (columns + 1) + column;
                vertices[index] = MarsGeospatial.ToUnitSphere(row * 180f / rows - 90f, column * 360f / columns - 180f);
                uv[index] = new Vector2(column / (float)columns, row / (float)rows);
                if (row == rows || column == columns) continue;
                int above = index + columns + 1;
                indices[next++] = index; indices[next++] = above; indices[next++] = index + 1;
                indices[next++] = index + 1; indices[next++] = above; indices[next++] = above + 1;
            }
            Mesh mesh = new Mesh { name = "Georeferenced Mars Globe" };
            mesh.vertices = vertices; mesh.normals = vertices; mesh.uv = uv; mesh.triangles = indices; mesh.RecalculateBounds();
            return mesh;
        }

        private void OnDestroy()
        {
            ReleaseMissionZoneMapTexture();
            ReleaseGlobalMenuStyles();
            if (sphereMesh != null) Destroy(sphereMesh);
            if (globeMaterial != null) Destroy(globeMaterial);
            if (arcMaterial != null) Destroy(arcMaterial);
            if (panelTexture != null) Destroy(panelTexture);
            if (buttonTexture != null) Destroy(buttonTexture);
            if (activeTexture != null) Destroy(activeTexture);
            if(endpointPillTexture!=null)Destroy(endpointPillTexture);
        }
    }
}

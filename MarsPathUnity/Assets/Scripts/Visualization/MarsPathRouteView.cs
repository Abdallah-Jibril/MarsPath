using System.Collections.Generic;
using UnityEngine;

namespace MarsPath
{
    public partial class MarsPathRouteView : MonoBehaviour
    {
        private readonly List<LineRenderer> renderers = new List<LineRenderer>();
        private readonly List<GameObject> arrowGroups = new List<GameObject>();
        private MissionData mission;
        private int selectedIndex;
        private bool missionMode;
        private GameObject markerRoot;
        private float selectedLength;
        private float planningScale=1;
        private readonly List<Transform> markers=new List<Transform>();
        private readonly List<Vector3> markerScales=new List<Vector3>();
        public int VisibleArrowCount { get; private set; }

        public Vector3[] SelectedWorldPoints { get; private set; }

        public void Build(MissionData source)
        {
            mission = source;
            CacheRenderedGround();
            for (int i = 0; i < mission.routes.Length; i++)
            {
                RouteData route = mission.routes[i];
                if(!route.available){renderers.Add(null);arrowGroups.Add(null);continue;}
                GameObject routeObject = new GameObject($"Route_{route.type}");
                routeObject.transform.SetParent(transform, false);
                LineRenderer line = routeObject.AddComponent<LineRenderer>();
                line.useWorldSpace = true;
                line.textureMode = LineTextureMode.Tile;
                line.numCornerVertices = 5;
                line.numCapVertices = 5;
                Color color = ParseColor(route.colorHex, Color.white);
                Material routeMaterial = MarsPathMaterialFactory.CreateRoute(Color.white, $"{route.displayName} Route Material");
                if (routeMaterial != null) line.material = routeMaterial;
                line.startColor = color;
                line.endColor = color;
                Vector3[] points = ConvertPoints(route.waypoints);
                line.positionCount = points.Length;
                line.SetPositions(points);
                line.startWidth = 0.22f;
                line.endWidth = 0.22f;
                line.sortingOrder = 6;
                renderers.Add(line);
                arrowGroups.Add(BuildArrowGroup(route, points, color, routeObject.transform));
            }

            Select(0);
            BuildMarkers();
            BuildPlanningArrowPool();
        }

        public void Select(int index)
        {
            selectedIndex = Mathf.Clamp(index, 0, renderers.Count - 1);
            RefreshVisuals();
            SelectedWorldPoints = ConvertPoints(mission.routes[selectedIndex].waypoints);
            selectedLength=0;for(int i=1;i<SelectedWorldPoints.Length;i++)selectedLength+=Vector3.Distance(SelectedWorldPoints[i-1],SelectedWorldPoints[i]);
        }

        private void RefreshVisuals()
        {
            for (int i = 0; i < renderers.Count; i++)
            {
                if(renderers[i]==null)continue;
                bool selected = i == selectedIndex;
                bool visible = !missionMode || selected;
                Color color = ParseColor(mission.routes[i].colorHex, Color.white);
                color.a = selected || missionMode ? 1f : 0.68f;
                renderers[i].enabled = !missionMode;
                renderers[i].startWidth = (selected ? 0.30f : 0.13f)*planningScale;
                renderers[i].endWidth = (selected ? 0.30f : 0.13f)*planningScale;
                renderers[i].startColor = color;
                renderers[i].endColor = color;
                renderers[i].sortingOrder = selected ? 10 : 5;
                renderers[i].gameObject.SetActive(visible);
                arrowGroups[i].SetActive(visible && selected);
                foreach (LineRenderer arrow in arrowGroups[i].GetComponentsInChildren<LineRenderer>(true))
                {
                    arrow.startColor = color;
                    arrow.endColor = color;
                    arrow.startWidth = missionMode?0.20f:0.12f;
                    arrow.endWidth = missionMode?0.20f:0.12f;
                    arrow.sortingOrder = selected ? 12 : 7;
                    arrow.enabled = true;
                }
            }
        }

        public void SetMissionMode(bool enabled)
        {
            missionMode = enabled;
            if (markerRoot != null) markerRoot.SetActive(!enabled);
            if(planningGuidanceRoot!=null)planningGuidanceRoot.SetActive(!enabled);
            RefreshVisuals();
        }
        public void SetPlanningVisualScale(float scale)
        {
            if(missionMode)return;planningScale=Mathf.Clamp(scale,0.002f,6);
            for(int i=0;i<renderers.Count;i++)
            {
                if(renderers[i]==null)continue;float width=(i==selectedIndex?0.3f:0.13f)*planningScale;
                renderers[i].startWidth=width;renderers[i].endWidth=width;
                if(arrowGroups[i]!=null)arrowGroups[i].SetActive(false);
            }
            for(int i=0;i<markers.Count;i++)if(markers[i]!=null)markers[i].localScale=markerScales[i]*planningScale;
        }

        public void SetMissionProgress(float progress)
        {ShowForwardArrows(progress*selectedLength);}

        public void SetMissionPosition(Vector3 position)
        {
            // Geographic progress is not a fraction of a sloping 3D polyline.
            // Locate the actual astronaut on the visible guidance instead.
            float nearest=float.MaxValue,along=0,current=0;
            for(int i=1;i<SelectedWorldPoints.Length;i++)
            {
                Vector3 a=SelectedWorldPoints[i-1],b=SelectedWorldPoints[i],delta=b-a;
                Vector2 d=new Vector2(delta.x,delta.z),offset=new Vector2(position.x-a.x,position.z-a.z);
                float t=d.sqrMagnitude>0.00001f?Mathf.Clamp01(Vector2.Dot(offset,d)/d.sqrMagnitude):0;
                Vector2 difference=offset-d*t;float length=delta.magnitude;
                if(difference.sqrMagnitude<nearest){nearest=difference.sqrMagnitude;current=along+length*t;}
                along+=length;
            }
            ShowForwardArrows(current);
        }
        private void ShowForwardArrows(float current)
        {
            if (!missionMode || selectedIndex < 0 || selectedIndex >= arrowGroups.Count) return;
            if(arrowGroups[selectedIndex]==null)return;
            Transform group = arrowGroups[selectedIndex].transform;
            int count = group.childCount;
            VisibleArrowCount=0;
            for (int i = 0; i < count; i++)
            {
                LineRenderer arrow = group.GetChild(i).GetComponent<LineRenderer>();
                if (arrow == null) continue;
                float ahead=group.GetChild(i).GetComponent<MarsNavigationArrow>().distance-current;
                arrow.enabled=ahead>(MarsPathCameraRig.ActiveHelmet?4.5f:2f)&&ahead<25f&&VisibleArrowCount<6;
                if(arrow.enabled)
                {VisibleArrowCount++;Color c=ParseColor(mission.routes[selectedIndex].colorHex,Color.cyan);c.a=Mathf.Lerp(1,0.55f,Mathf.InverseLerp(9,25,ahead));arrow.startColor=c;arrow.endColor=c;arrow.startWidth=arrow.endWidth=MarsPathCameraRig.ActiveHelmet ? .14f : .20f;}
            }
        }

        private GameObject BuildArrowGroup(RouteData route, Vector3[] points, Color color, Transform parent)
        {
            GameObject group = new GameObject($"{route.type}_Direction_Arrows");
            group.transform.SetParent(parent, false);
            Material material = MarsPathMaterialFactory.CreateRoute(Color.white, $"{route.displayName} Arrow Material");

            float distance=0,lastArrow=-2.8f;
            for (int i = 1; i < points.Length - 1; i++)
            {
                distance+=Vector3.Distance(points[i-1],points[i]);
                if(distance-lastArrow<3.8f)continue;lastArrow=distance;
                Vector3 direction = points[Mathf.Min(i + 3, points.Length - 1)] - points[Mathf.Max(i - 3, 0)];
                direction.y = 0f;
                if (direction.sqrMagnitude < 0.01f) continue;
                direction.Normalize();
                Vector3 perpendicular = new Vector3(-direction.z, 0f, direction.x);
                Vector3 center = points[i] + Vector3.up * 0.10f;
                Vector3 tip = center + direction * 0.72f;
                Vector3 left = center - direction * 0.18f + perpendicular * 0.33f;
                Vector3 right = center - direction * 0.18f - perpendicular * 0.33f;
                if(!MarsRegionalTerrain.PlanetaryView)
                {
                    // The source interpolant can sit below a rendered triangle
                    // between its vertices. Attach guidance to the actual face.
                    tip=GuidanceGroundPoint(tip.x,tip.z,.18f);
                    left=GuidanceGroundPoint(left.x,left.z,.18f);
                    right=GuidanceGroundPoint(right.x,right.z,.18f);
                }

                LineRenderer arrow = new GameObject($"Arrow_{i:000}").AddComponent<LineRenderer>();
                arrow.transform.SetParent(group.transform, false);
                arrow.gameObject.AddComponent<MarsNavigationArrow>().distance=distance;
                arrow.useWorldSpace = true;
                // Subdivide both arms so even a metre-scale crater edge cannot
                // bury the chord between otherwise correctly grounded tips.
                const int armSteps=4;var groundedArrow=new Vector3[armSteps*2+1];
                for(int step=0;step<=armSteps;step++)
                {Vector3 p=Vector3.Lerp(left,tip,step/(float)armSteps);groundedArrow[step]=GuidanceGroundPoint(p.x,p.z,.18f);}
                for(int step=1;step<=armSteps;step++)
                {Vector3 p=Vector3.Lerp(tip,right,step/(float)armSteps);groundedArrow[armSteps+step]=GuidanceGroundPoint(p.x,p.z,.18f);}
                arrow.positionCount = groundedArrow.Length;
                arrow.SetPositions(groundedArrow);
                arrow.startWidth = 0.24f;
                arrow.endWidth = 0.24f;
                arrow.numCornerVertices = 2;
                arrow.numCapVertices = 2;
                arrow.startColor = color;
                arrow.endColor = color;
                arrow.sortingOrder = 8;
                if (material != null) arrow.sharedMaterial = material;
            }

            return group;
        }

        private Vector3[] ConvertPoints(RoutePoint[] points)
        {
            List<Vector3> converted = new List<Vector3>();
            for (int segment = 0; segment < points.Length - 1; segment++)
            {
                float dx=(points[segment+1].x-points[segment].x)*60,dz=(points[segment+1].z-points[segment].z)*60;
                float step=groundCellStep>0?groundCellStep*.4f:.12f;
                int samplesPerSegment=Mathf.Clamp(Mathf.CeilToInt(Mathf.Sqrt(dx*dx+dz*dz)/Mathf.Max(.02f,step)),2,64);
                for (int sample = 0; sample < samplesPerSegment; sample++)
                {
                    float t = sample / (float)samplesPerSegment;
                    float normalizedX = Mathf.Lerp(points[segment].x, points[segment + 1].x, t);
                    float normalizedZ = Mathf.Lerp(points[segment].z, points[segment + 1].z, t);
                    float x = normalizedX * MarsTerrainGenerator.TerrainSize * 0.5f;
                    float z = normalizedZ * MarsTerrainGenerator.TerrainSize * 0.5f;
                    converted.Add(GuidanceGroundPoint(x,z,0.10f));
                }
            }

            RoutePoint last = points[points.Length - 1];
            float lastX = last.x * MarsTerrainGenerator.TerrainSize * 0.5f;
            float lastZ = last.z * MarsTerrainGenerator.TerrainSize * 0.5f;
            converted.Add(GuidanceGroundPoint(lastX,lastZ,0.10f));
            return converted.ToArray();
        }

        private void BuildMarkers()
        {
            markerRoot = new GameObject("Planning Markers");
            markerRoot.transform.SetParent(transform, false);
            if(mission.routes.Length>0)
            {
                RoutePoint[] endpoints=mission.routes[0].waypoints;
                if(endpoints!=null&&endpoints.Length>1)
                {
                    CreateMarker("Start",mission.currentLocation.label,endpoints[0].x,endpoints[0].z,new Color(0.25f,0.94f,0.75f),PrimitiveType.Cylinder);
                    CreateMarker("Destination",mission.destination.label,endpoints[endpoints.Length-1].x,endpoints[endpoints.Length-1].z,new Color(1f,0.64f,0.30f),PrimitiveType.Cylinder);
                }
            }
            if (mission.hazards != null)
            {
                foreach (HazardData hazard in mission.hazards)
                {
                    CreateMarker("Hazard", hazard.label, hazard.x, hazard.z, new Color(1f, 0.18f, 0.08f), PrimitiveType.Cylinder);
                }
            }

            if (mission.sciencePoints != null)
            {
                foreach (SciencePointData point in mission.sciencePoints)
                {
                    CreateMarker("Science", point.label, point.x, point.z, new Color(0.2f, 0.9f, 1f), PrimitiveType.Sphere);
                }
            }
        }

        private void CreateMarker(string category, string label, float normalizedX, float normalizedZ, Color color, PrimitiveType primitive)
        {
            float x = normalizedX * MarsTerrainGenerator.TerrainSize * 0.5f;
            float z = normalizedZ * MarsTerrainGenerator.TerrainSize * 0.5f;
            float y = MarsTerrainGenerator.SampleHeight(x, z) + 1.1f;
            GameObject marker = GameObject.CreatePrimitive(primitive);
            marker.name = $"{category}_{label}";
            marker.transform.SetParent(markerRoot.transform, false);
            marker.transform.position = MarsTerrainGenerator.SurfacePoint(x,z,0.35f);
            marker.transform.localScale = primitive == PrimitiveType.Sphere ? Vector3.one * 1.4f : new Vector3(1.4f, 0.25f, 1.4f);
            markers.Add(marker.transform);markerScales.Add(marker.transform.localScale);
            Material material = MarsPathMaterialFactory.Create(color, $"{category} Marker Material");
            if (material != null) marker.GetComponent<Renderer>().sharedMaterial = material;
        }

        public static Color ParseColor(string hex, Color fallback)
        {
            return ColorUtility.TryParseHtmlString(hex, out Color color) ? color : fallback;
        }

        private void OnDestroy()
        {
            HashSet<Material> materials=new HashSet<Material>();
            foreach(Renderer renderer in GetComponentsInChildren<Renderer>(true))foreach(Material material in renderer.sharedMaterials)if(material!=null)materials.Add(material);
            foreach(Material material in materials)Destroy(material);
        }
    }
}

using System.Collections.Generic;
using UnityEngine;

namespace MarsPath
{
    public partial class MarsPathRouteView
    {
        private const int PlanningChevronCapacity=48;
        private GameObject planningGuidanceRoot;
        private readonly List<LineRenderer> planningChevrons=new List<LineRenderer>();
        private readonly List<Vector3[]> planningGroundLines=new List<Vector3[]>();
        private readonly List<Vector3[]> planningOriginalLines=new List<Vector3[]>();
        private readonly List<Vector3[]> planningLiftedLines=new List<Vector3[]>();
        private Vector3[] groundVertices;
        private Transform groundTransform;
        private int groundResolution;
        private float groundCellStep,groundMinimumX,groundMinimumZ;
        public int VisiblePlanningArrowCount{get;private set;}
        public float PlanningSelectedLinePixels{get;private set;}
        public float PlanningMaximumGroundGap{get;private set;}
        public IReadOnlyList<LineRenderer> PlanningArrowRenderers=>planningChevrons;
        public Vector3[] SelectedPlanningGroundPoints=>selectedIndex>=0&&selectedIndex<planningGroundLines.Count?planningGroundLines[selectedIndex]:null;

        private void CacheRenderedGround()
        {
            foreach(MarsTerrainGenerator ground in FindObjectsByType<MarsTerrainGenerator>(FindObjectsSortMode.None))
            {
                Mesh mesh=ground.GetComponent<MeshFilter>()?.sharedMesh;
                if(mesh==null)continue;
                int n=Mathf.RoundToInt(Mathf.Sqrt(mesh.vertexCount));
                if(n<2||n*n!=mesh.vertexCount)continue;
                groundVertices=mesh.vertices;groundTransform=ground.transform;groundResolution=n;
                groundMinimumX=groundVertices[0].x;groundMinimumZ=groundVertices[0].z;
                groundCellStep=groundVertices[1].x-groundMinimumX;
                if(groundCellStep>0)return;
            }
        }

        // Navigation overlays follow the triangles that are actually drawn,
        // rather than a second DEM interpolation that can pass under a mesh face.
        private Vector3 GuidanceGroundPoint(float x,float z,float lift=0)
        {
            if(!missionMode&&MarsPlanningFocusTile.TryRenderedHeight(x,z,out float detailHeight))return new Vector3(x,detailHeight+lift,z);
            if(!missionMode&&MarsAdaptivePlanningTerrain.TryRenderedHeight(x,z,out float adaptiveHeight))return new Vector3(x,adaptiveHeight+lift,z);
            if(groundVertices!=null&&groundTransform!=null)
            {
                Vector3 local=groundTransform.InverseTransformPoint(new Vector3(x,0,z));
                float gx=(local.x-groundMinimumX)/groundCellStep,gz=(local.z-groundMinimumZ)/groundCellStep;
                if(gx>=0&&gz>=0&&gx<=groundResolution-1&&gz<=groundResolution-1)
                {
                    int ix=Mathf.Min(groundResolution-2,(int)gx),iz=Mathf.Min(groundResolution-2,(int)gz);
                    float fx=gx-ix,fz=gz-iz;int a=iz*groundResolution+ix;
                    float h=fx+fz<=1?groundVertices[a].y+(groundVertices[a+1].y-groundVertices[a].y)*fx+(groundVertices[a+groundResolution].y-groundVertices[a].y)*fz:
                        groundVertices[a+groundResolution+1].y+(groundVertices[a+groundResolution].y-groundVertices[a+groundResolution+1].y)*(1-fx)+(groundVertices[a+1].y-groundVertices[a+groundResolution+1].y)*(1-fz);
                    return groundTransform.TransformPoint(new Vector3(local.x,h,local.z))+Vector3.up*lift;
                }
            }
            return MarsTerrainGenerator.SurfacePoint(x,z,lift);
        }

        private void BuildPlanningArrowPool()
        {
            if(mission.generatedRegion&&MarsRegionalTerrain.HumanScale)return;
            planningGuidanceRoot=new GameObject("Screen-spaced planning guidance");planningGuidanceRoot.transform.SetParent(transform,false);
            Material material=MarsPathMaterialFactory.CreateRoute(Color.white,"Planning chevrons / depth tested");
            for(int i=0;i<PlanningChevronCapacity;i++)
            {
                LineRenderer line=new GameObject("Planning chevron "+i).AddComponent<LineRenderer>();line.transform.SetParent(planningGuidanceRoot.transform,false);
                line.useWorldSpace=true;line.positionCount=3;line.numCornerVertices=3;line.numCapVertices=3;line.sharedMaterial=material;line.enabled=false;
                planningChevrons.Add(line);
            }
            foreach(LineRenderer line in renderers)
            {
                if(line==null){planningGroundLines.Add(null);planningOriginalLines.Add(null);planningLiftedLines.Add(null);continue;}
                var points=new Vector3[line.positionCount];line.GetPositions(points);
                for(int i=0;i<points.Length;i++)points[i].y-=.10f;
                planningGroundLines.Add(points);planningOriginalLines.Add((Vector3[])points.Clone());planningLiftedLines.Add(new Vector3[points.Length]);
            }
        }

        internal Camera PlanningCamera { get; set; }

        private void LateUpdate()
        {if(!missionMode&&planningGuidanceRoot!=null)RefreshPlanningGuidance(PlanningCamera??Camera.main);}

        /// <summary>Re-ground the existing selected navigation after a focus tile changes.</summary>
        public void RefreshPlanningGeometry()
        {
            if(missionMode||planningGuidanceRoot==null)return;
            for(int route=0;route<planningGroundLines.Count;route++)
            {
                Vector3[] original=planningOriginalLines[route];if(original==null)continue;
                var points=new List<Vector3>(original.Length);
                float detailStep=MarsPlanningFocusTile.DisplayCellMeters/Mathf.Max(.001f,MarsRegionalTerrain.MetersPerUnit)*.4f;
                for(int i=1;i<original.Length;i++)
                {
                    Vector3 a=original[i-1],b=original[i],mid=(a+b)*.5f;
                    bool detail=detailStep>0&&(MarsPlanningFocusTile.TryRenderedHeight(a.x,a.z,out _)||MarsPlanningFocusTile.TryRenderedHeight(b.x,b.z,out _)||MarsPlanningFocusTile.TryRenderedHeight(mid.x,mid.z,out _));
                    float adaptiveStep=MarsAdaptivePlanningTerrain.CellMetersAt(mid.x,mid.z)/Mathf.Max(.001f,MarsRegionalTerrain.MetersPerUnit)*.4f;
                    float step=detail?detailStep:adaptiveStep;
                    int subdivisions=step>0?Mathf.Clamp(Mathf.CeilToInt(new Vector2(b.x-a.x,b.z-a.z).magnitude/Mathf.Max(.00005f,step)),1,256):1;
                    // Reserve at least one point for every remaining original
                    // segment so the budget never truncates the selected route.
                    subdivisions=Mathf.Min(subdivisions,Mathf.Max(1,16383-points.Count-(original.Length-1-i)));
                    for(int j=0;j<subdivisions;j++){Vector3 p=Vector3.Lerp(a,b,j/(float)subdivisions);points.Add(GuidanceGroundPoint(p.x,p.z));}
                }
                Vector3 last=original[original.Length-1];points.Add(GuidanceGroundPoint(last.x,last.z));
                planningGroundLines[route]=points.ToArray();planningLiftedLines[route]=new Vector3[points.Count];renderers[route].positionCount=points.Count;
            }
            Vector3[] selected=planningGroundLines[selectedIndex];
            if(selected!=null)
            {
                SelectedWorldPoints=new Vector3[selected.Length];selectedLength=0;
                for(int i=0;i<selected.Length;i++){SelectedWorldPoints[i]=selected[i]+Vector3.up*.10f;if(i>0)selectedLength+=Vector3.Distance(SelectedWorldPoints[i-1],SelectedWorldPoints[i]);}
            }
            RefreshPlanningGuidance(PlanningCamera??Camera.main);
        }

        /// <summary>Deterministic native acceptance entry point after a camera zoom/orbit.</summary>
        public void RefreshPlanningGuidance(Camera camera)
        {
            if(missionMode||camera==null||planningGuidanceRoot==null)return;
            VisiblePlanningArrowCount=0;PlanningMaximumGroundGap=0;
            foreach(LineRenderer arrow in planningChevrons)arrow.enabled=false;
            foreach(GameObject arrows in arrowGroups)if(arrows!=null)arrows.SetActive(false);
            float pixelFactor=2*Mathf.Tan(camera.fieldOfView*Mathf.Deg2Rad*.5f)/Mathf.Max(1,camera.pixelHeight);
            Vector3 cameraPosition=camera.transform.position,forward=camera.transform.forward;
            for(int route=0;route<renderers.Count;route++)
            {
                LineRenderer line=renderers[route];Vector3[] points=planningGroundLines[route];
                if(line==null||points==null)continue;
                Vector3[] lifted=planningLiftedLines[route];float nearestDepth=float.MaxValue;
                for(int i=0;i<points.Length;i++)
                {
                    float depth=Vector3.Dot(points[i]-cameraPosition,forward),upp=pixelFactor*Mathf.Max(camera.nearClipPlane,depth);
                    lifted[i]=points[i]+Vector3.up*Mathf.Max(.0004f,upp*.8f);
                    Vector3 v=camera.WorldToViewportPoint(points[i]);if(v.z>camera.nearClipPlane&&v.x>-.05f&&v.x<1.05f&&v.y>-.05f&&v.y<1.05f)nearestDepth=Mathf.Min(nearestDepth,depth);
                }
                if(nearestDepth==float.MaxValue)nearestDepth=Mathf.Max(.2f,Vector3.Dot(line.bounds.center-cameraPosition,forward));
                float pixels=route==selectedIndex?3.2f:1.85f,width=pixelFactor*Mathf.Max(.05f,nearestDepth)*pixels;
                line.startWidth=width;line.endWidth=width;line.SetPositions(lifted);
                if(route==selectedIndex)PlanningSelectedLinePixels=pixels;
            }
            if(selectedIndex<0||selectedIndex>=planningGroundLines.Count)return;
            Vector3[] selected=planningGroundLines[selectedIndex];if(selected==null)return;
            Color color=ParseColor(mission.routes[selectedIndex].colorHex,Color.cyan);
            float untilNext=35;
            for(int i=1;i<selected.Length&&VisiblePlanningArrowCount<planningChevrons.Count;i++)
            {
                Vector3 a=selected[i-1],b=selected[i],va=camera.WorldToViewportPoint(a),vb=camera.WorldToViewportPoint(b);
                if(va.z<=camera.nearClipPlane||vb.z<=camera.nearClipPlane)continue;
                if(!ClipGuidanceSegment(new Vector2(va.x,va.y),new Vector2(vb.x,vb.y),out float from,out float to))continue;
                Vector2 pixelDelta=new Vector2((vb.x-va.x)*camera.pixelWidth,(vb.y-va.y)*camera.pixelHeight);
                float screenLength=pixelDelta.magnitude,visibleLength=screenLength*(to-from);
                if(visibleLength<.001f)continue;
                while(untilNext<=visibleLength&&VisiblePlanningArrowCount<planningChevrons.Count)
                {
                    float screenFraction=from+untilNext/screenLength;
                    float t=screenFraction*va.z/Mathf.Max(.00001f,vb.z*(1-screenFraction)+va.z*screenFraction);
                    Vector3 center=Vector3.Lerp(a,b,t),direction=b-a;direction.y=0;
                    if(direction.sqrMagnitude>.00000001f)
                    {
                        direction.Normalize();float depth=Vector3.Dot(center-cameraPosition,forward),upp=pixelFactor*Mathf.Max(.05f,depth);
                        Vector3 side=new Vector3(-direction.z,0,direction.x);float lift=Mathf.Max(.0005f,upp*1.15f);
                        Vector3 tip=GuidanceGroundPoint(center.x+direction.x*upp*8,center.z+direction.z*upp*8,lift);
                        Vector3 left=GuidanceGroundPoint(center.x-direction.x*upp*5+side.x*upp*6,center.z-direction.z*upp*5+side.z*upp*6,lift);
                        Vector3 right=GuidanceGroundPoint(center.x-direction.x*upp*5-side.x*upp*6,center.z-direction.z*upp*5-side.z*upp*6,lift);
                        LineRenderer arrow=planningChevrons[VisiblePlanningArrowCount++];arrow.enabled=true;arrow.startColor=arrow.endColor=color;
                        arrow.startWidth=arrow.endWidth=upp*2.7f;arrow.SetPosition(0,left);arrow.SetPosition(1,tip);arrow.SetPosition(2,right);
                        PlanningMaximumGroundGap=Mathf.Max(PlanningMaximumGroundGap,lift);
                    }
                    untilNext+=74;
                }
                untilNext-=visibleLength;
            }
        }

        private static bool ClipGuidanceSegment(Vector2 a,Vector2 b,out float from,out float to)
        {
            from=0;to=1;Vector2 delta=b-a;
            return ClipGuidanceEdge(-delta.x,a.x-.025f,ref from,ref to)&&ClipGuidanceEdge(delta.x,.975f-a.x,ref from,ref to)&&ClipGuidanceEdge(-delta.y,a.y-.025f,ref from,ref to)&&ClipGuidanceEdge(delta.y,.975f-a.y,ref from,ref to);
        }
        private static bool ClipGuidanceEdge(float p,float q,ref float from,ref float to)
        {
            if(Mathf.Abs(p)<.000001f)return q>=0;float r=q/p;
            if(p<0){if(r>to)return false;from=Mathf.Max(from,r);}else{if(r<from)return false;to=Mathf.Min(to,r);}return true;
        }
    }
}

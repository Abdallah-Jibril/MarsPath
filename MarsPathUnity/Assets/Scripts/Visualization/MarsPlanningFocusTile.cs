using System;
using UnityEngine;

namespace MarsPath
{
    /// <summary>A bounded view-centred measured mesh, replacing the overview
    /// triangles only under close inspection. Never derives height from imagery.</summary>
    public sealed class MarsPlanningFocusTile:MonoBehaviour
    {
        private MarsPathDemoBootstrap owner;
        private MarsPathCameraRig rig;
        private MissionData mission;
        private Action changed;
        private Material overview,material;
        private Mesh coarse,mesh;
        private GameObject tile;
        private Vector3[] coarseVertices,vertices;
        private int coarseResolution,resolution,revision;
        private float coarseSize,minimumX,minimumZ,size,lastInput,lastBuild=-10,observedDistance;
        private Vector3 observedTarget;
        private Quaternion observedRotation;
        private bool dirty=true;
        private static MarsPlanningFocusTile active;
        public static int VisibleVertexCount=>active!=null&&active.tile!=null?active.vertices.Length:0;
        public static float DisplayCellMeters=>active!=null&&active.tile!=null?active.size/(active.resolution-1)*MarsRegionalTerrain.MetersPerUnit:0;
        public static float OverviewCellMeters=>active!=null?active.coarseSize/(active.coarseResolution-1)*MarsRegionalTerrain.MetersPerUnit:0;
        public void Initialize(MarsPathDemoBootstrap source,MarsPathCameraRig cameraRig,Action refreshed)
        {
            owner=source;rig=cameraRig;mission=source.ActiveMission;changed=refreshed;
            coarse=GetComponent<MeshFilter>().sharedMesh;overview=GetComponent<MeshRenderer>().sharedMaterial;
            coarseVertices=coarse.vertices;coarseResolution=Mathf.RoundToInt(Mathf.Sqrt(coarseVertices.Length));
            coarseSize=coarseVertices[coarseResolution-1].x-coarseVertices[0].x;
            observedDistance=rig.PlanningDistance;observedTarget=rig.PlanningTarget;revision=MarsRouteFacts.SourceRevision;
            observedRotation=rig.PlanningCamera.transform.rotation;
            lastInput=Time.unscaledTime;active=this;
        }
        private bool PlannerActive=>owner!=null&&mission==owner.ActiveMission&&!owner.IsSurfaceMissionStarted&&!owner.GlobalView.IsVisible&&!owner.IsTerrainPreparing;
        private void Update()
        {
            if(!PlannerActive){ClearTile();return;}
            Camera camera=rig.PlanningCamera;
            if(camera==null)return;
            bool moved=(observedTarget-rig.PlanningTarget).sqrMagnitude>.0001f||Mathf.Abs(observedDistance-rig.PlanningDistance)>.001f||
                Quaternion.Angle(observedRotation,camera.transform.rotation)>.2f;
            if(moved){lastInput=Time.unscaledTime;observedTarget=rig.PlanningTarget;observedDistance=rig.PlanningDistance;observedRotation=camera.transform.rotation;dirty=true;}
            int current=MarsRouteFacts.SourceRevision;if(current!=revision){revision=current;dirty=true;}
            if(!NeedsTile()){ClearTile();return;}
            if(dirty&&Time.unscaledTime-lastInput>.35f&&Time.unscaledTime-lastBuild>.5f)RebuildNow();
        }
        public void RebuildNow()
        {
            if(!PlannerActive||!NeedsTile())return;
            Camera camera=rig.PlanningCamera;
            float tan=Mathf.Tan(camera.fieldOfView*Mathf.Deg2Rad*.5f);
            float aspect=rig.PlanningAspect;
            Vector3 target=rig.PlanningTarget;
            GeoPoint focus=MarsRegionalTerrain.ToGeo(target.x,target.z);
            float sourceSpacing=MarsRegionalTerrain.SourceSpacingMeters(focus);
            float half=rig.PlanningDistance*tan*aspect;
            // Use actual ground intersections rather than the old blanket 1.7x
            // multiplier. Keep a bounded fine-source patch even in the overview
            // so a 20 m crater is not replaced by kilometre-wide triangles.
            foreach(Vector2 corner in new[]{new Vector2(0,0),new Vector2(1,0),new Vector2(0,1),new Vector2(1,1)})
            {
                Ray ray=camera.ViewportPointToRay(corner);
                if(ray.direction.y>=-.0001f)continue;
                float rayDistance=(target.y-ray.origin.y)/ray.direction.y;
                if(rayDistance<=0)continue;Vector3 p=ray.GetPoint(rayDistance);
                half=Mathf.Max(half,Mathf.Abs(p.x-target.x),Mathf.Abs(p.z-target.z));
            }
            float metresPerUnit=MarsRegionalTerrain.MetersPerUnit;
            bool headset=Application.platform==RuntimePlatform.Android;
            bool nativeJezero=MarsMissionZone.IsMission(mission)&&sourceSpacing<=1.1f;
            int maximumResolution=nativeJezero?(headset?449:577):(rig.PlanningInspectionActive?385:257);
            float maximumPhysicalSpan=Mathf.Clamp(sourceSpacing*1024,2000,32000);
            // Keep a full native-grid patch on the inspected ground or short
            // traverses. The surrounding corridor remains visible at its own LOD.
            if(nativeJezero&&(rig.PlanningInspectionActive||rig.PlanningDistance*metresPerUnit<=410f))
                maximumPhysicalSpan=(maximumResolution-1)*sourceSpacing;
            half=Mathf.Clamp(Mathf.Min(half*1.08f,maximumPhysicalSpan*.5f/metresPerUnit),.02f,coarseSize*.4f);
            minimumX=Mathf.Clamp(target.x-half,coarseVertices[0].x,coarseVertices[coarseResolution-1].x-half*2);
            minimumZ=Mathf.Clamp(target.z-half,coarseVertices[0].z,coarseVertices[(coarseResolution-1)*coarseResolution].z-half*2);
            size=half*2;
            resolution=Mathf.Clamp(Mathf.CeilToInt(size*metresPerUnit/Mathf.Max(1,sourceSpacing))+1,65,maximumResolution);
            if(tile==null)
            {
                tile=new GameObject("View-focused measured terrain");tile.transform.SetParent(transform,false);
                tile.AddComponent<MeshFilter>();tile.AddComponent<MeshRenderer>();
                material=new Material(overview){name="Native-source planning close-up"};
                material.SetFloat("_DetailCutoutEnabled",0);tile.GetComponent<MeshRenderer>().sharedMaterial=material;
                tile.GetComponent<MeshRenderer>().shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;
                tile.GetComponent<MeshRenderer>().receiveShadows=false;
            }
            // Keep the current registered source image, not an old material copy.
            material.CopyPropertiesFromMaterial(overview);material.SetFloat("_DetailCutoutEnabled",0);
            vertices=new Vector3[resolution*resolution];var uv=new Vector2[vertices.Length];var colors=new Color[vertices.Length];var triangles=new int[(resolution-1)*(resolution-1)*6];int t=0;
            for(int z=0;z<resolution;z++)for(int x=0;x<resolution;x++)
            {
                int k=z*resolution+x;float px=minimumX+size*x/(resolution-1),pz=minimumZ+size*z/(resolution-1);
                float fine=MarsRegionalTerrain.SampleVisualHeight(px,pz),edge=Mathf.Min(Mathf.Min(x,z),Mathf.Min(resolution-1-x,resolution-1-z));
                float blend=Mathf.SmoothStep(0,1,Mathf.Clamp01(edge/3));
                float height = edge >= 3 ? fine : Mathf.Lerp(CoarseHeight(px,pz),fine,blend);
                vertices[k]=new Vector3(px,height,pz);colors[k]=Color.white;
                GeoPoint p=MarsRegionalTerrain.ToGeo(px,pz);uv[k]=MarsGeospatial.ToEquirectangularUv(p.latitude,p.longitude);
                if(x==resolution-1||z==resolution-1)continue;
                triangles[t++]=k;triangles[t++]=k+resolution;triangles[t++]=k+1;triangles[t++]=k+1;triangles[t++]=k+resolution;triangles[t++]=k+resolution+1;
            }
            if(mesh!=null)Destroy(mesh);
            mesh=new Mesh{name="Measured close-up / "+DisplayCellMeters.ToString("0.#")+" m display",indexFormat=UnityEngine.Rendering.IndexFormat.UInt32,vertices=vertices,uv=uv,uv2=uv,colors=colors,triangles=triangles};
            mesh.RecalculateNormals();mesh.RecalculateBounds();tile.GetComponent<MeshFilter>().sharedMesh=mesh;
            overview.SetVector("_PlanningDetailRect",new Vector4(minimumX,minimumZ,size,size));overview.SetFloat("_DetailCutoutEnabled",1);
            lastBuild=Time.unscaledTime;dirty=false;active=this;changed?.Invoke();
        }
        private bool NeedsTile()
        {
            if(rig.PlanningDistance<=32)return true;
            Vector3 target=rig.PlanningTarget;
            float source=MarsRegionalTerrain.SourceSpacingMeters(MarsRegionalTerrain.ToGeo(target.x,target.z));
            float visible=MarsAdaptivePlanningTerrain.CellMetersAt(target.x,target.z);
            if(visible<=0)visible=OverviewCellMeters;
            return source<=100&&visible>source*3;
        }
        private float CoarseHeight(float x,float z)
        {
            if(MarsAdaptivePlanningTerrain.TryRenderedHeight(x,z,out float adaptive))return adaptive;
            return TriangleHeight(coarseVertices,coarseResolution,coarseVertices[0].x,coarseVertices[0].z,coarseSize,x,z);
        }
        private static float TriangleHeight(Vector3[] data,int count,float minX,float minZ,float span,float x,float z)
        {
            float px=Mathf.Clamp((x-minX)/span*(count-1),0,count-1.0001f),pz=Mathf.Clamp((z-minZ)/span*(count-1),0,count-1.0001f);
            int ix=(int)px,iz=(int)pz;float u=px-ix,v=pz-iz;int k=iz*count+ix;
            float a=data[k].y,b=data[k+1].y,c=data[k+count].y,d=data[k+count+1].y;
            return u+v<=1?a+(b-a)*u+(c-a)*v:d+(c-d)*(1-u)+(b-d)*(1-v);
        }
        public static bool TryRenderedHeight(float x,float z,out float height)
        {
            height=0;var a=active;
            if(a==null||a.tile==null||!a.PlannerActive||x<a.minimumX||x>a.minimumX+a.size||z<a.minimumZ||z>a.minimumZ+a.size)return false;
            height=TriangleHeight(a.vertices,a.resolution,a.minimumX,a.minimumZ,a.size,x,z);return true;
        }
        private void ClearTile()
        {
            if(tile==null)return;
            if(overview!=null)overview.SetFloat("_DetailCutoutEnabled",0);
            Destroy(tile);Destroy(mesh);Destroy(material);tile=null;mesh=null;material=null;
            dirty=true;changed?.Invoke();
        }
        private void OnDisable(){ClearTile();if(active==this)active=null;}
        private void OnDestroy(){ClearTile();}
    }
}

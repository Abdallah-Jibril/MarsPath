using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace MarsPath
{
    /// <summary>View-driven NASA MRO CTX V01 image LOD. Images refine concurrently
    /// with measured heights and never replace the retained regional overview.</summary>
    public sealed class MarsPlanningDetailStream:MonoBehaviour
    {
        private const float IdleSeconds=.4f,RequestInterval=1.8f,RetrySeconds=25;
        private MarsPathDemoBootstrap owner;private MarsPathCameraRig rig;private MissionData mission;private Action refreshMaterials;
        private Vector3 observedTarget;private Quaternion observedRotation;private float observedDistance,lastInput,lastRequest=-100;
        private bool active,viewChanged,loading,matchingLoading,overviewRequested;private int ticket,boundRevision=-1,boundOfficialRevision=-1;
        private readonly Dictionary<string,float> attempts=new Dictionary<string,float>();
        public string Status{get;private set;}="";
        public bool IsLoading=>loading;
        public float VisibleCoverageFraction{get;private set;}
        public float DesiredImageSpacingMeters{get;private set;}

        public void Initialize(MarsPathDemoBootstrap bootstrap,MarsPathCameraRig cameraRig,Action refresh)
        {ticket++;owner=bootstrap;rig=cameraRig;mission=owner?.ActiveMission;refreshMaterials=refresh;active=false;viewChanged=true;loading=false;matchingLoading=false;overviewRequested=false;attempts.Clear();boundRevision=boundOfficialRevision=-1;Status="";}
        public void Configure(MarsPathDemoBootstrap bootstrap,MarsPathCameraRig cameraRig,Action refresh)=>Initialize(bootstrap,cameraRig,refresh);
        private bool PlannerActive()=>isActiveAndEnabled&&owner!=null&&rig!=null&&rig.enabled&&mission!=null&&owner.ActiveMission==mission&&!owner.IsSurfaceMissionStarted&&!owner.IsTerrainPreparing&&owner.GlobalView!=null&&!owner.GlobalView.IsVisible&&!owner.GlobalView.IsBusy;
        private void Update()
        {
            if(!PlannerActive()){if(active){ticket++;active=false;viewChanged=true;}return;}
            Camera camera=rig.PlanningCamera;if(camera==null)return;
            if(MarsMissionZone.IsMission(mission))
            {MarsOfficialTerrain.ActivateCachedImagery(MarsRegionalTerrain.ToGeo(rig.PlanningTarget.x,rig.PlanningTarget.z));Status="Offline HiRISE mission-zone imagery";Rebind();return;}
            // Explicit diagnostic/offline mode suppresses new network jobs;
            // ordinary height refinement in progress does not block image LOD.
            if(!owner.RefinementEnabled){Rebind();return;}
            if(!active){active=true;Observe(camera);viewChanged=true;lastInput=Time.unscaledTime-IdleSeconds;}
            if(!overviewRequested){overviewRequested=true;StartCoroutine(LoadOverview(ticket));}
            float movement=Vector2.Distance(new Vector2(observedTarget.x,observedTarget.z),new Vector2(rig.PlanningTarget.x,rig.PlanningTarget.z));
            bool moved=movement>.00001f||Mathf.Abs(Mathf.Log(Mathf.Max(.0001f,rig.PlanningDistance)/Mathf.Max(.0001f,observedDistance)))>.001f||Quaternion.Angle(observedRotation,camera.transform.rotation)>.08f;
            if(moved){Observe(camera);viewChanged=true;lastInput=Time.unscaledTime;}
            Rebind();if(!viewChanged||Time.unscaledTime-lastInput<IdleSeconds)return;
            Footprint(camera,out GeoPoint center,out float widthKm,out float heightKm,out float desired);
            QueueMatchingPhotograph(center,Mathf.Max(widthKm,heightKm));
            if(loading||Time.unscaledTime-lastRequest<RequestInterval)return;
            DesiredImageSpacingMeters=desired;
            MarsOfficialTerrain.ActivateCachedImagery(center);
            MarsCtxImageryCache.ActivateViewAt(center,Mathf.Max(widthKm,heightKm));MarsCtxImageryCache.ActivateAt(center);Rebind();
            int covered=0;for(int y=-1;y<=1;y++)for(int x=-1;x<=1;x++)
            {GeoPoint p=MarsGeospatial.Destination(center,Mathf.Atan2(x*widthKm,y*heightKm)*Mathf.Rad2Deg,Mathf.Sqrt(x*x*widthKm*widthKm+y*y*heightKm*heightKm)*.5f);if(MarsCtxImageryCache.Covers(p,desired*1.5f))covered++;}
            VisibleCoverageFraction=covered/9f;
            if(VisibleCoverageFraction>=.75f){viewChanged=false;Status=MarsCtxImageryCache.SourceAt(center);return;}
            float footprint=Mathf.Max(widthKm,heightKm)*1.18f;
            int band=Mathf.Max(0,Mathf.CeilToInt(Mathf.Log(Mathf.Max(.65f,footprint)/.65f,2)));
            float size=.65f*Mathf.Pow(2,band),latStep=size*.12f*180/(Mathf.PI*(float)MarsRegionalTerrain.RadiusMeters/1000);
            int lat=Mathf.RoundToInt(center.latitude/latStep);float latitude=Mathf.Clamp(lat*latStep,-89.97f,89.97f),lonStep=latStep/Mathf.Max(.05f,Mathf.Cos(latitude*Mathf.Deg2Rad));
            int lon=Mathf.RoundToInt(center.longitude/lonStep);string key=band+"_"+lat+"_"+lon;
            if(attempts.TryGetValue(key,out float previous)&&Time.unscaledTime-previous<RetrySeconds)return;
            if(attempts.Count>96)attempts.Clear();attempts[key]=Time.unscaledTime;
            var focus=new GeoPoint{latitude=latitude,longitude=MarsGeospatial.NormalizeLongitude(lon*lonStep)};
            loading=true;lastRequest=Time.unscaledTime;viewChanged=false;StartCoroutine(LoadDetail(focus,size,ticket,rig.PlanningTarget,rig.PlanningDistance));
        }
        private void Observe(Camera camera){observedTarget=rig.PlanningTarget;observedRotation=camera.transform.rotation;observedDistance=rig.PlanningDistance;}
        private void Footprint(Camera camera,out GeoPoint center,out float widthKm,out float heightKm,out float desired)
        {
            float pixels=Mathf.Max(1,camera.pixelWidth),height=2*rig.PlanningDistance*Mathf.Tan(camera.fieldOfView*Mathf.Deg2Rad*.5f),width=height*camera.aspect;
            float metres=MarsRegionalTerrain.MetersPerUnit,pitch=Mathf.Max(.25f,Mathf.Abs(Mathf.Sin(camera.transform.eulerAngles.x*Mathf.Deg2Rad)));
            center=MarsRegionalTerrain.ToGeo(rig.PlanningTarget.x,rig.PlanningTarget.z);widthKm=width*metres/1000;heightKm=height/pitch*metres/1000;desired=Mathf.Max(5,width*metres/pixels*.8f);
        }
        private IEnumerator LoadOverview(int version)
        {
            var center=mission.regionCenter;float extent=Mathf.Max(6,mission.regionSpanKm*1.25f);
            Func<bool> cancelled=()=>version!=ticket||!PlannerActive();
            yield return MarsCtxImageryCache.PrepareWindow(center,extent,22,cancelled,false);
            if(!cancelled()){MarsCtxImageryCache.ActivateOverviewAt(center,extent);MarsCtxImageryCache.ActivateAt(MarsRegionalTerrain.ToGeo(rig.PlanningTarget.x,rig.PlanningTarget.z));Rebind();viewChanged=true;}
        }
        private IEnumerator LoadDetail(GeoPoint focus,float extent,int version,Vector3 target,float distance)
        {
            Func<bool> cancelled=()=>version!=ticket||!PlannerActive()||Vector2.Distance(new Vector2(target.x,target.z),new Vector2(rig.PlanningTarget.x,rig.PlanningTarget.z))*MarsRegionalTerrain.MetersPerUnit>extent*350||Mathf.Abs(Mathf.Log(Mathf.Max(.0001f,rig.PlanningDistance)/Mathf.Max(.0001f,distance)))>Mathf.Log(2);
            try
            {
                yield return MarsCtxImageryCache.PrepareWindow(focus,extent,18,cancelled,true,(p,message)=>Status=message);
                if(!cancelled()){GeoPoint current=MarsRegionalTerrain.ToGeo(rig.PlanningTarget.x,rig.PlanningTarget.z);MarsCtxImageryCache.ActivateViewAt(current,extent/1.18f);MarsCtxImageryCache.ActivateAt(current);Status=MarsCtxImageryCache.SourceAt(current);Rebind();}
            }
            finally{if(version==ticket){loading=false;viewChanged=true;}}
        }
        private void QueueMatchingPhotograph(GeoPoint focus,float extent)
        {
            if(matchingLoading)return;
            string key="ortho_"+Mathf.RoundToInt(focus.latitude*1000)+"_"+Mathf.RoundToInt(focus.longitude*1000);
            if(attempts.TryGetValue(key,out float previous)&&Time.unscaledTime-previous<RetrySeconds)return;
            if(!MarsOfficialTerrain.NeedsMatchingImagery(focus))return;
            if(attempts.Count>96)attempts.Clear();attempts[key]=Time.unscaledTime;matchingLoading=true;
            StartCoroutine(LoadMatchingPhotograph(focus,ticket,rig.PlanningTarget,rig.PlanningDistance,extent));
        }
        private IEnumerator LoadMatchingPhotograph(GeoPoint focus,int version,Vector3 target,float distance,float extent)
        {
            Func<bool> cancelled=()=>version!=ticket||!PlannerActive()||Vector2.Distance(new Vector2(target.x,target.z),new Vector2(rig.PlanningTarget.x,rig.PlanningTarget.z))*MarsRegionalTerrain.MetersPerUnit>Mathf.Max(260,extent*350)||Mathf.Abs(Mathf.Log(Mathf.Max(.0001f,rig.PlanningDistance)/Mathf.Max(.0001f,distance)))>Mathf.Log(2);
            try
            {
                // A separate bounded photograph request keeps the overview and
                // camera CTX jobs usable while a native matching ortho arrives.
                yield return MarsOfficialTerrain.PrepareMatchingImagery(focus,8,cancelled);
                if(!cancelled()){MarsOfficialTerrain.ActivateCachedImagery(MarsRegionalTerrain.ToGeo(rig.PlanningTarget.x,rig.PlanningTarget.z));Rebind();}
            }
            finally{if(version==ticket)matchingLoading=false;}
        }
        private void Rebind(){if(!PlannerActive()||boundRevision==MarsCtxImageryCache.Revision&&boundOfficialRevision==MarsOfficialTerrain.ImageryRevision)return;boundRevision=MarsCtxImageryCache.Revision;boundOfficialRevision=MarsOfficialTerrain.ImageryRevision;refreshMaterials?.Invoke();}
        private void OnDisable(){ticket++;active=false;viewChanged=true;loading=matchingLoading=false;Status="";}
    }
}


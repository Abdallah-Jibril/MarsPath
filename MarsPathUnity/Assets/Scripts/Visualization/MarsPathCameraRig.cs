using UnityEngine;

namespace MarsPath
{
    public class MarsPathCameraRig : MonoBehaviour
    {
        private Camera cameraComponent;
        private MarsQuestHeadTracking questHeadTracking;
        private float yaw = 28f;
        private float pitch = 48f;
        private float distance = 100f;
        private Vector3 target = new Vector3(0f, 0f, 0f);
        private bool missionMode;
        private Transform astronaut;
        private bool helmetView=true;
        private bool remoteHeadPose;
        private Vector3 remoteEyeOffset;
        private Quaternion remoteEyeRotation;
        private float? remoteLookHeading;
        private const float NeutralHelmetPitch=3;
        private float helmetPitch=NeutralHelmetPitch;
        public bool HelmetView=>missionMode&&helmetView;
        public bool HelmetSelected=>helmetView;
        public static bool ActiveHelmet { get; private set; }
        public float PlanningDistance => distance;
        public bool PlanningInspectionActive { get; private set; }
        private const float PlanningZoomOutLimit = 520f;
        private const float JezeroOverviewZoomLimit = 2.8f;
        private float PhysicalPlanningMinimum=>MarsRegionalTerrain.Current!=null?Mathf.Clamp(180f/Mathf.Max(1,MarsRegionalTerrain.MetersPerUnit),0.2f,12f):12f;
        public float MinimumPlanningDistance
        {
            get
            {
                if (!MarsMissionZone.IsMission(MarsRegionalTerrain.Current)) return PhysicalPlanningMinimum;
                float metresPerUnit=Mathf.Max(1,MarsRegionalTerrain.MetersPerUnit);
                // Allow a meaningful zoom even when the fitted view is already close, as at Belva.
                float physicalMinimum=Mathf.Min(fittedDistance*.60f,180f/metresPerUnit);
                return PlanningInspectionActive?physicalMinimum:Mathf.Max(physicalMinimum,fittedDistance/JezeroOverviewZoomLimit);
            }
        }
        public bool CanZoomPlanningIn => distance > MinimumPlanningDistance + .001f;
        public bool CanZoomPlanningOut => distance < PlanningZoomOutLimit - .001f;
        public float PlanningZoom => fittedDistance / Mathf.Max(.001f,distance);
        public Vector3 PlanningTarget => target;
        internal Camera PlanningCamera => questHeadTracking != null ? questHeadTracking.PlanningCamera : cameraComponent;
        internal float PlanningAspect => questHeadTracking != null ? PlanningCamera.aspect :
            Screen.width * cameraComponent.rect.width / Mathf.Max(1, Screen.height * cameraComponent.rect.height);
        internal bool QuestWalkTrackingReady => questHeadTracking != null && questHeadTracking.WalkTrackingReady;
        internal float? QuestLookHeading => questHeadTracking?.LookHeading;
        public struct PlanningView{public float yaw,pitch,distance;public Vector3 target;public bool inspection;public Vector4 overviewOrbit;public Vector3 overviewTarget;}
        private PlanningView inspectionOverview;
        public PlanningView CapturePlanningView()=>new PlanningView{yaw=yaw,pitch=pitch,distance=distance,target=target,inspection=PlanningInspectionActive,
            overviewOrbit=new Vector4(inspectionOverview.yaw,inspectionOverview.pitch,inspectionOverview.distance,0),overviewTarget=inspectionOverview.target};
        public void RestorePlanningView(PlanningView view)
        {yaw=view.yaw;pitch=view.pitch;distance=view.distance;target=view.target;PlanningInspectionActive=view.inspection;
            if(view.inspection)inspectionOverview=new PlanningView{yaw=view.overviewOrbit.x,pitch=view.overviewOrbit.y,distance=view.overviewOrbit.z,target=view.overviewTarget};ApplyOrbit();}
        private Vector3 lastMouse;
        private float lastPlanningClick=-10;
        private Vector2 lastPlanningClickPosition;
        private Vector2 planningPressPosition;
        private float planningPressTime;
        private bool planningPointerCaptured;
        private bool planningDragging;
        private bool planningPanOnly;
        private Bounds routeBounds;
        private Rect planningPanBounds = Rect.MinMaxRect(-170, -170, 170, 170);
        private float fittedDistance=100;
        private float fittedYaw=28;
        private float fittedPitch=43;
        private bool hasFrame;
        private float followYaw,followDistance=4.7f,followHeight=2.15f;
        public static readonly Rect PlanningArea=new Rect(375,105,1205,535);
        public static readonly Rect PlanningZoomArea=new Rect(1407,119,157,46);

        public void Initialize(Camera targetCamera)
        {
            cameraComponent = targetCamera;
            if (Application.platform == RuntimePlatform.Android)
            {
                questHeadTracking = targetCamera.gameObject.AddComponent<MarsQuestHeadTracking>();
                questHeadTracking.Initialize(GetComponent<MarsPathDemoBootstrap>(), this);
            }
            cameraComponent.farClipPlane=50000;
            cameraComponent.nearClipPlane=0.15f;
            MarsCameraBackground.Apply(cameraComponent,true);
            SetPlanningViewport();
            ApplyOrbit();
        }

        public void SetMissionMode(bool enabled, Transform astronautTarget, bool keepRemotePose = false)
        {
            missionMode = enabled;
            if (!enabled && !keepRemotePose) remoteHeadPose = false;
            astronaut = astronautTarget;
            if (questHeadTracking == null) lastMouse=Input.mousePosition;
            else if (enabled) questHeadTracking.HidePlanner();
            else questHeadTracking.ClearAnchor();
            planningPointerCaptured = false;
            lastPlanningClick = -10;
            if(enabled)cameraComponent.rect=new Rect(0,0,1,1);
            else {SetPlanningViewport();ApplyOrbit();}
            cameraComponent.farClipPlane=enabled?110000:50000;
            MarsCameraBackground.Apply(cameraComponent,!enabled);
            RenderSettings.fog=enabled;
            RenderSettings.fogMode=FogMode.Linear;RenderSettings.fogStartDistance=8000;RenderSettings.fogEndDistance=90000;
            RenderSettings.fogColor=new Color(0.78f,0.67f,0.57f);
            ApplyViewVisibility();
        }

        public void ResetPlanningView(){PlanningInspectionActive=false;yaw=hasFrame?fittedYaw:28;pitch=hasFrame?fittedPitch:43;target=hasFrame?routeBounds.center:Vector3.zero;distance=hasFrame?fittedDistance:110;SetPlanningViewport();ApplyOrbit();}
        private void SetPlanningViewport(){PlanningCamera.rect=questHeadTracking!=null?new Rect(0,0,1,1):new Rect(PlanningArea.x/1600f,1-PlanningArea.yMax/900f,PlanningArea.width/1600f,PlanningArea.height/900f);}
        public void FrameRoutes(MissionData mission)
        {
            bool measuredZone = MarsMissionZone.IsMission(mission) && MarsMissionZone.Available;
            planningPanBounds = Rect.MinMaxRect(-170, -170, 170, 170);
            if (measuredZone)
            {
                Vector2 southwest = MarsRegionalTerrain.ToWorld(MarsMissionZone.AtNormalized(0, 0));
                Vector2 northeast = MarsRegionalTerrain.ToWorld(MarsMissionZone.AtNormalized(1, 1));
                planningPanBounds = Rect.MinMaxRect(southwest.x, southwest.y, northeast.x, northeast.y);
            }
            bool first=true;Bounds bounds=new Bounds();
            var framePoints=new System.Collections.Generic.List<Vector3>();
            foreach(RouteData route in mission.routes)
            {
                if(!route.available||route.waypoints==null)continue;
                foreach(RoutePoint point in route.waypoints)
                {float x=point.x*60,z=point.z*60;Vector3 p=MarsTerrainGenerator.SurfacePoint(x,z,0.1f);framePoints.Add(p);if(first){bounds=new Bounds(p,Vector3.zero);first=false;}else bounds.Encapsulate(p);}
            }
            if(first)return;
            bounds.Expand(new Vector3(Mathf.Max(4,bounds.size.x*0.10f),Mathf.Max(2,bounds.size.y*0.12f),Mathf.Max(4,bounds.size.z*0.10f)));
            Vector2 journey=Vector2.zero;
            if(mission.generatedRegion&&MarsRegionalTerrain.Current==mission&&mission.currentLocation!=null&&mission.destination!=null)
                journey=MarsRegionalTerrain.ToWorld(mission.destination)-MarsRegionalTerrain.ToWorld(mission.currentLocation);
            fittedYaw=journey.sqrMagnitude>.000001f?Mathf.Atan2(-journey.y,journey.x)*Mathf.Rad2Deg:28;
            fittedPitch = measuredZone ? 50f : 43f;
            if (measuredZone) fittedYaw -= 22f;
            PlanningInspectionActive=false;routeBounds=bounds;hasFrame=true;yaw=fittedYaw;pitch=fittedPitch;target=bounds.center;SetPlanningViewport();
            Quaternion inverse=Quaternion.Inverse(Quaternion.Euler(pitch,yaw,0));
            Camera planCamera = PlanningCamera;
            float tanV=Mathf.Tan(planCamera.fieldOfView*Mathf.Deg2Rad/2)*0.90f;
            float aspect=PlanningAspect;
            float tanH=tanV*aspect,required=15;
            // Frame actual paths, not empty corners of their world-axis box.
            // A rotated long route otherwise wasted most of a wide viewport.
            foreach(Vector3 point in framePoints)
            {Vector3 v=inverse*(point-target);required=Mathf.Max(required,Mathf.Abs(v.x)/tanH-v.z,Mathf.Abs(v.y)/tanV-v.z);}
            distance=fittedDistance=Mathf.Clamp(required+3,26,300);ApplyOrbit();
        }
        public bool RoutesInsideViewport(MissionData mission)
        {
            foreach(RouteData route in mission.routes)if(route.available)foreach(RoutePoint point in route.waypoints)
            {float x=point.x*60,z=point.z*60;Vector3 v=PlanningCamera.WorldToViewportPoint(MarsTerrainGenerator.SurfacePoint(x,z,0.1f));if(v.z<=0||v.x<0.02f||v.x>0.98f||v.y<0.02f||v.y>0.98f)return false;}
            return true;
        }
        public void SetPlanningView(float heading,float angle,float zoom){yaw=heading;pitch=angle;distance=Mathf.Clamp(zoom,MinimumPlanningDistance,PlanningZoomOutLimit);ApplyOrbit();}
        public void TogglePlanningInspection()
        {
            if(missionMode||MarsRegionalTerrain.Current==null)return;
            if(PlanningInspectionActive){PlanningView previous=inspectionOverview;PlanningInspectionActive=false;RestorePlanningView(previous);return;}
            InspectPlanningAt(target);
        }
        public void InspectPlanningAt(Vector3 point)
        {
            if(missionMode||MarsRegionalTerrain.Current==null)return;
            if(!PlanningInspectionActive)inspectionOverview=CapturePlanningView();
            PlanningInspectionActive=true;
            target=new Vector3(point.x,MarsRegionalTerrain.SampleVisualHeight(point.x,point.z),point.z);
            float spacing=MarsRegionalTerrain.SourceSpacingMeters(MarsRegionalTerrain.ToGeo(point.x,point.z));
            bool measuredZone = MarsMissionZone.IsMission(MarsRegionalTerrain.Current);
            pitch = measuredZone ? 44f : 40f;
            float angle = pitch * Mathf.Deg2Rad;
            float halfFov = PlanningCamera.fieldOfView * Mathf.Deg2Rad / 2;
            float distanceMetres;
            if (measuredZone)
            {
                // The top and bottom rays meet the ground at different
                // distances in an oblique view. Fit their actual separation.
                float groundSpanPerUnit = Mathf.Sin(angle)
                    * (1 / Mathf.Tan(angle - halfFov) - 1 / Mathf.Tan(angle + halfFov));
                distanceMetres = 1100f / groundSpanPerUnit;
            }
            else
            {
                float groundHeight = Mathf.Clamp(spacing * 384, 6000, 16000);
                distanceMetres = groundHeight * .5f * Mathf.Sin(angle) / Mathf.Tan(halfFov);
            }
            float minimum=MinimumPlanningDistance;
            distance = Mathf.Clamp(distanceMetres / MarsRegionalTerrain.MetersPerUnit,minimum,Mathf.Max(minimum,Mathf.Min(fittedDistance,32)));
            ApplyOrbit();
        }
        public bool TryPlanningSurfaceAt(Vector2 screenPixels,out Vector3 point)
        {
            point=Vector3.zero;
            Camera planCamera = PlanningCamera;
            if(missionMode||MarsRegionalTerrain.Current==null||!planCamera.pixelRect.Contains(screenPixels))return false;
            Ray ray=planCamera.ScreenPointToRay(screenPixels);
            if(ray.direction.y>=-.00001f)return false;
            float plane=(target.y-ray.origin.y)/ray.direction.y;
            if(plane<=0)return false;
            float maximum=Mathf.Min(planCamera.farClipPlane,Mathf.Max(plane*2,distance*3));
            float priorT=0,prior=ray.origin.y-MarsRegionalTerrain.SampleVisualHeight(ray.origin.x,ray.origin.z);
            if(prior<=0)return false;
            // Find the first measured surface crossing, then refine its bracket.
            // No planning MeshCollider or unbounded ray march is required.
            for(int i=1;i<=96;i++)
            {
                float fraction=i/96f,t=maximum*fraction*fraction;Vector3 sample=ray.GetPoint(t);
                float height=sample.y-MarsRegionalTerrain.SampleVisualHeight(sample.x,sample.z);
                if(!float.IsNaN(height)&&!float.IsInfinity(height)&&height<=0&&prior>0)
                {
                    float low=priorT,high=t;
                    for(int step=0;step<18;step++)
                    {float middle=(low+high)*.5f;Vector3 p=ray.GetPoint(middle);if(p.y>MarsRegionalTerrain.SampleVisualHeight(p.x,p.z))low=middle;else high=middle;}
                    point=ray.GetPoint((low+high)*.5f);point.y=MarsRegionalTerrain.SampleVisualHeight(point.x,point.z);return true;
                }
                priorT=t;prior=height;
            }
            return false;
        }
        public void SnapToMission(Transform actor)
        {
            cameraComponent.rect=new Rect(0,0,1,1);
            if (questHeadTracking != null)
            {
                UpdateQuestAnchor(actor);
                return;
            }
            cameraComponent.transform.position=helmetView?EyePosition(actor):FollowPosition(actor);
            cameraComponent.transform.rotation=helmetView?EyeRotation(actor):Quaternion.LookRotation(actor.position+Vector3.up*1.15f-cameraComponent.transform.position);
        }
        private Vector3 EyePosition(Transform actor)=>remoteHeadPose ? actor.TransformPoint(remoteEyeOffset)
            : actor.position+Vector3.up*.94f+actor.forward*.08f;
        internal bool TryQuestRouteFacing(out float signedError)
        {
            signedError = 0;
            return astronaut != null && questHeadTracking != null &&
                questHeadTracking.TryRouteFacing(astronaut.forward, out signedError);
        }
        internal bool TryCaptureQuestView(Transform actor, out Vector3 offset, out Quaternion rotation)
        {
            offset = Vector3.zero; rotation = Quaternion.identity;
            if (questHeadTracking == null || !questHeadTracking.WalkTrackingReady || actor == null) return false;
            offset = actor.InverseTransformPoint(cameraComponent.transform.position);
            rotation = Quaternion.Inverse(actor.rotation) * cameraComponent.transform.rotation;
            return true;
        }
        internal void SetRemoteHeadPose(Vector3 offset, Quaternion rotation, float? lookHeading)
        {
            remoteEyeOffset = offset;
            remoteEyeRotation = rotation.normalized;
            remoteLookHeading = lookHeading;
            remoteHeadPose = true;
        }

        internal bool TryGetRemoteLookHeading(out float heading)
        {
            heading = 0;
            return remoteHeadPose && astronaut != null &&
                MarsCompass.TryGetHeading(EyeRotation(astronaut) * Vector3.forward, out heading);
        }
        private void UpdateQuestAnchor(Transform actor)
        {
            Vector3 ground = actor.position + actor.forward * .08f;
            ground.y = MarsTerrainGenerator.SampleHeight(ground.x, ground.z);
            questHeadTracking.SetGroundAnchor(ground, Quaternion.Euler(0, actor.eulerAngles.y, 0));
        }
        private Quaternion EyeRotation(Transform actor)
        {
            if (!remoteHeadPose) return Quaternion.Euler(helmetPitch, actor.eulerAngles.y + followYaw, 0);
            Quaternion view = actor.rotation * remoteEyeRotation;
            if (remoteLookHeading is float bearing && MarsCompass.TryGetHeading(view * Vector3.forward, out float localBearing))
                view = Quaternion.AngleAxis(Mathf.DeltaAngle(localBearing, bearing), Vector3.up) * view;
            return view;
        }
        public void SetHelmetView(bool enabled)
        {helmetView=enabled;followYaw=0;helmetPitch=NeutralHelmetPitch;ApplyViewVisibility();if(missionMode&&astronaut!=null)SnapToMission(astronaut);}
        private void ApplyViewVisibility()
        {
            if(cameraComponent==null)return;
            ActiveHelmet=missionMode&&helmetView;
            if(questHeadTracking==null)cameraComponent.fieldOfView=missionMode&&helmetView?66:58;
            cameraComponent.nearClipPlane=(missionMode&&helmetView) ? 0.05f : 0.15f;
            if(astronaut!=null)foreach(Renderer renderer in astronaut.GetComponentsInChildren<Renderer>())
                renderer.shadowCastingMode=missionMode&&helmetView?UnityEngine.Rendering.ShadowCastingMode.ShadowsOnly:UnityEngine.Rendering.ShadowCastingMode.On;
        }
        public void RebaseMission(Vector3 delta){cameraComponent.transform.position+=delta;}
        private Vector3 FollowPosition(Transform actor)
        {
            Vector3 p=actor.position-(Quaternion.AngleAxis(followYaw,Vector3.up)*actor.forward)*followDistance+Vector3.up*followHeight;
            p.y=Mathf.Max(p.y,MarsTerrainGenerator.SampleHeight(p.x,p.z)+1.1f);return p;
        }
        public void ZoomPlanning(float delta)
        {
            if(delta==0)return;
            distance=Mathf.Clamp(distance*Mathf.Exp(-delta*.12f),MinimumPlanningDistance,PlanningZoomOutLimit);
            ApplyOrbit();
        }

        private void PanPlanning(Vector2 delta)
        {
            Vector3 right = Quaternion.Euler(0, yaw, 0) * Vector3.right;
            Vector3 forward = Vector3.Cross(right, Vector3.up);
            Camera planCamera = PlanningCamera;
            float worldPerPixel = 2 * distance * Mathf.Tan(planCamera.fieldOfView * Mathf.Deg2Rad / 2)
                / Mathf.Max(1, planCamera.pixelHeight);
            float groundProjection = Mathf.Max(.3f, Mathf.Sin(pitch * Mathf.Deg2Rad));
            target -= (right * delta.x + forward * (delta.y / groundProjection)) * worldPerPixel;
            target.x = Mathf.Clamp(target.x, planningPanBounds.xMin, planningPanBounds.xMax);
            target.z = Mathf.Clamp(target.z, planningPanBounds.yMin, planningPanBounds.yMax);
            target.y = MarsTerrainGenerator.SampleHeight(target.x, target.z);
        }

        internal void MoveQuestPlanningView(Vector2 orbit, Vector2 panAndZoom)
        {
            if (missionMode || (orbit.sqrMagnitude < .01f && panAndZoom.sqrMagnitude < .01f)) return;
            float dt = Mathf.Min(Time.unscaledDeltaTime, .05f);
            yaw += orbit.x * 67f * dt;
            pitch = Mathf.Clamp(pitch - orbit.y * 46f * dt, 14f, 80f);
            if (Mathf.Abs(panAndZoom.x) > .1f)
                PanPlanning(new Vector2(-panAndZoom.x * PlanningCamera.pixelWidth * dt * .6f, 0));
            distance = Mathf.Clamp(distance * Mathf.Exp(-panAndZoom.y * dt * 1.6f), MinimumPlanningDistance, PlanningZoomOutLimit);
            ApplyOrbit();
        }

        private bool HandlePlanningPointer(Vector2 mouse, Vector2 delta, bool overTerrain)
        {
            float pointerScale = Mathf.Max(.5f, Screen.height / 900f);
            if (Input.GetMouseButtonDown(0))
            {
                planningPointerCaptured = overTerrain;
                planningDragging = false;
                planningPanOnly = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
                planningPressPosition = mouse;
                planningPressTime = Time.unscaledTime;
                return planningPointerCaptured;
            }

            if (!planningPointerCaptured) return false;
            if (Input.GetMouseButton(0) && !Input.GetMouseButtonUp(0))
            {
                if (!planningDragging)
                {
                    Vector2 travel = mouse - planningPressPosition;
                    if (travel.sqrMagnitude < 25 * pointerScale * pointerScale) return true;
                    planningDragging = true;
                    lastPlanningClick = -10;
                    delta = travel;
                }
                if (delta.sqrMagnitude > 0) PanPlanning(delta);
                return true;
            }

            planningPointerCaptured = false;
            bool click = Input.GetMouseButtonUp(0) && overTerrain && !planningDragging && !planningPanOnly
                && Time.unscaledTime - planningPressTime < .32f
                && (mouse - planningPressPosition).sqrMagnitude < 25 * pointerScale * pointerScale;
            if (!click)
            {
                lastPlanningClick = -10;
                return true;
            }

            if (Time.unscaledTime - lastPlanningClick < .32f
                && (mouse - lastPlanningClickPosition).sqrMagnitude < 144 * pointerScale * pointerScale
                && TryPlanningSurfaceAt(mouse, out Vector3 focus))
            {
                InspectPlanningAt(focus);
                lastPlanningClick = -10;
                return true;
            }

            lastPlanningClick = Time.unscaledTime;
            lastPlanningClickPosition = mouse;
            return true;
        }

        private void Update()
        {
            if (cameraComponent == null || questHeadTracking != null) return;

            if (missionMode && astronaut != null)
            {
                Vector3 m=Input.mousePosition,d=m-lastMouse;lastMouse=m;
                bool overScene=m.y/Screen.height>0.13f&&m.y/Screen.height<0.78f;
                if(overScene&&Input.GetMouseButton(1)&&!Input.GetMouseButtonDown(1))
                {if(!helmetView || !remoteHeadPose)followYaw+=d.x*0.25f;if(helmetView && !remoteHeadPose)helmetPitch=Mathf.Clamp(helmetPitch-d.y*.20f,-65,65);else if(!helmetView)followHeight=Mathf.Clamp(followHeight-d.y*0.018f,1.6f,6);}
                if(overScene&&!helmetView)followDistance=Mathf.Clamp(followDistance-Input.mouseScrollDelta.y*0.55f,3.2f,12);
                if(Input.GetKeyDown(KeyCode.V))SetHelmetView(!helmetView);
                if(Input.GetKeyDown(KeyCode.R)){followYaw=0;helmetPitch=NeutralHelmetPitch;followDistance=4.7f;followHeight=2.15f;}
                return;
            }

            Vector3 mouse=Input.mousePosition;
            Vector3 delta=mouse-lastMouse;lastMouse=mouse;
            float mx=mouse.x/Screen.width*1600f, my=(1-mouse.y/Screen.height)*900f;
            Vector2 canvasPointer=new Vector2(mx,my);
            bool overTerrain=PlanningArea.Contains(canvasPointer)&&!PlanningZoomArea.Contains(canvasPointer);
            bool handled = HandlePlanningPointer(mouse, delta, overTerrain);
            if (!handled && overTerrain && Input.GetMouseButton(2) && !Input.GetMouseButtonDown(2) && delta.sqrMagnitude > 0)
                PanPlanning(delta);
            else if (!handled && overTerrain && Input.GetMouseButton(1) && !Input.GetMouseButtonDown(1))
            {
                yaw += delta.x*0.28f;
                pitch = Mathf.Clamp(pitch-delta.y*0.24f,12f,85f);
            }
            if(overTerrain)ZoomPlanning(Input.mouseScrollDelta.y);
            if(Input.GetKeyDown(KeyCode.R))ResetPlanningView();
            ApplyOrbit();
        }

        private void LateUpdate()
        {
            if(!missionMode||astronaut==null||cameraComponent==null)return;
            if (questHeadTracking != null)
            {
                UpdateQuestAnchor(astronaut);
                return;
            }
            float blend=1-Mathf.Exp(-Time.deltaTime*8);
            if(helmetView)
            {
                cameraComponent.transform.position=EyePosition(astronaut);
                cameraComponent.transform.rotation=remoteHeadPose ? EyeRotation(astronaut)
                    : Quaternion.Slerp(cameraComponent.transform.rotation,EyeRotation(astronaut),blend);
                return;
            }
            cameraComponent.transform.position=Vector3.Lerp(cameraComponent.transform.position,FollowPosition(astronaut),blend);
            cameraComponent.transform.rotation=Quaternion.Slerp(cameraComponent.transform.rotation,Quaternion.LookRotation(astronaut.position+Vector3.up*1.15f-cameraComponent.transform.position),blend);
        }

        private void ApplyOrbit()
        {
            Camera planCamera = PlanningCamera;
            if(!missionMode)
            {
                planCamera.nearClipPlane=Mathf.Clamp(distance*.01f,.001f,.15f);
                planCamera.farClipPlane=Mathf.Clamp(distance*80,100,50000);
            }
            Quaternion rotation = Quaternion.Euler(pitch, yaw, 0f);
            planCamera.transform.position = target + rotation * new Vector3(0f, 0f, -distance);
            planCamera.transform.LookAt(target);
        }
    }
}

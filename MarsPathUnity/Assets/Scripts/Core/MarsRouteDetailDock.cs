using UnityEngine;

namespace MarsPath
{
    public partial class MarsPathDemoBootstrap
    {
        private GUIStyle helmetAction,profileMetric;

        private static string FormatRouteLength(float kilometres) => kilometres < 1
            ? $"{kilometres*1000:0} m" : $"{kilometres:0.00} km";

        private void DrawSelectedRouteDock(RouteData route)
        {
            if(!route.available)
            {
                GUI.Label(new Rect(405,667,1000,31),route.displayName.ToUpperInvariant()+" / NO FEASIBLE MODELED CORRIDOR",planLabel);
                GUI.Label(new Rect(405,716,790,70),"This profile did not find a traversable path at the available elevation resolution. Choose another profile or move the endpoints; no mission can be launched from this result.",planSmall);
                return;
            }
            MarsRouteElevationProfile profile=SelectedElevationProfile;MarsRouteFacts facts=MarsRouteFacts.For(mission,route,profile);
            SyncPlanningInspectionStation(profile);
            Color routeColor=MarsPathRouteView.ParseColor(route.colorHex,UiCyan);
            if(helmetAction==null)
            {
                Texture2D amber=MarsGlobalView.RoundedGlobalSurface(new Color(1,.65f,.27f));
                Texture2D hover=MarsGlobalView.RoundedGlobalSurface(new Color(1,.79f,.48f));
                helmetAction=new GUIStyle(planButton){fontSize=22,fontStyle=FontStyle.Bold,normal={background=amber,textColor=new Color(.035f,.045f,.055f)},hover={background=hover,textColor=Color.black},active={background=amber,textColor=Color.black}};
                profileMetric=new GUIStyle(planLabel){fontSize=23};
            }
            UiFill(new Rect(400,645,1160,2),new Color(.13f,.27f,.30f));
            GUI.Label(new Rect(405,653,670,29),route.displayName.ToUpperInvariant()+" / ROUTE BRIEF",planLabel);
            string[] labels={"ROUTE LENGTH","TIME ESTIMATE","ASCENT / DESCENT","MAX TRACK GRADE"};
            string[] values={FormatRouteLength(facts.routeLengthKm),TimeLabel(facts.etaMinutes),$"+{facts.ascentMeters:0} / −{facts.descentMeters:0} m",facts.maxGradeDegrees.ToString("0.0")+"°"};
            for(int i=0;i<4;i++)
            {float x=405+i*177;GUI.Label(new Rect(x,690,172,22),labels[i],planCaption);GUI.Label(new Rect(x,715,172,32),values[i],profileMetric);if(i>0)UiFill(new Rect(x-13,694,1,49),new Color(.16f,.29f,.32f));}
            Rect graph=new Rect(470,786,625,55);
            UiFill(graph,new Color(.025f,.052f,.065f));
            float range=Mathf.Max(1,profile.maximum-profile.minimum);
            for(int i=1;i<profile.heights.Length;i++)
            {
                float x0=graph.x+(i-1)/(float)(profile.heights.Length-1)*graph.width,x1=graph.x+i/(float)(profile.heights.Length-1)*graph.width;
                float y0=graph.yMax-(profile.heights[i-1]-profile.minimum)/range*graph.height,y1=graph.yMax-(profile.heights[i]-profile.minimum)/range*graph.height;
                if(i%4==0)UiFill(new Rect(x1,y1,graph.width/profile.heights.Length*4,graph.yMax-y1),new Color(routeColor.r,routeColor.g,routeColor.b,.09f));
                UiLine(new Vector2(x0,y0),new Vector2(x1,y1),routeColor,2);
            }
            GUI.Label(new Rect(404,757,370,24),"MEASURED ELEVATION / CLICK TO INSPECT",planCaption);
            GUI.Label(new Rect(404,785,70,24),profile.maximum.ToString("0"),planCaption);GUI.Label(new Rect(404,819,70,24),profile.minimum.ToString("0"),planCaption);
            if(GUI.Button(graph,GUIContent.none,GUIStyle.none))
            {
                inspectProgress=Mathf.Clamp01((Input.mousePosition.x/Screen.width*1600f-graph.x)/graph.width);
                int point=Mathf.RoundToInt(inspectProgress*(profile.locations.Length-1));
                Vector2 local=MarsRegionalTerrain.ToWorld(profile.locations[point]);
                cameraRig.InspectPlanningAt(MarsTerrainGenerator.SurfacePoint(local.x,local.y));
            }
            float stationX=graph.x+inspectProgress*graph.width;
            UiLine(new Vector2(stationX,graph.y-4),new Vector2(stationX,graph.yMax+4),Color.white,1.5f);
            GUI.Label(new Rect(470,851,660,23),$"{inspectProgress*profile.lengthKm:0.0} km along route  ·  {profile.HeightAt(inspectProgress):0} m measured elevation  /  vertical scale 1:1",planCaption);

            if (desktopBrowsingRoutes) return;
            GUI.enabled=!helmetEntryLoading&&!budgetReplanning&&!PlanningDispatchPending;
            if(GUI.Button(new Rect(1160,655,190,34),demoPresentation?"●  2 MIN DEMO":"2 MIN DEMO",demoPresentation?planSelected:planButton))demoPresentation=true;
            if(GUI.Button(new Rect(1360,655,194,34),demoPresentation?"FULL MISSION":"●  FULL MISSION",demoPresentation?planButton:planSelected))demoPresentation=false;
            GUI.enabled=route.available&&!helmetEntryLoading&&!budgetReplanning&&!PlanningDispatchPending;
            string entry = helmetEntryLoading ? $"PREPARING {helmetEntryPercent*100:0}%" : PlanningDispatchPending
                ? "WAITING FOR QUEST" : demoPresentation ? "ENTER HELMET DEMO  ›" : "START FULL MISSION  ›";
            if(GUI.Button(new Rect(1160,706,394,79),entry,helmetAction))EnterHelmetFromPlanner();
            string scope = helmetEntryLoading ? terrainPreparationMessage : PlannerDispatchDetail ??
                (demoPresentation ? DemoScope(route) : "FULL ROUTE · BEGINS AT SELECTED START");
            GUI.Label(new Rect(1167,789,387,28),scope,new GUIStyle(planCaption){alignment=TextAnchor.MiddleCenter});
            if(GUI.Button(new Rect(1160,826,394,39),$"LAPTOP PREVIEW AT {inspectProgress*100:0}%  ›",planButton))BeginHelmetEntry(true);
            GUI.enabled=true;
        }

        private MissionData inspectedMission;
        private Vector3 inspectedTarget;
        private int inspectedRoute=-1;
        private void SyncPlanningInspectionStation(MarsRouteElevationProfile profile)
        {
            if(!cameraRig.PlanningInspectionActive)return;
            Vector3 target=cameraRig.PlanningTarget;
            int index = PlanningRouteIndex;
            if(inspectedMission==mission&&inspectedRoute==index&&(target-inspectedTarget).sqrMagnitude<.000001f)return;
            float best=float.MaxValue;int closest=0;
            for(int i=0;i<profile.locations.Length;i++)
            {
                Vector2 point=MarsRegionalTerrain.ToWorld(profile.locations[i]);
                float distance=(point-new Vector2(target.x,target.z)).sqrMagnitude;
                if(distance<best){best=distance;closest=i;}
            }
            inspectProgress=closest/(float)(profile.locations.Length-1);
            inspectedMission=mission;inspectedRoute=index;inspectedTarget=target;
        }

        private void DrawBrowseLiveStatus()
        {
            RouteData active = mission.routes[selectedRoute];
            Color accent = LiveMissionFresh ? UiCyan : UiAmber;
            PlanningCard(new Rect(1160, 668, 394, 146), accent);
            GUI.Label(new Rect(1180, 681, 353, 22), LiveMissionFresh ? "LIVE ON QUEST" : "WAITING FOR QUEST UPDATE", planCaption);
            GUI.Label(new Rect(1180, 710, 264, 30), active.displayName, planLabel);
            float progress = currentLiveState?.progress ?? routeProgress;
            GUI.Label(new Rect(1442, 710, 91, 30), $"{progress:P1}", planLabel);
            float? heading = currentLiveState?.hasLookHeading == true ? currentLiveState.lookHeading : mirroredLookHeading;
            GUI.Label(new Rect(1180, 738, 354, 21), heading is float bearing
                ? $"LOOK HEADING   {MarsCompass.Format(bearing)}" : "WAITING FOR HEAD TRACKING", planCaption);
            UiBar(new Rect(1180, 763, 354, 5), progress, accent);
            string radio = MissionRadioActivity ?? missionRadio?.MicrophoneStatus ??
                (QuestRadioConnected ? "Y  ·  HOLD TO TALK" : QuestRadioStatus);
            GUI.Label(new Rect(1180, 781, 354, 22), radio,
                new GUIStyle(planCaption) { normal = { textColor = MissionRadioAccent } });
        }

        private static void UiLine(Vector2 a,Vector2 b,Color color,float width)
        {
            Matrix4x4 old=GUI.matrix;Vector2 d=b-a;
            GUI.matrix=old*Matrix4x4.TRS(new Vector3(a.x,a.y,0),Quaternion.Euler(0,0,Mathf.Atan2(d.y,d.x)*Mathf.Rad2Deg),Vector3.one);
            UiFill(new Rect(0,-width*.5f,d.magnitude,width),color);GUI.matrix=old;
        }
        private void DrawVisorFrame()
        {
            Color edge=new Color(.02f,.033f,.04f,.72f),line=new Color(.30f,.72f,.76f,.38f);
            for(int side=0;side<2;side++)
            {
                Vector2 previous=Vector2.zero;
                for(int i=0;i<=40;i++)
                {
                    float t=i/40f,y=25+t*850;
                    float x=16+70*Mathf.Pow(Mathf.Abs(t-.5f)*2,3);
                    if(side==1)x=1600-x;
                    Vector2 p=new Vector2(x,y);
                    if(i>0){UiLine(previous,p,edge,11);UiLine(previous+(side==0?Vector2.right:Vector2.left)*7,p+(side==0?Vector2.right:Vector2.left)*7,line,1);}
                    previous=p;
                }
            }
            UiLine(new Vector2(787,465),new Vector2(795,465),new Color(1,1,1,.45f),1);
            UiLine(new Vector2(805,465),new Vector2(813,465),new Color(1,1,1,.45f),1);
        }
    }
}

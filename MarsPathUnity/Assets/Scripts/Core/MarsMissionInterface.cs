using UnityEngine;

namespace MarsPath
{
    public partial class MarsPathDemoBootstrap
    {
        private GUIStyle metricStyle,captionStyle,centerStyle,interfaceButton;
        private static readonly Color UiCyan=new Color(0.30f,0.90f,0.94f),UiMuted=new Color(0.69f,0.79f,0.83f),UiAmber=new Color(1f,0.68f,0.34f);
        private void OnGUI()
        {
            if (Application.platform == RuntimePlatform.Android) return;
            if(mission==null||GlobalView!=null&&GlobalView.IsVisible)return;
            EnsureStyles();EnsureMissionStyles();Matrix4x4 previous=GUI.matrix;
            GUI.matrix=Matrix4x4.TRS(Vector3.zero,Quaternion.identity,new Vector3(Screen.width/1600f,Screen.height/900f,1));
            if(missionStarted)DrawEvaInterface();else DrawRegionalPlanning();GUI.matrix=previous;
        }
        private void EnsureMissionStyles()
        {
            if(metricStyle!=null)return;
            metricStyle=new GUIStyle(labelStyle){fontSize=27,fontStyle=FontStyle.Bold};
            captionStyle=new GUIStyle(smallStyle){fontSize=11,normal={textColor=UiMuted}};
            centerStyle=new GUIStyle(labelStyle){alignment=TextAnchor.MiddleCenter};
            interfaceButton=new GUIStyle(buttonStyle){fontSize=12,normal={background=darkTexture,textColor=Color.white},hover={background=selectedTexture,textColor=UiCyan},active={background=selectedTexture,textColor=Color.white}};
        }
        private static void UiFill(Rect rect,Color color){Color previous=GUI.color;GUI.color=color;GUI.DrawTexture(rect,Texture2D.whiteTexture);GUI.color=previous;}
        private void UiCard(Rect rect,Color accent)
        {GUI.Box(rect,GUIContent.none,cardStyle);UiFill(new Rect(rect.x,rect.y,3,rect.height),accent);}
        private void UiBar(Rect rect,float value,Color color)
        {UiFill(rect,new Color(0.16f,0.21f,0.24f));UiFill(new Rect(rect.x,rect.y,rect.width*Mathf.Clamp01(value),rect.height),color);}
        private static string TimeLabel(float minutes)
        {if(minutes<60)return minutes.ToString("0")+" min";if(minutes<1440)return (minutes/60).ToString("0.0")+" h";return (minutes/1440).ToString("0.0")+" days";}
        private string DataTag=>mission.generatedRegion?MarsRegionalTerrain.SourceTag(surfaceActive?new GeoPoint{latitude=currentLatitude,longitude=currentLongitude}:mission.regionCenter):"JEZERO / CTX DEM";
        private void DrawLegacyRegionalPlanning()
        {
            UiCard(new Rect(20,20,370,860),UiCyan);
            GUI.Label(new Rect(39,36,330,33),"MARSPATH",titleStyle);
            GUI.Label(new Rect(40,78,330,19),"02 / COMPARE YOUR ROUTES",captionStyle);
            GUI.Label(new Rect(40,108,330,44),mission.region,labelStyle);
            for(int i=0;i<mission.routes.Length;i++)
            {
                RouteData route=mission.routes[i];float y=164+i*82;Color color=MarsPathRouteView.ParseColor(route.colorHex,UiCyan);
                if(GUI.Button(new Rect(35,y,340,73),GUIContent.none,i==selectedRoute?selectedCardStyle:cardStyle))SelectRoute(i);
                UiFill(new Rect(35,y,4,73),color);GUI.Label(new Rect(50,y+10,311,24),route.displayName+(route.recommended?" / RECOMMENDED":""),labelStyle);
                GUI.Label(new Rect(50,y+40,311,23),route.available?$"{route.distanceKm:0.00} km  /  {TimeLabel(route.etaMinutes)}":"NO FEASIBLE CORRIDOR",captionStyle);
            }
            RouteData current=mission.routes[selectedRoute];
            GUI.Label(new Rect(40,594,330,19),"START",captionStyle);GUI.Label(new Rect(40,618,330,29),CoordinateShort(mission.currentLocation),smallStyle);
            GUI.Label(new Rect(40,660,330,19),"DESTINATION",captionStyle);GUI.Label(new Rect(40,684,330,29),CoordinateShort(mission.destination),smallStyle);
            GUI.Label(new Rect(40,738,330,64),!CanStartSelectedEva?"FULL ROUTE / EXPEDITION SIMULATION\nAssumes resupply every 4 mission hours. No actual support sites are verified.":"FULL SELECTED ROUTE\nSource terrain + modeled fine detail. Endpoints are retained.",warningStyle);
            GUI.enabled=current.available;
            if(GUI.Button(new Rect(39,817,333,45),CanStartSelectedEva?"START MISSION >":"START FULL EXPEDITION >",interfaceButton))StartMission();GUI.enabled=true;
            GUI.Label(new Rect(432,29,730,35),CanStartSelectedEva?"REGIONAL MISSION PLAN":"PLANETARY JOURNEY OVERVIEW",titleStyle);
            GUI.Label(new Rect(434,75,720,23),"Five A* profiles / selected endpoints / directional surface guidance",smallStyle);
            if(GUI.Button(new Rect(1320,25,257,38),"< GLOBAL MARS",interfaceButton))ReturnToGlobalMap();
            if(journeyOverview!=null&&GUI.Button(new Rect(1040,25,262,38),"< FULL JOURNEY",interfaceButton))ReturnToJourneyOverview();
            UiCard(new Rect(1218,89,359,171),UiAmber);GUI.Label(new Rect(1235,102,324,24),"DATA COVERAGE",labelStyle);
            GUI.Label(new Rect(1235,135,324,22),DataTag,warningStyle);GUI.Label(new Rect(1235,166,324,80),mission.dataNotice,captionStyle);
            UiCard(new Rect(430,708,1147,172),MarsPathRouteView.ParseColor(current.colorHex,UiCyan));
            GUI.Label(new Rect(449,724,1075,28),current.displayName.ToUpperInvariant(),labelStyle);
            GUI.Label(new Rect(449,760,510,55),current.summary,smallStyle);
            GUI.Label(new Rect(449,831,900,35),"DRAG: orbit / SHIFT + DRAG or MMB: pan / WHEEL: zoom / R: reset\nMeasured elevation / vertical scale 1:1 / grid spacing varies with source coverage.",captionStyle);
            if(GUI.Button(new Rect(1430,831,125,30),"RESET VIEW",interfaceButton))cameraRig.ResetPlanningView();
            DrawModelMetric(new Rect(1000,727,250,44),"TRAVERSABILITY MODEL",current.safetyScore,UiCyan);
            DrawModelMetric(new Rect(1288,727,250,44),"ENERGY MODEL",current.energyScore,new Color(0.76f,0.88f,0.36f));
            GUI.Label(new Rect(1000,791,540,23),$"Max modeled slope {current.maxSlopeDegrees:0.0}° / Candidate science {current.sciencePointIds?.Length??0}",captionStyle);
            DrawEndpointWorldLabels();
        }
        private void DrawModelMetric(Rect rect,string label,float value,Color color)
        {GUI.Label(new Rect(rect.x,rect.y,rect.width,20),label+" "+value.ToString("0"),captionStyle);UiBar(new Rect(rect.x,rect.y+29,rect.width,5),value/100,color);}
        private void DrawEndpointWorldLabels()
        {
            RoutePoint[] points=mission.routes[selectedRoute].waypoints;if(points==null||points.Length<2)return;
            MarsPlanningEndpointLabels labels=GetComponent<MarsPlanningEndpointLabels>();
            if(labels==null)labels=gameObject.AddComponent<MarsPlanningEndpointLabels>();
            labels.Draw(MarsPlanningEndpointLabels.Project(mainCamera,points),UiCyan,UiAmber);
        }
        private static string CoordinateShort(GeoPoint point)=>$"{Mathf.Abs(point.latitude):0.00000}°{(point.latitude>=0?"N":"S")} / {Mathf.Abs(point.longitude):0.00000}°{(point.longitude>=0?"E":"W")}";
        private void DrawLegacyEvaInterface()
        {
            RouteData route=mission.routes[selectedRoute];Color routeColor=MarsPathRouteView.ParseColor(route.colorHex,UiCyan);
            UiCard(new Rect(22,22,310,181),routeColor);
            GUI.Label(new Rect(40,36,276,23),expeditionMode?"MARSPATH / EXPEDITION SIM":"MARSPATH / EVA SIMULATION",captionStyle);GUI.Label(new Rect(40,65,276,27),route.displayName.ToUpperInvariant(),labelStyle);
            float remaining=route.distanceKm*(1-routeProgress);GUI.Label(new Rect(40,102,276,36),remaining>10?remaining.ToString("0.0")+" km":(remaining*1000).ToString("0")+" m",metricStyle);
            GUI.Label(new Rect(40,145,276,22),$"ETA {TimeLabel(route.etaMinutes*(1-routeProgress))}",smallStyle);
            GUI.Label(new Rect(40,173,276,23),$"Surface {currentSlopeDegrees:0.0}° / DEM grade {currentDemSlopeDegrees:0.00}°",captionStyle);
            UiCard(new Rect(1268,22,310,200),UiCyan);GUI.Label(new Rect(1285,36,275,22),"SUIT / MODELED TELEMETRY",captionStyle);
            GUI.Label(new Rect(1285,71,275,24),$"OXYGEN {oxygen:0.0}%",labelStyle);UiBar(new Rect(1285,104,275,7),oxygen/100,UiCyan);
            GUI.Label(new Rect(1285,127,275,24),$"POWER {power:0.0}%",labelStyle);UiBar(new Rect(1285,160,275,7),power/100,UiAmber);
            GUI.Label(new Rect(1285,184,275,22),$"{metabolicWatts:0} W / {heartRateBpm:0} bpm / {oxygenFlowGramsPerHour:0} g O2/h",captionStyle);
            DrawCompass();
            GUI.Label(new Rect(478,96,644,23),arrived?"DESTINATION REACHED":$"TRAVERSE PROGRESS {routeProgress*100:0}%",centerStyle);
            UiBar(new Rect(502,130,596,4),routeProgress,routeColor);
            GUI.Label(new Rect(550,147,500,22),"PLAYBACK / "+playbackSpeed.ToString("0")+"x  (1x = mission time)",centerStyle);
            float[] speeds={1,30,50};for(int i=0;i<speeds.Length;i++)if(GUI.Button(new Rect(641+i*111,178,101,29),speeds[i].ToString("0")+"x",interfaceButton))SetPlaybackSpeed(speeds[i]);
            UiCard(new Rect(505,214,590,96),UiCyan);
            GUI.Label(new Rect(520,222,570,24),expeditionMode?"ASSUMED 4 h RESUPPLY / LEG "+(resupplyLeg+1)+" / SITES UNVERIFIED":"FULL SELECTED ROUTE / ORIGINAL DESTINATION RETAINED",captionStyle);
            GUI.Label(new Rect(510,250,580,20),"EXPLORE THE COMPLETE ROUTE / drag to any point",captionStyle);
            float seek=GUI.HorizontalSlider(new Rect(515,281,570,22),routeProgress,0,1);
            if(Mathf.Abs(seek-routeProgress)>0.0001f)pendingSeek=seek;
            UiCard(new Rect(22,776,430,102),routeColor);GUI.Label(new Rect(40,789,393,22),paused?"MISSION PAUSED":arrived?"MISSION COMPLETE":"FOLLOW THE SURFACE ARROWS",labelStyle);
            GUI.enabled=!reserveStop;if(GUI.Button(new Rect(39,829,111,32),paused?"RESUME":"PAUSE",interfaceButton))paused=!paused;GUI.enabled=true;
            if(GUI.Button(new Rect(163,829,269,32),"< ROUTE PLANNING",interfaceButton))ReturnToPlanning();
            UiCard(new Rect(1248,746,330,132),UiCyan);GUI.Label(new Rect(1265,760,295,22),"POSITION / REGIONAL FRAME",captionStyle);
            GUI.Label(new Rect(1265,793,295,23),FormatCoordinate("LAT",currentLatitude,'N','S'),labelStyle);GUI.Label(new Rect(1265,825,295,23),FormatCoordinate("LON",currentLongitude,'E','W'),labelStyle);
            System.TimeSpan clock=System.TimeSpan.FromSeconds(totalElapsedSeconds);
            GUI.Label(new Rect(500,855,700,22),"MISSION CLOCK "+(clock.Days>0?clock.Days+"d ":"")+clock.ToString(@"hh\:mm\:ss")+" / "+DataTag,captionStyle);
            DrawEvaWarning();
            if(arrived)DrawCompletionCard();
        }
        private void DrawCompass()
        {
            UiCard(new Rect(526,22,548,59),UiCyan);
            GUI.Label(new Rect(545,37,507,29),$"{HeadingCardinal(currentHeadingDegrees)}   {currentHeadingDegrees:000}°    /    ROUTE GUIDANCE",centerStyle);
        }
        private void DrawEvaWarning()
        {
            string heading=null,detail=null,action=null;Color accent=UiAmber;
            if(oxygen<=20.01f||power<=15.01f)
            {UiCard(new Rect(487,508,626,119),new Color(1,0.32f,0.23f));GUI.Label(new Rect(505,523,590,24),"LIFE SUPPORT / RESERVE STOP",warningStyle);GUI.Label(new Rect(505,561,590,49),"Traversal paused at the modeled reserve boundary. Return to route planning. Terrain alerts remain visible below.",smallStyle);}
            if(nearbyHazard!=null){heading=nearbyHazard.severity=="HIGH"?"TERRAIN / HIGH CAUTION":"TERRAIN / CAUTION";detail=nearbyHazard.warning;action=nearbyHazard.action;}
            else if(upcomingSurfaceSlope>10){heading="SLOPE AHEAD / SIMULATED SURFACE";detail=$"Surface ahead up to {upcomingSurfaceSlope:0.0}° / current DEM grade {currentDemSlopeDegrees:0.00}°.";action="Inspect footing. Fine-scale terrain is modeled, not measured by MOLA.";}
            if(rockAhead){heading=(heading==null?"ROCKS / CAUTION":"ROCKS + "+heading);detail=(detail==null?"Visual boulders lie close to the guidance corridor.":detail);action="Slow down, keep clearance and inspect the highlighted corridor.";}
            if(heading==null)
            {UiCard(new Rect(487,701,626,64),UiCyan);GUI.Label(new Rect(505,711,590,22),"LOCAL SCAN / NO NEARBY VISUAL ALERT",captionStyle);GUI.Label(new Rect(505,737,590,22),"Rock proximity and surface slope active / fine-scale hazards unverified",captionStyle);return;}
            UiCard(new Rect(487,653,626,119),accent);GUI.Label(new Rect(505,667,590,24),heading,warningStyle);
            GUI.Label(new Rect(505,701,590,29),detail,smallStyle);GUI.Label(new Rect(505,739,590,26),action,captionStyle);
        }
        private void DrawCompletionCard()
        {
            UiCard(new Rect(500,350,600,183),new Color(0.36f,0.91f,0.64f));GUI.Label(new Rect(524,370,550,31),"DESTINATION REACHED",titleStyle);
            GUI.Label(new Rect(524,416,550,28),mission.destination.label,labelStyle);GUI.Label(new Rect(524,452,550,22),"Simulation complete. Review route estimates and source coverage.",smallStyle);
            if(GUI.Button(new Rect(524,487,550,29),"RETURN TO PLANNING",interfaceButton))ReturnToPlanning();
        }
    }
}

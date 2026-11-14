using UnityEngine;

namespace MarsPath
{
    public partial class MarsPathDemoBootstrap
    {
        private bool showMissionDetails,showPlaybackControls,showPlanningDetails;
        private GUIStyle planTitle,planLabel,planCaption,planSmall,planButton,planCard,planSelected;
        private static readonly Color FrameColor=new Color(0.018f,0.030f,0.046f);
        private void EnsurePlanningStyles()
        {
            if(planTitle!=null)return;
            Color ink=Color.white,muted=new Color(0.68f,0.77f,0.80f),teal=UiCyan;
            planTitle=new GUIStyle(titleStyle){fontSize=27,normal={textColor=ink}};
            planLabel=new GUIStyle(labelStyle){fontSize=17,normal={textColor=ink}};
            planCaption=new GUIStyle(captionStyle){fontSize=13,normal={textColor=muted}};
            planSmall=new GUIStyle(smallStyle){fontSize=14,normal={textColor=muted}};
            Texture2D paper=MarsGlobalView.RoundedGlobalSurface(new Color(.030f,.059f,.075f));
            Texture2D selected=MarsGlobalView.RoundedGlobalSurface(new Color(.043f,.211f,.239f));
            Texture2D button=MarsGlobalView.RoundedGlobalSurface(new Color(.045f,.116f,.140f));
            planCard=new GUIStyle(cardStyle){border=new RectOffset(7,7,7,7),normal={background=paper},hover={background=selected},active={background=selected}};
            planSelected=new GUIStyle(planCard){normal={background=selected}};
            planSelected.normal.textColor=UiCyan;planSelected.hover.textColor=Color.white;
            planButton=new GUIStyle(interfaceButton){border=new RectOffset(7,7,7,7),normal={background=button,textColor=Color.white},hover={background=selected,textColor=Color.white},active={background=selected,textColor=Color.white}};
        }
        private static void PlanningCard(Rect rect,Color accent)
        {UiFill(rect,new Color(0.025f,0.045f,0.060f));UiFill(new Rect(rect.x,rect.y,3,rect.height),accent);}
        private void DrawRegionalPlanning()
        {
            EnsurePlanningStyles();
            UiFill(new Rect(0,0,375,900),FrameColor);UiFill(new Rect(375,0,1225,105),FrameColor);UiFill(new Rect(375,640,1225,260),FrameColor);
            UiFill(new Rect(1580,105,20,535),FrameColor);
            UiFill(new Rect(374,0,1,900),new Color(.16f,.29f,.32f));
            GUI.Label(new Rect(30,26,305,34),"MARSPATH",planTitle);
            GUI.Label(new Rect(32,67,305,22),"LAGRANGE V   /   MISSION CONTROL",planCaption);
            UiFill(new Rect(32,103,307,1),new Color(.16f,.29f,.32f));
            string region=mission.region;string[] halves=region.Split(new[]{" to "},System.StringSplitOptions.None);
            if(halves.Length==2&&halves[0]==halves[1])region=halves[0]=="Selected Mars region"?halves[0]:halves[0]+" / Selected region";
            GUI.Label(new Rect(32,121,307,42),region,planLabel);
            GUI.Label(new Rect(32,169,307,22),"CHOOSE YOUR ROUTE",planCaption);
            for(int i=0;i<mission.routes.Length;i++)
            {
                RouteData r=mission.routes[i];float y=204+i*75;Color c=MarsPathRouteView.ParseColor(r.colorHex,UiCyan);
                GUI.enabled=!helmetEntryLoading&&!PlanningDispatchPending;
                if(GUI.Button(new Rect(27,y,321,66),GUIContent.none,i==PlanningRouteIndex?planSelected:planCard))SelectPlanningRoute(i);
                GUI.enabled=true;
                UiFill(new Rect(34,y+12,3,42),c);
                GUI.Label(new Rect(46,y+14,25,24),(i+1).ToString("00"),new GUIStyle(planCaption){normal={textColor=c}});
                GUI.Label(new Rect(77,y+8,250,24),r.displayName,planLabel);
                GUI.Label(new Rect(77,y+37,250,21),r.available?$"{FormatRouteLength(r.distanceKm)}   ·   {TimeLabel(r.etaMinutes)} est.{(r.recommended?"   ·   Recommended":"")}":"No feasible modeled corridor",planCaption);
                if(i==PlanningRouteIndex)UiFill(new Rect(333,y+20,3,25),c);
            }
            RouteData route=mission.routes[PlanningRouteIndex];
            UiFill(new Rect(32,591,307,1),new Color(.16f,.29f,.32f));
            GUI.Label(new Rect(32,605,150,22),"TRAVERSABILITY  "+route.safetyScore.ToString("0"),planCaption);UiBar(new Rect(32,635,143,3),route.safetyScore/100,UiCyan);
            GUI.Label(new Rect(197,605,150,22),"ENERGY  "+route.energyScore.ToString("0"),planCaption);UiBar(new Rect(197,635,142,3),route.energyScore/100,UiAmber);
            GUI.Label(new Rect(32,648,307,21),"RELATIVE MODEL SCORES / 100",planCaption);
            GUI.Label(new Rect(32,688,307,20),"01  START",new GUIStyle(planCaption){normal={textColor=UiCyan}});GUI.Label(new Rect(32,713,310,27),CoordinateShort(mission.currentLocation),planSmall);
            GUI.Label(new Rect(32,755,307,20),"02  DESTINATION",new GUIStyle(planCaption){normal={textColor=UiAmber}});GUI.Label(new Rect(32,780,310,27),CoordinateShort(mission.destination),planSmall);
            if(GUI.Button(new Rect(28,835,150,39),"FIT VIEW",planButton))cameraRig.ResetPlanningView();
            if(GUI.Button(new Rect(190,835,158,39),showPlanningDetails?"CLOSE DETAILS":"SHOW DETAILS",showPlanningDetails?planSelected:planButton))showPlanningDetails=!showPlanningDetails;
            GUI.Label(new Rect(404,13,126,22),"01  ENDPOINTS",planCaption);
            UiFill(new Rect(534,15,3,17),UiCyan);
            GUI.Label(new Rect(544,13,105,22),"02  ROUTES",new GUIStyle(planCaption){normal={textColor=UiCyan}});
            GUI.Label(new Rect(659,13,105,22),"03  EVA",planCaption);
            GUI.Label(new Rect(402,39,575,35),"COMPARE YOUR ROUTES",planTitle);
            GUI.Label(new Rect(404,78,575,22),"DRAG TO PAN · RIGHT-DRAG TO ORBIT · SCROLL TO ZOOM",planCaption);
            if (!desktopBrowsingRoutes) DrawPlanningRadio();
            if(GUI.Button(new Rect(990,18,310,39),cameraRig.PlanningInspectionActive?"‹ ROUTE OVERVIEW":"INSPECT TERRAIN",planButton))
                cameraRig.TogglePlanningInspection();
            MarsPlanningDetailStream detail=GetComponent<MarsPlanningDetailStream>();
            GeoPoint imageFocus=MarsRegionalTerrain.ToGeo(cameraRig.PlanningTarget.x,cameraRig.PlanningTarget.z);
            float imageGrid=MarsCtxImageryCache.DisplayedSpacingAt(imageFocus);
            bool matching=MarsOfficialTerrain.TryActiveMatchingImage(imageFocus,out string imageKind,out float matchingGrid)&&matchingGrid<imageGrid;
            if(MarsRegionalTerrain.CoarsePlanningDisplay)GUI.Label(new Rect(990,77,580,22),$"MEASURED GLOBAL CONTEXT · LOCAL HEIGHT GRID {MarsRegionalTerrain.SourceSpacingMeters(imageFocus):0.#} m",planCaption);
            else if(matching)GUI.Label(new Rect(990,77,580,22),$"{imageKind.ToUpperInvariant()} IMAGE {matchingGrid:0.##} m · HEIGHT {MarsRegionalTerrain.SourceSpacingMeters(imageFocus):0.#} m · MESH {PlanningMeshCellMeters:0.#} m",planCaption);
            else if(imageGrid<float.MaxValue)GUI.Label(new Rect(990,77,580,22),$"CTX IMAGE {imageGrid:0.#} m · HEIGHT {MarsRegionalTerrain.SourceSpacingMeters(imageFocus):0.#} m · LOCAL MESH {PlanningMeshCellMeters:0.#} m",planCaption);
            else GUI.Label(new Rect(990,77,580,22),$"HEIGHT {MarsRegionalTerrain.SourceSpacingMeters(imageFocus):0.#} m · LOCAL MESH {PlanningMeshCellMeters:0.#} m · CONTEXT IMAGE",planCaption);
            GUI.enabled=!PlanningDispatchPending;
            bool browse = desktopBrowsingRoutes;
            string backLabel = browse ? "BACK TO LIVE  ›" : MarsMissionZone.IsMission(mission) ? "‹ JEZERO MISSION ZONE" : "‹ GLOBAL MARS";
            bool goBack = GUI.Button(new Rect(1324,18,248,39), backLabel, browse ? planSelected : planButton);
            GUI.enabled=true;
            if (goBack)
            {
                if (browse) ReturnToLiveMission();
                else ReturnToGlobalMap();
                return;
            }
            DrawSelectedRouteDock(route);
            if (browse) DrawBrowseLiveStatus();
            DrawEndpointWorldLabels();
            DrawPlanningDistanceScale();
            DrawPlanningZoomControls();
            if(showPlanningDetails)DrawRouteEvidenceSheet(route);
        }

        private void DrawPlanningZoomControls()
        {
            UiFill(MarsPathCameraRig.PlanningZoomArea,new Color(.018f,.030f,.046f,.94f));
            bool wasEnabled=GUI.enabled;
            GUI.enabled=wasEnabled&&cameraRig.CanZoomPlanningOut;
            if(GUI.Button(new Rect(1413,125,34,34),"−",planButton))cameraRig.ZoomPlanning(-2);
            GUI.enabled=wasEnabled&&cameraRig.CanZoomPlanningIn;
            if(GUI.Button(new Rect(1524,125,34,34),"+",planButton))cameraRig.ZoomPlanning(2);
            GUI.enabled=wasEnabled;
            GUI.Label(new Rect(1458,131,58,22),$"{cameraRig.PlanningZoom:0.0}×",planCaption);
        }

        private float PlanningMeshCellMeters
        {
            get
            {
                Vector3 target=cameraRig.PlanningTarget;
                if(MarsPlanningFocusTile.TryRenderedHeight(target.x,target.z,out _))return MarsPlanningFocusTile.DisplayCellMeters;
                float adaptive=MarsAdaptivePlanningTerrain.CellMetersAt(target.x,target.z);
                return adaptive>0?adaptive:MarsTerrainGenerator.PlanningOverviewCellMeters;
            }
        }

        private void DrawPlanningDistanceScale()
        {
            Vector3 origin=cameraRig.PlanningTarget;
            Vector3 right=mainCamera.transform.right;right.y=0;right.Normalize();
            Vector3 pixelA=mainCamera.WorldToScreenPoint(origin),pixelB=mainCamera.WorldToScreenPoint(origin+right);
            float pixelsPerUnit=Mathf.Abs(pixelB.x-pixelA.x)*1600/Screen.width;
            if(pixelA.z<=0||pixelsPerUnit<.01f)return;
            GeoPoint a=MarsRegionalTerrain.ToGeo(origin.x,origin.z),b=MarsRegionalTerrain.ToGeo(origin.x+right.x,origin.z+right.z);
            double metresPerUnit=MarsGeospatial.AngularDistanceDegrees(a,b)*System.Math.PI/180*MarsRegionalTerrain.RadiusMeters;
            double desired=130/pixelsPerUnit*metresPerUnit;
            if(desired<=0||double.IsNaN(desired)||double.IsInfinity(desired))return;
            double power=System.Math.Pow(10,System.Math.Floor(System.Math.Log10(desired)));
            double fraction=desired/power,nice=(fraction>=5?5:fraction>=2?2:1)*power;
            float width=(float)(nice/metresPerUnit)*pixelsPerUnit;
            Rect card=new Rect(395,576,Mathf.Max(176,width+28),54);
            UiFill(card,new Color(.018f,.030f,.046f,.86f));
            string label=nice>=1000?(nice/1000).ToString("0.#")+" km":nice.ToString("0")+" m";
            GUI.Label(new Rect(card.x+13,card.y+6,card.width-26,20),label+"  ·  GROUND SCALE",planCaption);
            UiFill(new Rect(card.x+13,card.y+36,width,2),Color.white);
            UiFill(new Rect(card.x+13,card.y+31,2,11),Color.white);
            UiFill(new Rect(card.x+11+width,card.y+31,2,11),Color.white);
        }

        private void DrawEvaInterface()
        {
            EnsureVisorStyles();ObserveVisorContext();
            if(Event.current.type==EventType.KeyDown&&Event.current.keyCode==KeyCode.Escape)
            {showMissionMenu=!showMissionMenu;Event.current.Use();}
            if(Event.current.type==EventType.KeyDown&&Event.current.keyCode==KeyCode.D)
            {showMissionDetails=!showMissionDetails;Event.current.Use();}
            if(cameraRig.HelmetView)DrawVisorFrame();
            Matrix4x4 hudMatrix=GUI.matrix;
            if(cameraRig.HelmetView)
            {
                Rect safe=HelmetHudSafeRect;
                GUI.matrix=hudMatrix*Matrix4x4.TRS(new Vector3(safe.x,safe.y,0),Quaternion.identity,new Vector3(safe.width/1600,safe.height/900,1));
            }
            GUI.BeginGroup(new Rect(0,0,1600,900));
            RouteData route=mission.routes[selectedRoute];Color color=MarsPathRouteView.ParseColor(route.colorHex,UiCyan);
            DrawVisorDistance(route,color);
            if (desktopMissionMirror) DrawLiveMissionProgress(color);
            else DrawVisorCompassTape();
            VisorPanel(new Rect(1230,22,348,130),UiCyan);
            GUI.Label(new Rect(1248,33,310,21),"SUIT / MODELED RESERVES",visorMicro);
            GUI.Label(new Rect(1248,64,148,27),$"O₂  {oxygen:0.0}%",visorHeading);
            GUI.Label(new Rect(1417,64,146,27),$"PWR  {power:0.0}%",visorHeading);
            UiBar(new Rect(1250,104,143,5),oxygen/100,UiCyan);UiBar(new Rect(1419,104,141,5),power/100,UiAmber);
            GUI.Label(new Rect(1248,120,310,19),$"{electricalWatts:0} W suit   ·   {oxygenFlowGramsPerHour:0} g O₂/h",visorMicro);
            if (!desktopMissionMirror)
            {
                string mode=demoPresentation?"2 MIN DEMO":"MISSION TIME · ×"+playbackSpeed.ToString("0");
                string status=helmetPreview?"HELMET PREVIEW":arrived?"DESTINATION REACHED":reserveStop?"RESERVE STOP · "+mode:DemoEnded?"DEMO COMPLETE":paused?"PAUSED · "+mode:mode;
                GUI.Label(new Rect(459,107,682,27),status,visorStatus);
                UiBar(new Rect(568,145,464,3),demoPresentation?DemoElapsed/MarsDemoDirector.DurationSeconds:routeProgress,color);
                string countdown = demoPresentation && !DemoEnded && !arrived
                    ? $"{Mathf.CeilToInt(MarsDemoDirector.DurationSeconds - DemoElapsed)}s LEFT   ·   " : string.Empty;
                GUI.Label(new Rect(484,161,632,23),countdown +
                    $"{routeProgress*100:0.0}% OF ROUTE   ·   SURFACE {currentSlopeDegrees:0.0}°",visorCenteredSmall);
            }
            DrawVisorSourceBadge();
            DrawVisorActions(color);
            DrawMissionRadio();
            if (!desktopMissionMirror || !cameraRig.HelmetView)
                GUI.Label(new Rect(1230,838,348,23),"RMB LOOK   /   R RESET",visorMicro);
            if(showMissionMenu)DrawVisorMissionMenu();
            if(showMissionDetails)DrawVisorDataDetails();
            if(showPlaybackControls)DrawVisorTimeline();
            DrawCompactWarning();
            if(arrived)DrawCompletionCard();
            GUI.EndGroup();GUI.matrix=hudMatrix;
        }
        public static Rect HelmetHudSafeRect=>new Rect(110,70,1380,760);
        internal static readonly Color EvaWarningRed = new Color(1f, .34f, .37f);
        internal static float EvaWarningOpacity(float time) =>
            .81f + .19f * (float)System.Math.Cos(time * (2 * System.Math.PI / 1.8));

        private bool TryGetCompactWarning(out string title, out string detail, out MarsHazardIcons.Symbol symbol)
        {
            title = detail = null;
            symbol = MarsHazardIcons.Symbol.Terrain;
            if (!missionStarted || arrived) return false;
            if (desktopMissionMirror)
            {
                title = currentLiveState?.warning;
                detail = currentLiveState?.warningDetail;
                if (currentLiveState != null && System.Enum.IsDefined(typeof(MarsHazardIcons.Symbol), currentLiveState.warningSymbol))
                    symbol = (MarsHazardIcons.Symbol)currentLiveState.warningSymbol;
                return !string.IsNullOrEmpty(title);
            }
            if(reserveStop)
            {title="RESERVE STOP";detail="LIFE SUPPORT · PAUSED";symbol=MarsHazardIcons.Symbol.LifeSupport;}
            else if (QuestFacingBlocked)
            { title = questRouteFacing.Title; detail = questRouteFacing.Instruction; symbol = MarsHazardIcons.Symbol.Direction; }
            else if(rockAhead)
            {
                title=rockFieldCount>0?"ROCK FIELD AHEAD":"BOULDER AHEAD";
                detail=rockFieldCount>0?$"{rockFieldCount} exposed rocks · {rockAheadDistance:0} m":"Keep clear · "+rockAheadDistance.ToString("0")+" m";
                symbol=MarsHazardIcons.Symbol.Rocks;
            }
            else if(nearbyHazard!=null)
            {
                string hazard=(nearbyHazard.label??string.Empty).ToUpperInvariant();
                if(hazard.Contains("ROCK")){title="MODELED TERRAIN CAUTION";symbol=MarsHazardIcons.Symbol.Terrain;}
                else if(hazard.Contains("SLOPE")||hazard.Contains("RIM"))
                {title=hazard.Contains("RIM")?"STEEP RIM":"STEEP SLOPE";symbol=MarsHazardIcons.Symbol.Slope;}
                else title=hazard.Contains("REGOLITH")?"LOOSE GROUND":"TERRAIN CAUTION";
                detail=$"{Mathf.Max(0,nearbyHazardDistance):0} m · SLOW DOWN";
            }
            else if(upcomingSurfaceSlope>MarsHazardPhysics.SlopeWarningDegrees)
            {title="STEEP SLOPE";detail=$"{upcomingSurfaceSlope:0}° DEM · SLOW DOWN";symbol=MarsHazardIcons.Symbol.Slope;}
            return title != null;
        }

        private void DrawCompactWarning()
        {
            if (!TryGetCompactWarning(out string title, out string detail, out MarsHazardIcons.Symbol symbol)) return;
            Rect card = new Rect(1230, 171, 348, 64);
            float opacity = EvaWarningOpacity(Time.unscaledTime);
            Color accent = EvaWarningRed;
            accent.a = opacity;
            UiFill(card, new Color(.075f, .018f, .025f, .83f * opacity));
            UiFill(new Rect(card.x, card.y, 3, card.height), accent);
            Color previous = GUI.color;
            GUI.color = new Color(previous.r, previous.g, previous.b, previous.a * opacity);
            MarsHazardIcons.Draw(new Rect(card.x+9,card.y+7,49,49),symbol,accent);
            GUIStyle heading=new GUIStyle(warningStyle){fontSize=16,wordWrap=false,alignment=TextAnchor.MiddleLeft,normal={textColor=EvaWarningRed}};
            GUIStyle note=new GUIStyle(captionStyle){fontSize=13,wordWrap=false,alignment=TextAnchor.MiddleLeft,normal={textColor=new Color(.86f,.91f,.93f)}};
            GUI.Label(new Rect(card.x+64,card.y+8,270,24),title,heading);
            GUI.Label(new Rect(card.x+64,card.y+33,270,21),detail,note);
            GUI.color = previous;
        }
        private void DrawMissionDetailSheet()
        {
            UiCard(new Rect(1231,397,347,388),UiCyan);GUI.Label(new Rect(1250,412,309,29),"MISSION DETAILS / MODEL",labelStyle);
            GUI.Label(new Rect(1250,458,309,28),$"Estimated route time: {TimeLabel(mission.routes[selectedRoute].etaMinutes)}",smallStyle);
            GUI.Label(new Rect(1250,500,309,43),CoordinateShort(new GeoPoint{latitude=currentLatitude,longitude=currentLongitude}),smallStyle);
            GUI.Label(new Rect(1250,550,309,28),$"Body {metabolicWatts:0} W / suit {electricalWatts:0} W / O₂ {oxygenFlowGramsPerHour:0} g/h",captionStyle);
            GUI.Label(new Rect(1250,591,309,27),$"Signed DEM grade {currentSignedDemGradeDegrees:+0.0;-0.0;0.0}° / g 3.71 m/s²",captionStyle);
            GUI.Label(new Rect(1250,634,309,26),DataTag,captionStyle);
            GUI.Label(new Rect(1250,679,309,85),expeditionMode?"Concept suit / 0.8 kg O₂ / 447 Wh. Assumed resupply every 4 mission hours; sites are unverified.":"Concept suit / 0.8 kg O₂ / 447 Wh. Weir O₂ conversion; gait and electrical loads require calibration.",captionStyle);
        }
    }
}

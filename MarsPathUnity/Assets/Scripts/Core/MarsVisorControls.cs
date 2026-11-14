using UnityEngine;

namespace MarsPath
{
    public partial class MarsPathDemoBootstrap
    {
        private bool showMissionMenu,observedVisorCamera;
        private MissionData observedVisorMission;
        private int observedVisorRoute=-1;
        private GUIStyle visorHeading,visorSmall,visorMicro,visorStatus,visorCenteredSmall,visorBody,visorButton,visorActiveButton,visorDistance;
        private GUIStyle visorRouteRight,visorDistanceMeta,visorAction,visorActionActive,visorActionLabel,visorKeycap;
        private Texture2D visorRouteDot;

        public void ResetVisorPanels()
        {
            showMissionMenu=false;showMissionDetails=false;showPlaybackControls=false;
            observedVisorMission=mission;observedVisorRoute=selectedRoute;
            observedVisorCamera=cameraRig!=null&&cameraRig.HelmetView;
        }
        private void ObserveVisorContext()
        {
            if(observedVisorMission!=mission||observedVisorRoute!=selectedRoute||observedVisorCamera!=cameraRig.HelmetView)ResetVisorPanels();
        }
        private void EnsureVisorStyles()
        {
            if(visorHeading!=null)return;
            visorHeading=new GUIStyle(labelStyle){fontSize=17,wordWrap=false,normal={textColor=new Color(.93f,.97f,.98f)}};
            visorSmall=new GUIStyle(captionStyle){fontSize=13,wordWrap=true,normal={textColor=new Color(.78f,.87f,.89f)}};
            visorMicro=new GUIStyle(visorSmall){fontSize=11,normal={textColor=new Color(.63f,.75f,.79f)}};
            visorRouteRight=new GUIStyle(visorMicro){alignment=TextAnchor.MiddleRight,wordWrap=false};
            visorStatus=new GUIStyle(centerStyle){fontSize=17,normal={textColor=new Color(.89f,.97f,.98f)}};
            visorCenteredSmall=new GUIStyle(visorSmall){alignment=TextAnchor.MiddleCenter,wordWrap=false};
            visorBody=new GUIStyle(visorSmall){fontSize=14};
            visorDistance=new GUIStyle(metricStyle)
            {
                fontSize=40,wordWrap=false,richText=true,alignment=TextAnchor.MiddleLeft,
                padding=new RectOffset(),normal={textColor=new Color(.97f,.99f,1f)}
            };
            visorDistanceMeta=new GUIStyle(visorSmall){wordWrap=false,alignment=TextAnchor.MiddleLeft,padding=new RectOffset()};
            Texture2D idle=MakeTexture(new Color(.045f,.105f,.125f,.96f)),hover=MakeTexture(new Color(.08f,.23f,.26f,.98f));
            visorButton=new GUIStyle(interfaceButton){fontSize=13,normal={background=idle,textColor=new Color(.85f,.94f,.95f)},hover={background=hover,textColor=Color.white},active={background=hover,textColor=UiCyan}};
            visorActiveButton=new GUIStyle(visorButton){normal={background=hover,textColor=UiCyan}};
            visorAction=new GUIStyle(visorButton){normal={background=MakeTexture(new Color(.03f,.075f,.09f,.96f))}};
            visorActionActive=new GUIStyle(visorAction){normal={background=MakeTexture(new Color(.06f,.16f,.18f,.98f))}};
            visorActionLabel=new GUIStyle(visorHeading){fontSize=14,alignment=TextAnchor.MiddleLeft,padding=new RectOffset()};
            visorKeycap=new GUIStyle(visorMicro)
            {
                fontStyle=FontStyle.Bold,wordWrap=false,alignment=TextAnchor.MiddleCenter,padding=new RectOffset(),
                normal={background=MakeTexture(new Color(.13f,.25f,.28f,.9f)),textColor=new Color(.86f,.94f,.95f)}
            };
        }
        private void DrawVisorDistance(RouteData route,Color accent)
        {
            VisorPanel(new Rect(22,22,348,138),accent);
            GUI.Label(new Rect(42,34,306,24),"EVA  /  "+route.displayName.ToUpperInvariant(),visorDistanceMeta);
            float remaining=Mathf.Clamp01(1-routeProgress);
            float km=route.distanceKm*remaining;
            string distance=km>10?km.ToString("0.0"):(km*1000).ToString("0");
            string unit=km>10?"km":"m";
            GUI.Label(new Rect(41,57,310,54),$"{distance} <size=22><color=#B5CCD2>{unit}</color></size>",visorDistance);
            UiFill(new Rect(42,115,306,1),new Color(accent.r,accent.g,accent.b,.27f));
            GUI.Label(new Rect(42,124,306,23),"TO DESTINATION  ·  "+TimeLabel(route.etaMinutes*remaining)+" est.",visorDistanceMeta);
        }
        private void DrawVisorActions(Color routeColor)
        {
            bool previousEnabled=GUI.enabled;
            GUI.enabled=previousEnabled&&!reserveStop;
            if(DrawVisorAction(new Rect(504,820,176,52),helmetPreview?"START EVA":DemoEnded?"CONTINUE ROUTE":paused?"RESUME":"PAUSE",null,routeColor,true))
            {if(helmetPreview)StartFromHelmetPreview();else if(DemoEnded)ContinueAfterDemo();else paused=!paused;}
            GUI.enabled=previousEnabled;
            if(DrawVisorAction(new Rect(701,820,198,52),cameraRig.HelmetView?"CHASE VIEW":"VISOR VIEW","V",UiCyan,false))
            {cameraRig.SetHelmetView(!cameraRig.HelmetView);ResetVisorPanels();}
            if(DrawVisorAction(new Rect(920,820,176,52),showMissionMenu?"CLOSE MENU":"MISSION","ESC",UiCyan,showMissionMenu))
                showMissionMenu=!showMissionMenu;
        }
        private bool DrawVisorAction(Rect rect,string title,string key,Color accent,bool selected)
        {
            bool clicked=GUI.Button(rect,GUIContent.none,selected?visorActionActive:visorAction);
            Color line=new Color(accent.r,accent.g,accent.b,GUI.enabled?(selected?.80f:.30f):.20f);
            UiFill(new Rect(rect.x,rect.y,rect.width,1),line);
            UiFill(new Rect(rect.x,rect.yMax-1,rect.width,1),new Color(.44f,.66f,.70f,.23f));
            if(key==null)GUI.Label(rect,title,visorStatus);
            else
            {
                GUI.Label(new Rect(rect.x+16,rect.y,rect.width-67,rect.height),title,visorActionLabel);
                GUI.Label(new Rect(rect.xMax-47,rect.y+13,33,26),key,visorKeycap);
            }
            return clicked;
        }
        private static void VisorPanel(Rect rect,Color accent)
        {
            UiFill(rect,new Color(.014f,.031f,.043f,.78f));
            Color line=new Color(accent.r,accent.g,accent.b,.60f);
            UiFill(new Rect(rect.x,rect.y,30,2),line);UiFill(new Rect(rect.x,rect.y,2,14),line);
            UiFill(new Rect(rect.xMax-30,rect.yMax-2,30,2),line);UiFill(new Rect(rect.xMax-2,rect.yMax-14,2,14),line);
            UiFill(new Rect(rect.x+30,rect.y,rect.width-30,1),new Color(.35f,.68f,.72f,.16f));
        }
        private void DrawVisorCompassTape()
        {
            const float left=568,width=464;VisorPanel(new Rect(left,22,width,70),UiCyan);
            GUI.BeginGroup(new Rect(left+8,25,width-16,63));
            for(int degree=0;degree<360;degree+=15)
            {
                float delta=Mathf.DeltaAngle(currentHeadingDegrees,degree),x=(width-16)/2+delta*3.1f;
                if(x<6||x>width-22)continue;
                bool cardinal=degree%90==0;
                UiFill(new Rect(x,31,1,cardinal?13:7),new Color(.58f,.79f,.81f,.65f));
                if(degree%30==0)GUI.Label(new Rect(x-24,7,48,21),cardinal?HeadingCardinal(degree):degree.ToString("000"),visorCenteredSmall);
            }
            UiFill(new Rect((width-16)/2-1,28,2,21),UiCyan);GUI.EndGroup();
            GUI.Label(new Rect(704,67,192,23),$"{HeadingCardinal(currentHeadingDegrees)}  {currentHeadingDegrees:000}°",visorCenteredSmall);
        }
        private string VisorCoverage(GeoPoint point)
        {
            string source=mission.generatedRegion?MarsRegionalTerrain.SourceTag(point):DataTag;
            return source.Contains("GLOBAL")||source.Contains("global")?"GLOBAL COVERAGE":"LOCAL / REGIONAL COVERAGE";
        }
        private void DrawVisorSourceBadge()
        {
            // Source diagnostics are opt-in through Mission > Data & Mission Details.
            if(!showMissionDetails)return;
            GeoPoint point=JourneyLocation;float spacing=mission.generatedRegion?MarsRegionalTerrain.SourceSpacingMeters(point):20;
            // The compact route context shares the existing source card. When
            // the full data sheet is open, retain its original non-overlapping
            // layout instead of placing two panels over the same terrain.
            VisorPanel(new Rect(22,174,432,showMissionDetails?70:168),surfaceSourceLoading?UiAmber:UiCyan);
            GUI.Label(new Rect(39,184,397,22),$"GROUND {currentSlopeDegrees:0.0}°   ·   ELEV {MarsRegionalTerrain.MeasuredElevation(point):0} m",visorSmall);
            bool matching=MarsOfficialTerrain.TryActiveMatchingImage(point,out string imageKind,out float imageGrid)&&imageGrid<MarsCtxImageryCache.SpacingAt(point);
            string resolution=matching?$"HEIGHT GRID {spacing:0.#} m  ·  {imageKind.ToUpperInvariant()} IMAGE {imageGrid:0.##} m":MarsCtxImageryCache.Covers(point)?$"HEIGHT GRID {spacing:0.#} m  ·  CTX IMAGE {MarsCtxImageryCache.SpacingAt(point):0.#} m":
                surfaceSourceLoading?"SOURCE REFINEMENT IN BACKGROUND":spacing<=2?"MEASURED STEREO / "+spacing.ToString("0.#")+" m GRID":$"MEASURED GRID {spacing:0.#} m / FINER FEATURES UNRESOLVED";
            GUI.Label(new Rect(39,206,397,18),resolution,visorMicro);
            string source=mission.generatedRegion?MarsRegionalTerrain.SourceTag(point):DataTag;
            source=source.Replace("NASA / ","").Replace("ESA / MEX ","").Replace(" science-team / preliminary","");
            GUI.Label(new Rect(39,225,397,18),source,visorMicro);
            if(!showMissionDetails)DrawVisorRouteContext(point);
        }

        private void DrawVisorRouteContext(GeoPoint point)
        {
            // This cached getter samples the selected geographic route and
            // restores the active metre-based surface frame if a rebuild is
            // needed. Never configure a planning frame from this HUD renderer.
            MarsRouteElevationProfile profile=SelectedElevationProfile;
            if(profile==null||profile.heights==null||profile.heights.Length<2)return;
            GUI.Label(new Rect(39,248,267,19),"ROUTE ELEVATION / m · CLICK TO INSPECT",visorMicro);
            GUI.Label(new Rect(269,248,167,19),$"{JourneyProgress*100:0.0}%  ·  {profile.lengthKm:0.0} km",visorRouteRight);
            Rect graph=new Rect(96,273,340,38);
            UiFill(graph,new Color(.025f,.065f,.078f,.86f));
            float range=Mathf.Max(1,profile.maximum-profile.minimum);
            int count=Mathf.Min(97,profile.heights.Length);
            Vector2 prior=Vector2.zero;
            for(int sample=0;sample<count;sample++)
            {
                int index=sample*(profile.heights.Length-1)/(count-1);
                float fraction=index/(float)(profile.heights.Length-1);
                Vector2 next=new Vector2(graph.x+fraction*graph.width,graph.yMax-(profile.heights[index]-profile.minimum)/range*graph.height);
                if(sample>0)UiLine(prior,next,new Color(UiCyan.r,UiCyan.g,UiCyan.b,.82f),1.2f);
                prior=next;
            }
            GUI.Label(new Rect(39,266,52,18),profile.maximum.ToString("0"),visorRouteRight);
            GUI.Label(new Rect(39,296,52,18),profile.minimum.ToString("0"),visorRouteRight);
            float progress=Mathf.Clamp01(JourneyProgress);
            Vector2 marker=new Vector2(graph.x+progress*graph.width,graph.yMax-(profile.HeightAt(progress)-profile.minimum)/range*graph.height);
            UiLine(new Vector2(marker.x,graph.y-2),new Vector2(marker.x,graph.yMax+2),new Color(1,1,1,.57f),1);
            if(visorRouteDot==null)
            {
                visorRouteDot=new Texture2D(12,12,TextureFormat.RGBA32,false){name="Visor route position dot",filterMode=FilterMode.Bilinear};
                var pixels=new Color[144];
                for(int y=0;y<12;y++)for(int x=0;x<12;x++)
                {
                    float radius=Mathf.Sqrt((x-5.5f)*(x-5.5f)+(y-5.5f)*(y-5.5f));
                    pixels[y*12+x]=new Color(1,1,1,Mathf.Clamp01(5.5f-radius));
                }
                visorRouteDot.SetPixels(pixels);visorRouteDot.Apply(false,true);
            }
            Color old=GUI.color;GUI.color=UiCyan;
            GUI.DrawTexture(new Rect(marker.x-4,marker.y-4,8,8),visorRouteDot);GUI.color=old;
            bool previousEnabled=GUI.enabled;GUI.enabled=previousEnabled&&!helmetEntryLoading;
            if(GUI.Button(graph,GUIContent.none,GUIStyle.none))
            {
                // Undo both the window scale and nested visor-safe transform.
                // A raw Screen.width fraction would seek the wrong station.
                Vector3 logical=GUI.matrix.inverse.MultiplyPoint3x4(new Vector3(Input.mousePosition.x,Screen.height-Input.mousePosition.y,0));
                UseTimedPlayback(1);paused=true;
                pendingSeek=Mathf.Clamp01((logical.x-graph.x)/graph.width);
            }
            GUI.enabled=previousEnabled;
            GUI.Label(new Rect(39,319,397,18),"HERE  ·  "+CoordinateShort(point),visorMicro);
        }
        private void DrawVisorMissionMenu()
        {
            VisorPanel(new Rect(1134,252,444,534),UiCyan);
            GUI.Label(new Rect(1154,269,352,28),"MISSION",visorHeading);
            if(GUI.Button(new Rect(1523,264,36,32),"×",visorButton))showMissionMenu=false;
            GUI.Label(new Rect(1154,305,404,22),demoPresentation?"ACTIVE · 2 MIN DEMO":"ACTIVE · MISSION TIME ×"+playbackSpeed.ToString("0"),visorSmall);
            UiFill(new Rect(1154,343,404,1),new Color(.35f,.68f,.72f,.22f));
            GUI.Label(new Rect(1154,358,404,20),"PLAYBACK MODE",visorMicro);
            if(GUI.Button(new Rect(1154,387,195,38),demoPresentation?"DEMO · RESTART":"2 MIN DEMO",demoPresentation?visorActiveButton:visorButton))RestartTwoMinuteDemo();
            if(GUI.Button(new Rect(1360,387,198,38),"MISSION TIME",!demoPresentation?visorActiveButton:visorButton))UseTimedPlayback(playbackSpeed);
            if(TimedControlsVisible)
            {
                float[] speeds={1,30,50};
                for(int i=0;i<speeds.Length;i++)if(GUI.Button(new Rect(1154+i*138,437,128,35),"×"+speeds[i].ToString("0"),Mathf.Approximately(playbackSpeed,speeds[i])?visorActiveButton:visorButton))UseTimedPlayback(speeds[i]);
            }
            else GUI.Label(new Rect(1154,443,404,28),DemoScope(mission.routes[selectedRoute]),visorSmall);
            UiFill(new Rect(1154,491,404,1),new Color(.35f,.68f,.72f,.22f));
            if(GUI.Button(new Rect(1154,508,404,39),showPlaybackControls?"HIDE ROUTE TIMELINE":"ROUTE TIMELINE",showPlaybackControls?visorActiveButton:visorButton))showPlaybackControls=!showPlaybackControls;
            if(GUI.Button(new Rect(1154,558,404,39),showMissionDetails?"HIDE DATA DETAILS":"DATA & MISSION DETAILS",showMissionDetails?visorActiveButton:visorButton))showMissionDetails=!showMissionDetails;
            RouteData route=mission.routes[selectedRoute];
            GUI.Label(new Rect(1154,612,404,46),route.displayName+" · "+route.distanceKm.ToString("0.0")+" km\n"+TimeLabel(route.etaMinutes*(1-routeProgress))+" estimated remaining",visorSmall);
            UiFill(new Rect(1154,681,404,1),new Color(.35f,.68f,.72f,.22f));
            if(GUI.Button(new Rect(1154,700,404,40),"‹ RETURN TO ROUTE PLANNER",visorButton)){ResetVisorPanels();ReturnToPlanning();}
            GUI.Label(new Rect(1154,753,404,20),"ESC closes this menu",visorMicro);
        }
        private void DrawVisorTimeline()
        {
            // Kept clear of the warning, side sheets and the persistent dock.
            VisorPanel(new Rect(474,748,652,56),UiCyan);
            GUI.Label(new Rect(490,753,592,22),"SEEK ROUTE · dragging switches to mission time ×1",visorMicro);
            if(GUI.Button(new Rect(1089,752,26,23),"×",visorButton))showPlaybackControls=false;
            float seek=GUI.HorizontalSlider(new Rect(491,785,618,16),routeProgress,0,1);
            if(Mathf.Abs(seek-routeProgress)>.0001f){UseTimedPlayback(1);pendingSeek=seek;}
        }
        private void DrawVisorDataDetails()
        {
            GeoPoint point=JourneyLocation;RouteData route=mission.routes[selectedRoute];MarsRouteFacts facts=MarsRouteFacts.For(mission,route,SelectedElevationProfile);
            float spacing=mission.generatedRegion?MarsRegionalTerrain.SourceSpacingMeters(point):20;
            VisorPanel(new Rect(22,267,432,519),UiCyan);
            GUI.Label(new Rect(42,281,334,27),"LIVE MISSION DATA",visorHeading);
            if(GUI.Button(new Rect(399,277,36,32),"×",visorButton))showMissionDetails=false;
            GUI.Label(new Rect(42,320,392,22),route.displayName.ToUpperInvariant()+" / SELECTED ROUTE",visorMicro);
            DrawVisorFactRow(353,"TRAVERSED / REMAINING",$"{route.distanceKm*routeProgress:0.00} / {route.distanceKm*(1-routeProgress):0.00} km");
            DrawVisorFactRow(387,"REMAINING TIME · ESTIMATE",TimeLabel(route.etaMinutes*(1-routeProgress)));
            DrawVisorFactRow(421,"MEASURED ELEVATION",$"{MarsRegionalTerrain.MeasuredElevation(point):0.0} m");
            DrawVisorFactRow(455,"LOCAL / MAX TRACK GRADE",$"{currentDemSlopeDegrees:0.0}° / {facts.maxGradeDegrees:0.0}°");
            UiFill(new Rect(42,492,392,1),new Color(.35f,.68f,.72f,.22f));
            GUI.Label(new Rect(42,506,392,20),"POSITION / ACTIVE ELEVATION SOURCE",visorMicro);
            GUI.Label(new Rect(42,531,392,22),CoordinateShort(point),visorSmall);
            GUI.Label(new Rect(42,562,392,39),DataTag,visorSmall);
            GUI.Label(new Rect(42,606,392,32),$"Grid {spacing:0.#} m · grid spacing ≠ vertical accuracy",visorMicro);
            GUI.Label(new Rect(42,645,392,22),$"ASCENT {facts.ascentMeters:0} m   /   DESCENT {facts.descentMeters:0} m",visorSmall);
            GUI.Label(new Rect(42,678,392,22),$"EFFORT {metabolicWatts:0} W   /   O₂ {oxygenFlowGramsPerHour:0} g/h",visorSmall);
            GUI.Label(new Rect(42,717,392,53),expeditionMode?"Model: resupply every 4 mission hours, leg "+(resupplyLeg+1)+". Individual rocks and suit resources are simulated.":"Elevation comes from source data. Individual rocks, suit reserves and effort are simulated.",visorMicro);
        }
        private void DrawVisorFactRow(float y,string label,string value)
        {GUI.Label(new Rect(42,y,233,23),label,visorMicro);GUI.Label(new Rect(260,y-2,174,26),value,new GUIStyle(visorSmall){alignment=TextAnchor.MiddleRight,wordWrap=false});}
    }
}

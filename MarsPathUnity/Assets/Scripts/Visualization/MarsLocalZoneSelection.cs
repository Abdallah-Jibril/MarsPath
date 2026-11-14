using UnityEngine;

namespace MarsPath
{
    public partial class MarsGlobalView
    {
        public bool LocalZoneVisible{get;private set;}
        private bool resumeMissionZone,zoneDragging;
        private float zoneZoom=1,zoneDragTravel;
        private Vector2 zonePan,zoneMouseDown;
        private Texture2D zoneLandscapeImage,zoneLandscapeSource;
        private static Rect ZoneViewport=>new Rect(415,105,1160,770);

        public void OpenMissionZone()
        {
            if(IsBusy)return;
            if(!MarsMissionZone.Available||MarsMissionZone.Image==null)
            {SetError("The measured Jezero mission zone is not available in this build.");return;}
            LocalZoneVisible=true;resumeMissionZone=true;IsVisible=true;
            MarsMissionZone.RequiredForSelection=true;
            dragging=false;zoneDragging=false;zoneZoom=1;zonePan=Vector2.zero;
            globeRoot.SetActive(questGlobe != null);localCamera.enabled=false;localRig.enabled=false;
            RenderSettings.fog=false;inputError=false;
            selectEnd=MarsMissionZone.Contains(StartLocation)&&!MarsMissionZone.Contains(EndLocation);
            selectionMessage=MissionZoneSelectionMessage();
            // Opening the zone changes only the view. Existing coordinates stay
            // untouched until a valid map pick or manual entry is accepted.
            SyncCoordinateFields();
        }

        public void CloseMissionZone()
        {
            if(IsBusy)return;
            LocalZoneVisible=false;resumeMissionZone=false;zoneDragging=false;
            MarsMissionZone.RequiredForSelection=false;
            Show();selectionMessage="Global Mars / choose locations or return to JEZERO MISSION ZONE.";
        }

        /// <summary>East/north fractions of the selectable measured rectangle.
        /// Mapping is read-only; accepted pointer clicks use SelectLocation.</summary>
        public bool TryZoneMapPick(Vector2 normalized,out GeoPoint point)
        {
            point=null;
            if(!LocalZoneVisible||!MarsMissionZone.Available||float.IsNaN(normalized.x)||float.IsNaN(normalized.y)||
                normalized.x<0||normalized.x>1||normalized.y<0||normalized.y>1)return false;
            GeoPoint candidate=MarsMissionZone.AtNormalized(normalized.x,normalized.y);
            if(candidate==null||!MarsMissionZone.Contains(candidate))return false;
            point=candidate;return true;
        }

        // The displayed map is rotated clockwise: north points right and east
        // points down. Geographic east/north fractions remain unchanged in the
        // public map API and in the source metadata.
        public bool TryZoneCanvasPick(Vector2 canvasPosition,out GeoPoint point)
        {
            point=null;if(!ZoneViewport.Contains(canvasPosition))return false;
            Rect image=ZoneImageRect;
            return TryZoneMapPick(new Vector2((canvasPosition.y-image.y)/image.height,
                (canvasPosition.x-image.x)/image.width),out point);
        }

        public bool TryZoneScreenPosition(GeoPoint point,out Vector2 screen)=>ZoneAnchor(point,ZoneImageRect,out screen);

        private string MissionZoneSelectionMessage()
        {
            bool start=MarsMissionZone.Contains(StartLocation),end=MarsMissionZone.Contains(EndLocation);
            if(!start&&!end)return "Choose START and DESTINATION inside the outlined zone. Existing coordinates have been preserved.";
            if(!start)return "Choose START inside the outlined zone, or enter its coordinates below.";
            if(!end)return "Choose DESTINATION inside the outlined zone, or enter its coordinates below.";
            return "Both endpoints are inside the measured zone. Refine either point or choose VIEW ROUTES.";
        }

        private Rect ZoneImageRect
        {
            get
            {
                float aspect=Mathf.Max(.01f,MarsMissionZone.HeightKm/Mathf.Max(.01f,MarsMissionZone.WidthKm));
                float height=Mathf.Min(ZoneViewport.height-20,(ZoneViewport.width-20)/aspect);
                Vector2 size=new Vector2(height*aspect,height)*zoneZoom;
                return new Rect(ZoneViewport.center+zonePan-size*.5f,size);
            }
        }

        private void ClampZonePan()
        {
            Rect image=ZoneImageRect;
            float x=Mathf.Max(0,(image.width-ZoneViewport.width)*.5f),y=Mathf.Max(0,(image.height-ZoneViewport.height)*.5f);
            zonePan=new Vector2(Mathf.Clamp(zonePan.x,-x,x),Mathf.Clamp(zonePan.y,-y,y));
        }

        private void DrawMissionZoneInterface()
        {
            Matrix4x4 previous=GUI.matrix;
            GUI.matrix=Matrix4x4.TRS(Vector3.zero,Quaternion.identity,new Vector3(Screen.width/CanvasWidth,Screen.height/CanvasHeight,1));
            bool previousEnabled=GUI.enabled;GUI.enabled=previousEnabled&&!IsBusy;
            Fill(new Rect(0,0,1600,900),new Color(.018f,.030f,.046f));
            GUI.Label(new Rect(30,23,350,40),"MARSPATH",title);
            GUI.Label(new Rect(31,69,350,20),"LAGRANGE V / LOCAL MISSION PLANNING",small);
            GUI.Label(new Rect(443,24,850,36),"JEZERO MISSION ZONE",title);
            GUI.Label(new Rect(445,70,840,24),"01 CHOOSE START / 02 CHOOSE DESTINATION / 03 COMPARE ROUTES",small);
            if(GUI.Button(new Rect(1370,38,185,32),"‹ GLOBAL MARS",button))CloseMissionZone();

            GUI.Box(new Rect(30,116,350,112),GUIContent.none,panel);
            GUI.Label(new Rect(45,128,320,25),MarsMissionZone.Name,heading);
            GUI.Label(new Rect(45,162,320,22),$"{MarsMissionZone.WidthKm:0.0} × {MarsMissionZone.HeightKm:0.0} km · {MarsMissionZone.SpacingMeters:0.##} m HEIGHT GRID",small);
            GUI.Label(new Rect(45,190,320,30),"Choose freely inside the outlined measured terrain.",body);
            DrawZoneEndpointCard(new Rect(30,250,350,155),false);
            DrawZoneEndpointCard(new Rect(30,420,350,155),true);
            bool startInside=MarsMissionZone.Contains(StartLocation),endInside=MarsMissionZone.Contains(EndLocation);
            float separation=MarsGeospatial.AngularDistanceDegrees(StartLocation,EndLocation)*Mathf.Deg2Rad*(float)MarsRegionalTerrain.RadiusMeters/1000;
            GUI.Label(new Rect(31,586,350,20),startInside&&endInside?$"SELECTED SEPARATION {separation:0.000} km":"CHOOSE BOTH ENDPOINTS INSIDE THE ZONE",small);
            GUI.enabled=previousEnabled&&!IsBusy&&startInside&&endInside;
            if(GUI.Button(new Rect(30,610,350,55),IsBusy?"ANALYZING TERRAIN...":"VIEW ROUTES >",activeButton))SubmitRoutes();
            GUI.enabled=previousEnabled&&!IsBusy;
            if(GUI.Button(new Rect(30,685,150,35),"FIT MISSION ZONE",button)){zoneZoom=1;zonePan=Vector2.zero;}
            if(GUI.Button(new Rect(190,685,190,35),"HACKATHON DEMO >",activeButton))JezeroDemoRequested?.Invoke();
            GUI.Label(new Rect(30,735,350,58),selectionMessage,inputError?inputErrorStyle:body);
            GUI.Label(new Rect(30,804,350,45),"CLICK to pick · DRAG to pan · WHEEL to zoom\nR to fit zone · NORTH → / EAST ↓",body);
            GUI.Label(new Rect(30,863,350,20),"NASA MRO / HiRISE · REGISTERED LOCAL DATA",small);
            GUI.enabled=previousEnabled;
            DrawMissionZoneMap();
            if(!IsBusy)HandleZonePointer(Event.current);
            GUI.matrix=previous;
        }

        private void DrawZoneEndpointCard(Rect rect,bool end)
        {
            GUI.Box(rect,GUIContent.none,panel);Fill(new Rect(rect.x,rect.y,3,rect.height),end?Orange:Cyan);
            if(GUI.Button(new Rect(rect.x+13,rect.y+10,rect.width-26,29),(end?"02 DESTINATION":"01 START")+
                (selectEnd==end?" / ACTIVE":""),selectEnd==end?activeButton:button))selectEnd=end;
            GUI.Label(new Rect(rect.x+13,rect.y+47,150,18),"LATITUDE",small);
            GUI.Label(new Rect(rect.x+180,rect.y+47,157,18),"LONGITUDE",small);
            GUI.SetNextControlName(end?"ZoneEndLatitude":"ZoneStartLatitude");
            string lat=GUI.TextField(new Rect(rect.x+13,rect.y+64,150,30),end?endLat:startLat,coordinateField);
            GUI.SetNextControlName(end?"ZoneEndLongitude":"ZoneStartLongitude");
            string lon=GUI.TextField(new Rect(rect.x+180,rect.y+64,157,30),end?endLon:startLon,coordinateField);
            if(end){endLat=lat;endLon=lon;}else{startLat=lat;startLon=lon;}
            GeoPoint point=end?EndLocation:StartLocation;
            GUI.Label(new Rect(rect.x+13,rect.y+99,324,21),Coordinate(point),small);
            GUI.Label(new Rect(rect.x+13,rect.y+125,194,25),point.label,small);
            if(GUI.Button(new Rect(rect.x+220,rect.y+121,117,25),"APPLY",button))ApplyManualCoordinates(end,lat,lon);
        }

        private void DrawMissionZoneMap()
        {
            Rect image=ZoneImageRect;
            GUI.BeginGroup(ZoneViewport);
            Rect local=image;local.position-=ZoneViewport.position;
            // Cache the rotated pixels rather than rotating IMGUI's matrix.
            // A matrix-rotated portrait quad was clipped before rotation by the
            // GUI group, leaving black bands inside the selectable map bounds.
            Texture2D landscape=MissionZoneLandscapeImage;
            Rect sourceUV=MarsMissionZone.TextureUV;
            Rect landscapeUV=new Rect(sourceUV.y,1-sourceUV.x-sourceUV.width,sourceUV.height,sourceUV.width);
            Color previous=GUI.color;GUI.color=MarsMissionZone.MapTint;
            if(landscape!=null)GUI.DrawTextureWithTexCoords(local,landscape,landscapeUV,true);
            GUI.color=previous;
            Fill(new Rect(local.x,local.y,local.width,1.5f),Cyan);Fill(new Rect(local.x,local.yMax-1.5f,local.width,1.5f),Cyan);
            Fill(new Rect(local.x,local.y,1.5f,local.height),Cyan);Fill(new Rect(local.xMax-1.5f,local.y,1.5f,local.height),Cyan);
            GUI.EndGroup();
            GUI.Label(new Rect(435,116,150,23),$"ZOOM {zoneZoom:0.0}×",small);
            GUI.Label(new Rect(ZoneViewport.xMax-103,116,88,24),"N →",heading);

            bool a=ZoneAnchor(StartLocation,image,out Vector2 start),b=ZoneAnchor(EndLocation,image,out Vector2 end);
            Rect labelBounds=new Rect(ZoneViewport.x+12,ZoneViewport.y+12,ZoneViewport.width-24,ZoneViewport.height-24);
            var labels=MarsPlanningEndpointLabels.Arrange(start,a,end,b,labelBounds);
            GUI.BeginGroup(ZoneViewport);
            if(a)DrawEndpointMapLabel(start,labels.startLabel,"Start",Cyan);
            if(b)DrawEndpointMapLabel(end,labels.endLabel,"Destination",Orange);
            GUI.EndGroup();
        }

        private Texture2D MissionZoneLandscapeImage
        {
            get
            {
                Texture2D source=MarsMissionZone.Image;
                if(source==null)return null;
                if(zoneLandscapeImage!=null&&zoneLandscapeSource==source)return zoneLandscapeImage;
                ReleaseMissionZoneMapTexture();
                Color32[] pixels=source.GetPixels32(),rotated=new Color32[pixels.Length];
                int width=source.width,height=source.height;
                // Unity pixel arrays are bottom-up. This clockwise rotation
                // gives destination[x,y] = source[width-1-y,x].
                for(int y=0;y<height;y++)for(int x=0;x<width;x++)
                    rotated[(width-1-x)*height+y]=pixels[y*width+x];
                var texture=new Texture2D(height,width,TextureFormat.RGBA32,true,false)
                {name="Jezero mission-zone landscape map",wrapMode=TextureWrapMode.Clamp,filterMode=FilterMode.Trilinear,anisoLevel=8};
                texture.SetPixels32(rotated);texture.Apply(true,true);
                zoneLandscapeSource=source;zoneLandscapeImage=texture;return texture;
            }
        }

        private void ReleaseMissionZoneMapTexture()
        {
            if(zoneLandscapeImage!=null)Destroy(zoneLandscapeImage);
            zoneLandscapeImage=null;zoneLandscapeSource=null;
        }

        private static bool ZoneAnchor(GeoPoint point,Rect image,out Vector2 screen)
        {
            screen=Vector2.zero;if(!MarsMissionZone.Contains(point))return false;
            Vector2 normalized=MarsMissionZone.Normalized(point);
            screen=new Vector2(image.x+normalized.y*image.width,image.y+normalized.x*image.height);
            return ZoneViewport.Contains(screen);
        }

        private void HandleZonePointer(Event current)
        {
            bool overMap=ZoneViewport.Contains(current.mousePosition);
            if(current.type==EventType.MouseDown&&overMap&&(current.button==0||current.button==1||current.button==2))
            {zoneDragging=true;zoneDragTravel=0;zoneMouseDown=current.mousePosition;current.Use();}
            else if(current.type==EventType.MouseDrag&&zoneDragging)
            {zoneDragTravel+=current.delta.magnitude;if(zoneDragTravel>5){zonePan+=current.delta;ClampZonePan();}current.Use();}
            else if(current.type==EventType.MouseUp&&zoneDragging)
            {
                if(current.button==0&&overMap&&zoneDragTravel<=5&&(current.mousePosition-zoneMouseDown).sqrMagnitude<=25)
                {
                    if(TryZoneCanvasPick(current.mousePosition,out GeoPoint point))SelectLocation(point);
                    else SetError("Choose a point inside the outlined measured area.");
                }
                zoneDragging=false;current.Use();
            }
            else if(current.type==EventType.ScrollWheel&&overMap)
            {
                Rect before=ZoneImageRect;
                Vector2 fraction=new Vector2((current.mousePosition.x-before.x)/before.width,(current.mousePosition.y-before.y)/before.height);
                zoneZoom=Mathf.Clamp(zoneZoom*Mathf.Exp(-current.delta.y*.12f),1,12);
                Rect after=ZoneImageRect;
                zonePan+=current.mousePosition-new Vector2(after.x+fraction.x*after.width,after.y+fraction.y*after.height);
                ClampZonePan();current.Use();
            }
            else if(current.type==EventType.KeyDown&&current.keyCode==KeyCode.R&&string.IsNullOrEmpty(GUI.GetNameOfFocusedControl()))
            {zoneZoom=1;zonePan=Vector2.zero;current.Use();}
        }
    }
}

using System;
using System.Globalization;
using UnityEngine;

namespace MarsPath
{
    public partial class MarsGlobalView
    {
        public event Action<GeoPoint,GeoPoint> RoutesRequested;
        public event Action JezeroDemoRequested;
        public bool IsBusy { get; set; }
        private Vector2 landmarkScroll;
        private string siteSearch="",startLat="",startLon="",endLat="",endLon="";
        private bool inputError;
        private GUIStyle coordinateField, inputErrorStyle;
        private GUIStyle globalCard, globalChoice, globalZoneAction, globalDemoAction, globalRoutesAction;
        private GUIStyle zoneActionTitle, demoActionTitle, demoActionDetail;
        private Texture2D globalCardTexture, globalChoiceTexture, globalChoiceHoverTexture,
            globalZoneTexture, globalZoneHoverTexture, globalAmberTexture, globalAmberHoverTexture,
            globalRoutesTexture, globalRoutesHoverTexture;

        private void SyncCoordinateFields()
        {
            if(StartLocation==null||EndLocation==null)return;
            startLat=StartLocation.latitude.ToString("0.00000",CultureInfo.InvariantCulture);startLon=StartLocation.longitude.ToString("0.00000",CultureInfo.InvariantCulture);
            endLat=EndLocation.latitude.ToString("0.00000",CultureInfo.InvariantCulture);endLon=EndLocation.longitude.ToString("0.00000",CultureInfo.InvariantCulture);
        }
        public void SetEndpoints(GeoPoint start,GeoPoint end)
        {StartLocation=CopyPoint(start);EndLocation=CopyPoint(end);SyncCoordinateFields();UpdateArc();inputError=false;}
        public void UseSite(int index,bool forEnd)
        {if(IsBusy)return;selectEnd=forEnd;SelectLocation(MarsSiteCatalog.Sites[index].Point);FocusLandmark(index);}
        public void LocalTraverseAtSite(int index)
        {
            if(IsBusy)return;
            GeoPoint start=MarsSiteCatalog.Sites[index].Point,end=MarsGeospatial.Destination(start,48f,2.4f);end.label=start.label+" / Local target";
            SetEndpoints(start,end);FocusLandmark(index);distance=3.5f;selectionMessage="Local 2.4 km traverse selected. Refine coordinates or choose VIEW ROUTES.";
        }
        public void UseHiRiseTerrainExample()
        {
            if(IsBusy)return;
            SetEndpoints(new GeoPoint{latitude=18.4620552f,longitude=77.42473f,label="Jezero / HiRISE scarp approach"},new GeoPoint{latitude=18.4640789f,longitude=77.42473f,label="Jezero / measured traverse"});
            FocusLandmark(0);selectionMessage="Measured scarp beside a 120 m traverse / 1 m heights / 0.25 m matching image. Choose VIEW ROUTES.";
        }
        public void SetError(string message){selectionMessage=message;inputError=true;IsBusy=false;}
        public void SetStatus(string message){selectionMessage=message;inputError=false;}
        public bool ApplyManualCoordinates(bool forEnd,string latText,string lonText)
        {
            if(IsBusy)return false;
            if(!TryCoordinate(latText,true,out float lat)||!TryCoordinate(lonText,false,out float lon))
            {SetError("Invalid coordinates. Latitude: -90 to 90; longitude: -180 to 360. N/S/E/W suffixes are supported.");return false;}
            GeoPoint old=forEnd?EndLocation:StartLocation;
            float normalizedLongitude=MarsGeospatial.NormalizeLongitude(lon);
            bool unchanged=Mathf.Abs(old.latitude-lat)<0.00001f&&Mathf.Abs(MarsGeospatial.NormalizeLongitude(old.longitude-normalizedLongitude))<0.00001f;
            GeoPoint p=new GeoPoint{label=unchanged?old.label:"Entered coordinates",latitude=lat,longitude=normalizedLongitude};
            if(LocalZoneVisible&&!MarsMissionZone.Contains(p))
            {SetError("These coordinates are outside the measured mission zone. Choose a point inside the outlined area.");return false;}
            if(forEnd)EndLocation=p;else StartLocation=p;
            // Do not overwrite pending edits in the other endpoint's card.
            if(forEnd){endLat=lat.ToString("0.00000",CultureInfo.InvariantCulture);endLon=p.longitude.ToString("0.00000",CultureInfo.InvariantCulture);}
            else{startLat=lat.ToString("0.00000",CultureInfo.InvariantCulture);startLon=p.longitude.ToString("0.00000",CultureInfo.InvariantCulture);}
            UpdateArc();inputError=false;selectionMessage="Coordinates applied. Choose VIEW ROUTES when both endpoints are ready.";return true;
        }
        private static bool TryCoordinate(string text,bool lat,out float value)
        {
            value=0;if(string.IsNullOrWhiteSpace(text))return false;
            string s=text.Trim().ToUpperInvariant().Replace("°","").Replace('٫','.').Replace('،','.').Replace(',','.');
            foreach(char c in s.ToCharArray())if(c>='٠'&&c<='٩')s=s.Replace(c,(char)('0'+c-'٠'));
            char suffix=s[s.Length-1];bool hemisphere="NSEW".IndexOf(suffix)>=0;
            if(hemisphere){if(lat&&suffix!='N'&&suffix!='S'||!lat&&suffix!='E'&&suffix!='W')return false;s=s.Substring(0,s.Length-1).Trim();}
            if(!float.TryParse(s,NumberStyles.Float,CultureInfo.InvariantCulture,out value)||float.IsNaN(value)||float.IsInfinity(value))return false;
            if(hemisphere)value=Mathf.Abs(value)*(suffix=='S'||suffix=='W'?-1f:1f);
            return lat?value>=-90&&value<=90:value>=-180&&value<=360;
        }
        private void SubmitRoutes()
        {
            if(IsBusy)return;
            string a=startLat,b=startLon,c=endLat,d=endLon;
            if(!ApplyManualCoordinates(false,a,b)||!ApplyManualCoordinates(true,c,d))return;
            if(LocalZoneVisible&&(!MarsMissionZone.Contains(StartLocation)||!MarsMissionZone.Contains(EndLocation)))
            {SetError("Select both START and DESTINATION inside the measured mission zone.");return;}
            float separation=MarsGeospatial.AngularDistanceDegrees(StartLocation,EndLocation)*Mathf.Deg2Rad*(float)MarsRegionalTerrain.RadiusMeters/1000;
            if(separation<0.08f){SetError("Choose endpoints at least 80 m apart.");return;}
            RoutesRequested?.Invoke(CopyPoint(StartLocation),CopyPoint(EndLocation));
        }
        private void OnGUI()
        {
            if(Application.platform==RuntimePlatform.Android)return;
            if(!IsVisible)return;EnsureStyles();
            if(inputErrorStyle==null)inputErrorStyle=new GUIStyle(body){normal={textColor=Orange}};
            if(coordinateField==null)coordinateField=new GUIStyle(GUI.skin.textField){fontSize=15,padding=new RectOffset(10,6,6,5),normal={background=buttonTexture,textColor=Color.white},focused={background=activeTexture,textColor=Cyan}};
            if(LocalZoneVisible){DrawMissionZoneInterface();return;}
            EnsureGlobalMenuStyles();
            Matrix4x4 previous=GUI.matrix;GUI.matrix=Matrix4x4.TRS(Vector3.zero,Quaternion.identity,new Vector3(Screen.width/CanvasWidth,Screen.height/CanvasHeight,1));
            bool enabledBefore=GUI.enabled;GUI.enabled=enabledBefore&&!IsBusy;
            Color frame=new Color(.018f,.030f,.046f,.94f);
            Fill(new Rect(0,0,415,900),frame);
            Fill(new Rect(415,0,1185,105),frame);
            Fill(new Rect(415,603,1185,297),frame);
            Fill(new Rect(1575,105,25,498),frame);
            Fill(new Rect(414,24,1,850),new Color(.22f,.42f,.46f,.32f));
            DrawGlobalSidebar();

            GUI.Label(new Rect(440,24,820,36),"PLAN YOUR MARSWALK",title);
            Fill(new Rect(443,73,3,17),Cyan);
            GUI.Label(new Rect(454,69,123,24),"01  ENDPOINTS",zoneActionTitle);
            GUI.Label(new Rect(587,69,123,24),"02  ROUTES",small);
            GUI.Label(new Rect(704,69,123,24),"03  EVA",small);
            if(GUI.Button(new Rect(1413,36,142,36),showGrid?"GRID ON":"GRID OFF",globalChoice))showGrid=!showGrid;
            GUI.Label(new Rect(441,111,1000,23),IsBusy?"BUILDING REGIONAL ROUTES...":"CLICK TO SET "+(selectEnd?"DESTINATION":"START")+"  ·  DRAG TO ROTATE  ·  SCROLL TO ZOOM",small);
            DrawRegionalMapLabels();
            DrawEndpointCard(new Rect(440,622,550,170),false);
            DrawEndpointCard(new Rect(1006,622,550,170),true);

            float separation=MarsGeospatial.AngularDistanceDegrees(StartLocation,EndLocation)*Mathf.Deg2Rad*(float)MarsRegionalTerrain.RadiusMeters/1000;
            GUI.Label(new Rect(444,810,790,21),$"GEOGRAPHIC SEPARATION  ·  {separation:0.00} km",small);
            GUI.Label(new Rect(444,836,790,37),selectionMessage,inputError?inputErrorStyle:body);
            if(GUI.Button(new Rect(1268,813,288,56),IsBusy?"ANALYZING TERRAIN...":"VIEW ROUTES   ›",globalRoutesAction))SubmitRoutes();
            GUI.enabled=enabledBefore;
            GUI.Label(new Rect(443,877,1110,18),"NASA / JPL-CALTECH VIKING BASEMAP · MGS MOLA / LOCAL STEREO ELEVATION · PHYSICAL HEIGHT SCALE 1:1",small);
            if(!IsBusy)HandleMapPointer(Event.current);
            GUI.matrix=previous;
        }

        private void DrawGlobalSidebar()
        {
            GUI.Label(new Rect(30,23,350,40),"MARSPATH",title);GUI.Label(new Rect(31,69,350,20),"LAGRANGE V / PLAN • SURVIVE • EXPLORE",small);
            GUI.Label(new Rect(30,109,350,24),"EXPLORE MARS",heading);
            GUI.SetNextControlName("SiteSearch");siteSearch=GUI.TextField(new Rect(30,146,350,37),siteSearch,coordinateField);
            if(string.IsNullOrEmpty(siteSearch))GUI.Label(new Rect(41,153,320,24),"Search landmarks…",small);
            int visible=0;foreach(MarsSite site in MarsSiteCatalog.Sites)if(MatchesSite(site))visible++;
            Rect list=new Rect(30,196,350,289);
            landmarkScroll=GUI.BeginScrollView(list,landmarkScroll,new Rect(0,0,332,visible*58));int row=0;
            for(int i=0;i<Landmarks.Length;i++)
            {
                MarsSite site=MarsSiteCatalog.Sites[i];if(!MatchesSite(site))continue;
                Rect rect=new Rect(0,row++*58,332,52);
                bool selected=i==selectedLandmark;
                if(GUI.Button(rect,GUIContent.none,selected?globalZoneAction:globalChoice))FocusLandmark(i);
                if(selected)Fill(new Rect(1,rect.y+11,2,30),Cyan);
                GUI.Label(new Rect(13,rect.y+5,308,23),site.name,heading);
                GUI.Label(new Rect(13,rect.y+28,308,18),site.category,small);
            }
            GUI.EndScrollView();
            if(visible==0)GUI.Label(new Rect(42,list.y+12,315,28),"No matching landmarks",body);

            MarsSite chosen=MarsSiteCatalog.Sites[selectedLandmark];
            GUI.Box(new Rect(30,503,350,133),GUIContent.none,globalCard);
            GUI.Label(new Rect(44,513,322,24),chosen.name,heading);
            GUI.Label(new Rect(44,541,322,44),chosen.description,body);
            if(GUI.Button(new Rect(44,589,153,33),"USE AS START",globalChoice))UseSite(selectedLandmark,false);
            if(GUI.Button(new Rect(209,589,157,33),"USE AS DESTINATION",globalChoice))UseSite(selectedLandmark,true);

            GUI.Label(new Rect(30,654,350,22),"JEZERO MISSIONS",heading);
            string zone=MarsMissionZone.Available?$"JEZERO {MarsMissionZone.WidthKm:0.#} × {MarsMissionZone.HeightKm:0.#} KM ZONE":"JEZERO MISSION ZONE";
            if(DrawJezeroAction(new Rect(30,685,350,60),zone,"Choose both endpoints",false))OpenMissionZone();
            if(DrawJezeroAction(new Rect(30,756,350,60),"JEZERO HACKATHON DEMO","Preset route through Belva Crater",true))JezeroDemoRequested?.Invoke();
            if(GUI.Button(new Rect(30,834,350,34),"RESTORE JEZERO ENDPOINTS",globalChoice))UseJezeroSample();
        }

        private void EnsureGlobalMenuStyles()
        {
            if(globalCard!=null)return;
            globalCardTexture=RoundedGlobalSurface(new Color(.033f,.067f,.087f));
            globalChoiceTexture=RoundedGlobalSurface(new Color(.075f,.12f,.15f));
            globalChoiceHoverTexture=RoundedGlobalSurface(new Color(.115f,.20f,.24f));
            globalZoneTexture=RoundedGlobalSurface(new Color(.045f,.25f,.28f));
            globalZoneHoverTexture=RoundedGlobalSurface(new Color(.075f,.34f,.37f));
            globalAmberTexture=RoundedGlobalSurface(new Color(1f,.66f,.34f));
            globalAmberHoverTexture=RoundedGlobalSurface(new Color(1f,.77f,.50f));
            globalRoutesTexture=RoundedGlobalSurface(new Color(.34f,.83f,.85f));
            globalRoutesHoverTexture=RoundedGlobalSurface(new Color(.56f,.93f,.94f));
            globalCard=new GUIStyle(panel){border=new RectOffset(8,8,8,8),normal={background=globalCardTexture}};
            var darkText=new Color(.027f,.068f,.078f);
            globalChoice=GlobalMenuButton(globalChoiceTexture,globalChoiceHoverTexture,Color.white,11);
            globalZoneAction=GlobalMenuButton(globalZoneTexture,globalZoneHoverTexture,Cyan,13);
            globalDemoAction=GlobalMenuButton(globalAmberTexture,globalAmberHoverTexture,darkText,14);
            globalRoutesAction=GlobalMenuButton(globalRoutesTexture,globalRoutesHoverTexture,darkText,15);
            zoneActionTitle=new GUIStyle(heading){fontSize=13,normal={textColor=Cyan}};
            demoActionTitle=new GUIStyle(zoneActionTitle){normal={textColor=darkText}};
            demoActionDetail=new GUIStyle(small){normal={textColor=darkText}};
        }

        private bool DrawJezeroAction(Rect rect,string name,string description,bool preset)
        {
            bool clicked=GUI.Button(rect,GUIContent.none,preset?globalDemoAction:globalZoneAction);
            GUIStyle titleStyle=preset?demoActionTitle:zoneActionTitle;
            GUI.Label(new Rect(rect.x+14,rect.y+9,rect.width-53,22),name,titleStyle);
            GUI.Label(new Rect(rect.x+14,rect.y+32,rect.width-53,18),description,preset?demoActionDetail:body);
            GUI.Label(new Rect(rect.xMax-30,rect.y+17,22,26),"›",titleStyle);
            return clicked;
        }

        private GUIStyle GlobalMenuButton(Texture2D normal,Texture2D hover,Color textColor,int size)
        {
            return new GUIStyle(button)
            {
                border=new RectOffset(8,8,8,8),fontSize=size,alignment=TextAnchor.MiddleCenter,
                normal={background=normal,textColor=textColor},hover={background=hover,textColor=textColor},
                active={background=normal,textColor=textColor},focused={background=hover,textColor=textColor}
            };
        }

        internal static Texture2D RoundedGlobalSurface(Color fill)
        {
            const int size=20;
            const float radius=6;
            var texture=new Texture2D(size,size,TextureFormat.RGBA32,false)
                {name="MarsPath rounded panel",wrapMode=TextureWrapMode.Clamp,filterMode=FilterMode.Bilinear};
            for(int y=0;y<size;y++)for(int x=0;x<size;x++)
            {
                float dx=Mathf.Max(0,Mathf.Abs(x+.5f-size*.5f)-(size*.5f-radius));
                float dy=Mathf.Max(0,Mathf.Abs(y+.5f-size*.5f)-(size*.5f-radius));
                Color pixel=fill;
                pixel.a*=Mathf.Clamp01(radius+.5f-Mathf.Sqrt(dx*dx+dy*dy));
                texture.SetPixel(x,y,pixel);
            }
            texture.Apply(false,true);
            return texture;
        }

        private void ReleaseGlobalMenuStyles()
        {
            if(globalCardTexture==null)return;
            foreach(Texture2D texture in new[]{globalCardTexture,globalChoiceTexture,globalChoiceHoverTexture,
                globalZoneTexture,globalZoneHoverTexture,globalAmberTexture,globalAmberHoverTexture,
                globalRoutesTexture,globalRoutesHoverTexture})Destroy(texture);
        }

        private bool MatchesSite(MarsSite site)=>string.IsNullOrEmpty(siteSearch)||(site.name+" "+site.category).IndexOf(siteSearch,StringComparison.OrdinalIgnoreCase)>=0;
        private void DrawEndpointCard(Rect rect,bool end)
        {
            GUI.Box(rect,GUIContent.none,globalCard);
            Fill(new Rect(rect.x+1,rect.y+13,3,rect.height-26),end?Orange:Cyan);
            if(GUI.Button(new Rect(rect.x+15,rect.y+11,rect.width-30,30),(end?"02 DESTINATION":"01 START")+(selectEnd==end?"  ·  ACTIVE":""),selectEnd==end?globalZoneAction:globalChoice))selectEnd=end;
            GUI.Label(new Rect(rect.x+16,rect.y+48,250,18),"LATITUDE (-90 TO +90)",small);GUI.Label(new Rect(rect.x+280,rect.y+48,250,18),"LONGITUDE (-180 TO +360)",small);
            GUI.SetNextControlName(end?"EndLatitude":"StartLatitude");string lat=GUI.TextField(new Rect(rect.x+15,rect.y+73,248,33),end?endLat:startLat,coordinateField);
            GUI.SetNextControlName(end?"EndLongitude":"StartLongitude");string lon=GUI.TextField(new Rect(rect.x+280,rect.y+73,254,33),end?endLon:startLon,coordinateField);
            if(end){endLat=lat;endLon=lon;}else{startLat=lat;startLon=lon;}
            GeoPoint p=end?EndLocation:StartLocation;GUI.Label(new Rect(rect.x+16,rect.y+117,345,21),Coordinate(p),body);GUI.Label(new Rect(rect.x+16,rect.y+145,345,18),p.label,small);
            if(GUI.Button(new Rect(rect.x+382,rect.y+120,152,33),"APPLY COORDINATES",globalChoice))ApplyManualCoordinates(end,lat,lon);
        }
        private void DrawRegionalMapLabels()
        {
            EndpointLabelLayout endpoints=GetEndpointLabelLayout();
            var landmarkLabels=new System.Collections.Generic.List<Rect>(4);
            int shown=0;for(int i=0;i<Landmarks.Length&&shown<4;i++)
            {
                Landmark site=Landmarks[i];if(!VisiblePoint(MarsGeospatial.ToUnitSphere(site.lat,site.lon),out Vector2 p))continue;
                GeoPoint point=new GeoPoint{latitude=site.lat,longitude=site.lon};
                if(MarsGeospatial.AngularDistanceDegrees(point,StartLocation)<2||MarsGeospatial.AngularDistanceDegrees(point,EndLocation)<2)continue;
                Rect labelRect=new Rect(Mathf.Clamp(p.x+10,430,1400),Mathf.Clamp(p.y-10,140,555),155,24);
                if(endpoints.startVisible&&ExpandedLabel(endpoints.startRect,8).Overlaps(labelRect)||
                    endpoints.endVisible&&ExpandedLabel(endpoints.endRect,8).Overlaps(labelRect)||
                    endpoints.startVisible&&ExpandedLabel(endpoints.startPin,4).Overlaps(labelRect)||
                    endpoints.endVisible&&ExpandedLabel(endpoints.endPin,4).Overlaps(labelRect))continue;
                bool occupied=false;foreach(Rect used in landmarkLabels)if(ExpandedLabel(used,6).Overlaps(labelRect)){occupied=true;break;}
                if(occupied)continue;
                if(GUI.Button(labelRect,site.name,globalChoice))FocusLandmark(i);shown++;landmarkLabels.Add(labelRect);
            }
            GUI.BeginGroup(MapRect);
            if(endpoints.startVisible)DrawEndpointMapLabel(endpoints.startAnchor,endpoints.startRect,"Start",Cyan);
            if(endpoints.endVisible)DrawEndpointMapLabel(endpoints.endAnchor,endpoints.endRect,"Destination",Orange);
            GUI.EndGroup();
        }

        public struct EndpointLabelLayout
        {
            public bool startVisible,endVisible;
            public Vector2 startAnchor,endAnchor;
            public Rect startRect,endRect,startPin,endPin;
        }

        // The same deterministic layout drives drawing and native acceptance
        // checks. Anchors remain at the exact projected geographic positions.
        public EndpointLabelLayout GetEndpointLabelLayout()
        {
            bool hasStart=VisiblePoint(MarsGeospatial.ToUnitSphere(StartLocation.latitude,StartLocation.longitude),out Vector2 start);
            bool hasEnd=VisiblePoint(MarsGeospatial.ToUnitSphere(EndLocation.latitude,EndLocation.longitude),out Vector2 end);
            return ArrangeEndpointLabels(start,hasStart,end,hasEnd);
        }

        public static Rect EndpointLabelBounds=>new Rect(MapRect.x+12,140,MapRect.width-24,MapRect.yMax-152);

        public static EndpointLabelLayout ArrangeEndpointLabels(Vector2 start,bool hasStart,Vector2 end,bool hasEnd)
        {
            const float startWidth=62,endWidth=93,height=22,gap=9;
            Rect bounds=EndpointLabelBounds;
            Rect aPin=EndpointPinRect(start),bPin=EndpointPinRect(end);
            Rect a=EndpointLabelBeside(start,startWidth,start.x+18+startWidth>bounds.xMax);
            Rect b=EndpointLabelBeside(end,endWidth,end.x+18+endWidth>bounds.xMax);
            if(hasStart&&hasEnd&&EndpointLabelsCrowded(a,b,aPin,bPin))
            {
                // Fan close points toward opposite sides before stacking. This
                // also gives coincident points two readable, distinct labels.
                bool startLeft=start.x<=end.x;
                a=EndpointLabelBeside(start,startWidth,startLeft);
                b=EndpointLabelBeside(end,endWidth,!startLeft);
                if(EndpointLabelsCrowded(a,b,aPin,bPin))
                {
                    bool right=(start.x+end.x)*.5f<MapRect.center.x;
                    float x=right?Mathf.Max(start.x,end.x)+18:Mathf.Min(start.x,end.x)-18;
                    a.x=right?x:x-startWidth;b.x=right?x:x-endWidth;
                    float top=Mathf.Clamp((start.y+end.y)*.5f-15-height-gap*.5f,bounds.yMin,bounds.yMax-height*2-gap);
                    bool startAbove=start.y<=end.y;
                    a.y=top+(startAbove?0:height+gap);
                    b.y=top+(startAbove?height+gap:0);
                    a=ClampEndpointLabel(a,bounds);b=ClampEndpointLabel(b,bounds);
                }
            }
            return new EndpointLabelLayout{startVisible=hasStart,endVisible=hasEnd,startAnchor=start,endAnchor=end,startRect=a,endRect=b,startPin=aPin,endPin=bPin};
        }

        public static Rect EndpointPinRect(Vector2 tip)=>new Rect(tip.x-15*73f/127,tip.y-30,30*73f/127,30);
        private static Rect EndpointLabelBeside(Vector2 tip,float width,bool left)
        {return ClampEndpointLabel(new Rect(left?tip.x-18-width:tip.x+18,tip.y-26,width,22),EndpointLabelBounds);}
        private static bool EndpointLabelsCrowded(Rect a,Rect b,Rect pinA,Rect pinB)=>ExpandedLabel(a,6).Overlaps(b)||
            ExpandedLabel(a,3).Overlaps(pinA)||ExpandedLabel(a,3).Overlaps(pinB)||ExpandedLabel(b,3).Overlaps(pinA)||ExpandedLabel(b,3).Overlaps(pinB);

        private static Rect ClampEndpointLabel(Rect rect,Rect bounds)
        {rect.x=Mathf.Clamp(rect.x,bounds.xMin,bounds.xMax-rect.width);rect.y=Mathf.Clamp(rect.y,bounds.yMin,bounds.yMax-rect.height);return rect;}

        private static Rect ExpandedLabel(Rect rect,float margin)
        {return new Rect(rect.x-margin,rect.y-margin,rect.width+margin*2,rect.height+margin*2);}

        private void DrawEndpointMapLabel(Vector2 anchor,Rect rect,string text,Color color)
        {
            Rect pinRect=EndpointPinRect(anchor);
            pinRect.position-=MapRect.position;rect.position-=MapRect.position;
            Vector2 center=pinRect.center;
            Vector2 attachment=new Vector2(Mathf.Clamp(center.x,rect.xMin,rect.xMax),Mathf.Clamp(center.y,rect.yMin,rect.yMax));
            Vector2 delta=attachment-center;
            float leaderLength=delta.magnitude-pinRect.width*.45f;
            if(leaderLength>1)
            {
                Vector2 lineStart=center+delta.normalized*pinRect.width*.45f;
                Matrix4x4 previous=GUI.matrix;
                GUIUtility.RotateAroundPivot(Mathf.Atan2(delta.y,delta.x)*Mathf.Rad2Deg,lineStart);
                Fill(new Rect(lineStart.x,lineStart.y-.5f,leaderLength,1),new Color(color.r,color.g,color.b,.68f));
                GUI.matrix=previous;
            }
            Color previousColor=GUI.color;GUI.color=Color.white;
            if(endpointPinTexture!=null)GUI.DrawTexture(pinRect,endpointPinTexture,ScaleMode.StretchToFill,true);
            GUI.color=previousColor;
            endpointLabel.normal.textColor=color;
            GUI.Label(rect,text,endpointLabel);
        }
    }
}

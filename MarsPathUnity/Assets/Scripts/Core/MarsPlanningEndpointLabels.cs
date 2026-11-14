using UnityEngine;

namespace MarsPath
{
    /// <summary>Screen-space route markers. Pin tips retain the exact projected
    /// terrain position; only the small text labels move to avoid collisions.</summary>
    public sealed class MarsPlanningEndpointLabels : MonoBehaviour
    {
        public const float PinHeight=30;
        private const float PinAspect=73f/127f;
        private const float LabelHeight=22;
        private const float StartWidth=60;
        private const float DestinationWidth=88;
        private const float PinGap=16;
        private const float StackGap=8;
        private Texture2D pin,pill;
        private GUIStyle label;
        public bool HasPinTexture=>PinTexture!=null;
        private Texture2D PinTexture=>pin!=null?pin:pin=Resources.Load<Texture2D>("NASA/MapPinEmoji");
        public static Rect Viewport=>MarsPathCameraRig.PlanningArea;
        public static Rect LabelBounds=>new Rect(Viewport.x+10,Viewport.y+10,Viewport.width-20,Viewport.height-20);

        public struct Layout
        {
            public bool startVisible,endVisible;
            public Vector2 startAnchor,endAnchor;
            public Rect startPin,endPin,startLabel,endLabel;
        }

        public static Layout Project(Camera camera,RoutePoint[] points)
        {
            if(camera==null||points==null||points.Length<2)return default;
            bool a=ProjectPoint(camera,points[0],out Vector2 start);
            bool b=ProjectPoint(camera,points[points.Length-1],out Vector2 end);
            return Arrange(start,a,end,b);
        }

        private static bool ProjectPoint(Camera camera,RoutePoint point,out Vector2 anchor)
        {
            Vector3 p=camera.WorldToScreenPoint(MarsTerrainGenerator.SurfacePoint(point.x*60,point.z*60,0));
            anchor=new Vector2(p.x/Screen.width*1600,(Screen.height-p.y)/Screen.height*900);
            return p.z>0&&Viewport.Contains(anchor);
        }

        public static Layout Arrange(Vector2 start,bool hasStart,Vector2 end,bool hasEnd)=>
            Arrange(start,hasStart,end,hasEnd,LabelBounds);

        internal static Layout Arrange(Vector2 start,bool hasStart,Vector2 end,bool hasEnd,Rect bounds)
        {
            Rect aPin=PinRect(start),bPin=PinRect(end);
            Rect a=InitialLabel(start,StartWidth,bounds),b=InitialLabel(end,DestinationWidth,bounds);
            if(hasStart&&hasEnd&&Crowded(a,b,aPin,bPin))
            {
                bool startLeft=start.x<=end.x;
                a=Beside(start,a.width,startLeft,bounds);
                b=Beside(end,b.width,!startLeft,bounds);
                if(Crowded(a,b,aPin,bPin))
                {
                    // At a viewport edge both labels use the inward side and
                    // stack. The pins themselves never leave their coordinates.
                    bool right=(start.x+end.x)*.5f<bounds.center.x;
                    float x=right?Mathf.Max(start.x,end.x)+PinGap:Mathf.Min(start.x,end.x)-PinGap;
                    a.x=right?x:x-a.width;b.x=right?x:x-b.width;
                    float top=Mathf.Clamp((start.y+end.y)*.5f-PinHeight*.5f-LabelHeight-StackGap*.5f,
                        bounds.yMin,bounds.yMax-LabelHeight*2-StackGap);
                    bool startAbove=start.y<=end.y;
                    a.y=top+(startAbove?0:LabelHeight+StackGap);
                    b.y=top+(startAbove?LabelHeight+StackGap:0);
                    a=Clamp(a,bounds);b=Clamp(b,bounds);
                }
            }
            return new Layout{startVisible=hasStart,endVisible=hasEnd,startAnchor=start,endAnchor=end,
                startPin=aPin,endPin=bPin,startLabel=a,endLabel=b};
        }

        private static Rect PinRect(Vector2 tip)=>new Rect(tip.x-PinHeight*PinAspect*.5f,tip.y-PinHeight,PinHeight*PinAspect,PinHeight);
        private static Rect InitialLabel(Vector2 tip,float width,Rect bounds)
        {return Beside(tip,width,tip.x+PinGap+width>bounds.xMax,bounds);}
        private static Rect Beside(Vector2 tip,float width,bool left,Rect bounds)
        {return Clamp(new Rect(left?tip.x-PinGap-width:tip.x+PinGap,tip.y-PinHeight*.5f-LabelHeight*.5f,width,LabelHeight),bounds);}
        private static Rect Clamp(Rect rect,Rect bounds)
        {rect.x=Mathf.Clamp(rect.x,bounds.xMin,bounds.xMax-rect.width);rect.y=Mathf.Clamp(rect.y,bounds.yMin,bounds.yMax-rect.height);return rect;}
        private static Rect Expanded(Rect rect,float by)=>new Rect(rect.x-by,rect.y-by,rect.width+by*2,rect.height+by*2);
        private static bool Crowded(Rect a,Rect b,Rect pinA,Rect pinB)=>Expanded(a,6).Overlaps(b)||
            Expanded(a,3).Overlaps(pinA)||Expanded(a,3).Overlaps(pinB)||Expanded(b,3).Overlaps(pinA)||Expanded(b,3).Overlaps(pinB);

        public void Draw(Layout layout,Color startColor,Color endColor)
        {
            EnsureStyle();
            GUI.BeginGroup(Viewport);
            if(layout.startVisible)DrawMarker(layout.startPin,layout.startLabel,"Start",startColor);
            if(layout.endVisible)DrawMarker(layout.endPin,layout.endLabel,"Destination",endColor);
            GUI.EndGroup();
        }

        private void DrawMarker(Rect marker,Rect caption,string text,Color accent)
        {
            marker.position-=Viewport.position;caption.position-=Viewport.position;
            Vector2 center=marker.center;
            Vector2 attach=new Vector2(Mathf.Clamp(center.x,caption.xMin,caption.xMax),Mathf.Clamp(center.y,caption.yMin,caption.yMax));
            Vector2 delta=attach-center;
            float length=delta.magnitude-marker.width*.45f;
            if(length>2)
            {
                Vector2 from=center+delta.normalized*marker.width*.45f;
                Matrix4x4 previousMatrix=GUI.matrix;Color previousColor=GUI.color;
                GUIUtility.RotateAroundPivot(Mathf.Atan2(delta.y,delta.x)*Mathf.Rad2Deg,from);
                GUI.color=new Color(accent.r,accent.g,accent.b,.66f);
                GUI.DrawTexture(new Rect(from.x,from.y-.5f,length,1),Texture2D.whiteTexture);
                GUI.color=previousColor;GUI.matrix=previousMatrix;
            }
            Color saved=GUI.color;GUI.color=Color.white;
            if(PinTexture!=null)GUI.DrawTexture(marker,PinTexture,ScaleMode.StretchToFill,true);
            GUI.color=saved;
            label.normal.textColor=accent;GUI.Label(caption,text,label);
        }

        private void EnsureStyle()
        {
            if(label!=null)return;
            pill=new Texture2D(48,24,TextureFormat.RGBA32,false){name="Regional endpoint caption",wrapMode=TextureWrapMode.Clamp};
            for(int y=0;y<24;y++)for(int x=0;x<48;x++)
            {
                Vector2 q=new Vector2(Mathf.Abs(x+.5f-24)-14,Mathf.Abs(y+.5f-12)-2);
                float edge=new Vector2(Mathf.Max(q.x,0),Mathf.Max(q.y,0)).magnitude+Mathf.Min(Mathf.Max(q.x,q.y),0)-10;
                pill.SetPixel(x,y,new Color(.018f,.035f,.043f,.76f*Mathf.Clamp01(.5f-edge)));
            }
            pill.Apply(false,true);
            label=new GUIStyle(GUI.skin.label){fontSize=11,fontStyle=FontStyle.Bold,alignment=TextAnchor.MiddleCenter,
                border=new RectOffset(10,10,10,10),padding=new RectOffset(9,9,2,2),wordWrap=false,
                clipping=TextClipping.Clip,normal={background=pill,textColor=Color.white}};
        }

        private void OnDestroy(){if(pill!=null)Destroy(pill);}
    }
}

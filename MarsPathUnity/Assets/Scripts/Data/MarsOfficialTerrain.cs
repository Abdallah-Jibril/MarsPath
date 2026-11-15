using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

namespace MarsPath
{
    /// <summary>Coverage-aware official HiRISE/CTX stereo terrain. No elevations
    /// are derived from image brightness and no noise is added to these heights.</summary>
    public static partial class MarsOfficialTerrain
    {
        [Serializable] public sealed class Asset
        {public string id,kind,url,ortho;public double[] bbox,transform;public float spacing,scale=1,offset,nodata;}
        [Serializable] private sealed class Catalog{public string retrieved,source;public Asset[] assets;}
        [Serializable] private sealed class Crop
        {public string id,kind,source;public int width,height;public double west,north,dx,dy;public float spacing;[NonSerialized]public float[] values;[NonSerialized]public byte[] edge;[NonSerialized]public Dictionary<int,float> gaps;[NonSerialized]public long touched;[NonSerialized]public string cacheKey;[NonSerialized]public bool missionZone;}
        private static Asset[] catalog;
        private static readonly List<Crop> crops=new List<Crop>();
        private static readonly Dictionary<string,float> attempted=new Dictionary<string,float>();
        private static bool bundledLoaded;
        private static long touch;
        private static void EnsureBundled()
        {
            if(bundledLoaded)return;bundledLoaded=true;
            TextAsset info=Resources.Load<TextAsset>("NASA/Stereo/JezeroHiRise"),data=Resources.Load<TextAsset>("NASA/Stereo/JezeroHiRiseHeights");
            if(info==null||data==null)return;
            byte[] bytes=data.bytes;Crop crop=JsonUtility.FromJson<Crop>(info.text);if(bytes.Length!=crop.width*crop.height*4)return;
            crop.values=new float[bytes.Length/4];Buffer.BlockCopy(bytes,0,crop.values,0,bytes.Length);Remember(crop);
            TextAsset image=Resources.Load<TextAsset>("NASA/Stereo/JezeroHiRiseImage");if(image!=null)images.Add(JsonUtility.FromJson<ImageRecord>(image.text));
            LoadBundledWindows();
        }
        public static int Revision{get;private set;}
        public static int CatalogCount{get{LoadCatalog();return catalog.Length;}}
        public static string Status{get;private set;}="Official stereo terrain not loaded";
        public static string LastError{get;private set;}="";
        private static string Cache=>Path.Combine(Application.persistentDataPath,"OfficialTerrain","v1","windows");
        private static void LoadCatalog()
        {if(catalog!=null)return;TextAsset file=Resources.Load<TextAsset>("NASA/OfficialDtmCatalog");catalog=file==null?new Asset[0]:JsonUtility.FromJson<Catalog>(file.text).assets;}
        public static Asset[] Candidates(GeoPoint point)
        {
            LoadCatalog();var found=catalog.Where(a=>Contains(a,point))
                .OrderBy(a=>a.spacing).ThenBy(a=>Math.Abs(point.longitude-(a.bbox[0]+a.bbox[2])*.5)+Math.Abs(point.latitude-(a.bbox[1]+a.bbox[3])*.5)).ToArray();
            return found.Where(a=>a.kind=="HiRISE").Take(4).Concat(found.Where(a=>a.kind=="CTX").Take(4)).ToArray();
        }
        private static bool Contains(Asset a,GeoPoint p)
        {
            if(a.bbox==null||a.bbox.Length!=4||p.latitude<a.bbox[1]||p.latitude>a.bbox[3])return false;
            // Narrow images crossing 180 degrees can have a numerically huge
            // STAC envelope. The valid footprint is the complementary interval.
            return a.bbox[2]-a.bbox[0]>180?p.longitude<=a.bbox[0]||p.longitude>=a.bbox[2]:p.longitude>=a.bbox[0]&&p.longitude<=a.bbox[2];
        }
        public static bool TryElevation(GeoPoint point,out float height)
        {return TrySample(point.latitude,point.longitude,out height,out _);}
        internal static bool TrySample(double latitude,double longitude,out float height,out float blend)
        {
            EnsureBundled();
            if(MarsMissionZone.ElevationsActive)EnsureMissionZone();
            // The producer's coherent mission mosaic is authoritative inside
            // its audited bounds. Cached equal/finer individual strips may use
            // a different registration and must not override it after a seek.
            if(MarsMissionZone.ElevationsActive&&TryMissionZoneSample(latitude,longitude,out height)){blend=1;return true;}
            // Compose coarse-to-fine coverage, including the distance to real
            // nodata edges. A HiRISE footprint fades into measured CTX context.
            height=0;blend=0;
            foreach(Crop c in crops)if((!c.missionZone||MarsMissionZone.ElevationsActive)&&PreferredSample(c,latitude,longitude,out float value,out float weight,out _))
            {height=height*(1-weight)+value*weight;blend=blend*(1-weight)+weight;}
            if(blend<=0)return false;height/=blend;return true;
        }
        public static bool TryWorld(GeoPoint origin,float x,float z,float metres,out float height,out float blend)
        {
            double lat=origin.latitude*Math.PI/180,lon=origin.longitude*Math.PI/180,angle=Math.Sqrt((double)x*x+(double)z*z)*metres/MarsRegionalTerrain.RadiusMeters,bearing=Math.Atan2(x,z);
            double resultLat=Math.Asin(Math.Sin(lat)*Math.Cos(angle)+Math.Cos(lat)*Math.Sin(angle)*Math.Cos(bearing));
            double resultLon=lon+Math.Atan2(Math.Sin(bearing)*Math.Sin(angle)*Math.Cos(lat),Math.Cos(angle)-Math.Sin(lat)*Math.Sin(resultLat));
            return TrySample(resultLat*180/Math.PI,resultLon*180/Math.PI,out height,out blend);
        }
        public static bool TryMeasured(GeoPoint point,out float height,out float blend)=>TrySample(point.latitude,point.longitude,out height,out blend);
        public static float Spacing(GeoPoint point)
        {EnsureBundled();if(MarsMissionZone.ElevationsActive&&TryMissionZoneElevation(point,out _))return MarsMissionZone.SpacingMeters;float best=float.MaxValue;foreach(Crop c in crops)if((!c.missionZone||MarsMissionZone.ElevationsActive)&&c.spacing<best&&Sample(c,point,out _))best=c.spacing;return best<float.MaxValue?best:0;}
        public static string Source(GeoPoint point)
        {EnsureBundled();if(MarsMissionZone.ElevationsActive&&TryMissionZoneElevation(point,out _))return $"NASA / USGS HiRISE TRN / {MarsMissionZone.SpacingMeters:0.0} m grid";Crop best=null;foreach(Crop c in crops)if((!c.missionZone||MarsMissionZone.ElevationsActive)&&(best==null||c.spacing<best.spacing)&&Sample(c,point,out _))best=c;return best==null?null:$"NASA / {best.kind} DTM / {best.spacing:0.0} m grid"+(IsEstimated(point)?" / GAP ESTIMATE":"");}
        public static bool IsEstimated(GeoPoint point)
        {
            if(point==null)return false;
            EnsureBundled();bool estimated=false;
            if(MarsMissionZone.ElevationsActive&&TryMissionZoneElevation(point,out _))return false;
            foreach(Crop crop in crops)
            {
                if(crop.missionZone&&!MarsMissionZone.ElevationsActive)continue;
                if(!PreferredSample(crop,point.latitude,point.longitude,out _,out float weight,out bool localEstimate))continue;
                estimated=localEstimate||(weight<.999999f&&estimated);
            }
            return estimated;
        }
        private static bool Sample(Crop c,GeoPoint p,out float value)
        {return PreferredSample(c,p.latitude,p.longitude,out value,out _,out _);}
        private static bool PreferredSample(Crop crop,double latitude,double longitude,out float value,out float blend,out bool estimated)
        {
            if(!Sample(crop,latitude,longitude,out value,out blend,out estimated))return false;
            if(!estimated)return true;
            // A display estimate must never replace a real measurement from an
            // overlapping crop of equal or finer resolution, regardless of sort order.
            foreach(Crop other in crops)
                if((!other.missionZone||MarsMissionZone.ElevationsActive)&&other!=crop&&other.spacing<=crop.spacing&&Sample(other,latitude,longitude,out _,out _,out bool otherEstimate)&&!otherEstimate)
                    return false;
            return true;
        }
        private static bool Sample(Crop c,double latitude,double longitude,out float value,out float blend)
        {return Sample(c,latitude,longitude,out value,out blend,out _);}
        private static bool Sample(Crop c,double latitude,double longitude,out float value,out float blend,out bool estimated)
        {
            value=0;blend=0;estimated=false;double lon=longitude,around=(c.west+c.width*c.dx*.5)/3396190*180/Math.PI;
            while(lon-around>180)lon-=360;while(lon-around< -180)lon+=360;
            double x=(lon*Math.PI/180*3396190-c.west)/c.dx-.5,y=(latitude*Math.PI/180*3396190-c.north)/c.dy-.5;
            int ix=(int)Math.Floor(x),iy=(int)Math.Floor(y);if(ix<2||iy<2||ix>=c.width-3||iy>=c.height-3)return false;
            int k=iy*c.width+ix;
            float a=DisplaySample(c,k,out bool estimateA),b=DisplaySample(c,k+1,out bool estimateB);
            float d=DisplaySample(c,k+c.width,out bool estimateD),e=DisplaySample(c,k+c.width+1,out bool estimateE);
            if(!Valid(a)||!Valid(b)||!Valid(d)||!Valid(e))return false;
            estimated=estimateA||estimateB||estimateD||estimateE;
            float tx=(float)(x-ix),ty=(float)(y-iy);
            value=Mathf.Lerp(Mathf.Lerp(a,b,tx),Mathf.Lerp(d,e,tx),ty);
            // The approved offline sector has real measured support throughout.
            // Identical-source tile seams must not blend back to coarse MOLA.
            if(c.missionZone&&!estimated&&MarsMissionZone.BoundsContain(latitude,longitude)){blend=1;return true;}
            // Feather the *source support*, not the scientific heights. Taking
            // min(edge[quad]) made this weight constant in each source cell:
            // two DTMs with different height datums became a sawtooth staircase.
            // Bilinear distance is C0 across valid cells. Reserve two support
            // cells so its weight reaches zero before any bilinear height quad
            // touches nodata (including a diagonal invalid corner).
            float support = c.missionZone
                ? Mathf.Lerp(Mathf.Lerp(RectangularSupport(c,ix,iy),RectangularSupport(c,ix+1,iy),tx),
                    Mathf.Lerp(RectangularSupport(c,ix,iy+1),RectangularSupport(c,ix+1,iy+1),tx),ty)-2
                : c.edge==null?1000:Mathf.Lerp(Mathf.Lerp(c.edge[k],c.edge[k+1],tx),Mathf.Lerp(c.edge[k+c.width],c.edge[k+c.width+1],tx),ty)-2;
            float distance=Mathf.Max(0,support)*c.spacing;
            // A coarse fallback and a local stereo DTM may differ by tens of
            // metres at nodata boundaries. A four-cell transition formed false
            // steep walls there; distribute that source transition over 12 cells.
            blend=Mathf.SmoothStep(0,1,Mathf.Clamp01(distance/Mathf.Max(80,c.spacing*12)));return blend>0;
        }
        private static bool Valid(float value)=>!float.IsNaN(value)&&!float.IsInfinity(value)&&value> -10000&&value<30000;
        private static int RectangularSupport(Crop crop,int x,int y) =>
            Mathf.Min(255, Mathf.Min(Mathf.Min(x, crop.width - 1 - x), Mathf.Min(y, crop.height - 1 - y)));
        private static float DisplaySample(Crop crop,int index,out bool estimated)
        {
            float value=crop.values[index];estimated=false;
            if(Valid(value))return value;
            if(crop.gaps!=null&&crop.gaps.TryGetValue(index,out float estimate)){estimated=true;return estimate;}
            return value;
        }
        private static string Key(Asset a,GeoPoint p)=>a.id+"_"+Math.Floor(p.latitude/.015)+"_"+Math.Floor(p.longitude/.015);
        private static void Remember(Crop c)
        {
            if(c.gaps==null)
            {
                c.gaps=MarsDtmVoidEstimates.Build(c.values,c.width,c.height,c.spacing);
                c.edge=null;
            }
            // The audited Jezero tiles have no interior nodata. Their exact
            // rectangular edge distances do not need a byte per elevation post.
            if(c.edge==null&&!c.missionZone)
            {
                c.edge=new byte[c.values.Length];for(int i=0;i<c.edge.Length;i++)c.edge[i]=Valid(c.values[i])||c.gaps.ContainsKey(i)?(byte)255:(byte)0;
                for(int y=0;y<c.height;y++)for(int x=0;x<c.width;x++){int i=y*c.width+x;if(x==0||y==0||x==c.width-1||y==c.height-1)c.edge[i]=0;else c.edge[i]=(byte)Math.Min(c.edge[i],Math.Min(255,Math.Min(c.edge[i-1],c.edge[i-c.width])+1));}
                for(int y=c.height-2;y>=1;y--)for(int x=c.width-2;x>=1;x--){int i=y*c.width+x;c.edge[i]=(byte)Math.Min(c.edge[i],Math.Min(255,Math.Min(c.edge[i+1],c.edge[i+c.width])+1));}
            }
            c.touched=++touch;crops.RemoveAll(x=>x.id==c.id&&Math.Abs(x.west-c.west)<.01&&Math.Abs(x.north-c.north)<.01);crops.Add(c);
            long size=crops.Where(x=>!x.missionZone).Sum(x=>(long)x.values.Length*5+(x.gaps?.Count??0)*24L);
            while((size>256L*1024*1024||crops.Count(x=>!x.missionZone)>24)&&crops.Count>1){Crop oldest=crops.Where(x=>!x.missionZone).OrderBy(x=>x.touched).FirstOrDefault();if(oldest==null)break;size-=oldest.values.Length*5L+(oldest.gaps?.Count??0)*24L;crops.Remove(oldest);}
            crops.Sort((a,b)=>b.spacing.CompareTo(a.spacing));
            Revision++;Status=$"{c.kind} / {c.spacing:0.0} m / {c.id}";
        }
        private static bool NoCoarserThan(float resident,float candidate)
        {return resident<=candidate+Mathf.Max(.001f,candidate*.0001f);}
        private static bool SupportsMeasuredSource(Crop crop,GeoPoint point,float maximumSpacing)
        {
            return (!crop.missionZone||MarsMissionZone.ElevationsActive)&&NoCoarserThan(crop.spacing,maximumSpacing)&&
                Sample(crop,point.latitude,point.longitude,out _,out float coverage,out bool estimate)&&coverage>.99f&&!estimate;
        }
        private static bool SupportsMeasuredContext(GeoPoint point,float targetSpacing)
        {
            return MarsRegionalTerrain.InJezeroCrop(point)&&NoCoarserThan(20,targetSpacing)||
                crops.Any(c=>(c.kind=="CTX"||c.kind=="HiRISE"&&c.spacing<=2)&&SupportsMeasuredSource(c,point,targetSpacing));
        }
        public static IEnumerator Prepare(GeoPoint focus,Action<float,string> progress=null,Func<bool> cancelled=null,bool imagery=true)
        {
            EnsureBundled();RestoreCachedGeometry(focus);
            float began=Time.realtimeSinceStartup;Func<bool> callerCancelled=cancelled;
            cancelled=()=>callerCancelled!=null&&callerCancelled()||Time.realtimeSinceStartup-began>30;
            Asset[] choices=Candidates(focus);if(choices.Length==0){Status="No stereo DTM at this location / using HRSC-MOLA";yield break;}
            // Obtain a useful measured footprint before spending the complete
            // request budget on narrow HiRISE strips. Cached fine measurements
            // still win; this changes download priority, never source priority.
            float ctxTarget=choices.Where(a=>a.kind=="CTX").Select(a=>a.spacing).DefaultIfEmpty(20).Min();
            bool ctxReady=SupportsMeasuredContext(focus,ctxTarget);
            int hiCount=0,ctxCount=0;
            foreach(Asset asset in choices.OrderBy(a=>a.kind=="CTX"?0:1).ThenBy(a=>a.spacing))
            {
                if(cancelled())yield break;
                if(asset.kind=="CTX"&&(ctxReady||Time.realtimeSinceStartup-began>=12))continue;
                if(asset.kind=="HiRISE"&&hiCount++>=4||asset.kind=="CTX"&&ctxCount++>=2)continue;
                if(crops.Any(c=>c.kind==asset.kind&&SupportsMeasuredSource(c,focus,asset.spacing))){if(asset.kind=="CTX")ctxReady=SupportsMeasuredContext(focus,ctxTarget);continue;}
                string key=Key(asset,focus);Crop crop=crops.FirstOrDefault(c=>c.cacheKey==key);Directory.CreateDirectory(Cache);
                string metadata=Path.Combine(Cache,key+".json"),binary=Path.Combine(Cache,key+".f32");
                try
                {
                    if(crop==null&&File.Exists(metadata)&&File.Exists(binary))
                    {crop=JsonUtility.FromJson<Crop>(File.ReadAllText(metadata));byte[] bytes=File.ReadAllBytes(binary);if(crop.width<=0||crop.height<=0||crop.width>3072||crop.height>3072||bytes.Length!=crop.width*crop.height*4)crop=null;else{crop.values=new float[bytes.Length/4];Buffer.BlockCopy(bytes,0,crop.values,0,bytes.Length);}}
                }
                catch(Exception){crop=null;}
                if(crop==null&&(!attempted.TryGetValue(key,out float retry)||Time.realtimeSinceStartup>=retry))
                {
                    // A cancelled or timed-out download is retryable. A single
                    // attempt must not blacklist finer terrain for the session.
                    attempted[key]=Time.realtimeSinceStartup+35;MarsCogWindow.Window window=null;
                    float requestBegan=Time.realtimeSinceStartup;
                    float requestBudget=asset.kind=="CTX"?Mathf.Min(10,12-(requestBegan-began)):30;
                    Func<bool> requestCancelled=()=>cancelled()||Time.realtimeSinceStartup-requestBegan>requestBudget;
                    yield return MarsCogWindow.Read(asset.url,focus,asset.kind=="HiRISE"?800:13000,requestCancelled,p=>progress?.Invoke(p,$"Loading official {asset.kind} stereo elevations"),w=>window=w);
                    if(window!=null)
                    {
                        for(int i=0;i<window.values.Length;i++){float h=window.values[i];window.values[i]=h==asset.nodata||!Valid(h)?float.NaN:h*asset.scale+asset.offset;}
                        crop=new Crop{id=asset.id,kind=asset.kind,source=asset.url,width=window.width,height=window.height,west=window.west,north=window.north,dx=window.dx,dy=window.dy,spacing=window.spacing,values=window.values};
                        try{byte[] bytes=new byte[crop.values.Length*4];Buffer.BlockCopy(crop.values,0,bytes,0,bytes.Length);File.WriteAllBytes(binary,bytes);File.WriteAllText(metadata,JsonUtility.ToJson(crop));}catch(IOException){}
                    }
                    else{LastError=MarsCogWindow.LastError;attempted[key]=Time.realtimeSinceStartup+15;}
                }
                if(crop!=null){crop.cacheKey=key;if(!crops.Contains(crop))Remember(crop);if(asset.kind=="CTX")ctxReady=SupportsMeasuredContext(focus,ctxTarget);}
            }
            if(imagery)yield return PrepareImagery(focus,choices,progress,cancelled);
            progress?.Invoke(1,Status);
        }
        private static Texture2D MakeImage(MarsCogWindow.Window image)
        {
            var samples=new List<float>();int stride=Math.Max(1,image.values.Length/16384);
            for(int i=0;i<image.values.Length;i+=stride)if(image.values[i]>0&&image.values[i]<65535)samples.Add(image.values[i]);
            float[] valid=samples.ToArray();if(valid.Length<100)return null;
            Array.Sort(valid);float min=valid[(int)(valid.Length*.02f)],max=valid[(int)(valid.Length*.98f)];
            Color32[] pixels=new Color32[image.values.Length];
            for(int y=0;y<image.height;y++)for(int x=0;x<image.width;x++)
            {float h=image.values[y*image.width+x];byte v=(byte)Mathf.Clamp(Mathf.InverseLerp(min,max,h)*220+18,0,255);pixels[(image.height-1-y)*image.width+x]=h>0&&h<65535?new Color32(v,v,v,255):new Color32(0,0,0,0);}
            var texture=new Texture2D(image.width,image.height,TextureFormat.RGBA32,true,false){name="Official HiRISE matching orthoimage",wrapMode=TextureWrapMode.Clamp,filterMode=FilterMode.Trilinear,anisoLevel=12};texture.SetPixels32(pixels);texture.Apply(true,false);
            return texture;
        }
    }
}

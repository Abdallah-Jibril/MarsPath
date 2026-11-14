using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Globalization;
using UnityEngine;
using UnityEngine.Networking;

namespace MarsPath
{
    /// <summary>Original NASA PDS HRSC areoid strips. Their sinusoidal map
    /// projection and 3396 km radius are retained; no brightness displacement.</summary>
    public static partial class MarsHrscStereoTerrain
    {
        [Serializable]public sealed class Strip
        {public string id,url,resource,sourceLabel,sha256;public int width,height,dataOffset;public double radius,longitude,west,north,spacing;[NonSerialized]public byte[] bytes;[NonSerialized]public byte[] supportEdge;}
        [Serializable]private class Index{public Strip[] strips;}
        [Serializable]private class Reply{public Result ODEResults;}
        [Serializable]private class Result{public Products Products;public string Status,Error;}
        [Serializable]private class Products{public Product[] Product;}
        [Serializable]private class Product{public string pdsid;public ProductFiles Product_files;}
        [Serializable]private class ProductFiles{public ProductFile[] Product_file;}
        [Serializable]private class ProductFile{public string Type,URL,KBytes;}
        private static readonly List<Strip> strips=new List<Strip>();
        private static readonly Dictionary<string,float> requested=new Dictionary<string,float>();
        private static bool loaded;
        public static int Revision{get;private set;}
        public static string LastError{get;private set;}="";
        private static string Cache=>Path.Combine(Application.persistentDataPath,"HrscStereo","v1");
        private static void Load()
        {
            if(loaded)return;loaded=true;TextAsset catalog=Resources.Load<TextAsset>("NASA/HRSC/StripCatalog");if(catalog==null)return;
            foreach(Strip strip in JsonUtility.FromJson<Index>(catalog.text).strips)
            {TextAsset file=Resources.Load<TextAsset>(strip.resource);if(file==null)continue;strip.bytes=file.bytes;if(ValidLayout(strip))strips.Add(strip);}
            strips.Sort((a,b)=>b.spacing.CompareTo(a.spacing));Revision++;
        }
        private static bool ValidLayout(Strip s)=>s.width>1&&s.height>1&&s.width<50000&&s.height<100000&&s.dataOffset>0&&s.dataOffset+(long)s.width*s.height*2<=s.bytes.Length;
        private static short Value(Strip s,int x,int y){int i=s.dataOffset+(y*s.width+x)*2;return(short)(s.bytes[i]<<8|s.bytes[i+1]);}
        private static bool Sample(Strip s,GeoPoint p,out float value,out float weight)
        {
            value=0;weight=0;double phi=p.latitude*Math.PI/180,dl=MarsGeospatial.NormalizeLongitude(p.longitude-(float)s.longitude)*Math.PI/180;
            double x=(s.radius*Math.Cos(phi)*dl-s.west)/s.spacing-.5,y=(s.north-s.radius*phi)/s.spacing-.5;
            int ix=(int)Math.Floor(x),iy=(int)Math.Floor(y);if(ix<1||iy<1||ix>=s.width-2||iy>=s.height-2)return false;
            short a=Value(s,ix,iy),b=Value(s,ix+1,iy),c=Value(s,ix,iy+1),d=Value(s,ix+1,iy+1);if(a==-32768||b==-32768||c==-32768||d==-32768)return false;
            float tx=(float)(x-ix),ty=(float)(y-iy);
            value=Mathf.Lerp(Mathf.Lerp(a,b,tx),Mathf.Lerp(c,d,tx),ty);
            EnsureSupportEdge(s);int k=iy*s.width+ix;
            // Continuous source support prevents a change of strip/datum from
            // becoming a stair at an integer source row, column or nodata hole.
            // The actual native bilinear elevations above stay unchanged.
            float edge=Mathf.Lerp(Mathf.Lerp(s.supportEdge[k],s.supportEdge[k+1],tx),Mathf.Lerp(s.supportEdge[k+s.width],s.supportEdge[k+s.width+1],tx),ty)-2;
            weight=Mathf.SmoothStep(0,1,Mathf.Clamp01(edge/12f));return weight>0;
        }
        private static void EnsureSupportEdge(Strip s)
        {
            if(s.supportEdge!=null)return;
            byte[] edge=s.supportEdge=new byte[s.width*s.height];
            for(int i=0;i<edge.Length;i++){int b=s.dataOffset+i*2;edge[i]=(short)(s.bytes[b]<<8|s.bytes[b+1])==-32768?(byte)0:(byte)255;}
            for(int y=0;y<s.height;y++)for(int x=0;x<s.width;x++)
            {int i=y*s.width+x;if(x==0||y==0||x==s.width-1||y==s.height-1)edge[i]=0;else edge[i]=(byte)Math.Min(edge[i],Math.Min(255,Math.Min(edge[i-1],edge[i-s.width])+1));}
            for(int y=s.height-2;y>=1;y--)for(int x=s.width-2;x>=1;x--)
            {int i=y*s.width+x;edge[i]=(byte)Math.Min(edge[i],Math.Min(255,Math.Min(edge[i+1],edge[i+s.width])+1));}
        }
        public static bool TryElevation(GeoPoint p,out float value,out float weight)
        {
            Load();RestoreTeamCachedGeometry(p);value=weight=0;
            foreach(Strip s in strips)if(Sample(s,p,out float h,out float w)){value=value*(1-w)+h*w;weight=weight*(1-w)+w;}
            if(weight<=0)return false;value/=weight;return true;
        }
        public static float Spacing(GeoPoint p){Load();RestoreTeamCachedGeometry(p);double best=double.MaxValue;foreach(Strip s in strips)if(s.spacing<best&&Sample(s,p,out _,out _))best=s.spacing;return best<double.MaxValue?(float)best:0;}
        public static string Source(GeoPoint p){Load();RestoreTeamCachedGeometry(p);Strip best=null;foreach(Strip s in strips)if((best==null||s.spacing<best.spacing)&&Sample(s,p,out _,out _))best=s;return best==null?null:(string.IsNullOrEmpty(best.sourceLabel)?"NASA PDS / HRSC stereo":best.sourceLabel)+" / "+best.spacing.ToString("0")+" m grid";}
        private sealed class Bounded:DownloadHandlerScript
        {
            private readonly MemoryStream stream=new MemoryStream();private readonly int limit;
            public Bounded(int max):base(new byte[65536]){limit=max;}
            protected override bool ReceiveData(byte[] data,int count){if(stream.Length+count>limit)return false;stream.Write(data,0,count);return true;}
            protected override byte[] GetData()=>stream.ToArray();
        }
        public static IEnumerator Prepare(GeoPoint focus,Func<bool> cancelled=null)
        {
            Load();
            float existingSpacing=Spacing(focus),officialSpacing=MarsOfficialTerrain.Spacing(focus);
            if(existingSpacing>0&&existingSpacing<=75||officialSpacing>0&&officialSpacing<=75&&!MarsOfficialTerrain.IsEstimated(focus))yield break;
            yield return PrepareTeam(focus,cancelled);
            if(Spacing(focus)>0||cancelled!=null&&cancelled())yield break;
            float began=Time.realtimeSinceStartup;Func<bool> callerCancelled=cancelled;
            cancelled=()=>callerCancelled!=null&&callerCancelled()||Time.realtimeSinceStartup-began>25;
            string key=Math.Floor(focus.latitude*4)+"_"+Math.Floor(focus.longitude*4);
            if(requested.TryGetValue(key,out float retry)&&Time.realtimeSinceStartup<retry)yield break;
            requested[key]=Time.realtimeSinceStartup+60;
            Directory.CreateDirectory(Cache);string queryFile=Path.Combine(Cache,key+".json");string json=null;
            if(File.Exists(queryFile))try{json=File.ReadAllText(queryFile);}catch(IOException){}
            if(json!=null)try{Result cached=JsonUtility.FromJson<Reply>(json)?.ODEResults;if(cached==null||cached.Status!="Success"||!string.IsNullOrEmpty(cached.Error))json=null;}catch(Exception){json=null;}
            if(json==null)
            {
                string url=QueryUrl(focus);
                using(var request=new UnityWebRequest(url,"GET",new Bounded(4*1024*1024),null){timeout=15})
                {var operation=request.SendWebRequest();while(!operation.isDone){if(cancelled()){request.Abort();yield break;}yield return null;}if(request.result!=UnityWebRequest.Result.Success)yield break;json=Encoding.UTF8.GetString(request.downloadHandler.data);try{File.WriteAllText(queryFile,json);}catch(IOException){}}
            }
            Product[] products=null;try{products=JsonUtility.FromJson<Reply>(json)?.ODEResults?.Products?.Product;}catch(Exception){}if(products==null)yield break;
            int attempts=0;
            foreach(Product p in products)
            {
                if(cancelled!=null&&cancelled())yield break;
                if(p.pdsid==null||!p.pdsid.EndsWith("_DA4.IMG",StringComparison.OrdinalIgnoreCase)||strips.Any(s=>s.id==p.pdsid)||attempts++>=3)continue;
                ProductFile f=p.Product_files?.Product_file?.FirstOrDefault(a=>a.Type=="Product"&&a.URL!=null&&a.URL.StartsWith("https://pds-geosciences.wustl.edu/",StringComparison.Ordinal));
                if(f==null||!int.TryParse(f.KBytes,out int kb)||kb>65536)continue;
                string file=Path.Combine(Cache,p.pdsid+".bytes");byte[] data=null;if(File.Exists(file))try{data=File.ReadAllBytes(file);}catch(IOException){}
                if(data==null)
                {
                    using(var request=new UnityWebRequest(f.URL,"GET",new Bounded(64*1024*1024),null){timeout=35})
                    {var operation=request.SendWebRequest();while(!operation.isDone){if(cancelled()){request.Abort();yield break;}yield return null;}if(request.result!=UnityWebRequest.Result.Success)continue;data=request.downloadHandler.data;}
                }
                Strip strip=null;try{strip=Parse(data,p.pdsid,f.URL);}catch(Exception e){LastError=e.Message;}
                if(strip==null||strip.spacing>175)continue;
                if(!File.Exists(file))try{File.WriteAllBytes(file,data);}catch(IOException){}
                strips.Add(strip);strips.Sort((a,b)=>b.spacing.CompareTo(a.spacing));Revision++;
                while(strips.Sum(s=>(long)s.bytes.Length)>128L*1024*1024&&strips.Count(s=>string.IsNullOrEmpty(s.resource))>1)
                {Strip old=strips.First(s=>string.IsNullOrEmpty(s.resource)&&s!=strip);strips.Remove(old);}
                if(Spacing(focus)>0)yield break;
            }
        }
        public static string QueryUrl(GeoPoint focus)
        {
            // ODE rejects point-sized boxes (minlat==maxlat). Query a real,
            // bounded ~12 m envelope and validate raster nodata after retrieval.
            const double epsilon=.0001;double lon=Mathf.Repeat(focus.longitude,360);
            string minlat=Math.Max(-90,focus.latitude-epsilon).ToString("R",CultureInfo.InvariantCulture),maxlat=Math.Min(90,focus.latitude+epsilon).ToString("R",CultureInfo.InvariantCulture);
            string west=Math.Max(0,lon-epsilon).ToString("R",CultureInfo.InvariantCulture),east=Math.Min(360,lon+epsilon).ToString("R",CultureInfo.InvariantCulture);
            return "https://oderest.rsl.wustl.edu/live2/?query=product&target=mars&ihid=mex&iid=hrsc&pt=DTMRDR&minlat="+minlat+"&maxlat="+maxlat+"&westernlon="+west+"&easternlon="+east+"&results=fp&output=JSON&limit=100";
        }
        private static Strip Parse(byte[] data,string id,string url)
        {
            string header=Encoding.ASCII.GetString(data,0,Math.Min(65536,data.Length));
            Func<string,string> field=key=>Regex.Match(header,@"(?m)^\s*"+Regex.Escape(key)+@"\s*=\s*([^\r\n]+)").Groups[1].Value.Trim();
            Func<string,double> number=key=>double.Parse(Regex.Match(field(key),@"[-+]?\d+(?:\.\d+)?(?:[eE][-+]?\d+)?").Value,CultureInfo.InvariantCulture);
            if(!field("MAP_PROJECTION_TYPE").Contains("SINUSOIDAL")||!field("SAMPLE_TYPE").Contains("MSB_INTEGER")||number("SAMPLE_BITS")!=16)throw new InvalidDataException("Unsupported HRSC scientific projection/layout");
            double spacing=number("MAP_SCALE")*1000;
            var strip=new Strip{id=id,url=url,width=(int)number("LINE_SAMPLES"),height=(int)number("LINES"),dataOffset=checked(((int)number("^IMAGE")-1)*(int)number("RECORD_BYTES")),spacing=spacing,radius=number("A_AXIS_RADIUS")*1000,longitude=number("CENTER_LONGITUDE"),west=-(number("SAMPLE_PROJECTION_OFFSET")+.5)*spacing,north=(number("LINE_PROJECTION_OFFSET")+.5)*spacing,bytes=data};
            if(!ValidLayout(strip))throw new InvalidDataException("Invalid HRSC data bounds");return strip;
        }
    }
}

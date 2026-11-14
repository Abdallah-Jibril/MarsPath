using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.Networking;

namespace MarsPath
{
    /// <summary>NASA MRO / Murray Lab V01's CTX image pyramid, independent of
    /// terrain requests. Overview and local windows retain their own textures;
    /// preparing a future EVA station never replaces the image at the current one.</summary>
    public static class MarsCtxImageryCache
    {
        public const string OfficialSourceUrl="https://www.arcgis.com/home/item.html?id=30c4acfe36284b1784bf07d5d73f9e4d";
        public const string CapabilitiesUrl="https://astro.arcgis.com/arcgis/rest/services/OnMars/CTX1/MapServer?f=pjson";
        public const string TileUrl="https://astro.arcgis.com/arcgis/rest/services/OnMars/CTX1/MapServer/tile/";
        private const int MaximumLevel=12,TilePixels=512,ConcurrentDownloads=8;
        private const long AtlasBudget=224L*1024*1024,TileByteBudget=16L*1024*1024;
        public sealed class ImageWindow
        {
            public string key;public int level,column,row,columns,rows;public float spacingMeters,lastUse;public bool detail;
            public Texture2D texture;public Vector4 uvRect;
            public long Bytes=>(long)columns*rows*TilePixels*TilePixels*16/3;
        }
        private sealed class Tile
        {
            public string key,url;public byte[] bytes;public bool done,failed,queued,priority;public int attempt,consumers;public float lastUse;
            public UnityWebRequest request;public UnityWebRequestAsyncOperation operation;
        }
        [Serializable]private sealed class DiskWindow{public string key;public int level,column,row,columns,rows;public bool detail;public string dataset,source=TileUrl,officialSource=OfficialSourceUrl,serviceMetadata=CapabilitiesUrl,credit="NASA/JPL/MSSS/The Murray Lab";}
        private sealed class LimitedImageBuffer:DownloadHandlerScript
        {
            private readonly byte[] data=new byte[1536*1024];private int count;
            public LimitedImageBuffer():base(new byte[16384]){}
            protected override bool ReceiveData(byte[] bytes,int length){if(length>data.Length-count)return false;Buffer.BlockCopy(bytes,0,data,count,length);count+=length;return true;}
            protected override byte[] GetData(){var result=new byte[count];Buffer.BlockCopy(data,0,result,0,count);return result;}
        }
        private static readonly List<ImageWindow> windows=new List<ImageWindow>();
        private static readonly Dictionary<string,Tile> tiles=new Dictionary<string,Tile>();
        private static readonly LinkedList<Tile> pending=new LinkedList<Tile>();
        private static readonly List<Tile> running=new List<Tile>();
        private static readonly HashSet<string> assembling=new HashSet<string>();
        private static MarsCtxTilePump driver;
        public static ImageWindow Overview{get;private set;}
        public static ImageWindow Detail{get;private set;}
        public static ImageWindow View{get;private set;}
        public static int Revision{get;private set;}
        public static string LastError{get;private set;}="";
        public static int PendingTileCount=>running.Count+pending.Count;
        public static int CachedWindowCount=>windows.Count;
        public static long CachedAtlasBytes{get{long n=0;foreach(var w in windows)n+=w.Bytes;return n;}}
        public static string CacheDirectory=>Path.Combine(Application.persistentDataPath,"NASA-Trek-CTX","v2");
        public static ImageWindow DescribeWindow(GeoPoint center,float extentKm,bool detail=true)=>Describe(center,extentKm,detail);
        public static string[] TileKeysForWindow(GeoPoint center,float extentKm,bool detail=true)
        {ImageWindow w=Describe(center,extentKm,detail);var keys=new string[w.columns*w.rows];for(int r=0;r<w.rows;r++)for(int c=0;c<w.columns;c++)keys[r*w.columns+c]=w.level+"_"+(w.row+r)+"_"+Mod(w.column+c,2<<w.level);return keys;}

        public static IEnumerator PrepareWindow(GeoPoint center,float extentKm,float budgetSeconds,Func<bool> cancel=null,bool detail=true,Action<float,string> progress=null)
        {
            if(center==null)yield break;ImageWindow wanted=Describe(center,extentKm,detail);
            ImageWindow existing=Find(wanted.key);if(existing!=null){existing.lastUse=Time.unscaledTime;if(!detail)existing.detail=false;yield break;}
            float began=Time.realtimeSinceStartup;
            Func<bool> stopped=()=>cancel!=null&&cancel()||Time.realtimeSinceStartup-began>Mathf.Max(.1f,budgetSeconds);
            while(assembling.Contains(wanted.key)){if(stopped())yield break;yield return null;}
            existing=Find(wanted.key);if(existing!=null){if(!detail)existing.detail=false;yield break;}
            assembling.Add(wanted.key);
            var selected=new List<Tile>();
            try
            {
                if(TryRestore(wanted)){Remember(wanted);yield break;}
                EnsurePump();int totalColumns=2<<wanted.level;
                for(int r=0;r<wanted.rows;r++)for(int c=0;c<wanted.columns;c++)selected.Add(GetTile(wanted.level,wanted.row+r,Mod(wanted.column+c,totalColumns),detail));
                for(;;)
                {
                    if(stopped())yield break;int finished=0;foreach(Tile tile in selected)if(tile.done)finished++;
                    progress?.Invoke(finished/(float)selected.Count,"NASA MRO CTX V01 / "+finished+" of "+selected.Count+" tiles");
                    if(finished==selected.Count)break;yield return null;
                }
                foreach(Tile tile in selected)if(tile.failed||!MarsCtxImageIntegrity.IsCompleteImage(tile.bytes)){LastError="Incomplete CTX V01 tile "+tile.key+" / previous imagery retained";yield break;}
                int useful=0,decoded=0;var atlas=new Texture2D(wanted.columns*512,wanted.rows*512,TextureFormat.RGBA32,true,false){name="NASA CTX V01 / "+wanted.spacingMeters.ToString("0.#")+" m display",wrapMode=TextureWrapMode.Clamp,filterMode=FilterMode.Trilinear,anisoLevel=16};
                atlas.SetPixels32(new Color32[atlas.width*atlas.height]);
                for(int index=0;index<selected.Count;index++)
                {
                    if(stopped()){UnityEngine.Object.Destroy(atlas);yield break;}
                    Tile source=selected[index];if(source.failed||source.bytes==null)continue;
                    var image=new Texture2D(2,2,TextureFormat.RGBA32,false,false);
                    if(ImageConversion.LoadImage(image,source.bytes,false)&&image.width==512&&image.height==512)
                    {
                        decoded++;
                        Color32[] pixels=image.GetPixels32();int valid=0,min=255,max=0;
                        foreach(Color32 p in pixels)if(p.a>=16){min=Math.Min(min,p.r);max=Math.Max(max,p.r);}
                        bool empty=max-min<2&&(max<=5||min>=250);
                        // Preserve genuine black crater shadows and bright rim
                        // pixels. Brightness is not a per-pixel nodata mask.
                        for(int k=0;k<pixels.Length;k++){Color32 p=pixels[k];if(empty||p.a<16)pixels[k]=new Color32(0,0,0,0);else{p.a=255;pixels[k]=p;valid++;}}
                        if(valid>0){atlas.SetPixels32(index%wanted.columns*512,(wanted.rows-1-index/wanted.columns)*512,512,512,pixels);useful++;}
                    }
                    UnityEngine.Object.Destroy(image);source.lastUse=Time.unscaledTime;
                    if(index%2==1)yield return null;
                }
                if(decoded!=selected.Count||useful==0){UnityEngine.Object.Destroy(atlas);LastError=decoded!=selected.Count?"A CTX V01 tile could not be decoded; previous imagery retained":"No valid CTX image pixels in requested window";yield break;}
                atlas.Apply(true,false);wanted.texture=atlas;Remember(wanted);SaveWindow(wanted);TrimTiles();
                progress?.Invoke(1,"NASA MRO CTX V01 / "+wanted.spacingMeters.ToString("0.#")+" m display ready");
            }
            finally{assembling.Remove(wanted.key);foreach(Tile tile in selected)tile.consumers=Mathf.Max(0,tile.consumers-1);TrimTiles();}
        }

        public static bool ActivateAt(GeoPoint point,bool detail=true)
        {
            ImageWindow best=null;
            foreach(ImageWindow w in windows)if((detail||!w.detail)&&Contains(w,point,true)&&(best==null||w.spacingMeters<best.spacingMeters))best=w;
            if(best==null)return false;best.lastUse=Time.unscaledTime;
            if(detail){if(Detail!=best){Detail=best;Revision++;}}else if(Overview!=best){Overview=best;Revision++;}
            return true;
        }
        /// <summary>Select the sharpest image covering the requested visible
        /// footprint, rather than a tiny higher-resolution square at its centre.</summary>
        public static bool ActivateViewAt(GeoPoint point,float extentKm)
        {
            ImageWindow best=BestForFootprint(point,extentKm);
            if(best==null)return false;best.lastUse=Time.unscaledTime;if(View!=best){View=best;Revision++;}return true;
        }
        public static bool ActivateOverviewAt(GeoPoint point,float extentKm)
        {
            ImageWindow best=BestForFootprint(point,extentKm);
            if(best==null)return false;best.lastUse=Time.unscaledTime;if(Overview!=best){Overview=best;Revision++;}return true;
        }
        private static ImageWindow BestForFootprint(GeoPoint point,float extentKm)
        {
            ImageWindow best=null;float half=Mathf.Max(.1f,extentKm*.5f);
            foreach(ImageWindow w in windows)
            {
                if(best!=null&&w.spacingMeters>=best.spacingMeters||!Contains(w,point,true))continue;
                bool all=true;for(int y=-1;y<=1&&all;y++)for(int x=-1;x<=1;x++)
                {GeoPoint p=MarsGeospatial.Destination(point,Mathf.Atan2(x,y)*Mathf.Rad2Deg,Mathf.Sqrt(x*x+y*y)*half);if(!Contains(w,p,false)){all=false;break;}}
                if(all)best=w;
            }
            return best;
        }
        public static float SpacingAt(GeoPoint point)
        {float best=float.MaxValue;foreach(ImageWindow w in windows)if(w.spacingMeters<best&&Contains(w,point,true))best=w.spacingMeters;return best;}
        public static float DisplayedSpacingAt(GeoPoint point)
        {float best=float.MaxValue;foreach(ImageWindow w in new[]{Overview,View,Detail})if(w!=null&&w.spacingMeters<best&&Contains(w,point,true))best=w.spacingMeters;return best;}
        public static bool Covers(GeoPoint point,float maxSpacing=float.MaxValue){float spacing=SpacingAt(point);return spacing<=maxSpacing&&spacing<float.MaxValue;}
        public static string SourceAt(GeoPoint point)
        {float step=SpacingAt(point);return step<float.MaxValue?"NASA MRO CTX / Murray Lab V01 / 5 m source / "+step.ToString("0.#")+" m display / semi-controlled mosaic":"No cached CTX V01 image at this point";}
        public static void Bind(Material material)
        {
            if(material==null)return;BindWindow(material,"_CtxOverview",Overview);BindWindow(material,"_CtxView",View);BindWindow(material,"_CtxDetail",Detail);MarsMissionZone.BindOverview(material);
            MarsOfficialTerrain.BindMissionImageryContext(material);
        }
        private static void BindWindow(Material material,string prefix,ImageWindow window)
        {
            if(!material.HasProperty(prefix+"Tex"))return;
            material.SetTexture(prefix+"Tex",window?.texture);material.SetVector(prefix+"Rect",window?.uvRect??Vector4.zero);
            material.SetFloat(prefix+"Blend",window!=null&&window.texture!=null?1:0);material.SetFloat(prefix+"Spacing",window?.spacingMeters??0);
        }
        private static bool Contains(ImageWindow window,GeoPoint p,bool valid)
        {
            if(p==null||window.texture==null)return false;float u=p.longitude/360+.5f,v=p.latitude/180+.5f;u+=Mathf.Round(window.uvRect.x+window.uvRect.z*.5f-u);
            float x=(u-window.uvRect.x)/window.uvRect.z,y=(v-window.uvRect.y)/window.uvRect.w;
            if(x<.002f||x>.998f||y<.002f||y>.998f)return false;return !valid||window.texture.GetPixelBilinear(x,y).a>.1f;
        }
        private static ImageWindow Describe(GeoPoint point,float extentKm,bool detail)
        {
            double half=Math.Max(.2,extentKm)*.5,west=point.longitude,east=west,north=point.latitude,south=north;
            for(int y=-1;y<=1;y++)for(int x=-1;x<=1;x++)
            {GeoPoint p=MarsGeospatial.Destination(point,Mathf.Atan2(x,y)*Mathf.Rad2Deg,(float)(Math.Sqrt(x*x+y*y)*half));double lon=p.longitude;while(lon-point.longitude>180)lon-=360;while(lon-point.longitude< -180)lon+=360;west=Math.Min(west,lon);east=Math.Max(east,lon);north=Math.Max(north,p.latitude);south=Math.Min(south,p.latitude);}
            north=Math.Min(89.999,north);south=Math.Max(-89.999,south);int level=MaximumLevel,column,row,columns,rows,max=detail?3:8;double span;
            for(;;){span=180.0/(1<<level);column=(int)Math.Floor((west+180)/span);row=Math.Max(0,(int)Math.Floor((90-north)/span));columns=Math.Max(1,(int)Math.Ceiling((east+180)/span)-column);rows=Math.Max(1,Math.Min((1<<level)-row,(int)Math.Ceiling((90-south)/span)-row));if(columns<=max&&rows<=max||level==0)break;level--;}
            return new ImageWindow{level=level,column=column,row=row,columns=columns,rows=rows,detail=detail,key=$"ctx_{level}_{column}_{row}_{columns}_{rows}",spacingMeters=(float)(span/512*Math.PI/180*MarsRegionalTerrain.RadiusMeters),uvRect=new Vector4((float)(column*span/360),(float)(1-(row+rows)*span/180),(float)(columns*span/360),(float)(rows*span/180))};
        }
        private static ImageWindow Find(string key){foreach(ImageWindow w in windows)if(w.key==key&&w.texture!=null)return w;return null;}
        private static void Remember(ImageWindow image)
        {windows.Add(image);image.lastUse=Time.unscaledTime;Revision++;while(windows.Count>12||CachedAtlasBytes>AtlasBudget){ImageWindow oldest=null;foreach(ImageWindow w in windows)if(w!=Overview&&w!=View&&w!=Detail&&w!=image&&(oldest==null||w.lastUse<oldest.lastUse))oldest=w;if(oldest==null)break;windows.Remove(oldest);UnityEngine.Object.Destroy(oldest.texture);}}
        private static bool TryRestore(ImageWindow image)
        {
            if(!MarsTerrainCachePolicy.ReadPersistentCache)return false;
            try{string file=Path.Combine(CacheDirectory,image.key+".png"),metadata=Path.Combine(CacheDirectory,image.key+".json");if(!File.Exists(file)||!File.Exists(metadata)||JsonUtility.FromJson<DiskWindow>(File.ReadAllText(metadata)).dataset!="CTX1_V01")return false;byte[] png=File.ReadAllBytes(file);if(!MarsCtxImageIntegrity.IsCompleteImage(png))return false;var texture=new Texture2D(2,2,TextureFormat.RGBA32,true,false){name="Cached NASA CTX V01",wrapMode=TextureWrapMode.Clamp,filterMode=FilterMode.Trilinear,anisoLevel=16};if(!ImageConversion.LoadImage(texture,png,false)||texture.width!=image.columns*512||texture.height!=image.rows*512){UnityEngine.Object.Destroy(texture);return false;}image.texture=texture;return true;}catch(Exception e){LastError=e.Message;return false;}
        }
        private static void SaveWindow(ImageWindow image)
        {try{Directory.CreateDirectory(CacheDirectory);File.WriteAllBytes(Path.Combine(CacheDirectory,image.key+".png"),image.texture.EncodeToPNG());File.WriteAllText(Path.Combine(CacheDirectory,image.key+".json"),JsonUtility.ToJson(new DiskWindow{key=image.key,level=image.level,column=image.column,row=image.row,columns=image.columns,rows=image.rows,detail=image.detail,dataset="CTX1_V01"}));}catch(IOException e){LastError=e.Message;}}
        private static Tile GetTile(int level,int row,int column,bool priority)
        {
            string key=level+"_"+row+"_"+column;if(tiles.TryGetValue(key,out Tile cached)){if(cached.failed&&Time.unscaledTime-cached.lastUse>25){cached.done=false;cached.failed=false;cached.attempt=0;}cached.lastUse=Time.unscaledTime;cached.consumers++;if(priority&&cached.queued&&!cached.priority){pending.Remove(cached);Queue(cached,true);}else if(!cached.done&&cached.request==null&&!cached.queued)Queue(cached,priority);return cached;}
            var tile=new Tile{key=key,url=TileUrl+level+"/"+row+"/"+column,lastUse=Time.unscaledTime,consumers=1};tiles.Add(key,tile);
            // Optional build-time prewarm uses the exact same official tile
            // bytes and matrix keys. It is a cache, never a coordinate override.
            TextAsset bundled=Resources.Load<TextAsset>("NASA/CtxV01Tiles/"+key);
            if(bundled!=null){byte[] bytes=bundled.bytes;if(MarsCtxImageIntegrity.IsCompleteImage(bytes)){tile.bytes=bytes;tile.done=true;}Resources.UnloadAsset(bundled);}
            if(!tile.done&&MarsTerrainCachePolicy.ReadPersistentCache)try{string file=Path.Combine(CacheDirectory,"tiles",key+".png");if(File.Exists(file)){byte[] bytes=File.ReadAllBytes(file);if(MarsCtxImageIntegrity.IsCompleteImage(bytes)){tile.bytes=bytes;tile.done=true;}}}catch(IOException){}
            if(!tile.done)Queue(tile,priority);return tile;
        }
        private static void Queue(Tile tile,bool priority)
        {tile.queued=true;tile.priority=priority;if(priority){var after=pending.First;while(after!=null&&after.Value.priority)after=after.Next;if(after==null)pending.AddLast(tile);else pending.AddBefore(after,tile);}else pending.AddLast(tile);}
        private static void EnsurePump(){if(driver!=null)return;var go=new GameObject("NASA CTX V01 / shared bounded tile queue");UnityEngine.Object.DontDestroyOnLoad(go);driver=go.AddComponent<MarsCtxTilePump>();}
        internal static void Pump()
        {
            while(running.Count<ConcurrentDownloads&&pending.Count>0){Tile tile=pending.First.Value;pending.RemoveFirst();tile.queued=false;if(tile.consumers==0){tiles.Remove(tile.key);continue;}tile.attempt++;tile.request=new UnityWebRequest(tile.url,UnityWebRequest.kHttpVerbGET,new LimitedImageBuffer(),null){timeout=12};tile.operation=tile.request.SendWebRequest();running.Add(tile);}
            for(int i=running.Count-1;i>=0;i--)
            {
                Tile tile=running[i];if(tile.consumers==0){tile.request.Abort();tile.request.Dispose();tile.request=null;running.RemoveAt(i);tiles.Remove(tile.key);continue;}if(!tile.operation.isDone)continue;bool okay=tile.request.result==UnityWebRequest.Result.Success&&tile.request.responseCode==200;
                if(okay){tile.bytes=tile.request.downloadHandler.data;okay=MarsCtxImageIntegrity.IsCompleteImage(tile.bytes);if(!okay)tile.bytes=null;}
                if(!okay)LastError="NASA CTX V01 tile "+tile.key+": "+tile.request.responseCode;
                tile.request.Dispose();tile.request=null;running.RemoveAt(i);
                if(!okay&&tile.attempt<2)Queue(tile,tile.priority);else{tile.failed=!okay;tile.done=true;if(okay)try{Directory.CreateDirectory(Path.Combine(CacheDirectory,"tiles"));File.WriteAllBytes(Path.Combine(CacheDirectory,"tiles",tile.key+".png"),tile.bytes);}catch(IOException){}}
            }
        }
        private static void TrimTiles()
        {long bytes=0;foreach(Tile tile in tiles.Values)bytes+=tile.bytes?.Length??0;while(bytes>TileByteBudget){Tile oldest=null;foreach(Tile tile in tiles.Values)if(tile.done&&tile.consumers==0&&tile.bytes!=null&&(oldest==null||tile.lastUse<oldest.lastUse))oldest=tile;if(oldest==null)break;bytes-=oldest.bytes.Length;tiles.Remove(oldest.key);}while(tiles.Count>256){Tile forgotten=null;foreach(Tile tile in tiles.Values)if(tile.consumers==0&&tile.request==null&&!tile.queued&&tile.bytes==null&&(forgotten==null||tile.lastUse<forgotten.lastUse))forgotten=tile;if(forgotten==null)break;tiles.Remove(forgotten.key);}}
        internal static void DisposeRequests(){foreach(Tile tile in running)if(tile.request!=null){tile.request.Abort();tile.request.Dispose();}running.Clear();pending.Clear();}
        private static int Mod(int x,int m)=>(x%m+m)%m;
    }
    public sealed class MarsCtxTilePump:MonoBehaviour
    {private void Update(){MarsCtxImageryCache.Pump();}private void OnDestroy(){MarsCtxImageryCache.DisposeRequests();}}
}

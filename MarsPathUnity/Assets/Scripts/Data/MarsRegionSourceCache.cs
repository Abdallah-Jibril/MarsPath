using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using UnityEngine;
using UnityEngine.Networking;

namespace MarsPath
{
    /// <summary>Bounded on-demand scientific source loading, independent of the moving
    /// render frame. Real source pixels are cached on disk; failed requests retain
    /// the bundled terrain fallback. Never requests the complete 11 GB DEM.</summary>
    public static class MarsRegionSourceCache
    {
        public const string DemUrl="https://planetarymaps.usgs.gov/mosaic/Mars/HRSC_MOLA_Blend/Mars_HRSC_MOLA_BlendDEM_Global_200mp_v2.tif";
        private const string TileRoot="https://astro.arcgis.com/arcgis/rest/services/OnMars/";
        private const double SourceStep=0.003374120830641;
        private const int SourceWidth=106694,SourceHeight=53347,MaxDemDimension=2048;
        private const long MaxDemTransfer=48L*1024*1024;
        private const int MaxDemRangeBytes=128*1024,RangeDigestBytes=64;
        private const long MaxDemRangeCacheBytes=512L*1024*1024;
        private static long demRangeCacheBytes=-1;
        private static readonly List<DemCrop> memoryCrops=new List<DemCrop>();
        private static int generation;
        public static int ElevationRevision{get;private set;}
        public static Texture2D OrbitalTexture{get;private set;}
        /// <summary>Global equirectangular UV west,south,width,height. U can unwrap
        /// across the dateline; wrap a queried longitude around this rectangle.</summary>
        public static Vector4 OrbitalUvRect{get;private set;}
        public static bool OrbitalIsMonochrome{get;private set;}
        public static float ImageryDisplayMetersPerPixel{get;private set;}
        public static float ElevationDisplayMetersPerPixel{get;private set;}
        public static float ElevationSpacingMeters=>ElevationDisplayMetersPerPixel;
        public static float ImagerySpacingMeters=>ImageryDisplayMetersPerPixel;
        public static bool HasDynamicElevation=>memoryCrops.Count>0;
        public static bool HasDynamicImagery=>OrbitalTexture!=null;
        public static string ElevationStatus{get;private set;}="Bundled elevation sources";
        public static string ImageryStatus{get;private set;}="Bundled Viking context";
        public static string LastError{get;private set;}="";
        public static string CacheDirectory=>Path.Combine(Application.persistentDataPath,"RegionalSources","v3");
        public static int DemRangeCacheHits{get;private set;}
        public static int DemRangeCacheWrites{get;private set;}
        private static string DemRangeDirectory=>Path.Combine(CacheDirectory,"row-ranges");
        public static void Cancel(){generation++;}
        public static void SetOfficialImagery(Texture2D texture,Vector4 rect,float spacing,string status)
        {
            if(OrbitalTexture!=null&&OrbitalTexture!=texture)UnityEngine.Object.Destroy(OrbitalTexture);
            OrbitalTexture=texture;OrbitalUvRect=rect;OrbitalIsMonochrome=true;ImageryDisplayMetersPerPixel=spacing;ImageryStatus=status;
        }
        public static bool CoversImagery(GeoPoint point)
        {
            if(OrbitalTexture==null||point==null)return false;
            double u=Unwrap(point.longitude,(OrbitalUvRect.x+OrbitalUvRect.z*.5)*360-180)/360+.5;
            double v=point.latitude/180.0+.5;
            return u>=OrbitalUvRect.x&&u<=OrbitalUvRect.x+OrbitalUvRect.z&&v>=OrbitalUvRect.y&&v<=OrbitalUvRect.y+OrbitalUvRect.w;
        }

        [Serializable] private sealed class DemCrop
        {
            public string version="HRSC_MOLA_v2_200m",key,sha256;
            public int width,height,sourceX,sourceY,stride;
            public double west,north,step;
            [NonSerialized] public byte[] data;
        }
        [Serializable] private sealed class ImageWindow
        {
            public string version="CTX_beta01_uncontrolled",key,sha256;
            public int level,col,row,columns,rows;
            public bool monochrome=true;
        }
        private sealed class WindowRequest{public DemCrop dem;public ImageWindow image;public string demLimit;}
        private sealed class SourceDescriptor{public long offsets;public int bytes;}
        private sealed class Job
        {
            public string url;public long start=-1,end=-1;public int limit,attempt;
            public Action<byte[]> accept;public UnityWebRequest request;public UnityWebRequestAsyncOperation operation;
        }
        private sealed class BoundedBuffer:DownloadHandlerScript
        {
            private readonly byte[] bytes;private int used;
            public bool TooLarge{get;private set;}
            public int Count=>used;
            public BoundedBuffer(int limit):base(new byte[16384]){bytes=new byte[limit];}
            protected override bool ReceiveData(byte[] data,int count)
            {if(data==null||count==0)return true;if(count>bytes.Length-used){TooLarge=true;return false;}Buffer.BlockCopy(data,0,bytes,used,count);used+=count;return true;}
            protected override byte[] GetData(){byte[] result=new byte[used];Buffer.BlockCopy(bytes,0,result,0,used);return result;}
        }

        public static IEnumerator PrepareRegion(GeoPoint start,GeoPoint end,Action<float,string> progress,Func<bool> cancelled=null,bool includeElevation=true,bool includeImagery=true,float timeBudget=90)
        {
            int ticket=++generation;LastError="";float began=Time.realtimeSinceStartup;
            Func<bool> stop=()=>ticket!=generation||(cancelled!=null&&cancelled())||Time.realtimeSinceStartup-began>timeBudget;
            WindowRequest selection=null;
            try{selection=Describe(start,end);Directory.CreateDirectory(CacheDirectory);}
            catch(Exception e){LastError="Source cache: "+e.Message;}
            if(selection==null){progress?.Invoke(1,"Using bundled source coverage");yield break;}
            if(stop())yield break;
            // Camera-detail requests must never mutate the elevation provider
            // underneath an already-built terrain mesh.
            bool demReady=!includeElevation||LoadDem(selection.dem),imageReady=!includeImagery||LoadImage(selection.image);
            // Cached Viking fallback may be displayed during a retry, but it
            // must not permanently suppress a requested CTX download.
            if(!imageReady)LoadImage(CopyImageWindow(selection.image,false));
            if(demReady&&imageReady){progress?.Invoke(1,!includeImagery?"Cached elevation window ready":includeElevation?"Cached imagery and elevation ready":"Cached orbital imagery ready");yield break;}
            if(!demReady&&selection.dem!=null)
            {
                SourceDescriptor descriptor=null;bool headerOkay=false;
                var headerJobs=new List<Job>{new Job{url=DemUrl,start=0,end=65535,limit=65536,accept=b=>descriptor=ParseDescriptor(b)}};
                yield return Fetch(headerJobs,1,p=>progress?.Invoke(p*0.025f,"Reading official USGS elevation metadata"),stop,b=>headerOkay=b);
                if(stop())yield break;
                if(headerOkay&&descriptor!=null)
                {
                    DemCrop crop=selection.dem;int sourceRows=(crop.height-1)*crop.stride+1;
                    byte[] rowOffsets=new byte[sourceRows*descriptor.bytes];bool offsetsOkay=false;
                    long first=descriptor.offsets+(long)crop.sourceY*descriptor.bytes;
                    var jobs=new List<Job>();
                    for(int offset=0;offset<rowOffsets.Length;offset+=MaxDemRangeBytes)
                    {
                        int target=offset,count=Math.Min(MaxDemRangeBytes,rowOffsets.Length-offset);
                        jobs.Add(new Job{url=DemUrl,start=first+offset,end=first+offset+count-1,limit=count,accept=b=>{
                            for(int at=0;at<b.Length;at+=descriptor.bytes)ReadOffset(b,at,descriptor.bytes);
                            Buffer.BlockCopy(b,0,rowOffsets,target,b.Length);
                        }});
                    }
                    yield return Fetch(jobs,1,p=>progress?.Invoke(0.025f+p*0.025f,"Locating selected elevation pixels"),stop,b=>offsetsOkay=b);
                    if(stop())yield break;
                    if(offsetsOkay)
                    {
                        crop.data=new byte[crop.width*crop.height*2];jobs=new List<Job>();bool rowLayoutOkay=true;
                        int readWidth=(crop.width-1)*crop.stride+1;
                        try
                        {
                        for(int row=0;row<crop.height;row++)
                        {
                            int outputRow=row;long rowBase=ReadOffset(rowOffsets,row*crop.stride*descriptor.bytes,descriptor.bytes);
                            int sourceColumn=Mod(crop.sourceX,SourceWidth),consumed=0;
                            while(consumed<readWidth)
                            {
                                int firstColumn=sourceColumn,firstSample=consumed,count=Math.Min(MaxDemRangeBytes/2,Math.Min(SourceWidth-sourceColumn,readWidth-consumed));
                                long byteStart=rowBase+(long)firstColumn*2;
                                jobs.Add(new Job{url=DemUrl,start=byteStart,end=byteStart+count*2L-1,limit=count*2,accept=b=>{
                                    for(int x=0;x<crop.width;x++){int sourceSample=x*crop.stride-firstSample;if(sourceSample<0||sourceSample>=count)continue;
                                        int target=(outputRow*crop.width+x)*2;crop.data[target]=b[sourceSample*2];crop.data[target+1]=b[sourceSample*2+1];}
                                }});
                                consumed+=count;sourceColumn=(sourceColumn+count)%SourceWidth;
                            }
                        }
                        }
                        catch(Exception e){rowLayoutOkay=false;LastError="DEM row layout: "+e.Message;}
                        bool rowsOkay=false;
                        if(rowLayoutOkay)yield return Fetch(jobs,12,p=>progress?.Invoke(0.05f+p*0.68f,"Loading selected USGS elevations / "+Mathf.RoundToInt(p*100)+"%"),stop,b=>rowsOkay=b);
                        if(stop())yield break;
                        if(rowsOkay)
                        {
                            crop.sha256=Hash(crop.data);Remember(crop);demReady=true;
                            SaveDem(crop);ElevationStatus="USGS HRSC/MOLA / 200 m source / "+(200*crop.stride)+" m grid";
                        }
                    }
                }
            }
            if(includeElevation&&!demReady)ElevationStatus=selection.dem==null?selection.demLimit:"Bundled elevation / online crop unavailable";
            if(!imageReady)
            {
                bool loaded=false;
                yield return FetchImage(selection.image,p=>progress?.Invoke(0.73f+p*0.27f,"Loading NASA MRO CTX surface imagery / "+Mathf.RoundToInt(p*100)+"%"),stop,b=>loaded=b);
                if(stop())yield break;
                // A failed refresh must not replace a good, already visible CTX
                // centre with a coarser/different MDIM fallback.
                if(!loaded&&CurrentCtxCoversCenter(selection.image))loaded=true;
                if(!loaded)
                {
                    ImageWindow fallback=CopyImageWindow(selection.image,false);
                    if(!LoadImage(fallback))yield return FetchImage(fallback,p=>progress?.Invoke(0.73f+p*0.27f,"Loading Viking MDIM imagery fallback"),stop,b=>loaded=b);
                    else loaded=true;
                }
                imageReady=loaded;
            }
            if(stop())yield break;
            progress?.Invoke(1,!includeImagery?(demReady?"Selected measured elevation ready":"Using available elevation coverage"):includeElevation?(demReady?"Selected measured terrain ready":"Using available elevation coverage")+(imageReady?" / orbital imagery ready":" / bundled orbital context"):(imageReady?"Selected orbital imagery ready":"Using bundled orbital context"));
        }

        /// <summary>Only reads existing cache files, never starts networking. Useful
        /// for deterministic captures and offline startup.</summary>
        public static bool TryLoadCachedRegion(GeoPoint start,GeoPoint end)
        {
            if(!MarsTerrainCachePolicy.ReadPersistentCache)return false;
            try{WindowRequest r=Describe(start,end);bool dem=LoadDem(r.dem);bool image=LoadImage(r.image);if(!image)image=LoadImage(CopyImageWindow(r.image,false));if(!image)image=CurrentCtxCoversCenter(r.image);return dem||image;}
            catch(Exception e){LastError=e.Message;return false;}
        }
        public static bool TryLoadCachedImagery(GeoPoint start,GeoPoint end)
        {
            if(!MarsTerrainCachePolicy.ReadPersistentCache)return false;
            try{ImageWindow image=Describe(start,end).image;return LoadImage(image)||LoadImage(CopyImageWindow(image,false));}
            catch(Exception e){LastError=e.Message;return false;}
        }
        public static bool TryLoadCachedElevation(GeoPoint start,GeoPoint end)
        {if(!MarsTerrainCachePolicy.ReadPersistentCache)return false;try{return LoadDem(Describe(start,end).dem);}catch(Exception){return false;}}
        public static bool TryElevation(GeoPoint point,out float height)
        {
            height=0;int best=int.MaxValue;DemCrop chosen=null;
            for(int i=memoryCrops.Count-1;i>=0;i--)
            {
                DemCrop crop=memoryCrops[i];
                if(crop.stride<best&&Sample(crop,point,out float candidate)){height=candidate;best=crop.stride;chosen=crop;}
            }
            if(chosen!=null)
            {
                double longitude=Unwrap(point.longitude,chosen.west+chosen.width*chosen.step*.5);
                double col=(longitude-chosen.west)/chosen.step-.5,row=(chosen.north-point.latitude)/chosen.step-.5;
                float edge=(float)Math.Min(Math.Min(col,chosen.width-1-col),Math.Min(row,chosen.height-1-row));
                if(edge<4)
                {
                    float fallback=MarsRegionalTerrain.MolaElevation(point.latitude,point.longitude);int next=int.MaxValue;
                    foreach(DemCrop crop in memoryCrops)if(crop.stride>best&&crop.stride<next&&Sample(crop,point,out float value)){fallback=value;next=crop.stride;}
                    height=Mathf.Lerp(fallback,height,Mathf.SmoothStep(0,1,Mathf.Clamp01(edge/4)));
                }
            }
            return best<int.MaxValue;
        }
        public static float GridSpacingAt(GeoPoint point)
        {
            int best=int.MaxValue;for(int i=memoryCrops.Count-1;i>=0;i--)if(memoryCrops[i].stride<best&&Sample(memoryCrops[i],point,out _))best=memoryCrops[i].stride;
            return best<int.MaxValue?200*best:0;
        }
        private static bool Sample(DemCrop c,GeoPoint point,out float height)
        {
            height=0;double longitude=Unwrap(point.longitude,c.west+c.width*c.step*0.5);
            double col=(longitude-c.west)/c.step-0.5,row=(c.north-point.latitude)/c.step-0.5;
            int x=(int)Math.Floor(col),y=(int)Math.Floor(row);if(x<0||y<0||x>=c.width-1||y>=c.height-1)return false;
            short a=Value(c,x,y),b=Value(c,x+1,y),d=Value(c,x,y+1),e=Value(c,x+1,y+1);
            if(a==-32768||b==-32768||d==-32768||e==-32768)return false;
            height=Mathf.Lerp(Mathf.Lerp(a,b,(float)(col-x)),Mathf.Lerp(d,e,(float)(col-x)),(float)(row-y));return true;
        }
        private static short Value(DemCrop c,int x,int y){int k=(y*c.width+x)*2;return(short)(c.data[k]|c.data[k+1]<<8);}

        private static WindowRequest Describe(GeoPoint start,GeoPoint end)
        {
            if(start==null||end==null)throw new ArgumentException("Both geographic endpoints are required");
            GeoPoint center=MarsRegionalTerrain.Midpoint(start,end);
            double distance=MarsGeospatial.AngularDistanceDegrees(start,end)*Math.PI/180*MarsRegionalTerrain.RadiusMeters/1000;
            double span=Math.Max(6,distance*1.72),west=center.longitude,east=west,north=center.latitude,south=north;
            // Sample the actual projected square, including edge midpoints, not
            // merely the route endpoints. This covers the full 1.25*span mesh.
            for(int z=-1;z<=1;z++)for(int x=-1;x<=1;x++)
            {
                float dx=(float)(x*span*.625),dz=(float)(z*span*.625);
                GeoPoint p=MarsGeospatial.Destination(center,Mathf.Atan2(dx,dz)*Mathf.Rad2Deg,Mathf.Sqrt(dx*dx+dz*dz));
                double lon=Unwrap(p.longitude,center.longitude);west=Math.Min(west,lon);east=Math.Max(east,lon);north=Math.Max(north,p.latitude);south=Math.Min(south,p.latitude);
            }
            double latPad=Math.Max(SourceStep*3,(north-south)*.035),lonPad=Math.Max(SourceStep*3,(east-west)*.035);
            west-=lonPad;east+=lonPad;north=Math.Min(89.98,north+latPad);south=Math.Max(-89.98,south-latPad);
            if(east-west>359){west=-180;east=180;}if(north-south>179){north=90;south=-90;}
            int sx=(int)Math.Floor((west+180)/SourceStep),sy=Math.Max(0,(int)Math.Floor((90-north)/SourceStep));
            int sw=Math.Min(SourceWidth,(int)Math.Ceiling((east-west)/SourceStep)+2),sh=Math.Min(SourceHeight-sy,(int)Math.Ceiling((north-south)/SourceStep)+2);
            int stride=Math.Max(sw,sh)<=MaxDemDimension?1:(int)Math.Ceiling(Math.Max(sw,sh)/1024.0);
            int w=Math.Max(2,(sw-1)/stride+1),h=Math.Max(2,(sh-1)/stride+1);
            DemCrop crop=new DemCrop{sourceX=sx,sourceY=sy,width=w,height=h,stride=stride,step=SourceStep*stride,
                west=-180+(sx+.5)*SourceStep-SourceStep*stride*.5,north=90-(sy+.5)*SourceStep+SourceStep*stride*.5};
            crop.key=$"dem_{sx}_{sy}_{w}_{h}_{stride}";
            long transfer=(long)((w-1)*stride+1)*h*2;
            string limit="Global MOLA fallback / requested region exceeds bounded online DEM transfer";
            // Wide maps get explicitly labelled overview grids from the same
            // 200 m product. Never replace the finer global MOLA fallback with
            // a coarser overview; ground requests retain the native 200 m grid.
            if(stride>16||sw>=SourceWidth-2||transfer>MaxDemTransfer||span>7000)crop=null;
            int level=12,columns,rows,col,row;double tileSpan;
            for(;;)
            {
                tileSpan=180.0/(1<<level);col=(int)Math.Floor((west+180)/tileSpan);row=Math.Max(0,(int)Math.Floor((90-north)/tileSpan));
                columns=Math.Max(1,(int)Math.Ceiling((east+180)/tileSpan)-col);rows=Math.Max(1,(int)Math.Ceiling((90-south)/tileSpan)-row);
                rows=Math.Min(rows,(1<<level)-row);
                if((columns<=8&&rows<=8)||level==0)break;level--;
            }
            ImageWindow image=new ImageWindow{level=level,col=col,row=row,columns=columns,rows=rows,monochrome=level>=4,version=level>=4?"CTX_beta01_uncontrolled":"MDIM21_color"};image.key=ImageKey(image);
            return new WindowRequest{dem=crop,image=image,demLimit=limit};
        }
        private static ImageWindow CopyImageWindow(ImageWindow source,bool monochrome)
        {var r=new ImageWindow{level=source.level,col=source.col,row=source.row,columns=source.columns,rows=source.rows,monochrome=monochrome,version=monochrome?"CTX_beta01_uncontrolled":"MDIM21_color"};r.key=ImageKey(r);return r;}
        private static string ImageKey(ImageWindow w)=>(w.monochrome?"ctx_":"mdim_")+$"{w.level}_{w.col}_{w.row}_{w.columns}_{w.rows}";
        private static Vector4 ImageUvRect(ImageWindow window)
        {
            double span=180.0/(1<<window.level);
            return new Vector4((float)(window.col*span/360),(float)(1-(window.row+window.rows)*span/180),(float)(window.columns*span/360),(float)(window.rows*span/180));
        }
        public static bool CoversImageryWindowCenter(Vector4 requestedUvRect)
        {
            GeoPoint center=new GeoPoint{longitude=MarsGeospatial.NormalizeLongitude((requestedUvRect.x+requestedUvRect.z*.5f)*360-180),latitude=(requestedUvRect.y+requestedUvRect.w*.5f-.5f)*180};
            return CoversImagery(center);
        }
        private static bool CurrentCtxCoversCenter(ImageWindow window)=>OrbitalIsMonochrome&&CoversImageryWindowCenter(ImageUvRect(window));
        private static int Mod(int n,int m)=>(n%m+m)%m;
        private static double Unwrap(double longitude,double around){while(longitude-around>180)longitude-=360;while(longitude-around< -180)longitude+=360;return longitude;}

        private static IEnumerator FetchImage(ImageWindow window,Action<float> progress,Func<bool> stop,Action<bool> completed)
        {
            int width=window.columns*512,height=window.rows*512,valid=0;
            Texture2D texture=new Texture2D(width,height,TextureFormat.RGBA32,true,false){name=window.monochrome?"NASA MRO CTX / selected region":"NASA Viking MDIM / selected region",wrapMode=TextureWrapMode.Clamp,filterMode=FilterMode.Trilinear,anisoLevel=8};
            texture.SetPixels32(new Color32[width*height]);
            List<Job> jobs=new List<Job>();int totalColumns=2<<window.level;
            for(int r=0;r<window.rows;r++)for(int c=0;c<window.columns;c++)
            {
                int targetX=c*512,targetY=(window.rows-1-r)*512;
                string url=TileRoot+(window.monochrome?"CTX":"MDIM")+"/MapServer/tile/"+window.level+"/"+(window.row+r)+"/"+Mod(window.col+c,totalColumns);
                jobs.Add(new Job{url=url,limit=1536*1024,accept=b=>{
                    Texture2D tile=new Texture2D(2,2,TextureFormat.RGBA32,false,false);
                    try{if(!ImageConversion.LoadImage(tile,b,false)||tile.width!=512||tile.height!=512)throw new InvalidDataException("Unexpected orbital tile dimensions");
                        Color32[] pixels=tile.GetPixels32();int useful=PrepareValidity(pixels,window.monochrome);
                        if(useful>0){texture.SetPixels32(targetX,targetY,512,512,pixels);valid++;}}
                    finally{UnityEngine.Object.Destroy(tile);}
                }});
            }
            int failedTiles=0;bool okay=false;yield return Fetch(jobs,8,progress,stop,b=>okay=b,allowPartial:true,reportFailures:n=>failedTiles=n);
            // Refreshes are atomic: no partially downloaded HTTP window becomes
            // visible. Actual source nodata remains transparent, with RGB zero
            // so interpolation/mips are premultiplied by valid coverage.
            if(stop()||!okay||valid==0){UnityEngine.Object.Destroy(texture);completed(false);yield break;}
            texture.Apply(true,false);SetOrbital(texture,window);
            try{if(failedTiles==0){byte[] encoded=ImageConversion.EncodeToPNG(texture);window.sha256=Hash(encoded);WriteAtomic(Path.Combine(CacheDirectory,window.key+".png"),encoded);WriteAtomic(Path.Combine(CacheDirectory,window.key+".json"),System.Text.Encoding.UTF8.GetBytes(JsonUtility.ToJson(window)));}}
            catch(Exception e){Debug.LogWarning("MarsPath source image cache could not be saved: "+e.Message);}
            completed(true);
        }
        private static int PrepareValidity(Color32[] pixels,bool monochrome)
        {
            int useful=0;
            for(int i=0;i<pixels.Length;i++)
            {
                Color32 p=pixels[i];
                if(monochrome&&(p.r<=5||p.r>=250)){pixels[i]=new Color32(0,0,0,0);continue;}
                p.a=255;pixels[i]=p;useful++;
            }
            return useful;
        }
        private static void SetOrbital(Texture2D texture,ImageWindow window)
        {
            if(OrbitalTexture!=null&&OrbitalTexture!=texture)UnityEngine.Object.Destroy(OrbitalTexture);
            OrbitalTexture=texture;OrbitalIsMonochrome=window.monochrome;
            double span=180.0/(1<<window.level);
            OrbitalUvRect=ImageUvRect(window);
            ImageryDisplayMetersPerPixel=(float)(span/512*Math.PI/180*MarsRegionalTerrain.RadiusMeters);
            ImageryStatus=window.monochrome?"NASA MRO CTX / 5 m source / "+ImageryDisplayMetersPerPixel.ToString("0")+" m display / uncontrolled mosaic":"NASA Viking MDIM / 231 m source / "+ImageryDisplayMetersPerPixel.ToString("0")+" m display";
        }

        private static IEnumerator Fetch(List<Job> jobs,int parallel,Action<float> progress,Func<bool> stop,Action<bool> completed,bool allowPartial=false,Action<int> reportFailures=null)
        {
            List<Job> active=new List<Job>();Queue<Job> pending=new Queue<Job>(jobs);int done=0,failed=0;float started=Time.realtimeSinceStartup;
            try
            {
                while(done<jobs.Count)
                {
                    if(stop()||Time.realtimeSinceStartup-started>120){LastError="Source request cancelled or exceeded its time budget";completed(false);yield break;}
                    while(active.Count<parallel&&pending.Count>0)
                    {
                        Job j=pending.Dequeue();
                        if(TryReadDemRange(j,out byte[] cached))
                        {
                            bool accepted=true;
                            try{j.accept?.Invoke(cached);}catch(Exception){accepted=false;DeleteDemRange(j);}
                            if(accepted){done++;DemRangeCacheHits++;continue;}
                        }
                        j.attempt++;
                        j.request=new UnityWebRequest(j.url,UnityWebRequest.kHttpVerbGET,new BoundedBuffer(j.limit),null){timeout=20};
                        if(j.start>=0)j.request.SetRequestHeader("Range","bytes="+j.start+"-"+j.end);
                        j.operation=j.request.SendWebRequest();active.Add(j);
                    }
                    for(int i=active.Count-1;i>=0;i--)
                    {
                        Job j=active[i];if(!j.operation.isDone)continue;
                        bool good=j.request.result==UnityWebRequest.Result.Success&&j.request.responseCode==(j.start>=0?206:200);
                        if(good&&j.start>=0){string contentRange=j.request.GetResponseHeader("Content-Range");good=contentRange!=null&&contentRange.StartsWith("bytes "+j.start+"-"+j.end+"/",StringComparison.OrdinalIgnoreCase);}
                        byte[] bytes=null;
                        if(good){bytes=j.request.downloadHandler.data;good=bytes!=null&&(j.start<0||bytes.Length==j.limit);}
                        if(good)try{j.accept?.Invoke(bytes);}catch(Exception e){good=false;LastError=e.Message;}
                        if(good)SaveDemRange(j,bytes);
                        if(!good)LastError="Source fetch: "+j.request.responseCode+" / "+j.request.error;
                        j.request.Dispose();j.request=null;active.RemoveAt(i);
                        if(!good&&j.attempt<3)pending.Enqueue(j);else{done++;if(!good)failed++;}
                    }
                    progress?.Invoke(jobs.Count==0?1:done/(float)jobs.Count);
                    if(failed>0&&!allowPartial){completed(false);yield break;}
                    if(done<jobs.Count)yield return null;
                }
                reportFailures?.Invoke(failed);completed(allowPartial?failed<jobs.Count:failed==0);
            }
            finally
            {
                foreach(Job j in active)if(j.request!=null){j.request.Abort();j.request.Dispose();j.request=null;}
                PruneDemRanges();
            }
        }

        // These records are independent of a requested crop. A cancelled request
        // keeps every completed header, offset block and row range for its next
        // attempt, including requests with a different crop key.
        private static bool IsCacheableDemRange(Job job)=>job.url==DemUrl&&job.start>=0&&job.end>=job.start&&
            job.end-job.start+1==job.limit&&job.limit>0&&job.limit<=MaxDemRangeBytes;
        private static string DemRangeFile(Job job)
        {
            string identity="HRSC_MOLA_v2_200m_range_v1\n"+job.url+"\n"+job.start+"-"+job.end;
            return Path.Combine(DemRangeDirectory,Hash(System.Text.Encoding.UTF8.GetBytes(identity))+".range");
        }
        private static bool TryReadDemRange(Job job,out byte[] bytes)
        {
            bytes=null;if(!MarsTerrainCachePolicy.ReadPersistentCache||!IsCacheableDemRange(job))return false;
            string file=DemRangeFile(job);
            try
            {
                if(!File.Exists(file))return false;
                if(new FileInfo(file).Length!=job.limit+RangeDigestBytes){DeleteDemRange(job);return false;}
                byte[] record=File.ReadAllBytes(file),payload=new byte[job.limit];
                Buffer.BlockCopy(record,RangeDigestBytes,payload,0,payload.Length);
                if(System.Text.Encoding.ASCII.GetString(record,0,RangeDigestBytes)!=Hash(payload)){DeleteDemRange(job);return false;}
                bytes=payload;
                // Explicit touching works even where automatic access-time
                // updates are disabled. No directory scan is needed per row.
                try{File.SetLastWriteTimeUtc(file,DateTime.UtcNow);}catch(Exception){}
                return true;
            }
            catch(Exception){return false;}
        }
        private static void SaveDemRange(Job job,byte[] bytes)
        {
            if(!IsCacheableDemRange(job)||bytes==null||bytes.Length!=job.limit)return;
            try
            {
                Directory.CreateDirectory(DemRangeDirectory);
                if(demRangeCacheBytes<0)
                {
                    long total=0;
                    foreach(FileInfo file in new DirectoryInfo(DemRangeDirectory).GetFiles("*.range*"))total+=file.Length;
                    demRangeCacheBytes=total;
                }
                string path=DemRangeFile(job);long previous=File.Exists(path)?new FileInfo(path).Length:0;
                if(File.Exists(path+".partial"))previous+=new FileInfo(path+".partial").Length;
                byte[] record=new byte[RangeDigestBytes+bytes.Length];
                Buffer.BlockCopy(System.Text.Encoding.ASCII.GetBytes(Hash(bytes)),0,record,0,RangeDigestBytes);
                Buffer.BlockCopy(bytes,0,record,RangeDigestBytes,bytes.Length);
                WriteAtomic(path,record);demRangeCacheBytes+=record.Length-previous;DemRangeCacheWrites++;
            }
            catch(Exception e){demRangeCacheBytes=-1;Debug.LogWarning("MarsPath DEM range cache could not be saved: "+e.Message);}
        }
        private static void DeleteDemRange(Job job)
        {
            try
            {
                string path=DemRangeFile(job);if(!File.Exists(path))return;
                long length=new FileInfo(path).Length;File.Delete(path);
                if(demRangeCacheBytes>=0)demRangeCacheBytes=Math.Max(0,demRangeCacheBytes-length);
            }
            catch(Exception){}
        }
        private static void PruneDemRanges()
        {
            if(demRangeCacheBytes>=0&&demRangeCacheBytes<=MaxDemRangeCacheBytes)return;
            try
            {
                if(!Directory.Exists(DemRangeDirectory)){demRangeCacheBytes=0;return;}
                FileInfo[] files=new DirectoryInfo(DemRangeDirectory).GetFiles("*.range*");
                demRangeCacheBytes=0;foreach(FileInfo file in files)demRangeCacheBytes+=file.Length;
                if(demRangeCacheBytes<=MaxDemRangeCacheBytes)return;
                Array.Sort(files,(a,b)=>a.LastWriteTimeUtc.CompareTo(b.LastWriteTimeUtc));
                foreach(FileInfo file in files)
                {
                    if(demRangeCacheBytes<=MaxDemRangeCacheBytes)break;
                    long length=file.Length;
                    try{file.Delete();demRangeCacheBytes-=length;}catch(IOException){}catch(UnauthorizedAccessException){}
                }
            }
            catch(Exception e){Debug.LogWarning("MarsPath DEM range cache could not be trimmed: "+e.Message);}
        }

        private static SourceDescriptor ParseDescriptor(byte[] bytes)
        {
            if(bytes==null||bytes.Length<32||bytes[0]!='I'||bytes[1]!='I'||BitConverter.ToUInt16(bytes,2)!=43||BitConverter.ToUInt16(bytes,4)!=8)throw new InvalidDataException("Unsupported scientific DEM header");
            long ifd=(long)BitConverter.ToUInt64(bytes,8);if(ifd<16||ifd>bytes.Length-8)throw new InvalidDataException("DEM metadata offset outside header");
            int count=(int)BitConverter.ToUInt64(bytes,(int)ifd);if(count<1||count>128||ifd+8+count*20L>bytes.Length)throw new InvalidDataException("Invalid DEM metadata table");
            Dictionary<int,long[]> tags=new Dictionary<int,long[]>();
            for(int i=0;i<count;i++){int k=(int)ifd+8+i*20;tags[BitConverter.ToUInt16(bytes,k)]=new[]{(long)BitConverter.ToUInt16(bytes,k+2),(long)BitConverter.ToUInt64(bytes,k+4),(long)BitConverter.ToUInt64(bytes,k+12)};}
            if(tags[256][2]!=SourceWidth||tags[257][2]!=SourceHeight||tags[258][2]!=16||tags[259][2]!=1||tags[278][2]!=1||tags[277][2]!=1||tags[339][2]!=2)throw new InvalidDataException("Elevation raster no longer matches the verified source layout");
            long[] strips=tags[273];if(strips[1]!=SourceHeight||(strips[0]!=16&&strips[0]!=4)||strips[2]<=65535)throw new InvalidDataException("Unsupported elevation strip layout");
            return new SourceDescriptor{offsets=strips[2],bytes=strips[0]==16?8:4};
        }
        private static long ReadOffset(byte[] bytes,int at,int size)
        {long value=size==8?(long)BitConverter.ToUInt64(bytes,at):BitConverter.ToUInt32(bytes,at);if(value<65536||value>12000000000L)throw new InvalidDataException("Invalid source row offset");return value;}
        private static string Hash(byte[] data){using(SHA256 sha=SHA256.Create())return BitConverter.ToString(sha.ComputeHash(data)).Replace("-","").ToLowerInvariant();}
        private static void WriteAtomic(string path,byte[] bytes)
        {string temp=path+".partial";File.WriteAllBytes(temp,bytes);if(File.Exists(path))File.Delete(path);File.Move(temp,path);}
        private static void Remember(DemCrop crop)
        {
            ElevationRevision++;
            memoryCrops.RemoveAll(c=>c.key==crop.key);memoryCrops.Add(crop);while(memoryCrops.Count>12)memoryCrops.RemoveAt(0);
            ElevationDisplayMetersPerPixel=200*crop.stride;
        }
        private static void SaveDem(DemCrop crop)
        {try{WriteAtomic(Path.Combine(CacheDirectory,crop.key+".bytes"),crop.data);WriteAtomic(Path.Combine(CacheDirectory,crop.key+".json"),System.Text.Encoding.UTF8.GetBytes(JsonUtility.ToJson(crop)));}catch(Exception e){Debug.LogWarning("MarsPath elevation cache could not be saved: "+e.Message);}}
        private static bool LoadDem(DemCrop expected)
        {
            if(expected==null)return false;
            try{string file=Path.Combine(CacheDirectory,expected.key);if(!File.Exists(file+".json")||!File.Exists(file+".bytes"))return false;
                DemCrop crop=JsonUtility.FromJson<DemCrop>(File.ReadAllText(file+".json"));byte[] data=File.ReadAllBytes(file+".bytes");
                if(crop.version!="HRSC_MOLA_v2_200m"||crop.key!=expected.key||crop.width!=expected.width||crop.height!=expected.height||crop.sourceX!=expected.sourceX||crop.sourceY!=expected.sourceY||crop.stride!=expected.stride||Math.Abs(crop.west-expected.west)>1e-8||Math.Abs(crop.north-expected.north)>1e-8||Math.Abs(crop.step-expected.step)>1e-12||data.Length!=crop.width*crop.height*2||Hash(data)!=crop.sha256)return false;
                crop.data=data;Remember(crop);ElevationStatus="Cached USGS HRSC/MOLA / 200 m source / "+(200*crop.stride)+" m grid";return true;}
            catch(Exception){return false;}
        }
        private static bool LoadImage(ImageWindow expected)
        {
            if(!expected.monochrome&&CurrentCtxCoversCenter(expected))return false;
            try{string file=Path.Combine(CacheDirectory,expected.key),imageFile=File.Exists(file+".png")?file+".png":file+".jpg";if(!File.Exists(file+".json")||!File.Exists(imageFile))return false;
                ImageWindow window=JsonUtility.FromJson<ImageWindow>(File.ReadAllText(file+".json"));byte[] data=File.ReadAllBytes(imageFile);
                if(window.key!=expected.key||window.columns!=expected.columns||window.rows!=expected.rows||window.level!=expected.level||window.col!=expected.col||window.row!=expected.row||window.version!=expected.version||window.monochrome!=expected.monochrome||Hash(data)!=window.sha256)return false;
                Texture2D texture=new Texture2D(2,2,TextureFormat.RGBA32,true,false){wrapMode=TextureWrapMode.Clamp,filterMode=FilterMode.Trilinear,anisoLevel=8};
                if(!ImageConversion.LoadImage(texture,data,false)||texture.width!=window.columns*512||texture.height!=window.rows*512){UnityEngine.Object.Destroy(texture);return false;}
                Color32[] pixels=texture.GetPixels32();if(PrepareValidity(pixels,window.monochrome)==0){UnityEngine.Object.Destroy(texture);return false;}texture.SetPixels32(pixels);
                texture.Apply(true,false);SetOrbital(texture,window);return true;}
            catch(Exception){return false;}
        }
    }
}

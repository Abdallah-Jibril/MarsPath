using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

namespace MarsPath
{
    public static partial class MarsOfficialTerrain
    {
        [Serializable]private sealed class ImageRecord{public string key,source,kind,resource;public Vector4 rect;public float spacing;public bool nativeResolution;public double west,south,east,north;[NonSerialized]public bool missionZone;}
        private static readonly List<ImageRecord> images=new List<ImageRecord>();
        private static readonly HashSet<string> imageRequests=new HashSet<string>();
        private static string activeImageKey;
        private static Texture2D activeImage;
        private static ImageRecord activeImageRecord;
        private static Texture2D missionContextImage;
        private static ImageRecord missionContextRecord;
        public static int ImageryRevision{get;private set;}
        public static bool TryActiveMatchingImage(GeoPoint point,out string kind,out float spacing)
        {
            kind=null;spacing=0;
            if(point==null||activeImageRecord==null||activeImage==null||MarsRegionSourceCache.OrbitalTexture!=activeImage)return false;
            ImageRecord record=activeImageRecord;
            if(!ImageContains(record,point))
            {
                if(!MarsMissionZone.ElevationsActive||missionContextImage==null||!ImageContains(missionContextRecord,point))return false;
                record=missionContextRecord;
            }
            kind=record.kind;spacing=record.spacing;return true;
        }
        private static ImageRecord BestImage(GeoPoint focus)
        {
            // A zone uses its own co-registered mosaic consistently, rather
            // than a finer-looking photograph from another height solution.
            if(MarsMissionZone.ElevationsActive&&MarsMissionZone.BoundsContain(focus))
            {
                ImageRecord zone=BestMissionImage(focus,false);
                if(zone!=null)return zone;
            }
            return images.Where(i=>!i.missionZone&&i.spacing>0&&ImageContains(i,focus)).OrderBy(i=>i.spacing).FirstOrDefault();
        }
        private static ImageRecord BestMissionImage(GeoPoint point,bool contextOnly)
        {
            ImageRecord best=null;
            double bestMargin=0;
            foreach(ImageRecord record in images)
            {
                if(!record.missionZone||record.spacing<=0||(contextOnly&&record.spacing<.75f)||!ImageContains(record,point))continue;
                ImageBounds(record,out double west,out double south,out double east,out double north);
                double margin=Math.Min(Math.Min((point.longitude-west)/(east-west),(east-point.longitude)/(east-west)),
                    Math.Min((point.latitude-south)/(north-south),(north-point.latitude)/(north-south)));
                // Equal-resolution neighbours hand off inside the overlap,
                // where both photographs still have opaque native pixels.
                if(best==null||record.spacing<best.spacing||record.spacing==best.spacing&&margin>bestMargin)
                {best=record;bestMargin=margin;}
            }
            return best;
        }
        private static bool ImageContains(ImageRecord image,GeoPoint point)
        {
            ImageBounds(image,out double west,out double south,out double east,out double north);
            double longitude=point.longitude+360*Math.Round(((west+east)*.5-point.longitude)/360);
            double width=east-west,height=north-south;
            if(image.missionZone&&MarsMissionZone.BoundsContain(point))
            {double margin=image.spacing*2/MarsRegionalTerrain.RadiusMeters*180/Math.PI;return longitude>west+margin&&longitude<east-margin&&point.latitude>south+margin&&point.latitude<north-margin;}
            return longitude>west+width*.08&&longitude<west+width*.92&&point.latitude>south+height*.08&&point.latitude<north-height*.08;
        }
        private static bool ImageBounds(ImageRecord image,out double west,out double south,out double east,out double north)
        {
            west=image.west;south=image.south;east=image.east;north=image.north;
            if(east>west&&north>south)return true;
            // Older, coarser images keep their legacy rectangle. Native images
            // carry double degree bounds all the way to the local shader phase.
            west=((double)image.rect.x-.5)*360;south=((double)image.rect.y-.5)*180;
            east=west+(double)image.rect.z*360;north=south+(double)image.rect.w*180;
            return false;
        }
        public static bool BindPreciseImagery(Material material,GeoPoint origin,double metresPerUnit)
        {
            if(material==null||!material.HasProperty("_OrbitalPrecise"))return false;
            material.SetFloat("_OrbitalPrecise",0);
            if(origin==null||activeImage==null||MarsRegionSourceCache.OrbitalTexture!=activeImage||activeImageRecord==null||
                !ImageBounds(activeImageRecord,out double west,out double south,out double east,out double north))return false;
            double longitude=origin.longitude+360*Math.Round(((west+east)*.5-origin.longitude)/360);
            double latitude=origin.latitude*Math.PI/180;
            // Subtract the double geographic origin before converting to float.
            // A global float U near .715 has metre-sized steps on Mars.
            material.SetVector("_OrbitalUvAnchor",new Vector4((float)((longitude-west)/(east-west)),(float)((origin.latitude-south)/(north-south)),(float)(180/Math.PI/(east-west)),(float)(180/Math.PI/(north-south))));
            material.SetVector("_OrbitalGeoFrame",new Vector4((float)Math.Sin(latitude),(float)Math.Cos(latitude),(float)(metresPerUnit/MarsRegionalTerrain.RadiusMeters),0));
            material.SetFloat("_OrbitalPrecise",1);
            return true;
        }
        public static bool ActivateCachedImagery(GeoPoint focus)
        {
            EnsureBundled();LoadLocalWindowIndex();
            return ActivateImagery(focus, BestImage(focus));
        }
        internal static void WarmMissionZoneImagery(GeoPoint focus)
        {
            EnsureMissionZone();
            ActivateImagery(focus, BestMissionImage(focus, false));
        }
        private static bool ActivateImagery(GeoPoint focus, ImageRecord best)
        {
            if(best==null)return false;
            if(activeImageKey!=best.key||activeImage==null||MarsRegionSourceCache.OrbitalTexture!=activeImage)
            {
                Texture2D texture=LoadImageTexture(best);
                if(texture==null)return false;
                BindImage(best,texture);
            }
            ImageRecord context=best.missionZone&&best.spacing<.75f?BestMissionImage(focus,true):null;
            if(context!=missionContextRecord)
            {
                if(missionContextImage!=null)UnityEngine.Object.Destroy(missionContextImage);
                missionContextImage=context!=null?LoadImageTexture(context):null;
                missionContextRecord=context;
                ImageryRevision++;
            }
            return true;
        }
        private static Texture2D LoadImageTexture(ImageRecord record)
        {
            try
            {
                TextAsset bundled=string.IsNullOrEmpty(record.resource)?null:Resources.Load<TextAsset>(record.resource);
                if(!string.IsNullOrEmpty(record.resource)&&bundled==null)return null;
                byte[] png=bundled!=null?bundled.bytes:File.ReadAllBytes(Path.Combine(Cache,record.key+".png"));
                if(bundled!=null)Resources.UnloadAsset(bundled);
                var texture=new Texture2D(2,2,TextureFormat.RGBA32,true,false){name="Official "+record.kind+" matching orthoimage",wrapMode=TextureWrapMode.Clamp,filterMode=FilterMode.Trilinear,anisoLevel=12};
                if(!ImageConversion.LoadImage(texture,png)){UnityEngine.Object.Destroy(texture);return null;}
                // Prepare alpha before upload and discard the CPU texture copy.
                FeatherOfficialImage(texture);
                return texture;
            }
            catch(IOException){return null;}
        }
        internal static void BindMissionImageryContext(Material material)
        {
            if(!MarsMissionZone.ElevationsActive||missionContextImage==null||!material.HasProperty("_CtxDetailTex"))return;
            material.SetTexture("_CtxDetailTex",missionContextImage);
            material.SetVector("_CtxDetailRect",missionContextRecord.rect);
            material.SetFloat("_CtxDetailBlend",1);
            material.SetFloat("_CtxDetailSpacing",missionContextRecord.spacing);
        }
        private static void BindImage(ImageRecord record,Texture2D texture)
        {
            activeImageKey=record.key;activeImage=texture;activeImageRecord=record;
            MarsRegionSourceCache.SetOfficialImagery(texture,record.rect,record.spacing,"NASA / "+record.kind+" matching ortho / "+record.spacing.ToString(record.spacing<1?"0.00":"0.0")+" m grid");
            ImageryRevision++;
        }
        private static Asset MatchingImageAsset(GeoPoint focus,Asset[] choices)
        {
            Crop selected=crops.Where(c=>(!c.missionZone||MarsMissionZone.ElevationsActive)&&Sample(c,focus,out _)).OrderBy(c=>c.spacing).FirstOrDefault();
            return selected==null?null:choices.FirstOrDefault(a=>a.id==selected.id&&!string.IsNullOrEmpty(a.ortho));
        }
        private static string MatchingOrthoSource(Asset asset)
        {
            // This particular A product was checked against its native GeoTIFF
            // transform: 0.254138 m, rather than the C product's 1.016553 m.
            // Other products retain their catalog URL; filenames alone do not
            // establish whether a finer registered orthophoto exists.
            return asset.id=="DTEEC_045994_1985_046060_1985_U01"
                ?"https://astrogeo-ard.s3-us-west-2.amazonaws.com/mars/mro/hirise/controlled/dtm/ESP_045994_1985_ESP_046060_1985/ESP_045994_1985_RED_A_01_ORTHO.tif"
                :asset.ortho;
        }
        private static bool NeedsImage(GeoPoint focus,Asset asset)
        {
            if(asset==null)return false;
            ImageRecord cached=BestImage(focus);
            float desired=asset.kind=="HiRISE" ? .26f : 5.2f;
            if(cached!=null&&cached.spacing<=desired)return false;
            // The COG reader records the actual native IFD grid. A native 1 m
            // source cannot become 25 cm merely by requesting it again.
            string source=MatchingOrthoSource(asset);
            if(images.Any(i=>i.source==source&&i.nativeResolution&&ImageContains(i,focus)))return false;
            return asset.kind=="HiRISE"||!MarsCtxImageryCache.Covers(focus,5.2f);
        }
        public static bool NeedsMatchingImagery(GeoPoint focus)
        {
            if(focus==null)return false;
            EnsureBundled();LoadLocalWindowIndex();
            ImageRecord cached=BestImage(focus);
            if(cached!=null&&cached.spacing<=.26f)return false;
            return NeedsImage(focus,MatchingImageAsset(focus,Candidates(focus)));
        }
        /// <summary>Prefetch a matching native ortho without changing the current
        /// material or waiting for geometry. Activation belongs to the current
        /// view, so a future route station cannot steal the live photograph.</summary>
        public static IEnumerator PrepareMatchingImagery(GeoPoint focus,float budgetSeconds=8,Func<bool> cancelled=null)
        {
            EnsureBundled();LoadLocalWindowIndex();
            float began=Time.realtimeSinceStartup;
            Func<bool> stop=()=>cancelled!=null&&cancelled()||Time.realtimeSinceStartup-began>Mathf.Max(.1f,budgetSeconds);
            yield return FetchMatchingImagery(focus,Candidates(focus),null,stop);
        }
        private static IEnumerator PrepareImagery(GeoPoint focus,Asset[] choices,Action<float,string> progress,Func<bool> cancelled)
        {
            // Keep the current cached photograph visible while a genuinely
            // finer/native source is fetched; a cached image is not a veto.
            ActivateCachedImagery(focus);
            yield return FetchMatchingImagery(focus,choices,progress,cancelled);
            if(cancelled==null||!cancelled())ActivateCachedImagery(focus);
        }
        private static IEnumerator FetchMatchingImagery(GeoPoint focus,Asset[] choices,Action<float,string> progress,Func<bool> cancelled)
        {
            Asset asset=MatchingImageAsset(focus,choices);if(!NeedsImage(focus,asset))yield break;
            string key=Key(asset,focus)+"_ortho_nativev2";
            if(!imageRequests.Add(key))yield break;
            try
            {
                MarsCogWindow.Window image=null;
                // Native pixels only: 0.26 m is a desired HiRISE threshold, not
                // a resampling instruction or a claim about this source.
                string source=MatchingOrthoSource(asset);
                yield return MarsCogWindow.Read(source,focus,asset.kind=="HiRISE"?260:800,cancelled,p=>progress?.Invoke(p,"Loading matching official "+asset.kind+" orthoimage"),w=>image=w);
                if(image==null&&source!=asset.ortho&&(cancelled==null||!cancelled()))
                {
                    source=asset.ortho;
                    yield return MarsCogWindow.Read(source,focus,260,cancelled,p=>progress?.Invoke(p,"Loading cached-grid official orthoimage"),w=>image=w);
                }
                if(image==null||cancelled!=null&&cancelled())yield break;
                Texture2D texture=MakeImage(image);if(texture==null)yield break;
                double circumference=2*Math.PI*3396190;
                var record=new ImageRecord{key=key,source=source,kind=asset.kind,spacing=image.spacing,nativeResolution=true,rect=new Vector4((float)(image.west/circumference+.5),(float)((image.north+image.height*image.dy)/(circumference*.5)+.5),(float)(image.width*image.dx/circumference),(float)(-image.height*image.dy/(circumference*.5)))};
                record.west=image.west/circumference*360;record.east=(image.west+image.width*image.dx)/circumference*360;
                record.south=(image.north+image.height*image.dy)/circumference*360;record.north=image.north/circumference*360;
                try
                {
                    Directory.CreateDirectory(Cache);
                    File.WriteAllBytes(Path.Combine(Cache,key+".png"),texture.EncodeToPNG());
                    File.WriteAllText(Path.Combine(Cache,key+".image.json"),JsonUtility.ToJson(record));
                    images.RemoveAll(i=>i.key==key);images.Add(record);
                    if(activeImageKey==key)activeImageKey=null;
                }
                catch(IOException){}
                // The generated texture is deliberately unbound. Current-view
                // activation loads the cached image and owns its replacement;
                // distant prefetches do not leave orphan GPU textures behind.
                UnityEngine.Object.Destroy(texture);
            }
            finally{imageRequests.Remove(key);}
        }
        public static void RestoreCachedGeometry(GeoPoint focus)
        {
            EnsureBundled();LoadLocalWindowIndex();
            if(!MarsTerrainCachePolicy.ReadPersistentCache)return;
            // Cache restoration must not parse the 45,480-entry network STAC
            // catalog. Inspect only the small local metadata index.
            foreach(var cached in localWindows)
            {
                Crop record=cached.Value;
                string key=Path.GetFileName(cached.Key);
                if(crops.Any(c=>c.cacheKey==key))continue;
                if(crops.Any(c=>c.id==record.id&&Sample(c,focus,out _)))continue;
                double longitude=focus.longitude,around=(record.west+record.width*record.dx*.5)/3396190*180/Math.PI;
                while(longitude-around>180)longitude-=360;while(longitude-around< -180)longitude+=360;
                double x=(longitude*Math.PI/180*3396190-record.west)/record.dx-.5,y=(focus.latitude*Math.PI/180*3396190-record.north)/record.dy-.5;
                if(x<2||y<2||x>=record.width-3||y>=record.height-3)continue;
                try
                {
                    string binary=cached.Key+".f32";if(!File.Exists(binary))continue;
                    Crop c=JsonUtility.FromJson<Crop>(File.ReadAllText(cached.Key+".json"));byte[] bytes=File.ReadAllBytes(binary);
                    if(c.width<=0||c.height<=0||c.width>3072||c.height>3072||bytes.Length!=c.width*c.height*4)continue;
                    c.values=new float[bytes.Length/4];Buffer.BlockCopy(bytes,0,c.values,0,bytes.Length);c.cacheKey=key;Remember(c);
                }
                catch(Exception){}
            }
        }
        private static bool localIndexLoaded;
        private static readonly Dictionary<string,Crop> localWindows=new Dictionary<string,Crop>();
        private static void LoadLocalWindowIndex()
        {
            if(localIndexLoaded||!MarsTerrainCachePolicy.ReadPersistentCache)return;localIndexLoaded=true;
            if(!Directory.Exists(Cache))return;
            foreach(string file in Directory.GetFiles(Cache,"*.json"))try
            {
                string json=File.ReadAllText(file);
                if(file.EndsWith(".image.json")){ImageRecord r=JsonUtility.FromJson<ImageRecord>(json);if(!images.Any(i=>i.key==r.key))images.Add(r);}
                else{Crop c=JsonUtility.FromJson<Crop>(json);if(c.width>0&&c.height>0&&c.dx>0&&c.dy<0)localWindows[Path.Combine(Cache,Path.GetFileNameWithoutExtension(file))]=c;}
            }catch(Exception){}
        }
    }
}

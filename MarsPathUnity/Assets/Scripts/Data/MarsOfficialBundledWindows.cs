using System;
using UnityEngine;

namespace MarsPath
{
    public static partial class MarsOfficialTerrain
    {
        [Serializable]private sealed class BundledWindow{public string metadata,heights;}
        [Serializable]private sealed class BundledWindowIndex{public BundledWindow[] windows;public string[] images;}
        private static bool missionZoneLoaded;
        private static void LoadBundledWindows()
        {
            LoadBundledWindowIndex("NASA/Stereo/BundledWindows",false);
        }
        private static void EnsureMissionZone()
        {
            EnsureBundled();if(missionZoneLoaded)return;missionZoneLoaded=true;
            float began=Time.realtimeSinceStartup;
            LoadBundledWindowIndex("NASA/MissionZone/BundledWindows",true);
            Debug.Log($"MISSION_ZONE_LOCAL_LOAD_SECONDS: {Time.realtimeSinceStartup-began:0.000}");
        }
        private static void LoadBundledWindowIndex(string resource,bool missionZone)
        {
            TextAsset manifest=Resources.Load<TextAsset>(resource);if(manifest==null)return;
            var index=JsonUtility.FromJson<BundledWindowIndex>(manifest.text);if(index?.windows==null)return;
            foreach(BundledWindow record in index.windows)
            {
                TextAsset info=Resources.Load<TextAsset>(record.metadata),data=Resources.Load<TextAsset>(record.heights);if(info==null||data==null)continue;
                Crop crop=JsonUtility.FromJson<Crop>(info.text);byte[] bytes=data.bytes;
                if(crop.width<2||crop.height<2||crop.width>3072||crop.height>3072||bytes.Length!=(long)crop.width*crop.height*4)continue;
                crop.values=new float[bytes.Length/4];Buffer.BlockCopy(bytes,0,crop.values,0,bytes.Length);
                crop.missionZone=missionZone;
                // A mission zone never synthesizes missing height samples.
                // Source validity is audited before bundling; unexpected nodata
                // still fails the native sampler rather than receiving a fill.
                if(missionZone)crop.gaps=new System.Collections.Generic.Dictionary<int,float>();
                Remember(crop);
                Resources.UnloadAsset(data);Resources.UnloadAsset(info);
            }
            if(index.images!=null)foreach(string path in index.images)
            {TextAsset image=Resources.Load<TextAsset>(path);if(image!=null){ImageRecord record=JsonUtility.FromJson<ImageRecord>(image.text);record.missionZone=missionZone;images.Add(record);}}
        }
        internal static bool TryMissionZoneElevation(GeoPoint point,out float elevation)
        {return TryMissionZoneSample(point.latitude,point.longitude,out elevation);}
        private static bool TryMissionZoneSample(double latitude,double longitude,out float elevation)
        {
            EnsureMissionZone();elevation=0;
            if(!MarsMissionZone.BoundsContain(latitude,longitude))return false;
            foreach(Crop crop in crops)
                if(crop.missionZone&&Sample(crop,latitude,longitude,out float height,out float weight,out bool estimated)&&weight>.99f&&!estimated)
                {elevation=height;return true;}
            return false;
        }
    }
}

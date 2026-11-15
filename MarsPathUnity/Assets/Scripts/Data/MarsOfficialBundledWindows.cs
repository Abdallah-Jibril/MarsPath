using System;
using System.Collections;
using UnityEngine;

namespace MarsPath
{
    public static partial class MarsOfficialTerrain
    {
        [Serializable]private sealed class BundledWindow{public string metadata,heights;}
        [Serializable]private sealed class BundledWindowIndex{public BundledWindow[] windows;public string[] images;}
        private static bool missionZoneLoaded;
        private static BundledWindowIndex missionZoneIndex;
        private static int nextMissionWindow;
        private static double missionZoneLoadSeconds;
        private static void LoadBundledWindows()
        {
            LoadBundledWindowIndex("NASA/Stereo/BundledWindows",false);
        }
        private static void EnsureMissionZone()
        {
            while (!missionZoneLoaded) LoadNextMissionWindow();
        }
        internal static IEnumerator WarmMissionZone(Func<bool> waiting)
        {
            while (!missionZoneLoaded && waiting())
            {
                // Bound main-thread work in the lobby to one tile per frame.
                // A foreground reader can finish the same sequence immediately.
                LoadNextMissionWindow();
                yield return null;
            }
        }
        private static void LoadNextMissionWindow()
        {
            double began = Time.realtimeSinceStartupAsDouble;
            EnsureBundled();
            if (missionZoneIndex == null)
                missionZoneIndex = ReadBundledWindowIndex("NASA/MissionZone/BundledWindows");
            if (missionZoneIndex?.windows != null && nextMissionWindow < missionZoneIndex.windows.Length)
                LoadBundledWindow(missionZoneIndex.windows[nextMissionWindow++], true);
            if (missionZoneIndex?.windows == null || nextMissionWindow == missionZoneIndex.windows.Length)
            {
                RegisterBundledImages(missionZoneIndex, true);
                missionZoneLoaded = true;
            }
            missionZoneLoadSeconds += Time.realtimeSinceStartupAsDouble - began;
            if (missionZoneLoaded)
                Debug.Log($"MISSION_ZONE_LOCAL_LOAD_SECONDS: {missionZoneLoadSeconds:0.000}");
        }
        private static void LoadBundledWindowIndex(string resource,bool missionZone)
        {
            BundledWindowIndex index = ReadBundledWindowIndex(resource);
            if (index?.windows == null) return;
            foreach (BundledWindow record in index.windows) LoadBundledWindow(record, missionZone);
            RegisterBundledImages(index, missionZone);
        }
        private static BundledWindowIndex ReadBundledWindowIndex(string resource)
        {
            TextAsset manifest = Resources.Load<TextAsset>(resource);
            return manifest == null ? null : JsonUtility.FromJson<BundledWindowIndex>(manifest.text);
        }
        private static void LoadBundledWindow(BundledWindow record, bool missionZone)
        {
            TextAsset info = Resources.Load<TextAsset>(record.metadata);
            TextAsset data = Resources.Load<TextAsset>(record.heights);
            try
            {
                if (info == null || data == null) return;
                Crop crop = JsonUtility.FromJson<Crop>(info.text);
                byte[] bytes = data.bytes;
                if (crop.width < 2 || crop.height < 2 || crop.width > 3072 || crop.height > 3072 ||
                    bytes.Length != (long)crop.width * crop.height * 4) return;
                crop.values=new float[bytes.Length/4];Buffer.BlockCopy(bytes,0,crop.values,0,bytes.Length);
                crop.missionZone=missionZone;
                // A mission zone never synthesizes missing height samples.
                // Source validity is audited before bundling; unexpected nodata
                // still fails the native sampler rather than receiving a fill.
                if(missionZone)crop.gaps=new System.Collections.Generic.Dictionary<int,float>();
                Remember(crop);
            }
            finally
            {
                if (data != null) Resources.UnloadAsset(data);
                if (info != null) Resources.UnloadAsset(info);
            }
        }
        private static void RegisterBundledImages(BundledWindowIndex index, bool missionZone)
        {
            if(index?.images!=null)foreach(string path in index.images)
            {TextAsset image=Resources.Load<TextAsset>(path);if(image!=null){ImageRecord record=JsonUtility.FromJson<ImageRecord>(image.text);record.missionZone=missionZone;images.Add(record);}}
        }
        internal static bool TryMissionZoneElevation(GeoPoint point,out float elevation)
        {return TryMissionZoneSample(point.latitude,point.longitude,out elevation);}
        internal static bool TryMissionZoneSample(double latitude,double longitude,out float elevation)
        {
            elevation=0;
            if(!MarsMissionZone.BoundsContain(latitude,longitude))return false;
            EnsureMissionZone();
            double east = longitude * Math.PI / 180 * 3396190;
            double north = latitude * Math.PI / 180 * 3396190;
            foreach(Crop crop in crops)
            {
                if (!crop.missionZone) continue;
                double x = (east - crop.west) / crop.dx - .5;
                if (x < 2 || x >= crop.width - 3) continue;
                double y = (north - crop.north) / crop.dy - .5;
                if (y < 2 || y >= crop.height - 3) continue;
                int ix = (int)x, iy = (int)y;
                int k = iy * crop.width + ix;
                float a = crop.values[k], b = crop.values[k + 1];
                float c = crop.values[k + crop.width], d = crop.values[k + crop.width + 1];
                if (!Valid(a) || !Valid(b) || !Valid(c) || !Valid(d)) continue;
                float tx = (float)(x - ix), ty = (float)(y - iy);
                elevation = Mathf.Lerp(Mathf.Lerp(a, b, tx), Mathf.Lerp(c, d, tx), ty);
                return true;
            }
            return false;
        }
    }
}

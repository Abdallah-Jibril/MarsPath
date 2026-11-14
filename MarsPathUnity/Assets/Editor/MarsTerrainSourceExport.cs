using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace MarsPath.Editor
{
    public static class MarsTerrainSourceExport
    {
        public static void ProbeNativeRoutes()
        {
            MarsTerrainCachePolicy.ReadPersistentCache=false;
            var low=new GeoPoint{latitude=18.46205517f,longitude=77.42472712f};
            var ridge=new GeoPoint{latitude=18.46470f,longitude=77.42535f};
            foreach(GeoPoint center in new[]{low,ridge})
                foreach(float bearing in new[]{0f,45f,90f,135f,180f,225f,270f,315f})
                {
                    GeoPoint end=MarsGeospatial.DestinationPrecise(center,bearing,120);
                    MissionData m=MarsRoutePlanner.Generate(center,end);
                    int count=0;foreach(RouteData r in m.routes)if(r.available)count++;
                    Debug.Log($"HIRISE_PROBE {center.latitude:R},{center.longitude:R} -> {end.latitude:R},{end.longitude:R} / bearing{bearing} / available{count} / {m.routes[0].optimalityStatus}");
                }
        }
        [Serializable]public sealed class Track{public string type;public float lengthKm;public GeoPoint[] points;}
        [Serializable]public sealed class Export{public string generated;public Track[] routes;}
        public static void ExportReportedCorridor()
        {
            GeoPoint start=new GeoPoint{latitude=26.73911f,longitude=82.70998f},end=new GeoPoint{latitude=24.24785f,longitude=84.94257f};
            MarsRegionSourceCache.TryLoadCachedRegion(start,end);
            foreach(GeoPoint point in new[]{start,end,MarsRegionalTerrain.Midpoint(start,end)})MarsOfficialTerrain.RestoreCachedGeometry(point);
            MissionData mission=MarsRoutePlanner.Generate(start,end);
            var tracks=new List<Track>();
            foreach(RouteData route in mission.routes)
            {
                if(!route.available)continue;
                var track=new MarsGeographicTrack(mission,route);var points=new GeoPoint[1025];
                for(int i=0;i<points.Length;i++)points[i]=track.At(i/(float)(points.Length-1));
                tracks.Add(new Track{type=route.type,lengthKm=(float)track.LengthKm,points=points});
            }
            string workspace=Directory.GetParent(Application.dataPath).Parent.FullName;
            string path=Path.Combine(workspace,"work","reported199-actual-route-footprints.json");
            File.WriteAllText(path,JsonUtility.ToJson(new Export{generated=DateTime.UtcNow.ToString("O"),routes=tracks.ToArray()},true));
            Debug.Log("SOURCE_ROUTE_EXPORT_READY: "+path);
        }
    }
}

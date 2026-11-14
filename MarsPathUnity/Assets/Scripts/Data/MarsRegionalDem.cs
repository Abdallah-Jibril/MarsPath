using System;
using UnityEngine;

namespace MarsPath
{
    /// <summary>Offline crops of the USGS HRSC/MOLA 200 m blended DEM.
    /// Pixel-centre bilinear sampling; nodata and uncovered regions fall back.</summary>
    public static class MarsRegionalDem
    {
        [Serializable] private class Catalog { public Crop[] crops; }
        [Serializable] private class Crop
        {
            public string name,resource;public int width,height;public double west,north,step;
            [NonSerialized] public byte[] data;
        }
        private static Crop[] crops;
        public static int CropCount{get{Load();return crops.Length;}}
        private static void Load()
        {
            if(crops!=null)return;
            TextAsset catalog=Resources.Load<TextAsset>("NASA/RegionalDem200");
            crops=catalog==null?new Crop[0]:JsonUtility.FromJson<Catalog>(catalog.text).crops;
            foreach(Crop crop in crops)crop.data=Resources.Load<TextAsset>("NASA/"+crop.resource)?.bytes;
        }
        public static bool TryElevation(GeoPoint point,out float elevation)
        {
            bool dynamic=MarsRegionSourceCache.TryElevation(point,out float dynamicHeight);
            if(dynamic&&MarsRegionSourceCache.GridSpacingAt(point)<=200){elevation=dynamicHeight;return true;}
            Load();elevation=0;
            foreach(Crop crop in crops)
            {
                if(crop.data==null||crop.data.Length!=crop.width*crop.height*2)continue;
                double col=(MarsGeospatial.NormalizeLongitude(point.longitude)-crop.west)/crop.step-0.5;
                double row=(crop.north-point.latitude)/crop.step-0.5;
                int x=(int)Math.Floor(col),y=(int)Math.Floor(row);
                if(x<0||y<0||x>=crop.width-1||y>=crop.height-1)continue;
                short a=Value(crop,x,y),b=Value(crop,x+1,y),c=Value(crop,x,y+1),d=Value(crop,x+1,y+1);
                if(a==-32768||b==-32768||c==-32768||d==-32768)continue;
                elevation=Mathf.Lerp(Mathf.Lerp(a,b,(float)(col-x)),Mathf.Lerp(c,d,(float)(col-x)),(float)(row-y));return true;
            }
            if(dynamic&&(!MarsGlobalMola64.Available||MarsRegionSourceCache.GridSpacingAt(point)<926)){elevation=dynamicHeight;return true;}return false;
        }
        private static short Value(Crop crop,int x,int y)
        {int i=(y*crop.width+x)*2;return(short)(crop.data[i]|crop.data[i+1]<<8);}
    }
}

using System;
using System.Collections.Generic;
using UnityEngine;

namespace MarsPath
{
    /// <summary>A frozen geographic copy of the complete selected route, independent
    /// of the moving terrain frame used to show the local surface.</summary>
    public sealed class MarsGeographicTrack
    {
        private readonly GeoPoint[] points;
        private readonly double[] cumulative;
        public double LengthKm=>cumulative[cumulative.Length-1];
        public MarsGeographicTrack(MissionData mission,RouteData route)
        {
            MarsRegionalTerrain.Configure(mission);
            points=new GeoPoint[route.waypoints.Length];cumulative=new double[points.Length];
            for(int i=0;i<points.Length;i++)
            {
                RoutePoint p=route.waypoints[i];points[i]=MarsRegionalTerrain.ToGeo(p.x*60,p.z*60);
                if(i>0)cumulative[i]=cumulative[i-1]+MarsGeospatial.AngularDistanceDegrees(points[i-1],points[i])*Math.PI/180*MarsRegionalTerrain.RadiusMeters/1000;
            }
            points[0]=mission.currentLocation;points[points.Length-1]=mission.destination;
            for(int i=1;i<points.Length;i++)cumulative[i]=cumulative[i-1]+DistanceKm(points[i-1],points[i]);
        }
        private static double DistanceKm(GeoPoint a,GeoPoint b)
        {
            double latA=a.latitude*Math.PI/180,latB=b.latitude*Math.PI/180,d=(b.longitude-a.longitude)*Math.PI/180;
            double h=Math.Pow(Math.Sin((latB-latA)/2),2)+Math.Cos(latA)*Math.Cos(latB)*Math.Pow(Math.Sin(d/2),2);
            return 2*Math.Atan2(Math.Sqrt(Math.Max(0,h)),Math.Sqrt(Math.Max(0,1-h)))*MarsRegionalTerrain.RadiusMeters/1000;
        }
        public GeoPoint At(float progress)
        {PrecisePoint p=AtPrecise((double)progress*LengthKm*1000);return p.Geo;}

        private struct PrecisePoint
        {
            public double lat,lon;
            public GeoPoint Geo=>new GeoPoint{latitude=(float)(lat*180/Math.PI),longitude=(float)(lon*180/Math.PI)};
        }
        private PrecisePoint AtPrecise(double metres)
        {
            if(metres<=0)return new PrecisePoint{lat=points[0].latitude*Math.PI/180,lon=points[0].longitude*Math.PI/180};
            if(metres>=LengthKm*1000)return new PrecisePoint{lat=points[points.Length-1].latitude*Math.PI/180,lon=points[points.Length-1].longitude*Math.PI/180};
            double at=Math.Max(0,Math.Min(LengthKm,metres/1000));int segment=Array.BinarySearch(cumulative,at);if(segment<0)segment=~segment-1;
            segment=Mathf.Clamp(segment,0,points.Length-2);
            double t=(at-cumulative[segment])/Math.Max(0.0000001,cumulative[segment+1]-cumulative[segment]);
            double a=points[segment].latitude*Math.PI/180,b=points[segment+1].latitude*Math.PI/180;
            double lon=points[segment].longitude*Math.PI/180,dLon=MarsGeospatial.NormalizeLongitude(points[segment+1].longitude-points[segment].longitude)*Math.PI/180;
            double y=Math.Sin(dLon)*Math.Cos(b),x=Math.Cos(a)*Math.Sin(b)-Math.Sin(a)*Math.Cos(b)*Math.Cos(dLon);
            double bearing=Math.Atan2(y,x),angle=(cumulative[segment+1]-cumulative[segment])*1000/MarsRegionalTerrain.RadiusMeters*t;
            double lat=Math.Asin(Math.Sin(a)*Math.Cos(angle)+Math.Cos(a)*Math.Sin(angle)*Math.Cos(bearing));
            double resultLon=lon+Math.Atan2(Math.Sin(bearing)*Math.Sin(angle)*Math.Cos(a),Math.Cos(angle)-Math.Sin(a)*Math.Sin(lat));
            return new PrecisePoint{lat=lat,lon=resultLon};
        }
        public GeoPoint AtMeters(double metres)=>AtPrecise(metres).Geo;
        public Vector2 OffsetMeters(double metres,double originMetres)
        {
            PrecisePoint a=AtPrecise(originMetres),b=AtPrecise(metres);double dl=b.lon-a.lon,dy=b.lat-a.lat;
            double h=Math.Pow(Math.Sin(dy/2),2)+Math.Cos(a.lat)*Math.Cos(b.lat)*Math.Pow(Math.Sin(dl/2),2);
            double angle=2*Math.Atan2(Math.Sqrt(Math.Max(0,h)),Math.Sqrt(Math.Max(0,1-h)));
            double bearing=Math.Atan2(Math.Sin(dl)*Math.Cos(b.lat),Math.Cos(a.lat)*Math.Sin(b.lat)-Math.Sin(a.lat)*Math.Cos(b.lat)*Math.Cos(dl));
            double radius=angle*MarsRegionalTerrain.RadiusMeters;
            return new Vector2((float)(Math.Sin(bearing)*radius),(float)(Math.Cos(bearing)*radius));
        }
        public float Heading(float progress)
        {float delta=(float)Math.Min(0.01,0.025/Math.Max(0.001,LengthKm));return MarsGeospatial.InitialBearing(At(Mathf.Max(0,progress-delta)),At(Mathf.Min(1,progress+delta)));}
        public RoutePoint[] LocalWindow(float progress,out float localProgress)
        {
            float start=Mathf.Max(0,progress-(float)(0.10/LengthKm)),end=Mathf.Min(1,progress+(float)(0.40/LengthKm));
            int count=Mathf.Clamp(Mathf.CeilToInt((end-start)*(float)LengthKm/0.004f),2,160);
            List<RoutePoint> local=new List<RoutePoint>();
            for(int i=0;i<=count;i++)
            {Vector2 p=MarsRegionalTerrain.ToWorld(At(Mathf.Lerp(start,end,i/(float)count)));local.Add(new RoutePoint{x=p.x/60,z=p.y/60});}
            localProgress=Mathf.InverseLerp(start,end,progress);return local.ToArray();
        }
        public RoutePoint[] LocalWindowMeters(double metres)
        {
            double start=Math.Max(0,metres-100),end=Math.Min(LengthKm*1000,metres+400);
            int count=Mathf.Clamp((int)Math.Ceiling((end-start)/4),2,160);var local=new List<RoutePoint>();
            for(int i=0;i<=count;i++){Vector2 p=OffsetMeters(start+(end-start)*i/count,metres);local.Add(new RoutePoint{x=p.x/60,z=p.y/60});}
            return local.ToArray();
        }
        private static GeoPoint Copy(GeoPoint p)=>new GeoPoint{latitude=p.latitude,longitude=p.longitude,label=p.label};
    }
}

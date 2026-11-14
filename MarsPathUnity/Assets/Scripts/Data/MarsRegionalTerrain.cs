using System;
using UnityEngine;

namespace MarsPath
{
    /// <summary>Geodesic regional frame with real MOLA/CTX macro elevations.
    /// Measured, nested stereo DTMs; no synthetic displacement in regional scenes.</summary>
    public static class MarsRegionalTerrain
    {
        public const double RadiusMeters = 3396000.0;
        public static MissionData Current { get; private set; }
        public static float MetersPerUnit { get; private set; }
        public static float DisplayReliefScale{get;private set;}=1;
        // This limits the wide-view mesh budget, never the scientific source.
        // A long journey must retain finer resident DTMs under close inspection.
        public static bool CoarsePlanningDisplay=>Current!=null&&!HumanScale&&Current.regionSpanKm>=1520;
        public static bool HasMola => mola != null && mola.Length == 5760 * 2880 * 2;
        private static byte[] mola, jezero,fullJezero;
        public static bool HasFullJezero=>fullJezero!=null&&fullJezero.Length==4456*5067*2;
        private static Vector3 center, east, north;
        private static float seed, centerElevation, visualGain;
        private static string profile;
        // Planning is an azimuthal-equidistant relief map at every distance.
        // The only globe belongs to the global location picker.
        public static bool PlanetaryView=>false;
        public static bool HumanScale=>Current!=null&&Current.regionSpanKm<0.2f;
        public static void LoadData()
        {
            if (mola == null) mola = Resources.Load<TextAsset>("NASA/MarsMola16")?.bytes;
            if (jezero == null) jezero = Resources.Load<TextAsset>("NASA/JezeroHeight16")?.bytes;
            if(fullJezero==null)fullJezero=Resources.Load<TextAsset>("NASA/JezeroFull20")?.bytes;
        }

        public static void Configure(MissionData mission)
        {
            LoadData();
            Current = mission;
            MetersPerUnit = Mathf.Max(1f, mission.regionSpanKm * 1000f / MarsTerrainGenerator.TerrainSize);
            GeoPoint origin = mission.regionCenter ?? mission.currentLocation;
            center = MarsGeospatial.ToUnitSphere(origin.latitude, origin.longitude);
            east = MarsGeospatial.ToUnitSphere(0f, origin.longitude + 90f);
            north = Vector3.Cross(east, center).normalized;
            seed = origin.latitude * 0.173f + origin.longitude * 0.217f;
            profile = mission.terrainProfile ?? "crater";
            centerElevation = Elevation(origin, mission.usesJezeroDem);
            // The planner and walking view share one physical height scale.
            // Legacy saved display multipliers must not turn a real low rim
            // into a mountain that disappears when the user enters the visor.
            DisplayReliefScale=1;
            visualGain = DisplayReliefScale/MetersPerUnit;
        }

        public static GeoPoint Midpoint(GeoPoint start, GeoPoint end)
        {
            Vector3 a = MarsGeospatial.ToUnitSphere(start.latitude, start.longitude);
            Vector3 b = MarsGeospatial.ToUnitSphere(end.latitude, end.longitude);
            Vector3 midpoint = a + b;
            if (midpoint.sqrMagnitude < 0.000001f) midpoint = Vector3.Cross(a, Mathf.Abs(a.y) < 0.9f ? Vector3.up : Vector3.right);
            return MarsGeospatial.FromUnitSphere(midpoint.normalized);
        }

        public static GeoPoint ToGeo(float x, float z)
        {
            GeoPoint origin=Current.regionCenter??Current.currentLocation;
            double distance=Math.Sqrt((double)x*x+(double)z*z)*MetersPerUnit;
            return MarsGeospatial.DestinationPrecise(origin,Math.Atan2(x,z)*180/Math.PI,distance);
        }

        public static Vector2 ToWorld(GeoPoint point)
        {
            // A unit-sphere tangent for a human step is smaller than Unity's
            // Vector3.Normalize epsilon. Compute distance/bearing directly so
            // sub-34-metre positions never collapse to the origin.
            GeoPoint origin=Current.regionCenter??Current.currentLocation;
            double angle=MarsGeospatial.AngularDistanceDegrees(origin,point)*Math.PI/180;
            double bearing=MarsGeospatial.InitialBearing(origin,point)*Math.PI/180;
            double radius=angle*RadiusMeters/MetersPerUnit;
            return new Vector2((float)(Math.Sin(bearing)*radius),(float)(Math.Cos(bearing)*radius));
        }

        public static float MolaElevation(float latitude, float longitude)
        {
            if(MarsGlobalMola64.TryElevation(latitude,longitude,out float offline))return offline;
            return Mola16Elevation(latitude,longitude);
        }
        public static float Mola16Elevation(float latitude,float longitude)
        {
            LoadData();
            if (!HasMola) throw new InvalidOperationException("Global MOLA elevation data is unavailable.");
            float column = Mathf.Repeat(longitude, 360f) * 16f - 0.5f;
            float row = Mathf.Clamp((90f - latitude) * 16f - 0.5f, 0f, 2879f);
            int rawX = Mathf.FloorToInt(column), y0 = Mathf.FloorToInt(row);
            int x0 = (rawX % 5760 + 5760) % 5760, x1 = (x0 + 1) % 5760;
            int y1 = Mathf.Min(y0 + 1, 2879);
            float tx = column - rawX, ty = row - y0;
            return Mathf.Lerp(Mathf.Lerp(ReadMola(x0, y0), ReadMola(x1, y0), tx), Mathf.Lerp(ReadMola(x0, y1), ReadMola(x1, y1), tx), ty);
        }

        private static float ReadMola(int x, int y)
        {
            int index = (y * 5760 + x) * 2;
            return (short)((mola[index] << 8) | mola[index + 1]);
        }
        public static float SampleContextHeight(float x,float z)
        {
            // Context used to bypass every resident finer provider and sample
            // the 16 ppd (~3.7 km) MOLA raster. This flattened small crater rims
            // and made the map's outer surface disagree with the walking view.
            // All providers below are local/cache lookups: sampling starts no
            // downloads and uses the same measured hierarchy at every scale.
            return (WorldElevation(x,z)-centerElevation)*visualGain-1.5f;
        }

        public static bool InJezeroCrop(GeoPoint point)
        {LoadData();return TryFullJezero(point,out _);}

        public static bool TryFullJezero(GeoPoint point,out float elevation)
        {
            elevation=0;if(!HasFullJezero)return false;
            const double radius=3396190;
            double column=(radius*Math.Cos(18.4663*Math.PI/180)*point.longitude*Math.PI/180-4329000)/20-0.5;
            double row=(1143420-radius*point.latitude*Math.PI/180)/20-0.5;
            int x=(int)Math.Floor(column),y=(int)Math.Floor(row);
            if(x<0||x>=4455||y<0||y>=5066)return false;
            short a=ReadFull(x,y),b=ReadFull(x+1,y),c=ReadFull(x,y+1),d=ReadFull(x+1,y+1);
            if(a==-32768||b==-32768||c==-32768||d==-32768)return false;
            elevation=Mathf.Lerp(Mathf.Lerp(a,b,(float)(column-x)),Mathf.Lerp(c,d,(float)(column-x)),(float)(row-y));return true;
        }
        private static short ReadFull(int x,int y){int offset=(y*4456+x)*2;return (short)(fullJezero[offset]|fullJezero[offset+1]<<8);}
        public static Vector2 FullOrthoUv(float x,float z)
        {
            GeoPoint p=ToGeo(x,z);const double radius=3396190;
            double east=radius*Math.Cos(18.4663*Math.PI/180)*p.longitude*Math.PI/180,north=radius*p.latitude*Math.PI/180;
            return new Vector2((float)((east-4329192.10812596)/(17766*5.00206468110263)),(float)(1-(1143347.424175-north)/(20234*5.00226297161329)));
        }

        private static float Elevation(GeoPoint point, bool useJezero)
        {
            float basis=BaseElevation(point);
            return MarsOfficialTerrain.TryMeasured(point,out float official,out float blend)?Mathf.Lerp(basis,official,blend):basis;
        }
        private static float BaseElevation(GeoPoint point)
        {
            if(TryFullJezero(point,out float full))return full;
            float basis=MarsRegionalDem.TryElevation(point,out float regional)?regional:MolaElevation(point.latitude,point.longitude);
            return MarsHrscStereoTerrain.TryElevation(point,out float hrsc,out float coverage)?Mathf.Lerp(basis,hrsc,coverage):basis;
        }
        private static float WorldElevation(float x,float z)
        {
            GeoPoint point=ToGeo(x,z);float basis=BaseElevation(point);
            return MarsOfficialTerrain.TryWorld(Current.regionCenter??Current.currentLocation,x,z,MetersPerUnit,out float official,out float blend)?Mathf.Lerp(basis,official,blend):basis;
        }
        public static float MeasuredHeight(float x,float z)=>WorldElevation(x,z);
        public static float MeasuredElevation(GeoPoint point){LoadData();return Elevation(point,InJezeroCrop(point));}
        public static string SourceTag(GeoPoint point)
        {
            string official=MarsOfficialTerrain.Source(point);if(official!=null)return official;
            if(InJezeroCrop(point))return "USGS CTX / 20 m source";
            string hrsc=MarsHrscStereoTerrain.Source(point);if(hrsc!=null)return hrsc;
            float dynamicSpacing=MarsRegionSourceCache.GridSpacingAt(point);
            if(dynamicSpacing>0&&(!MarsGlobalMola64.Available||dynamicSpacing<926))return $"USGS HRSC/MOLA / {dynamicSpacing:0} m grid";
            if(MarsRegionalDem.TryElevation(point,out _))return "USGS HRSC / MOLA / 200 m source";
            return MarsGlobalMola64.Available?"NASA MOLA / 926 m global grid":"GLOBAL MOLA / ~3.7 km source";
        }
        public static float SourceSpacingMeters(GeoPoint point)
        {
            if(point==null)return 3705;
            float official=MarsOfficialTerrain.Spacing(point);if(official>0)return official;
            if(InJezeroCrop(point))return 20;
            float hrsc=MarsHrscStereoTerrain.Spacing(point);if(hrsc>0)return hrsc;
            float spacing=MarsRegionSourceCache.GridSpacingAt(point);if(spacing>0&&(!MarsGlobalMola64.Available||spacing<926))return spacing;
            return MarsRegionalDem.TryElevation(point,out _)?200:MarsGlobalMola64.Available?926:3705;
        }
        private static float LegacyCropElevation(GeoPoint point,bool useJezero)
        {
            if (!useJezero || jezero == null || !InJezeroCrop(point)) return MolaElevation(point.latitude, point.longitude);
            float x = Mathf.InverseLerp(77.42715f, 77.45365f, point.longitude) * 256f;
            float y = Mathf.InverseLerp(18.440325f, 18.471575f, point.latitude) * 256f;
            int x0 = Mathf.FloorToInt(x), y0 = Mathf.FloorToInt(y);
            float value = Mathf.Lerp(Mathf.Lerp(ReadJezero(x0, y0), ReadJezero(Mathf.Min(x0 + 1, 256), y0), x - x0),
                Mathf.Lerp(ReadJezero(x0, Mathf.Min(y0 + 1, 256)), ReadJezero(Mathf.Min(x0 + 1, 256), Mathf.Min(y0 + 1, 256)), x - x0), y - y0);
            return Mathf.Lerp(-2574.872f, -2519.799f, value);
        }
        private static float ReadJezero(int x, int y)
        { int index = (y * 257 + x) * 2; return (jezero[index] | (jezero[index + 1] << 8)) / 65535f; }

        public static float SurfaceDetail(float x, float z)
        {
            if(HumanScale)return MarsSurfaceGeology.Height(x,z,Current.regionCenter);
            float broad = Mathf.PerlinNoise(x * 0.025f + seed + 54f, z * 0.025f - seed + 28f);
            float medium = Mathf.PerlinNoise(x * 0.084f + seed + 83f, z * 0.084f + seed + 91f) - 0.5f;
            float fine = Mathf.PerlinNoise(x * 0.31f + 53f, z * 0.31f + seed + 17f) - 0.5f;
            float result = (broad - 0.5f) * 3f + medium * 0.70f + fine * 0.16f;
            // Do not invent a volcano/crater at the centre of every local patch.
            // Real large landforms come from the DEM, modeled detail stays local.
            float localBlend=1f-Mathf.SmoothStep(0,1,Mathf.InverseLerp(15,120,Current.regionSpanKm));
            return result*localBlend;
        }

        public static float SampleVisualHeight(float x, float z)
        {
            // Journey length controls mesh density, not which source heights
            // are allowed. Planner, context and EVA use one physical 1:1 field.
            return SampleContextHeight(x,z);
        }

        public static Vector3 RenderPoint(float x,float z,float offset=0)
        {
            return new Vector3(x,SampleVisualHeight(x,z)+offset,z);
        }
        public static Vector3 PlanetPoint(GeoPoint point,float offset=0)
        {
            Vector3 direction=MarsGeospatial.ToUnitSphere(point.latitude,point.longitude);
            float radius=(float)RadiusMeters/MetersPerUnit;
            float relief=(Elevation(point,Current.usesJezeroDem)-centerElevation)*visualGain;
            Vector3 local=new Vector3(Vector3.Dot(direction,east),Vector3.Dot(direction,center),Vector3.Dot(direction,north));
            return local*(radius+relief+offset)-Vector3.up*(radius+1.5f);
        }

        public static float ModelElevation(float x, float z)
        {
            GeoPoint point=ToGeo(x,z);
            return Elevation(point, Current.usesJezeroDem);
        }
    }
}

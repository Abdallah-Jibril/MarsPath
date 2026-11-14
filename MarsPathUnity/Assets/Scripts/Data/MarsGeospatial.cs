using UnityEngine;

namespace MarsPath
{
    /// <summary>
    /// Planet-scale coordinate helpers shared by the future global Mars view
    /// and the high-detail regional mission tiles.
    /// </summary>
    public static class MarsGeospatial
    {
        public static GeoPoint FromUnitSphere(Vector3 position)
        {
            Vector3 direction = position.normalized;
            return new GeoPoint
            {
                label = "Selected location",
                latitude = Mathf.Asin(Mathf.Clamp(direction.y, -1f, 1f)) * Mathf.Rad2Deg,
                longitude = NormalizeLongitude(Mathf.Atan2(-direction.x, direction.z) * Mathf.Rad2Deg)
            };
        }

        public static Vector3 ToUnitSphere(float latitudeDegrees, float longitudeDegrees)
        {
            float latitude = latitudeDegrees * Mathf.Deg2Rad;
            float longitude = NormalizeLongitude(longitudeDegrees) * Mathf.Deg2Rad;
            float equatorialRadius = Mathf.Cos(latitude);
            return new Vector3(
                -equatorialRadius * Mathf.Sin(longitude),
                Mathf.Sin(latitude),
                equatorialRadius * Mathf.Cos(longitude));
        }

        public static Vector2 ToEquirectangularUv(float latitudeDegrees, float longitudeDegrees)
        {
            float u = NormalizeLongitude(longitudeDegrees) / 360f + 0.5f;
            float v = Mathf.Clamp01(latitudeDegrees / 180f + 0.5f);
            return new Vector2(u, v);
        }

        public static float AngularDistanceDegrees(GeoPoint first, GeoPoint second)
        {
            if (first == null || second == null) return 0f;
            // atan2 retains precision for nearby EVA points and at the antipode.
            double latA = first.latitude * System.Math.PI / 180.0;
            double latB = second.latitude * System.Math.PI / 180.0;
            double dLat = latB - latA;
            double dLon = NormalizeLongitude(second.longitude - first.longitude) * System.Math.PI / 180.0;
            double h = System.Math.Sin(dLat / 2) * System.Math.Sin(dLat / 2)
                + System.Math.Cos(latA) * System.Math.Cos(latB) * System.Math.Sin(dLon / 2) * System.Math.Sin(dLon / 2);
            h = System.Math.Max(0.0, System.Math.Min(1.0, h));
            return (float)(2.0 * System.Math.Atan2(System.Math.Sqrt(h), System.Math.Sqrt(1.0 - h)) * 180.0 / System.Math.PI);
        }

        public static float NormalizeLongitude(float longitudeDegrees)
        {
            if (longitudeDegrees >= -180f && longitudeDegrees < 180f) return longitudeDegrees;
            double wrapped = ((double)longitudeDegrees + 180.0) % 360.0;
            if (wrapped < 0) wrapped += 360.0;
            return (float)(wrapped - 180.0);
        }

        public static GeoPoint Destination(GeoPoint start, float bearingDegrees, float distanceKm)
        {return DestinationPrecise(start,bearingDegrees,(double)distanceKm*1000);}

        public static GeoPoint DestinationPrecise(GeoPoint start,double bearingDegrees,double distanceMeters)
        {
            double latitude = start.latitude * System.Math.PI / 180.0;
            double longitude = start.longitude * System.Math.PI / 180.0;
            double bearing = bearingDegrees * System.Math.PI / 180.0;
            double angle = distanceMeters / MarsRegionalTerrain.RadiusMeters;
            // Rotate the position along its local north/east tangent. The scalar
            // longitude formula subtracts nearly equal cosines at a polar
            // origin and can move continental route points to wrong meridians.
            double cp=System.Math.Cos(latitude),sp=System.Math.Sin(latitude),cl=System.Math.Cos(longitude),sl=System.Math.Sin(longitude);
            double cb=System.Math.Cos(bearing),sb=System.Math.Sin(bearing),ca=System.Math.Cos(angle),sa=System.Math.Sin(angle);
            double x=cp*cl*ca+(-sp*cl*cb-sl*sb)*sa,y=cp*sl*ca+(-sp*sl*cb+cl*sb)*sa,z=sp*ca+cp*cb*sa;
            double resultLat=System.Math.Atan2(z,System.Math.Sqrt(x*x+y*y)),resultLon=System.Math.Atan2(y,x);
            return new GeoPoint { label = "Local destination", latitude = (float)(resultLat * 180 / System.Math.PI), longitude = NormalizeLongitude((float)(resultLon * 180 / System.Math.PI)) };
        }

        public static float InitialBearing(GeoPoint start, GeoPoint end)
        {
            double a=start.latitude*System.Math.PI/180, b=end.latitude*System.Math.PI/180;
            double lon=NormalizeLongitude(end.longitude-start.longitude)*System.Math.PI/180;
            return Mathf.Repeat((float)(System.Math.Atan2(System.Math.Sin(lon)*System.Math.Cos(b),
                System.Math.Cos(a)*System.Math.Sin(b)-System.Math.Sin(a)*System.Math.Cos(b)*System.Math.Cos(lon))*180/System.Math.PI),360);
        }
    }
}

using UnityEngine;

namespace MarsPath
{
    public sealed class MarsSite
    {
        public readonly string name, category, profile, description;
        public readonly float latitude, longitude;
        public MarsSite(string name, string category, float latitude, float longitude, string profile, string description)
        { this.name = name; this.category = category; this.latitude = latitude; this.longitude = longitude; this.profile = profile; this.description = description; }
        public GeoPoint Point => new GeoPoint { label = name, latitude = latitude, longitude = longitude };
    }

    public static class MarsSiteCatalog
    {
        public static readonly MarsSite[] Sites =
        {
            new MarsSite("Jezero", "CRATER / PERSEVERANCE", 18.455f, 77.441f, "crater", "Ancient impact basin and delta. The included mission crop has a 20 m CTX DEM."),
            new MarsSite("Gale", "CRATER / CURIOSITY", -4.59f, 137.44f, "crater", "Curiosity landing neighborhood near the layered mountain in Gale crater."),
            new MarsSite("Olympus Mons", "SHIELD VOLCANO", 18.65f, -133.8f, "volcanic", "Large volcanic shield in Tharsis. MOLA supplies the regional elevation context."),
            new MarsSite("Valles Marineris", "CANYON SYSTEM", -14.01f, -58.59f, "canyon", "Extensive canyon system. Plan steep terrain crossings with particular care."),
            new MarsSite("Hellas Planitia", "IMPACT BASIN", -42.43f, 70.50f, "basin", "Deep impact basin in the southern hemisphere."),
            new MarsSite("Utopia Planitia", "NORTHERN PLAINS", 46.74f, 117.52f, "plains", "Broad lowland plain in the northern hemisphere."),
            new MarsSite("Elysium Planitia", "VOLCANIC PLAINS", 2.98f, 154.74f, "plains", "Lowland volcanic plains of the Elysium region."),
            new MarsSite("Isidis Planitia", "IMPACT BASIN", 13.94f, 88.38f, "basin", "Large basin beside the Jezero exploration region."),
            new MarsSite("Arcadia Planitia", "NORTHERN PLAINS", 49.02f, -171.85f, "plains", "Northern lowland terrain near the antimeridian."),
            new MarsSite("Argyre Planitia", "SOUTHERN BASIN", -49.84f, -43.31f, "basin", "Impact basin surrounded by southern highlands."),
            new MarsSite("Chryse Planitia", "OUTFLOW PLAINS", 28.43f, -40.31f, "plains", "Lowland region at the outlet of major ancient channels."),
            new MarsSite("Eridania Planitia", "ANCIENT HIGHLANDS", -38.15f, 122.21f, "crater", "Southern highland basin and ancient terrain.")
        };

        public static MarsSite Nearest(GeoPoint point)
        {
            MarsSite result = Sites[0];
            float best = float.MaxValue;
            foreach (MarsSite site in Sites)
            {
                float angle = MarsGeospatial.AngularDistanceDegrees(point, site.Point);
                if (angle < best) { best = angle; result = site; }
            }
            return result;
        }
    }
}

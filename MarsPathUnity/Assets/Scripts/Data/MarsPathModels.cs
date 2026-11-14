using System;
using UnityEngine;

namespace MarsPath
{
    [Serializable]
    public class MissionData
    {
        public string missionId;
        public string region;
        public string dataNotice;
        public GeoPoint currentLocation;
        public GeoPoint destination;
        public TelemetryData telemetry;
        public RouteData[] routes;
        public HazardData[] hazards;
        public SciencePointData[] sciencePoints;
        public bool generatedRegion;
        public bool usesJezeroDem;
        public float regionSpanKm;
        public float mapReliefScale;
        public GeoPoint regionCenter;
        public string terrainProfile;
        public MarsRoutingOptions routingOptions;
    }

    [Serializable]
    public class GeoPoint
    {
        public string label;
        public float latitude;
        public float longitude;
    }

    [Serializable]
    public class TelemetryData
    {
        public float oxygenPercent;
        public float suitPowerPercent;
        public float heartRateBpm;
    }

    [Serializable]
    public class RouteData
    {
        public string id;
        public string type;
        public string displayName;
        public string summary;
        public string colorHex;
        public float distanceKm;
        public float etaMinutes;
        public float safetyScore;
        public float energyScore;
        public float scienceScore;
        public float maxSlopeDegrees;
        public float elevationGainMeters;
        public bool available = true;
        public bool recommended;
        public string algorithm;
        public string optimalityStatus;
        public float totalRisk;
        public float estimatedEnergyKwh;
        public float elevationLossMeters;
        public float routingGridMeters;
        public bool paretoAlternative;
        public bool withinBudgets;
        public RoutePoint[] waypoints;
        public string[] hazardIds;
        public string[] sciencePointIds;
    }

    [Serializable]
    public class RoutePoint
    {
        public float x;
        public float z;
    }

    [Serializable]
    public class HazardData
    {
        public string id;
        public string label;
        public string severity;
        public string warning;
        public string action;
        public float x;
        public float z;
    }

    [Serializable]
    public class SciencePointData
    {
        public string id;
        public string label;
        public string category;
        public float x;
        public float z;
    }
}

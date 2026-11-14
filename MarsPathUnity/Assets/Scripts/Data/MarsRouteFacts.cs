using System;
using System.Collections.Generic;
using UnityEngine;

namespace MarsPath
{
    /// <summary>
    /// Route-specific, source-derived statistics, sampled from the frozen selected
    /// geographic track. A source grid is a spacing, not an accuracy guarantee.
    /// Suit use, ETA, candidate science and risk remain explicitly model outputs.
    /// </summary>
    public sealed class MarsRouteFacts
    {
        public sealed class SourceShare
        {
            public string source;
            public float distanceKm,percent,gridMeters;
        }
        private sealed class Cached
        {
            public int revision;
            public MarsRouteFacts facts;
        }
        private static MissionData cachedMission;
        private static readonly Dictionary<RouteData,Cached> cache=new Dictionary<RouteData,Cached>();
        public static int SourceRevision
        {
            get{unchecked{return MarsOfficialTerrain.Revision*73856093^MarsHrscStereoTerrain.Revision*19349663^MarsRegionSourceCache.ElevationRevision*83492791;}}
        }

        public readonly MarsRouteElevationProfile profile;
        public readonly float routeLengthKm,groundTrackKm,etaMinutes;
        public readonly float ascentMeters,descentMeters,minimumElevationMeters,maximumElevationMeters,maxGradeDegrees,meanAbsoluteGradeDegrees;
        public readonly float finestGridMeters,coarsestGridMeters,profileStepMeters,stereoCoveragePercent,globalCoveragePercent;
        public readonly int modeledScienceCandidates,modeledHazardZones;
        public readonly float estimatedOxygenGrams,estimatedEnergyKwh;
        public readonly SourceShare[] sources;
        public readonly string routeId;
        public readonly int revision;

        public static MarsRouteFacts For(MissionData mission,RouteData route,MarsRouteElevationProfile sampledProfile=null)
        {
            if(mission==null||route==null)throw new ArgumentNullException(mission==null?nameof(mission):nameof(route));
            if(cachedMission!=mission){cache.Clear();cachedMission=mission;}
            int version=SourceRevision;
            if(cache.TryGetValue(route,out Cached value)&&value.revision==version)return value.facts;
            var profile=sampledProfile!=null&&sampledProfile.sourceRevision==version?sampledProfile:new MarsRouteElevationProfile(mission,route);
            var result=new MarsRouteFacts(mission,route,profile);
            cache[route]=new Cached{revision=result.revision,facts=result};return result;
        }

        private MarsRouteFacts(MissionData mission,RouteData route,MarsRouteElevationProfile sampled)
        {
            profile=sampled;routeId=route.id;revision=SourceRevision;
            routeLengthKm=route.distanceKm;groundTrackKm=sampled.lengthKm;etaMinutes=route.etaMinutes;
            ascentMeters=sampled.ascent;descentMeters=sampled.descent;minimumElevationMeters=sampled.minimum;maximumElevationMeters=sampled.maximum;
            maxGradeDegrees=sampled.maxGrade;meanAbsoluteGradeDegrees=sampled.meanAbsoluteGrade;profileStepMeters=sampled.StepMeters;
            finestGridMeters=float.MaxValue;coarsestGridMeters=0;
            var grouped=new Dictionary<string,SourceShare>();
            float totalWeight=0,stereoWeight=0,globalWeight=0;
            for(int i=0;i<sampled.heights.Length;i++)
            {
                float weight=(i==0||i==sampled.heights.Length-1) ? .5f : 1f;
                string source=sampled.sourceNames[i];float grid=sampled.sourceSpacings[i];
                finestGridMeters=Mathf.Min(finestGridMeters,grid);coarsestGridMeters=Mathf.Max(coarsestGridMeters,grid);
                if(!grouped.TryGetValue(source,out SourceShare share)){share=new SourceShare{source=source,gridMeters=grid};grouped.Add(source,share);}
                share.distanceKm+=weight*profileStepMeters/1000;totalWeight+=weight;
                bool global=source.IndexOf("global",StringComparison.OrdinalIgnoreCase)>=0;
                bool composite=source.IndexOf("MOLA",StringComparison.OrdinalIgnoreCase)>=0;
                if(global)globalWeight+=weight;else if(source.IndexOf("CTX",StringComparison.OrdinalIgnoreCase)>=0||source.IndexOf("HiRISE",StringComparison.OrdinalIgnoreCase)>=0||(!composite&&source.IndexOf("HRSC",StringComparison.OrdinalIgnoreCase)>=0))stereoWeight+=weight;
            }
            var list=new List<SourceShare>(grouped.Values);list.Sort((a,b)=>b.distanceKm.CompareTo(a.distanceKm));
            foreach(SourceShare share in list)share.percent=groundTrackKm>0?100*share.distanceKm/groundTrackKm:0;
            sources=list.ToArray();stereoCoveragePercent=100*stereoWeight/Mathf.Max(.001f,totalWeight);globalCoveragePercent=100*globalWeight/Mathf.Max(.001f,totalWeight);
            modeledScienceCandidates=route.sciencePointIds?.Length??0;modeledHazardZones=route.hazardIds?.Length??0;
            // Integrate signed source grades with the same physical calculator as
            // playback. Route ranking scores are never physiological inputs.
            // These totals exclude resupply and are not measured suit telemetry.
            double totalLength=0, oxygenGrams=0, energyKwh=0;
            for(int i=1;i<sampled.heights.Length;i++)
            {
                double rise=sampled.heights[i]-sampled.heights[i-1];
                totalLength+=Math.Sqrt(sampled.StepMeters*sampled.StepMeters+rise*rise);
            }
            double seconds=Math.Max(1,etaMinutes*60.0),speed=totalLength/seconds;
            for(int i=1;i<sampled.heights.Length;i++)
            {
                double rise=sampled.heights[i]-sampled.heights[i-1];
                double length=Math.Sqrt(sampled.StepMeters*sampled.StepMeters+rise*rise);
                double hours=totalLength>0 ? seconds*length/totalLength/3600 : seconds/(sampled.heights.Length-1)/3600;
                var demand=MarsEvaPhysics.Evaluate(speed,MarsEvaPhysics.SignedGradeDegrees(rise,sampled.StepMeters),
                    (sampled.heights[i]+sampled.heights[i-1])*.5);
                oxygenGrams+=demand.oxygenGramsPerHour*hours;
                energyKwh+=demand.electricalWatts*hours/1000;
            }
            estimatedOxygenGrams=(float)oxygenGrams;estimatedEnergyKwh=(float)energyKwh;
        }
    }
}

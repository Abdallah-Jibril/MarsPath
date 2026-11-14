using System;
using System.Collections;
using UnityEngine;

namespace MarsPath
{
    /// <summary>Native acceptance of route identity, source sampling and frame isolation.</summary>
    public sealed class MarsRouteFactsCheck:MonoBehaviour
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {foreach(string arg in Environment.GetCommandLineArgs())if(arg=="--mars-route-facts-check")new GameObject("Route statistics acceptance").AddComponent<MarsRouteFactsCheck>();}
        private IEnumerator Start()
        {
            yield return null;bool valid=true;
            var mission=MarsRoutePlanner.Generate(new GeoPoint{latitude=18.46470f,longitude=77.42535f},new GeoPoint{latitude=18.46820f,longitude=77.42534f});
            var local=new MissionData{generatedRegion=true,regionCenter=mission.currentLocation,currentLocation=mission.currentLocation,destination=mission.destination,regionSpanKm=.12f,usesJezeroDem=true};
            MarsRegionalTerrain.Configure(local);
            float before=MarsRegionalTerrain.SampleVisualHeight(12,9);
            var first=MarsRouteFacts.For(mission,mission.routes[0]);
            valid&=MarsRegionalTerrain.Current==local&&MarsRegionalTerrain.HumanScale&&Mathf.Abs(before-MarsRegionalTerrain.SampleVisualHeight(12,9))<.0001f;
            foreach(RouteData route in mission.routes)
            {
                if(!route.available)continue;
                var facts=MarsRouteFacts.For(mission,route);
                valid&=facts.routeId==route.id&&Mathf.Abs(facts.routeLengthKm-route.distanceKm)<.000001f&&Mathf.Abs(facts.etaMinutes-route.etaMinutes)<.00001f;
                valid&=ReferenceEquals(facts,MarsRouteFacts.For(mission,route));
                valid&=facts.finestGridMeters<2&&facts.coarsestGridMeters<2&&facts.sources.Length>0;
                float percent=0;foreach(var source in facts.sources)percent+=source.percent;valid&=Mathf.Abs(percent-100)<.01f;
                var p=facts.profile;valid&=Mathf.Abs((p.ascent-p.descent)-(p.heights[p.heights.Length-1]-p.heights[0]))<.02f;
                valid&=p.minimum<=p.heights[0]&&p.maximum>=p.heights[0]&&facts.estimatedOxygenGrams>0&&facts.estimatedEnergyKwh>0;
                for(int i=0;i<p.heights.Length;i+=37)valid&=Mathf.Abs(p.heights[i]-MarsRegionalTerrain.MeasuredElevation(p.locations[i]))<.001f;
                Debug.Log($"ROUTE_FACTS: {route.id} / {facts.routeLengthKm:0.000} km / up {facts.ascentMeters:0.0} m / down {facts.descentMeters:0.0} m / grade {facts.maxGradeDegrees:0.00} / {p.heights.Length} samples / {facts.profileStepMeters:0.00} m step / {facts.modeledScienceCandidates} candidate science");
            }
            var different=MarsRoutePlanner.Generate(new GeoPoint{latitude=19.37447f,longitude=67.33276f},new GeoPoint{latitude=24.83584f,longitude=64.00996f});
            var other=MarsRouteFacts.For(different,different.routes[0]);
            valid&=!ReferenceEquals(first,other)&&Mathf.Abs(first.routeLengthKm-other.routeLengthKm)>10&&Mathf.Abs(first.etaMinutes-other.etaMinutes)>60&&other.coarsestGridMeters>first.coarsestGridMeters;
            Debug.Log(valid?"ROUTE_FACTS_PASSED: route identity, real DEM samples, sampled coverage, cached facts, different selected region, preserved walking frame.":"ROUTE_FACTS_FAILED");Application.Quit(valid?0:2);
        }
    }
}

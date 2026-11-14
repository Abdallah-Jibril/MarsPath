using System;
using System.IO;
using UnityEngine;

namespace MarsPath
{
    /// <summary>Local evidence for reproducing a scene after the app is closed.
    /// This diagnostic record never changes endpoints, routing or terrain.</summary>
    internal static class MarsMissionEvidence
    {
        [Serializable]private sealed class Record
        {
            public string savedUtc,region,route,mode,heightSource,imageSource,missionZoneId;
            public bool diagnosticRun;
            public GeoPoint start,destination,position;
            public float progress,heightGridMeters,imageGridMeters,elevationMeters;
        }
        private static float lastSaved=-10;
        private static MissionData lastMission;
        private static readonly bool DiagnosticRun=Array.Exists(Environment.GetCommandLineArgs(),arg=>arg.StartsWith("--mars-",StringComparison.Ordinal));
        public static void Save(MissionData mission,RouteData route,bool demo,GeoPoint position,double metres,double totalMetres,bool rebased)
        {
            if(!rebased&&ReferenceEquals(lastMission,mission)&&Time.unscaledTime-lastSaved<3)return;
            lastSaved=Time.unscaledTime;lastMission=mission;
            string image=MarsCtxImageryCache.SourceAt(position);float imageGrid=MarsCtxImageryCache.SpacingAt(position);
            if(MarsOfficialTerrain.TryActiveMatchingImage(position,out string kind,out float spacing)&&spacing<imageGrid){image=kind+" matching orthoimage";imageGrid=spacing;}
            if(imageGrid==float.MaxValue)imageGrid=0;
            var record=new Record{savedUtc=DateTime.UtcNow.ToString("O"),region=mission.region,route=route.type,mode=demo?"120 second continuous preview":"mission time",diagnosticRun=DiagnosticRun,missionZoneId=mission.routingOptions?.MissionZoneId??"",
                start=mission.currentLocation,destination=mission.destination,position=position,progress=totalMetres>0?(float)(metres/totalMetres):0,
                heightSource=MarsRegionalTerrain.SourceTag(position),heightGridMeters=MarsRegionalTerrain.SourceSpacingMeters(position),
                elevationMeters=MarsRegionalTerrain.MeasuredElevation(position),imageSource=image,imageGridMeters=imageGrid};
            try{File.WriteAllText(Path.Combine(Application.persistentDataPath,DiagnosticRun?"LastDiagnosticScene.json":"LastMissionScene.json"),JsonUtility.ToJson(record,true));}
            catch(IOException){}catch(UnauthorizedAccessException){}
        }
    }
}

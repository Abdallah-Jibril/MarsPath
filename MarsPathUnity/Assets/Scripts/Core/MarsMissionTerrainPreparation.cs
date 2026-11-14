using System.Collections;
using UnityEngine;

namespace MarsPath
{
    public partial class MarsPathDemoBootstrap
    {
        private string terrainPreparationMessage="";
        private IEnumerator PrepareMissionTerrain(MarsGeographicTrack track,float from,bool presentation,bool dispatched)
        {
            GeoPoint entry=track.At(from);
            if(MarsMissionZone.IsMission(mission))
            {MarsOfficialTerrain.ActivateCachedImagery(entry);terrainPreparationMessage="OFFLINE MEASURED TERRAIN READY";helmetEntryPercent=1;yield return null;yield break;}
            bool geometryFinished=true;
            float entryBegan=Time.realtimeSinceStartup;
            System.Func<bool> entryCancelled=()=>GlobalView.IsVisible&&!dispatched||Time.realtimeSinceStartup-entryBegan>5;
            if(RefinementEnabled&&!Application.isBatchMode&&(MarsRegionalTerrain.SourceSpacingMeters(entry)>25||MarsOfficialTerrain.IsEstimated(entry)))
            {
                geometryFinished=false;
                StartCoroutine(PrepareEntryGeometry(entry,entryCancelled,()=>geometryFinished=true));
            }
            if(RefinementEnabled&&!dispatched&&!Application.isBatchMode&&!MarsCtxImageryCache.Covers(entry,12))
            {
                terrainPreparationMessage="NASA CTX / SURFACE IMAGERY";
                yield return MarsCtxImageryCache.PrepareWindow(entry,3.2f,5,()=>GlobalView.IsVisible,true,
                    (p,s)=>{helmetEntryPercent=p*.8f;terrainPreparationMessage=s;});
            }
            while(!geometryFinished&&!entryCancelled())
            {
                terrainPreparationMessage="LOADING MEASURED SURFACE";
                yield return null;
            }
            MarsCtxImageryCache.ActivateAt(entry);
            int stations=presentation?MarsDemoDirector.TerrainStationCount:1;
            for(int i=0;i<stations;i++)
            {
                float progress=presentation?MarsDemoDirector.StationProgress(i,track.LengthKm):from;
                GeoPoint focus=track.At(progress);
                terrainPreparationMessage=$"TERRAIN {i+1} / {stations}";
                MarsOfficialTerrain.RestoreCachedGeometry(focus);
                if(MarsRegionalTerrain.SourceSpacingMeters(focus)>200)
                {
                    GeoPoint a=MarsGeospatial.Destination(focus,270,4),b=MarsGeospatial.Destination(focus,90,4);
                    MarsRegionSourceCache.TryLoadCachedElevation(a,b);
                }
                if(GlobalView.IsVisible&&!dispatched)yield break;
                helmetEntryPercent=(i+1f)/stations;
                yield return null;
            }
            terrainPreparationMessage="TERRAIN READY";
        }

        private IEnumerator PrepareEntryGeometry(GeoPoint entry,System.Func<bool> cancelled,System.Action finished)
        {
            try
            {
                if(MarsOfficialTerrain.Candidates(entry).Length>0)
                    yield return MarsOfficialTerrain.Prepare(entry,null,cancelled,false);
                if(!cancelled()&&(MarsRegionalTerrain.SourceSpacingMeters(entry)>25||MarsOfficialTerrain.IsEstimated(entry)))
                    yield return MarsHrscStereoTerrain.Prepare(entry,cancelled);
            }
            finally{finished();}
        }
    }
}

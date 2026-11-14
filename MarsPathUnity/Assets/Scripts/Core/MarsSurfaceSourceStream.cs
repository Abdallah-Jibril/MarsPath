using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
namespace MarsPath
{
    public partial class MarsPathDemoBootstrap
    {
        private int surfaceSourceRevision,surfaceGeometryJobs,surfaceImageJobs;
        private int surfaceBaselineJobs;
        private readonly Dictionary<string,float> surfaceBaselineAttempts=new Dictionary<string,float>();
        private bool surfaceSourceLoading;
        private bool surfaceFallbackBusy;
        private const float SurfaceViewExtentKm = 30;
        private string surfaceNeighborhoodKey;
        private readonly List<GeoPoint> surfaceNeighborhood = new List<GeoPoint>();
        private readonly Dictionary<string, bool> surfaceNeighborhoodCoverage = new Dictionary<string, bool>();
        private readonly Dictionary<string,float> surfaceGeometryAttempts=new Dictionary<string,float>(),surfaceImageAttempts=new Dictionary<string,float>();
        public bool SurfaceSourceLoading=>surfaceSourceLoading;
        internal static void SurfaceSourceEndpoints(GeoPoint center,out GeoPoint a,out GeoPoint b)
        {a=MarsGeospatial.Destination(center,270,14);b=MarsGeospatial.Destination(center,90,14);}
        private static string SurfaceJobKey(GeoPoint p)=>Mathf.RoundToInt(p.latitude*100)+"_"+Mathf.RoundToInt(p.longitude*100);
        private IEnumerator EnsureSurfaceSources()
        {
            int ticket=++surfaceSourceRevision;surfaceGeometryJobs=surfaceImageJobs=0;
            surfaceBaselineJobs=0;surfaceBaselineAttempts.Clear();
            surfaceGeometryAttempts.Clear();surfaceImageAttempts.Clear();
            surfaceNeighborhood.Clear();surfaceNeighborhoodCoverage.Clear();surfaceNeighborhoodKey=null;
            while(surfaceActive&&missionStarted&&ticket==surfaceSourceRevision)
            {
                GeoPoint focus=JourneyLocation;
                int officialImageRevision=MarsOfficialTerrain.ImageryRevision;
                MarsOfficialTerrain.ActivateCachedImagery(focus);
                if(MarsMissionZone.IsMission(mission))
                {if(officialImageRevision!=MarsOfficialTerrain.ImageryRevision)surfaceRoot?.GetComponentInChildren<MarsTerrainGenerator>()?.RefreshSourceImagery();surfaceSourceLoading=false;yield return new WaitForSecondsRealtime(.5f);continue;}
                bool imageActivated = MarsCtxImageryCache.ActivateViewAt(focus, SurfaceViewExtentKm);
                imageActivated |= MarsCtxImageryCache.ActivateAt(focus);
                imageActivated |= officialImageRevision!=MarsOfficialTerrain.ImageryRevision;
                if(imageActivated)surfaceRoot?.GetComponentInChildren<MarsTerrainGenerator>()?.RefreshSourceImagery();
                QueueSurfaceFocus(focus,ticket);
                QueueGlobalSurfaceBaseline(focus,ticket);
                QueueSurfaceView(focus,ticket);
                for(int lookahead=1;lookahead<=2;lookahead++)
                {
                    GeoPoint next=demoPresentation?fullTrack.At(MarsDemoDirector.StationProgress(demoDirector.TerrainStation+lookahead,fullTrack.LengthKm)):
                        fullTrack.AtMeters(surfaceMetres+lookahead*900);
                    QueueSurfaceFocus(next,ticket);
                    QueueGlobalSurfaceBaseline(next,ticket);
                    if(lookahead==1)QueueSurfaceNeighborhood(focus,ticket);
                }
                surfaceSourceLoading=surfaceGeometryJobs>0||surfaceBaselineJobs>0||!MarsCtxImageryCache.Covers(focus,12);
                yield return new WaitForSecondsRealtime(.5f);
            }
            if(ticket==surfaceSourceRevision)surfaceSourceLoading=false;
        }
        private void QueueGlobalSurfaceBaseline(GeoPoint point,int ticket)
        {
            if(surfaceBaselineJobs>0||MarsRegionalTerrain.SourceSpacingMeters(point)<=200)return;
            GeoPoint center=GlobalBaselineFocus(point);string key=SurfaceJobKey(center);
            if(surfaceBaselineAttempts.TryGetValue(key,out float tried)&&Time.realtimeSinceStartup-tried<20)return;
            if(surfaceBaselineAttempts.Count>128)surfaceBaselineAttempts.Clear();
            surfaceBaselineAttempts[key]=Time.realtimeSinceStartup;surfaceBaselineJobs++;
            StartCoroutine(PrepareGlobalSurfaceBaseline(point,ticket));
        }
        private IEnumerator PrepareGlobalSurfaceBaseline(GeoPoint point,int ticket)
        {
            int before=MarsRouteFacts.SourceRevision;
            try{yield return PrepareGlobalBaselineAt(point,()=>!surfaceActive||ticket!=surfaceSourceRevision,18);if(before!=MarsRouteFacts.SourceRevision)planningTerrainDirty=true;}
            finally{if(ticket==surfaceSourceRevision)surfaceBaselineJobs=Mathf.Max(0,surfaceBaselineJobs-1);}
        }
        private void QueueSurfaceFocus(GeoPoint point,int ticket)
        {
            string key=SurfaceJobKey(point);float now=Time.realtimeSinceStartup;
            if(surfaceGeometryJobs<2&&MarsRegionalTerrain.SourceSpacingMeters(point)>2&&
                (!surfaceGeometryAttempts.TryGetValue(key,out float tried)||now-tried>60))
            {surfaceGeometryAttempts[key]=now;surfaceGeometryJobs++;StartCoroutine(PrepareSurfaceGeometry(point,ticket));}
            if(surfaceImageJobs<2&&(!MarsCtxImageryCache.Covers(point,12)||MarsOfficialTerrain.NeedsMatchingImagery(point))&&
                (!surfaceImageAttempts.TryGetValue(key,out float imageTried)||now-imageTried>20))
            {surfaceImageAttempts[key]=now;surfaceImageJobs++;StartCoroutine(PrepareSurfaceImage(point,ticket,false));}
        }

        private void QueueSurfaceView(GeoPoint point, int ticket)
        {
            if (surfaceImageJobs >= 2) return;
            bool activated = MarsCtxImageryCache.ActivateViewAt(point, SurfaceViewExtentKm);
            float desiredSpacing = MarsCtxImageryCache.DescribeWindow(point, SurfaceViewExtentKm).spacingMeters;
            if (activated && MarsCtxImageryCache.View.spacingMeters <= desiredSpacing * 1.05f) return;

            string key = "view_" + SurfaceJobKey(point);
            float now = Time.realtimeSinceStartup;
            if (surfaceImageAttempts.TryGetValue(key, out float tried) && now - tried < 30) return;
            surfaceImageAttempts[key] = now;
            surfaceImageJobs++;
            StartCoroutine(PrepareSurfaceImage(point, ticket, true));
        }

        private void QueueSurfaceNeighborhood(GeoPoint point, int ticket)
        {
            if (surfaceGeometryJobs >= 2) return;
            string regionKey = SurfaceJobKey(point);
            if (surfaceNeighborhoodKey != regionKey)
            {
                surfaceNeighborhoodKey = regionKey;
                surfaceNeighborhood.Clear();
                surfaceNeighborhoodCoverage.Clear();
                float originHeight = MarsRegionalTerrain.MeasuredElevation(point);
                var ranked = new List<KeyValuePair<GeoPoint, float>>();
                foreach (float radiusKm in new[] { 1f, 4f, 8f })
                {
                    for (int bearing = 0; bearing < 360; bearing += 45)
                    {
                        GeoPoint sample = MarsGeospatial.Destination(point, bearing, radiusKm);
                        float relief = Mathf.Abs(MarsRegionalTerrain.MeasuredElevation(sample) - originHeight);
                        ranked.Add(new KeyValuePair<GeoPoint, float>(sample, relief));
                    }
                }
                // Bring the largest measured surrounding changes into view
                // first. This ranks source requests only, never the route.
                ranked.Sort((a, b) => b.Value.CompareTo(a.Value));
                foreach (KeyValuePair<GeoPoint, float> candidate in ranked)
                    surfaceNeighborhood.Add(candidate.Key);
            }

            float now = Time.realtimeSinceStartup;
            foreach (GeoPoint sample in surfaceNeighborhood)
            {
                string key = SurfaceJobKey(sample);
                if (surfaceGeometryAttempts.TryGetValue(key, out float tried) && now - tried < 60) continue;
                float spacing = MarsRegionalTerrain.SourceSpacingMeters(sample);
                if (spacing <= 2) continue;
                if (!surfaceNeighborhoodCoverage.TryGetValue(key, out bool canRefine))
                {
                    foreach (MarsOfficialTerrain.Asset candidate in MarsOfficialTerrain.Candidates(sample))
                        if (candidate.spacing < spacing * .9f) { canRefine = true; break; }
                    canRefine |= spacing > 75 && MarsHrscStereoTerrain.GlobalCandidates(sample).Length > 0;
                    surfaceNeighborhoodCoverage[key] = canRefine;
                    // At most one new catalogue search per scheduler tick.
                    // Empty footprints remain cached for this neighborhood.
                    if (!canRefine) return;
                }
                if (!canRefine) continue;
                surfaceGeometryAttempts[key] = now;
                surfaceGeometryJobs++;
                StartCoroutine(PrepareSurfaceGeometry(sample, ticket));
                return;
            }
        }
        private IEnumerator PrepareSurfaceGeometry(GeoPoint point,int ticket)
        {
            float began=Time.realtimeSinceStartup;int before=MarsRouteFacts.SourceRevision;
            Func<bool> stop=()=>!surfaceActive||ticket!=surfaceSourceRevision||Time.realtimeSinceStartup-began>35;
            try
            {
                // A genuine measured window survives presentation jumps instead
                // of being aborted before a 15-second shot can use it.
                yield return MarsOfficialTerrain.Prepare(point,null,stop,false);
                if(!stop()&&MarsRegionalTerrain.SourceSpacingMeters(point)>25)yield return MarsHrscStereoTerrain.Prepare(point,stop);
                // The general 200m baseline has its own resumable job. A slow
                // stereo server must not delay it for the whole geometry budget.
                if(before!=MarsRouteFacts.SourceRevision)planningTerrainDirty=true;
            }
            finally{if(ticket==surfaceSourceRevision)surfaceGeometryJobs=Mathf.Max(0,surfaceGeometryJobs-1);}
        }
        private IEnumerator PrepareSurfaceImage(GeoPoint point,int ticket,bool wideView)
        {
            Func<bool> stop=()=>!surfaceActive||ticket!=surfaceSourceRevision;
            try
            {
                // Fetch only caches; a distant station never steals the current
                // view or replaces the planetary overview texture.
                if(!wideView)
                    yield return MarsOfficialTerrain.PrepareMatchingImagery(point,8,stop);
                if(!stop()&&(wideView||!MarsCtxImageryCache.Covers(point,12)))
                    yield return MarsCtxImageryCache.PrepareWindow(point,wideView?SurfaceViewExtentKm:3.2f,wideView?15:10,stop,true);
                if (!stop())
                {
                    int officialImageRevision=MarsOfficialTerrain.ImageryRevision;
                    MarsOfficialTerrain.ActivateCachedImagery(JourneyLocation);
                    bool activated = MarsCtxImageryCache.ActivateViewAt(JourneyLocation, SurfaceViewExtentKm);
                    activated |= MarsCtxImageryCache.ActivateAt(JourneyLocation);
                    activated |= officialImageRevision!=MarsOfficialTerrain.ImageryRevision;
                    if (activated) surfaceRoot?.GetComponentInChildren<MarsTerrainGenerator>()?.RefreshSourceImagery();
                }
            }
            finally{if(ticket==surfaceSourceRevision)surfaceImageJobs=Mathf.Max(0,surfaceImageJobs-1);}
        }
    }
}

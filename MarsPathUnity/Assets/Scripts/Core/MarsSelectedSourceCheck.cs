using System;
using System.Collections;
using System.IO;
using UnityEngine;

namespace MarsPath
{
    /// <summary>Real source download/cache and native render verification for the
    /// reported new region. Ordinary startup never runs these diagnostics.</summary>
    public sealed class MarsSelectedSourceCheck:MonoBehaviour
    {
        private static string mode;
        public static GeoPoint ReportedStart=>new GeoPoint{label="Selected start",latitude=26.55588f,longitude=80.49558f};
        public static GeoPoint ReportedEnd=>new GeoPoint{label="Selected destination",latitude=25.79927f,longitude=81.48650f};
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            foreach(string argument in Environment.GetCommandLineArgs())
                if(argument=="--mars-source-check"||argument=="--mars-capture-source-region"||argument=="--mars-capture-source-zoom"||argument=="--mars-capture-source-ground")
                {mode=argument;new GameObject("Selected Source Verification").AddComponent<MarsSelectedSourceCheck>();break;}
        }
        private IEnumerator Start()
        {
            yield return null;int lastPercent=-10;
            MarsRegionSourceCache.TryLoadCachedRegion(ReportedStart,ReportedEnd);
            GeoPoint midpoint=MarsRegionalTerrain.Midpoint(ReportedStart,ReportedEnd);
            if(MarsRegionSourceCache.GridSpacingAt(midpoint)!=200||!MarsRegionSourceCache.CoversImagery(midpoint)||!MarsRegionSourceCache.OrbitalIsMonochrome)
                yield return MarsRegionSourceCache.PrepareRegion(ReportedStart,ReportedEnd,(p,message)=>{int percent=Mathf.RoundToInt(p*100);if(percent>=lastPercent+5){lastPercent=percent;Debug.Log("MARSPATH_SOURCE_PROGRESS: "+percent+" / "+message);}});
            MarsPathDemoBootstrap demo=FindAnyObjectByType<MarsPathDemoBootstrap>();
            bool okay=true;
            try
            {
                GeoPoint center=MarsRegionalTerrain.Midpoint(ReportedStart,ReportedEnd);
                if(!MarsRegionSourceCache.TryElevation(center,out float height)||MarsRegionSourceCache.GridSpacingAt(center)!=200)throw new Exception("Reported region did not acquire a real 200 m elevation grid. "+MarsRegionSourceCache.LastError);
                if(!MarsRegionSourceCache.CoversImagery(center)||!MarsRegionSourceCache.OrbitalIsMonochrome)throw new Exception("Reported region did not acquire NASA MRO CTX coverage. "+MarsRegionSourceCache.LastError);
                if(height< -9000||height>24000||MarsRegionSourceCache.OrbitalTexture.width>4096||MarsRegionSourceCache.OrbitalTexture.height>4096)throw new Exception("Source dimensions or elevations invalid");
                GeoPoint[] golden={new GeoPoint{latitude=26.176817428f,longitude=80.9899333105f},new GeoPoint{latitude=26.176817428f,longitude=81.00680391465f},new GeoPoint{latitude=26.15994682386f,longitude=80.9899333105f}};
                float[] expected={-717,-755,-714};
                for(int i=0;i<golden.Length;i++)if(!MarsRegionSourceCache.TryElevation(golden[i],out float actual)||Mathf.Abs(actual-expected[i])>1.5f)throw new Exception("Downloaded grid differs from independent GDAL source sample "+i+" / "+actual);
                demo.LoadRegionalMission(MarsRoutePlanner.Generate(ReportedStart,ReportedEnd));demo.GlobalView.SetEndpoints(ReportedStart,ReportedEnd);demo.GlobalView.OpenJezeroMission();
                Debug.Log($"MARSPATH_SOURCE_VERIFIED: center {center.latitude},{center.longitude} / height {height:0.000} m / grid {MarsRegionSourceCache.ElevationDisplayMetersPerPixel} m / CTX image {MarsRegionSourceCache.ImageryDisplayMetersPerPixel:0.0} m/pixel / {MarsRegionSourceCache.OrbitalTexture.width}x{MarsRegionSourceCache.OrbitalTexture.height}");
            }
            catch(Exception e){okay=false;Debug.LogError("MARSPATH_SELECTED_SOURCE_FAILED: "+e);}
            yield return null;
            if(okay)
            {
                try
                {
                    GameObject terrain=GameObject.Find("Selected Regional Terrain");
                    Mesh mesh=terrain.GetComponent<MeshFilter>().sharedMesh;
                    if(terrain.transform.Find("Continuous planetary context")!=null||mesh.uv2.Length!=mesh.vertexCount)throw new Exception("Expected georeferenced regional relief map");
                    Vector3[] vertices=mesh.vertices;
                    for(int i=0;i<vertices.Length;i+=997)
                    {Vector3 p=vertices[i];if(Mathf.Abs(p.y-MarsTerrainGenerator.SampleHeight(p.x,p.z))>.001f)throw new Exception("Rendered relief differs from selected DEM");}
                    Debug.Log("MARSPATH_SELECTED_SOURCE_PASSED: 200 m arbitrary-region elevation, genuine CTX imagery, georeferenced regional relief geometry and cached offline reuse.");
                }
                catch(Exception e){okay=false;Debug.LogError("MARSPATH_SELECTED_SOURCE_FAILED: "+e);}
            }
            if(mode=="--mars-source-check"){Application.Quit(okay?0:2);yield break;}
            if(!okay){Application.Quit(2);yield break;}
            if(mode=="--mars-capture-source-zoom")
            {
                float initialSpacing=MarsRegionSourceCache.ImageryDisplayMetersPerPixel;
                MarsRegionSourceCache.TryElevation(midpoint,out float originalHeight);
                FindAnyObjectByType<MarsPathCameraRig>().SetPlanningView(15,48,8);
                MarsPlanningDetailStream stream=demo.GetComponent<MarsPlanningDetailStream>();bool seenLoading=false;float until=Time.realtimeSinceStartup+50;
                do{yield return new WaitForSeconds(0.1f);seenLoading|=stream.IsLoading;if(!stream.IsLoading&&MarsRegionSourceCache.ImageryDisplayMetersPerPixel<=15)break;}while(Time.realtimeSinceStartup<until);
                MarsRegionSourceCache.TryElevation(midpoint,out float afterHeight);
                if(MarsRegionSourceCache.ImageryDisplayMetersPerPixel>15||Mathf.Abs(originalHeight-afterHeight)>0.001f)
                {Debug.LogError("MARSPATH_DETAIL_STREAM_FAILED: no sharper image or frozen elevation changed");Application.Quit(2);yield break;}
                Debug.Log($"MARSPATH_DETAIL_STREAM_PASSED: image sampling {initialSpacing:0.0} -> {MarsRegionSourceCache.ImageryDisplayMetersPerPixel:0.0} m; existing DEM and mesh unchanged.");
            }
            if(mode=="--mars-capture-source-ground"){demo.StartFullRouteForTest();demo.SeekFullJourney(0.48f);}
            yield return new WaitForSeconds(2);
            if(mode=="--mars-capture-source-region")
            {float until=Time.realtimeSinceStartup+30;while(demo.GetComponent<MarsPlanningDetailStream>().IsLoading&&Time.realtimeSinceStartup<until)yield return new WaitForSeconds(0.1f);}
            string name=mode=="--mars-capture-source-region"?"MarsPathSourceRegion.png":mode=="--mars-capture-source-zoom"?"MarsPathSourceDetail.png":"MarsPathSourceGround.png";
            ScreenCapture.CaptureScreenshot(Path.Combine(Application.persistentDataPath,name));yield return new WaitForSeconds(2);
            Debug.Log("MARSPATH_SOURCE_CAPTURE: "+name);
            if(mode=="--mars-capture-source-zoom")
            {
                demo.StartFullRouteForTest();yield return null;demo.PreparePlanningCapture();
                float until=Time.realtimeSinceStartup+45;
                while((!MarsRegionSourceCache.CoversImagery(ReportedStart)||!MarsRegionSourceCache.CoversImagery(ReportedEnd))&&Time.realtimeSinceStartup<until)yield return new WaitForSeconds(0.1f);
                if(!MarsRegionSourceCache.CoversImagery(ReportedStart)||!MarsRegionSourceCache.CoversImagery(ReportedEnd))
                {Debug.LogError("MARSPATH_DETAIL_RETURN_FAILED: regional image coverage did not recover after EVA");Application.Quit(2);yield break;}
                Debug.Log("MARSPATH_DETAIL_RETURN_PASSED: full regional imagery recovers after close-up and EVA return.");
            }
            Application.Quit(0);
        }
    }
}

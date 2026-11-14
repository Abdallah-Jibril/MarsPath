using System;
using System.Collections;
using System.IO;
using UnityEngine;

namespace MarsPath
{
    public sealed class MarsOfficialTerrainCheck:MonoBehaviour
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {foreach(string arg in Environment.GetCommandLineArgs())if(arg=="--mars-official-terrain-check")new GameObject("Official terrain verification").AddComponent<MarsOfficialTerrainCheck>();}
        private IEnumerator Start()
        {
            StartCoroutine(Deadline());yield return null;
            GeoPoint point=new GeoPoint{latitude=18.46f,longitude=77.44f};
            Debug.Log("OFFICIAL_CATALOG_COUNT: "+MarsOfficialTerrain.CatalogCount);
            foreach(var a in MarsOfficialTerrain.Candidates(point))Debug.Log("OFFICIAL_CANDIDATE: "+a.kind+" / "+a.id+" / "+a.url);
            yield return MarsOfficialTerrain.Prepare(point,(p,s)=>{if(p==1)Debug.Log("OFFICIAL_STAGE: "+s);},null,true);
            if(!MarsOfficialTerrain.TryElevation(point,out float height)||MarsOfficialTerrain.Spacing(point)>2.1f)
            {Debug.LogError("OFFICIAL_TEST_FAILED: "+MarsOfficialTerrain.LastError+" / "+MarsOfficialTerrain.Status);Application.Quit(2);yield break;}
            Debug.Log("OFFICIAL_POINT: "+point.latitude.ToString("R")+","+point.longitude.ToString("R")+" / "+height.ToString("R")+" / "+MarsOfficialTerrain.Source(point));
            MarsCogWindow.Window window=null;
            string url="https://astrogeo-ard.s3-us-west-2.amazonaws.com/mars/mro/hirise/controlled/dtm/ESP_045994_1985_ESP_046060_1985/DTEEC_045994_1985_046060_1985_U01.tif";
            yield return MarsCogWindow.Read(url,point,800,null,null,w=>window=w);
            if(window==null){Debug.LogError("OFFICIAL_TEST_FAILED: direct COG "+MarsCogWindow.LastError);Application.Quit(2);yield break;}
            string dir=Path.Combine(Application.persistentDataPath,"OfficialTerrainCheck");Directory.CreateDirectory(dir);
            byte[] bytes=new byte[window.values.Length*4];Buffer.BlockCopy(window.values,0,bytes,0,bytes.Length);File.WriteAllBytes(Path.Combine(dir,"golden.f32"),bytes);
            File.WriteAllText(Path.Combine(dir,"golden.json"),JsonUtility.ToJson(new Metadata{width=window.width,height=window.height,west=window.west,north=window.north,dx=window.dx,dy=window.dy,source=url},true));
            int index=window.height/2*window.width+window.width/2;
            Debug.Log("OFFICIAL_COG_PIXEL: "+window.width+"x"+window.height+" / center "+window.values[index].ToString("R"));
            Debug.Log("OFFICIAL_TEST_PASSED: decoded native floating point HiRISE stereo tiles; "+MarsOfficialTerrain.Source(point)+"; matching imagery "+MarsRegionSourceCache.ImageryStatus);
            GeoPoint gale=new GeoPoint{latitude=-4.5f,longitude=137.4f};
            yield return MarsOfficialTerrain.Prepare(gale,null,null,false);
            if(!MarsOfficialTerrain.TryElevation(gale,out float galeHeight)||MarsOfficialTerrain.Spacing(gale)>21||galeHeight< -5500||galeHeight> -2500)
            {Debug.LogError("OFFICIAL_TEST_FAILED: non-Jezero stereo source / "+MarsOfficialTerrain.LastError+" / height "+galeHeight);Application.Quit(2);yield break;}
            Debug.Log("OFFICIAL_GALE_PASSED: "+galeHeight.ToString("R")+" m / "+MarsOfficialTerrain.Source(gale));
            yield return FindAnyObjectByType<MarsPathDemoBootstrap>().ReviewOfficialJourney();
        }
        [Serializable]private class Metadata{public int width,height;public double west,north,dx,dy;public string source;}
        private IEnumerator Deadline(){yield return new WaitForSecondsRealtime(600);Debug.LogError("OFFICIAL_TEST_FAILED: deadline");Application.Quit(2);}
    }
    public partial class MarsPathDemoBootstrap
    {
        public IEnumerator ReviewOfficialJourney()
        {
            GeoPoint start=new GeoPoint{latitude=18.4647f,longitude=77.42535f,label="Jezero stereo terrain"},end=new GeoPoint{latitude=18.4695f,longitude=77.4295f,label="Mapped outcrop"};
            yield return MarsRegionSourceCache.PrepareRegion(start,end,null,null,true,true);
            yield return MarsOfficialTerrain.Prepare(start,null,null,true);yield return MarsOfficialTerrain.Prepare(end,null,null,false);
            LoadRegionalMission(MarsRoutePlanner.Generate(start,end));GlobalView.SetEndpoints(start,end);GlobalView.OpenJezeroMission();
            cameraRig.ResetPlanningView();
            if(!OfficialMeshMatches(terrainRoot)){Application.Quit(2);yield break;}
            yield return CaptureHelmetReview("MarsPathOfficialPlanner.png");
            demoPresentation=true;yield return PrepareHelmetEntry(false);paused=true;
            if(!OfficialMeshMatches(surfaceRoot)||!cameraRig.HelmetView){Application.Quit(2);yield break;}
            yield return CaptureHelmetReview("MarsPathOfficialHelmet.png");
            for(int shot=0;shot<8;shot++)
            {
                if(shot>0)AdvancePresentation(15,mission.routes[selectedRoute]);
                if(MarsRegionalTerrain.SourceSpacingMeters(JourneyLocation)>2.1f||!OfficialMeshMatches(surfaceRoot))
                {Debug.LogError("OFFICIAL_TEST_FAILED: presentation station "+shot+" / "+MarsRegionalTerrain.SourceTag(JourneyLocation));Application.Quit(2);yield break;}
                Debug.Log("OFFICIAL_DEMO_STATION: "+shot+" / "+JourneyLocation.latitude+","+JourneyLocation.longitude+" / "+MarsRegionalTerrain.SourceTag(JourneyLocation));
                if(shot==4){cameraRig.SnapToMission(astronaut.transform);yield return CaptureHelmetReview("MarsPathOfficialOutcrop.png");}
            }
            AdvancePresentation(15,mission.routes[selectedRoute]);
            bool okay=DemoEnded&&MarsGeospatial.AngularDistanceDegrees(JourneyLocation,DemoPreviewEnd)<.00002f;
            UseTimedPlayback(1);
            foreach(float station in new[]{0f,.25f,.5f,.75f,1f})
            {
                SeekFullJourney(station);
                okay&=OfficialMeshMatches(surfaceRoot)&&MarsRegionalTerrain.SourceSpacingMeters(JourneyLocation)<2.1f;
                if(station<1){Vector3 before=astronaut.transform.position;AdvanceJourney(.5f,mission.routes[selectedRoute]);UpdateTimedJourneyPosition(mission.routes[selectedRoute]);okay&=Vector3.Distance(before,astronaut.transform.position)>.03f;}
            }
            Debug.Log(okay?"OFFICIAL_JOURNEY_PASSED: planner, continuous demo and full-mission checkpoints share native measured stereo elevation.":"OFFICIAL_TEST_FAILED: journey continuity");
            Application.Quit(okay?0:2);
        }
        private bool OfficialMeshMatches(GameObject root)
        {
            MeshFilter[] filters=root.GetComponentsInChildren<MeshFilter>();int checkedVertices=0;
            foreach(MeshFilter f in filters)
            {
                if(f.gameObject.name!="Continuous Surface Terrain"&&f.gameObject.name!="Selected Regional Terrain")continue;
                Vector3[] v=f.sharedMesh.vertices;
                for(int i=0;i<v.Length;i+=Mathf.Max(1,v.Length/1000)){Vector3 p=v[i];if(Mathf.Abs(p.y-MarsRegionalTerrain.SampleVisualHeight(p.x,p.z))>.002f){Debug.LogError("OFFICIAL_TEST_FAILED: mesh != elevation at "+p);return false;}checkedVertices++;}
            }
            return checkedVertices>50;
        }
    }
}

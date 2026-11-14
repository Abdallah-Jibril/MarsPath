using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace MarsPath
{
    public sealed class MarsReported199Review : MonoBehaviour
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            foreach(string argument in Environment.GetCommandLineArgs())
                if(argument=="--mars-reported199-review")new GameObject("Reported crater route acceptance").AddComponent<MarsReported199Review>();
        }
        private IEnumerator Start(){yield return null;yield return FindAnyObjectByType<MarsPathDemoBootstrap>().ReviewReported199();}
    }

    public partial class MarsPathDemoBootstrap
    {
        public IEnumerator ReviewReported199()
        {
            RefinementEnabled=false;
            bool passed=true;
            GeoPoint start=new GeoPoint{latitude=26.73911f,longitude=82.70998f,label="Reported start"};
            GeoPoint end=new GeoPoint{latitude=24.24785f,longitude=84.94257f,label="Reported destination"};
            ReturnToGlobalMap();GlobalView.SetEndpoints(start,end);
            float began=Time.realtimeSinceStartup;
            yield return BuildSelectedRegion(start,end);
            passed&=mission.routes[selectedRoute].available&&cameraRig.RoutesInsideViewport(mission);
            Debug.Log($"REPORTED199_PLANNING: {Time.realtimeSinceStartup-began:0.000}s / {MarsAdaptivePlanningTerrain.VertexCount} vertices / {MarsAdaptivePlanningTerrain.SampleCount} samples / {MarsAdaptivePlanningTerrain.LeafCount} leaves");
            passed&=MarsAdaptivePlanningTerrain.VertexCount>0&&MarsAdaptivePlanningTerrain.VertexCount<=70000&&MarsAdaptivePlanningTerrain.SampleCount<=100000;
            passed&=AdaptiveMeshSourceAndSeamsMatch();
            var track=new MarsGeographicTrack(mission,mission.routes[selectedRoute]);
            MarsRouteElevationProfile profile=SelectedElevationProfile;
            int floor=0,descent=1,ascent=1;
            float falling=0,rising=0;
            for(int i=4;i<profile.heights.Length-4;i++)
            {
                if(profile.heights[i]<profile.heights[floor])floor=i;
                float change=profile.heights[i+4]-profile.heights[i-4];
                if(change<falling){falling=change;descent=i;}
                if(change>rising){rising=change;ascent=i;}
            }
            float[] fractions={0,descent/(float)(profile.heights.Length-1),floor/(float)(profile.heights.Length-1),ascent/(float)(profile.heights.Length-1)};
            float[] plannerHeights=new float[fractions.Length];GeoPoint[] positions=new GeoPoint[fractions.Length];
            float mapCenter=MarsRegionalTerrain.MeasuredElevation(mission.regionCenter),mapMetres=MarsRegionalTerrain.MetersPerUnit;
            for(int i=0;i<fractions.Length;i++)
            {
                positions[i]=track.At(fractions[i]);plannerHeights[i]=MarsRegionalTerrain.MeasuredElevation(positions[i]);
                Vector2 local=MarsRegionalTerrain.ToWorld(positions[i]);
                bool mapped=MarsAdaptivePlanningTerrain.TryRenderedHeight(local.x,local.y,out float drawn);
                if(mapped)
                    Debug.Log($"REPORTED199_MAP_STATION: {i} / {fractions[i]*100:0.0}% / measured {plannerHeights[i]:0.00}m / displayed {(drawn+1.5f)*mapMetres+mapCenter:0.00}m / cell {MarsAdaptivePlanningTerrain.CellMetersAt(local.x,local.y):0.0}m");
                // Test visible height fidelity rather than a uniform cell-size
                // proxy: a planar plateau needs fewer vertices than a crater rim.
                passed&=mapped&&Mathf.Abs((drawn+1.5f)*mapMetres+mapCenter-plannerHeights[i])<=10;
            }
            passed&=Mathf.Abs(plannerHeights[0]+1729.245914f)<.15f&&plannerHeights[2]<plannerHeights[0]-1000;
            yield return PreparePlanningPhotographs(mission,refinementTicket);
            yield return CaptureHelmetReview("MarsPathReported199Planner.png");
            // A planner-selected profile station must open the identical point,
            // rather than restarting the route when requesting a ground preview.
            inspectProgress=fractions[1];demoPresentation=true;
            yield return PrepareHelmetEntry(true);paused=true;
            passed&=MarsGeospatial.AngularDistanceDegrees(JourneyLocation,positions[1])<.00002f;
            passed&=Mathf.Abs(MarsRegionalTerrain.MeasuredElevation(JourneyLocation)-plannerHeights[1])<.2f;
            StartFromHelmetPreview();paused=true;
            passed&=MarsGeospatial.AngularDistanceDegrees(JourneyLocation,start)<.00002f;
            UseTimedPlayback(1);
            passed&=MarsHazardIcons.HasPicture(MarsHazardIcons.Symbol.Rocks)&&MarsHazardIcons.HasPicture(MarsHazardIcons.Symbol.Slope);
            for(int i=0;i<fractions.Length;i++)
            {
                SeekFullJourney(fractions[i]);paused=true;
                float until=Time.realtimeSinceStartup+1;
                while(Time.realtimeSinceStartup<until)yield return null;
                passed&=MarsGeospatial.AngularDistanceDegrees(JourneyLocation,positions[i])<.00002f;
                float actual=MarsRegionalTerrain.MeasuredElevation(JourneyLocation);
                passed&=Mathf.Abs(actual-plannerHeights[i])<.2f&&OfficialMeshMatches(surfaceRoot)&&SurfaceHorizonMatches();
                passed&=MarsRegionalTerrain.HumanScale&&Mathf.Abs(MarsRegionalTerrain.MetersPerUnit-1)<.001f;
                GeoPoint anchor=surfaceCenter;
                _=SelectedElevationProfile;
                passed&=MarsRegionalTerrain.HumanScale&&MarsGeospatial.AngularDistanceDegrees(MarsRegionalTerrain.Current.regionCenter,anchor)<.000001f;
                cameraRig.SetHelmetView(true);cameraRig.SnapToMission(astronaut.transform);
                yield return MarsCtxImageryCache.PrepareWindow(JourneyLocation,30,8,()=>false,false);
                MarsCtxImageryCache.ActivateViewAt(JourneyLocation,30);
                surfaceRoot.GetComponentInChildren<MarsTerrainGenerator>().RefreshSourceImagery();
                yield return CaptureHelmetReview("MarsPathReported199Visor"+i+".png");
                if(i==2)
                {
                    cameraRig.SetHelmetView(false);cameraRig.SnapToMission(astronaut.transform);
                    yield return CaptureHelmetReview("MarsPathReported199FloorChase.png");
                }
                Debug.Log($"REPORTED199_COLOCATION: {i} / {JourneyLocation.latitude:0.000000},{JourneyLocation.longitude:0.000000} / planner {plannerHeights[i]:0.000}m / visor {actual:0.000}m / {DataTag}");
            }
            cameraRig.SetHelmetView(true);cameraRig.SnapToMission(astronaut.transform);
            // UI-only diagnostic: exercise both illustrations independently of
            // whether the current measured station happens to trigger a sensor.
            reserveStop=false;rockAhead=true;rockAheadDistance=8;nearbyHazard=null;
            yield return CaptureHelmetReview("MarsPathReported199RockWarningUI.png");
            rockAhead=false;nearbyHazard=null;upcomingSurfaceSlope=18;
            yield return CaptureHelmetReview("MarsPathReported199SlopeWarningUI.png");
            SeekFullJourney(1);
            passed&=MarsGeospatial.AngularDistanceDegrees(JourneyLocation,end)<.00002f;
            RestartTwoMinuteDemo();AdvancePresentation(30,mission.routes[selectedRoute]);paused=true;
            passed&=DemoMode&&MarsGeospatial.AngularDistanceDegrees(JourneyLocation,fullTrack.At(JourneyProgress))<.00002f&&MarsRegionalTerrain.HumanScale;
            cameraRig.SetHelmetView(true);cameraRig.SnapToMission(astronaut.transform);
            yield return CaptureHelmetReview("MarsPathReported199DemoRim.png");
            Debug.Log(passed?"REPORTED199_PASSED: bounded crack-free adaptive map, original source elevations, colocated rim/floor/plateau, frame-safe live context, exact endpoints and illustrated warning UI.":"REPORTED199_FAILED: inspect geometry/colocation assertions.");
            Application.Quit(passed?0:2);
        }

        private bool AdaptiveMeshSourceAndSeamsMatch()
        {
            var adaptive=terrainRoot.GetComponentInChildren<MarsAdaptivePlanningTerrain>();
            if(adaptive==null)return false;
            Mesh mesh=adaptive.GetComponent<MeshFilter>().sharedMesh;Vector3[] vertices=mesh.vertices;int[] triangles=mesh.triangles;
            bool passed=true;float maximumError=0;
            for(int i=0;i<vertices.Length;i+=Mathf.Max(1,vertices.Length/2000))
            {
                Vector3 v=vertices[i];
                maximumError=Mathf.Max(maximumError,Mathf.Abs(v.y-MarsRegionalTerrain.SampleVisualHeight(v.x,v.z))*MarsRegionalTerrain.MetersPerUnit);
            }
            passed&=maximumError<.05f;
            var edges=new Dictionary<long,int>();
            for(int i=0;i<triangles.Length;i+=3)for(int e=0;e<3;e++)
            {
                int a=triangles[i+e],b=triangles[i+(e+1)%3];long key=((long)Mathf.Min(a,b)<<32)|(uint)Mathf.Max(a,b);
                edges.TryGetValue(key,out int count);edges[key]=count+1;
            }
            float bound=mesh.bounds.extents.x;int gaps=0;
            foreach(var pair in edges)
            {
                if(pair.Value==2)continue;
                Vector3 a=vertices[(int)(pair.Key>>32)],b=vertices[(int)(pair.Key&0xffffffff)];
                bool outside=Mathf.Abs(Mathf.Abs(a.x)-bound)<.00001f&&Mathf.Abs(a.x-b.x)<.00001f||Mathf.Abs(Mathf.Abs(a.z)-bound)<.00001f&&Mathf.Abs(a.z-b.z)<.00001f;
                if(pair.Value!=1||!outside)gaps++;
            }
            Debug.Log($"REPORTED199_MESH: max source error {maximumError:0.0000}m / internal unmatched edges {gaps}");
            return passed&&gaps==0;
        }
    }
}

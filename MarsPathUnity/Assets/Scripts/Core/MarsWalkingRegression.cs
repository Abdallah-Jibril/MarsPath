using System;
using UnityEngine;

namespace MarsPath
{
    public partial class MarsPathDemoBootstrap
    {
        public void CheckReportedWalking()
        {
            GeoPoint start=new GeoPoint{latitude=18.455f,longitude=77.441f,label="Jezero"};
            GeoPoint end=new GeoPoint{latitude=26.70201f,longitude=85.09122f,label="Selected destination"};
            LoadRegionalMission(MarsRoutePlanner.Generate(start,end));GlobalView.SetEndpoints(start,end);GlobalView.OpenJezeroMission();
            RestartTwoMinuteDemo();StartMission();
            foreach(float metres in new[]{1f,4f,12f,25f})
            {
                GeoPoint point=MarsGeospatial.Destination(surfaceCenter,334,metres/1000);
                float recovered=MarsRegionalTerrain.ToWorld(point).magnitude;
                if(Mathf.Abs(recovered-metres)>0.45f)throw new Exception($"Short step collapsed: {metres} m became {recovered} m");
            }
            Vector3 initial=astronaut.transform.position;
            for(int second=0;second<8;second++)
            {
                Vector3 previous=astronaut.transform.position;
                for(int frame=0;frame<60;frame++)AdvancePresentation(1f/60,mission.routes[selectedRoute]);
                float moved=Vector3.Distance(previous,astronaut.transform.position);
                if(moved<1.4f||moved>1.8f)throw new Exception("Continuous demo displacement per second invalid: "+moved);
                if(VisibleSurfaceArrows<3||VisibleSurfaceArrows>6)throw new Exception("Forward arrows missing during walking: "+VisibleSurfaceArrows);
            }
            float distance=Vector3.Distance(initial,astronaut.transform.position);
            if(distance<11.5f||distance>14.1f)throw new Exception("Eight-second continuous preview displacement invalid: "+distance);
            Debug.Log($"MARSPATH_WALKING_PASSED: reported ~700 km route, 1/4/12/25 m projections, eight seconds moved {distance:0.00} m, 3–6 arrows ahead every second.");
            paused=true;
        }
    }
}

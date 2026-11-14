using UnityEngine;

namespace MarsPath
{
    public partial class MarsPathDemoBootstrap
    {
        private readonly MarsDemoDirector demoDirector=new MarsDemoDirector();
        private bool demoPresentation=true;
        public bool DemoMode=>demoPresentation;
        public bool TimedControlsVisible=>missionStarted&&!demoPresentation;
        public float DemoElapsed=>desktopMissionMirror ? mirrorDemoSeconds : demoDirector.Elapsed;
        private bool DemoEnded => demoPresentation && (desktopMissionMirror
            ? currentLiveState?.demoComplete == true : demoDirector.Complete);
        internal GeoPoint DemoPreviewEnd => fullTrack.AtMeters(MarsDemoDirector.PreviewMetres(fullTrack.LengthKm * 1000));
        private static string DemoScope(RouteData route)
        {
            double length = route.distanceKm * 1000.0;
            double preview = MarsDemoDirector.PreviewMetres(length);
            return length <= preview ? "120 SECONDS · COMPLETE ROUTE" : $"120 SECONDS · FIRST {preview:0} m FROM START";
        }

        private void ResetDemo() => demoDirector.Reset();

        internal void AdvancePresentation(float seconds,RouteData route)
        {
            demoDirector.Advance(seconds, surfaceActive ? fullTrack.LengthKm : route.distanceKm);
            double targetSeconds = Mathf.Max(1, route.etaMinutes * 60) * (double)demoDirector.Progress;
            AdvanceJourney((float)System.Math.Max(0, targetSeconds - totalElapsedSeconds), route);
            if (evaSimulation.ReserveReached) UpdateTimedJourneyPosition(route);
            else
            {
                routeProgress = demoDirector.Progress;
                if (surfaceActive) UpdateSurfaceDistance(demoDirector.DistanceMeters);
                else UpdateAstronautPosition(routeProgress);
            }
            if (DemoEnded) paused = true;
        }
        private void ContinueAfterDemo()
        {
            UseTimedPlayback(1);
            paused = reserveStop || arrived;
            if (Application.platform == RuntimePlatform.Android) PauseQuestTraverse();
        }
        public void UseTimedPlayback(float speed)
        {
            bool previous=expeditionMode;demoPresentation=false;expeditionMode=!CanStartSelectedEva;SetPlaybackSpeed(speed);
            if(missionStarted&&previous!=expeditionMode)SeekFullJourney(routeProgress);
        }
        public void RestartTwoMinuteDemo()
        {
            demoPresentation=true;ResetDemo();paused=Application.platform==RuntimePlatform.Android;reserveStop=false;arrived=false;
            if(missionStarted)
            {
                expeditionMode=true;
                SeekFullJourney(0);
                if (paused && Application.platform == RuntimePlatform.Android) PauseQuestTraverse();
            }
        }
        public void PrepareDemoRegionCapture(bool ground)
        {
            GeoPoint start=new GeoPoint{label="Selected start",latitude=17.6049f,longitude=169.877f};
            GeoPoint end=new GeoPoint{label="Selected destination",latitude=23.69213f,longitude=167.9657f};
            LoadRegionalMission(MarsRoutePlanner.Generate(start,end));GlobalView.SetEndpoints(start,end);GlobalView.OpenJezeroMission();
            if(ground){demoPresentation=true;StartMission();AdvancePresentation(60,mission.routes[selectedRoute]);paused=true;}
        }
        public void PrepareTerrainReviewCapture(int view)
        {
            if(view==0)
            {
                GeoPoint start=new GeoPoint{label="Jezero start",latitude=18.44470f,longitude=77.45100f};
                GeoPoint end=new GeoPoint{label="Selected destination",latitude=29.84734f,longitude=49.40358f};
                LoadRegionalMission(MarsRoutePlanner.Generate(start,end));GlobalView.SetEndpoints(start,end);GlobalView.OpenJezeroMission();return;
            }
            PrepareJezeroWideCapture();demoPresentation=true;StartMission();AdvancePresentation(75,mission.routes[selectedRoute]);paused=true;
            if(view==2)UseTimedPlayback(30);
        }
    }
}

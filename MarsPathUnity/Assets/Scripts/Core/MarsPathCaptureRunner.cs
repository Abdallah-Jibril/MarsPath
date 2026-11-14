using System.Collections;
using System.IO;
using UnityEngine;

namespace MarsPath
{
    public class MarsPathCaptureRunner : MonoBehaviour
    {
        private static bool captureMission;
        private static bool captureArrival;
        private static bool captureGlobal;
        private static bool captureSelection;
        private static bool captureRegional,captureRegionalEva;
        private static bool captureWalk;
        private static bool captureJourney,captureLocalSection;
        private static bool captureFullMid,captureFullEnd,captureJezeroWide;
        private static bool captureDemoRegion,captureCleanMission;
        private static int terrainReview=-1;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void InstallWhenRequested()
        {
            foreach (string argument in System.Environment.GetCommandLineArgs())
            {
                if(argument=="--mars-capture-walking-fix")
                {terrainReview=3;new GameObject("Walking fix capture").AddComponent<MarsPathCaptureRunner>();break;}
                if(argument=="--mars-capture-real-region"||argument=="--mars-capture-real-ground"||argument=="--mars-capture-real-timed")
                {terrainReview=argument=="--mars-capture-real-region"?0:argument=="--mars-capture-real-ground"?1:2;new GameObject("Terrain Review Capture").AddComponent<MarsPathCaptureRunner>();break;}
                if (argument != "--mars-capture" && argument != "--mars-capture-mission" && argument != "--mars-capture-arrival"
                    && argument != "--mars-capture-global" && argument != "--mars-capture-global-selection"
                    && argument != "--mars-capture-region" && argument != "--mars-capture-region-eva" && argument != "--mars-capture-walk"
                    && argument != "--mars-capture-journey" && argument != "--mars-capture-local-section"
                    && argument != "--mars-capture-full-mid" && argument != "--mars-capture-full-end" && argument != "--mars-capture-jezero-wide"
                    && argument != "--mars-capture-demo-region" && argument != "--mars-capture-clean-mission") continue;
                captureDemoRegion=argument=="--mars-capture-demo-region";captureCleanMission=argument=="--mars-capture-clean-mission";
                captureFullMid=argument=="--mars-capture-full-mid";captureFullEnd=argument=="--mars-capture-full-end";captureJezeroWide=argument=="--mars-capture-jezero-wide";
                captureJourney=argument=="--mars-capture-journey"||argument=="--mars-capture-local-section";
                captureLocalSection=argument=="--mars-capture-local-section";
                captureMission = argument == "--mars-capture-mission" || argument == "--mars-capture-arrival";
                captureArrival = argument == "--mars-capture-arrival";
                captureGlobal = argument == "--mars-capture-global" || argument == "--mars-capture-global-selection";
                captureSelection = argument == "--mars-capture-global-selection";
                captureWalk=argument=="--mars-capture-walk";
                captureRegional=argument=="--mars-capture-region"||argument=="--mars-capture-region-eva"||captureWalk;
                captureRegionalEva=argument=="--mars-capture-region-eva"||captureWalk;
                new GameObject("MarsPath Capture Runner").AddComponent<MarsPathCaptureRunner>();
                break;
            }
        }

        private IEnumerator Start()
        {
            yield return new WaitForSeconds(0.5f);
            MarsPathDemoBootstrap demo = Object.FindAnyObjectByType<MarsPathDemoBootstrap>();
            if (demo != null)
            {
                if(terrainReview==3)demo.CheckReportedWalking();
                else if(terrainReview>=0)demo.PrepareTerrainReviewCapture(terrainReview);
                else if(captureDemoRegion||captureCleanMission)demo.PrepareDemoRegionCapture(captureCleanMission);
                else if(captureFullMid||captureFullEnd)demo.PrepareFullJourneyCapture(captureFullEnd?0.98f:0.63f);
                else if(captureJezeroWide)demo.PrepareJezeroWideCapture();
                else if(captureJourney)demo.PrepareJourneyCapture(captureLocalSection);
                else if(captureRegional)demo.PrepareRegionalCapture(3,captureRegionalEva);
                else if (captureGlobal) demo.GlobalView.PrepareGlobalCapture(captureSelection);
                else if (captureMission)
                {
                    if (captureArrival) demo.PrepareArrivalCapture();
                    else demo.PrepareMissionCapture();
                }
                else demo.PreparePlanningCapture();
            }
            yield return new WaitForSeconds(2f);
            if(captureWalk)
            {
                string directory=Path.Combine(Application.persistentDataPath,"MarsPathWalkCapture");
                if(demo!=null)demo.SetPlaybackSpeed(30);
                Directory.CreateDirectory(directory);Time.captureFramerate=24;
                for(int frame=0;frame<192;frame++)
                {
                    yield return new WaitForEndOfFrame();
                    Texture2D frameTexture=ScreenCapture.CaptureScreenshotAsTexture();
                    File.WriteAllBytes(Path.Combine(directory,$"frame-{frame:0000}.png"),frameTexture.EncodeToPNG());
                    Destroy(frameTexture);
                }
                Time.captureFramerate=0;yield return new WaitForSeconds(1f);
                Debug.Log("MARSPATH_WALK_CAPTURE_COMPLETE: "+directory);Application.Quit(0);yield break;
            }
            string fileName = captureDemoRegion?"MarsPathDemoRegion.png":captureCleanMission?"MarsPathCleanMission.png":captureFullMid?"MarsPathFullMid.png":captureFullEnd?"MarsPathFullEnd.png":captureJezeroWide?"MarsPathJezeroWide.png":captureJourney?(captureLocalSection?"MarsPathLocalSectionCapture.png":"MarsPathJourneyCapture.png"):captureRegional?(captureRegionalEva?"MarsPathRegionalEvaCapture.png":"MarsPathRegionalCapture.png"):captureGlobal ? (captureSelection ? "MarsPathGlobalSelectionCapture.png" : "MarsPathGlobalCapture.png")
                : captureArrival ? "MarsPathArrivalCapture.png" : captureMission ? "MarsPathMissionCapture.png" : "MarsPathCapture.png";
            string path = Path.Combine(Application.persistentDataPath, fileName);
            if(terrainReview>=0)path=Path.Combine(Application.persistentDataPath,"MarsPathTerrainReview"+terrainReview+".png");
            ScreenCapture.CaptureScreenshot(path);
            yield return new WaitForSeconds(2f);
            Debug.Log($"MARSPATH_CAPTURE_COMPLETE: {path}");
            Application.Quit(0);
        }
    }
}

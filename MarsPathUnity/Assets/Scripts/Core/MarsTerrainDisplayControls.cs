using UnityEngine;

namespace MarsPath
{
    public partial class MarsPathDemoBootstrap
    {
        private void ChangeMapRelief(float scale)
        {
            int chosen=selectedRoute;mission.mapReliefScale=1;
            LoadRegionalMission(mission);SelectRoute(chosen);cameraRig.FrameRoutes(mission);
        }
    }
}

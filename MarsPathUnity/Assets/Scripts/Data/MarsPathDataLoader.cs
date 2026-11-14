using UnityEngine;

namespace MarsPath
{
    public static class MarsPathDataLoader
    {
        public static MissionData LoadMockMission()
        {
            TextAsset missionFile = Resources.Load<TextAsset>("mock_mission");
            if (missionFile == null)
            {
                Debug.LogError("MarsPath mock mission resource is missing.");
                return null;
            }

            MissionData mission = JsonUtility.FromJson<MissionData>(missionFile.text);
            if (mission == null || mission.routes == null || mission.routes.Length == 0)
            {
                Debug.LogError("MarsPath mission JSON is invalid or contains no routes.");
                return null;
            }

            return mission;
        }
    }
}

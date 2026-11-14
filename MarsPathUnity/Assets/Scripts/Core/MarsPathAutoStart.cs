using UnityEngine;

namespace MarsPath
{
    public static class MarsPathAutoStart
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void CreateDemo()
        {
            if (Object.FindAnyObjectByType<MarsPathDemoBootstrap>() != null) return;
            new GameObject("MarsPath Demo Bootstrap").AddComponent<MarsPathDemoBootstrap>();
        }
    }
}

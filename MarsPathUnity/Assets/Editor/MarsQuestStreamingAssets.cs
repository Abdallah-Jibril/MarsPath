#if UNITY_ANDROID
using System.IO;
using UnityEditor.Android;
using UnityEngine;

namespace MarsPath.Editor
{
    internal sealed class MarsQuestStreamingAssets : IPostGenerateGradleAndroidProject
    {
        public int callbackOrder => 0;

        public void OnPostGenerateGradleAndroidProject(string unityLibraryPath)
        {
            // This is a generated copy. Android uses the embedded global MOLA grid
            // and the native-resolution Jezero Resources, which are packed separately.
            string assets = Path.Combine(unityLibraryPath, "src", "main", "assets");
            string desktopRaster = Path.Combine(assets, "Mola64");
            if (!Directory.Exists(desktopRaster)) return;

            foreach (string file in Directory.EnumerateFiles(desktopRaster, "megt*.img"))
                File.Delete(file);
            File.Delete(Path.Combine(desktopRaster, "sources.json"));
            Debug.Log("MARSPATH_QUEST_ASSETS: Kept embedded terrain and omitted desktop-only MOLA rasters.");
        }
    }
}
#endif

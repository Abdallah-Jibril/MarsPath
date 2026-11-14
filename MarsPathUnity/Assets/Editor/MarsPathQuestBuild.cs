using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.XR.Management;
using UnityEditor.XR.Management.Metadata;
using UnityEditor.XR.OpenXR.Features;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.XR.Management;
using UnityEngine.XR.OpenXR;
using UnityEngine.XR.OpenXR.Features;
using UnityEngine.XR.OpenXR.Features.Interactions;
using UnityEngine.XR.OpenXR.Features.MetaQuestSupport;

namespace MarsPath.Editor
{
    public static class MarsPathQuestBuild
    {
        private const string OutputPath = "Builds/Quest/MarsPath.apk";
        private const string ScenePath = "Assets/Scenes/MarsPathDemo.unity";
        private const BuildTargetGroup AndroidGroup = BuildTargetGroup.Android;

        [MenuItem("MarsPath/Build Quest APK")]
        public static void BuildApk()
        {
            if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.Android)
                throw new InvalidOperationException("Select the Android build profile or start Unity with -buildTarget Android.");

            EnsureInputBackend();
            ConfigureOpenXR();
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.Android, ScriptingImplementation.IL2CPP);
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android, "com.lagrangev.marspath");
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
            PlayerSettings.Android.forceInternetPermission = true;
            PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel29;
            PlayerSettings.Android.targetSdkVersion = AndroidSdkVersions.AndroidApiLevel34;
            PlayerSettings.Android.useCustomKeystore = false;
            PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.Android, false);
            PlayerSettings.SetGraphicsAPIs(BuildTarget.Android, new[] { GraphicsDeviceType.Vulkan });
            EditorUserBuildSettings.buildAppBundle = false;
            EditorUserBuildSettings.exportAsGoogleAndroidProject = false;
            AssetDatabase.SaveAssets();

            Directory.CreateDirectory(Path.GetDirectoryName(OutputPath));
            BuildReport report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = new[] { ScenePath },
                locationPathName = OutputPath,
                target = BuildTarget.Android,
                options = BuildOptions.None
            });
            if (report.summary.result != BuildResult.Succeeded)
                throw new InvalidOperationException($"MarsPath Quest build failed: {report.summary.result}");

            Debug.Log($"MARSPATH_QUEST_BUILD_COMPLETE: {report.summary.outputPath} ({report.summary.totalSize} bytes)");
        }

        private static void ConfigureOpenXR()
        {
            if (!EditorBuildSettings.TryGetConfigObject(XRGeneralSettings.settingsKey,
                out XRGeneralSettingsPerBuildTarget general))
            {
                if (!AssetDatabase.IsValidFolder("Assets/XR")) AssetDatabase.CreateFolder("Assets", "XR");
                if (!AssetDatabase.IsValidFolder("Assets/XR/Settings")) AssetDatabase.CreateFolder("Assets/XR", "Settings");
                const string path = "Assets/XR/Settings/XRGeneralSettingsPerBuildTarget.asset";
                general = AssetDatabase.LoadAssetAtPath<XRGeneralSettingsPerBuildTarget>(path);
                if (general == null)
                {
                    general = ScriptableObject.CreateInstance<XRGeneralSettingsPerBuildTarget>();
                    AssetDatabase.CreateAsset(general, path);
                }
                EditorBuildSettings.AddConfigObject(XRGeneralSettings.settingsKey, general, true);
            }
            if (!general.HasManagerSettingsForBuildTarget(AndroidGroup))
                general.CreateDefaultManagerSettingsForBuildTarget(AndroidGroup);

            XRGeneralSettings android = general.SettingsForBuildTarget(AndroidGroup);
            android.InitManagerOnStart = true;
            string loader = typeof(OpenXRLoader).FullName;
            if (!XRPackageMetadataStore.IsLoaderAssigned(loader, AndroidGroup) &&
                !XRPackageMetadataStore.AssignLoader(android.Manager, loader, AndroidGroup))
                throw new InvalidOperationException("Unity could not assign the Android OpenXR loader.");

            FeatureHelpers.RefreshFeatures(AndroidGroup);
            OpenXRSettings openXR = OpenXRSettings.GetSettingsForBuildTargetGroup(AndroidGroup);
            if (openXR == null) throw new InvalidOperationException("Unity could not create the Android OpenXR settings.");
            // The built-in terrain and route shaders must draw correctly in both eyes.
            openXR.renderMode = OpenXRSettings.RenderMode.MultiPass;
            EnableFeature<MetaQuestFeature>(openXR);
            EnableFeature<MetaQuestTouchPlusControllerProfile>(openXR);
            EnableFeature<OculusTouchControllerProfile>(openXR);
            EditorUtility.SetDirty(general);
            EditorUtility.SetDirty(android);
            EditorUtility.SetDirty(openXR);
        }

        private static void EnableFeature<T>(OpenXRSettings settings) where T : OpenXRFeature
        {
            T feature = settings.GetFeature<T>();
            if (feature == null) throw new InvalidOperationException($"Missing OpenXR feature: {typeof(T).Name}");
            feature.enabled = true;
            EditorUtility.SetDirty(feature);
        }

        private static void EnsureInputBackend()
        {
#if !ENABLE_INPUT_SYSTEM || ENABLE_LEGACY_INPUT_MANAGER
            throw new InvalidOperationException("Android OpenXR requires Input System Package (New) as the only active backend. Use tools/build-quest.ps1 or set it in Player Settings and restart Unity.");
#endif
        }
    }
}

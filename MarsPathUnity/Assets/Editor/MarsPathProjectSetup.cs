using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace MarsPath.Editor
{
    public static class MarsPathProjectSetup
    {
        private const string ScenePath = "Assets/Scenes/MarsPathDemo.unity";
        private const string NasaAstronautModelPath = "Assets/External/NASA/Astronaut.obj";
        private const string NasaAstronautPrefabPath = "Assets/Resources/NASA/NasaAstronaut.prefab";

        [MenuItem("MarsPath/Prepare Demo Project")]
        public static void PrepareDemoProject()
        {
            ConfigureTerrainTextures();
            CreateNasaAstronautPrefab();
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            EditorSceneManager.SaveScene(scene, ScenePath);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };

            PlayerSettings.companyName = "LAGRANGE V";
            PlayerSettings.productName = "MarsPath";
            ConfigureApplicationIcon();
            PlayerSettings.colorSpace = ColorSpace.Linear;
            PlayerSettings.defaultScreenWidth = 1440;
            PlayerSettings.defaultScreenHeight = 900;
            PlayerSettings.resizableWindow = true;

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("MARSPATH_SETUP_COMPLETE: Scene and build settings are ready.");
        }

        private static void ConfigureApplicationIcon()
        {
            const string path="Assets/Branding/MarsPathIcon.png";
            AssetDatabase.ImportAsset(path,ImportAssetOptions.ForceSynchronousImport);
            var importer=AssetImporter.GetAtPath(path) as TextureImporter;
            if(importer!=null){importer.textureType=TextureImporterType.Default;importer.alphaIsTransparency=true;importer.mipmapEnabled=false;importer.textureCompression=TextureImporterCompression.Uncompressed;importer.maxTextureSize=512;importer.SaveAndReimport();}
            Texture2D icon=AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if(icon==null)throw new System.Exception("MarsPath application icon is missing.");
            foreach(var target in new[]{UnityEditor.Build.NamedBuildTarget.Unknown,UnityEditor.Build.NamedBuildTarget.Standalone})
            {
                int[] sizes=PlayerSettings.GetIconSizes(target,IconKind.Application);var icons=new Texture2D[sizes.Length];
                for(int i=0;i<icons.Length;i++)icons[i]=icon;
                PlayerSettings.SetIcons(target,icons,IconKind.Application);
            }
            Debug.Log("MARSPATH_ICON_CONFIGURED: supplied Mars planet artwork.");
        }

        private static void ConfigureTerrainTextures()
        {
            ConfigureTexture("Assets/Resources/NASA/JezeroMissionSurface.jpg", false);
            ConfigureTexture("Assets/Resources/NASA/JezeroMissionNormal.png", true);
            ConfigureTexture("Assets/Resources/NASA/MarsGlobalViking.jpg", false);
            ConfigureTexture("Assets/Resources/NASA/MarsMdimColor8k.jpg", false);
            ConfigureTexture("Assets/Resources/NASA/JezeroFullOrtho.jpg", false);
            ConfigureTexture("Assets/Resources/NASA/JezeroGround.jpg", false);
        }

        private static void ConfigureTexture(string path, bool normalMap)
        {
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null) return;
            importer.textureType = normalMap ? TextureImporterType.NormalMap : TextureImporterType.Default;
            importer.sRGBTexture = !normalMap;
            importer.maxTextureSize = path.Contains("MdimColor8k")?8192:path.Contains("FullOrtho")?4096:2048;
            importer.anisoLevel=12;
            importer.textureCompression = TextureImporterCompression.CompressedHQ;
            importer.mipmapEnabled = true;
            importer.SaveAndReimport();
        }

        private static void CreateNasaAstronautPrefab()
        {
            ModelImporter astronautImporter = AssetImporter.GetAtPath(NasaAstronautModelPath) as ModelImporter;
            if (astronautImporter != null && !astronautImporter.isReadable)
            {
                astronautImporter.isReadable = true;
                astronautImporter.SaveAndReimport();
            }
            AssetDatabase.ImportAsset(NasaAstronautModelPath, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
            GameObject model = AssetDatabase.LoadAssetAtPath<GameObject>(NasaAstronautModelPath);
            if (model == null)
            {
                Debug.LogWarning("NASA astronaut model is unavailable; the procedural astronaut will remain as fallback.");
                return;
            }

            GameObject root = new GameObject("NASA Astronaut — DigitalSpace Corporation");
            GameObject visual = Object.Instantiate(model);
            visual.name = "NASA Astronaut Visual";
            visual.transform.SetParent(root.transform, false);
            Renderer[] renderers = visual.GetComponentsInChildren<Renderer>();
            Material[] suitMaterials=new Material[2];
            for(int materialIndex=0;materialIndex<2;materialIndex++)
            {
                string path=$"Assets/Resources/NASA/NasaSuit{materialIndex+1}.mat";
                Material suit=AssetDatabase.LoadAssetAtPath<Material>(path);
                if(suit==null){suit=new Material(Resources.Load<Shader>("MarsPathSurface"));AssetDatabase.CreateAsset(suit,path);}
                suit.mainTexture=AssetDatabase.LoadAssetAtPath<Texture2D>($"Assets/External/NASA/Astronaut_Diffuse_{materialIndex+1}.png");
                suit.SetColor("_Color",new Color(0.92f,0.94f,0.95f));suit.SetFloat("_RegionalBlend",0);suit.SetFloat("_DetailMode",0);suit.SetFloat("_Smoothness",0.12f);EditorUtility.SetDirty(suit);suitMaterials[materialIndex]=suit;
            }
            foreach(Renderer renderer in renderers)
            {
                Material[] assigned=renderer.sharedMaterials;
                for(int i=0;i<assigned.Length;i++)assigned[i]=suitMaterials[assigned[i]!=null&&assigned[i].name.Contains("_2")?1:0];
                renderer.sharedMaterials=assigned;
            }
            if (renderers.Length > 0)
            {
                Bounds bounds = renderers[0].bounds;
                for (int i = 1; i < renderers.Length; i++) bounds.Encapsulate(renderers[i].bounds);
                float scale = bounds.size.y > 0.001f ? 2.35f / bounds.size.y : 1f;
                visual.transform.localScale = Vector3.one * scale;
                visual.transform.localPosition = new Vector3(-bounds.center.x * scale, -bounds.min.y * scale - 0.78f, -bounds.center.z * scale);
                visual.transform.localRotation = Quaternion.identity;
            }
            root.AddComponent<MarsArticulatedAstronaut>();
            PrefabUtility.SaveAsPrefabAsset(root, NasaAstronautPrefabPath);
            Object.DestroyImmediate(root);
        }

        public static void BuildWindowsPrototype()
        {
            PrepareDemoProject();
            var options = new BuildPlayerOptions
            {
                scenes = new[] { ScenePath },
                locationPathName = "Builds/Windows/MarsPath.exe",
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.None
            };

            BuildReport report = BuildPipeline.BuildPlayer(options);
            if (report.summary.result != BuildResult.Succeeded)
                throw new System.Exception($"MarsPath Windows build failed: {report.summary.result}");

            Debug.Log($"MARSPATH_BUILD_COMPLETE: {report.summary.outputPath} ({report.summary.totalSize} bytes)");
        }
    }
}

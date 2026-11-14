using System;
using System.Collections;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;

namespace MarsPath
{
    /// <summary>Opt-in desktop acceptance against independently checked native
    /// HiRISE pixels. Ordinary application startup never runs this diagnostic.</summary>
    public sealed class MarsHiRiseNativeReview : MonoBehaviour
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            foreach (string argument in Environment.GetCommandLineArgs())
                if (argument == "--mars-hirise-native-review")
                { new GameObject("Native HiRISE desktop acceptance").AddComponent<MarsHiRiseNativeReview>(); break; }
        }
        private IEnumerator Start()
        {
            StartCoroutine(Deadline());
            yield return null;
            MarsPathDemoBootstrap demo = FindAnyObjectByType<MarsPathDemoBootstrap>();
            if (demo == null) { Debug.LogError("HIRISE_NATIVE_REVIEW_FAILED: missing application bootstrap"); Application.Quit(2); yield break; }
            yield return demo.ReviewHiRiseNative();
        }
        private IEnumerator Deadline()
        { yield return new WaitForSecondsRealtime(120); Debug.LogError("HIRISE_NATIVE_REVIEW_FAILED: 120-second diagnostic deadline"); Application.Quit(2); }
    }

    public partial class MarsPathDemoBootstrap
    {
        private bool hiRiseNativePassed;
        private StringBuilder hiRiseNativeReport;
        private string hiRiseNativeFolder;

        public IEnumerator ReviewHiRiseNative()
        {
            RefinementEnabled = false;
            MarsTerrainCachePolicy.ReadPersistentCache = false;
            hiRiseNativePassed = true;
            hiRiseNativeReport = new StringBuilder("Native HiRISE desktop acceptance\nOriginal measured DTM / matching RED_A image / physical height scale 1:1\n");
            hiRiseNativeFolder = Path.Combine(Application.persistentDataPath, "HiRiseNativeReview");
            Directory.CreateDirectory(hiRiseNativeFolder);
            GeoPoint start = NativeReviewPoint(18.4620552f, 77.42473f, "Measured scarp approach");
            GeoPoint end = NativeReviewPoint(18.4640789f, 77.42473f, "Measured traverse destination");
            GeoPoint low = NativeReviewPoint(18.46205517f, 77.42472712f, "Measured scarp bottom");
            GeoPoint high = NativeReviewPoint(18.46402740f, 77.42277203f, "Measured scarp upper edge");

            ReturnToGlobalMap(); GlobalView.SetEndpoints(start, end);
            float began = Time.realtimeSinceStartup;
            yield return BuildSelectedRegion(start, end);
            NativeReviewCheck(mission.routes[selectedRoute].available && cameraRig.RoutesInsideViewport(mission), "Original ridge endpoints produce an available, framed route");
            NativeReviewCheck(MarsRegionalTerrain.DisplayReliefScale == 1, "Planning vertical scale is physical 1:1");
            NativeReviewCheck(MarsOfficialTerrain.ActivateCachedImagery(start), "Native matching photograph activates from bundled assets without network");
            terrainRoot.GetComponent<MarsTerrainGenerator>().RefreshSourceImagery();
            NativeReviewImageCheck(start, end, low, high, terrainRoot);
            float lowHeight = MarsRegionalTerrain.MeasuredElevation(low), highHeight = MarsRegionalTerrain.MeasuredElevation(high);
            NativeReviewCheck(Mathf.Abs(lowHeight - (-2541.2722f)) <= 2.5f, "Measured scarp bottom agrees with original DTM: " + lowHeight.ToString("F3", CultureInfo.InvariantCulture) + " m");
            NativeReviewCheck(Mathf.Abs(highHeight - (-2474.7068f)) <= 2.5f, "Measured scarp upper edge agrees with original DTM: " + highHeight.ToString("F3", CultureInfo.InvariantCulture) + " m");
            NativeReviewCheck(Mathf.Abs((highHeight - lowHeight) - 66.5654f) <= 2.5f, "Native scarp rise is 66.57 m within float-coordinate/interpolation tolerance");
            foreach (GeoPoint point in new[] { start, end, low, high }) NativeReviewPlannerHeight(point);
            NativeReviewCheck(MarsOfficialTerrain.NativeReviewOpaqueImagePixelsStayValid(), "Genuine dark and bright opaque image pixels retain coverage; luminance is not nodata");
            Debug.Log("HIRISE_NATIVE_REVIEW_PLANNING_SECONDS: " + (Time.realtimeSinceStartup - began).ToString("F3", CultureInfo.InvariantCulture));
            cameraRig.ResetPlanningView();
            yield return NativeReviewCapture("MarsPathHiRiseNativePlanner.png");

            // Verify entry using the actual originally selected ridge mission.
            demoPresentation = false;
            yield return PrepareHelmetEntry(false); paused = true;
            NativeReviewCheck(MarsGeospatial.AngularDistanceDegrees(JourneyLocation, start) < .00002f, "Helmet entry preserves the selected ridge start");
            NativeReviewCheck(MarsRegionalTerrain.HumanScale && Mathf.Abs(MarsRegionalTerrain.MetersPerUnit - 1) < .001f, "Helmet geometry uses one metre per scene unit");
            NativeReviewImageCheck(start, end, low, high, surfaceRoot);
            NativeReviewCheck(OfficialMeshMatches(surfaceRoot), "Initial ridge surface vertices match the measured provider");
            cameraRig.SetHelmetView(true); cameraRig.SnapToMission(astronaut.transform);
            yield return NativeReviewCapture("MarsPathHiRiseNativeRidgeVisor.png");

            // Look at the independently measured upper scarp from the same
            // accessible route start. Do not route directly up a blocked cliff
            // or weaken the production 25-degree slope limit for a screenshot.
            NativeReviewCheck(mission.routes[selectedRoute].available, "The scarp approach respects the production slope limit");
            NativeReviewCheck(MarsGeospatial.AngularDistanceDegrees(JourneyLocation, low) < .00002f, "Scarp camera is located at the original measured bottom point");
            NativeReviewCheck(MarsRegionalTerrain.HumanScale && Mathf.Abs(MarsRegionalTerrain.MetersPerUnit - 1) < .001f && MarsRegionalTerrain.DisplayReliefScale == 1, "Scarp remains physically 1:1 in human-scale coordinates");
            NativeReviewImageCheck(start, end, low, high, surfaceRoot);
            Physics.SyncTransforms();
            float renderedLow = NativeReviewGroundHeight(low), renderedHigh = NativeReviewGroundHeight(high);
            NativeReviewCheck(!float.IsNaN(renderedLow) && Mathf.Abs(renderedLow - lowHeight) <= 2.5f, "Rendered scarp bottom triangle agrees with measured height: " + renderedLow.ToString("F3", CultureInfo.InvariantCulture) + " m");
            NativeReviewCheck(!float.IsNaN(renderedHigh) && Mathf.Abs(renderedHigh - highHeight) <= 2.5f, "Rendered scarp upper triangle agrees with measured height: " + renderedHigh.ToString("F3", CultureInfo.InvariantCulture) + " m");
            NativeReviewCheck(!float.IsNaN(renderedHigh) && !float.IsNaN(renderedLow) && Mathf.Abs((renderedHigh - renderedLow) - 66.5654f) <= 2.5f, "Visible scarp mesh retains the original approximately 66.57 m rise");
            currentHeadingDegrees = 318;
            astronaut.transform.rotation = Quaternion.Euler(0, 318, 0);
            cameraRig.SetHelmetView(true); cameraRig.SnapToMission(astronaut.transform);
            float eye = Camera.main.transform.position.y - MarsTerrainGenerator.SampleHeight(astronaut.transform.position.x, astronaut.transform.position.z);
            NativeReviewCheck(Mathf.Abs(eye - 1.70f) <= .04f, "Scarp is viewed from a real 1.70 m eye height");
            Vector2 target = MarsRegionalTerrain.ToWorld(high);
            Vector3 ray = new Vector3(target.x, MarsTerrainGenerator.SampleHeight(target.x, target.y), target.y) - Camera.main.transform.position;
            NativeReviewCheck(Vector3.Angle(Camera.main.transform.forward, ray) < 35, "Northwest 318-degree visor framing contains the measured upper edge");
            yield return NativeReviewCapture("MarsPathHiRiseNativeScarpVisor.png");
            cameraRig.SetHelmetView(false); cameraRig.SnapToMission(astronaut.transform);
            yield return NativeReviewCapture("MarsPathHiRiseNativeScarpChase.png");
            string result = hiRiseNativePassed ? "HIRISE_NATIVE_REVIEW_PASSED" : "HIRISE_NATIVE_REVIEW_FAILED";
            hiRiseNativeReport.AppendLine(result);
            File.WriteAllText(Path.Combine(hiRiseNativeFolder, "NativeHiRiseReview.txt"), hiRiseNativeReport.ToString());
            Debug.Log(result + ": original native RED_A image, measured scarp, visible mesh heights, and 1:1 desktop eye view. Captures: " + hiRiseNativeFolder);
            Application.Quit(hiRiseNativePassed ? 0 : 2);
        }

        private static GeoPoint NativeReviewPoint(float lat, float lon, string label)
        { return new GeoPoint { latitude = lat, longitude = lon, label = label }; }
        private void NativeReviewCheck(bool okay, string message)
        {
            hiRiseNativePassed &= okay;
            string line = (okay ? "PASS: " : "FAIL: ") + message;
            hiRiseNativeReport.AppendLine(line);
            if (okay) Debug.Log("HIRISE_NATIVE_REVIEW " + line); else Debug.LogError("HIRISE_NATIVE_REVIEW " + line);
        }
        private void NativeReviewImageCheck(GeoPoint a, GeoPoint b, GeoPoint low, GeoPoint high, GameObject root)
        {
            bool state = MarsOfficialTerrain.NativeReviewImageState(out string source, out float spacing, out int width, out int height);
            NativeReviewCheck(state && source.EndsWith("/ESP_045994_1985_RED_A_01_ORTHO.tif", StringComparison.Ordinal) && Mathf.Abs(spacing - .25413823f) < .00001f && width == 3072 && height == 3072,
                "Active matching image is original 3072-square RED_A at 0.254138 m grid: " + source + " / " + spacing.ToString("F8", CultureInfo.InvariantCulture));
            NativeReviewCheck(MarsRegionSourceCache.CoversImagery(a) && MarsRegionSourceCache.CoversImagery(b) && MarsRegionSourceCache.CoversImagery(low) && MarsRegionSourceCache.CoversImagery(high), "One registered photograph covers both ridge endpoints and the measured scarp");
            MarsTerrainGenerator generator = root == null ? null : root.GetComponentInChildren<MarsTerrainGenerator>();
            Material material = generator == null ? null : generator.GetComponent<Renderer>()?.sharedMaterial;
            NativeReviewCheck(material != null && material.HasProperty("_OrbitalTex") && material.GetTexture("_OrbitalTex") == MarsRegionSourceCache.OrbitalTexture && material.GetFloat("_OrbitalBlend") > .99f,
                "Visible terrain material binds the active matching photograph with real source coverage");
            NativeReviewCheck(material != null && material.GetFloat("_OrbitalPrecise") > .99f, "The material uses local high-precision image registration");
        }
        private void NativeReviewPlannerHeight(GeoPoint point)
        {
            Vector2 local = MarsRegionalTerrain.ToWorld(point);
            bool found = MarsAdaptivePlanningTerrain.TryRenderedHeight(local.x, local.y, out float drawn);
            float expected = MarsRegionalTerrain.MeasuredElevation(point);
            float actual = (drawn + 1.5f) * MarsRegionalTerrain.MetersPerUnit + MarsRegionalTerrain.MeasuredElevation(mission.regionCenter);
            NativeReviewCheck(found && Mathf.Abs(actual - expected) <= 2.5f && MarsRegionalTerrain.SourceSpacingMeters(point) < 1.02f,
                "Planner triangle agrees with native 1.016553 m DTM at " + point.label + ": measured " + expected.ToString("F3", CultureInfo.InvariantCulture) + " / rendered " + actual.ToString("F3", CultureInfo.InvariantCulture));
        }
        private float NativeReviewGroundHeight(GeoPoint point)
        {
            MarsTerrainGenerator generator = surfaceRoot.GetComponentInChildren<MarsTerrainGenerator>();
            MeshCollider collider = generator.GetComponent<MeshCollider>();
            Vector2 local = MarsRegionalTerrain.ToWorld(point);
            if (collider == null || !collider.Raycast(new Ray(new Vector3(local.x, 10000, local.y), Vector3.down), out RaycastHit hit, 20000)) return float.NaN;
            return (hit.point.y + 1.5f) * MarsRegionalTerrain.MetersPerUnit + MarsRegionalTerrain.MeasuredElevation(surfaceCenter);
        }
        private IEnumerator NativeReviewCapture(string name)
        {
            if (Application.isBatchMode) yield break;
            yield return new WaitForSecondsRealtime(.5f);
            yield return new WaitForEndOfFrame();
            string path = Path.Combine(hiRiseNativeFolder, name);
            ScreenCapture.CaptureScreenshot(path);
            yield return new WaitForSecondsRealtime(.5f);
            Debug.Log("HIRISE_NATIVE_REVIEW_CAPTURE: " + path);
        }
    }

    public static partial class MarsOfficialTerrain
    {
        internal static bool NativeReviewImageState(out string source, out float spacing, out int width, out int height)
        {
            source = ""; spacing = 0; width = 0; height = 0;
            if (activeImage == null || activeImage != MarsRegionSourceCache.OrbitalTexture) return false;
            foreach (ImageRecord record in images)
                if (record.key == activeImageKey)
                { source = record.source ?? ""; spacing = record.spacing; width = activeImage.width; height = activeImage.height; return true; }
            return false;
        }
        internal static bool NativeReviewOpaqueImagePixelsStayValid()
        {
            const int size = 128;
            foreach (byte brightness in new byte[] { 2, 253 })
            {
                var pixels = new Color32[size * size];
                for (int i = 0; i < pixels.Length; i++) pixels[i] = new Color32(brightness, brightness, brightness, 255);
                PrepareOfficialImagePixels(pixels, size, size);
                Color32 center = pixels[64 * size + 64];
                if (center.a != 255 || center.r != brightness) return false;
            }
            return ImageFootprintSelfCheck();
        }
    }
}

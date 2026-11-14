using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace MarsPath
{
    public partial class MarsPathDemoBootstrap
    {
        private IEnumerator ReviewQuestGlobeUi()
        {
            const int layer = 28;
            var root = new GameObject("Quest globe UI verification");
            var cameraObject = new GameObject("Quest globe verification camera");
            cameraObject.transform.SetParent(root.transform, false);
            Camera camera = cameraObject.AddComponent<Camera>();
            camera.enabled = true;
            camera.stereoTargetEye = StereoTargetEyeMask.None;
            camera.cullingMask = 1 << layer;
            camera.fieldOfView = 60;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Color.black;
            var render = new RenderTexture(512, 512, 24);
            var pixels = new Texture2D(512, 512, TextureFormat.RGBA32, false);
            camera.targetTexture = render;

            GameObject planet = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            planet.layer = layer;
            planet.transform.SetParent(root.transform, false);
            planet.transform.localScale = Vector3.one * 2;
            Destroy(planet.GetComponent<Collider>());
            var planetMaterial = new Material(Resources.Load<Shader>("MarsPathGlobe"));
            planetMaterial.SetTexture("_MainTex", Texture2D.whiteTexture);
            planetMaterial.SetFloat("_Grid", 0);
            planet.GetComponent<Renderer>().sharedMaterial = planetMaterial;
            var arc = new GameObject("Globe route arc");
            arc.transform.SetParent(root.transform, false);
            arc.AddComponent<LineRenderer>().positionCount = 0;

            var globe = cameraObject.AddComponent<MarsQuestGlobe>();
            globe.Initialize(null, null, camera, planet.transform, arc.transform);
            Transform navigation = planet.transform.parent;
            // Keep the globe behind the translucent cards in the reference image.
            navigation.localScale = Vector3.one * 16;
            navigation.localPosition = new Vector3(0, 0, 18.4f);
            Canvas.ForceUpdateCanvases();
            yield return null;

            Image[] cards = System.Array.FindAll(cameraObject.GetComponentsInChildren<Image>(), image =>
                image.name == "Jezero zone button" || image.name == "Jezero demo button" || image.name == "Plan route button");
            bool passed = cards.Length == 3;
            // Opaque samples isolate depth occlusion from the globe's lighting and alpha blending.
            foreach (Image card in cards)
            {
                Color color = card.color;
                color.a = 1;
                card.color = color;
            }
            var reference = new Color[cards.Length];
            CaptureQuestUi(camera, render, pixels);
            for (int i = 0; i < cards.Length; i++)
            {
                reference[i] = QuestCardPixel(camera, pixels, cards[i]);
                Debug.Log($"QUEST_GLOBE_UI_REFERENCE: {cards[i].name}, color {reference[i]}, shader {cards[i].material.shader.name}.");
                passed &= reference[i].maxColorComponent > .12f;
                passed &= cards[i].material.shader.name == "MarsPath/Quest UI Foreground";
            }

            float[] zooms = { 2, 16, 480 };
            for (int view = 0; view < zooms.Length; view++)
            {
                float radius = zooms[view];
                navigation.localScale = Vector3.one * radius;
                navigation.localPosition = new Vector3(0, 0, radius + .2f);
                camera.transform.localPosition = new Vector3((view - 1) * .15f, .05f, .08f);
                CaptureQuestUi(camera, render, pixels);
                foreach (Image card in cards)
                {
                    int index = System.Array.IndexOf(cards, card);
                    Color actual = QuestCardPixel(camera, pixels, card);
                    passed &= Mathf.Abs(actual.r - reference[index].r) < .08f &&
                        Mathf.Abs(actual.g - reference[index].g) < .08f &&
                        Mathf.Abs(actual.b - reference[index].b) < .08f;
                    Ray ray = new Ray(camera.transform.position, card.transform.position - camera.transform.position);
                    bool selectable = MarsQuestPanel.Intersects(card, ray, out float hitDistance) && hitDistance > 0;
                    passed &= selectable;
                    Debug.Log($"QUEST_GLOBE_UI_VIEW: {radius}x, {card.name}, color {actual}, selectable {selectable}.");
                }
            }

            camera.targetTexture = null;
            Destroy(root);
            render.Release();
            Destroy(render);
            Destroy(pixels);
            Destroy(planetMaterial);
            if (passed) Debug.Log("QUEST_GLOBE_UI_PASSED: all three buttons stay visible and selectable with an occluding globe at three zoom levels and head positions.");
            else Debug.LogError("QUEST_GLOBE_UI_FAILED: globe occlusion or button selection.");
            Application.Quit(passed ? 0 : 2);
        }

        private static void CaptureQuestUi(Camera camera, RenderTexture render, Texture2D pixels)
        {
            Canvas.ForceUpdateCanvases();
            camera.Render();
            RenderTexture previous = RenderTexture.active;
            RenderTexture.active = render;
            pixels.ReadPixels(new Rect(0, 0, render.width, render.height), 0, 0);
            pixels.Apply(false);
            RenderTexture.active = previous;
        }

        private static Color QuestCardPixel(Camera camera, Texture2D pixels, Image card)
        {
            Vector3 world = card.rectTransform.TransformPoint(new Vector3(0, -card.rectTransform.rect.height * .35f, 0));
            Vector3 point = camera.WorldToViewportPoint(world);
            return pixels.GetPixel(Mathf.Clamp((int)(point.x * pixels.width), 0, pixels.width - 1),
                Mathf.Clamp((int)(point.y * pixels.height), 0, pixels.height - 1));
        }
    }
}

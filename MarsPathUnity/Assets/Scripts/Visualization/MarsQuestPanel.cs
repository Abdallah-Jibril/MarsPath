using UnityEngine;
using UnityEngine.UI;

namespace MarsPath
{
    internal sealed class MarsQuestPanel
    {
        internal static readonly Color Background = new Color(.026f, .057f, .075f, .94f);
        internal static readonly Color Button = new Color(.053f, .125f, .153f, .97f);
        internal static readonly Color Hover = new Color(.106f, .265f, .31f, .99f);
        internal static readonly Color Muted = new Color(.74f, .84f, .88f);
        internal static readonly Color Cyan = new Color(.33f, .9f, 1f);
        internal static readonly Color Amber = new Color(1f, .67f, .35f);
        internal static readonly Color DarkInk = new Color(.025f, .052f, .062f);
        private static Sprite rounded;
        private static Material foregroundMaterial;

        private readonly Canvas canvas;
        private readonly Font font;
        private readonly bool foreground;

        public MarsQuestPanel(Camera camera, string name, int layer, bool foreground = false)
        {
            this.foreground = foreground;
            if (foreground && foregroundMaterial == null)
            {
                foregroundMaterial = new Material(Resources.Load<Shader>("MarsPathQuestUI"))
                {
                    name = "Quest globe foreground UI",
                    hideFlags = HideFlags.HideAndDontSave
                };
            }
            var root = new GameObject(name, typeof(RectTransform), typeof(Canvas)) { layer = layer };
            var rect = (RectTransform)root.transform;
            rect.SetParent(camera.transform, false);
            rect.localPosition = new Vector3(0, 0, 2f);
            rect.localScale = Vector3.one * .0015f;
            rect.sizeDelta = new Vector2(1400, 1280);

            canvas = root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.worldCamera = camera;
            font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        }

        public GameObject Root => canvas.gameObject;

        public Image Plate(string name, Rect layout, Color color)
        {
            var graphic = Add<Image>(name, layout);
            graphic.color = color;
            return graphic;
        }

        public Image Card(string name, Rect layout, Color color)
        {
            if (rounded == null)
            {
                const int size = 32, radius = 8;
                var texture = new Texture2D(size, size, TextureFormat.RGBA32, false) { name = "Quest card corners" };
                var pixels = new Color32[size * size];
                for (int y = 0; y < size; y++)
                    for (int x = 0; x < size; x++)
                    {
                        float dx = Mathf.Max(0f, Mathf.Abs(x + .5f - size / 2f) - (size / 2f - radius));
                        float dy = Mathf.Max(0f, Mathf.Abs(y + .5f - size / 2f) - (size / 2f - radius));
                        byte alpha = (byte)Mathf.RoundToInt(255f * Mathf.Clamp01(radius + .25f - Mathf.Sqrt(dx * dx + dy * dy)));
                        pixels[y * size + x] = new Color32(255, 255, 255, alpha);
                    }
                texture.SetPixels32(pixels);
                texture.Apply(false, true);
                rounded = Sprite.Create(texture, new Rect(0, 0, size, size), Vector2.one * .5f, 100f, 0,
                    SpriteMeshType.FullRect, new Vector4(radius, radius, radius, radius));
            }
            Image image = Plate(name, layout, color);
            image.sprite = rounded;
            image.type = Image.Type.Sliced;
            return image;
        }

        public RawImage Map(string name, Rect layout, Texture texture)
        {
            var image = Add<RawImage>(name, layout);
            image.texture = texture;
            return image;
        }

        public Text Label(string name, Rect layout, string value, int size, Color color, bool bold = false,
            TextAnchor alignment = TextAnchor.MiddleCenter)
        {
            var label = Add<Text>(name, layout);
            label.font = font;
            label.fontSize = size;
            label.fontStyle = bold ? FontStyle.Bold : FontStyle.Normal;
            label.alignment = alignment;
            label.color = color;
            label.text = value;
            return label;
        }

        private T Add<T>(string name, Rect layout) where T : Graphic
        {
            var element = new GameObject(name, typeof(RectTransform)) { layer = Root.layer };
            var rect = (RectTransform)element.transform;
            rect.SetParent(canvas.transform, false);
            rect.anchorMin = rect.anchorMax = rect.pivot = Vector2.one * .5f;
            rect.anchoredPosition = layout.position;
            rect.sizeDelta = layout.size;
            var graphic = element.AddComponent<T>();
            if (foreground) graphic.material = foregroundMaterial;
            graphic.raycastTarget = false;
            return graphic;
        }

        public static bool Intersects(Graphic target, Ray ray, out float distance)
            => Intersects(target, ray, out distance, out _);

        public static bool Intersects(Graphic target, Ray ray, out float distance, out Vector2 normalized)
        {
            RectTransform rect = target.rectTransform;
            var plane = new Plane(-rect.forward, rect.position);
            normalized = default;
            if (!plane.Raycast(ray, out distance)) return false;
            Vector2 point = rect.InverseTransformPoint(ray.GetPoint(distance));
            if (!rect.rect.Contains(point)) return false;
            normalized = Rect.PointToNormalized(rect.rect, point);
            return true;
        }
    }

    internal sealed class MarsQuestPointer
    {
        private readonly LineRenderer line;

        public MarsQuestPointer(Transform parent, int layer)
        {
            var rayObject = new GameObject("Quest right controller aim") { layer = layer };
            rayObject.transform.SetParent(parent, false);
            line = rayObject.AddComponent<LineRenderer>();
            line.useWorldSpace = true;
            line.widthMultiplier = .0035f;
            line.positionCount = 2;
            line.sharedMaterial = MarsPathMaterialFactory.CreateRoute(Color.white, "Quest controller aim");
            Hide();
        }

        public void Hide() => line.enabled = false;

        public void Show(Ray ray, float hitDistance, bool onAction)
        {
            line.enabled = true;
            line.startColor = new Color(.42f, .88f, 1f, .45f);
            line.endColor = onAction ? MarsQuestPanel.Amber : MarsQuestPanel.Cyan;
            line.SetPosition(0, ray.GetPoint(.035f));
            line.SetPosition(1, ray.GetPoint(hitDistance > .06f ? hitDistance - .016f : 1.25f));
        }
    }
}

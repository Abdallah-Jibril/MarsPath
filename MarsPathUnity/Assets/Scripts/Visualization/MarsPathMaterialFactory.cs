using UnityEngine;

namespace MarsPath
{
    public static class MarsPathMaterialFactory
    {
        public static Material Create(Color color, string name)
        {
            Shader shader = Resources.Load<Shader>("MarsPathSurface");
            if (shader == null) shader = Shader.Find("MarsPath/Surface");
            if (shader == null)
            {
                Debug.LogWarning("MarsPath surface shader is unavailable; rendering will use Unity defaults.");
                return null;
            }

            Material material = new Material(shader) { name = name };
            material.SetColor("_Color", color);
            return material;
        }

        public static Material CreateTextured(Texture texture, Color color, string name, Vector2 tiling)
        {
            Material material = Create(color, name);
            if (material == null) return null;
            material.mainTexture = texture;
            material.mainTextureScale = tiling;
            material.SetFloat("_RegionalBlend", 1f);
            return material;
        }

        public static Material CreateRock(Color baseColor, Color dustColor, float variation, string name)
        {
            Shader shader = Resources.Load<Shader>("MarsPathRock");
            if (shader == null) shader = Shader.Find("MarsPath/Rock");
            if (shader == null) return Create(baseColor, name);

            Material material = new Material(shader) { name = name, enableInstancing = true };
            material.SetColor("_BaseColor", baseColor);
            material.SetColor("_DustColor", dustColor);
            material.SetFloat("_Variation", variation);
            material.SetFloat("_Seed", 0f);
            material.SetTexture("_GrainTex",Resources.Load<Texture2D>("NASA/CuriositySandstone"));
            return material;
        }

        public static Material CreateRoute(Color color, string name)
        {
            Shader shader = Resources.Load<Shader>("MarsPathRoute");
            if (shader == null) shader = Shader.Find("MarsPath/Route Unlit");
            if (shader == null) return Create(color, name);
            Material material = new Material(shader) { name = name };
            material.SetColor("_Color", color);
            return material;
        }

        public static Material CreatePanorama(Texture texture)
        {
            Shader shader = Resources.Load<Shader>("MarsPathPanorama");
            if (shader == null) shader = Shader.Find("MarsPath/Panorama");
            if (shader == null) return null;
            Material material = new Material(shader) { name = "NASA Jezero Panorama Material", mainTexture = texture };
            material.SetColor("_Color", new Color(0.92f, 0.9f, 0.88f, 1f));
            return material;
        }
    }
}

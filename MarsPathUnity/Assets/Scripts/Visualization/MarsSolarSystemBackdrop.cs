using System.Collections.Generic;
using UnityEngine;

namespace MarsPath
{
    // Globe decorations use fixed world positions so Quest head rotation
    // reveals them naturally while Mars remains the navigation target.
    internal sealed class MarsSolarSystemBackdrop : MonoBehaviour
    {
        private readonly List<Material> materials = new List<Material>();
        private Mesh rings;

        internal void Populate(Mesh sphere, Shader planetShader, int layer, bool headset)
        {
            Vector3 jupiter = headset ? new Vector3(11, 3, 20) : new Vector3(20, 5, -7);
            Vector3 earth = headset ? new Vector3(-16, 2, -8) : new Vector3(-6, 6, 20);
            Vector3 saturn = headset ? new Vector3(2, 3, -23) : new Vector3(4, -1, -19);
            AddPlanet("Jupiter", jupiter, new Vector3(1.8f, 1.68f, 1.8f), new Vector3(6, 165, 8),
                new Color(.15f, .10f, .05f), sphere, planetShader, layer);
            AddPlanet("Earth", earth, Vector3.one * .92f, new Vector3(-3, -65, -12),
                new Color(.035f, .16f, .31f), sphere, planetShader, layer);
            Transform ringedPlanet = AddPlanet("Saturn", saturn, new Vector3(1.3f, 1.18f, 1.3f),
                new Vector3(23, 38, -19), new Color(.17f, .12f, .06f), sphere, planetShader, layer);
            if (ringedPlanet != null) AddRings(ringedPlanet, layer);
        }

        private Transform AddPlanet(string title, Vector3 position, Vector3 scale, Vector3 angles,
            Color limb, Mesh sphere, Shader shader, int layer)
        {
            Texture2D texture = Resources.Load<Texture2D>("NASA/SolarSystem/" + title);
            if (texture == null) return null;
            var planet = new GameObject(title) { layer = layer };
            planet.transform.SetParent(transform, false);
            planet.transform.localPosition = position;
            planet.transform.localScale = scale;
            planet.transform.localRotation = Quaternion.Euler(angles);
            planet.AddComponent<MeshFilter>().sharedMesh = sphere;
            var material = new Material(shader) { name = title + " globe", mainTexture = texture };
            material.SetFloat("_Grid", 0);
            material.SetVector("_LimbColor", new Vector4(limb.r, limb.g, limb.b, 1));
            materials.Add(material);
            planet.AddComponent<MeshRenderer>().sharedMaterial = material;
            return planet.transform;
        }

        private void AddRings(Transform planet, int layer)
        {
            Shader shader = Resources.Load<Shader>("MarsSaturnRings");
            if (shader == null) return;
            var ringObject = new GameObject("Saturn rings") { layer = layer };
            ringObject.transform.SetParent(planet, false);
            const int segments = 96;
            var vertices = new Vector3[(segments + 1) * 2];
            var uv = new Vector2[vertices.Length];
            var triangles = new int[segments * 6];
            for (int i = 0; i <= segments; i++)
            {
                int j = i * 2;
                float angle = 2 * Mathf.PI * i / segments;
                var radial = new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle));
                vertices[j] = radial * 1.25f;
                vertices[j + 1] = radial * 2.21f;
                uv[j] = new Vector2(0, i / (float)segments);
                uv[j + 1] = new Vector2(1, i / (float)segments);
                if (i == segments) continue;
                int k = i * 6;
                triangles[k] = j;
                triangles[k + 1] = j + 1;
                triangles[k + 2] = j + 2;
                triangles[k + 3] = j + 2;
                triangles[k + 4] = j + 1;
                triangles[k + 5] = j + 3;
            }
            rings = new Mesh { name = "Saturn rings", vertices = vertices, uv = uv, triangles = triangles };
            ringObject.AddComponent<MeshFilter>().sharedMesh = rings;
            var material = new Material(shader);
            materials.Add(material);
            ringObject.AddComponent<MeshRenderer>().sharedMaterial = material;
        }

        private void OnDestroy()
        {
            foreach (Material material in materials) Destroy(material);
            if (rings != null) Destroy(rings);
        }
    }
}

using UnityEngine;

namespace MarsPath
{
    public class NasaAstronautMotion : MonoBehaviour
    {
        private Transform visual;
        private Vector3 basePosition;
        private Quaternion baseRotation;
        private float gaitClock;
        private bool walking;

        private void Awake()
        {
            if (transform.childCount == 0) return;
            visual = transform.GetChild(0);
            PoseStaticModelForEVA();
            basePosition = visual.localPosition;
            baseRotation = visual.localRotation;
        }

        private void PoseStaticModelForEVA()
        {
            // NASA's downloadable mesh is supplied in a presentation/T pose and
            // has no skeleton. Bend the arm vertices into a relaxed EVA stance so
            // the official suit model reads naturally while walking.
            foreach (MeshFilter filter in visual.GetComponentsInChildren<MeshFilter>())
            {
                if (filter.sharedMesh == null) continue;
                Mesh posed = Instantiate(filter.sharedMesh);
                posed.name = filter.sharedMesh.name + " — Relaxed EVA Pose";
                Vector3[] vertices = posed.vertices;
                Bounds bounds = posed.bounds;
                float shoulderX = bounds.extents.x * 0.29f;
                float shoulderY = bounds.center.y + bounds.extents.y * 0.24f;
                float blendWidth = bounds.extents.x * 0.12f;

                for (int i = 0; i < vertices.Length; i++)
                {
                    Vector3 vertex = vertices[i];
                    float side = Mathf.Sign(vertex.x);
                    float beyondShoulder = Mathf.Abs(vertex.x) - shoulderX;
                    float lowerArmLimit = bounds.center.y + bounds.extents.y * 0.18f;
                    if (beyondShoulder <= 0f || vertex.y < lowerArmLimit) continue;
                    float blend = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(beyondShoulder / blendWidth));
                    Vector2 pivot = new Vector2(side * shoulderX, shoulderY);
                    Vector2 offset = new Vector2(vertex.x, vertex.y) - pivot;
                    float radians = -side * 34f * Mathf.Deg2Rad;
                    Vector2 rotated = new Vector2(
                        offset.x * Mathf.Cos(radians) - offset.y * Mathf.Sin(radians),
                        offset.x * Mathf.Sin(radians) + offset.y * Mathf.Cos(radians));
                    Vector2 posedXY = Vector2.Lerp(new Vector2(vertex.x, vertex.y), pivot + rotated, blend);
                    vertices[i] = new Vector3(posedXY.x, posedXY.y, vertex.z);
                }

                posed.vertices = vertices;
                posed.RecalculateNormals();
                posed.RecalculateBounds();
                filter.sharedMesh = posed;
            }
        }

        public void SetWalking(bool value)
        {
            walking = value;
        }

        private void Update()
        {
            if (visual == null) return;
            gaitClock += Time.deltaTime * (walking ? 5.4f : 1.1f);
            float step = Mathf.Sin(gaitClock);
            float bob = walking ? Mathf.Abs(step) * 0.055f : Mathf.Sin(gaitClock) * 0.008f;
            float lean = walking ? step * 2.2f : 0f;
            float pitch = walking ? Mathf.Cos(gaitClock * 2f) * 1.4f : 0f;
            visual.localPosition = Vector3.Lerp(visual.localPosition, basePosition + Vector3.up * bob, Time.deltaTime * 10f);
            visual.localRotation = Quaternion.Slerp(visual.localRotation, baseRotation * Quaternion.Euler(pitch, 0f, lean), Time.deltaTime * 9f);
        }
    }
}

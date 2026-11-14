using UnityEngine;

namespace MarsPath
{
    public class MarsAstronautVisual : MonoBehaviour
    {
        private Transform leftArm;
        private Transform rightArm;
        private Transform leftLeg;
        private Transform rightLeg;
        private bool walking;
        private float walkClock;

        public void Build()
        {
            Material suit = MarsPathMaterialFactory.Create(new Color(0.9f, 0.92f, 0.88f), "Astronaut Suit");
            Material joint = MarsPathMaterialFactory.Create(new Color(0.2f, 0.23f, 0.24f), "Astronaut Joints");
            Material visor = MarsPathMaterialFactory.Create(new Color(0.12f, 0.055f, 0.015f), "Gold Visor");
            Material lifeSupport = MarsPathMaterialFactory.Create(new Color(0.72f, 0.76f, 0.73f), "Life Support Pack");

            CreatePart("Torso", PrimitiveType.Capsule, new Vector3(0f, 1.15f, 0f), new Vector3(0.64f, 0.64f, 0.46f), Vector3.zero, suit);
            CreatePart("Chest Control Unit", PrimitiveType.Cube, new Vector3(0f, 1.2f, 0.43f), new Vector3(0.72f, 0.42f, 0.16f), Vector3.zero, joint);
            CreatePart("Life Support Backpack", PrimitiveType.Cube, new Vector3(0f, 1.25f, -0.48f), new Vector3(0.82f, 1.05f, 0.34f), Vector3.zero, lifeSupport);

            CreatePart("Helmet", PrimitiveType.Sphere, new Vector3(0f, 2.18f, 0f), new Vector3(0.82f, 0.86f, 0.82f), Vector3.zero, suit);
            CreatePart("Visor", PrimitiveType.Sphere, new Vector3(0f, 2.19f, 0.36f), new Vector3(0.64f, 0.55f, 0.24f), Vector3.zero, visor);
            CreatePart("Helmet Collar", PrimitiveType.Cylinder, new Vector3(0f, 1.78f, 0f), new Vector3(0.68f, 0.12f, 0.68f), Vector3.zero, joint);

            leftArm = CreatePart("Left Arm", PrimitiveType.Capsule, new Vector3(-0.68f, 1.18f, 0f), new Vector3(0.24f, 0.58f, 0.24f), new Vector3(0f, 0f, -8f), suit).transform;
            rightArm = CreatePart("Right Arm", PrimitiveType.Capsule, new Vector3(0.68f, 1.18f, 0f), new Vector3(0.24f, 0.58f, 0.24f), new Vector3(0f, 0f, 8f), suit).transform;
            CreatePart("Left Glove", PrimitiveType.Sphere, new Vector3(-0.77f, 0.6f, 0.04f), Vector3.one * 0.28f, Vector3.zero, joint);
            CreatePart("Right Glove", PrimitiveType.Sphere, new Vector3(0.77f, 0.6f, 0.04f), Vector3.one * 0.28f, Vector3.zero, joint);

            CreatePart("Hip", PrimitiveType.Cube, new Vector3(0f, 0.43f, 0f), new Vector3(0.72f, 0.35f, 0.48f), Vector3.zero, joint);
            leftLeg = CreatePart("Left Leg", PrimitiveType.Capsule, new Vector3(-0.28f, -0.12f, 0f), new Vector3(0.29f, 0.65f, 0.29f), Vector3.zero, suit).transform;
            rightLeg = CreatePart("Right Leg", PrimitiveType.Capsule, new Vector3(0.28f, -0.12f, 0f), new Vector3(0.29f, 0.65f, 0.29f), Vector3.zero, suit).transform;
            CreatePart("Left Boot", PrimitiveType.Cube, new Vector3(-0.28f, -0.78f, 0.15f), new Vector3(0.42f, 0.28f, 0.62f), Vector3.zero, joint);
            CreatePart("Right Boot", PrimitiveType.Cube, new Vector3(0.28f, -0.78f, 0.15f), new Vector3(0.42f, 0.28f, 0.62f), Vector3.zero, joint);

            transform.localScale = Vector3.one * 0.82f;
        }

        public void SetWalking(bool value)
        {
            walking = value;
        }

        private void Update()
        {
            if (leftArm == null) return;
            walkClock += Time.deltaTime * (walking ? 5f : 1.5f);
            float amount = walking ? Mathf.Sin(walkClock) * 20f : Mathf.Sin(walkClock) * 2f;
            leftArm.localRotation = Quaternion.Euler(amount, 0f, -8f);
            rightArm.localRotation = Quaternion.Euler(-amount, 0f, 8f);
            leftLeg.localRotation = Quaternion.Euler(-amount * 0.55f, 0f, 0f);
            rightLeg.localRotation = Quaternion.Euler(amount * 0.55f, 0f, 0f);
        }

        private GameObject CreatePart(string partName, PrimitiveType type, Vector3 position, Vector3 scale, Vector3 rotation, Material material)
        {
            GameObject part = GameObject.CreatePrimitive(type);
            part.name = partName;
            part.transform.SetParent(transform, false);
            part.transform.localPosition = position;
            part.transform.localScale = scale;
            part.transform.localEulerAngles = rotation;
            if (material != null) part.GetComponent<Renderer>().sharedMaterial = material;
            Collider collider = part.GetComponent<Collider>();
            if (collider != null) Destroy(collider);
            return part;
        }
    }
}

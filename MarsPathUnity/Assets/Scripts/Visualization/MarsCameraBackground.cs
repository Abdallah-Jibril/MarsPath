using UnityEngine;

namespace MarsPath
{
    /// <summary>Per-camera sky materials keep orbital space separate from EVA atmosphere.</summary>
    public static class MarsCameraBackground
    {
        private static Material space,atmosphere;

        public static void Apply(Camera camera,bool orbital)
        {
            if(camera==null)return;
            Material material=orbital?Space:Atmosphere;
            Skybox sky=camera.GetComponent<Skybox>();
            if(sky==null)sky=camera.gameObject.AddComponent<Skybox>();
            sky.material=material;sky.enabled=material!=null;
            camera.clearFlags=material!=null?CameraClearFlags.Skybox:CameraClearFlags.SolidColor;
            camera.backgroundColor=orbital?new Color(0.004f,0.007f,0.014f):new Color(0.58f,0.46f,0.39f);
        }

        private static Material Space
        {
            get
            {
                if(space==null)
                {
                    Shader shader=Resources.Load<Shader>("MarsPathStars");
                    if(shader!=null)space=new Material(shader){name="MarsPath / Orbital star field"};
                }
                return space;
            }
        }
        private static Material Atmosphere
        {
            get
            {
                if(atmosphere==null)
                {
                    Shader shader=Resources.Load<Shader>("MarsPathSky");
                    if(shader!=null)atmosphere=new Material(shader){name="MarsPath / Surface dust atmosphere"};
                }
                return atmosphere;
            }
        }
    }
}

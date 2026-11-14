using System.Collections.Generic;
using UnityEngine;

namespace MarsPath
{
    /// <summary>Automatically weights the NASA suit to a demonstration skeleton.
    /// Independent leg IK and arm swing replace whole-mesh lateral rocking.</summary>
    public class MarsArticulatedAstronaut : MonoBehaviour
    {
        private Transform[] bones;
        private Vector3 pelvisRest,lastPosition;
        private float floorY,height,upperLength,lowerLength,ankleY,clock,walkBlend;
        private bool walking;
        private readonly List<Mesh> meshes=new List<Mesh>();
        public bool HasArticulatedRig=>bones!=null&&bones.Length==13;
        public int RigidBackpackVertices { get; private set; }
        public void SetWalking(bool value){walking=value;}
        private void Awake()
        {
            NasaAstronautMotion legacy=GetComponent<NasaAstronautMotion>();if(legacy!=null)legacy.enabled=false;
            MeshFilter[] filters=GetComponentsInChildren<MeshFilter>();Bounds bounds=new Bounds();bool first=true;
            foreach(MeshFilter filter in filters)
            {
                if(filter.sharedMesh==null||!filter.sharedMesh.isReadable)continue;
                Matrix4x4 matrix=transform.worldToLocalMatrix*filter.transform.localToWorldMatrix;
                foreach(Vector3 v in filter.sharedMesh.vertices){Vector3 p=matrix.MultiplyPoint3x4(v);if(first){bounds=new Bounds(p,Vector3.zero);first=false;}else bounds.Encapsulate(p);}
            }
            if(first)return;
            floorY=bounds.min.y;height=bounds.size.y;ankleY=floorY+height*0.06f;
            bones=new Transform[13];float hipY=floorY+height*0.46f,kneeY=floorY+height*0.26f,shoulderY=floorY+height*0.77f;
            upperLength=hipY-kneeY;lowerLength=kneeY-ankleY;
            bones[0]=Bone("Pelvis",transform,new Vector3(0,hipY,0));pelvisRest=bones[0].localPosition;
            bones[1]=Bone("Spine",bones[0],new Vector3(0,height*0.19f,0));bones[2]=Bone("Helmet",bones[1],new Vector3(0,height*0.25f,0));
            for(int side=0;side<2;side++)
            {
                int leg=side==0?3:6;float sign=side==0?-1:1;
                bones[leg]=Bone(side==0?"Left Hip":"Right Hip",bones[0],new Vector3(sign*height*0.105f,0,0));
                bones[leg+1]=Bone("Knee",bones[leg],new Vector3(0,-upperLength,0));bones[leg+2]=Bone("Ankle",bones[leg+1],new Vector3(0,-lowerLength,0));
                int arm=side==0?9:11;
                bones[arm]=Bone(side==0?"Left Shoulder":"Right Shoulder",bones[1],new Vector3(sign*height*0.18f,shoulderY-(hipY+height*0.19f),0));
                bones[arm+1]=Bone("Elbow",bones[arm],new Vector3(sign*height*0.16f,0,0));
            }
            Matrix4x4[] binds=new Matrix4x4[bones.Length];for(int i=0;i<bones.Length;i++)binds[i]=bones[i].worldToLocalMatrix*transform.localToWorldMatrix;
            foreach(MeshFilter filter in filters)
            {
                Mesh source=filter.sharedMesh;MeshRenderer original=filter.GetComponent<MeshRenderer>();if(source==null||original==null||!source.isReadable)continue;
                Mesh mesh=Instantiate(source);mesh.name=source.name+" / Articulated EVA";meshes.Add(mesh);
                Matrix4x4 matrix=transform.worldToLocalMatrix*filter.transform.localToWorldMatrix,normalMatrix=matrix.inverse.transpose;
                Vector3[] vertices=mesh.vertices,normals=mesh.normals;BoneWeight[] weights=new BoneWeight[vertices.Length];
                for(int i=0;i<vertices.Length;i++){vertices[i]=matrix.MultiplyPoint3x4(vertices[i]);if(normals.Length==vertices.Length)normals[i]=normalMatrix.MultiplyVector(normals[i]).normalized;weights[i]=Weight(vertices[i]);}
                mesh.vertices=vertices;if(normals.Length==vertices.Length)mesh.normals=normals;mesh.boneWeights=weights;mesh.bindposes=binds;mesh.RecalculateBounds();
                GameObject skinObject=new GameObject("Articulated "+filter.name);skinObject.transform.SetParent(transform,false);
                SkinnedMeshRenderer skin=skinObject.AddComponent<SkinnedMeshRenderer>();skin.sharedMesh=mesh;skin.sharedMaterials=original.sharedMaterials;skin.bones=bones;skin.rootBone=bones[0];
                skin.localBounds=new Bounds(bounds.center,bounds.size+Vector3.one*1.6f);skin.quality=SkinQuality.Bone2;skin.updateWhenOffscreen=true;original.enabled=false;
            }
            lastPosition=transform.position;ApplyPose(0);
        }
        private static Transform Bone(string name,Transform parent,Vector3 position)
        {Transform bone=new GameObject(name).transform;bone.SetParent(parent,false);bone.localPosition=position;return bone;}
        private static BoneWeight Blend(int a,int b,float t)=>new BoneWeight{boneIndex0=a,boneIndex1=b,weight0=1-Mathf.Clamp01(t),weight1=Mathf.Clamp01(t)};
        private BoneWeight Weight(Vector3 vertex)
        {
            float y=(vertex.y-floorY)/height,x=Mathf.Abs(vertex.x)/height;bool left=vertex.x<0;
            // The life-support pack extends below the hips. Height-only skinning
            // assigned its lower corners to opposite thighs, stretching it like cloth.
            if(y>0.27f&&y<0.85f&&x<0.19f&&vertex.z < -height*0.035f)
            {RigidBackpackVertices++;return Blend(1,1,0);}
            if(y<0.47f)
            {
                int leg=left?3:6;if(y<0.14f)return Blend(leg+2,leg+1,Mathf.InverseLerp(0.08f,0.15f,y));
                if(y<0.34f)return Blend(leg+1,leg,Mathf.SmoothStep(0,1,Mathf.InverseLerp(0.23f,0.32f,y)));
                return Blend(leg,0,Mathf.SmoothStep(0,1,Mathf.InverseLerp(0.40f,0.47f,y)));
            }
            if(x>0.155f&&y>0.55f&&y<0.86f)
            {
                int arm=left?9:11;if(x<0.205f)return Blend(1,arm,Mathf.SmoothStep(0,1,Mathf.InverseLerp(0.155f,0.205f,x)));
                return Blend(arm,arm+1,Mathf.SmoothStep(0,1,Mathf.InverseLerp(0.31f,0.39f,x)));
            }
            if(y>0.84f)return Blend(1,2,Mathf.SmoothStep(0,1,Mathf.InverseLerp(0.84f,0.88f,y)));
            return Blend(0,1,Mathf.SmoothStep(0,1,Mathf.InverseLerp(0.46f,0.59f,y)));
        }
        private void LateUpdate()
        {
            if(!HasArticulatedRig)return;float traveled=Vector3.Distance(transform.position,lastPosition);lastPosition=transform.position;
            float speed=traveled<1.5f?traveled/Mathf.Max(Time.deltaTime,0.001f):0;
            walkBlend=Mathf.MoveTowards(walkBlend,walking?1:0,Time.deltaTime*3.5f);
            // One cycle advances 0.64 / 0.63 metres during stance. Drive phase
            // by actual travel, not a free-running clock that slides planted feet.
            if(walking&&traveled<1.5f)clock+=traveled/(0.64f/0.63f);
            ApplyPose(clock);
        }
        private void ApplyPose(float phase)
        {
            bones[0].localPosition=pelvisRest+Vector3.up*(-0.035f+Mathf.Sin(phase*Mathf.PI*4)*0.012f*walkBlend);
            bones[1].localRotation=Quaternion.Euler(3.5f*walkBlend,Mathf.Sin(phase*Mathf.PI*2)*1.1f*walkBlend,0);bones[2].localRotation=Quaternion.Euler(-2*walkBlend,0,0);
            for(int side=0;side<2;side++)
            {
                float cycle=Mathf.Repeat(phase+side*0.5f,1),stride=0.32f*walkBlend,z,lift;
                if(cycle<0.63f){z=Mathf.Lerp(stride,-stride,cycle/0.63f);lift=0;}
                else{float swing=(cycle-0.63f)/0.37f;z=Mathf.Lerp(-stride,stride,Mathf.SmoothStep(0,1,swing));lift=Mathf.Sin(swing*Mathf.PI)*0.13f*walkBlend;}
                int leg=side==0?3:6;float hipX=bones[leg].localPosition.x;
                Vector3 footWorld=transform.TransformPoint(new Vector3(hipX,ankleY,z));
                float groundDelta=MarsTerrainGenerator.SampleHeight(footWorld.x,footWorld.z)-(transform.position.y-0.76f);
                float dy=ankleY+lift+Mathf.Clamp(groundDelta,-0.16f,0.16f)-bones[0].localPosition.y;
                float length=Mathf.Clamp(Mathf.Sqrt(z*z+dy*dy),Mathf.Abs(upperLength-lowerLength)+0.01f,(upperLength+lowerLength)*0.995f);
                float angle=Mathf.Atan2(z,-dy),hipOffset=Mathf.Acos(Mathf.Clamp((upperLength*upperLength+length*length-lowerLength*lowerLength)/(2*upperLength*length),-1,1));
                float knee=Mathf.PI-Mathf.Acos(Mathf.Clamp((upperLength*upperLength+lowerLength*lowerLength-length*length)/(2*upperLength*lowerLength),-1,1));
                float hip=(-angle-hipOffset)*Mathf.Rad2Deg;bones[leg].localRotation=Quaternion.Euler(hip,0,0);bones[leg+1].localRotation=Quaternion.Euler(knee*Mathf.Rad2Deg,0,0);
                bones[leg+2].localRotation=Quaternion.Euler(-hip-knee*Mathf.Rad2Deg+Mathf.Sin(cycle*Mathf.PI*2)*3*walkBlend,0,0);
                int arm=side==0?9:11;float sign=side==0?-1:1;
                bones[arm].localRotation=Quaternion.Euler(-Mathf.Sin((phase+side*0.5f)*Mathf.PI*2)*10*walkBlend,0,-sign*65);
                bones[arm+1].localRotation=Quaternion.Euler(0,sign*13,0);
            }
        }
        private void OnDestroy(){foreach(Mesh mesh in meshes)if(mesh!=null)Destroy(mesh);}
    }
}

using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace MarsPath
{
    /// <summary>Illustrative, globally anchored centimetre gravel. Individual
    /// positions are not NASA measurements. No colliders, DEM displacement or
    /// hazard obstacles; this is a bounded instanced foreground material layer.</summary>
    public sealed class MarsSurfaceGravel:MonoBehaviour
    {
        public const int MaximumInstances=3072;
        public const float FieldRadiusMetres=15;
        private const double CellMetres=.42,Radians=Math.PI/180;
        private const int Variants=3,BatchCapacity=1023;
        private struct Stone
        {public ulong id;public uint seed;public Vector2 local;public float diameter,distance;}
        private readonly List<Stone> candidates=new List<Stone>(6000);
        private readonly Matrix4x4[][] batches=new Matrix4x4[Variants*2][];
        private readonly int[] counts=new int[Variants*2];
        private readonly int[] variantCounts=new int[Variants];
        private readonly Mesh[] meshes=new Mesh[Variants];
        private readonly Material[] materials=new Material[Variants];
        private Vector3[] ground;
        private int resolution,total;
        private float size,metresPerUnit;
        private double zoneWest,zoneSouth,zoneEastScale;
        private bool jezeroZone;
        private MissionData mission;
        private GeoPoint origin;
        private Vector2 lastFocus;
        private Transform actor;
        private Camera drawCamera;
        private bool built;
        public int InstanceCount=>total;
        public int DrawBatchCount{get{int n=0;foreach(int count in counts)if(count>0)n++;return n;}}

        public void Build(MissionData source,Vector3[] measuredVertices,int gridResolution,float gridSize)
        {
            if(!SystemInfo.supportsInstancing||!source.generatedRegion||!MarsRegionalTerrain.HumanScale)return;
            mission=source;origin=source.regionCenter;ground=measuredVertices;resolution=gridResolution;size=gridSize;
            metresPerUnit=MarsRegionalTerrain.MetersPerUnit;drawCamera=Camera.main;
            jezeroZone = MarsMissionZone.IsMission(source);
            if (jezeroZone)
            {
                zoneWest = MarsMissionZone.West * Radians;
                zoneSouth = MarsMissionZone.South * Radians;
                zoneEastScale = Math.Cos(zoneSouth) * MarsRegionalTerrain.RadiusMeters;
            }
            Shader shader=Resources.Load<Shader>("MarsPathIllustrativeRock");if(shader==null)return;
            Texture2D grain=MarsSurfaceRockScatter.LoadRockGrain(jezeroZone,out bool roverGrain);
            Color[] tones={new Color(.35f,.30f,.26f),new Color(.47f,.38f,.30f),new Color(.54f,.46f,.37f)};
            for(int i=0;i<Variants;i++)
            {
                meshes[i]=CreateMesh(i);
                materials[i]=new Material(shader){name="Illustrative instanced centimetre gravel "+i,enableInstancing=true};
                materials[i].SetColor("_BaseColor",tones[i]);materials[i].SetColor("_DustColor",new Color(.66f,.49f,.36f));
                materials[i].SetFloat("_Variation",.62f);materials[i].SetTexture("_GrainTex",grain);
                materials[i].SetFloat("_MicroRelief", 0f);
                if(roverGrain)
                {
                    materials[i].SetFloat("_RoverGrain",1f);
                    materials[i].SetFloat("_Bedding",.12f);
                }
                batches[i*2]=new Matrix4x4[BatchCapacity];batches[i*2+1]=new Matrix4x4[BatchCapacity];
            }
            built=true;Refresh(Vector2.zero);
        }
        private void LateUpdate()
        {
            if(!built||!ReferenceEquals(mission,MarsRegionalTerrain.Current))return;
            if(actor==null)
            {
                MarsArticulatedAstronaut articulated=FindAnyObjectByType<MarsArticulatedAstronaut>();
                if(articulated!=null)actor=articulated.transform;
                else{MarsAstronautVisual astronaut=FindAnyObjectByType<MarsAstronautVisual>();if(astronaut!=null)actor=astronaut.transform;}
            }
            Vector3 position=actor!=null?transform.InverseTransformPoint(actor.position):Vector3.zero;
            Vector2 focus=new Vector2(position.x,position.z);
            if((focus-lastFocus).sqrMagnitude*metresPerUnit*metresPerUnit>6.25f)Refresh(focus);
            for(int i=0;i<batches.Length;i++)if(counts[i]>0)
                Graphics.DrawMeshInstanced(meshes[i/2],0,materials[i/2],batches[i],counts[i],null,ShadowCastingMode.Off,true,gameObject.layer,drawCamera,LightProbeUsage.Off);
        }
        private void Refresh(Vector2 focus)
        {
            lastFocus=focus;candidates.Clear();Array.Clear(counts,0,counts.Length);Array.Clear(variantCounts,0,variantCounts.Length);total=0;
            Coordinates(focus,out double latitude,out double longitude);
            double north=(latitude+Math.PI*.5)*MarsRegionalTerrain.RadiusMeters;
            int first=Math.Max(0,(int)Math.Floor((north-FieldRadiusMetres-CellMetres)/CellMetres));
            int last=Math.Min((int)(Math.PI*MarsRegionalTerrain.RadiusMeters/CellMetres),(int)Math.Floor((north+FieldRadiusMetres+CellMetres)/CellMetres));
            for(int row=first;row<=last;row++)
            {
                double rowLat=Math.Min(Math.PI*.5,(row+.5)*CellMetres/MarsRegionalTerrain.RadiusMeters-Math.PI*.5);
                int columns=Math.Max(1,(int)Math.Round(2*Math.PI*MarsRegionalTerrain.RadiusMeters*Math.Cos(rowLat)/CellMetres));
                int centre=(int)Math.Floor((longitude+Math.PI)/(2*Math.PI)*columns);
                int half=Math.Min(columns/2+1,(int)Math.Ceiling((FieldRadiusMetres+CellMetres*2)/Math.Max(.01,Math.Cos(rowLat)*MarsRegionalTerrain.RadiusMeters)/(2*Math.PI)*columns)+2);
                int visits=Math.Min(columns,half*2+1);
                for(int j=0;j<visits;j++)
                {
                    int column=Mod(centre-half+j,columns);uint seed=Hash(unchecked((uint)row*73856093u^(uint)column*19349663u));
                    double lat=Math.Max(-Math.PI*.5,Math.Min(Math.PI*.5,((row+Unit(seed^0x135ab6cdu))*CellMetres/MarsRegionalTerrain.RadiusMeters)-Math.PI*.5));
                    double lon=((column+Unit(seed^0x3ca052b1u))/(double)columns)*(2*Math.PI)-Math.PI;
                    float density = .80f, patch = .5f;
                    if (jezeroZone)
                    {
                        // Dust pockets and drifts interrupt the gravel layer at
                        // a few metres across. Its phase survives window rebases.
                        float east = (float)((lon - zoneWest) * zoneEastScale);
                        float zoneNorthing = (float)((lat - zoneSouth) * MarsRegionalTerrain.RadiusMeters);
                        float localGrain = Mathf.PerlinNoise(east * .19f + zoneNorthing * .07f, zoneNorthing * .16f - east * .06f);
                        patch = MarsSurfaceRockScatter.JezeroRockDeposit(lat, lon) * .66f + localGrain * .34f;
                        density = Mathf.Lerp(.12f, .94f, Mathf.InverseLerp(.25f, .72f, patch));
                    }
                    if (Unit(seed ^ 0x718c2d31u) > density) continue;
                    Vector2 local=ToLocal(lat,lon);float distance=(local-focus).sqrMagnitude*metresPerUnit*metresPerUnit;
                    if(distance>FieldRadiusMetres*FieldRadiusMetres)continue;
                    float diameter=Mathf.Lerp(.018f,jezeroZone?Mathf.Lerp(.10f,.19f,patch):.15f,Mathf.Pow(Unit(seed^0x53bcd129u),2.8f));
                    candidates.Add(new Stone{id=((ulong)(uint)row<<32)|(uint)column,seed=seed,local=local,diameter=diameter,distance=distance});
                }
            }
            candidates.Sort((a,b)=>{int d=a.distance.CompareTo(b.distance);return d!=0?d:a.id.CompareTo(b.id);});
            foreach(Stone stone in candidates)
            {
                if(total>=MaximumInstances)break;
                Vector3 p=GroundPoint(stone.local.x,stone.local.y,out Vector3 normal);
                if(normal.y<.5f)continue;
                int variant=(int)(stone.seed%Variants),slot=variant*2+variantCounts[variant]/BatchCapacity;
                if(slot>=variant*2+2)continue;
                float diameter=stone.diameter/metresPerUnit;
                Quaternion rotation=Quaternion.FromToRotation(Vector3.up,normal)*Quaternion.Euler(0,Unit(stone.seed^0x93b62e71u)*360,0);
                Vector3 scale=new Vector3(diameter,diameter*Mathf.Lerp(.75f,1.15f,Unit(stone.seed^0x19afde39u)),diameter*Mathf.Lerp(.8f,1.15f,Unit(stone.seed^0x149c35adu)));
                p-=normal*diameter*.035f;
                batches[slot][counts[slot]++]=transform.localToWorldMatrix*Matrix4x4.TRS(p,rotation,scale);
                variantCounts[variant]++;total++;
            }
        }
        private Vector3 GroundPoint(float x,float z,out Vector3 normal)
        {
            float gx=Mathf.Clamp((x/size+.5f)*(resolution-1),0,resolution-1.0001f),gz=Mathf.Clamp((z/size+.5f)*(resolution-1),0,resolution-1.0001f);
            int ix=(int)gx,iz=(int)gz,k=iz*resolution+ix;float u=gx-ix,v=gz-iz;
            Vector3 a=ground[k],b=ground[k+1],c=ground[k+resolution],d=ground[k+resolution+1];float height;
            if(u+v<=1){height=a.y+(b.y-a.y)*u+(c.y-a.y)*v;normal=Vector3.Cross(c-a,b-a).normalized;}
            else{height=d.y+(c.y-d.y)*(1-u)+(b.y-d.y)*(1-v);normal=Vector3.Cross(b-d,c-d).normalized;}
            return new Vector3(x,height,z);
        }
        private Vector2 ToLocal(double latitude,double longitude)
        {
            double a=origin.latitude*Radians,b=origin.longitude*Radians,dl=Wrap(longitude-b),s=Math.Sin((latitude-a)*.5),t=Math.Sin(dl*.5);
            double h=Math.Max(0,Math.Min(1,s*s+Math.Cos(a)*Math.Cos(latitude)*t*t));
            double distance=2*Math.Atan2(Math.Sqrt(h),Math.Sqrt(1-h))*MarsRegionalTerrain.RadiusMeters/metresPerUnit;
            double bearing=Math.Atan2(Math.Sin(dl)*Math.Cos(latitude),Math.Cos(a)*Math.Sin(latitude)-Math.Sin(a)*Math.Cos(latitude)*Math.Cos(dl));
            return new Vector2((float)(Math.Sin(bearing)*distance),(float)(Math.Cos(bearing)*distance));
        }
        private void Coordinates(Vector2 local,out double latitude,out double longitude)
        {
            double a=origin.latitude*Radians,b=origin.longitude*Radians,d=Math.Sqrt((double)local.x*local.x+(double)local.y*local.y)*metresPerUnit/MarsRegionalTerrain.RadiusMeters,c=Math.Atan2(local.x,local.y);
            latitude=Math.Asin(Math.Sin(a)*Math.Cos(d)+Math.Cos(a)*Math.Sin(d)*Math.Cos(c));
            longitude=Wrap(b+Math.Atan2(Math.Sin(c)*Math.Sin(d)*Math.Cos(a),Math.Cos(d)-Math.Sin(a)*Math.Sin(latitude)));
        }
        private static Mesh CreateMesh(int variant)
        {
            const int sides=8;var vertices=new List<Vector3>();var triangles=new List<int>();
            // Two angular rings and a fractured cap;32 triangles,96 vertices.
            Vector3[] bottom=new Vector3[sides],middle=new Vector3[sides];uint seed=Hash((uint)(variant+917));
            for(int i=0;i<sides;i++)
            {
                float angle=i*Mathf.PI*2/sides,radius=Mathf.Lerp(.40f,.52f,Unit(seed^(uint)i*0x73a1425du));
                bottom[i]=new Vector3(Mathf.Cos(angle)*radius*.77f,0,Mathf.Sin(angle)*radius*.77f);
                middle[i]=new Vector3(Mathf.Cos(angle)*radius,Mathf.Lerp(.09f,.19f,Unit(seed^(uint)i*0xf17ca319u)),Mathf.Sin(angle)*radius);
            }
            Vector3 cap=new Vector3(.05f*(variant-1),variant==0 ? .23f : variant==1 ? .34f : .29f,-.03f*variant);
            for(int i=0;i<sides;i++)
            {
                int j=(i+1)%sides;AddFace(bottom[i],middle[i],bottom[j]);AddFace(bottom[j],middle[i],middle[j]);AddFace(middle[i],cap,middle[j]);AddFace(bottom[j],Vector3.zero,bottom[i]);
            }
            Mesh mesh=new Mesh{name="Shared illustrative centimetre gravel "+variant};mesh.SetVertices(vertices);mesh.SetTriangles(triangles,0);mesh.RecalculateNormals();
            Vector3[] normals = mesh.normals;
            var tangents = new Vector4[normals.Length];
            for (int i = 0; i < tangents.Length; i++) tangents[i] = MarsSurfaceRockScatter.GrainTangent(normals[i]);
            mesh.tangents = tangents;
            Color[] colours=new Color[vertices.Count];for(int i=0;i<colours.Length;i++)colours[i]=Color.Lerp(new Color(.72f,.70f,.67f),Color.white,Mathf.Clamp01(vertices[i].y*3));mesh.colors=colours;mesh.RecalculateBounds();return mesh;
            void AddFace(Vector3 a,Vector3 b,Vector3 c){int k=vertices.Count;vertices.Add(a);vertices.Add(b);vertices.Add(c);triangles.Add(k);triangles.Add(k+1);triangles.Add(k+2);}
        }
        private static uint Hash(uint v){unchecked{v^=v>>16;v*=0x7feb352du;v^=v>>15;v*=0x846ca68bu;return v^(v>>16);}}
        private static float Unit(uint v)=>(Hash(v)&0x00ffffffu)/16777216f;
        private static int Mod(int a,int b){int n=a%b;return n<0?n+b:n;}
        private static double Wrap(double a){a=(a+Math.PI)%(Math.PI*2);if(a<0)a+=Math.PI*2;return a-Math.PI;}
        private void OnDestroy(){foreach(Mesh mesh in meshes)if(mesh!=null)Destroy(mesh);foreach(Material material in materials)if(material!=null)Destroy(material);}
    }
}

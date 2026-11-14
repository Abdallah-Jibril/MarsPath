using System;
using System.Collections.Generic;
using UnityEngine;

namespace MarsPath
{
    /// <summary>Bounded source-height geometry across the planning corridor.
    /// Shared edge vertices remove quadtree T-junctions. Every drawn vertex is
    /// sampled from the same physical elevation provider used by the visor.</summary>
    public sealed class MarsAdaptivePlanningTerrain : MonoBehaviour
    {
        private const int BaseCells=64, Grid=8192, MaximumLeaves=16000, MaximumSamples=98000;
        private sealed class Cell
        {
            public int x,z,size,depth;
            public float priority;
            public Cell[] children;
            public int center;
            public int[] boundary;
        }
        private static MarsAdaptivePlanningTerrain active;
        private readonly Dictionary<long,float> samples=new Dictionary<long,float>();
        private readonly List<Cell> queue=new List<Cell>();
        private Cell[] roots;
        private float span,metres;
        private bool measuredJezeroZone;
        private struct RouteSegment
        {public Vector2 a,d;public float lengthSquared;}
        private List<RouteSegment>[] corridor;
        private Vector3[] vertices;
        private int leafCount;
        public static int VertexCount=>Usable?active.vertices.Length:0;
        public static int SampleCount=>Usable?active.samples.Count:0;
        public static int LeafCount=>Usable?active.leafCount:0;
        private static bool Usable=>active!=null&&active.gameObject.activeInHierarchy&&active.vertices!=null&&MarsRegionalTerrain.Current!=null&&!MarsRegionalTerrain.HumanScale;

        public static bool Build(Transform parent,Mesh regular,Material material,MissionData mission)
        {
            if(mission.regionSpanKm>=1520||Mathf.RoundToInt(Mathf.Sqrt(regular.vertexCount))!=BaseCells+1)return false;
            var child=new GameObject("Adaptive measured planning corridor");child.transform.SetParent(parent,false);
            var instance=child.AddComponent<MarsAdaptivePlanningTerrain>();
            instance.Create(regular,material,mission);active=instance;return true;
        }
        private static long Key(int x,int z)=>((long)x<<32)|(uint)z;
        private float At(int x,int z)
        {
            long key=Key(x,z);
            if(!samples.TryGetValue(key,out float value))
            {value=MarsRegionalTerrain.SampleVisualHeight(World(x),World(z));samples.Add(key,value);}
            return value;
        }
        private float World(int p)=>((float)p/Grid-.5f)*span;
        private void Create(Mesh regular,Material material,MissionData mission)
        {
            Vector3[] source=regular.vertices;span=source[BaseCells].x-source[0].x;metres=MarsRegionalTerrain.MetersPerUnit;
            measuredJezeroZone=MarsMissionZone.IsMission(mission);
            for(int z=0;z<=BaseCells;z++)for(int x=0;x<=BaseCells;x++)samples.Add(Key(x*128,z*128),source[z*(BaseCells+1)+x].y);
            BuildCorridor(mission);
            roots=new Cell[BaseCells*BaseCells];leafCount=roots.Length;
            for(int z=0;z<BaseCells;z++)for(int x=0;x<BaseCells;x++)
            {
                var cell=new Cell{x=x*128,z=z*128,size=128};roots[z*BaseCells+x]=cell;
                Evaluate(cell);
            }
            int leafLimit = measuredJezeroZone ? MaximumLeaves + 2600 : MaximumLeaves;
            int sampleLimit = measuredJezeroZone ? MaximumSamples + 14000 : MaximumSamples;
            while(queue.Count>0&&leafCount+3<=leafLimit&&samples.Count+24<sampleLimit)
            {
                Cell cell=Pop();int half=cell.size/2;
                cell.children=new Cell[4];
                for(int i=0;i<4;i++)
                {
                    var next=new Cell{x=cell.x+(i%2)*half,z=cell.z+(i/2)*half,size=half,depth=cell.depth+1};
                    cell.children[i]=next;Evaluate(next);
                }
                leafCount+=3;
            }
            Assemble(material);
        }
        private void BuildCorridor(MissionData mission)
        {
            corridor=new List<RouteSegment>[BaseCells*BaseCells];
            float margin=400/metres;
            foreach(RouteData route in mission.routes)
            {
                if(!route.available||route.waypoints==null)continue;
                for(int i=1;i<route.waypoints.Length;i++)
                {
                    Vector2 a=new Vector2(route.waypoints[i-1].x,route.waypoints[i-1].z)*60,b=new Vector2(route.waypoints[i].x,route.waypoints[i].z)*60;
                    var segment=new RouteSegment{a=a,d=b-a,lengthSquared=(b-a).sqrMagnitude};
                    int minimumX=Mathf.Clamp(Mathf.FloorToInt(((Mathf.Min(a.x,b.x)-margin)/span+.5f)*BaseCells),0,BaseCells-1);
                    int maximumX=Mathf.Clamp(Mathf.FloorToInt(((Mathf.Max(a.x,b.x)+margin)/span+.5f)*BaseCells),0,BaseCells-1);
                    int minimumZ=Mathf.Clamp(Mathf.FloorToInt(((Mathf.Min(a.y,b.y)-margin)/span+.5f)*BaseCells),0,BaseCells-1);
                    int maximumZ=Mathf.Clamp(Mathf.FloorToInt(((Mathf.Max(a.y,b.y)+margin)/span+.5f)*BaseCells),0,BaseCells-1);
                    for(int z=minimumZ;z<=maximumZ;z++)for(int x=minimumX;x<=maximumX;x++)
                    {
                        int cell=z*BaseCells+x;
                        if(corridor[cell]==null)corridor[cell]=new List<RouteSegment>(8);
                        corridor[cell].Add(segment);
                    }
                }
            }
        }
        private bool NearRoute(Cell cell)
        {
            int h=cell.size/2,index=Mathf.Min(63,(cell.z+h)/128)*BaseCells+Mathf.Min(63,(cell.x+h)/128);
            List<RouteSegment> candidates=corridor[index];if(candidates==null)return false;
            Vector2 p=new Vector2(World(cell.x+h),World(cell.z+h));
            // Include the cell's own half diagonal so a route crossing only a
            // corner cannot disappear between coarse parent-center samples.
            float radius=400/metres+cell.size*span/Grid*.707107f,radiusSquared=radius*radius;
            foreach(RouteSegment segment in candidates)
            {
                float t=segment.lengthSquared>0?Mathf.Clamp01(Vector2.Dot(p-segment.a,segment.d)/segment.lengthSquared):0;
                if((p-segment.a-segment.d*t).sqrMagnitude<=radiusSquared)return true;
            }
            return false;
        }
        private void Evaluate(Cell cell)
        {
            int x=cell.x,z=cell.z,s=cell.size,h=s/2;
            float a=At(x,z),b=At(x+s,z),c=At(x,z+s),d=At(x+s,z+s);
            float center=At(x+h,z+h),west=At(x,z+h),east=At(x+s,z+h),south=At(x+h,z),north=At(x+h,z+s);
            // Preserve all original continuous-context boundary segments. Border
            // cells still have their true sampled center and shared inner edges.
            if(cell.depth>=6||x==0||z==0||x+s==Grid||z+s==Grid)return;
            float error=Mathf.Max(Mathf.Abs(center-(b+c)*.5f),Mathf.Abs(west-(a+c)*.5f),Mathf.Abs(east-(b+d)*.5f),Mathf.Abs(south-(a+b)*.5f),Mathf.Abs(north-(c+d)*.5f))*metres;
            bool near=NearRoute(cell);
            GeoPoint point=MarsRegionalTerrain.ToGeo(World(x+h),World(z+h));
            float source=MarsRegionalTerrain.SourceSpacingMeters(point);
            float width=s*span/Grid*metres;
            if(width<=source*1.5f)return;
            bool nativeJezero=measuredJezeroZone&&source<=1.1f;
            float tolerance=near?Mathf.Clamp(source*.05f,nativeJezero?.10f:.15f,6):Mathf.Clamp(source*.2f,nativeJezero?.8f:2f,30);
            // Complete mandatory route sampling before spending any remaining
            // budget on global curvature. A wide, center-only corridor allowed
            // distant cliffs to consume all leaves and left route cells coarse.
            float target=Mathf.Max(nativeJezero?38:150,source*4);
            cell.priority=near&&width>target?1000000+width/target:error/tolerance;
            if(cell.priority>1)Push(cell);
        }
        private void Push(Cell cell)
        {
            int i=queue.Count;queue.Add(cell);
            while(i>0){int parent=(i-1)/2;if(queue[parent].priority>=cell.priority)break;queue[i]=queue[parent];i=parent;}queue[i]=cell;
        }
        private Cell Pop()
        {
            Cell result=queue[0],last=queue[queue.Count-1];queue.RemoveAt(queue.Count-1);if(queue.Count==0)return result;
            int i=0;while(i*2+1<queue.Count){int child=i*2+1;if(child+1<queue.Count&&queue[child+1].priority>queue[child].priority)child++;if(queue[child].priority<=last.priority)break;queue[i]=queue[child];i=child;}queue[i]=last;return result;
        }
        private static void Leaves(Cell cell,List<Cell> result)
        {if(cell.children==null)result.Add(cell);else foreach(Cell child in cell.children)Leaves(child,result);}
        private static void AddEdgePoint(Dictionary<int,HashSet<int>> edges,int row,int value)
        {if(!edges.TryGetValue(row,out HashSet<int> points)){points=new HashSet<int>();edges.Add(row,points);}points.Add(value);}
        private void Assemble(Material material)
        {
            var leaves=new List<Cell>(leafCount);foreach(Cell root in roots)Leaves(root,leaves);
            var horizontal=new Dictionary<int,HashSet<int>>();var vertical=new Dictionary<int,HashSet<int>>();
            foreach(Cell leaf in leaves)
            {
                foreach(int x in new[]{leaf.x,leaf.x+leaf.size})foreach(int z in new[]{leaf.z,leaf.z+leaf.size})
                {AddEdgePoint(horizontal,z,x);AddEdgePoint(vertical,x,z);}
            }
            var rows=new Dictionary<int,List<int>>();var columns=new Dictionary<int,List<int>>();
            foreach(var pair in horizontal){var sorted=new List<int>(pair.Value);sorted.Sort();rows.Add(pair.Key,sorted);}
            foreach(var pair in vertical){var sorted=new List<int>(pair.Value);sorted.Sort();columns.Add(pair.Key,sorted);}
            var positions=new List<Vector3>(60000);var uv=new List<Vector2>(60000);var triangles=new List<int>(leaves.Count*24);var ids=new Dictionary<long,int>();
            Func<int,int,int> vertex=(x,z)=>
            {
                long key=Key(x,z);if(ids.TryGetValue(key,out int found))return found;
                int index=positions.Count;ids.Add(key,index);float px=World(x),pz=World(z);positions.Add(new Vector3(px,At(x,z),pz));
                GeoPoint p=MarsRegionalTerrain.ToGeo(px,pz);Vector2 image=MarsGeospatial.ToEquirectangularUv(p.latitude,p.longitude);
                image.x=(MarsRegionalTerrain.Current.regionCenter.longitude+180)/360+MarsGeospatial.NormalizeLongitude(p.longitude-MarsRegionalTerrain.Current.regionCenter.longitude)/360;uv.Add(image);return index;
            };
            foreach(Cell leaf in leaves)
            {
                int x=leaf.x,z=leaf.z,s=leaf.size;var edge=new List<int>(12);
                AppendEdge(edge,columns[x],z,z+s,false,v=>vertex(x,v),false);
                AppendEdge(edge,rows[z+s],x,x+s,false,v=>vertex(v,z+s),true);
                AppendEdge(edge,columns[x+s],z,z+s,true,v=>vertex(x+s,v),true);
                AppendEdge(edge,rows[z],x,x+s,true,v=>vertex(v,z),true);
                if(edge.Count>1&&edge[edge.Count-1]==edge[0])edge.RemoveAt(edge.Count-1);
                leaf.center=vertex(x+s/2,z+s/2);leaf.boundary=edge.ToArray();
                for(int i=0;i<edge.Count;i++){triangles.Add(leaf.center);triangles.Add(edge[i]);triangles.Add(edge[(i+1)%edge.Count]);}
            }
            vertices=positions.ToArray();
            if(vertices.Length>70000||samples.Count>100000)throw new InvalidOperationException("Adaptive terrain budget exceeded.");
            var mesh=new Mesh{name="Measured adaptive route corridor",indexFormat=UnityEngine.Rendering.IndexFormat.UInt32,vertices=vertices,uv=uv.ToArray(),uv2=uv.ToArray(),triangles=triangles.ToArray()};
            var colors=new Color[vertices.Length];for(int i=0;i<colors.Length;i++)colors[i]=Color.white;mesh.colors=colors;
            mesh.RecalculateNormals();mesh.RecalculateTangents();mesh.RecalculateBounds();
            gameObject.AddComponent<MeshFilter>().sharedMesh=mesh;var renderer=gameObject.AddComponent<MeshRenderer>();renderer.sharedMaterial=material;
            renderer.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;renderer.receiveShadows=false;
        }
        private static void AppendEdge(List<int> edge,List<int> values,int low,int high,bool reverse,Func<int,int> vertex,bool skipFirst)
        {
            int begin=values.BinarySearch(low);if(begin<0)begin=~begin;int end=values.BinarySearch(high);if(end<0)end=~end-1;
            if(reverse){for(int i=end;i>=begin;i--){if(skipFirst&&i==end)continue;edge.Add(vertex(values[i]));}}
            else for(int i=begin;i<=end;i++){if(skipFirst&&i==begin)continue;edge.Add(vertex(values[i]));}
        }
        private Cell Find(float x,float z)
        {
            float gx=(x/span+.5f)*Grid,gz=(z/span+.5f)*Grid;if(gx<0||gz<0||gx>Grid||gz>Grid)return null;
            Cell leaf=roots[Mathf.Min(63,(int)gz/128)*BaseCells+Mathf.Min(63,(int)gx/128)];
            while(leaf.children!=null){int h=leaf.size/2;leaf=leaf.children[(gx>=leaf.x+h?1:0)+(gz>=leaf.z+h?2:0)];}return leaf;
        }
        public static float CellMetersAt(float x,float z)
        {if(!Usable)return 0;Cell cell=active.Find(x,z);return cell==null?0:cell.size*active.span/Grid*active.metres;}
        public static bool TryRenderedHeight(float x,float z,out float height)
        {
            height=0;if(!Usable)return false;Cell leaf=active.Find(x,z);if(leaf==null)return false;
            Vector3 center=active.vertices[leaf.center];
            for(int i=0;i<leaf.boundary.Length;i++)
            {
                Vector3 a=active.vertices[leaf.boundary[i]],b=active.vertices[leaf.boundary[(i+1)%leaf.boundary.Length]];
                float denominator=(a.z-b.z)*(center.x-b.x)+(b.x-a.x)*(center.z-b.z);if(Mathf.Abs(denominator)<1e-12f)continue;
                float u=((a.z-b.z)*(x-b.x)+(b.x-a.x)*(z-b.z))/denominator;
                float v=((b.z-center.z)*(x-b.x)+(center.x-b.x)*(z-b.z))/denominator,w=1-u-v;
                if(u>=-.0001f&&v>=-.0001f&&w>=-.0001f){height=center.y*u+a.y*v+b.y*w;return true;}
            }
            return false;
        }
        private void OnEnable(){active=this;}
        private void OnDisable(){if(active==this)active=null;}
    }
}

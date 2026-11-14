using System.Collections.Generic;
using UnityEngine;

namespace MarsPath
{
    /// <summary>Measured map context beyond the regional analysis mesh. Distant
    /// display geometry uses the in-memory official MOLA16 raster, in the same
    /// geodesic frame and elevation datum as the fine boundary.</summary>
    public static class MarsContinuousPlanningTerrain
    {
        public static void Build(Transform parent,Mesh fine,Material material,int resolution)
        {
            var border=new List<Vector3>();Vector3[] source=fine.vertices;
            for(int i=0;i<resolution;i++)border.Add(source[i]);
            for(int i=1;i<resolution;i++)border.Add(source[i*resolution+resolution-1]);
            for(int i=resolution-2;i>=0;i--)border.Add(source[(resolution-1)*resolution+i]);
            for(int i=resolution-2;i>0;i--)border.Add(source[i*resolution]);
            const int rings=32;int count=border.Count;
            // The azimuthal-equidistant map has one unique geodesic disk. Beyond
            // its antipode, Destination would repeat the globe over many laps.
            float maximumRadius=(float)(System.Math.PI*MarsRegionalTerrain.RadiusMeters/MarsRegionalTerrain.MetersPerUnit);
            var vertices=new Vector3[count*(rings+1)];var uv=new Vector2[vertices.Length];var colors=new Color[vertices.Length];var triangles=new int[count*rings*6];
            for(int ring=0;ring<=rings;ring++)
            {
                float fraction=Mathf.Pow(ring/(float)rings,2.3f);
                for(int i=0;i<count;i++)
                {
                    int k=ring*count+i;Vector3 edge=border[i];float radius=new Vector2(edge.x,edge.z).magnitude;
                    float outer=Mathf.Max(radius,Mathf.Min(radius*24,maximumRadius));
                    float distance=Mathf.Lerp(radius,outer,fraction),factor=distance/radius;
                    Vector3 p=new Vector3(edge.x*factor,edge.y,edge.z*factor);
                    if(ring>0)
                    {
                        // Keep the exact fine-mesh boundary, then blend between
                        // measured resolutions over a short annulus. This has
                        // no disk I/O and never asks all fine providers to page
                        // distant, sparsely sampled planetary coordinates.
                        float transition=Mathf.Min((outer-radius)*.1f,Mathf.Max(radius*.08f,7400/MarsRegionalTerrain.MetersPerUnit));
                        float blend=transition>0?Mathf.SmoothStep(0,1,Mathf.Clamp01((distance-radius)/transition)):0;
                        p.y=Mathf.Lerp(edge.y,MarsRegionalTerrain.SampleContextHeight(p.x,p.z),blend);
                    }
                    vertices[k]=p;GeoPoint point=MarsRegionalTerrain.ToGeo(p.x,p.z);uv[k]=MarsGeospatial.ToEquirectangularUv(point.latitude,point.longitude);
                    float center=MarsRegionalTerrain.Current.regionCenter.longitude;uv[k].x=(center+180)/360+MarsGeospatial.NormalizeLongitude(point.longitude-center)/360;colors[k]=Color.white;
                    if(ring==rings)continue;int next=ring*count+(i+1)%count,t=(ring*count+i)*6;
                    triangles[t]=k;triangles[t+1]=next;triangles[t+2]=k+count;triangles[t+3]=next;triangles[t+4]=next+count;triangles[t+5]=k+count;
                }
            }
            var mesh=new Mesh{name="Continuous measured regional surroundings",indexFormat=UnityEngine.Rendering.IndexFormat.UInt32,vertices=vertices,uv=uv,uv2=uv,colors=colors,triangles=triangles};
            mesh.RecalculateNormals();mesh.RecalculateTangents();mesh.RecalculateBounds();
            var context=new GameObject("Continuous measured map surroundings");context.transform.SetParent(parent,false);context.AddComponent<MeshFilter>().sharedMesh=mesh;
            var renderer=context.AddComponent<MeshRenderer>();renderer.sharedMaterial=material;renderer.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;renderer.receiveShadows=false;
        }
    }
}

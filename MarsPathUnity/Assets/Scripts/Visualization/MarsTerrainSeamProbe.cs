using System.Collections.Generic;
using UnityEngine;

namespace MarsPath
{
    /// <summary>Independent inspection of a regular regional mesh against the
    /// actual coarse planetary triangles. Temporary collision geometry is removed
    /// after inspection; existing render materials, mesh buffers and scene objects
    /// are not altered.</summary>
    public static class MarsTerrainSeamProbe
    {
        public sealed class Report
        {
            public bool Available;
            public string Error;
            public int VertexSamples,MidpointSamples,Misses;
            public float VertexMaxGap,MidpointMaxGap;
            public float VertexMinNormalDot=1,MidpointMinNormalDot=1;
            public float VertexMaxUvError,MidpointMaxUvError;
            public float PositionTolerance=0.005f;
            public bool GeometryWithinTolerance=>Available&&Misses==0&&VertexMaxGap<=PositionTolerance&&MidpointMaxGap<=PositionTolerance;
            public bool Passed=>GeometryWithinTolerance&&VertexMinNormalDot>0.9999f&&MidpointMinNormalDot>0.9999f&&VertexMaxUvError<0.0001f&&MidpointMaxUvError<0.0001f;
            public override string ToString()
            {
                return $"available={Available}; vertices={VertexSamples}; midpoints={MidpointSamples}; misses={Misses}; "
                    +$"vertexGap={VertexMaxGap:G6}; midpointGap={MidpointMaxGap:G6}; "
                    +$"vertexNormalDot={VertexMinNormalDot:G7}; midpointNormalDot={MidpointMinNormalDot:G7}; "
                    +$"vertexUvError={VertexMaxUvError:G6}; midpointUvError={MidpointMaxUvError:G6}; passed={Passed}"
                    +(string.IsNullOrEmpty(Error)?"":"; error="+Error);
            }
        }

        public static Report Inspect(MeshFilter fine,MeshFilter coarse,int fineWidth=0,bool log=true,float positionTolerance=0.005f)
        {
            Report report=new Report{PositionTolerance=positionTolerance};
            GameObject probeObject=null;Mesh collisionMesh=null;
            try
            {
                if(fine==null||coarse==null||fine.sharedMesh==null||coarse.sharedMesh==null)
                {report.Error="Both generated mesh filters are required.";return report;}
                Mesh fineMesh=fine.sharedMesh,coarseMesh=coarse.sharedMesh;
                Vector3[] fineVertices=fineMesh.vertices,fineNormals=fineMesh.normals,coarseVertices=coarseMesh.vertices,coarseNormals=coarseMesh.normals;
                Vector2[] fineUv=fineMesh.uv,coarseUv=coarseMesh.uv;int[] triangles=coarseMesh.triangles;
                if(fineWidth<=0)fineWidth=Mathf.RoundToInt(Mathf.Sqrt(fineVertices.Length));
                if(fineWidth<2||fineWidth*fineWidth!=fineVertices.Length||fineNormals.Length!=fineVertices.Length||fineUv.Length!=fineVertices.Length
                    ||coarseNormals.Length!=coarseVertices.Length||coarseUv.Length!=coarseVertices.Length)
                {report.Error="Expected a square regional grid with normals and UV buffers on both meshes.";return report;}

                float radius=(float)MarsRegionalTerrain.RadiusMeters/MarsRegionalTerrain.MetersPerUnit;
                Vector3 planetCentre=coarse.transform.TransformPoint(Vector3.down*(radius+1.5f));
                Vector3[] worldCoarse=new Vector3[coarseVertices.Length];float outerRadius=0;
                for(int i=0;i<worldCoarse.Length;i++)
                {worldCoarse[i]=coarse.transform.TransformPoint(coarseVertices[i]);outerRadius=Mathf.Max(outerRadius,(worldCoarse[i]-planetCentre).magnitude);}
                float rayRadius=outerRadius+Mathf.Max(1,outerRadius*0.02f);
                collisionMesh=new Mesh{name="Temporary coarse seam inspection",indexFormat=UnityEngine.Rendering.IndexFormat.UInt32};
                collisionMesh.vertices=worldCoarse;collisionMesh.triangles=triangles;collisionMesh.RecalculateBounds();
                probeObject=new GameObject("Temporary Mars terrain seam probe"){hideFlags=HideFlags.HideAndDontSave,layer=2};
                MeshCollider collider=probeObject.AddComponent<MeshCollider>();collider.sharedMesh=collisionMesh;
                Physics.SyncTransforms();
                Matrix4x4 fineNormalMatrix=fine.transform.localToWorldMatrix.inverse.transpose;
                Matrix4x4 coarseNormalMatrix=coarse.transform.localToWorldMatrix.inverse.transpose;
                List<int> boundary=new List<int>(4*(fineWidth-1));
                for(int x=0;x<fineWidth;x++)boundary.Add(x);
                for(int y=1;y<fineWidth;y++)boundary.Add(y*fineWidth+fineWidth-1);
                for(int x=fineWidth-2;x>=0;x--)boundary.Add((fineWidth-1)*fineWidth+x);
                for(int y=fineWidth-2;y>0;y--)boundary.Add(y*fineWidth);
                report.Available=true;
                for(int index=0;index<boundary.Count;index++)
                {
                    int a=boundary[index],b=boundary[(index+1)%boundary.Count];
                    Vector3 first=fine.transform.TransformPoint(fineVertices[a]),second=fine.transform.TransformPoint(fineVertices[b]);
                    Vector3 firstNormal=fineNormalMatrix.MultiplyVector(fineNormals[a]).normalized;
                    Vector3 secondNormal=fineNormalMatrix.MultiplyVector(fineNormals[b]).normalized;
                    Check(first,firstNormal,fineUv[a],true);
                    Check((first+second)*0.5f,(firstNormal+secondNormal).normalized,(fineUv[a]+fineUv[b])*0.5f,false);
                }

                void Check(Vector3 point,Vector3 normal,Vector2 uv,bool vertex)
                {
                    if(vertex)report.VertexSamples++;else report.MidpointSamples++;
                    Vector3 direction=(point-planetCentre).normalized;
                    Ray ray=new Ray(planetCentre+direction*rayRadius,-direction);
                    if(!collider.Raycast(ray,out RaycastHit hit,rayRadius*2.1f)){report.Misses++;return;}
                    float gap=Vector3.Distance(hit.point,point);int t=hit.triangleIndex*3;
                    if(t<0||t+2>=triangles.Length){report.Misses++;return;}
                    int i=triangles[t],j=triangles[t+1],k=triangles[t+2];Vector3 weights=hit.barycentricCoordinate;
                    Vector3 expectedNormal=coarseNormalMatrix.MultiplyVector(coarseNormals[i]*weights.x+coarseNormals[j]*weights.y+coarseNormals[k]*weights.z).normalized;
                    Vector2 expectedUv=coarseUv[i]*weights.x+coarseUv[j]*weights.y+coarseUv[k]*weights.z;
                    float dot=Vector3.Dot(normal,expectedNormal);
                    float du=Mathf.Abs(Mathf.Repeat(uv.x-expectedUv.x+0.5f,1)-0.5f);
                    float uvError=Mathf.Max(du,Mathf.Abs(uv.y-expectedUv.y));
                    if(vertex)
                    {report.VertexMaxGap=Mathf.Max(report.VertexMaxGap,gap);report.VertexMinNormalDot=Mathf.Min(report.VertexMinNormalDot,dot);report.VertexMaxUvError=Mathf.Max(report.VertexMaxUvError,uvError);}
                    else
                    {report.MidpointMaxGap=Mathf.Max(report.MidpointMaxGap,gap);report.MidpointMinNormalDot=Mathf.Min(report.MidpointMinNormalDot,dot);report.MidpointMaxUvError=Mathf.Max(report.MidpointMaxUvError,uvError);}
                }
            }
            catch(System.Exception exception){report.Available=false;report.Error=exception.Message;}
            finally
            {
                if(probeObject!=null)
                {
                    Collider collider=probeObject.GetComponent<Collider>();if(collider!=null)collider.enabled=false;
                    if(Application.isPlaying)Object.Destroy(probeObject);else Object.DestroyImmediate(probeObject);
                }
                if(collisionMesh!=null){if(Application.isPlaying)Object.Destroy(collisionMesh);else Object.DestroyImmediate(collisionMesh);}
                if(log)Debug.Log("MARSPATH_SEAM_PROBE: "+report);
            }
            return report;
        }
    }
}

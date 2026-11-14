using UnityEngine;

namespace MarsPath
{
    /// <summary>The exact triangle surface of the coarse globe. The finer
    /// regional mesh joins this surface, including normals and image UVs.</summary>
    public sealed class MarsPlanetGeometry
    {
        public const int Columns=512,Rows=256;
        public readonly Mesh mesh;
        private readonly Vector3[] vertices,normals;
        private readonly Vector2[] uv;
        public MarsPlanetGeometry()
        {
            int width=Columns+1;vertices=new Vector3[width*(Rows+1)];uv=new Vector2[vertices.Length];
            Color[] colors=new Color[vertices.Length];int[] triangles=new int[Columns*Rows*6];int k=0;
            for(int y=0;y<=Rows;y++)for(int x=0;x<=Columns;x++)
            {
                float lat=90-y*180f/Rows,lon=-180+x*360f/Columns;int i=y*width+x;
                vertices[i]=MarsRegionalTerrain.PlanetPoint(new GeoPoint{latitude=lat,longitude=lon});
                uv[i]=new Vector2(x/(float)Columns,1-y/(float)Rows);colors[i]=Color.white;
                if(x<Columns&&y<Rows){triangles[k++]=i;triangles[k++]=i+1;triangles[k++]=i+width;triangles[k++]=i+1;triangles[k++]=i+width+1;triangles[k++]=i+width;}
            }
            mesh=new Mesh{name="Continuous measured Mars context",indexFormat=UnityEngine.Rendering.IndexFormat.UInt32};
            mesh.vertices=vertices;mesh.uv=uv;mesh.uv2=uv;mesh.colors=colors;mesh.triangles=triangles;mesh.RecalculateNormals();mesh.RecalculateTangents();mesh.RecalculateBounds();normals=mesh.normals;
        }
        public void Sample(GeoPoint point,out Vector3 position,out Vector3 normal,out Vector2 imageUv)
        {
            float gx=Mathf.Repeat((point.longitude+180)/360f,1)*Columns,gy=Mathf.Clamp((90-point.latitude)/180f*Rows,0,Rows-0.0001f);
            int x=Mathf.FloorToInt(gx),y=Mathf.FloorToInt(gy),a=y*(Columns+1)+x,b=a+1,c=a+Columns+1,d=c+1;
            float tx=gx-x,ty=gy-y;int i,j,l;Vector3 w;
            if(tx+ty<=1){i=a;j=b;l=c;w=new Vector3(1-tx-ty,tx,ty);}
            else{i=d;j=c;l=b;w=new Vector3(tx+ty-1,1-tx,1-ty);}
            position=vertices[i]*w.x+vertices[j]*w.y+vertices[l]*w.z;
            normal=(normals[i]*w.x+normals[j]*w.y+normals[l]*w.z).normalized;
            imageUv=uv[i]*w.x+uv[j]*w.y+uv[l]*w.z;
        }
        public static float EdgeBlend(float x,float z)
        {return Mathf.SmoothStep(0,1,Mathf.InverseLerp(60,75,Mathf.Max(Mathf.Abs(x),Mathf.Abs(z))));}
    }
}

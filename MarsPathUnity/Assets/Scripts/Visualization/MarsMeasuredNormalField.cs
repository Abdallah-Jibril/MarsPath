using UnityEngine;

namespace MarsPath
{
    /// <summary>Lighting normals sampled at the source's useful resolution.
    /// Dense metre-scale differences of a 20–100 m source only amplify geographic
    /// float quantization. This field changes no DEM vertex or physical slope.</summary>
    internal sealed class MarsMeasuredNormalField
    {
        private readonly float edge,step;
        private readonly int cells,width;
        private readonly Vector3[] normals;
        public MarsMeasuredNormalField(float halfExtent,float sourceSpacing,float meshStep)
        {
            edge=halfExtent;
            float target=Mathf.Max(meshStep,MarsTerrainGenerator.MeasuredNormalBaseline(sourceSpacing));
            cells=Mathf.Max(2,Mathf.CeilToInt(halfExtent*2/target));step=halfExtent*2/cells;width=cells+1;
            int sampleWidth=width+2;var heights=new float[sampleWidth*sampleWidth];
            for(int z=0;z<sampleWidth;z++)for(int x=0;x<sampleWidth;x++)
                heights[z*sampleWidth+x]=MarsTerrainGenerator.SampleHeight(-edge+(x-1)*step,-edge+(z-1)*step);
            normals=new Vector3[width*width];
            for(int z=0;z<width;z++)for(int x=0;x<width;x++)
            {
                int k=(z+1)*sampleWidth+x+1;
                // Symmetric 3×3 least-squares slope on actual source samples.
                // Averaging across the orthogonal direction rejects coordinate
                // read noise without smoothing or replacing any terrain heights.
                float dx=heights[k-sampleWidth+1]+2*heights[k+1]+heights[k+sampleWidth+1]
                    -heights[k-sampleWidth-1]-2*heights[k-1]-heights[k+sampleWidth-1];
                float dz=heights[k+sampleWidth-1]+2*heights[k+sampleWidth]+heights[k+sampleWidth+1]
                    -heights[k-sampleWidth-1]-2*heights[k-sampleWidth]-heights[k-sampleWidth+1];
                normals[z*width+x]=new Vector3(-dx,8*step,-dz).normalized;
            }
        }
        public Vector3 At(float x,float z)
        {
            float gx=Mathf.Clamp((x+edge)/step,0,cells),gz=Mathf.Clamp((z+edge)/step,0,cells);
            int ix=Mathf.Min(cells-1,Mathf.FloorToInt(gx)),iz=Mathf.Min(cells-1,Mathf.FloorToInt(gz));
            float tx=gx-ix,tz=gz-iz;int k=iz*width+ix;
            return Vector3.Lerp(Vector3.Lerp(normals[k],normals[k+1],tx),Vector3.Lerp(normals[k+width],normals[k+width+1],tx),tz).normalized;
        }
        public static Vector3 SourceKernelAt(float x,float z,float delta)
        {
            float nw=MarsTerrainGenerator.SampleHeight(x-delta,z+delta),n=MarsTerrainGenerator.SampleHeight(x,z+delta),ne=MarsTerrainGenerator.SampleHeight(x+delta,z+delta);
            float w=MarsTerrainGenerator.SampleHeight(x-delta,z),e=MarsTerrainGenerator.SampleHeight(x+delta,z);
            float sw=MarsTerrainGenerator.SampleHeight(x-delta,z-delta),s=MarsTerrainGenerator.SampleHeight(x,z-delta),se=MarsTerrainGenerator.SampleHeight(x+delta,z-delta);
            return new Vector3(-(ne+2*e+se-nw-2*w-sw),8*delta,-(nw+2*n+ne-sw-2*s-se)).normalized;
        }
    }
}

using UnityEngine;

namespace MarsPath
{
    public static partial class MarsOfficialTerrain
    {
        private const int ImageFeatherPixels=24;
        private static byte[] imagePremultiply;
        private static ColorSpace imagePremultiplySpace;

        private static void FeatherOfficialImage(Texture2D texture)
        {
            Color32[] pixels=texture.GetPixels32();
            PrepareOfficialImagePixels(pixels,texture.width,texture.height);
            texture.SetPixels32(pixels);texture.Apply(true,true);
        }
        private static void PrepareOfficialImagePixels(Color32[] pixels,int width,int height)
        {
            var edge=new byte[pixels.Length];
            for(int i=0;i<edge.Length;i++)edge[i]=pixels[i].a==0?(byte)0:(byte)255;
            for(int y=0;y<height;y++)for(int x=0;x<width;x++)
            {int i=y*width+x;if(x==0||y==0||x==width-1||y==height-1)edge[i]=0;else edge[i]=(byte)Mathf.Min(edge[i],Mathf.Min(edge[i-1],edge[i-width])+1);}
            for(int y=height-2;y>0;y--)for(int x=width-2;x>0;x--)
            {int i=y*width+x;edge[i]=(byte)Mathf.Min(edge[i],Mathf.Min(edge[i+1],edge[i+width])+1);}
            EnsureImagePremultiply();
            for(int i=0;i<pixels.Length;i++)
            {
                Color32 p=pixels[i];
                byte alpha=(byte)Mathf.RoundToInt(p.a*Mathf.SmoothStep(0,1,Mathf.Clamp01(edge[i]/(float)ImageFeatherPixels)));
                if(alpha==255)continue; // Retain central native display colours exactly.
                if(alpha==0){pixels[i]=new Color32(0,0,0,0);continue;}
                int k=alpha<<8;
                pixels[i]=new Color32(imagePremultiply[k+p.r],imagePremultiply[k+p.g],imagePremultiply[k+p.b],alpha);
            }
        }
        private static void EnsureImagePremultiply()
        {
            ColorSpace space=QualitySettings.activeColorSpace;
            if(imagePremultiply!=null&&imagePremultiplySpace==space)return;
            imagePremultiplySpace=space;imagePremultiply=new byte[65536];
            for(int a=0;a<256;a++)for(int c=0;c<256;c++)
            {
                float value=c/255f,opacity=a/255f;
                // Surface.shader divides sampled RGB by sampled coverage in
                // linear rendering. Encode linear-premultiplied RGB back into
                // sRGB bytes so mip filtering cannot create bright/dark rims.
                if(space==ColorSpace.Linear)value=Mathf.LinearToGammaSpace(Mathf.GammaToLinearSpace(value)*opacity);
                else value*=opacity;
                imagePremultiply[(a<<8)+c]=(byte)Mathf.RoundToInt(Mathf.Clamp01(value)*255);
            }
        }
        public static bool ImageFootprintSelfCheck()
        {
            const int size=128;var pixels=new Color32[size*size];
            for(int y=0;y<size;y++)for(int x=0;x<size;x++)pixels[y*size+x]=x>=24&&x<36&&y>=24&&y<36?new Color32(0,0,0,0):new Color32(180,180,180,255);
            PrepareOfficialImagePixels(pixels,size,size);
            Color32 centre=pixels[80*size+80],border=pixels[64*size],hole=pixels[30*size+30],transition=pixels[64*size+10];
            float recovered=transition.r/255f,original=180/255f;
            if(QualitySettings.activeColorSpace==ColorSpace.Linear){recovered=Mathf.GammaToLinearSpace(recovered);original=Mathf.GammaToLinearSpace(original);}
            recovered/=transition.a/255f;
            return centre.r==180&&centre.a==255&&border.r==0&&border.a==0&&hole.a==0&&transition.a>0&&transition.a<255&&Mathf.Abs(recovered-original)<.025f;
        }
    }
}

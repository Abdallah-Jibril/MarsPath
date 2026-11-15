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
            EnsureImagePremultiply();
            if (FullyOpaque(pixels))
            {
                // For an opaque raster, the distance transform is exactly its
                // distance to the rectangular border. Interior pixels never change.
                for (int y = 0; y < height; y++)
                {
                    int vertical = Mathf.Min(y, height - y - 1);
                    if (vertical < ImageFeatherPixels)
                    {
                        for (int x = 0; x < width; x++)
                            FeatherImagePixel(pixels, y * width + x, Mathf.Min(vertical, Mathf.Min(x, width - x - 1)));
                    }
                    else
                    {
                        int border = Mathf.Min(ImageFeatherPixels, (width + 1) / 2);
                        for (int x = 0; x < border; x++)
                        {
                            FeatherImagePixel(pixels, y * width + x, x);
                            int opposite = width - x - 1;
                            if (opposite != x) FeatherImagePixel(pixels, y * width + opposite, x);
                        }
                    }
                }
                return;
            }
            var edge=new byte[pixels.Length];
            for(int i=0;i<edge.Length;i++)edge[i]=pixels[i].a==0?(byte)0:(byte)255;
            for(int y=0;y<height;y++)for(int x=0;x<width;x++)
            {int i=y*width+x;if(x==0||y==0||x==width-1||y==height-1)edge[i]=0;else edge[i]=(byte)Mathf.Min(edge[i],Mathf.Min(edge[i-1],edge[i-width])+1);}
            for(int y=height-2;y>0;y--)for(int x=width-2;x>0;x--)
            {int i=y*width+x;edge[i]=(byte)Mathf.Min(edge[i],Mathf.Min(edge[i+1],edge[i+width])+1);}
            for(int i=0;i<pixels.Length;i++)
                if (edge[i] < ImageFeatherPixels || pixels[i].a != 255) FeatherImagePixel(pixels, i, edge[i]);
        }
        private static bool FullyOpaque(Color32[] pixels)
        {
            foreach (Color32 pixel in pixels)
                if (pixel.a != 255) return false;
            return true;
        }
        private static void FeatherImagePixel(Color32[] pixels, int index, int distance)
        {
            Color32 p = pixels[index];
            byte alpha = (byte)Mathf.RoundToInt(p.a * Mathf.SmoothStep(0, 1, Mathf.Clamp01(distance / (float)ImageFeatherPixels)));
            if (alpha == 255) return;
            if (alpha == 0) { pixels[index] = new Color32(0, 0, 0, 0); return; }
            int k = alpha << 8;
            pixels[index] = new Color32(imagePremultiply[k + p.r], imagePremultiply[k + p.g], imagePremultiply[k + p.b], alpha);
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
            if (!(centre.r==180&&centre.a==255&&border.r==0&&border.a==0&&hole.a==0&&transition.a>0&&transition.a<255&&Mathf.Abs(recovered-original)<.025f))
                return false;
            var opaque = new Color32[size * size];
            for (int i = 0; i < opaque.Length; i++) opaque[i] = new Color32(180, 180, 180, 255);
            PrepareOfficialImagePixels(opaque, size, size);
            return opaque[64 * size].Equals(border) && opaque[64 * size + 10].Equals(transition) &&
                opaque[80 * size + 80].Equals(centre);
        }
    }
}

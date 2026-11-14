using UnityEditor;
using UnityEngine;

namespace MarsPath.Editor
{
    public sealed class MarsHudIllustrationImport : AssetPostprocessor
    {
        private void OnPreprocessTexture()
        {
            if(!assetPath.EndsWith("/HudHazardAtlas.png",System.StringComparison.Ordinal)&&!assetPath.EndsWith("/MapPinEmoji.png",System.StringComparison.Ordinal))return;
            var texture=(TextureImporter)assetImporter;
            texture.textureType=TextureImporterType.Default;
            texture.alphaSource=TextureImporterAlphaSource.FromInput;
            texture.alphaIsTransparency=true;
            texture.sRGBTexture=true;
            texture.mipmapEnabled=false;
            texture.wrapMode=TextureWrapMode.Clamp;
            texture.filterMode=FilterMode.Bilinear;
            texture.maxTextureSize=2048;
            if(assetPath.EndsWith("/MapPinEmoji.png",System.StringComparison.Ordinal))
            {texture.npotScale=TextureImporterNPOTScale.None;texture.textureCompression=TextureImporterCompression.Uncompressed;}
        }
    }
}

using System;
using UnityEngine;

namespace MarsPath
{
    /// <summary>A bounded, audited offline mission sector. It constrains new
    /// regional plans without moving either of the user's chosen endpoints.</summary>
    public static class MarsMissionZone
    {
        public const string Id="jezero-offline-1m";
        internal static readonly Color MapTint = new Color(1f, .68f, .43f);
        [Serializable]private sealed class Definition
        {
            public string name,dtmSource,imageSource,imageResource;
            public double west,east,south,north,imageWest,imageEast,imageSouth,imageNorth;
            public float spacingMeters,widthKm,heightKm,imageDisplaySpacingMeters,imageOverviewSpacingMeters;
        }
        private static Definition definition;
        private static bool loaded;
        private static Texture2D image;
        public static bool RequiredForSelection{get;set;}
        internal static bool ElevationsActive=>RequiredForSelection||IsMission(MarsRegionalTerrain.Current);
        private static void Load()
        {
            if(loaded)return;loaded=true;
            TextAsset file=Resources.Load<TextAsset>("NASA/MissionZone/JezeroZone");
            if(file==null)return;
            Definition candidate=JsonUtility.FromJson<Definition>(file.text);
            if(candidate!=null&&candidate.east>candidate.west&&candidate.north>candidate.south&&candidate.spacingMeters>0&&candidate.spacingMeters<1.1f)definition=candidate;
        }
        public static bool Available{get{Load();return definition!=null;}}
        public static string Name{get{Load();return definition?.name??"Jezero measured mission zone";}}
        public static double West{get{Load();return definition?.west??0;}}
        public static double East{get{Load();return definition?.east??0;}}
        public static double South{get{Load();return definition?.south??0;}}
        public static double North{get{Load();return definition?.north??0;}}
        public static float SpacingMeters{get{Load();return definition?.spacingMeters??0;}}
        public static float WidthKm{get{Load();return definition?.widthKm??0;}}
        public static float HeightKm{get{Load();return definition?.heightKm??0;}}
        public static GeoPoint Center=>AtNormalized(.5f,.5f);
        public static Texture2D Image
        {
            get
            {
                Load();if(image!=null||definition==null)return image;
                TextAsset pixels=Resources.Load<TextAsset>(definition.imageResource);
                if(pixels==null)return null;
                var texture=new Texture2D(2,2,TextureFormat.RGBA32,true,false){name="Measured Jezero mission-zone map",wrapMode=TextureWrapMode.Clamp,filterMode=FilterMode.Trilinear,anisoLevel=8};
                if(!ImageConversion.LoadImage(texture,pixels.bytes)){UnityEngine.Object.Destroy(texture);return null;}
                image=texture;return image;
            }
        }
        // Geographic subset of the independently registered image, not an
        // assumption that image and elevation pixel indices coincide.
        public static Rect TextureUV
        {
            get
            {
                Load();if(definition==null)return new Rect(0,0,1,1);
                double width=definition.imageEast-definition.imageWest,height=definition.imageNorth-definition.imageSouth;
                if(width<=0||height<=0)return new Rect(0,0,1,1);
                return new Rect((float)((West-definition.imageWest)/width),(float)((South-definition.imageSouth)/height),
                    (float)((East-West)/width),(float)((North-South)/height));
            }
        }
        public static bool BoundsContain(GeoPoint point)=>point!=null&&BoundsContain(point.latitude,point.longitude);
        internal static bool BoundsContain(double latitude,double longitude)
        {return Available&&latitude>=South&&latitude<=North&&longitude>=West&&longitude<=East;}
        public static bool Contains(GeoPoint point)=>BoundsContain(point)&&MarsOfficialTerrain.TryMissionZoneElevation(point,out _);
        public static float Elevation(GeoPoint point)
        {
            if(!BoundsContain(point)||!MarsOfficialTerrain.TryMissionZoneElevation(point,out float elevation))
                throw new ArgumentOutOfRangeException(nameof(point),"Choose a point inside the measured Jezero mission zone.");
            return elevation;
        }
        public static GeoPoint AtNormalized(float eastFraction,float northFraction)=>new GeoPoint{label="Jezero zone map selection",
            latitude=(float)(South+(North-South)*northFraction),longitude=(float)(West+(East-West)*eastFraction)};
        public static Vector2 Normalized(GeoPoint point)=>Available&&point!=null
            ?new Vector2((float)((point.longitude-West)/(East-West)),(float)((point.latitude-South)/(North-South))):Vector2.zero;
        public static bool IsMission(MissionData mission)=>mission?.routingOptions?.MissionZoneId==Id;
        internal static void BindOverview(Material material)
        {
            if(!ElevationsActive||!Available||material==null||!material.HasProperty("_CtxOverviewTex")||Image==null)return;
            // Whole-sector fallback remains behind the independently registered
            // native foreground tile, so crossing a tile never exposes Viking.
            material.SetTexture("_CtxOverviewTex",Image);
            material.SetVector("_CtxOverviewRect",new Vector4((float)(definition.imageWest/360+.5),(float)(definition.imageSouth/180+.5),
                (float)((definition.imageEast-definition.imageWest)/360),(float)((definition.imageNorth-definition.imageSouth)/180)));
            material.SetFloat("_CtxOverviewSpacing",definition.imageOverviewSpacingMeters>0?definition.imageOverviewSpacingMeters:Mathf.Max(SpacingMeters,definition.imageDisplaySpacingMeters));
            material.SetFloat("_CtxOverviewBlend",1);material.SetFloat("_CtxViewBlend",0);material.SetFloat("_CtxDetailBlend",0);
        }
    }
}

using System.Collections.Generic;
using UnityEngine;

namespace MarsPath
{
    public class MarsTerrainGenerator : MonoBehaviour
    {
        public const float TerrainSize = 120f;
        public const float VisualTerrainSize = 360f;
        public const int Resolution = 385;
        public const int HeightDataResolution = 257;

        private Material terrainMaterial;
        private MarsMeasuredNormalField measuredNormals;
        public static int PlanningLayer{get;set;}=0;
        private Texture2D boundOrbital;
        private int boundCtxRevision=-1,boundOfficialImageryRevision=-1;
        private void LateUpdate()
        {if(boundOrbital!=MarsRegionSourceCache.OrbitalTexture||boundCtxRevision!=MarsCtxImageryCache.Revision||boundOfficialImageryRevision!=MarsOfficialTerrain.ImageryRevision){boundOrbital=MarsRegionSourceCache.OrbitalTexture;boundCtxRevision=MarsCtxImageryCache.Revision;boundOfficialImageryRevision=MarsOfficialTerrain.ImageryRevision;RefreshSourceImagery();}}
        private static byte[] jezeroHeightData;
        private MissionData mission;
        private GeoPoint imageryOrigin;
        private double imageryMetresPerUnit;
        private MarsPlanetGeometry planetGeometry;
        private static bool regionalMode;
        private struct SurfaceRock { public Vector3 center; public float radius; }
        private static readonly List<SurfaceRock> surfaceRocks=new List<SurfaceRock>();
        public static int SurfaceRockCount=>surfaceRocks.Count+MarsSurfaceRockScatter.SurfaceRockCount;
        public static float SurfaceSlope(float x,float z)
        {
            const float step=0.5f;
            float dx=SampleHeight(x+step,z)-SampleHeight(x-step,z),dz=SampleHeight(x,z+step)-SampleHeight(x,z-step);
            return Mathf.Atan2(Mathf.Sqrt(dx*dx+dz*dz),2*step)*Mathf.Rad2Deg;
        }
        public static bool TryRockAhead(Vector3 position,Vector3 forward,out float distance)
        {
            return TryRockAhead(position,forward,out distance,out _);
        }
        public static bool TryRockAhead(Vector3 position,Vector3 forward,out float distance,out int fieldCount)
        {
            if(MarsSurfaceRockScatter.TryRockAhead(position,forward,out distance,out fieldCount))return true;
            distance=float.MaxValue;forward.y=0;forward.Normalize();
            foreach(SurfaceRock rock in surfaceRocks)
            {
                Vector3 offset=rock.center-position;offset.y=0;
                float along=Vector3.Dot(offset,forward);
                float lateral=(offset-forward*along).magnitude;
                if(along>0&&along<11f&&lateral<rock.radius+1.6f)
                    distance=Mathf.Min(distance,Mathf.Max(0,offset.magnitude-rock.radius));
            }
            return distance<float.MaxValue;
        }
        public static bool RockSensorSelfCheck()
        {
            if(MarsSurfaceRockScatter.SurfaceRockCount>0)return MarsSurfaceRockScatter.RockSensorSelfCheck();
            if(surfaceRocks.Count==0)return false;
            SurfaceRock rock=surfaceRocks[0];
            return TryRockAhead(rock.center-Vector3.forward*(rock.radius+3),Vector3.forward,out float found)&&found<4;
        }

        public void Build(MissionData sourceMission)
        {
            mission = sourceMission;
            surfaceRocks.Clear();
            regionalMode = sourceMission.generatedRegion;
            if (regionalMode) MarsRegionalTerrain.Configure(sourceMission);
            GeoPoint sourceOrigin=sourceMission.regionCenter??sourceMission.currentLocation;
            imageryOrigin=sourceOrigin==null?null:new GeoPoint{latitude=sourceOrigin.latitude,longitude=sourceOrigin.longitude};
            imageryMetresPerUnit=regionalMode?MarsRegionalTerrain.MetersPerUnit:1;
            LoadJezeroHeightData();
            bool journeyMap=regionalMode&&!MarsRegionalTerrain.HumanScale;
            float visualSize=journeyMap?TerrainSize*1.25f:VisualTerrainSize;
            float spacing=regionalMode?MarsRegionalTerrain.SourceSpacingMeters(sourceMission.regionCenter):20f;
            int resolution=regionalMode?(MarsRegionalTerrain.HumanScale?(spacing<=2?385:spacing<=25?193:97):Mathf.Clamp(Mathf.CeilToInt(visualSize*MarsRegionalTerrain.MetersPerUnit/spacing)+1,193,513)):Resolution;
            bool adaptivePlanning=journeyMap&&!MarsRegionalTerrain.CoarsePlanningDisplay;
            // A planetary overview cannot display sub-kilometre cells at this
            // zoom. Bound geometry instead of substituting coarser elevations:
            // every retained post still samples the finest resident source.
            if(journeyMap&&MarsRegionalTerrain.CoarsePlanningDisplay)resolution=Mathf.Min(resolution,257);
            // A small regular mesh remains as exact boundary/inspection metadata.
            // The visible regional surface below spends its bounded vertex budget
            // on measured curvature along every route, rather than a uniform map.
            if(adaptivePlanning)resolution=65;
            if(journeyMap)PlanningOverviewCellMeters=visualSize*MarsRegionalTerrain.MetersPerUnit/(resolution-1);
            Mesh mesh = new Mesh { name = "Measured regional Mars terrain" };
            mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;

            Vector3[] vertices = new Vector3[resolution * resolution];
            Color[] colors = new Color[vertices.Length];
            Vector2[] uvs = new Vector2[vertices.Length];
            Vector2[] geographicUvs=new Vector2[vertices.Length];
            int[] triangles = new int[(resolution - 1) * (resolution - 1) * 6];

            int t = 0;
            for (int z = 0; z < resolution; z++)
            {
                for (int x = 0; x < resolution; x++)
                {
                    float worldX = ((float)x / (resolution - 1) - 0.5f) * visualSize;
                    float worldZ = ((float)z / (resolution - 1) - 0.5f) * visualSize;
                    float height = SampleHeight(worldX, worldZ);
                    GeoPoint location=regionalMode?MarsRegionalTerrain.ToGeo(worldX,worldZ):null;
                    int index = z * resolution + x;
                    if(regionalMode)geographicUvs[index]=MarsGeospatial.ToEquirectangularUv(location.latitude,location.longitude);
                    Vector3 vertex=new Vector3(worldX,height,worldZ);
                    vertices[index] = vertex;
                    uvs[index] = new Vector2(
                        Mathf.Clamp01(worldX / TerrainSize + 0.5f),
                        Mathf.Clamp01(worldZ / TerrainSize + 0.5f));
                    if(regionalMode)uvs[index]=new Vector2(worldX/18f,worldZ/18f);
                    if(regionalMode&&sourceMission.usesJezeroDem&&!journeyMap)uvs[index]=MarsRegionalTerrain.FullOrthoUv(worldX,worldZ);
                    if(journeyMap)
                    {
                        Vector2 globalUv=MarsGeospatial.ToEquirectangularUv(location.latitude,location.longitude);
                        globalUv.x=(sourceMission.regionCenter.longitude+180f)/360f+MarsGeospatial.NormalizeLongitude(location.longitude-sourceMission.regionCenter.longitude)/360f;
                        uvs[index]=globalUv;
                    }

                    float shade = Mathf.InverseLerp(-3f, 10f, height);
                    float mineralVariation = Mathf.PerlinNoise((worldX + 30f) * 0.14f, (worldZ - 40f) * 0.14f);
                    Color low = new Color(0.72f, 0.58f, 0.50f);
                    Color high = new Color(1.05f, 0.93f, 0.82f);
                    Color mineral = new Color(0.94f, 0.73f, 0.59f);
                    colors[index] = Color.Lerp(Color.Lerp(low, high, shade), mineral, mineralVariation * 0.24f);
                    if(journeyMap)colors[index]=Color.white;
                    if(regionalMode&&MarsRegionalTerrain.HumanScale)colors[index]=Color.white;

                    if (x < resolution - 1 && z < resolution - 1)
                    {
                        // A planetary azimuthal map ends at the antipode.
                        // Unrendered degenerate cells prevent repeated Mars laps
                        // beyond its unique disk on very long traverses.
                        if(journeyMap)
                        {
                            float limit=(float)(System.Math.PI*MarsRegionalTerrain.RadiusMeters/MarsRegionalTerrain.MetersPerUnit);
                            float step=visualSize/(resolution-1),farX=Mathf.Max(Mathf.Abs(worldX),Mathf.Abs(worldX+step)),farZ=Mathf.Max(Mathf.Abs(worldZ),Mathf.Abs(worldZ+step));
                            if(farX*farX+farZ*farZ>limit*limit){t+=6;continue;}
                        }
                        int nextRow = index + resolution;
                        triangles[t++] = index;
                        triangles[t++] = nextRow;
                        triangles[t++] = index + 1;
                        triangles[t++] = index + 1;
                        triangles[t++] = nextRow;
                        triangles[t++] = nextRow + 1;
                    }
                }
            }

            mesh.vertices = vertices;
            mesh.triangles = triangles;
            mesh.colors = colors;
            mesh.uv = uvs;
            mesh.uv2=geographicUvs;
            mesh.RecalculateNormals();
            if(regionalMode&&MarsRegionalTerrain.HumanScale)
            {
                measuredNormals=new MarsMeasuredNormalField(visualSize*.5f+12,spacing,visualSize/(resolution-1));
                var normals=new Vector3[vertices.Length];
                for(int i=0;i<vertices.Length;i++)
                {Vector3 p=vertices[i];normals[i]=measuredNormals.At(p.x,p.z);}
                mesh.normals=normals;
            }
            mesh.RecalculateTangents();
            mesh.RecalculateBounds();

            MeshFilter filter = gameObject.AddComponent<MeshFilter>();
            filter.sharedMesh = mesh;
            MeshRenderer renderer = gameObject.AddComponent<MeshRenderer>();
            Texture2D groundTexture = Resources.Load<Texture2D>("NASA/JezeroMissionSurface");
            terrainMaterial = MarsPathMaterialFactory.CreateTextured(groundTexture, new Color(1.12f, 1.04f, 0.98f), "USGS/NASA Jezero Surface Material", Vector2.one);
            Texture2D normalTexture = Resources.Load<Texture2D>("NASA/JezeroMissionNormal");
            if (terrainMaterial != null && normalTexture != null)
            {
                terrainMaterial.SetTexture("_BumpMap", normalTexture);
                terrainMaterial.SetFloat("_BumpScale", 0.72f);
                terrainMaterial.SetFloat("_Smoothness", 0.08f);
            }
            if (regionalMode && terrainMaterial != null)
            {
                terrainMaterial.SetFloat("_RegionalBlend", 0f);
                terrainMaterial.SetFloat("_DetailMode", journeyMap?0f:1f);
                terrainMaterial.SetFloat("_BumpScale", 0f);
                terrainMaterial.SetTexture("_GroundTex",Resources.Load<Texture2D>("NASA/CuriositySandstone")??Resources.Load<Texture2D>("NASA/JezeroGround"));
                if(sourceMission.usesJezeroDem&&!journeyMap)
                {terrainMaterial.mainTexture=Resources.Load<Texture2D>("NASA/JezeroFullOrtho");terrainMaterial.SetFloat("_OrthoBlend",0.78f);}
                if(journeyMap)
                {
                    terrainMaterial.mainTexture=Resources.Load<Texture2D>("NASA/MarsMdimColor8k");terrainMaterial.color=Color.white;
                    terrainMaterial.SetFloat("_DepthOffsetUnits",-1);
                    renderer.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;renderer.receiveShadows=false;
                }
            }
            if(regionalMode&&MarsRegionalTerrain.HumanScale)
            {
                Shader groundShader=Resources.Load<Shader>("MarsPathMeasuredGround");
                if(groundShader!=null)
                {
                    Destroy(terrainMaterial);terrainMaterial=new Material(groundShader){name="Measured terrain / geographic albedo"};
                    terrainMaterial.SetTexture("_SoilTex",Resources.Load<Texture2D>("NASA/CuriositySandstone"));
                    terrainMaterial.SetTexture("_GlobalTex",Resources.Load<Texture2D>("NASA/MarsMdimColor8k"));
                    ConfigureGeographicMaterial(terrainMaterial,sourceMission.regionCenter);
                }
            }
            if (terrainMaterial != null) renderer.sharedMaterial = terrainMaterial;
            if(regionalMode)ApplyOrbitalImagery(terrainMaterial);
            if(journeyMap)
            {
                terrainMaterial.SetFloat("_MapLayer",PlanningLayer);terrainMaterial.SetFloat("_MapReliefScale",MarsRegionalTerrain.DisplayReliefScale);
                terrainMaterial.SetFloat("_GeodesicUv",1);
                terrainMaterial.SetVector("_GeoFrame",new Vector4(sourceMission.regionCenter.latitude*Mathf.Deg2Rad,sourceMission.regionCenter.longitude*Mathf.Deg2Rad,MarsRegionalTerrain.MetersPerUnit/(float)MarsRegionalTerrain.RadiusMeters,0));
            }
            if(!journeyMap)BuildDistantTerrain(terrainMaterial,mesh,resolution);
            else
            {
                MarsContinuousPlanningTerrain.Build(transform,mesh,terrainMaterial,resolution);
                if(adaptivePlanning&&MarsAdaptivePlanningTerrain.Build(transform,mesh,terrainMaterial,sourceMission))renderer.enabled=false;
            }
            // Orbital planning uses analytical camera controls and no surface
            // collision. Only the metre-scale ground needs a physics collider.
            if(!regionalMode||MarsRegionalTerrain.HumanScale)
            {MeshCollider collider=gameObject.AddComponent<MeshCollider>();collider.sharedMesh=mesh;}
            // Measured scenes get their landforms from the DTM. Decorative
            // boulders are retained only in the legacy illustrative mock scene.
            if(!regionalMode)BuildRockField();
            else if(MarsRegionalTerrain.HumanScale)
            {
                gameObject.AddComponent<MarsSurfaceRockScatter>().Build(sourceMission);
                gameObject.AddComponent<MarsSurfaceGravel>().Build(sourceMission,vertices,resolution,visualSize);
            }
            // The source panorama is retained in the project for reference and
            // later skybox work. A cylinder looked like an overhead ceiling from
            // the EVA camera, so the prototype relies on terrain + atmospheric fog.
        }

        public static float PlanningOverviewCellMeters { get; private set; }

        public static Vector3 SurfacePoint(float x,float z,float offset=0)
        {
            if(regionalMode&&!MarsRegionalTerrain.HumanScale)
            {
                if(MarsPlanningFocusTile.TryRenderedHeight(x,z,out float detail))return new Vector3(x,detail+offset,z);
                if(MarsAdaptivePlanningTerrain.TryRenderedHeight(x,z,out float adaptive))return new Vector3(x,adaptive+offset,z);
            }
            return regionalMode?MarsRegionalTerrain.RenderPoint(x,z,offset):new Vector3(x,SampleHeight(x,z)+offset,z);
        }

        private void BuildPlanetaryTerrain()
        {
            planetGeometry=new MarsPlanetGeometry();Mesh mesh=planetGeometry.mesh;
            GameObject planet=new GameObject("Continuous planetary context");planet.transform.SetParent(transform,false);
            planet.AddComponent<MeshFilter>().sharedMesh=mesh;
            Material material=MarsPathMaterialFactory.CreateTextured(Resources.Load<Texture2D>("NASA/MarsMdimColor8k"),Color.white,"NASA Viking MDIM 2.1 / MOLA planetary terrain",Vector2.one);
            material.SetFloat("_DetailMode",0);material.SetFloat("_RegionalBlend",0);material.SetFloat("_BumpScale",0);
            material.SetFloat("_PlanetaryRadius",(float)MarsRegionalTerrain.RadiusMeters/MarsRegionalTerrain.MetersPerUnit);
            material.SetFloat("_PlanetaryCutout",TerrainSize*0.625f-0.1f);
            MeshRenderer renderer=planet.AddComponent<MeshRenderer>();renderer.sharedMaterial=material;
            renderer.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;renderer.receiveShadows=false;
            ApplyOrbitalImagery(material);
        }

        private void ApplyOrbitalImagery(Material material)
        {
            if(material==null)return;
            MarsCtxImageryCache.Bind(material);
            if(!MarsRegionSourceCache.HasDynamicImagery)return;
            material.SetTexture("_OrbitalTex",MarsRegionSourceCache.OrbitalTexture);
            material.SetVector("_OrbitalRect",MarsRegionSourceCache.OrbitalUvRect);
            material.SetFloat("_OrbitalBlend",1);
            material.SetFloat("_OrbitalMono",MarsRegionSourceCache.OrbitalIsMonochrome?1:0);
            if(material.HasProperty("_SourceImageSpacing"))material.SetFloat("_SourceImageSpacing",MarsRegionSourceCache.ImageryDisplayMetersPerPixel);
            // Each terrain retains the frame it was built in. A background
            // preparation can temporarily configure another regional frame.
            MarsOfficialTerrain.BindPreciseImagery(material,imageryOrigin,imageryMetresPerUnit);
        }
        private static void ConfigureGeographicMaterial(Material material,GeoPoint origin)
        {
            if(material==null||origin==null)return;
            double lat=origin.latitude*System.Math.PI/180,lon=origin.longitude*System.Math.PI/180;
            double cl=System.Math.Cos(lat),sl=System.Math.Sin(lat),co=System.Math.Cos(lon),si=System.Math.Sin(lon),r=MarsRegionalTerrain.RadiusMeters;
            // Two fixed oblique planetary axes. Binary-rational material periods
            // wrap at 4096 m, allowing small precise phases instead of million-
            // metre floats. The same global point keeps its grain on rebasing.
            double a=r*(cl*co*.67+sl*.31+cl*si*.54),b=r*(-cl*co*.42+sl*.71+cl*si*.37);
            material.SetVector("_MaterialOrigin",new Vector4((float)(a-System.Math.Floor(a/4096)*4096),(float)(b-System.Math.Floor(b/4096)*4096),0,0));
            material.SetVector("_MaterialEast",new Vector4((float)(-si*.67+co*.54),(float)(si*.42+co*.37),0,0));
            material.SetVector("_MaterialNorth",new Vector4((float)(-sl*co*.67+cl*.31-sl*si*.54),(float)(sl*co*.42+cl*.71-sl*si*.37),0,0));
        }
        public void RefreshSourceImagery()
        {
            foreach(MeshRenderer renderer in GetComponentsInChildren<MeshRenderer>())
                foreach(Material material in renderer.sharedMaterials)if(material!=null&&material.HasProperty("_OrbitalTex"))ApplyOrbitalImagery(material);
        }
        public void RefreshMapLayer()
        {if(terrainMaterial!=null)terrainMaterial.SetFloat("_MapLayer",PlanningLayer);}

        private void BuildMapSkirt(Mesh surface,Material material,float size,int resolution)
        {
            var edge=new List<Vector3>();Vector3[] v=surface.vertices;
            for(int i=0;i<resolution;i++)edge.Add(v[i]);
            for(int i=1;i<resolution;i++)edge.Add(v[i*resolution+resolution-1]);
            for(int i=resolution-2;i>=0;i--)edge.Add(v[(resolution-1)*resolution+i]);
            for(int i=resolution-2;i>0;i--)edge.Add(v[i*resolution]);
            float bottom=surface.bounds.min.y-2.5f;
            Vector3[] vertices=new Vector3[edge.Count*2];int[] triangles=new int[edge.Count*6];
            for(int i=0;i<edge.Count;i++)
            {
                vertices[i*2]=edge[i];vertices[i*2+1]=new Vector3(edge[i].x,bottom,edge[i].z);
                int j=(i+1)%edge.Count,a=i*2,b=j*2,t=i*6;
                triangles[t]=a;triangles[t+1]=b;triangles[t+2]=a+1;triangles[t+3]=b;triangles[t+4]=b+1;triangles[t+5]=a+1;
            }
            Mesh mesh=new Mesh{name="Regional relief map edge",vertices=vertices,triangles=triangles};mesh.RecalculateNormals();mesh.RecalculateBounds();
            GameObject skirt=new GameObject("Regional map edge");skirt.transform.SetParent(transform,false);skirt.AddComponent<MeshFilter>().sharedMesh=mesh;
            skirt.AddComponent<MeshRenderer>().sharedMaterial=MarsPathMaterialFactory.CreateTextured(null,new Color(.16f,.11f,.085f),"Relief map section",Vector2.one);
        }

        private void BuildDistantTerrain(Material material,Mesh nearMesh,int nearResolution)
        {
            if(regionalMode&&MarsRegionalTerrain.HumanScale){BuildCartesianContext(material,nearMesh,nearResolution);return;}
            const int segments = 256;
            const int rings = 74;
            Vector3[] vertices = new Vector3[(segments + 1) * rings];
            Vector2[] uvs = new Vector2[vertices.Length];
            Vector2[] geographicUvs=new Vector2[vertices.Length];
            Color[] colors = new Color[vertices.Length];
            int[] triangles = new int[segments * (rings - 1) * 6];

            for (int ring = 0; ring < rings; ring++)
            {
                float ringT = ring / (float)(rings - 1);
                float halfExtent = Mathf.Lerp(VisualTerrainSize*0.5f,MarsRegionalTerrain.HumanScale?24000f:4000f,Mathf.Pow(ringT,2.5f));
                for (int segment = 0; segment <= segments; segment++)
                {
                    float angle = segment / (float)segments * Mathf.PI * 2f;
                    float cosine = Mathf.Cos(angle);
                    float sine = Mathf.Sin(angle);
                    // Concentric square rings meet the rectangular DEM tile at
                    // its exact border. A circular ring overlapped its corners
                    // and produced visible patches in the planning camera.
                    float radius = halfExtent / Mathf.Max(Mathf.Abs(cosine), Mathf.Abs(sine));
                    float x = cosine * radius;
                    float z = sine * radius;
                    float edgeX = Mathf.Clamp(x, -TerrainSize * 0.499f, TerrainSize * 0.499f);
                    float edgeZ = Mathf.Clamp(z, -TerrainSize * 0.499f, TerrainSize * 0.499f);
                    float height = SampleHeight(x,z);
                    int index = ring * (segments + 1) + segment;
                    if(regionalMode){GeoPoint p=MarsRegionalTerrain.ToGeo(x,z);geographicUvs[index]=MarsGeospatial.ToEquirectangularUv(p.latitude,p.longitude);}
                    vertices[index] = new Vector3(x, height, z);
                    uvs[index] = new Vector2(x / TerrainSize + 0.5f, z / TerrainSize + 0.5f);
                    if(regionalMode)uvs[index]=new Vector2(x/18f,z/18f);
                    if(regionalMode&&mission.usesJezeroDem)uvs[index]=MarsRegionalTerrain.FullOrthoUv(x,z);
                    float shade=Mathf.InverseLerp(-3,10,height);
                    float mineral=Mathf.PerlinNoise((x+30)*0.14f,(z-40)*0.14f);
                    colors[index]=regionalMode&&MarsRegionalTerrain.HumanScale?Color.white:Color.Lerp(Color.Lerp(new Color(0.72f,0.58f,0.50f),new Color(1.05f,0.93f,0.82f),shade),new Color(0.94f,0.73f,0.59f),mineral*0.24f);
                }
            }

            int triangleIndex = 0;
            for (int ring = 0; ring < rings - 1; ring++)
            {
                for (int segment = 0; segment < segments; segment++)
                {
                    int current = ring * (segments + 1) + segment;
                    int nextRing = current + segments + 1;
                    // Clockwise winding as seen from above. The previous winding
                    // faced down, so the context terrain vanished and exposed the
                    // square edge of the high-resolution mission tile.
                    triangles[triangleIndex++] = current;
                    triangles[triangleIndex++] = current + 1;
                    triangles[triangleIndex++] = nextRing;
                    triangles[triangleIndex++] = current + 1;
                    triangles[triangleIndex++] = nextRing + 1;
                    triangles[triangleIndex++] = nextRing;
                }
            }

            Mesh mesh = new Mesh { name = "Jezero Distant Context Terrain" };
            mesh.vertices = vertices;
            mesh.uv = uvs;
            mesh.uv2=geographicUvs;
            mesh.colors = colors;
            mesh.triangles = triangles;
            mesh.RecalculateNormals();
            if(regionalMode&&MarsRegionalTerrain.HumanScale)
            {
                // Radial context triangles must not imprint spokes into the
                // lighting. Both sides of the square seam use the same local
                // elevation derivative, rather than triangle-area averages.
                var normals=new Vector3[vertices.Length];const float delta=4f;
                for(int i=0;i<vertices.Length;i++)
                {Vector3 p=vertices[i];float dx=SampleHeight(p.x+delta,p.z)-SampleHeight(p.x-delta,p.z),dz=SampleHeight(p.x,p.z+delta)-SampleHeight(p.x,p.z-delta);normals[i]=new Vector3(-dx,2*delta,-dz).normalized;}
                mesh.normals=normals;
            }
            mesh.RecalculateTangents();
            mesh.RecalculateBounds();
            GameObject distant = new GameObject("Jezero Distant Mesas");
            distant.transform.SetParent(transform, false);
            distant.AddComponent<MeshFilter>().sharedMesh = mesh;
            MeshRenderer renderer = distant.AddComponent<MeshRenderer>();
            if (material != null) renderer.sharedMaterial = material;
        }

        private void BuildCartesianContext(Material material,Mesh nearMesh,int nearResolution)
        {
            // Cartesian bands avoid the radial triangle fans that projected
            // spoke-shaped shading into the horizon. Extend to the actual far
            // crater rims instead of ending every scene at a 24 km square.
            float edge=VisualTerrainSize*.5f,outer=MarsMeasuredHorizon.RadiusMetres;
            List<float> axis=BuildContextAxis(edge);
            int n=axis.Count;var vertices=new Vector3[n*n];var uv=new Vector2[n*n];var geographic=new Vector2[n*n];var colors=new Color[n*n];var triangles=new List<int>();
            int bridgeStart=axis.FindLastIndex(value=>value < -edge);
            int bridgeEnd=n-1-bridgeStart;
            float originElevation=MarsRegionalTerrain.MeasuredElevation(mission.regionCenter);
            for(int z=0;z<n;z++)for(int x=0;x<n;x++)
            {
                int k=z*n+x;float px=axis[x],pz=axis[z];vertices[k]=MarsMeasuredHorizon.Project(px,pz,SampleHeight(px,pz),originElevation);colors[k]=Color.white;
                uv[k]=mission.usesJezeroDem?MarsRegionalTerrain.FullOrthoUv(px,pz):new Vector2(px/18,pz/18);
                GeoPoint p=MarsRegionalTerrain.ToGeo(px,pz);geographic[k]=MarsGeospatial.ToEquirectangularUv(p.latitude,p.longitude);
                if(x==n-1||z==n-1)continue;
                if(x>=bridgeStart&&x<bridgeEnd&&z>=bridgeStart&&z<bridgeEnd)continue;
                float farX=Mathf.Max(Mathf.Abs(px),Mathf.Abs(axis[x+1])),farZ=Mathf.Max(Mathf.Abs(pz),Mathf.Abs(axis[z+1]));
                if(farX*farX+farZ*farZ>outer*outer)continue;
                triangles.Add(k);triangles.Add(k+n);triangles.Add(k+1);triangles.Add(k+1);triangles.Add(k+n);triangles.Add(k+n+1);
            }
            Mesh mesh=new Mesh{name="Measured spherical horizon / 60 km radius",indexFormat=UnityEngine.Rendering.IndexFormat.UInt32,vertices=vertices,uv=uv,uv2=geographic,colors=colors};
            mesh.SetTriangles(triangles,0);
            // Match the near patch's source-aware derivative at the shared seam.
            // Area-weighted normals on the expanding outer cells were flattening
            // the illuminated relief and leaving a visible low-detail boundary.
            var normals=new Vector3[vertices.Length];
            for(int z=0;z<n;z++)for(int x=0;x<n;x++)
            {
                int i=z*n+x;float px=axis[x],pz=axis[z];Vector3 east,north;
                if(measuredNormals!=null&&Mathf.Max(Mathf.Abs(px),Mathf.Abs(pz))<=VisualTerrainSize*.5f+10)
                {
                    normals[i]=measuredNormals.At(px,pz);continue;
                }
                if(Mathf.Max(Mathf.Abs(px),Mathf.Abs(pz))<=500)
                {
                    // The same native-scale 3×3 lighting kernel continues into
                    // the immediate horizon. No separate dense derivative noise
                    // or change to the source positions at the mesh boundary.
                    GeoPoint point=MarsRegionalTerrain.ToGeo(px,pz);
                    float localBaseline=MeasuredNormalBaseline(MarsRegionalTerrain.SourceSpacingMeters(point));
                    normals[i]=MarsMeasuredNormalField.SourceKernelAt(px,pz,localBaseline);continue;
                }
                else
                {
                    // The far grid already samples the measured source. Reuse
                    // its local derivatives instead of four more raster lookups
                    // at every outer vertex; the horizon remains fast to enter.
                    east=vertices[z*n+Mathf.Min(n-1,x+1)]-vertices[z*n+Mathf.Max(0,x-1)];
                    north=vertices[Mathf.Min(n-1,z+1)*n+x]-vertices[Mathf.Max(0,z-1)*n+x];
                }
                normals[i]=Vector3.Cross(north,east).normalized;
            }
            mesh.normals=normals;mesh.RecalculateTangents();mesh.RecalculateBounds();
            GameObject context=new GameObject("Measured terrain horizon");context.transform.SetParent(transform,false);context.AddComponent<MeshFilter>().sharedMesh=mesh;context.AddComponent<MeshRenderer>().sharedMaterial=material;
            BuildContextBridge(material,nearMesh,nearResolution,n,bridgeStart,bridgeEnd,vertices,normals,uv,geographic);
        }

        private void BuildContextBridge(Material material,Mesh nearMesh,int nearResolution,int contextResolution,
            int contextStart,int contextEnd,Vector3[] contextVertices,Vector3[] contextNormals,Vector2[] contextUv,Vector2[] contextGeo)
        {
            // The near patch has metre-spaced border posts and the horizon has a
            // coarser border. Join their actual edges so interpolation differences
            // cannot expose sky or form a step along the shared boundary.
            int outerCount=contextEnd-contextStart+1;
            int sideCount=nearResolution+outerCount;
            var vertices=new Vector3[4*sideCount];
            var normals=new Vector3[vertices.Length];
            var uv=new Vector2[vertices.Length];
            var geographic=new Vector2[vertices.Length];
            var triangles=new List<int>(12*(sideCount-2));
            Vector3[] nearVertices=nearMesh.vertices,nearNormals=nearMesh.normals;
            Vector2[] nearUv=nearMesh.uv,nearGeo=nearMesh.uv2;
            for(int side=0;side<4;side++)
            {
                int inner=side*sideCount,outer=inner+nearResolution;
                for(int i=0;i<nearResolution;i++)
                {
                    int index=SquareEdgeIndex(side,i,0,nearResolution-1,nearResolution);
                    vertices[inner+i]=nearVertices[index];normals[inner+i]=nearNormals[index];
                    uv[inner+i]=nearUv[index];geographic[inner+i]=nearGeo[index];
                }
                for(int i=0;i<outerCount;i++)
                {
                    int index=SquareEdgeIndex(side,i,contextStart,contextEnd,contextResolution);
                    vertices[outer+i]=contextVertices[index];normals[outer+i]=contextNormals[index];
                    uv[outer+i]=contextUv[index];geographic[outer+i]=contextGeo[index];
                }
                int a=0,b=0,lastInner=nearResolution-1,lastOuter=outerCount-1;
                while(a<lastInner||b<lastOuter)
                {
                    triangles.Add(inner+a);
                    bool advanceInner=b==lastOuter||a<lastInner&&(a+1)*lastOuter<=(b+1)*lastInner;
                    if(advanceInner){triangles.Add(inner+ ++a);triangles.Add(outer+b);}
                    else{triangles.Add(outer+b+1);triangles.Add(outer+b);b++;}
                }
            }
            var bridge=new Mesh{name="Measured near-ground to horizon seam",vertices=vertices,normals=normals,uv=uv,uv2=geographic};
            bridge.SetTriangles(triangles,0);bridge.RecalculateTangents();bridge.RecalculateBounds();
            var strip=new GameObject("Measured terrain seam");strip.transform.SetParent(transform,false);
            strip.AddComponent<MeshFilter>().sharedMesh=bridge;
            strip.AddComponent<MeshRenderer>().sharedMaterial=material;
        }

        private static int SquareEdgeIndex(int side,int step,int minimum,int maximum,int stride)
        {
            switch(side)
            {
                case 0:return minimum*stride+minimum+step;
                case 1:return (minimum+step)*stride+maximum;
                case 2:return maximum*stride+maximum-step;
                default:return (maximum-step)*stride+minimum;
            }
        }

        private static List<float> BuildContextAxis(float edge)
        {
            // Preserve nearby rims before spending vertices on the far skyline.
            // Each band samples the available DEM spacing around its footprint,
            // including finer windows away from the astronaut's current cell.
            float[] ends={500,1500,4000,12000,32000,MarsMeasuredHorizon.RadiusMetres};
            float[] minimumSteps={4,20,50,120,320,1000};
            // Spend the bounded mesh on nearby measured rims, rather than
            // imposing the same sample count on every annulus.
            int[] maximumSegments={48,40,40,32,24,20};
            if(MarsMissionZone.IsMission(MarsRegionalTerrain.Current))
            {
                bool headset=Application.platform==RuntimePlatform.Android;
                ends=new[]{360f,800f,1800f,4000f,12000f,32000f,MarsMeasuredHorizon.RadiusMetres};
                minimumSteps=new[]{1.5f,8f,28f,66f,160f,650f,1400f};
                // Concentrate posts on nearby measured rims. The far skyline
                // keeps the same coverage with fewer samples per band.
                maximumSegments=headset?new[]{112,48,27,22,15,12,10}:new[]{120,55,31,24,18,14,12};
            }
            var positive=new List<float>();
            float start=edge;
            for(int band=0;band<ends.Length;band++)
            {
                float end=ends[band],radius=(start+end)*.5f;
                float nativeSpacing=float.MaxValue;
                for(int bearing=0;bearing<360;bearing+=45)
                {
                    GeoPoint point=MarsGeospatial.Destination(MarsRegionalTerrain.Current.regionCenter,bearing,radius/1000);
                    nativeSpacing=Mathf.Min(nativeSpacing,MarsRegionalTerrain.SourceSpacingMeters(point));
                }
                float step=Mathf.Max(nativeSpacing,minimumSteps[band]);
                int segments=Mathf.Clamp(Mathf.CeilToInt((end-start)/step),4,maximumSegments[band]);
                for(int i=1;i<=segments;i++)positive.Add(Mathf.Lerp(start,end,i/(float)segments));
                start=end;
            }
            var axis=new List<float>(positive.Count*2+31);
            for(int i=positive.Count-1;i>=0;i--)axis.Add(-positive[i]);
            for(int i=0;i<=30;i++)axis.Add(Mathf.Lerp(-edge,edge,i/30f));
            axis.AddRange(positive);
            return axis;
        }

        internal static float MeasuredNormalBaseline(float spacing)
        {
            // Coarse source derivatives are not metre-scale slope observations.
            // Average the lighting normal over half a native grid post to avoid
            // emphasizing source-cell/coordinate quantization as repeated bands.
            // No vertex, collider, physical slope or elevation is filtered.
            return spacing>=20?Mathf.Clamp(spacing*.5f,8,40):.5f;
        }

        public static float SampleHeight(float x, float z)
        {
            if (regionalMode) return MarsRegionalTerrain.SampleVisualHeight(x, z);
            float macro = SampleJezeroMacroRelief(x, z);
            float outsideMissionTile = Mathf.Max(Mathf.Abs(x), Mathf.Abs(z)) - TerrainSize * 0.49f;
            float contextBlend = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(outsideMissionTile / 72f));
            float continental = (Mathf.PerlinNoise((x + 190f) * 0.012f, (z - 120f) * 0.012f) - 0.5f) * 13f;
            float brokenGround = (Mathf.PerlinNoise((x - 43f) * 0.041f, (z + 71f) * 0.041f) - 0.5f) * 2.4f;
            float erosion = (Mathf.PerlinNoise((x + 11f) * 0.105f, (z - 29f) * 0.105f) - 0.5f) * 0.58f;
            float ridgeSource = Mathf.PerlinNoise((x - 120f) * 0.025f, (z + 210f) * 0.025f);
            float rockyRidges = Mathf.Pow(1f - Mathf.Abs(ridgeSource * 2f - 1f), 3.2f) * 2.0f;
            float craterField =
                Crater(x, z, 108f, 66f, 23f, 5.8f) +
                Crater(x, z, -124f, 91f, 31f, 7.2f) +
                Crater(x, z, 137f, -82f, 19f, 4.6f) +
                Crater(x, z, -96f, -119f, 27f, 6.1f);
            float contextHeight = continental + brokenGround + erosion + rockyRidges + craterField;
            macro = Mathf.Lerp(macro, contextHeight, contextBlend);
            float detail = (Mathf.PerlinNoise((x + 15f) * 0.11f, (z - 10f) * 0.11f) - 0.5f) * 0.46f;
            float fine = (Mathf.PerlinNoise((x - 11f) * 0.34f, (z + 19f) * 0.34f) - 0.5f) * 0.18f;
            float windRipples = Mathf.Sin((x * 0.72f) + (z * 0.21f)) * 0.045f;
            return macro + detail + fine + windRipples - 2.2f;
        }

        private void OnDestroy()
        {
            // All meshes/materials under this object are generated at runtime.
            HashSet<Mesh> meshes = new HashSet<Mesh>();
            HashSet<Material> materials = new HashSet<Material>();
            foreach (MeshFilter filter in GetComponentsInChildren<MeshFilter>(true)) if (filter.sharedMesh != null) meshes.Add(filter.sharedMesh);
            foreach (Renderer renderer in GetComponentsInChildren<Renderer>(true)) foreach (Material material in renderer.sharedMaterials) if (material != null) materials.Add(material);
            foreach (Mesh mesh in meshes) Destroy(mesh);
            foreach (Material material in materials) Destroy(material);
        }

        private static void LoadJezeroHeightData()
        {
            if (jezeroHeightData != null) return;
            TextAsset heightAsset = Resources.Load<TextAsset>("NASA/JezeroHeight16");
            if (heightAsset != null && heightAsset.bytes.Length == HeightDataResolution * HeightDataResolution * 2)
                jezeroHeightData = heightAsset.bytes;
        }

        private static float SampleJezeroMacroRelief(float x, float z)
        {
            if (jezeroHeightData == null) return 0f;
            float u = Mathf.Clamp01(x / TerrainSize + 0.5f) * (HeightDataResolution - 1);
            float v = Mathf.Clamp01(z / TerrainSize + 0.5f) * (HeightDataResolution - 1);
            int x0 = Mathf.FloorToInt(u);
            int z0 = Mathf.FloorToInt(v);
            int x1 = Mathf.Min(x0 + 1, HeightDataResolution - 1);
            int z1 = Mathf.Min(z0 + 1, HeightDataResolution - 1);
            float tx = u - x0;
            float tz = v - z0;
            float a = ReadHeight16(z0 * HeightDataResolution + x0);
            float b = ReadHeight16(z0 * HeightDataResolution + x1);
            float c = ReadHeight16(z1 * HeightDataResolution + x0);
            float d = ReadHeight16(z1 * HeightDataResolution + x1);
            return (Mathf.Lerp(Mathf.Lerp(a, b, tx), Mathf.Lerp(c, d, tx), tz) - 0.30f) * 10.5f;
        }

        private static float ReadHeight16(int pixelIndex)
        {
            int byteIndex = pixelIndex * 2;
            ushort value = (ushort)(jezeroHeightData[byteIndex] | (jezeroHeightData[byteIndex + 1] << 8));
            return value / 65535f;
        }

        private void BuildJezeroHorizon()
        {
            const int segments = 96;
            // Keep the panorama well beyond the playable terrain so it reads as
            // a distant crater horizon instead of a curved wall above the camera.
            const float radius = 280f;
            const float bottom = -35f;
            const float top = 80f;
            Mesh mesh = new Mesh { name = "NASA Jezero Panorama Cylinder" };
            Vector3[] vertices = new Vector3[(segments + 1) * 2];
            Vector2[] uvs = new Vector2[vertices.Length];
            int[] triangles = new int[segments * 6];
            for (int i = 0; i <= segments; i++)
            {
                float u = i / (float)segments;
                float angle = u * Mathf.PI * 2f;
                float x = Mathf.Sin(angle) * radius;
                float z = Mathf.Cos(angle) * radius;
                vertices[i * 2] = new Vector3(x, bottom, z);
                vertices[i * 2 + 1] = new Vector3(x, top, z);
                uvs[i * 2] = new Vector2(u, 0f);
                uvs[i * 2 + 1] = new Vector2(u, 1f);
                if (i == segments) continue;
                int index = i * 6;
                int vertex = i * 2;
                triangles[index] = vertex;
                triangles[index + 1] = vertex + 1;
                triangles[index + 2] = vertex + 2;
                triangles[index + 3] = vertex + 2;
                triangles[index + 4] = vertex + 1;
                triangles[index + 5] = vertex + 3;
            }
            mesh.vertices = vertices;
            mesh.uv = uvs;
            mesh.triangles = triangles;
            mesh.RecalculateBounds();
            GameObject horizon = new GameObject("NASA Jezero Crater Horizon — PIA24264");
            horizon.transform.SetParent(transform, false);
            horizon.AddComponent<MeshFilter>().sharedMesh = mesh;
            Material material = MarsPathMaterialFactory.CreatePanorama(Resources.Load<Texture2D>("NASA/JezeroHorizon"));
            if (material != null) horizon.AddComponent<MeshRenderer>().sharedMaterial = material;
            else horizon.AddComponent<MeshRenderer>();
        }

        private void BuildRockField()
        {
            Random.State previous = Random.state;
            Random.InitState(5072026);
            Material[] rockMaterials =
            {
                MarsPathMaterialFactory.CreateRock(new Color(0.50f, 0.38f, 0.33f), new Color(0.76f, 0.49f, 0.34f), 0.66f, "Jezero Dark Basalt"),
                MarsPathMaterialFactory.CreateRock(new Color(0.62f, 0.43f, 0.33f), new Color(0.85f, 0.59f, 0.40f), 0.58f, "Jezero Dust-Coated Rock"),
                MarsPathMaterialFactory.CreateRock(new Color(0.68f, 0.56f, 0.49f), new Color(0.90f, 0.68f, 0.51f), 0.46f, "Jezero Weathered Rock")
            };
            GameObject rockRoot = new GameObject("Surface Rocks");
            rockRoot.transform.SetParent(transform, false);

            Mesh[] rockVariants = new Mesh[16];
            for (int i = 0; i < rockVariants.Length; i++) rockVariants[i] = CreateRockMesh(i + 410);

            if(regionalMode&&MarsRegionalTerrain.HumanScale)
            {
                BuildAnchoredRockField(rockRoot.transform,rockVariants,rockMaterials);
                Random.state=previous;return;
            }

            int created = 0;
            int attempts = 0;
            const int targetRockCount = 270;
            while (created < targetRockCount && attempts++ < 3200)
            {
                float x = Random.Range(-TerrainSize * 0.48f, TerrainSize * 0.48f);
                float z = Random.Range(-TerrainSize * 0.48f, TerrainSize * 0.48f);
                bool featureBoulder = created % 23 == 0;
                float scale = featureBoulder
                    ? Random.Range(0.58f, 1.24f)
                    : Mathf.Lerp(0.055f, 0.54f, Mathf.Pow(Random.value, 2.55f));
                float routeClearance = featureBoulder ? 1.25f : Mathf.Lerp(0.28f, 0.74f, Mathf.InverseLerp(0.055f, 0.54f, scale));
                if (IsNearAnyRoute(x, z, routeClearance)) continue;

                SpawnRock(rockRoot.transform, rockVariants, rockMaterials, created, x, z, scale, featureBoulder);
                created++;

                // Natural fields are clustered: large boulders shed smaller angular fragments.
                if (!featureBoulder) continue;
                int fragmentCount = Random.Range(3, 7);
                for (int fragment = 0; fragment < fragmentCount && created < targetRockCount; fragment++)
                {
                    Vector2 offset = Random.insideUnitCircle.normalized * Random.Range(scale * 1.1f, scale * 3.4f);
                    float fragmentX = x + offset.x;
                    float fragmentZ = z + offset.y;
                    float fragmentScale = scale * Random.Range(0.10f, 0.28f);
                    if (Mathf.Abs(fragmentX) > TerrainSize * 0.49f || Mathf.Abs(fragmentZ) > TerrainSize * 0.49f) continue;
                    if (IsNearAnyRoute(fragmentX, fragmentZ, 0.38f)) continue;
                    SpawnRock(rockRoot.transform, rockVariants, rockMaterials, created, fragmentX, fragmentZ, fragmentScale, false);
                    created++;
                }
            }

            // Sparse context geology beyond the high-resolution Jezero mission
            // tile keeps the wider Mars view from reading as an empty sand dune.
            int contextCreated = 0;
            int contextAttempts = 0;
            const int contextRockCount = 260;
            float contextLimit = VisualTerrainSize * 0.47f;
            while (contextCreated < contextRockCount && contextAttempts++ < 3600)
            {
                float x = Random.Range(-contextLimit, contextLimit);
                float z = Random.Range(-contextLimit, contextLimit);
                if (Mathf.Max(Mathf.Abs(x), Mathf.Abs(z)) < TerrainSize * 0.58f) continue;
                bool outcrop = contextCreated % 29 == 0;
                float scale = outcrop
                    ? Random.Range(0.58f, 1.15f)
                    : Mathf.Lerp(0.06f, 0.47f, Mathf.Pow(Random.value, 2.65f));
                SpawnRock(rockRoot.transform, rockVariants, rockMaterials, 1000 + contextCreated, x, z, scale, false);
                contextCreated++;
            }

            Random.state = previous;
        }

        private void BuildAnchoredRockField(Transform root,Mesh[] variants,Material[] materials)
        {
            GeoPoint origin=mission.regionCenter;double radians=System.Math.PI/180,r=MarsRegionalTerrain.RadiusMeters;
            double latitudeBand=System.Math.Floor(origin.latitude/5)*5+2.5;
            double cosine=System.Math.Max(.08,System.Math.Cos(latitudeBand*radians));
            double east=origin.longitude*radians*r*cosine,north=origin.latitude*radians*r;
            int cx=(int)System.Math.Floor(east/7),cz=(int)System.Math.Floor(north/7),created=0;
            var combined=new List<CombineInstance>[materials.Length];for(int i=0;i<combined.Length;i++)combined[i]=new List<CombineInstance>();
            for(int iz=cz-25;iz<=cz+25;iz++)for(int ix=cx-25;ix<=cx+25;ix++)
            {
                int seed;unchecked{seed=(ix*73856093)^(iz*19349663);}
                Random.InitState(seed);if(Random.value>.30f)continue;
                float x=(float)((ix+Random.value)*7-east),z=(float)((iz+Random.value)*7-north);
                if(Mathf.Max(Mathf.Abs(x),Mathf.Abs(z))>172)continue;
                bool large=Random.value<.075f;
                float scale=large?Random.Range(.9f,2.2f):Mathf.Lerp(.08f,.65f,Mathf.Pow(Random.value,2));
                if(IsNearAnyRoute(x,z,large?1.6f:.32f))continue;
                SpawnRock(root,variants,materials,created++,x,z,scale,false);
                Transform rock=root.GetChild(root.childCount-1);MeshFilter filter=rock.GetComponent<MeshFilter>();
                combined[(created-1)%materials.Length].Add(new CombineInstance{mesh=filter.sharedMesh,transform=root.worldToLocalMatrix*rock.localToWorldMatrix});
                rock.gameObject.SetActive(false);Destroy(rock.gameObject);
            }
            for(int i=0;i<materials.Length;i++)
            {
                Mesh mesh=new Mesh{name="Anchored basalt field "+i,indexFormat=UnityEngine.Rendering.IndexFormat.UInt32};mesh.CombineMeshes(combined[i].ToArray(),true,true);
                GameObject batch=new GameObject("Batched anchored geology "+i);batch.transform.SetParent(root,false);batch.AddComponent<MeshFilter>().sharedMesh=mesh;batch.AddComponent<MeshRenderer>().sharedMaterial=materials[i];
            }
            foreach(Mesh mesh in variants)Destroy(mesh);
        }

        private static void SpawnRock(Transform root, Mesh[] variants, Material[] materials, int index, float x, float z, float scale, bool addCollider)
        {
            GameObject rock = new GameObject($"Jezero Rock {index + 1:000}");
            rock.transform.SetParent(root, false);

            Vector3 surfaceNormal = SampleSurfaceNormal(x, z, Mathf.Clamp(scale * 0.24f, 0.14f, 0.55f));
            float burial = scale * Random.Range(0.07f, addCollider ? 0.18f : 0.15f);
            rock.transform.position = new Vector3(x, SampleHeight(x, z) - burial, z);
            rock.transform.localScale = new Vector3(
                scale * Random.Range(0.82f, 1.42f),
                scale * Random.Range(0.62f, 1.08f),
                scale * Random.Range(0.78f, 1.30f));
            if(scale>=0.18f)surfaceRocks.Add(new SurfaceRock{center=rock.transform.position,radius=scale*0.8f});
            Quaternion terrainAlignment = Quaternion.FromToRotation(Vector3.up, surfaceNormal);
            Quaternion weatheredTilt = Quaternion.Euler(Random.Range(-9f, 9f), Random.Range(0f, 360f), Random.Range(-9f, 9f));
            rock.transform.rotation = terrainAlignment * weatheredTilt;

            Mesh mesh = variants[index % variants.Length];
            rock.AddComponent<MeshFilter>().sharedMesh = mesh;
            MeshRenderer renderer = rock.AddComponent<MeshRenderer>();
            Material material = materials[index % materials.Length];
            if (material != null) renderer.sharedMaterial = material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
            renderer.receiveShadows = true;

            MaterialPropertyBlock properties = new MaterialPropertyBlock();
            properties.SetFloat("_Seed", index * 0.731f + 2.17f);
            properties.SetFloat("_Variation", Random.Range(0.42f, 0.86f));
            renderer.SetPropertyBlock(properties);

            if (addCollider)
            {
                MeshCollider collider = rock.AddComponent<MeshCollider>();
                collider.sharedMesh = mesh;
            }
        }

        private static Vector3 SampleSurfaceNormal(float x, float z, float step)
        {
            float left = SampleHeight(x - step, z);
            float right = SampleHeight(x + step, z);
            float back = SampleHeight(x, z - step);
            float forward = SampleHeight(x, z + step);
            return new Vector3(left - right, step * 2f, back - forward).normalized;
        }

        private bool IsNearAnyRoute(float x, float z, float clearance)
        {
            if (mission == null || mission.routes == null) return false;
            Vector2 point = new Vector2(x, z);
            foreach (RouteData route in mission.routes)
            {
                if (route.waypoints == null) continue;
                for (int i = 0; i < route.waypoints.Length - 1; i++)
                {
                    Vector2 a = new Vector2(route.waypoints[i].x, route.waypoints[i].z) * (TerrainSize * 0.5f);
                    Vector2 b = new Vector2(route.waypoints[i + 1].x, route.waypoints[i + 1].z) * (TerrainSize * 0.5f);
                    Vector2 ab = b - a;
                    float t = ab.sqrMagnitude < 0.0001f ? 0f : Mathf.Clamp01(Vector2.Dot(point - a, ab) / ab.sqrMagnitude);
                    if (Vector2.Distance(point, a + ab * t) < clearance) return true;
                }
            }
            return false;
        }

        private static Mesh CreateRockMesh(int seed)
        {
            Random.State previous = Random.state;
            Random.InitState(seed);

            List<Vector3> sourceVertices = new List<Vector3>();
            List<int> sourceTriangles = new List<int>();
            BuildIcosahedron(sourceVertices, sourceTriangles);
            for (int pass = 0; pass < 2; pass++) SubdivideRock(sourceVertices, sourceTriangles);

            int archetype = Mathf.Abs(seed) % 3;
            float width = archetype == 1 ? Random.Range(1.08f, 1.30f) : Random.Range(0.88f, 1.12f);
            float depth = archetype == 2 ? Random.Range(1.04f, 1.24f) : Random.Range(0.84f, 1.10f);
            float height = archetype == 1 ? Random.Range(0.48f, 0.63f) : archetype == 2 ? Random.Range(0.88f, 1.14f) : Random.Range(0.68f, 0.90f);
            Vector3 cutA = Random.onUnitSphere;
            Vector3 cutB = Random.onUnitSphere;
            cutA.y = Mathf.Abs(cutA.y) * 0.55f;
            cutB.y = Mathf.Abs(cutB.y) * 0.42f;
            cutA.Normalize();
            cutB.Normalize();
            float phase = Random.Range(-20f, 20f);

            Vector3[] shaped = new Vector3[sourceVertices.Count];
            for (int i = 0; i < sourceVertices.Count; i++)
            {
                Vector3 direction = sourceVertices[i].normalized;
                float coarse = Mathf.Sin(direction.x * 5.1f + direction.y * 3.7f + direction.z * 4.3f + phase) * 0.075f;
                float medium = Mathf.Sin(direction.x * 11.7f - direction.y * 8.3f + direction.z * 9.1f - phase * 0.37f) * 0.038f;
                float granular = (Mathf.PerlinNoise(direction.x * 3.2f + seed * 0.17f, direction.z * 3.2f - seed * 0.11f) - 0.5f) * 0.09f;
                float radius = 1f + coarse + medium + granular;
                radius -= Mathf.Max(0f, Vector3.Dot(direction, cutA) - 0.58f) * 0.34f;
                radius -= Mathf.Max(0f, Vector3.Dot(direction, cutB) - 0.66f) * 0.25f;

                float vertical01 = (direction.y + 1f) * 0.5f;
                float y = Mathf.Pow(vertical01, 0.90f) * height * radius;
                float crownShear = vertical01 * vertical01;
                shaped[i] = new Vector3(
                    direction.x * width * radius + cutA.x * crownShear * 0.10f,
                    y,
                    direction.z * depth * radius + cutA.z * crownShear * 0.10f);
            }

            // Duplicate triangle vertices so each geological face keeps a subtle
            // plane break. Blended normals prevent a low-poly appearance.
            int triangleCount = sourceTriangles.Count / 3;
            Vector3[] vertices = new Vector3[triangleCount * 3];
            Vector3[] normals = new Vector3[vertices.Length];
            Color[] colors = new Color[vertices.Length];
            int[] triangles = new int[vertices.Length];
            Vector3 visualCenter = new Vector3(0f, height * 0.47f, 0f);
            float normalBlend = archetype == 2 ? 0.80f : 0.87f;

            for (int triangle = 0; triangle < triangleCount; triangle++)
            {
                Vector3 a = shaped[sourceTriangles[triangle * 3]];
                Vector3 b = shaped[sourceTriangles[triangle * 3 + 1]];
                Vector3 c = shaped[sourceTriangles[triangle * 3 + 2]];
                Vector3 faceNormal = Vector3.Cross(b - a, c - a).normalized;
                Vector3[] corners = { a, b, c };
                for (int corner = 0; corner < 3; corner++)
                {
                    int index = triangle * 3 + corner;
                    Vector3 vertex = corners[corner];
                    Vector3 radial = (vertex - visualCenter).normalized;
                    vertices[index] = vertex;
                    normals[index] = Vector3.Slerp(faceNormal, radial, normalBlend).normalized;
                    float dustExposure = Mathf.Clamp01(normals[index].y * 0.55f + 0.45f);
                    float tone = Mathf.Lerp(0.82f, 1.06f, dustExposure);
                    colors[index] = new Color(tone, tone * 0.98f, tone * 0.95f, 1f);
                    triangles[index] = index;
                }
            }

            Mesh mesh = new Mesh { name = $"Geological Mars Boulder {seed}" };
            mesh.vertices = vertices;
            mesh.normals = normals;
            mesh.colors = colors;
            mesh.triangles = triangles;
            mesh.RecalculateBounds();
            Random.state = previous;
            return mesh;
        }

        private static void BuildIcosahedron(List<Vector3> vertices, List<int> triangles)
        {
            float golden = (1f + Mathf.Sqrt(5f)) * 0.5f;
            Vector3[] points =
            {
                new Vector3(-1, golden, 0), new Vector3(1, golden, 0), new Vector3(-1, -golden, 0), new Vector3(1, -golden, 0),
                new Vector3(0, -1, golden), new Vector3(0, 1, golden), new Vector3(0, -1, -golden), new Vector3(0, 1, -golden),
                new Vector3(golden, 0, -1), new Vector3(golden, 0, 1), new Vector3(-golden, 0, -1), new Vector3(-golden, 0, 1)
            };
            foreach (Vector3 point in points) vertices.Add(point.normalized);
            triangles.AddRange(new[]
            {
                0,11,5, 0,5,1, 0,1,7, 0,7,10, 0,10,11,
                1,5,9, 5,11,4, 11,10,2, 10,7,6, 7,1,8,
                3,9,4, 3,4,2, 3,2,6, 3,6,8, 3,8,9,
                4,9,5, 2,4,11, 6,2,10, 8,6,7, 9,8,1
            });
        }

        private static void SubdivideRock(List<Vector3> vertices, List<int> triangles)
        {
            Dictionary<long, int> midpointCache = new Dictionary<long, int>();
            List<int> refined = new List<int>(triangles.Count * 4);
            for (int i = 0; i < triangles.Count; i += 3)
            {
                int a = triangles[i];
                int b = triangles[i + 1];
                int c = triangles[i + 2];
                int ab = RockMidpoint(a, b, vertices, midpointCache);
                int bc = RockMidpoint(b, c, vertices, midpointCache);
                int ca = RockMidpoint(c, a, vertices, midpointCache);
                refined.AddRange(new[] { a, ab, ca, b, bc, ab, c, ca, bc, ab, bc, ca });
            }
            triangles.Clear();
            triangles.AddRange(refined);
        }

        private static int RockMidpoint(int first, int second, List<Vector3> vertices, Dictionary<long, int> cache)
        {
            int small = Mathf.Min(first, second);
            int large = Mathf.Max(first, second);
            long key = ((long)small << 32) + (uint)large;
            if (cache.TryGetValue(key, out int existing)) return existing;
            int index = vertices.Count;
            vertices.Add(((vertices[first] + vertices[second]) * 0.5f).normalized);
            cache[key] = index;
            return index;
        }

        private static float Crater(float x, float z, float cx, float cz, float radius, float depth)
        {
            float distance = Vector2.Distance(new Vector2(x, z), new Vector2(cx, cz));
            float bowl = -depth * Mathf.Clamp01(1f - distance / radius);
            float rim = depth * 0.35f * Mathf.Exp(-Mathf.Pow((distance - radius) / 2.2f, 2f));
            return bowl + rim;
        }
    }
}

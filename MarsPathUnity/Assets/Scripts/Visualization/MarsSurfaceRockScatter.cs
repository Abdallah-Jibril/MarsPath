using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using UnityEngine;
using UnityEngine.Rendering;

namespace MarsPath
{
    /// <summary>
    /// Illustrative, metre-scale rocks on the measured human-scale surface.
    /// The DEM does not resolve these individual objects: their positions and
    /// shapes are modeled, and must not be used by the scientific route planner.
    /// </summary>
    public sealed class MarsSurfaceRockScatter : MonoBehaviour
    {
        public const string DetailNotice = "The modeled rock fields illustrate rover-observed forms and clustering. Alerts count exposed models in the approach corridor. Individual locations are illustrative.";
        public const int MaximumRockCount = 224;
        // Cells locate whole fragment patches, never one evenly spaced stone.
        // Their fixed geographic frame keeps the same visible patch on rebases.
        private const double CellMetres = 22.0;
        private const double ClusterReachMetres = 15.0;
        private const float FieldRadiusMetres = 54f;
        private const float RefreshDistanceMetres = 9f;
        private const int VariantCount = 12;
        private const int MaxPatchFragments = 26;
        private const double Radians = Math.PI / 180.0;
        private const float AlertRangeMetres = 16f;
        private const float MinimumExposedRockMetres = .10f;

        /// <summary>A footprint of one currently rendered model, not a measured Mars hazard.</summary>
        public readonly struct RockObstacle
        {
            public readonly ulong GeographicId;
            public readonly double Latitude;
            public readonly double Longitude;
            public readonly Vector3 Center;
            public readonly float Radius;
            public readonly float HeightMetres;
            internal RockObstacle(ulong id, double latitude, double longitude, Vector3 center, float radius, float height)
            { GeographicId = id; Latitude = latitude; Longitude = longitude; Center = center; Radius = radius; HeightMetres = height; }
        }

        private struct Candidate
        {
            public ulong id;
            public uint seed;
            public double latitude, longitude;
            public Vector2 position;
            public float diameter, distanceSquared;
            public float yaw, burial;
            public int variant, material;
        }

        private sealed class PooledRock
        {
            public Transform transform;
            public MeshFilter filter;
            public MeshRenderer renderer;
        }

        private static MarsSurfaceRockScatter active;
        private static readonly RockObstacle[] EmptyRocks = new RockObstacle[0];
        private readonly List<RockObstacle> obstacles = new List<RockObstacle>(MaximumRockCount);
        private readonly List<PooledRock> pool = new List<PooledRock>(MaximumRockCount);
        private readonly List<Candidate> candidates = new List<Candidate>(400);
        private readonly Vector3[] patchFootprints = new Vector3[MaxPatchFragments];
        private ReadOnlyCollection<RockObstacle> readOnlyObstacles;
        private Mesh[] variants;
        private Vector3[][] supportVertices;
        private Material[] materials;
        private MissionData mission;
        private GeoPoint origin;
        private Vector2 lastFocus;
        private float metresPerUnit = 1f, normalStep = 2f;
        private bool built, jezeroZone;

        public IReadOnlyList<RockObstacle> Obstacles => readOnlyObstacles ?? (readOnlyObstacles = obstacles.AsReadOnly());
        public static IReadOnlyList<RockObstacle> VisibleRocks => IsActive ? active.Obstacles : EmptyRocks;
        public static int SurfaceRockCount => IsActive ? active.obstacles.Count : 0;
        private static bool IsActive => active != null && active.isActiveAndEnabled && active.built
            && MarsRegionalTerrain.HumanScale && ReferenceEquals(active.mission, MarsRegionalTerrain.Current);

        /// <summary>Call after configuring/building the measured human-scale terrain.</summary>
        public void Build(MissionData sourceMission)
        {
            ClearOwnedObjects();
            mission = sourceMission;
            built = false;
            if (sourceMission == null || !sourceMission.generatedRegion || !MarsRegionalTerrain.HumanScale
                || !ReferenceEquals(sourceMission, MarsRegionalTerrain.Current)) return;
            origin = sourceMission.regionCenter ?? sourceMission.currentLocation;
            if (origin == null) return;

            metresPerUnit = Mathf.Max(1f, MarsRegionalTerrain.MetersPerUnit);
            normalStep = Mathf.Clamp(MarsRegionalTerrain.SourceSpacingMeters(origin) * 0.1f, 0.6f, 5f) / metresPerUnit;
            jezeroZone = MarsMissionZone.IsMission(sourceMission);
            variants = new Mesh[VariantCount];
            supportVertices = new Vector3[VariantCount][];
            for (int i = 0; i < variants.Length; i++) variants[i] = CreateRockMesh(i, jezeroZone, out supportVertices[i]);
            Texture2D grain = LoadRockGrain(jezeroZone, out bool roverGrain);
            if (jezeroZone)
            {
                materials = new[]
                {
                    CreateMaterial(new Color(.31f,.28f,.25f),new Color(.57f,.43f,.33f),.46f,.08f,"Illustrative fractured dark rock",grain,roverGrain),
                    CreateMaterial(new Color(.43f,.32f,.26f),new Color(.68f,.49f,.35f),.61f,.24f,"Illustrative iron-stained rock",grain,roverGrain),
                    CreateMaterial(new Color(.51f,.44f,.37f),new Color(.70f,.56f,.43f),.55f,.85f,"Illustrative layered rock",grain,roverGrain)
                };
            }
            else materials = new[]
            {
                CreateMaterial(new Color(.31f,.28f,.25f),new Color(.63f,.46f,.33f),.58f,1f,"Illustrative dark basalt",grain,false),
                CreateMaterial(new Color(.47f,.34f,.26f),new Color(.71f,.52f,.35f),.76f,1f,"Illustrative dust-coated stone",grain,false),
                CreateMaterial(new Color(.52f,.44f,.35f),new Color(.75f,.58f,.42f),.68f,1f,"Illustrative weathered stone",grain,false)
            };
            built = true;
            active = this;
            Refresh(Vector2.zero);
        }

        /// <summary>
        /// Call after moving the astronaut. Work occurs only after nine metres;
        /// the same geographic cells persist when the human-scale origin rebases.
        /// </summary>
        public static void SetFocus(Vector3 worldPosition)
        {
            if (!IsActive) return;
            Vector3 local = active.transform.InverseTransformPoint(worldPosition);
            Vector2 focus = new Vector2(local.x, local.z);
            if ((focus - active.lastFocus).sqrMagnitude * active.metresPerUnit * active.metresPerUnit
                >= RefreshDistanceMetres * RefreshDistanceMetres) active.Refresh(focus);
        }

        /// <summary>Tests only currently rendered models. Distance is in physical metres.</summary>
        public static bool TryRockAhead(Vector3 position, Vector3 forward, out float distance)
        {
            return TryRockAhead(position, forward, out distance, out _);
        }

        public static bool TryRockAhead(Vector3 position, Vector3 forward, out float distance, out int fieldCount)
        {
            distance = float.MaxValue;
            fieldCount = 0;
            if (!IsActive) return false;
            forward.y = 0f;
            if (forward.sqrMagnitude < 0.0001f) return false;
            forward.Normalize();
            float units = active.metresPerUnit;
            float range = AlertRangeMetres / units;
            float closestBoulder = float.MaxValue;
            float closestFieldRock = float.MaxValue;
            float coveredArea = 0f;
            int exposedCount = 0;
            Vector3 eye = position;
            eye.y = MarsTerrainGenerator.SampleHeight(eye.x, eye.z) + 1.55f / units;
            foreach (RockObstacle rock in active.obstacles)
            {
                float radiusMetres = rock.Radius * units;
                if (rock.HeightMetres < MinimumExposedRockMetres || radiusMetres < .12f) continue;
                Vector3 offset = rock.Center - position;
                offset.y = 0f;
                float along = Vector3.Dot(offset, forward);
                if (along <= 0f || along > range + rock.Radius) continue;
                float lateralSquared = Mathf.Max(0f, offset.sqrMagnitude - along * along);
                // A widening forward corridor admits a visible debris field on
                // either shoulder without counting stones behind the observer.
                float corridor = rock.Radius + Mathf.Clamp(along * .53f, .9f / units, 4.8f / units);
                if (lateralSquared > corridor * corridor || !VisibleAboveGround(eye, rock, units)) continue;

                float clearance = Mathf.Max(0f, offset.magnitude - rock.Radius) * units;
                closestFieldRock = Mathf.Min(closestFieldRock, clearance);
                exposedCount++;
                coveredArea += Mathf.PI * radiusMetres * radiusMetres;
                float walkShoulder = rock.Radius + 1.9f / units;
                if (clearance <= 11f && rock.HeightMetres >= .22f && radiusMetres >= .24f &&
                    lateralSquared <= walkShoulder * walkShoulder)
                    closestBoulder = Mathf.Min(closestBoulder, clearance);
            }
            // Several visible stones and their collective footprint make a
            // field. One large obstacle gets its own singular warning.
            if (exposedCount >= 4 && coveredArea >= 1.2f && closestFieldRock <= 12f)
            {
                fieldCount = exposedCount;
                distance = closestFieldRock;
                return true;
            }
            distance = closestBoulder;
            return distance < float.MaxValue;
        }

        private static bool VisibleAboveGround(Vector3 eye, RockObstacle rock, float metresPerUnit)
        {
            Vector3 top = rock.Center;
            top.y = MarsTerrainGenerator.SampleHeight(top.x, top.z) + rock.HeightMetres / metresPerUnit;
            for (int i = 1; i <= 3; i++)
            {
                Vector3 sample = Vector3.Lerp(eye, top, i * .22f);
                if (MarsTerrainGenerator.SampleHeight(sample.x, sample.z) > sample.y + .06f / metresPerUnit)
                    return false;
            }
            return true;
        }

        public static bool RockSensorSelfCheck()
        {
            if (!IsActive) return false;
            foreach (RockObstacle rock in active.obstacles)
            {
                if (rock.HeightMetres < .22f || rock.Radius * active.metresPerUnit < .24f) continue;
                Vector3 position = rock.Center - Vector3.forward * (rock.Radius + 3f / active.metresPerUnit);
                if (TryRockAhead(position, Vector3.forward, out float distance) && distance <= 3.1f) return true;
            }
            return false;
        }

        private void Refresh(Vector2 focus)
        {
            lastFocus = focus;
            candidates.Clear();
            // Latitude rows and longitude cells are fixed on Mars, independent
            // of patch origin. Double precision avoids sub-metre float-geo jumps.
            GeoCoordinates(focus, out double focusLatitude, out double focusLongitude);
            double north = (focusLatitude + Math.PI * 0.5) * MarsRegionalTerrain.RadiusMeters;
            int rowMin = Math.Max(0, (int)Math.Floor((north - FieldRadiusMetres - CellMetres - ClusterReachMetres) / CellMetres));
            int maximumRow = (int)Math.Floor(Math.PI * MarsRegionalTerrain.RadiusMeters / CellMetres);
            int rowMax = Math.Min(maximumRow, (int)Math.Floor((north + FieldRadiusMetres + CellMetres + ClusterReachMetres) / CellMetres));
            double polarRadius = MarsRegionalTerrain.RadiusMeters * Math.Cos(focusLatitude);
            double longitudeRange = polarRadius < FieldRadiusMetres * 2 ? Math.PI
                : Math.Min(Math.PI, (FieldRadiusMetres + CellMetres * 2 + ClusterReachMetres) / polarRadius);

            for (int row = rowMin; row <= rowMax; row++)
            {
                double rowLatitude = Math.Min(Math.PI * 0.5, (row + 0.5) * CellMetres / MarsRegionalTerrain.RadiusMeters - Math.PI * 0.5);
                int columns = Math.Max(1, (int)Math.Round(2 * Math.PI * MarsRegionalTerrain.RadiusMeters * Math.Cos(rowLatitude) / CellMetres));
                int centerColumn = (int)Math.Floor((focusLongitude + Math.PI) / (2 * Math.PI) * columns);
                int halfColumns = Math.Min(columns / 2 + 1, (int)Math.Ceiling(longitudeRange / (2 * Math.PI) * columns) + 2);
                int visitCount = Math.Min(columns, halfColumns * 2 + 1);
                for (int i = 0; i < visitCount; i++)
                {
                    int column = Mod(centerColumn - halfColumns + i, columns);
                    uint cellSeed = Hash(unchecked((uint)row * 73856093u ^ (uint)column * 19349663u));
                    // Broad bare pockets alternate with locally denser patches.
                    // No regular six-metre spacing survives into the models.
                    if (!jezeroZone && Unit(cellSeed ^ 0x91e10da5u) > .86f) continue;
                    double latitude = (((double)row + .12 + Unit(cellSeed ^ 0x73aaf061u) * .76) * CellMetres / MarsRegionalTerrain.RadiusMeters) - Math.PI * 0.5;
                    latitude = Math.Max(-Math.PI * 0.5, Math.Min(Math.PI * 0.5, latitude));
                    double longitude = (((double)column + .12 + Unit(cellSeed ^ 0x89ab42d1u) * .76) / columns) * (2 * Math.PI) - Math.PI;
                    ulong id = ((ulong)(uint)row << 32) | (uint)column;
                    if (jezeroZone)
                    {
                        AddJezeroPatch(id, cellSeed, latitude, longitude, focus);
                        continue;
                    }
                    double bearing = Unit(cellSeed ^ 0x95b51e21u) * Math.PI * 2;
                    float reach = Mathf.Lerp(4.0f, 11.5f, Unit(cellSeed ^ 0x68bc21ebu));
                    float width = reach * Mathf.Lerp(.25f, .72f, Unit(cellSeed ^ 0x37d614a3u));
                    bool exposedPatch = Unit(cellSeed ^ 0x3c6ef372u) < .24f;
                    bool slabFamily = exposedPatch || Unit(cellSeed ^ 0x22fb45b7u) < .58f;
                    int material = (int)(Hash(cellSeed ^ 0x71e92f6du) % (uint)materials.Length);
                    int family = (int)(Hash(cellSeed ^ 0x84b2e719u) % 4u);
                    int fragments = 5 + (int)(Unit(cellSeed ^ 0x17af321bu) * 8);
                    for (int fragment = 0; fragment < fragments; fragment++)
                    {
                        uint fragmentSeed = Hash(cellSeed ^ unchecked((uint)(fragment + 1) * 0x9e3779b9u));
                        double angle = Unit(fragmentSeed ^ 0x08bf1249u) * Math.PI * 2;
                        // Square root gives a filled irregular ellipse; two
                        // overlapping lobes avoid a conspicuous ring of debris.
                        double radius = Math.Sqrt(Unit(fragmentSeed ^ 0x481de7a3u));
                        double along = Math.Cos(angle) * radius * reach;
                        double across = Math.Sin(angle) * radius * width;
                        if (fragment % 3 == 0) along = along * .4 + reach * .24;
                        if (exposedPatch && fragment == 0) { along = 0; across = 0; }
                        double eastOffset = Math.Sin(bearing) * along + Math.Cos(bearing) * across;
                        double northOffset = Math.Cos(bearing) * along - Math.Sin(bearing) * across;
                        OffsetGeographic(latitude, longitude, eastOffset, northOffset, out double fragmentLat, out double fragmentLon);
                        float population = Unit(fragmentSeed ^ 0x7b42a069u);
                        bool boulder = population < .035f;
                        bool plate = exposedPatch && fragment == 0;
                        float size = Unit(fragmentSeed ^ 0xa24baed5u);
                        float diameter = plate ? Mathf.Lerp(1.45f, 2.6f, size)
                            : boulder ? Mathf.Lerp(1.0f, 1.85f, size)
                            : population < .18f ? Mathf.Lerp(.52f, .94f, size)
                            : Mathf.Lerp(.10f, .51f, Mathf.Pow(size, 1.65f));
                        int archetype = plate || slabFamily && !boulder ? 0 : boulder ? 1 : (int)(fragmentSeed % 2u) + 1;
                        int variant = family * 3 + archetype;
                        float yaw = (float)(bearing / Radians) + Mathf.Lerp(-24f, 24f, Unit(fragmentSeed ^ 0x51ed270bu));
                        float burial = plate ? .18f : boulder ? .10f : Mathf.Lerp(.075f, .14f, Unit(fragmentSeed ^ 0x02e5be93u));
                        // Adjacent chips share their parent rock's material and
                        // fracture direction, rather than alternating colours.
                        AddCandidate(id * 16 + (uint)fragment, fragmentSeed, fragmentLat, fragmentLon, diameter, focus, boulder || plate,
                            variant, material, yaw, burial);
                    }
                }
            }

            candidates.Sort((a, b) =>
            {
                int distance = a.distanceSquared.CompareTo(b.distanceSquared);
                return distance != 0 ? distance : a.id.CompareTo(b.id);
            });
            obstacles.Clear();
            int count = 0;
            for (int i = 0; i < candidates.Count && count < MaximumRockCount; i++)
                if (Place(candidates[i], count)) count++;
            for (int i = count; i < pool.Count; i++) pool[i].transform.gameObject.SetActive(false);
        }

        private void AddJezeroPatch(ulong id, uint seed, double latitude, double longitude, Vector2 focus)
        {
            Vector2 center = ToLocal(latitude, longitude);
            float range = (FieldRadiusMetres + (float)ClusterReachMetres) / metresPerUnit;
            if ((center - focus).sqrMagnitude > range * range) return;

            // Orient some fragment patches downslope using measured heights.
            // This controls the illustrative scatter, not measured hazard data.
            const float sampleMetres = 4f;
            float step = sampleMetres / metresPerUnit;
            Vector2 downhill = new Vector2(
                MarsTerrainGenerator.SampleHeight(center.x - step, center.y) - MarsTerrainGenerator.SampleHeight(center.x + step, center.y),
                MarsTerrainGenerator.SampleHeight(center.x, center.y - step) - MarsTerrainGenerator.SampleHeight(center.x, center.y + step));
            float grade = downhill.magnitude / (2 * step);
            float gradeWeight = Mathf.InverseLerp(.04f, .28f, grade);
            float deposit = JezeroRockDeposit(latitude, longitude);
            float patchChance = Mathf.Lerp(.23f, .91f, Mathf.InverseLerp(.25f, .73f, deposit + gradeWeight * .13f));
            if (Unit(seed ^ 0x8e7bace9u) > patchChance) return;

            bool scree = grade > .11f && Unit(seed ^ 0x68bc21ebu) > .21f;
            double bearing = scree
                ? Math.Atan2(downhill.x, downhill.y) + (Unit(seed ^ 0x95b51e21u) - .5) * .9
                : Unit(seed ^ 0x95b51e21u) * Math.PI * 2;
            float spread = Mathf.Lerp(3.4f, scree ? 10f : 6.5f, Unit(seed ^ 0x17af321bu));
            float anchorSize = Mathf.Lerp(.88f, 3.15f, Mathf.Pow(Unit(seed ^ 0x22fb45b7u), 1.38f));
            int family = (int)(Hash(seed ^ 0x84b2e719u) % 4u);
            bool bedded = Unit(seed ^ 0x71e92f6du) < .47f;
            int material = bedded ? 2 : (int)(Hash(seed ^ 0x13ec25d1u) % 2u);
            int boulders = scree ? 3 : 2;
            int fragments = scree ? MaxPatchFragments : 19;
            int footprintCount = 0;
            for (int i = 0; i < fragments; i++)
            {
                uint fragment = Hash(seed ^ unchecked((uint)(i + 1) * 0x9e3779b9u));
                bool anchor = i == 0;
                bool boulder = i < boulders;
                float size = Unit(fragment ^ 0xa24baed5u);
                float diameter = anchor ? anchorSize : boulder ? Mathf.Lerp(.59f, 1.66f, size)
                    : i < 10 ? Mathf.Lerp(.28f, .87f, size) : Mathf.Lerp(.13f, .47f, Mathf.Pow(size, 1.7f));
                double azimuth = Unit(fragment ^ 0x08bf1249u) * Math.PI * 2;
                double radius = Math.Pow(Unit(fragment ^ 0x481de7a3u), 1.15);
                // Related blocks sit across an uneven apron of fragments. On
                // slopes the apron is longer downhill than it is across grade.
                double along = anchor ? 0 : boulder ? spread * (.26 + i * .30) * (i == 2 ? -.72 : 1)
                    : Math.Cos(azimuth) * spread * radius + spread * (scree ? .40 : .10);
                double across = anchor ? 0 : boulder ? spread * (i == 2 ? -.29 : .22)
                    : Math.Sin(azimuth) * spread * (scree ? .36 : .68) * radius;
                // Fit debris around its parent blocks before view-distance culling.
                // A moving terrain window cannot reorder this local arrangement.
                if (!TryReservePatchFootprint((float)along, (float)across, diameter, ref footprintCount)) continue;
                double east = Math.Sin(bearing) * along + Math.Cos(bearing) * across;
                double north = Math.Cos(bearing) * along - Math.Sin(bearing) * across;
                OffsetGeographic(latitude, longitude, east, north, out double fragmentLat, out double fragmentLon);
                // A bedded block tends to split into plates. The massive dark
                // rocks form taller isolated blocks and mixed angular debris.
                int profile = bedded && i != 1 && i % 3 != 2 ? 0
                    : boulder ? 1 + (int)(fragment % 2u) : (int)(fragment % 3u);
                float yawSpread = bedded ? 24f : 75f;
                float yaw = (float)(bearing / Radians) + Mathf.Lerp(-yawSpread, yawSpread, Unit(fragment ^ 0x51ed270bu));
                float burial = Mathf.Lerp(boulder ? .06f : .035f, boulder ? .19f : .12f, Unit(fragment ^ 0x02e5be93u));
                int fragmentMaterial = !boulder && !bedded && i % 6 == 0 ? 1 - material : material;
                AddCandidate(id * 32 + (uint)i, fragment, fragmentLat, fragmentLon, diameter, focus,
                    boulder, family * 3 + profile, fragmentMaterial, yaw, burial);
            }
        }

        private bool TryReservePatchFootprint(float along, float across, float diameter, ref int count)
        {
            for (int i = 0; i < count; i++)
            {
                Vector3 prior = patchFootprints[i];
                float x = along - prior.x, z = across - prior.y;
                float clearance = (diameter + prior.z) * .46f;
                if (x * x + z * z < clearance * clearance) return false;
            }
            patchFootprints[count++] = new Vector3(along, across, diameter);
            return true;
        }

        internal static float JezeroRockDeposit(double latitude, double longitude)
        {
            float east = (float)((longitude - MarsMissionZone.West * Radians) *
                Math.Cos(latitude) * MarsRegionalTerrain.RadiusMeters);
            float north = (float)((latitude - MarsMissionZone.South * Radians) * MarsRegionalTerrain.RadiusMeters);
            float apron = Mathf.PerlinNoise(east * .022f + 17.4f, north * .019f + 5.9f);
            float apronBreaks = Mathf.PerlinNoise(east * .081f - north * .037f + 41.2f,
                north * .063f + east * .027f + 29.7f);
            return Mathf.Lerp(apron, apronBreaks, .25f);
        }

        private void AddCandidate(ulong id, uint seed, double latitude, double longitude, float diameter, Vector2 focus, bool boulder,
            int variant, int material, float yaw, float burial)
        {
            Vector2 local = ToLocal(latitude, longitude);
            float distanceSquared = (local - focus).sqrMagnitude;
            if (distanceSquared * metresPerUnit * metresPerUnit > FieldRadiusMetres * FieldRadiusMetres) return;
            // Keep the whole model out of the walk corridor. Rocks are visual
            // only: they never modify the measured collider or planned route.
            // Keep a traversable centre, not the former road-width cleared lane.
            // Small gravel may sit beside the boots; these decorative objects
            // never erase DEM detail or participate in scientific route costs.
            float clearance = ((boulder ? 1.1f : 0.55f) + diameter * 0.55f) / metresPerUnit;
            if (NearRoute(local, clearance)) return;
            candidates.Add(new Candidate { id = id, seed = seed, latitude = latitude / Radians, longitude = longitude / Radians,
                position = local, diameter = diameter, distanceSquared = jezeroZone && boulder ? distanceSquared * .68f : distanceSquared,
                variant = variant, material = material, yaw = yaw, burial = burial });
        }

        private bool Place(Candidate candidate, int index)
        {
            float x = candidate.position.x, z = candidate.position.y;
            float height = MarsTerrainGenerator.SampleHeight(x, z);
            Vector3 normal = new Vector3(MarsTerrainGenerator.SampleHeight(x - normalStep, z) - MarsTerrainGenerator.SampleHeight(x + normalStep, z),
                normalStep * 2f, MarsTerrainGenerator.SampleHeight(x, z - normalStep) - MarsTerrainGenerator.SampleHeight(x, z + normalStep)).normalized;
            if (float.IsNaN(height) || float.IsInfinity(height) || normal.y < 0.74f) return false;
            int variant = candidate.variant;
            float size = candidate.diameter / metresPerUnit;
            Vector3 scale = new Vector3(size * Mathf.Lerp(0.90f, 1.14f, Unit(candidate.seed ^ 0xab34f1d5u)),
                size * Mathf.Lerp(0.76f, 1.12f, Unit(candidate.seed ^ 0x81d42a9bu)), size * Mathf.Lerp(0.88f, 1.10f, Unit(candidate.seed ^ 0xd31781a1u)));
            bool beddedSlab = candidate.material == 2 && variant % 3 == 0;
            float tilt = jezeroZone ? beddedSlab ? 11f : 17f : 4f;
            Vector3 restingNormal = jezeroZone && !beddedSlab ? Vector3.Slerp(Vector3.up, normal, .62f) : normal;
            Quaternion rotation = Quaternion.FromToRotation(Vector3.up, restingNormal)
                * Quaternion.Euler(Mathf.Lerp(-tilt, tilt, Unit(candidate.seed ^ 0x95b01a15u)),
                    candidate.yaw, Mathf.Lerp(-tilt, tilt, Unit(candidate.seed ^ 0x7769ba1du)));
            float support = float.MaxValue, footprint = 0f, minY = float.MaxValue, maxY = float.MinValue;
            foreach (Vector3 vertex in supportVertices[variant])
            {
                Vector3 shaped = rotation * Vector3.Scale(vertex, scale);
                support = Mathf.Min(support, Vector3.Dot(shaped, normal));
                footprint = Mathf.Max(footprint, shaped.x * shaped.x + shaped.z * shaped.z);
                minY = Mathf.Min(minY, shaped.y); maxY = Mathf.Max(maxY, shaped.y);
            }
            // Partially exposed rock, not a whole pebble balanced on its tip.
            // Cap embedding by the actual shape height so the smallest slabs
            // remain visible. This never moves the measured surface or collider.
            float embedding = Mathf.Min(size * candidate.burial, (maxY - minY) * .58f);
            Vector3 position = new Vector3(x, height - support / normal.y - embedding, z);
            if (index == pool.Count)
            {
                GameObject rock = new GameObject("Illustrative anchored rock");
                rock.layer = gameObject.layer;
                rock.transform.SetParent(transform, false);
                MeshRenderer renderer = rock.AddComponent<MeshRenderer>();
                renderer.shadowCastingMode = ShadowCastingMode.On;
                renderer.receiveShadows = true;
                pool.Add(new PooledRock { transform = rock.transform, filter = rock.AddComponent<MeshFilter>(), renderer = renderer });
            }
            PooledRock pooled = pool[index];
            pooled.transform.SetLocalPositionAndRotation(position, rotation);
            pooled.transform.localScale = scale;
            pooled.filter.sharedMesh = variants[variant];
            pooled.renderer.sharedMaterial = materials[candidate.material];
            pooled.transform.gameObject.SetActive(true);
            Vector3 worldCenter = transform.TransformPoint(position + Vector3.up * ((minY + maxY) * 0.5f));
            float worldRadius = Mathf.Sqrt(footprint) * Mathf.Max(Mathf.Abs(transform.lossyScale.x), Mathf.Abs(transform.lossyScale.z));
            float exposedHeight = Mathf.Max(0, position.y + maxY - height) * metresPerUnit;
            obstacles.Add(new RockObstacle(candidate.id, candidate.latitude, candidate.longitude, worldCenter, worldRadius, exposedHeight));
            return true;
        }

        private static void OffsetGeographic(double latitude, double longitude, double eastMetres, double northMetres,
            out double resultLatitude, out double resultLongitude)
        {
            // Spherical destination remains stable at high latitudes and the
            // dateline; longitude/cos(latitude) offsets become singular there.
            double range = Math.Sqrt(eastMetres * eastMetres + northMetres * northMetres);
            double angle = range / MarsRegionalTerrain.RadiusMeters, bearing = Math.Atan2(eastMetres, northMetres);
            resultLatitude = Math.Asin(Math.Max(-1, Math.Min(1, Math.Sin(latitude) * Math.Cos(angle)
                + Math.Cos(latitude) * Math.Sin(angle) * Math.Cos(bearing))));
            resultLongitude = WrapRadians(longitude + Math.Atan2(Math.Sin(bearing) * Math.Sin(angle) * Math.Cos(latitude),
                Math.Cos(angle) - Math.Sin(latitude) * Math.Sin(resultLatitude)));
        }

        private bool NearRoute(Vector2 point, float clearance)
        {
            if (mission.routes == null) return false;
            foreach (RouteData route in mission.routes)
            {
                if (route?.waypoints == null) continue;
                for (int i = 0; i < route.waypoints.Length - 1; i++)
                {
                    Vector2 a = new Vector2(route.waypoints[i].x, route.waypoints[i].z) * (MarsTerrainGenerator.TerrainSize * 0.5f);
                    Vector2 b = new Vector2(route.waypoints[i + 1].x, route.waypoints[i + 1].z) * (MarsTerrainGenerator.TerrainSize * 0.5f);
                    Vector2 ab = b - a;
                    float t = ab.sqrMagnitude < 0.000001f ? 0f : Mathf.Clamp01(Vector2.Dot(point - a, ab) / ab.sqrMagnitude);
                    if ((point - a - ab * t).sqrMagnitude < clearance * clearance) return true;
                }
            }
            return false;
        }

        private Vector2 ToLocal(double latitude, double longitude)
        {
            double lat0 = origin.latitude * Radians, lon0 = origin.longitude * Radians;
            double delta = WrapRadians(longitude - lon0);
            double a = Math.Sin((latitude - lat0) * 0.5), b = Math.Sin(delta * 0.5);
            double h = Math.Max(0, Math.Min(1, a * a + Math.Cos(lat0) * Math.Cos(latitude) * b * b));
            double distance = 2 * Math.Atan2(Math.Sqrt(h), Math.Sqrt(1 - h)) * MarsRegionalTerrain.RadiusMeters / metresPerUnit;
            double bearing = Math.Atan2(Math.Sin(delta) * Math.Cos(latitude),
                Math.Cos(lat0) * Math.Sin(latitude) - Math.Sin(lat0) * Math.Cos(latitude) * Math.Cos(delta));
            return new Vector2((float)(Math.Sin(bearing) * distance), (float)(Math.Cos(bearing) * distance));
        }

        private void GeoCoordinates(Vector2 local, out double latitude, out double longitude)
        {
            double lat0 = origin.latitude * Radians, lon0 = origin.longitude * Radians;
            double bearing = Math.Atan2(local.x, local.y);
            double angle = Math.Sqrt((double)local.x * local.x + (double)local.y * local.y) * metresPerUnit / MarsRegionalTerrain.RadiusMeters;
            latitude = Math.Asin(Math.Sin(lat0) * Math.Cos(angle) + Math.Cos(lat0) * Math.Sin(angle) * Math.Cos(bearing));
            longitude = WrapRadians(lon0 + Math.Atan2(Math.Sin(bearing) * Math.Sin(angle) * Math.Cos(lat0),
                Math.Cos(angle) - Math.Sin(lat0) * Math.Sin(latitude)));
        }

        internal static Texture2D LoadRockGrain(bool jezero, out bool roverGrain)
        {
            Texture2D grain = jezero ? Resources.Load<Texture2D>("NASA/JezeroDourbesRock") : null;
            roverGrain = grain != null;
            return grain != null ? grain : Resources.Load<Texture2D>("NASA/CuriositySandstone");
        }

        private static Material CreateMaterial(Color basalt, Color dust, float weathering, float bedding,
            string name, Texture2D grain, bool roverGrain)
        {
            Shader shader = Resources.Load<Shader>("MarsPathIllustrativeRock");
            if (shader == null) return MarsPathMaterialFactory.CreateRock(basalt, dust, weathering, name);
            Material material = new Material(shader) { name = name, enableInstancing = true };
            material.SetColor("_BaseColor", basalt); material.SetColor("_DustColor", dust);
            material.SetFloat("_Variation", weathering);
            material.SetFloat("_Bedding", bedding);
            material.SetFloat("_RoverGrain", roverGrain ? 1f : 0f);
            material.SetTexture("_GrainTex", grain);
            return material;
        }

        private static Mesh CreateRockMesh(int variant, bool jezero, out Vector3[] support)
        {
            float g = (1f + Mathf.Sqrt(5f)) * 0.5f;
            var points = new List<Vector3> { new Vector3(-1,g,0),new Vector3(1,g,0),new Vector3(-1,-g,0),new Vector3(1,-g,0),
                new Vector3(0,-1,g),new Vector3(0,1,g),new Vector3(0,-1,-g),new Vector3(0,1,-g),
                new Vector3(g,0,-1),new Vector3(g,0,1),new Vector3(-g,0,-1),new Vector3(-g,0,1) };
            for (int i = 0; i < points.Count; i++) points[i] = points[i].normalized;
            var faces = new List<int> { 0,11,5,0,5,1,0,1,7,0,7,10,0,10,11,1,5,9,5,11,4,11,10,2,10,7,6,7,1,8,
                3,9,4,3,4,2,3,2,6,3,6,8,3,8,9,4,9,5,2,4,11,6,2,10,8,6,7,9,8,1 };
            for (int pass = 0; pass < 2; pass++)
            {
                var midpoints = new Dictionary<long, int>();
                var refined = new List<int>(faces.Count * 4);
                for (int i = 0; i < faces.Count; i += 3)
                {
                    int a = faces[i], b = faces[i + 1], c = faces[i + 2];
                    int ab = Midpoint(a, b, points, midpoints), bc = Midpoint(b, c, points, midpoints), ca = Midpoint(c, a, points, midpoints);
                    refined.AddRange(new[] { a,ab,ca,b,bc,ab,c,ca,bc,ab,bc,ca });
                }
                faces = refined;
            }
            uint seed = Hash((uint)(variant + 410));
            int archetype = variant % 3;
            float crown = archetype == 0 ? 0.32f : archetype == 1 ? 0.68f : 0.57f;
            if (jezero) crown *= Mathf.Lerp(.87f, 1.19f, Unit(seed ^ 0x0ed1e991u)) *
                (archetype == 0 ? .90f : archetype == 1 ? 1.36f : 1.02f);
            float angle = Unit(seed ^ 0x67a12e15u) * 2f * Mathf.PI;
            Vector3 cut = new Vector3(Mathf.Cos(angle), .28f, Mathf.Sin(angle)).normalized;
            Vector3 secondCut = new Vector3(-cut.z, .22f, cut.x).normalized;
            Vector3 chip = new Vector3(-cut.x * .78f - cut.z * .52f, .52f, -cut.z * .78f + cut.x * .52f).normalized;
            float phase = Unit(seed ^ 0x61ac42d5u) * 20f;
            support = new Vector3[points.Count];
            for (int i = 0; i < points.Count; i++)
            {
                Vector3 d = points[i];
                if (jezero)
                {
                    // Blend rounded wind-worn forms with the broad shoulders
                    // of the jointed blocks and polygonal plates in Mastcam-Z.
                    float square = Mathf.Pow(d.x*d.x*d.x*d.x + d.y*d.y*d.y*d.y + d.z*d.z*d.z*d.z, .25f);
                    d = Vector3.Lerp(d, d / square, archetype == 2 ? .42f : .78f);
                }
                float radius = 1f + Mathf.Sin(d.x * 5.1f + d.y * 3.7f + d.z * 4.3f + phase) * .077f
                    + Mathf.Sin(d.x * 10.7f - d.y * 7.3f + d.z * 8.1f - phase) * .026f;
                float vertical = (d.y + 1f) * 0.5f;
                float lean = jezero && archetype == 1 ? .25f : .10f;
                Vector3 shaped = new Vector3(
                    d.x * .50f * radius * (archetype == 0 ? 1.22f : 1f) + vertical * vertical * cut.x * lean,
                    Mathf.Max(-.06f, (Mathf.Pow(vertical, .88f) * crown - .065f) * radius),
                    d.z * .50f * radius * (archetype == 0 ? .77f : 1f) + vertical * vertical * cut.z * lean);

                // Planar breaks, chipped edges, and a tilted cap retain large
                // recognizable faces on a silhouette of 320 triangles.
                shaped -= cut * Mathf.Max(0f, Vector3.Dot(shaped, cut) - (archetype == 1 ? .31f : .40f));
                shaped -= secondCut * Mathf.Max(0f, Vector3.Dot(shaped, secondCut) - (archetype == 2 ? .30f : .42f));
                float faceTilt = jezero && archetype == 1 ? .57f : .28f;
                float cap = crown * (archetype == 0 ? .68f : .80f) + (cut.x * shaped.x + cut.z * shaped.z) * faceTilt;
                shaped.y = Mathf.Min(shaped.y, cap);
                if (jezero)
                {
                    float lip = Mathf.Lerp(.29f, .43f, Unit(seed ^ 0x29c6510bu));
                    shaped -= chip * Mathf.Max(0f, Vector3.Dot(shaped, chip) - lip);
                    float lateral = shaped.x * cut.x + shaped.z * cut.z;
                    float bed = shaped.y - lateral * .19f;
                    float grain = Mathf.Sin(shaped.x * 31.2f + shaped.y * 27.5f - shaped.z * 17.1f + phase);
                    float ridge = Mathf.Sin(bed * 62f + Mathf.Sin(lateral * 19f + phase));
                    float relief = .018f * grain + (archetype == 0 ? .028f : .012f) * ridge;
                    shaped += d.normalized * relief;
                }
                support[i] = shaped;
            }
            Vector3[] vertices = new Vector3[faces.Count], normals = new Vector3[faces.Count];
            Vector4[] tangents = new Vector4[faces.Count];
            Color[] colors = new Color[faces.Count];
            int[] triangles = new int[faces.Count];
            Vector3 center = new Vector3(0f, crown * 0.43f, 0f);
            float texturePhase = Unit(seed ^ 0x1a791ef9u);
            for (int i = 0; i < faces.Count; i += 3)
            {
                Vector3 a = support[faces[i]], b = support[faces[i + 1]], c = support[faces[i + 2]];
                Vector3 faceNormal = Vector3.Cross(b - a, c - a).normalized;
                for (int corner = 0; corner < 3; corner++)
                {
                    int index = i + corner;
                    Vector3 vertex = support[faces[index]];
                    vertices[index] = vertex;
                    float rounding = jezero ? (archetype == 2 ? .52f : .30f) : (archetype == 0 ? .26f : .34f);
                    normals[index] = Vector3.Slerp(faceNormal, (vertex - center).normalized, rounding).normalized;
                    tangents[index] = GrainTangent(normals[index]);
                    float tone = Mathf.Lerp(jezero ? .65f : .72f, 1.04f, Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((vertex.y + .055f) / (crown * .44f))));
                    colors[index] = new Color(tone, tone * 0.98f, tone * 0.95f, jezero ? texturePhase : 1f);
                    triangles[index] = index;
                }
            }
            Mesh mesh = new Mesh { name = "Shared illustrative Mars stone " + variant };
            mesh.vertices = vertices; mesh.normals = normals; mesh.tangents = tangents; mesh.colors = colors; mesh.triangles = triangles;
            mesh.RecalculateBounds();
            return mesh;
        }

        internal static Vector4 GrainTangent(Vector3 normal)
        {
            Vector3 tangent = Vector3.Cross(Mathf.Abs(normal.y) > .9f ? Vector3.right : Vector3.up, normal).normalized;
            return new Vector4(tangent.x, tangent.y, tangent.z, 1f);
        }

        private static int Midpoint(int a, int b, List<Vector3> vertices, Dictionary<long, int> cache)
        {
            long key = ((long)Math.Min(a, b) << 32) | (uint)Math.Max(a, b);
            if (cache.TryGetValue(key, out int index)) return index;
            index = vertices.Count; vertices.Add((vertices[a] + vertices[b]).normalized); cache.Add(key, index); return index;
        }
        private static uint Hash(uint value)
        { unchecked { value ^= value >> 16; value *= 0x7feb352du; value ^= value >> 15; value *= 0x846ca68bu; return value ^ (value >> 16); } }
        private static float Unit(uint seed) => (Hash(seed) & 0x00ffffffu) / 16777216f;
        private static int Mod(int value, int modulus) { int result = value % modulus; return result < 0 ? result + modulus : result; }
        private static double WrapRadians(double value)
        { value = (value + Math.PI) % (Math.PI * 2); if (value < 0) value += Math.PI * 2; return value - Math.PI; }

        private void OnEnable() { if (built && ReferenceEquals(mission, MarsRegionalTerrain.Current)) active = this; }
        private void OnDisable() { if (active == this) active = null; }
        private void OnDestroy() { ClearOwnedObjects(); }
        private void ClearOwnedObjects()
        {
            if (active == this) active = null;
            foreach (PooledRock rock in pool) if (rock.transform != null) { rock.transform.gameObject.SetActive(false); Destroy(rock.transform.gameObject); }
            pool.Clear(); obstacles.Clear(); candidates.Clear();
            if (variants != null) foreach (Mesh mesh in variants) if (mesh != null) Destroy(mesh);
            if (materials != null) foreach (Material material in materials) if (material != null) Destroy(material);
            variants = null; materials = null; supportVertices = null; built = false;
        }
    }
}

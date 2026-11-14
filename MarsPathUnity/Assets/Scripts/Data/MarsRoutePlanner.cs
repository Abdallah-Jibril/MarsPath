using System;
using System.Collections.Generic;
using UnityEngine;

namespace MarsPath
{
    /// <summary>Measured DEM graph with separate team routing objectives.
    /// Risk weights and suit demand are experimental, source-aware models.</summary>
    public static class MarsRoutePlanner
    {
        private const float Half = 60;
        private static readonly string[] Types = { "BALANCED", "SAFEST", "FASTEST", "SCIENCE", "ENERGY" };
        private static readonly string[] Names = { "Balanced", "Safest", "Fastest", "Science priority", "Energy efficient" };
        private static readonly string[] Colors = { "#35E5FF", "#53F08A", "#FFB43B", "#B895FF", "#D5F060" };

        private sealed class TerrainGraph
        {
            public bool missionZone;
            public int size, start, end;
            public float cellMeters;
            public Vector2[] positions;
            public GeoPoint[] locations;
            public float[] elevations, slopes;
            public MarsGraphRouting.Graph graph;

            public int Index(Vector2 world)
            {
                int x = Mathf.Clamp(Mathf.RoundToInt((world.x / 120 + .5f) * (size - 1)), 0, size - 1);
                int z = Mathf.Clamp(Mathf.RoundToInt((world.y / 120 + .5f) * (size - 1)), 0, size - 1);
                return z * size + x;
            }
        }

        public static MissionData Generate(GeoPoint start, GeoPoint end) => Generate(start, end, null);

        public static MissionData Generate(GeoPoint start, GeoPoint end, MarsRoutingOptions settings)
        {
            if (!Finite(start) || !Finite(end)) throw new ArgumentException("Enter valid latitude and longitude for both endpoints.");
            MarsRoutingOptions options = settings?.Clone() ?? new MarsRoutingOptions();
            options.Validate();
            bool zone=options.MissionZoneId==MarsMissionZone.Id;
            if(zone&&(!MarsMissionZone.Contains(start)||!MarsMissionZone.Contains(end)))
                throw new ArgumentException("Choose START and DESTINATION inside the measured Jezero mission zone.");
            float separation = MarsGeospatial.AngularDistanceDegrees(start, end) * Mathf.Deg2Rad * (float)MarsRegionalTerrain.RadiusMeters / 1000;
            if (separation < .08f) throw new ArgumentException("Choose endpoints at least 80 meters apart.");
            if (options.ScienceStops != null)
                foreach (GeoPoint stop in options.ScienceStops)
                    if (!Finite(stop)||zone&&!MarsMissionZone.Contains(stop)) throw new ArgumentException("Enter valid coordinates inside the selected mission region for each science stop.");

            MissionData mission = NewMission(start, end, separation, options);
            MarsRegionalTerrain.Configure(mission);
            TerrainGraph terrain = BuildGraph(start, end, zone||separation < 5 ? 145 : 73, options);
            mission.sciencePoints = ScienceCandidates(terrain, options);
            mission.hazards = SlopeHazards(terrain);
            int[] orderedStops = Stops(terrain, options.ScienceStops);
            if (options.ReturnToBase)
            {
                var roundtrip = new List<int>(orderedStops) { terrain.end };
                orderedStops = roundtrip.ToArray();
                mission.destination = Clone(start);
            }
            int destination = options.ReturnToBase ? terrain.start : terrain.end;

            mission.routes = new RouteData[5];
            var checkedEdges = new HashSet<long>();
            for (int profile = 0; profile < mission.routes.Length; profile++)
            {
                int[] stops = profile == 3 && orderedStops.Length == 0 ? CandidateStops(terrain, mission.sciencePoints) : orderedStops;
                Func<MarsGraphRouting.Result> search;
                string algorithm;
                if (profile == 0 && options.ConstraintsEnabled)
                {
                    search = () => MarsGraphRouting.SolveConstrained(terrain.graph, terrain.start, destination, options, stops);
                    algorithm = "Multi-label A* / Pareto and lower-bound pruning";
                }
                else
                {
                    MarsGraphRouting.Objective objective = Objective(profile);
                    search = () => MarsGraphRouting.Solve(terrain.graph, terrain.start, destination, objective, stops);
                    algorithm = Algorithm(profile);
                }
                MarsGraphRouting.Result result = ValidateTrackEdges(terrain, search, options.MaxSlopeDegrees, checkedEdges);
                mission.routes[profile] = Measure(terrain, result, profile, mission, options, algorithm);
            }
            MarkParetoAlternatives(mission.routes);
            return mission;
        }

        // Lazy edge rejection keeps first-use planning bounded: only proposed
        // paths need dense source checks, rather than every planetary graph edge.
        // Valid edge costs stay unchanged, so optimality on the sampled graph
        // remains meaningful after invalid edges are removed.
        private static MarsGraphRouting.Result ValidateTrackEdges(TerrainGraph terrain,
            Func<MarsGraphRouting.Result> search, float maxSlope, HashSet<long> checkedEdges)
        {
            for (int pass = 0; pass < 16; pass++)
            {
                MarsGraphRouting.Result result = search();
                if (!result.Available) return result;
                bool rejected = false;
                for (int i = 1; i < result.nodes.Count; i++)
                {
                    int a = result.nodes[i - 1], b = result.nodes[i];
                    long key = ((long)Math.Min(a, b) << 32) | (uint)Math.Max(a, b);
                    if (checkedEdges.Contains(key)) continue;
                    if (TrackEdgeValid(terrain, a, b, maxSlope)) checkedEdges.Add(key);
                    else
                    {
                        terrain.graph.edges[a].RemoveAll(edge => edge.destination == b);
                        terrain.graph.edges[b].RemoveAll(edge => edge.destination == a);
                        rejected = true;
                    }
                }
                if (!rejected) return result;
            }
            return new MarsGraphRouting.Result { exact = false,
                status = "Terrain-check limit reached / choose a smaller planning region" };
        }

        private static bool TrackEdgeValid(TerrainGraph terrain, int a, int b, float maxSlope)
        {
            GeoPoint start = terrain.locations[a], end = terrain.locations[b];
            double distance = MarsGeospatial.AngularDistanceDegrees(start, end) * Math.PI / 180 * MarsRegionalTerrain.RadiusMeters;
            float spacing = Mathf.Min(MarsRegionalTerrain.SourceSpacingMeters(start), MarsRegionalTerrain.SourceSpacingMeters(end));
            double interval = Mathf.Clamp(spacing * .5f, 1, 250);
            // Long planetary edges are still a sampled approximation. The cap
            // bounds work and never changes the native elevation resolution.
            int steps = Mathf.Clamp((int)Math.Ceiling(distance / interval), 1, 64);
            double step = distance / steps;
            float bearing = MarsGeospatial.InitialBearing(start, end), prior = terrain.elevations[a];
            for (int sample = 1; sample <= steps; sample++)
            {
                GeoPoint point = sample == steps ? end : MarsGeospatial.DestinationPrecise(start, bearing, step * sample);
                if(terrain.missionZone&&!MarsMissionZone.Contains(point))return false;
                if (MarsOfficialTerrain.IsEstimated(point)) return false;
                float height = sample == steps ? terrain.elevations[b] : MarsRegionalTerrain.MeasuredElevation(point);
                if (!Finite(height) || Math.Atan2(Math.Abs(height - prior), Math.Max(.001, step)) * 180 / Math.PI > maxSlope) return false;
                prior = height;
            }
            return true;
        }

        private static MissionData NewMission(GeoPoint start, GeoPoint end, float separation, MarsRoutingOptions options)
        {
            GeoPoint midpoint = MarsRegionalTerrain.Midpoint(start, end);
            MarsSite nearest = MarsSiteCatalog.Nearest(midpoint);
            float span = Mathf.Max(.8f, separation * 1.72f);
            if (options.ScienceStops != null)
                foreach (GeoPoint stop in options.ScienceStops)
                {
                    float distance = MarsGeospatial.AngularDistanceDegrees(midpoint, stop) * Mathf.Deg2Rad * (float)MarsRegionalTerrain.RadiusMeters / 1000;
                    span = Mathf.Max(span, distance * 2.15f);
                }
            return new MissionData
            {
                missionId = "MP-REGION-" + DateTime.UtcNow.ToString("yyyyMMddHHmmss"),
                region = options.MissionZoneId==MarsMissionZone.Id?MarsMissionZone.Name:separation > 10 ? RegionName(start) + " to " + RegionName(end) : RegionName(midpoint) + " / Selected traverse",
                currentLocation = Clone(start), destination = Clone(end), regionCenter = midpoint,
                generatedRegion = true,
                usesJezeroDem = MarsRegionalTerrain.InJezeroCrop(start) && MarsRegionalTerrain.InJezeroCrop(end),
                regionSpanKm = span, terrainProfile = nearest.profile,
                telemetry = new TelemetryData { oxygenPercent = 96, suitPowerPercent = 94, heartRateBpm = 78 },
                routingOptions = options,
                dataNotice = (options.MissionZoneId==MarsMissionZone.Id?$"OFFLINE JEZERO MISSION ZONE / {MarsMissionZone.WidthKm:0.00} x {MarsMissionZone.HeightKm:0.00} km. Original measured {MarsMissionZone.SpacingMeters:0.00} m height grid, physical scale 1:1. " : "")+"Measured elevations and photographic imagery have different source grids. " +
                    "Routing uses an 8-connected sampled DEM graph. Slope limit " + options.MaxSlopeDegrees.ToString("0.#") +
                    " degrees. Risk, science candidates, suit demand and individual small rocks are modeled."
            };
        }

        private static TerrainGraph BuildGraph(GeoPoint start, GeoPoint end, int size, MarsRoutingOptions options)
        {
            float maxSlope = options.MaxSlopeDegrees;
            int count = size * size;
            var terrain = new TerrainGraph
            {
                size = size, missionZone=options.MissionZoneId==MarsMissionZone.Id, positions = new Vector2[count], locations = new GeoPoint[count],
                elevations = new float[count], slopes = new float[count], graph = new MarsGraphRouting.Graph(count),
                cellMeters = 120f / (size - 1) * MarsRegionalTerrain.MetersPerUnit
            };
            Vector2 from = MarsRegionalTerrain.ToWorld(start), to = MarsRegionalTerrain.ToWorld(end);
            terrain.start = terrain.Index(from); terrain.end = terrain.Index(to);
            if (terrain.start == terrain.end) throw new ArgumentException("The selected endpoints occupy the same routing cell.");
            var exactLocations = new Dictionary<int, GeoPoint> { { terrain.start, start }, { terrain.end, end } };
            if (options.ScienceStops != null)
                foreach (GeoPoint stop in options.ScienceStops)
                {
                    int cell = terrain.Index(MarsRegionalTerrain.ToWorld(stop));
                    if (exactLocations.TryGetValue(cell, out GeoPoint prior) && MarsGeospatial.AngularDistanceDegrees(prior, stop) > .000001)
                        throw new ArgumentException("Two selected waypoints occupy one routing cell. Choose a smaller region or more separated points.");
                    exactLocations[cell] = stop;
                }
            var uncertainty = new float[count];
            for (int z = 0; z < size; z++)
            {
                for (int x = 0; x < size; x++)
                {
                    int i = z * size + x;
                    Vector2 position = new Vector2(GridWorld(x, size), GridWorld(z, size));
                    if (exactLocations.TryGetValue(i, out GeoPoint exact)) position = MarsRegionalTerrain.ToWorld(exact);
                    terrain.positions[i] = position;
                    GeoPoint point = exact ?? MarsRegionalTerrain.ToGeo(position.x, position.y);
                    terrain.locations[i] = point;
                    float height = MarsRegionalTerrain.MeasuredElevation(point);
                    terrain.elevations[i] = height;
                    double radius = Math.Sqrt((double)position.x * position.x + (double)position.y * position.y) * MarsRegionalTerrain.MetersPerUnit;
                    terrain.graph.blocked[i] = !Finite(height) || MarsOfficialTerrain.IsEstimated(point) || terrain.missionZone&&!MarsMissionZone.Contains(point) || radius >= Math.PI * MarsRegionalTerrain.RadiusMeters;
                    double latitude = point.latitude * Math.PI / 180, longitude = point.longitude * Math.PI / 180;
                    // Base-sphere chord plus elevation is a lower bound for
                    // sqrt(surface-arc^2 + rise^2), including high plateaus.
                    double radiusMeters = MarsRegionalTerrain.RadiusMeters;
                    terrain.graph.x[i] = radiusMeters * Math.Cos(latitude) * Math.Cos(longitude);
                    terrain.graph.y[i] = radiusMeters * Math.Cos(latitude) * Math.Sin(longitude);
                    terrain.graph.z[i] = radiusMeters * Math.Sin(latitude);
                    terrain.graph.elevation[i] = height;
                    // Resolution is an uncertainty proxy, kept separate from
                    // measured slope and from the photographic pixel spacing.
                    uncertainty[i] = Mathf.Clamp01(Mathf.Log(Mathf.Max(1, MarsRegionalTerrain.SourceSpacingMeters(point)), 2) / 10);
                }
            }
            var roughness = new float[count];
            for (int z = 0; z < size; z++)
            {
                for (int x = 0; x < size; x++)
                {
                    int i = z * size + x;
                    float west = terrain.elevations[z * size + Mathf.Max(0, x - 1)];
                    float east = terrain.elevations[z * size + Mathf.Min(size - 1, x + 1)];
                    float south = terrain.elevations[Mathf.Max(0, z - 1) * size + x];
                    float north = terrain.elevations[Mathf.Min(size - 1, z + 1) * size + x];
                    float dx = (east - west) / (terrain.cellMeters * (x == 0 || x == size - 1 ? 1 : 2));
                    float dz = (north - south) / (terrain.cellMeters * (z == 0 || z == size - 1 ? 1 : 2));
                    terrain.slopes[i] = Mathf.Atan(Mathf.Sqrt(dx * dx + dz * dz)) * Mathf.Rad2Deg;
                    roughness[i] = Mathf.Clamp01(Mathf.Abs((west + east + south + north) * .25f - terrain.elevations[i]) / Mathf.Max(1, terrain.cellMeters * .1f));
                    if (!Finite(terrain.slopes[i]) || terrain.slopes[i] > maxSlope) terrain.graph.blocked[i] = true;
                }
            }
            for (int z = 0; z < size; z++)
            {
                for (int x = 0; x < size; x++)
                {
                    int i = z * size + x;
                    if (terrain.graph.blocked[i]) continue;
                    foreach (Vector2Int offset in Neighbors)
                    {
                        int nx = x + offset.x, nz = z + offset.y;
                        if (nx < 0 || nz < 0 || nx >= size || nz >= size) continue;
                        int next = nz * size + nx;
                        if (terrain.graph.blocked[next]) continue;
                        if (offset.x != 0 && offset.y != 0 &&
                            (terrain.graph.blocked[z * size + nx] || terrain.graph.blocked[nz * size + x])) continue;
                        double horizontal = MarsGeospatial.AngularDistanceDegrees(terrain.locations[i], terrain.locations[next]) * Math.PI / 180 * MarsRegionalTerrain.RadiusMeters;
                        double rise = terrain.elevations[next] - terrain.elevations[i];
                        double grade = Math.Atan2(Math.Abs(rise), Math.Max(.001, horizontal)) * 180 / Math.PI;
                        if (grade > maxSlope) continue;
                        double length = Math.Sqrt(horizontal * horizontal + rise * rise);
                        double density = .50 * Math.Pow(grade / maxSlope, 2) +
                            .20 * (roughness[i] + roughness[next]) * .5 +
                            .20 * (uncertainty[i] + uncertainty[next]) * .5 +
                            .10 * BlockedNeighborFraction(terrain, nx, nz);
                        double minutes = length / 42 * (1 + grade * .025);
                        double energy = minutes / 60 * (.20 + .12 * (1 + grade / 25)) + Math.Max(0, rise) * .000001;
                        terrain.graph.AddEdge(i, new MarsGraphRouting.Edge(next, length, length / 1000 * density, minutes, energy, rise, grade));
                    }
                }
            }
            return terrain;
        }

        private static readonly Vector2Int[] Neighbors =
        {
            new Vector2Int(-1, -1), new Vector2Int(0, -1), new Vector2Int(1, -1),
            new Vector2Int(-1, 0), new Vector2Int(1, 0),
            new Vector2Int(-1, 1), new Vector2Int(0, 1), new Vector2Int(1, 1)
        };

        private static float BlockedNeighborFraction(TerrainGraph terrain, int x, int z)
        {
            int blocked = 0, total = 0;
            foreach (Vector2Int d in Neighbors)
            {
                int nx = x + d.x, nz = z + d.y;
                if (nx < 0 || nz < 0 || nx >= terrain.size || nz >= terrain.size) continue;
                total++; if (terrain.graph.blocked[nz * terrain.size + nx]) blocked++;
            }
            return total > 0 ? blocked / (float)total : 0;
        }

        private static SciencePointData[] ScienceCandidates(TerrainGraph terrain, MarsRoutingOptions options)
        {
            if (options.ScienceStops != null && options.ScienceStops.Length > 0)
            {
                var selected = new SciencePointData[options.ScienceStops.Length];
                for (int i = 0; i < selected.Length; i++)
                {
                    GeoPoint point = options.ScienceStops[i]; Vector2 p = MarsRegionalTerrain.ToWorld(point);
                    if (Mathf.Abs(p.x) > Half || Mathf.Abs(p.y) > Half)
                        throw new ArgumentException("A science stop is outside this mission's planning region.");
                    selected[i] = Science("science-" + i, string.IsNullOrEmpty(point.label) ? "Selected science stop" : point.label, p, "SELECTED WAYPOINT");
                }
                return selected;
            }
            Vector2 from = terrain.positions[terrain.start], to = terrain.positions[terrain.end];
            Vector2 direction = (to - from).normalized, normal = new Vector2(-direction.y, direction.x);
            SciencePointData[] candidates=new[]
            {
                Science("sci-a", "Candidate outcrop (modeled)", Vector2.Lerp(from, to, .32f) + normal * 13, "MODELED CANDIDATE"),
                Science("sci-b", "Candidate sediment exposure (modeled)", Vector2.Lerp(from, to, .65f) - normal * 11, "MODELED CANDIDATE"),
                Science("sci-c", "Candidate ridge sample (modeled)", Vector2.Lerp(from, to, .80f) + normal * 8, "MODELED CANDIDATE")
            };
            return terrain.missionZone?Array.FindAll(candidates,point=>MarsMissionZone.Contains(MarsRegionalTerrain.ToGeo(point.x*Half,point.z*Half))):candidates;
        }

        private static int[] Stops(TerrainGraph terrain, GeoPoint[] points)
        {
            if (points == null || points.Length == 0) return Array.Empty<int>();
            var stops = new int[points.Length];
            for (int i = 0; i < points.Length; i++) stops[i] = terrain.Index(MarsRegionalTerrain.ToWorld(points[i]));
            return stops;
        }

        private static int[] CandidateStops(TerrainGraph terrain, SciencePointData[] points)
        {
            var stops = new List<int>();
            foreach (SciencePointData point in points)
            {
                int stop = terrain.Index(new Vector2(point.x * Half, point.z * Half));
                if (!terrain.graph.blocked[stop] && !stops.Contains(stop)) stops.Add(stop);
            }
            return stops.ToArray();
        }

        private static SciencePointData Science(string id, string label, Vector2 p, string category)
        {
            return new SciencePointData { id = id, label = label, category = category, x = Mathf.Clamp(p.x / Half, -1, 1), z = Mathf.Clamp(p.y / Half, -1, 1) };
        }

        private static HazardData[] SlopeHazards(TerrainGraph terrain)
        {
            var hazards = new List<HazardData>();
            for (int candidate = 0; candidate < 3; candidate++)
            {
                float maximum = 7; int selected = -1;
                for (int i = 0; i < terrain.slopes.Length; i++)
                {
                    Vector2 point = terrain.positions[i];
                    if (!Finite(terrain.slopes[i]) || point.magnitude > 69 || terrain.slopes[i] <= maximum ||
                        Vector2.Distance(point, terrain.positions[terrain.start]) < 8 ||
                        Vector2.Distance(point, terrain.positions[terrain.end]) < 8) continue;
                    bool close = false;
                    foreach (HazardData existing in hazards)
                        if (Vector2.Distance(point, new Vector2(existing.x, existing.z) * Half) < 14) close = true;
                    if (!close) { selected = i; maximum = terrain.slopes[i]; }
                }
                if (selected < 0) break;
                Vector2 p = terrain.positions[selected];
                hazards.Add(new HazardData { id = "model-risk-" + candidate, label = "Sampled slope zone", severity = maximum > 20 ? "HIGH" : "CAUTION",
                    warning = "Steep terrain ahead", action = "Reduce pace and review footing", x = p.x / Half, z = p.y / Half });
            }
            return hazards.ToArray();
        }

        private static MarsGraphRouting.Objective Objective(int profile)
        {
            switch (profile)
            {
                case 1: return MarsGraphRouting.Objective.Safest;
                case 2: return MarsGraphRouting.Objective.Shortest;
                case 4: return MarsGraphRouting.Objective.Energy;
                default: return MarsGraphRouting.Objective.Balanced;
            }
        }

        private static string Algorithm(int profile)
        {
            switch (profile)
            {
                case 1: return "Lexicographic Dijkstra / risk then distance";
                case 2: return "A* / 3D distance";
                case 3: return "Stage-expanded A* / ordered science waypoints";
                case 4: return "A* / estimated energy";
                default: return "A* / distance and integrated risk";
            }
        }

        private static RouteData Measure(TerrainGraph terrain, MarsGraphRouting.Result result, int profile,
            MissionData mission, MarsRoutingOptions options, string algorithm)
        {
            bool constrained = profile == 0 && options.ConstraintsEnabled;
            var route = new RouteData
            {
                id = "computed-" + (constrained ? "WITHINLIMITS" : Types[profile]), type = constrained ? "WITHINLIMITS" : Types[profile],
                displayName = constrained ? "Within limits" : Names[profile], colorHex = Colors[profile], recommended = profile == 0,
                available = result.Available, algorithm = algorithm, optimalityStatus = result.status,
                routingGridMeters = terrain.cellMeters, withinBudgets = constrained && result.Available,
                hazardIds = Array.Empty<string>(), sciencePointIds = Array.Empty<string>()
            };
            if (!result.Available)
            {
                route.summary = result.status + " / " + options.MaxSlopeDegrees.ToString("0.#") + " degree slope limit";
                Vector2 a = terrain.positions[terrain.start], b = terrain.positions[options.ReturnToBase ? terrain.start : terrain.end];
                route.waypoints = new[] { new RoutePoint { x = a.x / Half, z = a.y / Half }, new RoutePoint { x = b.x / Half, z = b.y / Half } };
                return route;
            }
            MarsGraphRouting.Totals totals = result.totals;
            route.waypoints = new RoutePoint[result.nodes.Count];
            for (int i = 0; i < result.nodes.Count; i++)
            {
                Vector2 p = terrain.positions[result.nodes[i]];
                route.waypoints[i] = new RoutePoint { x = p.x / Half, z = p.y / Half };
            }
            route.distanceKm = (float)(totals.distance / 1000); route.etaMinutes = (float)totals.minutes;
            route.totalRisk = (float)totals.risk; route.estimatedEnergyKwh = (float)totals.energy;
            route.maxSlopeDegrees = (float)totals.maxSlope; route.elevationGainMeters = (float)totals.ascent; route.elevationLossMeters = (float)totals.descent;
            float meanDensity = route.totalRisk / Mathf.Max(.001f, route.distanceKm);
            route.safetyScore = Mathf.Clamp(100 * (1 - meanDensity), 0, 100);
            double baseEnergy = totals.distance / 42 / 60 * .32;
            route.energyScore = Mathf.Clamp((float)(100 * baseEnergy / Math.Max(.000001, totals.energy)), 0, 100);
            var science = new List<string>(); var hazards = new List<string>();
            foreach (SciencePointData point in mission.sciencePoints)
                if (Visits(terrain, result.nodes, point.x, point.z, 9)) science.Add(point.id);
            foreach (HazardData hazard in mission.hazards)
                if (Visits(terrain, result.nodes, hazard.x, hazard.z, 10)) hazards.Add(hazard.id);
            route.sciencePointIds = science.ToArray(); route.hazardIds = hazards.ToArray();
            route.scienceScore = mission.sciencePoints.Length > 0 ? science.Count * 100f / mission.sciencePoints.Length : 0;
            route.summary = constrained ? "Shortest feasible route within the entered risk, time and energy budgets." :
                profile == 1 ? "Minimizes integrated risk, then distance among equally safe grid paths." :
                profile == 2 ? "Minimizes 3D travel distance on the sampled graph." :
                profile == 3 ? "Visits the listed science waypoints in their order." :
                profile == 4 ? "Minimizes estimated edge energy demand." : "Balances 3D distance and integrated terrain risk.";
            return route;
        }

        private static bool Visits(TerrainGraph terrain, List<int> nodes, float x, float z, float radius)
        {
            Vector2 target = new Vector2(x, z) * Half;
            foreach (int node in nodes) if (Vector2.Distance(terrain.positions[node], target) < radius) return true;
            return false;
        }

        private static void MarkParetoAlternatives(RouteData[] routes)
        {
            foreach (RouteData route in routes)
            {
                if (!route.available) continue;
                route.paretoAlternative = true;
                foreach (RouteData other in routes)
                {
                    if (other == route || !other.available) continue;
                    bool noWorse = other.distanceKm <= route.distanceKm && other.totalRisk <= route.totalRisk &&
                        other.etaMinutes <= route.etaMinutes && other.estimatedEnergyKwh <= route.estimatedEnergyKwh;
                    bool better = other.distanceKm < route.distanceKm || other.totalRisk < route.totalRisk ||
                        other.etaMinutes < route.etaMinutes || other.estimatedEnergyKwh < route.estimatedEnergyKwh;
                    if (noWorse && better) { route.paretoAlternative = false; break; }
                }
            }
        }

        private static bool Finite(GeoPoint p) => p != null && Finite(p.latitude) && Finite(p.longitude) && Mathf.Abs(p.latitude) <= 90 && Mathf.Abs(p.longitude) <= 180;
        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
        private static float GridWorld(int value, int size) => (value / (float)(size - 1) - .5f) * Half * 2;
        private static GeoPoint Clone(GeoPoint p) => new GeoPoint { label = string.IsNullOrEmpty(p.label) ? "Selected location" : p.label, latitude = p.latitude, longitude = p.longitude };

        private static string RegionName(GeoPoint point)
        {
            MarsSite site = MarsSiteCatalog.Nearest(point);
            float distance = MarsGeospatial.AngularDistanceDegrees(point, site.Point) * Mathf.Deg2Rad * (float)MarsRegionalTerrain.RadiusMeters / 1000;
            return distance < 100 ? site.name : "Selected Mars region";
        }
    }
}

using System;
using System.Collections.Generic;

namespace MarsPath
{
    [Serializable]
    public sealed class MarsRoutingOptions
    {
        public float MaxSlopeDegrees = 25f;
        public bool ConstraintsEnabled;
        public float MaxRisk = float.PositiveInfinity;
        public float MaxTimeMinutes = float.PositiveInfinity;
        public float MaxEnergyKwh = float.PositiveInfinity;
        public GeoPoint[] ScienceStops;
        public bool ReturnToBase;
        public int MaxLabels = 120000;
        public int MaxLabelsPerState = 24;
        public string MissionZoneId;

        public MarsRoutingOptions Clone()
        {
            var copy = (MarsRoutingOptions)MemberwiseClone();
            if (ScienceStops != null)
            {
                copy.ScienceStops = new GeoPoint[ScienceStops.Length];
                for (int i = 0; i < ScienceStops.Length; i++)
                {
                    GeoPoint point = ScienceStops[i];
                    copy.ScienceStops[i] = point == null ? null : new GeoPoint
                    {
                        label = point.label, latitude = point.latitude, longitude = point.longitude
                    };
                }
            }
            return copy;
        }

        public void Validate()
        {
            if(!string.IsNullOrEmpty(MissionZoneId)&&MissionZoneId!=MarsMissionZone.Id)
                throw new ArgumentException("The selected offline mission zone is unavailable.");
            if (!Finite(MaxSlopeDegrees) || MaxSlopeDegrees <= 0 || MaxSlopeDegrees >= 90)
                throw new ArgumentException("Choose a maximum slope between 0 and 90 degrees.");
            if (ConstraintsEnabled && (!Positive(MaxRisk) || !Positive(MaxTimeMinutes) || !Positive(MaxEnergyKwh)))
                throw new ArgumentException("Enter positive finite risk, time and energy budgets for Within limits.");
            if (MaxLabels < 100 || MaxLabelsPerState < 1)
                throw new ArgumentException("The constrained search needs a valid label budget.");
            if (ScienceStops != null && ScienceStops.Length > 6)
                throw new ArgumentException("Choose at most six ordered science stops.");
        }

        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
        private static bool Positive(float value) => Finite(value) && value > 0;
    }

    /// <summary>Nonnegative graph searches used by the desktop planner. Costs
    /// share the same edge model across every objective and every waypoint leg.</summary>
    public static class MarsGraphRouting
    {
        public enum Objective { Shortest, Safest, Balanced, Energy }

        public struct Totals
        {
            public double distance, risk, minutes, energy, ascent, descent, maxSlope;
            public long riskUnits;

            public Totals Add(Edge edge)
            {
                return new Totals
                {
                    distance = distance + edge.distance,
                    // Fixed 1e-9 risk-km units give lexicographic equality a
                    // stable meaning across different floating-point sums.
                    riskUnits = riskUnits + edge.riskUnits,
                    risk = (riskUnits + edge.riskUnits) / 1000000000.0,
                    minutes = minutes + edge.minutes,
                    energy = energy + edge.energy,
                    ascent = ascent + Math.Max(0, edge.rise),
                    descent = descent + Math.Max(0, -edge.rise),
                    maxSlope = Math.Max(maxSlope, edge.slope)
                };
            }
        }

        public struct Edge
        {
            public int destination;
            public double distance, risk, minutes, energy, rise, slope;
            public long riskUnits;

            public Edge(int destination, double distance, double risk, double minutes, double energy,
                double rise = 0, double slope = 0)
            {
                this.destination = destination;
                this.distance = distance;
                riskUnits = checked((long)Math.Round(risk * 1000000000.0));
                this.risk = riskUnits / 1000000000.0;
                this.minutes = minutes;
                this.energy = energy;
                this.rise = rise;
                this.slope = slope;
            }
        }

        public sealed class Graph
        {
            public readonly List<Edge>[] edges;
            public readonly double[] x, y, z, elevation;
            public readonly bool[] blocked;
            public int Count => edges.Length;

            public Graph(int count)
            {
                edges = new List<Edge>[count];
                x = new double[count]; y = new double[count]; z = new double[count];
                elevation = new double[count];
                blocked = new bool[count];
                for (int i = 0; i < count; i++) edges[i] = new List<Edge>(8);
            }

            public double LowerDistance(int from, int to)
            {
                double dx = x[from] - x[to], dy = y[from] - y[to], dz = z[from] - z[to];
                double dh = elevation[from] - elevation[to];
                return Math.Sqrt(dx * dx + dy * dy + dz * dz + dh * dh);
            }

            public void AddEdge(int from, Edge edge)
            {
                if (edge.destination < 0 || edge.destination >= Count || edge.distance < 0 ||
                    edge.risk < 0 || edge.minutes < 0 || edge.energy < 0 ||
                    !Finite(edge.distance) || !Finite(edge.risk) || !Finite(edge.minutes) || !Finite(edge.energy))
                    throw new ArgumentException("Routing edges need finite nonnegative resource costs.");
                edges[from].Add(edge);
            }
        }

        public sealed class Result
        {
            public List<int> nodes;
            public Totals totals;
            public bool exact;
            public string status;
            public int expandedLabels;
            public bool Available => nodes != null;
        }

        private struct QueueEntry
        {
            public int index, version;
            public double primary, secondary;
        }

        private sealed class MinHeap
        {
            private readonly List<QueueEntry> items = new List<QueueEntry>();
            public int Count => items.Count;

            private static bool Before(QueueEntry a, QueueEntry b)
            {
                int order = a.primary.CompareTo(b.primary);
                if (order != 0) return order < 0;
                order = a.secondary.CompareTo(b.secondary);
                return order != 0 ? order < 0 : a.index < b.index;
            }

            public void Push(int index, double primary, double secondary, int version = 0)
            {
                var value = new QueueEntry { index = index, primary = primary, secondary = secondary, version = version };
                items.Add(value);
                int at = items.Count - 1;
                while (at > 0)
                {
                    int parent = (at - 1) / 2;
                    if (!Before(value, items[parent])) break;
                    items[at] = items[parent]; at = parent;
                }
                items[at] = value;
            }

            public QueueEntry Pop()
            {
                QueueEntry answer = items[0], last = items[items.Count - 1];
                items.RemoveAt(items.Count - 1);
                if (items.Count == 0) return answer;
                int at = 0;
                while (true)
                {
                    int child = at * 2 + 1;
                    if (child >= items.Count) break;
                    if (child + 1 < items.Count && Before(items[child + 1], items[child])) child++;
                    if (!Before(items[child], last)) break;
                    items[at] = items[child]; at = child;
                }
                items[at] = last;
                return answer;
            }
        }

        public static Result Solve(Graph graph, int start, int end, Objective objective, int[] stops = null)
        {
            stops = stops ?? Array.Empty<int>();
            ValidateEndpoints(graph, start, end, stops);
            if (graph.blocked[start] || graph.blocked[end]) return Missing("Blocked endpoint", true);
            int count = graph.Count * (stops.Length + 1), initial = AdvanceStage(start, 0, stops) * graph.Count + start;
            var costs = new double[count]; var distances = new double[count]; var previous = new int[count];
            var versions = new int[count]; var totals = new Totals[count]; var closed = new bool[count];
            for (int i = 0; i < count; i++) { costs[i] = double.PositiveInfinity; distances[i] = double.PositiveInfinity; previous[i] = -1; }
            costs[initial] = distances[initial] = 0;
            var queue = new MinHeap(); queue.Push(initial, Heuristic(graph, start, initial / graph.Count, end, stops, objective), 0);
            int goal = -1, expanded = 0;
            while (queue.Count > 0)
            {
                QueueEntry entry = queue.Pop(); int state = entry.index;
                if (closed[state] || versions[state] != entry.version) continue;
                closed[state] = true; expanded++;
                int node = state % graph.Count, stage = state / graph.Count;
                if (node == end && stage == stops.Length) { goal = state; break; }
                foreach (Edge edge in graph.edges[node])
                {
                    int nextStage = AdvanceStage(edge.destination, stage, stops);
                    int next = nextStage * graph.Count + edge.destination;
                    if (graph.blocked[edge.destination] || closed[next]) continue;
                    Totals candidate = totals[state].Add(edge);
                    double primary = Cost(candidate, objective);
                    if (primary > costs[next] || primary == costs[next] && candidate.distance >= distances[next]) continue;
                    costs[next] = primary; distances[next] = candidate.distance; totals[next] = candidate; previous[next] = state;
                    versions[next]++;
                    double heuristic = Heuristic(graph, edge.destination, nextStage, end, stops, objective);
                    queue.Push(next, primary + heuristic, candidate.distance, versions[next]);
                }
            }
            if (goal < 0) return Missing("No feasible grid corridor", true, expanded);
            var path = new List<int>();
            for (int state = goal; state >= 0; state = previous[state]) path.Add(state % graph.Count);
            path.Reverse();
            return new Result { nodes = path, totals = totals[goal], exact = true, status = "Exact on the sampled graph", expandedLabels = expanded };
        }

        private sealed class Label
        {
            public int node, stage, parent;
            public Totals totals;
            public bool active = true;
        }

        public static Result SolveConstrained(Graph graph, int start, int end, MarsRoutingOptions options, int[] stops = null)
        {
            options.Validate(); stops = stops ?? Array.Empty<int>();
            ValidateEndpoints(graph, start, end, stops);
            if (graph.blocked[start] || graph.blocked[end]) return Missing("Blocked endpoint", true);
            int initialStage = AdvanceStage(start, 0, stops);
            var labels = new List<Label> { new Label { node = start, stage = initialStage, parent = -1 } };
            var atState = new List<int>[graph.Count * (stops.Length + 1)];
            atState[initialStage * graph.Count + start] = new List<int> { 0 };
            var queue = new MinHeap(); queue.Push(0, RemainingDistance(graph, start, initialStage, end, stops), 0);
            bool truncated = false; int expanded = 0, bestGoal = -1;
            while (queue.Count > 0)
            {
                int current = queue.Pop().index;
                Label label = labels[current];
                if (!label.active) continue;
                expanded++;
                if (label.node == end && label.stage == stops.Length)
                {
                    return LabelResult(labels, current, !truncated, expanded);
                }
                foreach (Edge edge in graph.edges[label.node])
                {
                    if (graph.blocked[edge.destination]) continue;
                    Totals candidate = label.totals.Add(edge);
                    int stage = AdvanceStage(edge.destination, label.stage, stops);
                    double remaining = RemainingDistance(graph, edge.destination, stage, end, stops);
                    if (!WithinBudgets(candidate, remaining, options)) continue;
                    int state = stage * graph.Count + edge.destination;
                    List<int> peers = atState[state];
                    if (peers == null) { peers = new List<int>(); atState[state] = peers; }
                    bool dominated = false;
                    foreach (int peer in peers) if (labels[peer].active && Dominates(labels[peer].totals, candidate)) { dominated = true; break; }
                    if (dominated) continue;
                    for (int i = peers.Count - 1; i >= 0; i--)
                    {
                        Label other = labels[peers[i]];
                        if (!other.active || Dominates(candidate, other.totals)) { other.active = false; peers.RemoveAt(i); }
                    }
                    if (peers.Count >= options.MaxLabelsPerState) { truncated = true; continue; }
                    if (labels.Count >= options.MaxLabels)
                        return bestGoal >= 0 ? LabelResult(labels, bestGoal, false, expanded) : Missing("Search limit reached before finding a feasible route", false, expanded);
                    int next = labels.Count;
                    var nextLabel = new Label { node = edge.destination, stage = stage, parent = current, totals = candidate };
                    labels.Add(nextLabel); peers.Add(next);
                    queue.Push(next, candidate.distance + remaining, candidate.risk);
                    if (nextLabel.node == end && stage == stops.Length && (bestGoal < 0 || candidate.distance < labels[bestGoal].totals.distance)) bestGoal = next;
                }
            }
            return Missing(truncated ? "Bounded search found no feasible route" : "No route satisfies all resource budgets", !truncated, expanded);
        }

        public static bool Dominates(Totals a, Totals b)
        {
            return a.distance <= b.distance && a.risk <= b.risk && a.minutes <= b.minutes && a.energy <= b.energy;
        }

        private static bool WithinBudgets(Totals totals, double remainingDistance, MarsRoutingOptions options)
        {
            // 42 m/min and 0.20 kW are lower bounds shared by the edge model.
            return totals.risk <= options.MaxRisk && totals.minutes + remainingDistance / 42 <= options.MaxTimeMinutes &&
                totals.energy + remainingDistance / 42 / 60 * .20 <= options.MaxEnergyKwh;
        }

        private static Result LabelResult(List<Label> labels, int goal, bool exact, int expanded)
        {
            var nodes = new List<int>();
            for (int at = goal; at >= 0; at = labels[at].parent) nodes.Add(labels[at].node);
            nodes.Reverse();
            return new Result { nodes = nodes, totals = labels[goal].totals, exact = exact,
                status = exact ? "Exact within budgets on the sampled graph" : "Feasible within budgets / search bounded", expandedLabels = expanded };
        }

        private static Result Missing(string status, bool exact, int expanded = 0)
        {
            return new Result { status = status, exact = exact, expandedLabels = expanded };
        }

        private static int AdvanceStage(int node, int stage, int[] stops)
        {
            while (stage < stops.Length && stops[stage] == node) stage++;
            return stage;
        }

        private static double RemainingDistance(Graph graph, int node, int stage, int end, int[] stops)
        {
            double distance = 0;
            for (int i = stage; i < stops.Length; i++) { distance += graph.LowerDistance(node, stops[i]); node = stops[i]; }
            return distance + graph.LowerDistance(node, end);
        }

        private static double Heuristic(Graph graph, int node, int stage, int end, int[] stops, Objective objective)
        {
            if (objective == Objective.Safest) return 0;
            double distance = RemainingDistance(graph, node, stage, end, stops);
            return objective == Objective.Energy ? distance / 42 / 60 * .20 : distance;
        }

        private static double Cost(Totals totals, Objective objective)
        {
            switch (objective)
            {
                case Objective.Safest: return totals.risk;
                case Objective.Balanced: return totals.distance + totals.risk * 2000;
                case Objective.Energy: return totals.energy;
                default: return totals.distance;
            }
        }

        private static void ValidateEndpoints(Graph graph, int start, int end, int[] stops)
        {
            if (graph == null || start < 0 || end < 0 || start >= graph.Count || end >= graph.Count)
                throw new ArgumentException("Invalid routing endpoints.");
            foreach (int stop in stops) if (stop < 0 || stop >= graph.Count) throw new ArgumentException("Invalid science stop.");
        }

        private static bool Finite(double value) => !double.IsNaN(value) && !double.IsInfinity(value);
    }
}

using System;
using System.Collections.Generic;
using UnityEngine;

namespace MarsPath
{
    /// <summary>Small independent enumeration catches objective substitution,
    /// incorrect risk aggregation, and unsafe constrained-search fallbacks.</summary>
    public sealed class MarsTeamRoutingCheck : MonoBehaviour
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            foreach (string argument in Environment.GetCommandLineArgs())
                if (argument == "--mars-team-routing-check")
                    new GameObject("Team routing correctness checks").AddComponent<MarsTeamRoutingCheck>();
        }

        private void Start()
        {
            try
            {
                RunChecks();
                Debug.Log("TEAM_ROUTING_PASSED: 40 randomized graphs checked against independent simple-path enumeration; distance A*, lexicographic risk Dijkstra, balanced A*, energy A*, RCSP budgets, ordered stages, and bounded-search status.");
                Application.Quit(0);
            }
            catch (Exception error)
            {
                Debug.LogError("TEAM_ROUTING_FAILED: " + error);
                Application.Quit(2);
            }
        }

        public static void RunChecks()
        {
            MarsDtmVoidEstimates.SelfCheck();
            var random = new System.Random(4112026);
            for (int test = 0; test < 40; test++)
            {
                var graph = new MarsGraphRouting.Graph(6);
                for (int i = 0; i < graph.Count; i++) graph.x[i] = i;
                for (int a = 0; a < graph.Count; a++)
                    for (int b = 0; b < graph.Count; b++)
                    {
                        if (a == b || b != a + 1 && random.NextDouble() < .55) continue;
                        double distance = Math.Abs(a - b) + .25 + random.NextDouble() * 3;
                        double risk = random.Next(0, 8) * .1;
                        double minutes = distance / 42 + random.NextDouble() * .04;
                        double energy = minutes / 60 * .2 + random.NextDouble() * .001;
                        graph.AddEdge(a, new MarsGraphRouting.Edge(b, distance, risk, minutes, energy));
                    }
                List<MarsGraphRouting.Totals> paths = Enumerate(graph, 0, 5);
                Assert(paths.Count > 0, "The mandatory forward chain must be feasible");
                foreach (MarsGraphRouting.Objective objective in Enum.GetValues(typeof(MarsGraphRouting.Objective)))
                {
                    MarsGraphRouting.Result actual = MarsGraphRouting.Solve(graph, 0, 5, objective);
                    MarsGraphRouting.Totals expected = Best(paths, objective);
                    Assert(actual.Available && actual.exact, "An unrestricted tiny graph needs an exact result");
                    Near(Cost(actual.totals, objective), Cost(expected, objective), "Objective " + objective);
                    if (objective == MarsGraphRouting.Objective.Safest) Near(actual.totals.distance, expected.distance, "Risk ties use distance");
                }
                var limits = new MarsRoutingOptions
                {
                    ConstraintsEnabled = true, MaxRisk = .9f,
                    MaxTimeMinutes = .4f, MaxEnergyKwh = .003f,
                    MaxLabels = 10000, MaxLabelsPerState = 500
                };
                MarsGraphRouting.Result constrained = MarsGraphRouting.SolveConstrained(graph, 0, 5, limits);
                bool feasible = false; double shortest = double.PositiveInfinity;
                foreach (MarsGraphRouting.Totals path in paths)
                    if (path.risk <= limits.MaxRisk && path.minutes <= limits.MaxTimeMinutes && path.energy <= limits.MaxEnergyKwh)
                    { feasible = true; shortest = Math.Min(shortest, path.distance); }
                Assert(constrained.Available == feasible && constrained.exact, "Constrained feasibility must match enumeration");
                if (feasible) Near(constrained.totals.distance, shortest, "Constrained shortest distance");
            }
            CheckOrderedStages();
            CheckResourceLimits();
            CheckSearchLimitStatus();
            CheckBlockedNodes();
        }

        private static List<MarsGraphRouting.Totals> Enumerate(MarsGraphRouting.Graph graph, int start, int end)
        {
            var paths = new List<MarsGraphRouting.Totals>(); var visited = new bool[graph.Count];
            Action<int, MarsGraphRouting.Totals> visit = null;
            visit = (node, totals) =>
            {
                if (node == end) { paths.Add(totals); return; }
                visited[node] = true;
                foreach (MarsGraphRouting.Edge edge in graph.edges[node])
                    if (!visited[edge.destination] && !graph.blocked[edge.destination]) visit(edge.destination, totals.Add(edge));
                visited[node] = false;
            };
            visit(start, new MarsGraphRouting.Totals());
            return paths;
        }

        private static MarsGraphRouting.Totals Best(List<MarsGraphRouting.Totals> paths, MarsGraphRouting.Objective objective)
        {
            MarsGraphRouting.Totals best = paths[0];
            foreach (MarsGraphRouting.Totals path in paths)
            {
                double value = Cost(path, objective), previous = Cost(best, objective);
                if (value < previous || value == previous && path.distance < best.distance) best = path;
            }
            return best;
        }

        private static double Cost(MarsGraphRouting.Totals totals, MarsGraphRouting.Objective objective)
        {
            switch (objective)
            {
                case MarsGraphRouting.Objective.Safest: return totals.risk;
                case MarsGraphRouting.Objective.Balanced: return totals.distance + totals.risk * 2000;
                case MarsGraphRouting.Objective.Energy: return totals.energy;
                default: return totals.distance;
            }
        }

        private static void CheckOrderedStages()
        {
            var graph = new MarsGraphRouting.Graph(3);
            graph.AddEdge(0, new MarsGraphRouting.Edge(1, 1, 1, 1, 1));
            graph.AddEdge(1, new MarsGraphRouting.Edge(0, 1, 1, 1, 1));
            graph.AddEdge(0, new MarsGraphRouting.Edge(2, 1, 1, 1, 1));
            MarsGraphRouting.Result result = MarsGraphRouting.Solve(graph, 0, 2, MarsGraphRouting.Objective.Shortest, new[] { 1 });
            Assert(result.Available && result.nodes.Count == 4 && result.nodes[1] == 1 && result.nodes[2] == 0, "A later stage may revisit a prior physical node");
            var limits = new MarsRoutingOptions { ConstraintsEnabled = true, MaxRisk = 2, MaxTimeMinutes = 10, MaxEnergyKwh = 10 };
            Assert(!MarsGraphRouting.SolveConstrained(graph, 0, 2, limits, new[] { 1 }).Available, "One risk budget is shared across waypoint legs");
            limits.MaxRisk = 3;
            Assert(MarsGraphRouting.SolveConstrained(graph, 0, 2, limits, new[] { 1 }).Available, "An exactly fitting shared budget remains feasible");
        }

        private static void CheckResourceLimits()
        {
            var graph = new MarsGraphRouting.Graph(4);
            graph.AddEdge(0, new MarsGraphRouting.Edge(1, 1, 4, 1, 1));
            graph.AddEdge(1, new MarsGraphRouting.Edge(3, 1, 4, 1, 1));
            graph.AddEdge(0, new MarsGraphRouting.Edge(2, 2, 1, 2, 2));
            graph.AddEdge(2, new MarsGraphRouting.Edge(3, 2, 1, 2, 2));
            var limits = new MarsRoutingOptions { ConstraintsEnabled = true, MaxRisk = 2, MaxTimeMinutes = 4, MaxEnergyKwh = 4 };
            MarsGraphRouting.Result result = MarsGraphRouting.SolveConstrained(graph, 0, 3, limits);
            Assert(result.Available && result.exact && result.nodes[1] == 2, "A safer detour beats an over-budget shortcut");
            limits.MaxTimeMinutes = 3;
            Assert(!MarsGraphRouting.SolveConstrained(graph, 0, 3, limits).Available, "Risk feasibility cannot override the time budget");
            limits.MaxTimeMinutes = 4; limits.MaxEnergyKwh = 3;
            Assert(!MarsGraphRouting.SolveConstrained(graph, 0, 3, limits).Available, "Risk feasibility cannot override the energy budget");
        }

        private static void CheckSearchLimitStatus()
        {
            var graph = new MarsGraphRouting.Graph(105);
            for (int i = 1; i < 104; i++)
            {
                graph.AddEdge(0, new MarsGraphRouting.Edge(i, i, 1, 1, 1));
                graph.AddEdge(i, new MarsGraphRouting.Edge(104, 200, 1, 1, 1));
            }
            var limits = new MarsRoutingOptions { ConstraintsEnabled = true, MaxRisk = 10, MaxTimeMinutes = 10, MaxEnergyKwh = 10, MaxLabels = 100 };
            MarsGraphRouting.Result result = MarsGraphRouting.SolveConstrained(graph, 0, 104, limits);
            Assert(!result.exact && !result.Available && result.status.Contains("limit"), "A search cap cannot be reported as exact infeasibility");
        }

        private static void CheckBlockedNodes()
        {
            var graph = new MarsGraphRouting.Graph(3);
            graph.AddEdge(0, new MarsGraphRouting.Edge(1, 1, 0, 1, 1));
            graph.AddEdge(1, new MarsGraphRouting.Edge(2, 1, 0, 1, 1));
            graph.blocked[1] = true;
            Assert(!MarsGraphRouting.Solve(graph, 0, 2, MarsGraphRouting.Objective.Shortest).Available, "Missing or blocked nodes cannot be crossed");
        }

        private static void Near(double actual, double expected, string label)
        {
            Assert(Math.Abs(actual - expected) <= 1e-8 * Math.Max(1, Math.Abs(expected)), label + " expected " + expected + " got " + actual);
        }

        private static void Assert(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }
    }
}

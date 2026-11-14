using UnityEngine;

namespace MarsPath
{
    public partial class MarsPathDemoBootstrap
    {
        private bool showRouteLimits;

        private void DrawRouteEvidenceSheet(RouteData route)
        {
            Color accent = MarsPathRouteView.ParseColor(route.colorHex, UiCyan);
            Rect sheet = new Rect(981, 120, 576, 505);
            PlanningCard(sheet, accent);
            UiFill(new Rect(sheet.x + 3, sheet.y, sheet.width - 3, 3), accent);
            GUI.Label(new Rect(1004, 137, 481, 29), route.displayName.ToUpperInvariant(), planLabel);
            if (GUI.Button(new Rect(1504, 132, 32, 29), "×", planButton)) showPlanningDetails = false;

            if (!desktopBrowsingRoutes)
            {
                if (GUI.Button(new Rect(1004, 177, 253, 31), "DATA", showRouteLimits ? planButton : planSelected))
                    showRouteLimits = false;
                if (GUI.Button(new Rect(1271, 177, 259, 31), "ROUTE LIMITS", showRouteLimits ? planSelected : planButton))
                    showRouteLimits = true;
                if (showRouteLimits)
                {
                    DrawRouteBudgetEditor();
                    return;
                }
            }

            var wrapped = new GUIStyle(planCaption) { wordWrap = true };
            string algorithm = string.IsNullOrWhiteSpace(route.algorithm) ? "Bundled route profile" : route.algorithm;
            GUI.Label(new Rect(1004, 219, 526, 23), algorithm, planCaption);
            string status = string.IsNullOrWhiteSpace(route.optimalityStatus) ? "Bundled mission data" : route.optimalityStatus;
            if (route.paretoAlternative) status = "PARETO ALTERNATIVE · " + status;
            GUI.Label(new Rect(1004, 245, 526, 33), status + $" · graph {route.routingGridMeters:0.#} m", wrapped);

            if (!route.available)
            {
                GUI.Label(new Rect(1004, 300, 526, 29), "NO FEASIBLE ROUTE FOR THIS PROFILE", planLabel);
                GUI.Label(new Rect(1004, 344, 526, 57),
                    "Review the route limits or choose different endpoints. Resource totals are available after a route is found.", wrapped);
                if (!desktopBrowsingRoutes && GUI.Button(new Rect(1004, 420, 526, 39), "ADJUST ROUTE LIMITS", planButton))
                    showRouteLimits = true;
                return;
            }

            MarsRouteFacts facts = MarsRouteFacts.For(mission, route, SelectedElevationProfile);
            EvidenceMetric(1004, 284, "TOTAL RISK / risk·km", route.totalRisk.ToString("0.##"), 163);
            EvidenceMetric(1183, 284, "TIME ESTIMATE", (route.etaMinutes / 60f).ToString("0.##") + " h", 163);
            EvidenceMetric(1362, 284, "ENERGY ESTIMATE", route.estimatedEnergyKwh.ToString("0.###") + " kWh", 168);
            EvidenceMetric(1004, 342, "MIN / MAX ELEVATION", $"{facts.minimumElevationMeters:0} / {facts.maximumElevationMeters:0} m");
            EvidenceMetric(1282, 342, "ASCENT / DESCENT", $"{facts.ascentMeters:0} / {facts.descentMeters:0} m");
            EvidenceMetric(1004, 400, "MAX TRACK GRADE", facts.maxGradeDegrees.ToString("0.0") + "°");
            EvidenceMetric(1282, 400, "PROFILE SAMPLE STEP", facts.profileStepMeters.ToString("0.#") + " m");
            GUI.Label(new Rect(1004, 462, 526, 20), "ELEVATION COVERAGE / SAMPLED ROUTE LENGTH", planCaption);
            for (int i = 0; i < Mathf.Min(2, facts.sources.Length); i++)
            {
                var source = facts.sources[i];
                float y = 485 + i * 28;
                GUI.Label(new Rect(1004, y, 406, 28), source.source, wrapped);
                GUI.Label(new Rect(1410, y, 120, 23), source.percent.ToString("0.0") + "%",
                    new GUIStyle(planSmall) { alignment = TextAnchor.UpperRight });
            }
            GUI.Label(new Rect(1004, 544, 526, 20),
                $"GRID {facts.finestGridMeters:0.#}–{facts.coarsestGridMeters:0.#} m · {facts.sources.Length} sources · spacing ≠ accuracy", planCaption);
            GUI.Label(new Rect(1004, 568, 526, 20),
                $"MODELED SUIT DEMAND · {facts.estimatedOxygenGrams:0} g O₂ before resupply", planCaption);
            GUI.Label(new Rect(1004, 588, 526, 34),
                "Elevation is sampled from the DEM. Risk, time, energy and science scores use the prototype model.", wrapped);
        }

        private void EvidenceMetric(float x, float y, string label, string value, float width = 258)
        {
            GUI.Label(new Rect(x, y, width, 22), label, planCaption);
            GUI.Label(new Rect(x, y + 23, width, 30), value, new GUIStyle(planLabel) { fontSize = 20 });
        }
    }
}

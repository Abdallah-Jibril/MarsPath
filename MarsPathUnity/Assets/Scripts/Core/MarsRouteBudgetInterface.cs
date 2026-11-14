using System;
using System.Collections;
using System.Globalization;
using UnityEngine;

namespace MarsPath
{
    public partial class MarsPathDemoBootstrap
    {
        private MissionData budgetEditorMission;
        private bool budgetConstraintsEnabled, budgetReplanning;
        private string budgetRisk = "", budgetHours = "", budgetEnergy = "", budgetSlope = "25";
        private string budgetMessage = "";
        private GUIStyle budgetInputStyle;

        private void InitializeBudgetEditor()
        {
            if (budgetEditorMission == mission) return;
            budgetEditorMission = mission;
            MarsRoutingOptions options = mission.routingOptions ?? new MarsRoutingOptions();
            budgetConstraintsEnabled = options.ConstraintsEnabled;
            budgetRisk = BudgetInput(options.MaxRisk);
            budgetHours = BudgetInput(options.MaxTimeMinutes / 60f);
            budgetEnergy = BudgetInput(options.MaxEnergyKwh);
            budgetSlope = options.MaxSlopeDegrees.ToString("G7", CultureInfo.InvariantCulture);
            budgetMessage = "";
        }

        private static string BudgetInput(float value)
        {
            return IsPositiveFinite(value) ? value.ToString("G7", CultureInfo.InvariantCulture) : "";
        }

        private static bool IsPositiveFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value) && value > 0;
        }

        private static bool ReadBudget(string text, out float value)
        {
            return float.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out value) && IsPositiveFinite(value);
        }

        private bool TryRoutingOptions(out MarsRoutingOptions options, out string message)
        {
            options = (mission.routingOptions ?? new MarsRoutingOptions()).Clone();
            message = "";
            if (!ReadBudget(budgetSlope, out float slope) || slope >= 90)
            {
                message = "Enter a maximum slope greater than 0 and below 90°.";
                return false;
            }
            options.MaxSlopeDegrees = slope;
            options.ConstraintsEnabled = budgetConstraintsEnabled;
            if (!budgetConstraintsEnabled) return true;
            if (!ReadBudget(budgetRisk, out float risk) || !ReadBudget(budgetHours, out float hours) ||
                !ReadBudget(budgetEnergy, out float energy) || !IsPositiveFinite(hours * 60))
            {
                message = "Fill risk, time and energy with positive numbers to enable resource limits.";
                return false;
            }
            options.MaxRisk = risk;
            options.MaxTimeMinutes = hours * 60;
            options.MaxEnergyKwh = energy;
            return true;
        }

        private void DrawRouteBudgetEditor()
        {
            InitializeBudgetEditor();
            if (budgetInputStyle == null)
                budgetInputStyle = new GUIStyle(planButton)
                {
                    fontSize = 17, alignment = TextAnchor.MiddleLeft,
                    padding = new RectOffset(10, 10, 3, 3)
                };
            var wrapped = new GUIStyle(planCaption) { wordWrap = true };
            GUI.Label(new Rect(1004, 219, 526, 23), "LIMITS FOR THE COMPLETE SELECTED ROUTE", planCaption);
            GUI.enabled = !budgetReplanning;
            GUI.Label(new Rect(1004, 255, 345, 24), "USE RESOURCE LIMITS", planSmall);
            if (GUI.Button(new Rect(1382, 246, 148, 32), budgetConstraintsEnabled ? "ON" : "OFF",
                budgetConstraintsEnabled ? planSelected : planButton))
                budgetConstraintsEnabled = !budgetConstraintsEnabled;

            budgetSlope = BudgetField(294, "MAXIMUM SLOPE / degrees", budgetSlope);
            GUI.enabled = !budgetReplanning && budgetConstraintsEnabled;
            budgetRisk = BudgetField(340, "TOTAL RISK / risk·km", budgetRisk);
            budgetHours = BudgetField(386, "TOTAL TIME / hours", budgetHours);
            budgetEnergy = BudgetField(432, "TOTAL ENERGY / kWh", budgetEnergy);
            GUI.enabled = true;

            bool valid = TryRoutingOptions(out MarsRoutingOptions requested, out string validation);
            string explanation = budgetConstraintsEnabled
                ? "The Within limits search checks risk, time and energy together. Enter your mission budgets."
                : "The five route profiles use the selected maximum slope. Enable resource limits to enter mission budgets.";
            GUI.Label(new Rect(1004, 478, 526, 43), explanation, wrapped);
            GUI.enabled = valid && !budgetReplanning;
            if (GUI.Button(new Rect(1004, 531, 526, 39), budgetReplanning ? "COMPUTING ROUTES…" : "APPLY & RECOMPUTE", planButton))
                StartCoroutine(ApplyRouteBudgets(requested));
            GUI.enabled = true;
            string status = valid ? budgetMessage : validation;
            GUI.Label(new Rect(1004, 582, 526, 35), status, wrapped);
        }

        private string BudgetField(float y, string label, string value)
        {
            GUI.Label(new Rect(1004, y + 3, 307, 26), label, planCaption);
            return GUI.TextField(new Rect(1320, y, 210, 29), value, 24, budgetInputStyle);
        }

        private IEnumerator ApplyRouteBudgets(MarsRoutingOptions options)
        {
            budgetReplanning = true;
            budgetMessage = "Computing routes for the selected endpoints…";
            MissionData previousMission = mission;
            MissionData previousTerrain = MarsRegionalTerrain.Current;
            int previousProfile = selectedRoute;
            MarsPathCameraRig.PlanningView view = cameraRig.CapturePlanningView();
            yield return null;
            if (mission != previousMission || missionStarted || GlobalView.IsVisible)
            {
                budgetReplanning = false;
                budgetMessage = "";
                yield break;
            }
            try
            {
                CancelTerrainRefinement();
                MissionData computed = MarsRoutePlanner.Generate(previousMission.currentLocation, previousMission.destination, options);
                LoadRegionalMission(computed);
                if (previousProfile < computed.routes.Length && computed.routes[previousProfile].available)
                    SelectRoute(previousProfile);
                cameraRig.RestorePlanningView(view);
                budgetEditorMission = null;
                InitializeBudgetEditor();
                showPlanningDetails = true;
                showRouteLimits = true;
                budgetMessage = "Route limits applied.";
                BeginTerrainRefinement();
            }
            catch (Exception error)
            {
                if (previousTerrain != null) MarsRegionalTerrain.Configure(previousTerrain);
                if (mission != previousMission)
                {
                    LoadRegionalMission(previousMission);
                    SelectRoute(previousProfile);
                }
                cameraRig.RestorePlanningView(view);
                budgetMessage = "Unable to apply route limits. " + error.Message;
                Debug.LogWarning(budgetMessage);
            }
            finally
            {
                budgetReplanning = false;
            }
        }
    }
}

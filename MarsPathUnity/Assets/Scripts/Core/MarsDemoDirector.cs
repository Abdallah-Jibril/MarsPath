using System;
using UnityEngine;

namespace MarsPath
{
    /// <summary>Advances the route and countdown together in active walking time.</summary>
    public sealed class MarsDemoDirector
    {
        public const float DurationSeconds = 120f;
        public const int TerrainStationCount = 8;
        private const double MaxDemoSpeedMetresPerSecond = 1.6;
        private double elapsedSeconds;
        public float Elapsed => (float)elapsedSeconds;
        public int TerrainStation => Math.Min(TerrainStationCount - 1, (int)(elapsedSeconds / DurationSeconds * TerrainStationCount));
        public float Progress { get; private set; }
        public double DistanceMeters { get; private set; }
        public bool Complete => elapsedSeconds >= DurationSeconds;
        public static double PreviewMetres(double routeLengthMetres) =>
            Math.Min(routeLengthMetres, DurationSeconds * MaxDemoSpeedMetresPerSecond);

        public static float StationProgress(int index, double routeLengthKm)
        {
            double length = routeLengthKm * 1000;
            return length <= 0 ? 0 : (float)(PreviewMetres(length) / length * Mathf.Clamp01(index / (float)(TerrainStationCount - 1)));
        }

        public void Reset()
        {
            elapsedSeconds = 0;
            Progress = 0;
            DistanceMeters = 0;
        }

        public void Advance(double walkingSeconds, double routeLengthKm, bool fullRoute = false)
        {
            elapsedSeconds = Math.Clamp(elapsedSeconds + Math.Max(0, walkingSeconds), 0, DurationSeconds);
            double length = routeLengthKm * 1000;
            double demoLength = fullRoute ? length : PreviewMetres(length);
            DistanceMeters = demoLength * (elapsedSeconds / DurationSeconds);
            Progress = length <= 0 ? 0 : (float)(DistanceMeters / length);
        }

        internal static string RemainingLabel(float elapsed)
        {
            int seconds = Mathf.CeilToInt(Mathf.Max(0, DurationSeconds - elapsed));
            return $"{seconds / 60}:{seconds % 60:00}";
        }
    }
}

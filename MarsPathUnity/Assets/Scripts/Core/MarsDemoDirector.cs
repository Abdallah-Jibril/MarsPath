using UnityEngine;

namespace MarsPath
{
    /// <summary>Distance clock for the continuous two-minute traverse.</summary>
    public sealed class MarsDemoDirector
    {
        public const float DurationSeconds=120f;
        public const int TerrainStationCount = 8;
        private const double MaxDemoSpeedMetresPerSecond = 1.6;
        public float Elapsed { get; private set; }
        public int TerrainStation => Mathf.Min(TerrainStationCount - 1, Mathf.FloorToInt(Elapsed / DurationSeconds * TerrainStationCount));
        public float Progress { get; private set; }
        public double DistanceMeters { get; private set; }
        public bool Complete=>Elapsed>=DurationSeconds;
        public static double PreviewMetres(double routeLengthMetres) =>
            System.Math.Min(routeLengthMetres, DurationSeconds * MaxDemoSpeedMetresPerSecond);

        public static float StationProgress(int index, double routeLengthKm)
        {
            double length = routeLengthKm * 1000;
            return length <= 0 ? 0 : (float)(PreviewMetres(length) / length * Mathf.Clamp01(index / (float)(TerrainStationCount - 1)));
        }

        public void Reset() { Elapsed = 0; Progress = 0; DistanceMeters = 0; }

        public void Advance(float realSeconds, double routeLengthKm)
        {
            Elapsed = Mathf.Clamp(Elapsed + Mathf.Max(0, realSeconds), 0, DurationSeconds);
            double length = routeLengthKm * 1000;
            DistanceMeters = PreviewMetres(length) * (Elapsed / DurationSeconds);
            Progress = length <= 0 ? 0 : (float)(DistanceMeters / length);
        }
    }
}

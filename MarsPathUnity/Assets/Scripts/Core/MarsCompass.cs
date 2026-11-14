using UnityEngine;

namespace MarsPath
{
    internal static class MarsCompass
    {
        private static readonly string[] Directions = { "N", "NE", "E", "SE", "S", "SW", "W", "NW" };

        internal static bool TryGetHeading(Vector3 forward, out float bearing)
        {
            bearing = 0;
            // The surface frame has +Z north and +X east. A nearly vertical
            // gaze has no stable horizontal direction.
            if (!(forward.x * forward.x + forward.z * forward.z > .01f)) return false;
            bearing = Mathf.Repeat(Mathf.Atan2(forward.x, forward.z) * Mathf.Rad2Deg, 360f);
            if (bearing >= 360f) bearing = 0;
            return true;
        }

        internal static string Cardinal(float bearing) =>
            Directions[Mathf.FloorToInt((Mathf.Repeat(bearing, 360f) + 22.5f) / 45f) % Directions.Length];

        internal static string Format(float bearing) =>
            $"{Cardinal(bearing)}  {Mathf.RoundToInt(Mathf.Repeat(bearing, 360f)) % 360:000}°";
    }
}

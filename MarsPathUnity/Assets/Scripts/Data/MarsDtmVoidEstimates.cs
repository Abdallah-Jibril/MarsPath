using System;
using System.Collections.Generic;

namespace MarsPath
{
    /// <summary>Separate display estimates for small, enclosed source voids.
    /// Original native samples stay untouched. Routing can reject estimates.</summary>
    public static class MarsDtmVoidEstimates
    {
        private const int MaximumPixels = 512;
        private const float MaximumDiameterMeters = 400;

        public static Dictionary<int, float> Build(float[] raw, int width, int height, float pixelMeters)
        {
            if (raw == null || width < 2 || height < 2 || raw.Length != (long)width * height ||
                float.IsNaN(pixelMeters) || float.IsInfinity(pixelMeters) || pixelMeters <= 0)
                throw new ArgumentException("Invalid native terrain layout for display gap estimates.");

            var estimates = new Dictionary<int, float>();
            var visited = new bool[raw.Length];
            var queue = new Queue<int>();
            var component = new List<int>(MaximumPixels);
            var boundary = new HashSet<int>();
            for (int seed = 0; seed < raw.Length; seed++)
            {
                if (visited[seed] || Valid(raw[seed])) continue;
                queue.Clear(); component.Clear(); boundary.Clear();
                queue.Enqueue(seed); visited[seed] = true;
                int count = 0, minX = width, maxX = 0, minY = height, maxY = 0;
                bool open = false;
                while (queue.Count > 0)
                {
                    int index = queue.Dequeue(), x = index % width, y = index / width;
                    count++;
                    minX = Math.Min(minX, x); maxX = Math.Max(maxX, x);
                    minY = Math.Min(minY, y); maxY = Math.Max(maxY, y);
                    if (x == 0 || y == 0 || x == width - 1 || y == height - 1) open = true;
                    if (count <= MaximumPixels) component.Add(index);
                    for (int dy = -1; dy <= 1; dy++)
                    {
                        for (int dx = -1; dx <= 1; dx++)
                        {
                            if (dx == 0 && dy == 0) continue;
                            int nx = x + dx, ny = y + dy;
                            if (nx < 0 || ny < 0 || nx >= width || ny >= height) continue;
                            int next = ny * width + nx;
                            if (Valid(raw[next]))
                            {
                                if (count <= MaximumPixels) boundary.Add(next);
                            }
                            else if (!visited[next])
                            {
                                visited[next] = true; queue.Enqueue(next);
                            }
                        }
                    }
                }
                double spanX = maxX - minX + 1, spanY = maxY - minY + 1;
                double diameter = Math.Sqrt(spanX * spanX + spanY * spanY) * pixelMeters;
                if (open || count > MaximumPixels || diameter > MaximumDiameterMeters || boundary.Count < 3) continue;
                float low = float.PositiveInfinity, high = float.NegativeInfinity;
                foreach (int neighbor in boundary)
                {
                    low = Math.Min(low, raw[neighbor]); high = Math.Max(high, raw[neighbor]);
                }
                foreach (int index in component)
                {
                    int x = index % width, y = index / width;
                    double weighted = 0, weights = 0;
                    foreach (int neighbor in boundary)
                    {
                        double dx = neighbor % width - x, dy = neighbor / width - y;
                        double weight = 1 / (dx * dx + dy * dy);
                        weighted += raw[neighbor] * weight; weights += weight;
                    }
                    estimates.Add(index, (float)Math.Max(low, Math.Min(high, weighted / weights)));
                }
            }
            return estimates;
        }

        public static bool Valid(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value) && value > -10000 && value < 30000;
        }

        public static void SelfCheck()
        {
            const int size = 40;
            var raw = new float[size * size];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++) raw[y * size + x] = 100 + x * 2 + y;
            int hole = 10 * size + 10; raw[hole] = float.NaN;
            for (int x = 0; x < 8; x++) raw[25 * size + x] = float.NaN;
            for (int y = 15; y < 39; y++)
                for (int x = 15; x < 39; x++) raw[y * size + x] = float.NaN;
            float[] original = (float[])raw.Clone();
            Dictionary<int, float> estimates = Build(raw, size, size, 20);
            if (estimates.Count != 1 || !estimates.TryGetValue(hole, out float estimate) || estimate < 127 || estimate > 133)
                throw new InvalidOperationException("Small source-void interpolation is outside the native boundary range.");
            for (int i = 0; i < raw.Length; i++)
                if (!raw[i].Equals(original[i])) throw new InvalidOperationException("Original terrain samples were modified.");
            if (Build(raw, size, size, 500).Count != 0)
                throw new InvalidOperationException("Large physical source voids must remain unresolved.");
        }
    }
}

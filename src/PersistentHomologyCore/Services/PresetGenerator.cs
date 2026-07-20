using PersistentHomologyCore.Models;

namespace PersistentHomologyCore.Services;

/// <summary>
/// Deterministic point-cloud generators for the six presets. Every method is
/// pure in (kind, count, noise, seed): same inputs always produce the same
/// cloud, and noise = 0 lies exactly on the ideal locus (no jitter).
/// </summary>
public static class PresetGenerator
{
    public const int MaxPoints = 300;

    public static List<Point2D> Generate(
        PresetKind kind,
        int count,
        double noise,
        int seed,
        double width = 640,
        double height = 640)
    {
        count = Math.Clamp(count, 1, MaxPoints);
        var random = new Random(seed);
        double cx = width / 2;
        double cy = height / 2;
        double radius = Math.Min(width, height) * 0.35;

        return kind switch
        {
            PresetKind.NoisyCircle => NoisyCircle(count, noise, radius, cx, cy, random),
            PresetKind.TwoCircles => TwoCircles(count, noise, radius, cx, cy, random),
            PresetKind.FigureEight => FigureEight(count, noise, radius, cx, cy, random),
            PresetKind.Annulus => Annulus(count, noise, radius, cx, cy, random),
            PresetKind.RandomClusters => RandomClusters(count, noise, width, height, random),
            PresetKind.Spiral => Spiral(count, noise, radius, cx, cy, random),
            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null)
        };
    }

    private static List<Point2D> NoisyCircle(int count, double noise, double radius, double cx, double cy, Random random)
    {
        var points = new List<Point2D>(count);
        double jitter = noise * radius * 0.25;
        for (int i = 0; i < count; i++)
        {
            double angle = 2 * Math.PI * i / count;
            double r = radius + jitter * NextGaussian(random);
            points.Add(new Point2D(cx + r * Math.Cos(angle), cy + r * Math.Sin(angle)));
        }
        return points;
    }

    private static List<Point2D> TwoCircles(int count, double noise, double radius, double cx, double cy, Random random)
    {
        var points = new List<Point2D>(count);
        double smallRadius = radius * 0.5;
        double gap = smallRadius * 2.9;
        double jitter = noise * smallRadius * 0.25;
        int half = count / 2;

        for (int i = 0; i < count; i++)
        {
            bool left = i < half;
            int localCount = left ? half : count - half;
            int localIndex = left ? i : i - half;
            double angle = 2 * Math.PI * localIndex / localCount;
            double r = smallRadius + jitter * NextGaussian(random);
            double centerX = cx + (left ? -gap / 2 : gap / 2);
            points.Add(new Point2D(centerX + r * Math.Cos(angle), cy + r * Math.Sin(angle)));
        }
        return points;
    }

    private static List<Point2D> FigureEight(int count, double noise, double radius, double cx, double cy, Random random)
    {
        var points = new List<Point2D>(count);
        double lobeRadius = radius * 0.5;
        double jitter = noise * lobeRadius * 0.25;
        int half = count / 2;

        for (int i = 0; i < count; i++)
        {
            bool top = i < half;
            int localCount = top ? half : count - half;
            int localIndex = top ? i : i - half;
            double angle = 2 * Math.PI * localIndex / localCount;
            double r = lobeRadius + jitter * NextGaussian(random);
            double centerY = cy + (top ? -lobeRadius : lobeRadius);
            points.Add(new Point2D(cx + r * Math.Cos(angle), centerY + r * Math.Sin(angle)));
        }
        return points;
    }

    private static List<Point2D> Annulus(int count, double noise, double radius, double cx, double cy, Random random)
    {
        var points = new List<Point2D>(count);
        double innerRadius = radius * 0.55;
        double outerRadius = radius;
        double band = (outerRadius - innerRadius) * (0.15 + 0.85 * noise);

        for (int i = 0; i < count; i++)
        {
            double angle = random.NextDouble() * 2 * Math.PI;
            double mid = (innerRadius + outerRadius) / 2;
            double r = Math.Clamp(mid + (random.NextDouble() * 2 - 1) * band, innerRadius * 0.5, outerRadius * 1.2);
            points.Add(new Point2D(cx + r * Math.Cos(angle), cy + r * Math.Sin(angle)));
        }
        return points;
    }

    private static List<Point2D> RandomClusters(int count, double noise, double width, double height, Random random)
    {
        int clusterCount = Math.Clamp(4 + random.Next(3), 4, 6);
        var centers = new List<Point2D>(clusterCount);
        double margin = Math.Min(width, height) * 0.15;

        for (int c = 0; c < clusterCount; c++)
        {
            centers.Add(new Point2D(
                margin + random.NextDouble() * (width - 2 * margin),
                margin + random.NextDouble() * (height - 2 * margin)));
        }

        double spread = Math.Min(width, height) * (0.03 + 0.10 * noise);
        var points = new List<Point2D>(count);
        for (int i = 0; i < count; i++)
        {
            var center = centers[i % clusterCount];
            points.Add(new Point2D(
                center.X + spread * NextGaussian(random),
                center.Y + spread * NextGaussian(random)));
        }
        return points;
    }

    private static List<Point2D> Spiral(int count, double noise, double radius, double cx, double cy, Random random)
    {
        var points = new List<Point2D>(count);
        double turns = 2.5;
        double jitter = noise * radius * 0.08;

        for (int i = 0; i < count; i++)
        {
            double t = (double)i / Math.Max(1, count - 1);
            double angle = t * turns * 2 * Math.PI;
            double r = t * radius + jitter * NextGaussian(random);
            points.Add(new Point2D(cx + r * Math.Cos(angle), cy + r * Math.Sin(angle)));
        }
        return points;
    }

    /// <summary>Standard normal sample via the Box-Muller transform.</summary>
    private static double NextGaussian(Random random)
    {
        double u1 = 1.0 - random.NextDouble();
        double u2 = random.NextDouble();
        return Math.Sqrt(-2.0 * Math.Log(u1)) * Math.Sin(2.0 * Math.PI * u2);
    }
}

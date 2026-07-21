using PersistentHomologyCore.Models;

namespace PersistentHomologyCore.Services;

/// <summary>
/// Deterministic point-cloud generators for the seven presets. Every method
/// is pure in (kind, count, noise, seed): same inputs always produce the
/// same cloud, and noise = 0 lies exactly on the ideal locus (no jitter).
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
        double radius = Math.Min(width, height) * 0.40;

        return kind switch
        {
            PresetKind.NoisyCircle => NoisyCircle(count, noise, radius, cx, cy, random),
            PresetKind.TwoCircles => TwoCircles(count, noise, radius, cx, cy, random),
            PresetKind.ThreeCircles => ThreeCircles(count, noise, radius, cx, cy, random),
            PresetKind.CircleSquareLine => CircleSquareLine(count, noise, radius, cx, cy, random),
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

    private static List<Point2D> ThreeCircles(int count, double noise, double radius, double cx, double cy, Random random)
    {
        var points = new List<Point2D>(count);
        double smallRadius = radius * 0.35;
        double arrangementRadius = radius * 0.75;
        double jitter = noise * smallRadius * 0.25;
        int per = count / 3;

        for (int c = 0; c < 3; c++)
        {
            int localCount = c < 2 ? per : count - 2 * per;
            double centerAngle = -Math.PI / 2 + c * 2 * Math.PI / 3;
            double centerX = cx + arrangementRadius * Math.Cos(centerAngle);
            // The apex cluster sits fully above cy while the two base clusters sit
            // only half as far below it, so a bare sin() offset leaves the trio
            // biased toward the top of the canvas. Nudge the whole arrangement
            // down by a quarter of arrangementRadius to balance the min/max Y.
            double centerY = cy + arrangementRadius * (Math.Sin(centerAngle) + 0.25);

            for (int i = 0; i < localCount; i++)
            {
                double angle = 2 * Math.PI * i / localCount;
                double r = smallRadius + jitter * NextGaussian(random);
                points.Add(new Point2D(centerX + r * Math.Cos(angle), centerY + r * Math.Sin(angle)));
            }
        }
        return points;
    }

    /// <summary>
    /// One loop shaped as a circle, one loop shaped as a square, and one open
    /// line - a visual reminder that H1 sees a loop's topology, not its shape:
    /// the circle and square both register as a hole, the line doesn't.
    /// </summary>
    private static List<Point2D> CircleSquareLine(int count, double noise, double radius, double cx, double cy, Random random)
    {
        var points = new List<Point2D>(count);
        double shapeRadius = radius * 0.35;
        double arrangementRadius = radius * 0.75;
        double jitter = noise * shapeRadius * 0.25;
        int per = count / 3;

        for (int c = 0; c < 3; c++)
        {
            int localCount = c < 2 ? per : count - 2 * per;
            double centerAngle = -Math.PI / 2 + c * 2 * Math.PI / 3;
            double centerX = cx + arrangementRadius * Math.Cos(centerAngle);
            // See the matching comment in ThreeCircles: balance the apex-up,
            // base-down triangle so it doesn't sit biased toward the top.
            double centerY = cy + arrangementRadius * (Math.Sin(centerAngle) + 0.25);

            switch (c)
            {
                case 0:
                    for (int i = 0; i < localCount; i++)
                    {
                        double angle = 2 * Math.PI * i / localCount;
                        double r = shapeRadius + jitter * NextGaussian(random);
                        points.Add(new Point2D(centerX + r * Math.Cos(angle), centerY + r * Math.Sin(angle)));
                    }
                    break;
                case 1:
                    double perimeter = shapeRadius * 8;
                    for (int i = 0; i < localCount; i++)
                    {
                        var (px, py) = SquarePerimeterPoint(perimeter * i / localCount, shapeRadius);
                        points.Add(new Point2D(
                            centerX + px + jitter * NextGaussian(random),
                            centerY + py + jitter * NextGaussian(random)));
                    }
                    break;
                default:
                    for (int i = 0; i < localCount; i++)
                    {
                        double t = localCount <= 1 ? 0.5 : (double)i / (localCount - 1);
                        double x = (t - 0.5) * 2 * shapeRadius;
                        points.Add(new Point2D(
                            centerX + x + jitter * NextGaussian(random),
                            centerY + jitter * NextGaussian(random)));
                    }
                    break;
            }
        }
        return points;
    }

    /// <summary>Walks a square's perimeter clockwise from its bottom-left corner.</summary>
    private static (double X, double Y) SquarePerimeterPoint(double distance, double halfSide)
    {
        double side = 2 * halfSide;
        double d = distance % (4 * side);
        if (d < side) return (-halfSide + d, -halfSide);
        d -= side;
        if (d < side) return (halfSide, -halfSide + d);
        d -= side;
        if (d < side) return (halfSide - d, halfSide);
        d -= side;
        return (-halfSide, halfSide - d);
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

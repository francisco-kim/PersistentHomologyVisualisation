using PersistentHomologyCore.Models;
using PersistentHomologyCore.Services;

namespace PersistentHomologyCore.Tests;

public class PresetGeneratorTests
{
    private const double Tolerance = 1e-9;

    public static IEnumerable<object[]> AllKinds() =>
        Enum.GetValues<PresetKind>().Select(k => new object[] { k });

    [Theory]
    [MemberData(nameof(AllKinds))]
    public void Generate_ProducesExactlyRequestedCount(PresetKind kind)
    {
        var points = PresetGenerator.Generate(kind, count: 50, noise: 0.2, seed: 1);

        Assert.Equal(50, points.Count);
    }

    [Theory]
    [MemberData(nameof(AllKinds))]
    public void Generate_ClampsCountToMaxPoints(PresetKind kind)
    {
        var points = PresetGenerator.Generate(kind, count: 10_000, noise: 0.2, seed: 1);

        Assert.Equal(PresetGenerator.MaxPoints, points.Count);
    }

    [Theory]
    [MemberData(nameof(AllKinds))]
    public void Generate_AllPointsWithinCanvasBounds(PresetKind kind)
    {
        const double width = 640, height = 640;
        var points = PresetGenerator.Generate(kind, count: 80, noise: 0.5, seed: 2, width: width, height: height);

        // Generous margin: shapes are inscribed with headroom, jitter should not
        // send points wildly outside the canvas.
        double margin = Math.Max(width, height) * 0.25;
        Assert.All(points, p =>
        {
            Assert.InRange(p.X, -margin, width + margin);
            Assert.InRange(p.Y, -margin, height + margin);
        });
    }

    [Theory]
    [MemberData(nameof(AllKinds))]
    public void Generate_SameSeedProducesIdenticalCloud(PresetKind kind)
    {
        var a = PresetGenerator.Generate(kind, count: 40, noise: 0.4, seed: 99);
        var b = PresetGenerator.Generate(kind, count: 40, noise: 0.4, seed: 99);

        Assert.Equal(a.Count, b.Count);
        for (int i = 0; i < a.Count; i++)
        {
            Assert.Equal(a[i].X, b[i].X, Tolerance);
            Assert.Equal(a[i].Y, b[i].Y, Tolerance);
        }
    }

    [Fact]
    public void NoisyCircle_ZeroNoise_LiesExactlyOnCircle()
    {
        const double width = 640, height = 640;
        var points = PresetGenerator.Generate(PresetKind.NoisyCircle, count: 30, noise: 0, seed: 1, width: width, height: height);

        double cx = width / 2, cy = height / 2;
        double expectedRadius = Math.Min(width, height) * 0.40;

        Assert.All(points, p =>
        {
            double r = new Point2D(cx, cy).DistanceTo(p);
            Assert.Equal(expectedRadius, r, 1e-6);
        });
    }

    [Fact]
    public void Spiral_ZeroNoise_RadiusGrowsMonotonically()
    {
        var points = PresetGenerator.Generate(PresetKind.Spiral, count: 40, noise: 0, seed: 1);
        double cx = 320, cy = 320;

        double previousRadius = -1;
        foreach (var p in points)
        {
            double r = new Point2D(cx, cy).DistanceTo(p);
            Assert.True(r >= previousRadius - 1e-9);
            previousRadius = r;
        }
    }
}

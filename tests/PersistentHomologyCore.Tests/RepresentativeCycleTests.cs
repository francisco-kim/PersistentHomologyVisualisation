using PersistentHomologyCore.Models;
using PersistentHomologyCore.Services;

namespace PersistentHomologyCore.Tests;

public class RepresentativeCycleTests
{
    [Fact]
    public void UnitSquare_H1RepresentativeIsExactlyTheFourSides()
    {
        List<Point2D> points = [new Point2D(0, 0), new Point2D(1, 0), new Point2D(1, 1), new Point2D(0, 1)];

        var result = PersistenceEngine.Compute(points, maxEpsilon: 100);
        var bar = Assert.Single(result.H1, p => !p.IsZeroPersistence);

        var edges = PairUp(bar.RepresentativeEdgePairs);
        Assert.Equal(4, edges.Count);

        var expectedSides = new HashSet<(int, int)> { (0, 1), (1, 2), (2, 3), (0, 3) };
        var actualSides = edges.Select(Normalize).ToHashSet();
        Assert.Equal(expectedSides, actualSides);
    }

    [Theory]
    [InlineData(PresetKind.NoisyCircle)]
    [InlineData(PresetKind.FigureEight)]
    [InlineData(PresetKind.Annulus)]
    public void H1Representatives_AreValidCyclesWithinFiltrationBounds(PresetKind kind)
    {
        var points = PresetGenerator.Generate(kind, count: 40, noise: 0.3, seed: 21);
        var result = PersistenceEngine.Compute(points, maxEpsilon: 260);
        var filtration = result.Filtration;

        foreach (var pair in result.H1)
        {
            var edges = PairUp(pair.RepresentativeEdgePairs);
            Assert.NotEmpty(edges);

            // Every vertex touched by the cycle has even degree (closed loop, zero boundary).
            var degree = new Dictionary<int, int>();
            foreach (var (a, b) in edges)
            {
                degree[a] = degree.GetValueOrDefault(a) + 1;
                degree[b] = degree.GetValueOrDefault(b) + 1;
            }
            Assert.All(degree.Values, d => Assert.True(d % 2 == 0));

            double bound = pair.IsEssential ? pair.Birth : pair.Death;
            foreach (var (a, b) in edges)
            {
                double edgeFiltration = points[a].DistanceTo(points[b]);
                Assert.True(edgeFiltration <= bound + 1e-9,
                    $"representative edge ({a},{b}) filtration {edgeFiltration} exceeds bound {bound}");
            }

            _ = filtration; // filtration referenced for context; edge lengths recomputed directly above
        }
    }

    [Fact]
    public void TwoTightClusters_H0DeathPairRepresentativeIsFullBlob()
    {
        const double separation = 150;
        var random = new Random(1);
        var points = new List<Point2D>();
        for (int i = 0; i < 15; i++) points.Add(new Point2D(random.NextDouble() * 10, random.NextDouble() * 10));
        for (int i = 0; i < 15; i++) points.Add(new Point2D(separation + random.NextDouble() * 10, random.NextDouble() * 10));

        var result = PersistenceEngine.Compute(points, maxEpsilon: 200);
        var bridge = Assert.Single(result.H0, p => !p.IsEssential && p.Death > 50);

        // The absorbed component at the moment of bridging must be entirely
        // on one side (indices < 15) or entirely the other (indices >= 15).
        bool allLeft = bridge.RepresentativeVertices.All(v => v < 15);
        bool allRight = bridge.RepresentativeVertices.All(v => v >= 15);
        Assert.True(allLeft || allRight);
        Assert.Equal(15, bridge.RepresentativeVertices.Length);
    }

    private static List<(int, int)> PairUp(int[] flattened)
    {
        var result = new List<(int, int)>(flattened.Length / 2);
        for (int i = 0; i < flattened.Length; i += 2) result.Add((flattened[i], flattened[i + 1]));
        return result;
    }

    private static (int, int) Normalize((int, int) edge) => edge.Item1 < edge.Item2 ? edge : (edge.Item2, edge.Item1);
}

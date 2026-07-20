using PersistentHomologyCore.Models;
using PersistentHomologyCore.Services;

namespace PersistentHomologyCore.Tests;

public class PersistenceReductionTests
{
    private const double Tolerance = 1e-9;

    [Fact]
    public void IsolatedPoints_AllEssentialH0_NoH1()
    {
        List<Point2D> points = [new Point2D(0, 0), new Point2D(1000, 0), new Point2D(0, 1000), new Point2D(1000, 1000)];

        var result = PersistenceEngine.Compute(points, maxEpsilon: 1); // far below any pairwise distance

        Assert.Equal(4, result.H0.Count);
        Assert.All(result.H0, p => Assert.True(p.IsEssential));
        Assert.Empty(result.H1);
    }

    [Fact]
    public void TwoPoints_OneEssentialOneFiniteH0()
    {
        double d = 7.5;
        List<Point2D> points = [new Point2D(0, 0), new Point2D(d, 0)];

        var result = PersistenceEngine.Compute(points, maxEpsilon: 100);

        Assert.Equal(2, result.H0.Count);
        Assert.Single(result.H0, p => p.IsEssential);
        var finite = Assert.Single(result.H0, p => !p.IsEssential);
        Assert.Equal(0, finite.Birth, Tolerance);
        Assert.Equal(d, finite.Death, Tolerance);
        Assert.Empty(result.H1);
    }

    [Fact]
    public void ScaleneTriangle_H0TwoFiniteDeaths_H1ZeroPersistencePair()
    {
        // Classic 3-4-5 right triangle: a=3, b=4, c=5.
        List<Point2D> points = [new Point2D(0, 0), new Point2D(4, 0), new Point2D(0, 3)];

        var result = PersistenceEngine.Compute(points, maxEpsilon: 100);

        Assert.Equal(3, result.H0.Count);
        Assert.Single(result.H0, p => p.IsEssential);
        var finiteDeaths = result.H0.Where(p => !p.IsEssential).Select(p => p.Death).OrderBy(x => x).ToArray();
        Assert.Equal([3.0, 4.0], finiteDeaths, comparer: DoubleComparer);

        var h1 = Assert.Single(result.H1);
        Assert.True(h1.IsZeroPersistence);
        Assert.Equal(5.0, h1.Birth, Tolerance);
        Assert.Equal(5.0, h1.Death, Tolerance);
    }

    [Fact]
    public void UnitSquare_H0ThreeFiniteDeathsAtOne_H1SingleBarOneToSqrt2()
    {
        List<Point2D> points = [new Point2D(0, 0), new Point2D(1, 0), new Point2D(1, 1), new Point2D(0, 1)];

        var result = PersistenceEngine.Compute(points, maxEpsilon: 100);

        Assert.Equal(4, result.H0.Count);
        Assert.Single(result.H0, p => p.IsEssential);
        Assert.Equal(3, result.H0.Count(p => !p.IsEssential));
        Assert.All(result.H0.Where(p => !p.IsEssential), p => Assert.Equal(1.0, p.Death, Tolerance));

        var meaningfulH1 = result.H1.Where(p => !p.IsZeroPersistence).ToList();
        var bar = Assert.Single(meaningfulH1);
        Assert.Equal(1.0, bar.Birth, Tolerance);
        Assert.Equal(Math.Sqrt(2), bar.Death, Tolerance);
    }

    [Fact]
    public void Regular20Gon_DominantH1BirthIsExactChordLength()
    {
        var points = PresetGenerator.Generate(PresetKind.NoisyCircle, count: 20, noise: 0, seed: 1);
        double radius = Math.Min(640, 640) * 0.35;
        double expectedBirth = 2 * radius * Math.Sin(Math.PI / 20);

        var result = PersistenceEngine.Compute(points, maxEpsilon: 500);

        var strong = result.H1.Where(p => p.Persistence > 0.5 * radius).ToList();
        var dominant = Assert.Single(strong);
        Assert.Equal(expectedBirth, dominant.Birth, 1e-6);

        double expectedDeath = Math.Sqrt(3) * radius;
        Assert.InRange(dominant.Death, expectedDeath * 0.85, expectedDeath * 1.15);
    }

    [Fact]
    public void TwoTightClusters_OneH0BarDiesNearSeparation()
    {
        const double separation = 150;
        var random = new Random(1);
        var points = new List<Point2D>();
        for (int i = 0; i < 15; i++) points.Add(new Point2D(random.NextDouble() * 10, random.NextDouble() * 10));
        for (int i = 0; i < 15; i++) points.Add(new Point2D(separation + random.NextDouble() * 10, random.NextDouble() * 10));

        var result = PersistenceEngine.Compute(points, maxEpsilon: 200);

        var bridging = result.H0.Where(p => !p.IsEssential && p.Death > 50).ToList();
        var bridge = Assert.Single(bridging);
        Assert.InRange(bridge.Death, separation * 0.9, separation * 1.1);

        var withinCluster = result.H0.Where(p => !p.IsEssential && p.Death <= 50);
        Assert.All(withinCluster, p => Assert.True(p.Death <= 15));
    }

    [Theory]
    [InlineData(PresetKind.NoisyCircle, 25)]
    [InlineData(PresetKind.Annulus, 25)]
    [InlineData(PresetKind.Spiral, 25)]
    public void EulerCharacteristic_ComponentsMinusHoles_MatchesVMinusEPlusT_BeforeAnyTriangles(PresetKind kind, int count)
    {
        // chi = beta0 - beta1 + beta2 in general. Our complex is capped at
        // dimension 2, so once enough triangles pack together to close off a
        // tetrahedral shell (four mutually-close points), beta2 can become
        // positive and the plain V-E+T = Components-Holes identity breaks -
        // that is a real topological fact about the capped complex, not a
        // reduction bug (verified by hand against BoundaryMatrixReducer's own
        // triangle-reduction output). Restricting to epsilon below the first
        // triangle's filtration value keeps beta2 = 0, where the identity is
        // guaranteed and still exercises Components/Holes end-to-end.
        var points = PresetGenerator.Generate(kind, count, noise: 0.2, seed: 42);
        var result = PersistenceEngine.Compute(points, maxEpsilon: 260);
        var filtration = result.Filtration;
        int v = filtration.Points.Count;

        double firstTriangle = filtration.TriangleFiltrations.Length > 0
            ? filtration.TriangleFiltrations[0]
            : filtration.MaxEpsilon;

        foreach (double fraction in new[] { 0.0, 0.25, 0.5, 0.75 })
        {
            double epsilon = firstTriangle * fraction;
            int e = filtration.CountEdgesUpTo(epsilon);
            int t = filtration.CountTrianglesUpTo(epsilon);
            Assert.Equal(0, t);

            int expected = v - e + t;
            int actual = result.Components(epsilon) - result.Holes(epsilon);

            Assert.Equal(expected, actual);
        }
    }

    [Theory]
    [InlineData(PresetKind.NoisyCircle)]
    [InlineData(PresetKind.RandomClusters)]
    [InlineData(PresetKind.FigureEight)]
    public void UnionFindH0_MatchesMatrixReductionH0(PresetKind kind)
    {
        var points = PresetGenerator.Generate(kind, count: 30, noise: 0.3, seed: 5);
        var filtration = RipsFiltrationBuilder.Build(points, maxEpsilon: 260);

        var matrixH0 = BoundaryMatrixReducer.Reduce(filtration).H0
            .Select(p => (p.Birth, p.Death)).OrderBy(x => x.Death).ToList();
        var unionFindH0 = UnionFind.ComputeH0Pairs(filtration)
            .Select(p => (p.Birth, p.Death)).OrderBy(x => x.Death).ToList();

        Assert.Equal(matrixH0, unionFindH0);
    }

    [Fact]
    public void Compute_SameInput_IsDeterministic()
    {
        var points = PresetGenerator.Generate(PresetKind.NoisyCircle, count: 30, noise: 0.4, seed: 8);

        var a = PersistenceEngine.Compute(points, maxEpsilon: 260);
        var b = PersistenceEngine.Compute(points, maxEpsilon: 260);

        Assert.Equal(a.H0.Select(p => (p.Birth, p.Death)), b.H0.Select(p => (p.Birth, p.Death)));
        Assert.Equal(a.H1.Select(p => (p.Birth, p.Death)), b.H1.Select(p => (p.Birth, p.Death)));
    }

    private static readonly IEqualityComparer<double> DoubleComparer = EqualityComparer<double>.Default;
}

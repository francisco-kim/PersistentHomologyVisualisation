using PersistentHomologyCore.Models;
using PersistentHomologyCore.Services;

namespace PersistentHomologyCore.Tests;

public class RipsFiltrationTests
{
    private const double Tolerance = 1e-9;

    [Fact]
    public void TwoPointsWithinEpsilon_ProducesOneEdge()
    {
        List<Point2D> points = [new Point2D(0, 0), new Point2D(3, 0)];

        var filtration = RipsFiltrationBuilder.Build(points, maxEpsilon: 5);

        Assert.Equal(3, filtration.Simplices.Count); // 2 vertices + 1 edge
        Assert.Single(filtration.EdgeFiltrations);
        Assert.Equal(3.0, filtration.EdgeFiltrations[0], Tolerance);
        Assert.Empty(filtration.TriangleFiltrations);
    }

    [Fact]
    public void TwoPointsBeyondEpsilon_ProducesNoEdge()
    {
        List<Point2D> points = [new Point2D(0, 0), new Point2D(10, 0)];

        var filtration = RipsFiltrationBuilder.Build(points, maxEpsilon: 5);

        Assert.Equal(2, filtration.Simplices.Count); // vertices only
        Assert.Empty(filtration.EdgeFiltrations);
    }

    [Fact]
    public void EquilateralTriangle_TriangleFiltrationEqualsSideLength()
    {
        double side = 2.0;
        List<Point2D> points =
        [
            new Point2D(0, 0),
            new Point2D(side, 0),
            new Point2D(side / 2, side * Math.Sqrt(3) / 2)
        ];

        var filtration = RipsFiltrationBuilder.Build(points, maxEpsilon: 10);

        Assert.Equal(3, filtration.EdgeFiltrations.Length);
        Assert.All(filtration.EdgeFiltrations, f => Assert.Equal(side, f, Tolerance));

        Assert.Single(filtration.TriangleFiltrations);
        Assert.Equal(side, filtration.TriangleFiltrations[0], Tolerance);
    }

    [Fact]
    public void UnitSquare_FourTrianglesAtDiagonalFiltration()
    {
        List<Point2D> points =
        [
            new Point2D(0, 0),
            new Point2D(1, 0),
            new Point2D(1, 1),
            new Point2D(0, 1)
        ];
        double diagonal = Math.Sqrt(2);

        var filtration = RipsFiltrationBuilder.Build(points, maxEpsilon: 10);

        Assert.Equal(6, filtration.EdgeFiltrations.Length); // C(4,2)
        Assert.Equal(4, filtration.TriangleFiltrations.Length); // C(4,3)
        Assert.All(filtration.TriangleFiltrations, f => Assert.Equal(diagonal, f, Tolerance));
    }

    [Fact]
    public void Simplices_AreSortedByFiltrationThenDimension()
    {
        var points = PresetGenerator.Generate(PresetKind.RandomClusters, count: 40, noise: 0.5, seed: 7);

        var filtration = RipsFiltrationBuilder.Build(points, maxEpsilon: 260);

        double lastFiltration = double.NegativeInfinity;
        int lastDimension = -1;
        foreach (var simplex in filtration.Simplices)
        {
            if (simplex.Filtration > lastFiltration)
            {
                lastFiltration = simplex.Filtration;
                lastDimension = simplex.Dimension;
            }
            else
            {
                Assert.Equal(lastFiltration, simplex.Filtration, Tolerance);
                Assert.True(simplex.Dimension >= lastDimension,
                    "faces must precede cofaces at equal filtration value");
                lastDimension = simplex.Dimension;
            }
        }
    }

    [Fact]
    public void CountEdgesAndTrianglesUpTo_MatchesLinearScan()
    {
        var points = PresetGenerator.Generate(PresetKind.NoisyCircle, count: 30, noise: 0.3, seed: 11);
        var filtration = RipsFiltrationBuilder.Build(points, maxEpsilon: 260);

        foreach (double epsilon in new[] { 0.0, 10.0, 50.0, 130.0, 260.0 })
        {
            int expectedEdges = filtration.EdgeFiltrations.Count(f => f <= epsilon);
            int expectedTriangles = filtration.TriangleFiltrations.Count(f => f <= epsilon);

            Assert.Equal(expectedEdges, filtration.CountEdgesUpTo(epsilon));
            Assert.Equal(expectedTriangles, filtration.CountTrianglesUpTo(epsilon));
        }
    }

    [Fact]
    public void TinyBudget_TruncatesTriangleDimensionOnly()
    {
        // A dense cluster so the full triangle count comfortably exceeds a tiny budget.
        var points = PresetGenerator.Generate(PresetKind.RandomClusters, count: 60, noise: 1.0, seed: 3, width: 100, height: 100);
        const int tinyBudget = 50;

        var filtration = RipsFiltrationBuilder.Build(points, maxEpsilon: 260, simplexBudget: tinyBudget);

        Assert.True(filtration.Truncated);
        Assert.True(filtration.TriangleCutoff < filtration.MaxEpsilon);
        Assert.All(filtration.TriangleFiltrations, f => Assert.True(f <= filtration.TriangleCutoff));
        // Vertices and edges are never truncated by the triangle budget.
        Assert.Equal(points.Count, filtration.Simplices.Count(s => s.Dimension == 0));
    }

    [Fact]
    public void LargeBudget_NeverTruncates()
    {
        var points = PresetGenerator.Generate(PresetKind.NoisyCircle, count: 50, noise: 0.2, seed: 5);

        var filtration = RipsFiltrationBuilder.Build(points, maxEpsilon: 260, simplexBudget: RipsFiltrationBuilder.DefaultSimplexBudget);

        Assert.False(filtration.Truncated);
        Assert.Equal(filtration.MaxEpsilon, filtration.TriangleCutoff, Tolerance);
    }
}

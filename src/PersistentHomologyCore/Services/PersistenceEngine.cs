using PersistentHomologyCore.Models;

namespace PersistentHomologyCore.Services;

/// <summary>Facade: point cloud in, full persistence result out.</summary>
public static class PersistenceEngine
{
    public static PersistenceResult Compute(
        IReadOnlyList<Point2D> points,
        double maxEpsilon,
        int simplexBudget = RipsFiltrationBuilder.DefaultSimplexBudget)
    {
        var filtration = RipsFiltrationBuilder.Build(points, maxEpsilon, simplexBudget);
        var matrixResult = BoundaryMatrixReducer.Reduce(filtration);
        var h0 = UnionFind.ComputeH0Pairs(filtration);

        h0.Sort((a, b) => b.Persistence.CompareTo(a.Persistence));

        return new PersistenceResult(filtration, h0, matrixResult.H1);
    }
}

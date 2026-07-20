using PersistentHomologyCore.Models;

namespace PersistentHomologyCore.Services;

/// <summary>
/// Immutable result of building a Vietoris-Rips filtration: every simplex
/// (vertices, edges, triangles) sorted by (Filtration, Dimension, vertices),
/// the tiebreak on Dimension guaranteeing every face precedes its cofaces at
/// equal filtration value, which the boundary-matrix reduction relies on.
/// </summary>
public sealed class RipsFiltration
{
    public IReadOnlyList<Point2D> Points { get; }
    public double MaxEpsilon { get; }
    public double TriangleCutoff { get; }
    public bool Truncated { get; }
    public IReadOnlyList<Simplex> Simplices { get; }

    public int[] EdgePairs { get; }
    public double[] EdgeFiltrations { get; }
    public int[] TriangleTriples { get; }
    public double[] TriangleFiltrations { get; }

    internal RipsFiltration(
        IReadOnlyList<Point2D> points,
        double maxEpsilon,
        double triangleCutoff,
        bool truncated,
        IReadOnlyList<Simplex> simplices,
        int[] edgePairs,
        double[] edgeFiltrations,
        int[] triangleTriples,
        double[] triangleFiltrations)
    {
        Points = points;
        MaxEpsilon = maxEpsilon;
        TriangleCutoff = triangleCutoff;
        Truncated = truncated;
        Simplices = simplices;
        EdgePairs = edgePairs;
        EdgeFiltrations = edgeFiltrations;
        TriangleTriples = triangleTriples;
        TriangleFiltrations = triangleFiltrations;
    }

    /// <summary>Number of edges with filtration &lt;= epsilon.</summary>
    public int CountEdgesUpTo(double epsilon) => UpperBound(EdgeFiltrations, epsilon);

    /// <summary>Number of triangles with filtration &lt;= epsilon.</summary>
    public int CountTrianglesUpTo(double epsilon) => UpperBound(TriangleFiltrations, epsilon);

    private static int UpperBound(double[] sortedFiltrations, double epsilon)
    {
        int lo = 0, hi = sortedFiltrations.Length;
        while (lo < hi)
        {
            int mid = lo + (hi - lo) / 2;
            if (sortedFiltrations[mid] <= epsilon) lo = mid + 1;
            else hi = mid;
        }
        return lo;
    }
}

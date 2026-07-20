using PersistentHomologyCore.Models;

namespace PersistentHomologyCore.Services;

/// <summary>
/// Builds a Vietoris-Rips filtration up to dimension 2 (vertices, edges,
/// triangles) from a point cloud. Edges connect points within maxEpsilon;
/// triangles fill in mutually-close triples. Triangle count is O(n^3) in the
/// worst case, so a histogram-based budget truncates the triangle dimension
/// (never vertices or edges) when a dense cloud would blow past it.
/// </summary>
public static class RipsFiltrationBuilder
{
    public const int DefaultSimplexBudget = 200_000;
    private const int HistogramBins = 1024;

    public static RipsFiltration Build(
        IReadOnlyList<Point2D> points,
        double maxEpsilon,
        int simplexBudget = DefaultSimplexBudget)
    {
        int n = points.Count;

        // --- Edges: all pairs within maxEpsilon, plus sorted adjacency lists. ---
        var edges = new List<(int I, int J, double F)>();
        var adjacency = new List<int>[n];
        for (int i = 0; i < n; i++) adjacency[i] = [];

        for (int i = 0; i < n; i++)
        {
            for (int j = i + 1; j < n; j++)
            {
                double d = points[i].DistanceTo(points[j]);
                if (d <= maxEpsilon)
                {
                    edges.Add((i, j, d));
                    adjacency[i].Add(j);
                    adjacency[j].Add(i);
                }
            }
        }
        foreach (var adj in adjacency) adj.Sort();

        var edgeFiltration = new Dictionary<long, double>(edges.Count);
        foreach (var e in edges) edgeFiltration[EdgeKey(e.I, e.J)] = e.F;

        int triangleBudget = Math.Max(0, simplexBudget - n - edges.Count);

        // --- Pass 1: histogram-count triangle candidates without storing them. ---
        double binWidth = maxEpsilon > 0 ? maxEpsilon / HistogramBins : 1;
        var histogram = new int[HistogramBins];
        long totalTriangles = 0;

        VisitTriangleCandidates(adjacency, edgeFiltration, (_, _, _, f) =>
        {
            totalTriangles++;
            int bin = Math.Clamp((int)(f / binWidth), 0, HistogramBins - 1);
            histogram[bin]++;
        });

        bool truncated = totalTriangles > triangleBudget;
        double triangleCutoff = maxEpsilon;

        if (truncated)
        {
            long cumulative = 0;
            int includedBins = 0;
            for (int b = 0; b < HistogramBins; b++)
            {
                if (cumulative + histogram[b] > triangleBudget) break;
                cumulative += histogram[b];
                includedBins++;
            }
            triangleCutoff = includedBins * binWidth;
        }

        // --- Pass 2: store triangles with filtration <= triangleCutoff. ---
        var triangles = new List<Simplex>();
        double cutoff = triangleCutoff;
        VisitTriangleCandidates(adjacency, edgeFiltration, (a, b, c, f) =>
        {
            if (f <= cutoff) triangles.Add(Simplex.Triangle(a, b, c, f));
        });
        triangles.Sort((x, y) => x.Filtration.CompareTo(y.Filtration));

        // --- Combine and globally sort: (Filtration, Dimension, lex vertices). ---
        var all = new List<Simplex>(n + edges.Count + triangles.Count);
        for (int i = 0; i < n; i++) all.Add(Simplex.Vertex(i));
        foreach (var e in edges) all.Add(Simplex.Edge(e.I, e.J, e.F));
        all.AddRange(triangles);

        all.Sort((x, y) =>
        {
            int cmp = x.Filtration.CompareTo(y.Filtration);
            if (cmp != 0) return cmp;
            cmp = x.Dimension.CompareTo(y.Dimension);
            if (cmp != 0) return cmp;
            cmp = x.V0.CompareTo(y.V0);
            if (cmp != 0) return cmp;
            cmp = x.V1.CompareTo(y.V1);
            if (cmp != 0) return cmp;
            return x.V2.CompareTo(y.V2);
        });

        // --- Project the dimension-1 / dimension-2 subsequences (each still
        //     ascending by filtration since the global sort is stable on ties). ---
        var edgePairs = new List<int>(edges.Count * 2);
        var edgeFiltrations = new List<double>(edges.Count);
        var triangleTriples = new List<int>(triangles.Count * 3);
        var triangleFiltrations = new List<double>(triangles.Count);

        foreach (var s in all)
        {
            if (s.Dimension == 1)
            {
                edgePairs.Add(s.V0);
                edgePairs.Add(s.V1);
                edgeFiltrations.Add(s.Filtration);
            }
            else if (s.Dimension == 2)
            {
                triangleTriples.Add(s.V0);
                triangleTriples.Add(s.V1);
                triangleTriples.Add(s.V2);
                triangleFiltrations.Add(s.Filtration);
            }
        }

        return new RipsFiltration(
            points,
            maxEpsilon,
            triangleCutoff,
            truncated,
            all,
            [.. edgePairs],
            [.. edgeFiltrations],
            [.. triangleTriples],
            [.. triangleFiltrations]);
    }

    /// <summary>
    /// Visits every triangle candidate (i &lt; j &lt; k, all pairwise edges
    /// present) exactly once, via adjacency-list intersection on each edge.
    /// </summary>
    private static void VisitTriangleCandidates(
        List<int>[] adjacency,
        Dictionary<long, double> edgeFiltration,
        Action<int, int, int, double> visit)
    {
        int n = adjacency.Length;
        for (int i = 0; i < n; i++)
        {
            var ai = adjacency[i];
            foreach (int j in ai)
            {
                if (j <= i) continue;
                var aj = adjacency[j];

                int pi = 0, pj = 0;
                while (pi < ai.Count && pj < aj.Count)
                {
                    int vi = ai[pi];
                    int vj = aj[pj];
                    if (vi == vj)
                    {
                        if (vi > j)
                        {
                            double dij = edgeFiltration[EdgeKey(i, j)];
                            double dik = edgeFiltration[EdgeKey(i, vi)];
                            double djk = edgeFiltration[EdgeKey(j, vi)];
                            double f = Math.Max(dij, Math.Max(dik, djk));
                            visit(i, j, vi, f);
                        }
                        pi++;
                        pj++;
                    }
                    else if (vi < vj) pi++;
                    else pj++;
                }
            }
        }
    }

    private static long EdgeKey(int i, int j) => ((long)i << 32) | (uint)j;
}

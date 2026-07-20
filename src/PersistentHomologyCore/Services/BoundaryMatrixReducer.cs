using PersistentHomologyCore.Models;

namespace PersistentHomologyCore.Services;

/// <summary>
/// Standard persistence boundary-matrix reduction over Z/2, with the
/// clearing (twist) optimisation: triangles (dimension 2) are reduced first,
/// and any edge column proven to be a triangle's pivot partner is skipped
/// during the edge (dimension 1) pass, since its own reduction is known to
/// vanish. Dimension-1 and dimension-2 reduction operate in disjoint row
/// spaces (edge rows vs. vertex rows), so processing them in two separate
/// passes - rather than interleaved in strict filtration order - is exact,
/// not an approximation.
///
/// H0 birth/death values fall out of the same edge pass (see
/// PersistenceReductionTests' union-find cross-check) but the richer H0
/// representatives (full component membership) come from <see cref="UnionFind"/>
/// instead, via <see cref="PersistenceEngine"/> - cheaper and simpler than
/// extracting component membership from the matrix.
/// </summary>
public static class BoundaryMatrixReducer
{
    public static PersistenceResult Reduce(RipsFiltration filtration)
    {
        var simplices = filtration.Simplices;
        int m = simplices.Count;
        int n = filtration.Points.Count;

        var vertexColumn = new int[n];
        var edgeColumnLookup = new Dictionary<long, int>();
        for (int col = 0; col < m; col++)
        {
            var s = simplices[col];
            if (s.Dimension == 0) vertexColumn[s.V0] = col;
            else if (s.Dimension == 1) edgeColumnLookup[EdgeKey(s.V0, s.V1)] = col;
        }

        var boundary = new List<int>?[m];
        for (int col = 0; col < m; col++)
        {
            var s = simplices[col];
            if (s.Dimension == 1)
            {
                boundary[col] = Sorted2(vertexColumn[s.V0], vertexColumn[s.V1]);
            }
            else if (s.Dimension == 2)
            {
                int e01 = edgeColumnLookup[EdgeKey(s.V0, s.V1)];
                int e02 = edgeColumnLookup[EdgeKey(s.V0, s.V2)];
                int e12 = edgeColumnLookup[EdgeKey(s.V1, s.V2)];
                var list = new List<int> { e01, e02, e12 };
                list.Sort();
                boundary[col] = list;
            }
        }

        var pivotOwner = new int[m];
        Array.Fill(pivotOwner, -1);
        var cleared = new bool[m];
        var reduced = new List<int>?[m];
        var h1Pairs = new List<PersistencePair>();

        // --- Phase A: triangles (dimension 2), filtration order. ---
        for (int col = 0; col < m; col++)
        {
            if (simplices[col].Dimension != 2) continue;

            var chain = ReduceColumn(new List<int>(boundary[col]!), pivotOwner, reduced);
            reduced[col] = chain;

            if (chain.Count == 0) continue;

            int low = chain[^1];
            pivotOwner[low] = col;
            cleared[low] = true;

            var birthEdge = simplices[low];
            h1Pairs.Add(new PersistencePair(
                Dimension: 1,
                Birth: birthEdge.Filtration,
                Death: simplices[col].Filtration,
                BirthSimplexOrder: low,
                DeathSimplexOrder: col,
                RepresentativeVertices: [],
                RepresentativeEdgePairs: FlattenEdges(chain, simplices)));
        }

        // --- Phase B: edges (dimension 1), filtration order, skipping cleared ones. ---
        var vColumn = new List<int>?[m];
        var h0Pairs = new List<PersistencePair>();

        for (int col = 0; col < m; col++)
        {
            if (simplices[col].Dimension != 1 || cleared[col]) continue;

            var chain = new List<int>(boundary[col]!);
            var vChain = new List<int> { col };
            while (chain.Count > 0)
            {
                int low = chain[^1];
                int owner = pivotOwner[low];
                if (owner == -1) break;
                chain = XorSorted(chain, reduced[owner]!);
                vChain = XorSorted(vChain, vColumn[owner]!);
            }
            reduced[col] = chain;
            vColumn[col] = vChain;

            if (chain.Count > 0)
            {
                int low = chain[^1];
                pivotOwner[low] = col;

                var birthVertex = simplices[low];
                h0Pairs.Add(new PersistencePair(
                    Dimension: 0,
                    Birth: birthVertex.Filtration,
                    Death: simplices[col].Filtration,
                    BirthSimplexOrder: low,
                    DeathSimplexOrder: col,
                    RepresentativeVertices: [],
                    RepresentativeEdgePairs: [simplices[col].V0, simplices[col].V1]));
            }
            else
            {
                h1Pairs.Add(new PersistencePair(
                    Dimension: 1,
                    Birth: simplices[col].Filtration,
                    Death: double.PositiveInfinity,
                    BirthSimplexOrder: col,
                    DeathSimplexOrder: -1,
                    RepresentativeVertices: [],
                    RepresentativeEdgePairs: FlattenEdges(vChain, simplices)));
            }
        }

        // --- Essential H0: vertex columns never claimed as a pivot. ---
        for (int col = 0; col < m; col++)
        {
            if (simplices[col].Dimension != 0 || pivotOwner[col] != -1) continue;

            h0Pairs.Add(new PersistencePair(
                Dimension: 0,
                Birth: simplices[col].Filtration,
                Death: double.PositiveInfinity,
                BirthSimplexOrder: col,
                DeathSimplexOrder: -1,
                RepresentativeVertices: [],
                RepresentativeEdgePairs: []));
        }

        h0Pairs.Sort((a, b) => b.Persistence.CompareTo(a.Persistence));
        h1Pairs.Sort((a, b) => b.Persistence.CompareTo(a.Persistence));

        return new PersistenceResult(filtration, h0Pairs, h1Pairs);
    }

    private static List<int> ReduceColumn(List<int> chain, int[] pivotOwner, List<int>?[] reduced)
    {
        while (chain.Count > 0)
        {
            int low = chain[^1];
            int owner = pivotOwner[low];
            if (owner == -1) break;
            chain = XorSorted(chain, reduced[owner]!);
        }
        return chain;
    }

    /// <summary>Symmetric difference of two ascending-sorted index lists (GF(2) column addition).</summary>
    private static List<int> XorSorted(List<int> a, List<int> b)
    {
        var result = new List<int>(a.Count + b.Count);
        int i = 0, j = 0;
        while (i < a.Count && j < b.Count)
        {
            if (a[i] == b[j]) { i++; j++; }
            else if (a[i] < b[j]) result.Add(a[i++]);
            else result.Add(b[j++]);
        }
        while (i < a.Count) result.Add(a[i++]);
        while (j < b.Count) result.Add(b[j++]);
        return result;
    }

    private static int[] FlattenEdges(List<int> edgeColumns, IReadOnlyList<Simplex> simplices)
    {
        var result = new int[edgeColumns.Count * 2];
        for (int i = 0; i < edgeColumns.Count; i++)
        {
            var e = simplices[edgeColumns[i]];
            result[2 * i] = e.V0;
            result[2 * i + 1] = e.V1;
        }
        return result;
    }

    private static List<int> Sorted2(int a, int b) => a < b ? [a, b] : [b, a];

    private static long EdgeKey(int i, int j) => ((long)i << 32) | (uint)j;
}

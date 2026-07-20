using PersistentHomologyCore.Models;

namespace PersistentHomologyCore.Services;

/// <summary>
/// Union-find over point indices, with each root tracking its full member
/// list so merges can report which vertices were just absorbed. Used to
/// compute H0 persistence pairs with rich representatives (cheaper and
/// simpler than extracting component membership from the boundary matrix).
/// </summary>
public sealed class UnionFind
{
    private readonly int[] _parent;
    private readonly List<int>[] _members;

    public UnionFind(int n)
    {
        _parent = new int[n];
        _members = new List<int>[n];
        for (int i = 0; i < n; i++)
        {
            _parent[i] = i;
            _members[i] = [i];
        }
    }

    public int Find(int i)
    {
        while (_parent[i] != i)
        {
            _parent[i] = _parent[_parent[i]];
            i = _parent[i];
        }
        return i;
    }

    /// <summary>
    /// Merges the components containing a and b. Returns the member list of
    /// the smaller (absorbed) component, or null if they were already joined.
    /// </summary>
    public List<int>? Union(int a, int b)
    {
        int rootA = Find(a);
        int rootB = Find(b);
        if (rootA == rootB) return null;

        if (_members[rootA].Count < _members[rootB].Count) (rootA, rootB) = (rootB, rootA);

        var absorbed = _members[rootB];
        _parent[rootB] = rootA;
        _members[rootA].AddRange(absorbed);
        return absorbed;
    }

    public IReadOnlyList<int> ComponentMembers(int root) => _members[root];

    /// <summary>
    /// Replays the filtration's edges in order, producing H0 persistence
    /// pairs (birth is always 0, since every point enters the filtration at
    /// filtration value 0) with full vertex representatives.
    /// </summary>
    public static List<PersistencePair> ComputeH0Pairs(RipsFiltration filtration)
    {
        var simplices = filtration.Simplices;
        int n = filtration.Points.Count;

        var vertexColumn = new int[n];
        for (int col = 0; col < simplices.Count; col++)
        {
            if (simplices[col].Dimension == 0) vertexColumn[simplices[col].V0] = col;
        }

        var unionFind = new UnionFind(n);
        var pairs = new List<PersistencePair>();

        for (int col = 0; col < simplices.Count; col++)
        {
            var s = simplices[col];
            if (s.Dimension != 1) continue;

            if (unionFind.Find(s.V0) == unionFind.Find(s.V1)) continue;

            var absorbed = unionFind.Union(s.V0, s.V1)!;
            pairs.Add(new PersistencePair(
                Dimension: 0,
                Birth: 0,
                Death: s.Filtration,
                BirthSimplexOrder: vertexColumn[absorbed[0]],
                DeathSimplexOrder: col,
                RepresentativeVertices: [.. absorbed],
                RepresentativeEdgePairs: [s.V0, s.V1]));
        }

        var seenRoots = new HashSet<int>();
        for (int v = 0; v < n; v++)
        {
            int root = unionFind.Find(v);
            if (!seenRoots.Add(root)) continue;

            var members = unionFind.ComponentMembers(root);
            pairs.Add(new PersistencePair(
                Dimension: 0,
                Birth: 0,
                Death: double.PositiveInfinity,
                BirthSimplexOrder: vertexColumn[members[0]],
                DeathSimplexOrder: -1,
                RepresentativeVertices: [.. members],
                RepresentativeEdgePairs: []));
        }

        return pairs;
    }
}

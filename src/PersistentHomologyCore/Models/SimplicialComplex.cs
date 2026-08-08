namespace PersistentHomologyCore.Models;

/// <summary>
/// An explicit, unfiltered simplicial complex: every simplex present, grouped
/// by dimension and sorted lexicographically within each dimension so that
/// boundary-matrix row and column ordering is stable and reproducible.
/// <para>
/// Kept separate from <see cref="Services.RipsFiltration"/>, which is a
/// filtration (simplices ordered by birth radius, dimensions interleaved).
/// Here nothing is born or dies: the complex just is what it is, which is what
/// makes a plain boundary matrix meaningful.
/// </para>
/// </summary>
public sealed class SimplicialComplex
{
    private readonly OrientedSimplex[][] _byDimension;
    private readonly Dictionary<OrientedSimplex, int>[] _indexByDimension;

    private SimplicialComplex(OrientedSimplex[][] byDimension)
    {
        _byDimension = byDimension;
        _indexByDimension = new Dictionary<OrientedSimplex, int>[byDimension.Length];
        for (int k = 0; k < byDimension.Length; k++)
        {
            var lookup = new Dictionary<OrientedSimplex, int>(byDimension[k].Length);
            for (int i = 0; i < byDimension[k].Length; i++) lookup[byDimension[k][i]] = i;
            _indexByDimension[k] = lookup;
        }
    }

    /// <summary>
    /// Highest dimension actually populated, or -1 for the empty complex. Note
    /// this is the top <em>non-empty</em> dimension: a complex whose triangles
    /// were all dropped reports 1 even though the array still has a slot for 2.
    /// </summary>
    public int MaxDimension
    {
        get
        {
            for (int k = _byDimension.Length - 1; k >= 0; k--)
            {
                if (_byDimension[k].Length > 0) return k;
            }
            return -1;
        }
    }

    /// <summary>Simplex count in dimension k; 0 for any k outside the complex.</summary>
    public int Count(int k) => k < 0 || k >= _byDimension.Length ? 0 : _byDimension[k].Length;

    /// <summary>The k-simplices in lexicographic order - the row/column order of the boundary matrices.</summary>
    public IReadOnlyList<OrientedSimplex> ByDimension(int k) =>
        k < 0 || k >= _byDimension.Length ? [] : _byDimension[k];

    /// <summary>Position of a simplex within its own dimension, or -1 if absent.</summary>
    public int IndexOf(OrientedSimplex simplex)
    {
        int k = simplex.Dimension;
        if (k < 0 || k >= _indexByDimension.Length) return -1;
        return _indexByDimension[k].TryGetValue(simplex, out int index) ? index : -1;
    }

    public bool Contains(OrientedSimplex simplex) => IndexOf(simplex) >= 0;

    /// <summary>Alternating sum of simplex counts - equals the alternating sum of Betti numbers.</summary>
    public int EulerCharacteristic
    {
        get
        {
            int sum = 0;
            for (int k = 0; k < _byDimension.Length; k++)
            {
                sum += (k % 2 == 0 ? 1 : -1) * _byDimension[k].Length;
            }
            return sum;
        }
    }

    /// <summary>
    /// Builds the closure of the given simplices: every face of every listed
    /// simplex is added, so callers can pass only the maximal ones (one
    /// tetrahedron rather than its 14 faces).
    /// </summary>
    public static SimplicialComplex FromMaximalSimplices(IEnumerable<OrientedSimplex> maximal)
    {
        var buckets = new HashSet<OrientedSimplex>[OrientedSimplex.MaxDimension + 1];
        for (int k = 0; k <= OrientedSimplex.MaxDimension; k++) buckets[k] = [];

        Span<int> vertices = stackalloc int[OrientedSimplex.MaxDimension + 1];
        Span<int> subset = stackalloc int[OrientedSimplex.MaxDimension + 1];

        foreach (var simplex in maximal)
        {
            int n = simplex.VertexCount;
            simplex.WriteVertices(vertices);

            // Every non-empty subset of a simplex's vertices is a face of it.
            for (int mask = 1; mask < (1 << n); mask++)
            {
                int size = 0;
                for (int bit = 0; bit < n; bit++)
                {
                    if ((mask & (1 << bit)) != 0) subset[size++] = vertices[bit];
                }
                var face = OrientedSimplex.FromVertices(subset[..size]);
                buckets[face.Dimension].Add(face);
            }
        }

        var byDimension = new OrientedSimplex[OrientedSimplex.MaxDimension + 1][];
        for (int k = 0; k <= OrientedSimplex.MaxDimension; k++)
        {
            var sorted = buckets[k].ToArray();
            Array.Sort(sorted);
            byDimension[k] = sorted;
        }
        return new SimplicialComplex(byDimension);
    }

    /// <summary>
    /// Drops every simplex above <paramref name="maxDimension"/>. The result is
    /// still a valid complex because faces are closed downwards - which is the
    /// whole implementation of the frame / surface / solid fill-level control.
    /// </summary>
    public SimplicialComplex Restrict(int maxDimension)
    {
        var byDimension = new OrientedSimplex[_byDimension.Length][];
        for (int k = 0; k < _byDimension.Length; k++)
        {
            byDimension[k] = k <= maxDimension ? _byDimension[k] : [];
        }
        return new SimplicialComplex(byDimension);
    }

    /// <summary>
    /// Every face of every simplex is itself present. The hand-authored presets
    /// must satisfy this; <see cref="FromMaximalSimplices"/> guarantees it by
    /// construction, so this only earns its keep against complexes assembled
    /// another way.
    /// </summary>
    public bool IsClosed()
    {
        for (int k = 1; k < _byDimension.Length; k++)
        {
            foreach (var simplex in _byDimension[k])
            {
                for (int i = 0; i < simplex.VertexCount; i++)
                {
                    if (!Contains(simplex.FaceOmitting(i))) return false;
                }
            }
        }
        return true;
    }
}

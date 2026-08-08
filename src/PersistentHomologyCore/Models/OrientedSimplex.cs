namespace PersistentHomologyCore.Models;

/// <summary>
/// An oriented simplex of dimension 0-3, stored as ascending vertex indices
/// with -1 marking unused slots: V1 == -1 marks a vertex, V2 == -1 an edge,
/// V3 == -1 a triangle, all four set a tetrahedron.
/// <para>
/// Distinct from <see cref="Simplex"/> on purpose. That type caps at dimension
/// 2 and carries a filtration value, which is what the Z/2 persistence path
/// needs; this one goes one dimension higher, drops the filtration, and is
/// meant for explicit unfiltered complexes where the <em>signed</em> boundary
/// operator is the object of interest.
/// </para>
/// </summary>
public readonly record struct OrientedSimplex(int V0, int V1, int V2, int V3)
    : IComparable<OrientedSimplex>
{
    public const int MaxDimension = 3;

    public int Dimension => V1 < 0 ? 0 : V2 < 0 ? 1 : V3 < 0 ? 2 : 3;

    public int VertexCount => Dimension + 1;

    public static OrientedSimplex Vertex(int a) => new(a, -1, -1, -1);

    public static OrientedSimplex Edge(int a, int b)
    {
        Span<int> v = [a, b];
        v.Sort();
        return new OrientedSimplex(v[0], v[1], -1, -1);
    }

    public static OrientedSimplex Triangle(int a, int b, int c)
    {
        Span<int> v = [a, b, c];
        v.Sort();
        return new OrientedSimplex(v[0], v[1], v[2], -1);
    }

    public static OrientedSimplex Tetrahedron(int a, int b, int c, int d)
    {
        Span<int> v = [a, b, c, d];
        v.Sort();
        return new OrientedSimplex(v[0], v[1], v[2], v[3]);
    }

    /// <summary>Builds from 1-4 vertex indices in any order; they are sorted ascending.</summary>
    public static OrientedSimplex FromVertices(ReadOnlySpan<int> vertices) => vertices.Length switch
    {
        1 => Vertex(vertices[0]),
        2 => Edge(vertices[0], vertices[1]),
        3 => Triangle(vertices[0], vertices[1], vertices[2]),
        4 => Tetrahedron(vertices[0], vertices[1], vertices[2], vertices[3]),
        _ => throw new ArgumentException($"Supported dimensions are 0-{MaxDimension}.", nameof(vertices))
    };

    public int this[int position] => position switch
    {
        0 => V0,
        1 => V1,
        2 => V2,
        3 => V3,
        _ => throw new ArgumentOutOfRangeException(nameof(position))
    };

    public void WriteVertices(Span<int> destination)
    {
        for (int i = 0; i < VertexCount; i++) destination[i] = this[i];
    }

    /// <summary>
    /// The codimension-1 face obtained by dropping the vertex at
    /// <paramref name="position"/> - the i-th term of the boundary sum, whose
    /// sign is (-1)^i.
    /// </summary>
    public OrientedSimplex FaceOmitting(int position)
    {
        if (position < 0 || position >= VertexCount) throw new ArgumentOutOfRangeException(nameof(position));
        if (Dimension == 0) throw new InvalidOperationException("A vertex has no codimension-1 faces.");

        Span<int> remaining = stackalloc int[VertexCount - 1];
        int next = 0;
        for (int i = 0; i < VertexCount; i++)
        {
            if (i != position) remaining[next++] = this[i];
        }
        return FromVertices(remaining);
    }

    /// <summary>Renders as "[0 1 3]" for matrix row and column headers.</summary>
    public string Label
    {
        get
        {
            Span<char> buffer = stackalloc char[2 + (VertexCount * 3)];
            int length = 0;
            buffer[length++] = '[';
            for (int i = 0; i < VertexCount; i++)
            {
                if (i > 0) buffer[length++] = ' ';
                bool written = this[i].TryFormat(buffer[length..], out int charsWritten);
                if (!written) return ToString();
                length += charsWritten;
            }
            buffer[length++] = ']';
            return new string(buffer[..length]);
        }
    }

    /// <summary>Dimension first, then lexicographic on the ascending vertex indices.</summary>
    public int CompareTo(OrientedSimplex other)
    {
        int byDimension = Dimension.CompareTo(other.Dimension);
        if (byDimension != 0) return byDimension;

        for (int i = 0; i < VertexCount; i++)
        {
            int byVertex = this[i].CompareTo(other[i]);
            if (byVertex != 0) return byVertex;
        }
        return 0;
    }

    public override string ToString() => Label;
}

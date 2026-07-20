namespace PersistentHomologyCore.Models;

/// <summary>
/// A vertex, edge, or triangle in the filtration. Vertex indices are always
/// stored ascending (V0 &lt; V1 &lt; V2). V1 == -1 marks a vertex; V2 == -1
/// marks an edge; both set marks a triangle.
/// </summary>
public readonly record struct Simplex(int V0, int V1, int V2, double Filtration)
{
    public int Dimension => V1 < 0 ? 0 : (V2 < 0 ? 1 : 2);

    public static Simplex Vertex(int v, double filtration = 0) => new(v, -1, -1, filtration);

    public static Simplex Edge(int a, int b, double filtration)
    {
        (a, b) = a < b ? (a, b) : (b, a);
        return new Simplex(a, b, -1, filtration);
    }

    public static Simplex Triangle(int a, int b, int c, double filtration)
    {
        Span<int> v = [a, b, c];
        v.Sort();
        return new Simplex(v[0], v[1], v[2], filtration);
    }
}

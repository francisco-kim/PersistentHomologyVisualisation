namespace PersistentHomologyCore.Models;

/// <summary>The hand-authored complexes shown on the boundary-operator page.</summary>
public enum BoundaryPresetKind
{
    /// <summary>A single tetrahedron on vertices 0-3.</summary>
    Tetrahedron,

    /// <summary>
    /// The same tetrahedron plus a fifth vertex outside it, joined to vertices
    /// 2 and 3 by edges. Together with the existing edge [2 3] that closes a
    /// triangular loop, but the triangle [2 3 4] itself is deliberately
    /// <em>not</em> filled - so the loop bounds nothing and survives as an
    /// honest one-dimensional hole at every fill level.
    /// <para>
    /// Earns its place three times over: it is the only preset with a
    /// non-trivial beta_1, it breaks the tetrahedron's symmetry so matrix rows
    /// become visually distinguishable, and its edges [2 4] and [3 4] are the
    /// face of no triangle - the clearest illustration of an empty coboundary.
    /// </para>
    /// </summary>
    TetrahedronWithOpenTriangle
}

/// <summary>
/// How far up the complex is filled in. The values are the maximum simplex
/// dimension kept, so applying one is exactly
/// <see cref="SimplicialComplex.Restrict"/> at that dimension.
/// </summary>
public enum FillLevel
{
    /// <summary>Vertices and edges only - the 1-skeleton.</summary>
    Frame = 1,

    /// <summary>Triangles filled in, interiors hollow.</summary>
    Surface = 2,

    /// <summary>Tetrahedra filled in too.</summary>
    Solid = 3
}

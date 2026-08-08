using PersistentHomologyCore.Models;

namespace PersistentHomologyCore.Services;

/// <summary>
/// The complex a boundary-explorer preset denotes, together with the 3D
/// coordinates used to draw it. The coordinates carry no topology - nothing
/// here is distance-based - they exist only so the picture is legible.
/// </summary>
/// <param name="Complex">The full complex, before any fill level is applied.</param>
/// <param name="Coordinates">One position per vertex index.</param>
public readonly record struct BoundaryPreset(SimplicialComplex Complex, IReadOnlyList<Point3D> Coordinates);

public static class BoundaryPresets
{
    // A regular tetrahedron on alternating cube corners, centred on the origin.
    // Edge length 2*sqrt(2); the renderer scales to fit, so only the shape matters.
    private static readonly Point3D[] TetrahedronCorners =
    [
        new Point3D(1, 1, 1),
        new Point3D(1, -1, -1),
        new Point3D(-1, 1, -1),
        new Point3D(-1, -1, 1)
    ];

    // Vertex 4 sits out beyond the midpoint of edge [2 3], on the ray from the
    // centroid through it, so the open triangle reads as a flap hanging off the
    // body rather than as clutter against it.
    private static readonly Point3D OpenTriangleApex = new(-3.0, 0, 0);

    public static BoundaryPreset Build(BoundaryPresetKind kind) => kind switch
    {
        BoundaryPresetKind.Tetrahedron => new BoundaryPreset(
            SimplicialComplex.FromMaximalSimplices([OrientedSimplex.Tetrahedron(0, 1, 2, 3)]),
            TetrahedronCorners),

        // The maximal simplices are the solid tetrahedron and two bare edges.
        // Listing edges rather than the triangle [2 3 4] is what leaves the
        // loop unfilled - and so is the entire reason this preset has a hole.
        BoundaryPresetKind.TetrahedronWithOpenTriangle => new BoundaryPreset(
            SimplicialComplex.FromMaximalSimplices(
            [
                OrientedSimplex.Tetrahedron(0, 1, 2, 3),
                OrientedSimplex.Edge(2, 4),
                OrientedSimplex.Edge(3, 4)
            ]),
            [.. TetrahedronCorners, OpenTriangleApex]),

        _ => throw new ArgumentOutOfRangeException(nameof(kind))
    };

    /// <summary>The preset restricted to a fill level - the complex actually displayed.</summary>
    public static SimplicialComplex Build(BoundaryPresetKind kind, FillLevel fillLevel) =>
        Build(kind).Complex.Restrict((int)fillLevel);
}

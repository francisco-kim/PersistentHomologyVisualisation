using PersistentHomologyCore.Models;
using PersistentHomologyCore.Services;

namespace PersistentHomologyCore.Tests;

public class BoundaryMatrixBuilderTests
{
    public static TheoryData<BoundaryPresetKind, FillLevel> AllPresets()
    {
        var data = new TheoryData<BoundaryPresetKind, FillLevel>();
        foreach (var kind in Enum.GetValues<BoundaryPresetKind>())
        {
            foreach (var fill in Enum.GetValues<FillLevel>()) data.Add(kind, fill);
        }
        return data;
    }

    // --- the explicit matrices the explainer prints, entry for entry ---
    //
    // These guard the sign convention against silent drift: the page's prose
    // shows these exact tables, so if the builder's convention changes the
    // documentation becomes wrong and these fail.

    [Fact]
    public void SolidTetrahedron_Boundary1_MatchesDocumentedMatrix()
    {
        var complex = BoundaryPresets.Build(BoundaryPresetKind.Tetrahedron, FillLevel.Solid);

        // columns: [01] [02] [03] [12] [13] [23]      rows: [0] [1] [2] [3]
        int[,] expected =
        {
            { -1, -1, -1,  0,  0,  0 },
            {  1,  0,  0, -1, -1,  0 },
            {  0,  1,  0,  1,  0, -1 },
            {  0,  0,  1,  0,  1,  1 }
        };

        Assert.Equal(expected, BoundaryMatrixBuilder.Build(complex, 1));
    }

    [Fact]
    public void SolidTetrahedron_Boundary2_MatchesDocumentedMatrix()
    {
        var complex = BoundaryPresets.Build(BoundaryPresetKind.Tetrahedron, FillLevel.Solid);

        // columns: [012] [013] [023] [123]
        // rows:    [01] [02] [03] [12] [13] [23]
        int[,] expected =
        {
            {  1,  1,  0,  0 },
            { -1,  0,  1,  0 },
            {  0, -1, -1,  0 },
            {  1,  0,  0,  1 },
            {  0,  1,  0, -1 },
            {  0,  0,  1,  1 }
        };

        Assert.Equal(expected, BoundaryMatrixBuilder.Build(complex, 2));
    }

    [Fact]
    public void SolidTetrahedron_Boundary3_MatchesDocumentedMatrix()
    {
        var complex = BoundaryPresets.Build(BoundaryPresetKind.Tetrahedron, FillLevel.Solid);

        // the single column [0123] = [123] - [023] + [013] - [012]
        int[,] expected = { { -1 }, { 1 }, { -1 }, { 1 } };

        Assert.Equal(expected, BoundaryMatrixBuilder.Build(complex, 3));
    }

    [Fact]
    public void Coboundary_IsTheTransposeOfTheBoundaryOneDimensionUp()
    {
        var complex = BoundaryPresets.Build(BoundaryPresetKind.Tetrahedron, FillLevel.Solid);

        // delta^0 = d_1 transposed: column [0] holds the edges vertex 0 is a
        // face of, all with sign -1 because 0 is the *first* vertex of each.
        int[,] expected =
        {
            { -1,  1,  0,  0 },
            { -1,  0,  1,  0 },
            { -1,  0,  0,  1 },
            {  0, -1,  1,  0 },
            {  0, -1,  0,  1 },
            {  0,  0, -1,  1 }
        };

        var coboundary = BoundaryMatrixBuilder.Transpose(BoundaryMatrixBuilder.Build(complex, 1));

        Assert.Equal(expected, coboundary);
    }

    [Fact]
    public void CoboundaryOfAVertexFunction_IsTheDiscreteDerivative()
    {
        var complex = BoundaryPresets.Build(BoundaryPresetKind.Tetrahedron, FillLevel.Solid);
        var coboundary = BoundaryMatrixBuilder.Transpose(BoundaryMatrixBuilder.Build(complex, 1));

        // An arbitrary function on the four vertices...
        int[] f = [5, 2, 9, -1];

        // ...pushed through delta^0 must give f(b) - f(a) on each edge [ab].
        var edges = complex.ByDimension(1);
        for (int e = 0; e < edges.Count; e++)
        {
            int value = 0;
            for (int v = 0; v < f.Length; v++) value += coboundary[e, v] * f[v];

            Assert.Equal(f[edges[e].V1] - f[edges[e].V0], value);
        }
    }

    // --- structural properties, across every preset and fill level ---

    [Theory]
    [MemberData(nameof(AllPresets))]
    public void BoundaryOfBoundary_IsZero(BoundaryPresetKind kind, FillLevel fillLevel)
    {
        var complex = BoundaryPresets.Build(kind, fillLevel);

        foreach (var coefficients in Enum.GetValues<Coefficients>())
        {
            for (int k = 1; k <= (int)fillLevel; k++)
            {
                var lower = BoundaryMatrixBuilder.Build(complex, k, coefficients);
                var upper = BoundaryMatrixBuilder.Build(complex, k + 1, coefficients);
                var product = BoundaryMatrixBuilder.Multiply(lower, upper, coefficients);

                for (int r = 0; r < product.GetLength(0); r++)
                {
                    for (int c = 0; c < product.GetLength(1); c++)
                    {
                        Assert.Equal(0, product[r, c]);
                    }
                }
            }
        }
    }

    [Theory]
    [InlineData(BoundaryPresetKind.Tetrahedron, FillLevel.Frame, 4, 6, 0, 0)]
    [InlineData(BoundaryPresetKind.Tetrahedron, FillLevel.Surface, 4, 6, 4, 0)]
    [InlineData(BoundaryPresetKind.Tetrahedron, FillLevel.Solid, 4, 6, 4, 1)]
    [InlineData(BoundaryPresetKind.TetrahedronWithOpenTriangle, FillLevel.Frame, 5, 8, 0, 0)]
    [InlineData(BoundaryPresetKind.TetrahedronWithOpenTriangle, FillLevel.Surface, 5, 8, 4, 0)]
    [InlineData(BoundaryPresetKind.TetrahedronWithOpenTriangle, FillLevel.Solid, 5, 8, 4, 1)]
    public void Presets_HaveExpectedSimplexCounts(
        BoundaryPresetKind kind, FillLevel fillLevel, int vertices, int edges, int triangles, int tetrahedra)
    {
        var complex = BoundaryPresets.Build(kind, fillLevel);

        Assert.Equal(vertices, complex.Count(0));
        Assert.Equal(edges, complex.Count(1));
        Assert.Equal(triangles, complex.Count(2));
        Assert.Equal(tetrahedra, complex.Count(3));
    }

    [Theory]
    [MemberData(nameof(AllPresets))]
    public void Presets_AreClosedUnderTakingFaces(BoundaryPresetKind kind, FillLevel fillLevel)
    {
        Assert.True(BoundaryPresets.Build(kind, fillLevel).IsClosed());
    }

    [Theory]
    [MemberData(nameof(AllPresets))]
    public void SimplicesAreOrderedLexicographicallyWithinEachDimension(
        BoundaryPresetKind kind, FillLevel fillLevel)
    {
        var complex = BoundaryPresets.Build(kind, fillLevel);

        for (int k = 0; k <= (int)fillLevel; k++)
        {
            var simplices = complex.ByDimension(k);
            for (int i = 1; i < simplices.Count; i++)
            {
                Assert.True(simplices[i - 1].CompareTo(simplices[i]) < 0);
            }
        }
    }

    [Theory]
    [InlineData(2, 4)]
    [InlineData(3, 4)]
    public void OpenTriangleEdges_AreTheFaceOfNoTriangle(int a, int b)
    {
        var complex = BoundaryPresets.Build(BoundaryPresetKind.TetrahedronWithOpenTriangle, FillLevel.Solid);
        var boundary2 = BoundaryMatrixBuilder.Build(complex, 2);
        int row = complex.IndexOf(OrientedSimplex.Edge(a, b));

        Assert.True(row >= 0);

        // An all-zero row of d_2 - equivalently an all-zero column of delta^1.
        for (int c = 0; c < boundary2.GetLength(1); c++) Assert.Equal(0, boundary2[row, c]);

        var coboundary1 = BoundaryMatrixBuilder.Transpose(boundary2);
        for (int r = 0; r < coboundary1.GetLength(0); r++) Assert.Equal(0, coboundary1[r, row]);
    }

    [Fact]
    public void OpenTriangle_HasItsThreeEdgesButNotItsFace()
    {
        var complex = BoundaryPresets.Build(BoundaryPresetKind.TetrahedronWithOpenTriangle, FillLevel.Solid);

        Assert.True(complex.Contains(OrientedSimplex.Edge(2, 3)));
        Assert.True(complex.Contains(OrientedSimplex.Edge(2, 4)));
        Assert.True(complex.Contains(OrientedSimplex.Edge(3, 4)));

        // The unfilled face is the whole point: it is what leaves a hole.
        Assert.False(complex.Contains(OrientedSimplex.Triangle(2, 3, 4)));
    }

    // --- degenerate degrees must be shaped, not rejected ---

    [Fact]
    public void Boundary0_IsTheZeroMapWithNoRows()
    {
        var complex = BoundaryPresets.Build(BoundaryPresetKind.Tetrahedron, FillLevel.Solid);

        var boundary0 = BoundaryMatrixBuilder.Build(complex, 0);

        Assert.Equal(0, boundary0.GetLength(0));
        Assert.Equal(4, boundary0.GetLength(1));
        Assert.Equal(0, MatrixRank.ComputeRational(boundary0));
    }

    [Fact]
    public void BoundaryAboveTheTopDimension_HasNoColumns()
    {
        var complex = BoundaryPresets.Build(BoundaryPresetKind.Tetrahedron, FillLevel.Surface);

        var boundary3 = BoundaryMatrixBuilder.Build(complex, 3);

        Assert.Equal(4, boundary3.GetLength(0)); // the four triangles
        Assert.Equal(0, boundary3.GetLength(1)); // no tetrahedra at this fill level
    }
}

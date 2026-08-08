using PersistentHomologyCore.Models;
using PersistentHomologyCore.Services;

namespace PersistentHomologyCore.Tests;

public class SimplicialHomologyTests
{
    // The fill-level progression is the page's central claim: the same four
    // points give three different homologies as columns appear in d_2 and d_3.
    [Theory]
    [InlineData(BoundaryPresetKind.Tetrahedron, FillLevel.Frame, 1, 3, 0)]
    [InlineData(BoundaryPresetKind.Tetrahedron, FillLevel.Surface, 1, 0, 1)]
    [InlineData(BoundaryPresetKind.Tetrahedron, FillLevel.Solid, 1, 0, 0)]
    // The open triangle is the preset that makes the Betti readout say
    // something: its unfilled loop is a hole no fill level can close.
    [InlineData(BoundaryPresetKind.TetrahedronWithOpenTriangle, FillLevel.Frame, 1, 4, 0)]
    [InlineData(BoundaryPresetKind.TetrahedronWithOpenTriangle, FillLevel.Surface, 1, 1, 1)]
    [InlineData(BoundaryPresetKind.TetrahedronWithOpenTriangle, FillLevel.Solid, 1, 1, 0)]
    public void Presets_HaveExpectedBettiNumbers(
        BoundaryPresetKind kind, FillLevel fillLevel, int beta0, int beta1, int beta2)
    {
        var complex = BoundaryPresets.Build(kind, fillLevel);
        int[] expected = [beta0, beta1, beta2];

        foreach (var coefficients in Enum.GetValues<Coefficients>())
        {
            var info = SimplicialHomology.Compute(complex, coefficients);

            for (int k = 0; k < expected.Length; k++)
            {
                int actual = k < info.Count ? info[k].Betti : 0;
                Assert.Equal(expected[k], actual);
            }
        }
    }

    [Theory]
    [MemberData(nameof(BoundaryMatrixBuilderTests.AllPresets), MemberType = typeof(BoundaryMatrixBuilderTests))]
    public void EulerCharacteristic_EqualsAlternatingSumOfBettiNumbers(
        BoundaryPresetKind kind, FillLevel fillLevel)
    {
        var complex = BoundaryPresets.Build(kind, fillLevel);
        var info = SimplicialHomology.Compute(complex);

        int fromBetti = 0;
        foreach (var dimension in info)
        {
            fromBetti += (dimension.Dimension % 2 == 0 ? 1 : -1) * dimension.Betti;
        }

        Assert.Equal(complex.EulerCharacteristic, fromBetti);
    }

    [Theory]
    [MemberData(nameof(BoundaryMatrixBuilderTests.AllPresets), MemberType = typeof(BoundaryMatrixBuilderTests))]
    public void RationalAndMod2Ranks_Agree(BoundaryPresetKind kind, FillLevel fillLevel)
    {
        // No torsion anywhere in these complexes, so the coefficient toggle is
        // a change of display rather than of answer. The explainer says so; if
        // a preset is ever added that breaks this, the claim needs revisiting.
        var complex = BoundaryPresets.Build(kind, fillLevel);

        for (int k = 0; k <= (int)fillLevel + 1; k++)
        {
            var overZ = BoundaryMatrixBuilder.Build(complex, k);
            var overF2 = BoundaryMatrixBuilder.Build(complex, k, Coefficients.Mod2);

            Assert.Equal(MatrixRank.ComputeRational(overZ), MatrixRank.ComputeMod2(overF2));
        }
    }

    [Fact]
    public void CycleRankAndBoundaryRank_SplitTheSimplexCount()
    {
        var complex = BoundaryPresets.Build(BoundaryPresetKind.TetrahedronWithOpenTriangle, FillLevel.Solid);

        foreach (var dimension in SimplicialHomology.Compute(complex))
        {
            // rank-nullity, which is what the readout's columns are showing
            Assert.Equal(dimension.SimplexCount, dimension.CycleRank + dimension.BoundaryRank);
            Assert.Equal(dimension.CycleRank - dimension.BoundaryRankAbove, dimension.Betti);
        }
    }

    [Fact]
    public void SolidTetrahedron_HasTheExpectedRankBreakdown()
    {
        var complex = BoundaryPresets.Build(BoundaryPresetKind.Tetrahedron, FillLevel.Solid);
        var info = SimplicialHomology.Compute(complex);

        //  k  n_k  rank d_k  dim ker d_k  rank d_(k+1)  beta_k
        //  0    4         0            4             3       1
        //  1    6         3            3             3       0
        //  2    4         3            1             1       0
        //  3    1         1            0             0       0
        Assert.Equal(new HomologyDimensionInfo(0, 4, 0, 4, 3, 1), info[0]);
        Assert.Equal(new HomologyDimensionInfo(1, 6, 3, 3, 3, 0), info[1]);
        Assert.Equal(new HomologyDimensionInfo(2, 4, 3, 1, 1, 0), info[2]);
        Assert.Equal(new HomologyDimensionInfo(3, 1, 1, 0, 0, 0), info[3]);
    }
}

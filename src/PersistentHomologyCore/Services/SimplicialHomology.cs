using PersistentHomologyCore.Models;

namespace PersistentHomologyCore.Services;

/// <summary>
/// Betti numbers of an explicit complex, straight from the definition:
/// <c>H_k = ker d_k / im d_(k+1)</c>, so
/// <c>beta_k = (n_k - rank d_k) - rank d_(k+1)</c>.
/// <para>
/// Deliberately not the persistence path. <see cref="BoundaryMatrixReducer"/>
/// pairs births with deaths across a filtration and is the right tool there;
/// here there is no filtration, and the point is to show that the plain ranks
/// of the displayed matrices are all the Betti numbers ever were.
/// </para>
/// </summary>
public static class SimplicialHomology
{
    /// <summary>
    /// One entry per dimension 0..maxDimension of the complex (at least
    /// dimension 0, so the empty complex still reports beta_0 = 0).
    /// </summary>
    public static IReadOnlyList<HomologyDimensionInfo> Compute(
        SimplicialComplex complex,
        Coefficients coefficients = Coefficients.Integer)
    {
        ArgumentNullException.ThrowIfNull(complex);

        int top = Math.Max(complex.MaxDimension, 0);
        var ranks = new int[top + 2];
        for (int k = 0; k <= top + 1; k++)
        {
            ranks[k] = MatrixRank.Compute(BoundaryMatrixBuilder.Build(complex, k, coefficients), coefficients);
        }

        var result = new HomologyDimensionInfo[top + 1];
        for (int k = 0; k <= top; k++)
        {
            int simplexCount = complex.Count(k);
            int cycleRank = simplexCount - ranks[k];
            result[k] = new HomologyDimensionInfo(
                Dimension: k,
                SimplexCount: simplexCount,
                BoundaryRank: ranks[k],
                CycleRank: cycleRank,
                BoundaryRankAbove: ranks[k + 1],
                Betti: cycleRank - ranks[k + 1]);
        }
        return result;
    }
}

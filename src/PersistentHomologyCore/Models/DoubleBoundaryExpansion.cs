namespace PersistentHomologyCore.Models;

/// <summary>
/// One term of the doubly-expanded boundary, carrying the sign it ends up with
/// and which other term it cancels against.
/// </summary>
/// <param name="Simplex">The codimension-2 face.</param>
/// <param name="Sign">The product of the outer and inner alternating signs.</param>
/// <param name="CancellingPair">
/// Shared with exactly one other term, the one carrying the opposite sign.
/// Used to colour-match the pair in the UI.
/// </param>
public readonly record struct DoubleBoundaryTerm(OrientedSimplex Simplex, int Sign, int CancellingPair);

/// <summary>One face of the original simplex, with its own boundary expanded.</summary>
public readonly record struct DoubleBoundaryRow(
    OrientedSimplex Face,
    int Sign,
    IReadOnlyList<DoubleBoundaryTerm> Terms);

/// <summary>
/// The worked expansion of <c>d(d sigma) = 0</c> for one simplex: every
/// codimension-2 face turns up exactly twice with opposite signs, so the whole
/// sum annihilates in pairs. That pairing is the mechanism behind the identity,
/// and it is invisible without signs - which is the case for showing integer
/// coefficients rather than Z/2.
/// </summary>
public sealed record DoubleBoundaryExpansion(OrientedSimplex Simplex, IReadOnlyList<DoubleBoundaryRow> Rows)
{
    /// <summary>Number of cancelling pairs - half the total number of terms.</summary>
    public int PairCount
    {
        get
        {
            int terms = 0;
            foreach (var row in Rows) terms += row.Terms.Count;
            return terms / 2;
        }
    }
}

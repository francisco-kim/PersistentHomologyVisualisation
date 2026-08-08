namespace PersistentHomologyCore.Models;

/// <summary>
/// The linear algebra of one dimension, as displayed in the boundary
/// explorer's readout: how many k-simplices there are, and how the rank-nullity
/// split of the two adjacent boundary maps produces the Betti number
/// <c>beta_k = dim ker d_k - rank d_(k+1)</c>.
/// </summary>
/// <param name="Dimension">k.</param>
/// <param name="SimplexCount">n_k, the number of k-simplices.</param>
/// <param name="BoundaryRank">rank d_k, the dimension of the boundaries one dimension down.</param>
/// <param name="CycleRank">dim ker d_k = n_k - rank d_k, the k-cycles.</param>
/// <param name="BoundaryRankAbove">rank d_(k+1), the k-cycles that bound something above.</param>
/// <param name="Betti">beta_k = CycleRank - BoundaryRankAbove.</param>
public readonly record struct HomologyDimensionInfo(
    int Dimension,
    int SimplexCount,
    int BoundaryRank,
    int CycleRank,
    int BoundaryRankAbove,
    int Betti);

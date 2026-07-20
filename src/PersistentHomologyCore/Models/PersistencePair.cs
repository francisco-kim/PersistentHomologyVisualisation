namespace PersistentHomologyCore.Models;

/// <summary>
/// One birth-death pair from the persistence computation, plus a concrete
/// geometric representative for highlighting the feature it corresponds to.
/// </summary>
/// <param name="Dimension">0 for a connected component, 1 for a loop.</param>
/// <param name="Death">double.PositiveInfinity for an essential (never-dying) feature.</param>
/// <param name="RepresentativeVertices">
/// H0 only: point indices of the absorbed component (finite pair) or the
/// full component (essential pair). Empty for H1.
/// </param>
/// <param name="RepresentativeEdgePairs">
/// Flattened point-index pairs [i0,j0,i1,j1,...] of the representative cycle
/// (H1) or the single merging edge (H0, finite pairs only).
/// </param>
public sealed record PersistencePair(
    int Dimension,
    double Birth,
    double Death,
    int BirthSimplexOrder,
    int DeathSimplexOrder,
    int[] RepresentativeVertices,
    int[] RepresentativeEdgePairs)
{
    public double Persistence => Death - Birth;

    public bool IsEssential => double.IsPositiveInfinity(Death);

    public bool IsZeroPersistence => !IsEssential && Birth == Death;
}

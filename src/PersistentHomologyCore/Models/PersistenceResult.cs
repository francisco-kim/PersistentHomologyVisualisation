using PersistentHomologyCore.Services;

namespace PersistentHomologyCore.Models;

public sealed class PersistenceResult
{
    public RipsFiltration Filtration { get; }
    public IReadOnlyList<PersistencePair> H0 { get; }
    public IReadOnlyList<PersistencePair> H1 { get; }

    public PersistenceResult(RipsFiltration filtration, IReadOnlyList<PersistencePair> h0, IReadOnlyList<PersistencePair> h1)
    {
        Filtration = filtration;
        H0 = h0;
        H1 = h1;
    }

    /// <summary>Number of connected components ("pieces") alive at epsilon.</summary>
    public int Components(double epsilon) => H0.Count(p => p.Birth <= epsilon && epsilon < p.Death);

    /// <summary>Number of independent loops ("holes") alive at epsilon.</summary>
    public int Holes(double epsilon) => H1.Count(p => p.Birth <= epsilon && epsilon < p.Death);
}

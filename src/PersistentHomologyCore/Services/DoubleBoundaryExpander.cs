using PersistentHomologyCore.Models;

namespace PersistentHomologyCore.Services;

/// <summary>
/// Expands <c>d_(k-1)(d_k sigma)</c> term by term, without collecting, so the
/// cancellation can be shown rather than asserted.
/// </summary>
public static class DoubleBoundaryExpander
{
    /// <summary>
    /// Requires a simplex of dimension at least 2 - below that there is nothing
    /// to expand, since d_0 is the zero map.
    /// </summary>
    public static DoubleBoundaryExpansion Expand(OrientedSimplex simplex)
    {
        if (simplex.Dimension < 2)
            throw new ArgumentException("Expansion needs a simplex of dimension 2 or more.", nameof(simplex));

        // First appearance of a codimension-2 face opens a pair; the second
        // closes it. Every face appears exactly twice - that is the theorem.
        var pairIds = new Dictionary<OrientedSimplex, int>();
        var rows = new DoubleBoundaryRow[simplex.VertexCount];

        for (int i = 0; i < simplex.VertexCount; i++)
        {
            var face = simplex.FaceOmitting(i);
            int outerSign = i % 2 == 0 ? 1 : -1;

            var terms = new DoubleBoundaryTerm[face.VertexCount];
            for (int j = 0; j < face.VertexCount; j++)
            {
                var lower = face.FaceOmitting(j);
                int innerSign = j % 2 == 0 ? 1 : -1;

                if (!pairIds.TryGetValue(lower, out int pairId))
                {
                    pairId = pairIds.Count;
                    pairIds[lower] = pairId;
                }
                terms[j] = new DoubleBoundaryTerm(lower, outerSign * innerSign, pairId);
            }
            rows[i] = new DoubleBoundaryRow(face, outerSign, terms);
        }
        return new DoubleBoundaryExpansion(simplex, rows);
    }
}

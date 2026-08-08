using PersistentHomologyCore.Models;
using PersistentHomologyCore.Services;

namespace PersistentHomologyCore.Tests;

public class DoubleBoundaryExpanderTests
{
    [Theory]
    [InlineData(2)]
    [InlineData(3)]
    public void EveryCodimension2Face_AppearsTwiceWithOppositeSigns(int dimension)
    {
        var simplex = dimension == 2
            ? OrientedSimplex.Triangle(0, 1, 2)
            : OrientedSimplex.Tetrahedron(0, 1, 2, 3);

        var totals = new Dictionary<OrientedSimplex, int>();
        var occurrences = new Dictionary<OrientedSimplex, int>();

        foreach (var row in DoubleBoundaryExpander.Expand(simplex).Rows)
        {
            foreach (var term in row.Terms)
            {
                totals[term.Simplex] = totals.GetValueOrDefault(term.Simplex) + term.Sign;
                occurrences[term.Simplex] = occurrences.GetValueOrDefault(term.Simplex) + 1;
            }
        }

        Assert.All(totals.Values, sum => Assert.Equal(0, sum));
        Assert.All(occurrences.Values, count => Assert.Equal(2, count));
    }

    [Fact]
    public void CancellingPairIds_GroupExactlyTwoTermsOfOppositeSign()
    {
        var expansion = DoubleBoundaryExpander.Expand(OrientedSimplex.Tetrahedron(0, 1, 2, 3));

        var byPair = new Dictionary<int, List<DoubleBoundaryTerm>>();
        foreach (var row in expansion.Rows)
        {
            foreach (var term in row.Terms)
            {
                if (!byPair.TryGetValue(term.CancellingPair, out var bucket))
                {
                    bucket = [];
                    byPair[term.CancellingPair] = bucket;
                }
                bucket.Add(term);
            }
        }

        Assert.Equal(6, expansion.PairCount); // 4 faces x 3 edges each, in pairs
        Assert.Equal(6, byPair.Count);
        Assert.All(byPair.Values, bucket =>
        {
            Assert.Equal(2, bucket.Count);
            Assert.Equal(bucket[0].Simplex, bucket[1].Simplex);
            Assert.Equal(0, bucket[0].Sign + bucket[1].Sign);
        });
    }

    [Fact]
    public void Tetrahedron_ExpandsIntoTheDocumentedFaces()
    {
        var expansion = DoubleBoundaryExpander.Expand(OrientedSimplex.Tetrahedron(0, 1, 2, 3));

        // d_3[0123] = [123] - [023] + [013] - [012]
        Assert.Equal(
            [OrientedSimplex.Triangle(1, 2, 3), OrientedSimplex.Triangle(0, 2, 3),
             OrientedSimplex.Triangle(0, 1, 3), OrientedSimplex.Triangle(0, 1, 2)],
            expansion.Rows.Select(row => row.Face));
        Assert.Equal([1, -1, 1, -1], expansion.Rows.Select(row => row.Sign));
    }

    [Fact]
    public void VerticesAndEdges_CannotBeExpanded()
    {
        Assert.Throws<ArgumentException>(() => DoubleBoundaryExpander.Expand(OrientedSimplex.Vertex(0)));
        Assert.Throws<ArgumentException>(() => DoubleBoundaryExpander.Expand(OrientedSimplex.Edge(0, 1)));
    }
}

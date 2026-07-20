# Persistent homology visualisation

An interactive, browser-based simulation of persistent homology on 2D point
clouds — aimed at non-mathematicians. It answers, visually, the question
topological data analysis is built to answer: *which shapes in noisy data are
real, and which are just noise?*

**Live demo:** <https://francisco-kim.github.io/PersistentHomologyVisualisation/>

The topology is not a cartoon: the app builds the actual Vietoris–Rips
filtration of your point cloud and computes real persistent homology (H0, H1)
via boundary-matrix reduction over Z/2, entirely in your browser
(.NET WebAssembly, AOT-compiled).

## What you can do

- **Edit the point cloud**: click to add points, right-click to remove them,
  or load a **preset** (noisy circle, two circles, figure-eight, annulus,
  random clusters, spiral) with an adjustable noise level.
- **Grow the radius ε** with a slider (or hit **Play** to animate it) and
  watch balls around each point merge into edges and triangles in real time.
- Read the **barcode**: one bar per connected component (H0) or loop (H1),
  spanning the range of ε where that feature exists. Short bars are noise;
  long bars are real shape.
- Hover a bar to **highlight the actual feature** — the cycle or cluster it
  represents — directly on the point cloud.
- Switch to the **advanced view** for the persistence diagram (birth vs.
  death scatter) alongside the barcode.

## Topology

See the in-app explainer (below the simulation, and in full at
[`/explainer`](https://francisco-kim.github.io/PersistentHomologyVisualisation/explainer))
for the conceptual walkthrough: the Vietoris–Rips complex, the filtration,
homology and Betti numbers, and the persistence diagram's stability
guarantee. What follows here is the numerical computation behind it.

### The Vietoris–Rips filtration

Given points $X = \{x_1, \dots, x_n\} \subset \mathbb{R}^2$ and a maximum
radius $\varepsilon_{\max}$, `RipsFiltrationBuilder` builds every simplex of
dimension $\le 2$:

- **Vertices**: all $n$ points, entering at filtration value 0.
- **Edges**: every pair $(i, j)$ with $d(x_i, x_j) \le \varepsilon_{\max}$,
  filtration value $d(x_i, x_j)$ — brute-force pairwise distance, $O(n^2)$,
  building a sorted per-vertex adjacency list as a by-product.
- **Triangles**: for every edge $(i, j)$, the adjacency lists of $i$ and $j$
  are intersected (two-pointer merge over the sorted lists) to find common
  neighbours $k > j$; each triangle is visited exactly once, with filtration
  value $\max(d_{ij}, d_{ik}, d_{jk})$.

Triangle count is $O(n^3)$ worst case — a dense cloud could produce millions.
Rather than cap $\varepsilon_{\max}$ globally (which would also truncate
edges and hide real H0 structure), the builder runs a **two-pass histogram
budget** restricted to triangles only: pass 1 counts triangles into a
1024-bin histogram over $[0, \varepsilon_{\max}]$ without storing them; if
the total exceeds the budget (200,000 simplices by default, minus vertices
and edges already counted), the largest histogram bin boundary
$\varepsilon_{\text{tri}}$ whose cumulative count still fits becomes the
triangle cutoff, and pass 2 stores only triangles with filtration
$\le \varepsilon_{\text{tri}}$ — reported to the UI as a truncation notice.

Every simplex is then sorted once by $(\text{filtration}, \text{dimension},
\text{vertices})$: the dimension tiebreak guarantees every face is ordered
before its cofaces at equal filtration value, which the reduction below
depends on.

### The boundary-matrix reduction

Persistence is computed by the standard algorithm (Edelsbrunner–Letscher–
Zomorodian / Zomorodian–Carlsson), over $\mathbb{Z}/2$ so a chain is just a
*set* of simplices and every boundary map is a sparse column of ascending
column indices with no signs to track:

$$
\partial_1[a,b] = \{a, b\}, \qquad \partial_2[a,b,c] = \{[a,b],\, [a,c],\, [b,c]\}
$$

Reduction repeatedly XORs (symmetric-differences) a column with an earlier
column sharing the same *low* (largest remaining index), until every
column's low is unique or the column vanishes. A vanishing column marks a
birth; a column with low $i$ pairs with simplex $i$ as its death partner.

`BoundaryMatrixReducer` applies the **clearing (twist) optimisation**:
dimension-2 (triangle) columns are reduced first, in filtration order,
against a pivot table indexed by edge columns. Any edge column that turns
out to be a triangle's low is marked *cleared* — proven, by the algorithm's
own theorem, to reduce to zero — so the dimension-1 pass skips it entirely
rather than recomputing the same result. The dimension-1 pass then reduces
the remaining (uncleared) edge columns against a pivot table indexed by
vertex columns, which simultaneously yields H0 birth/death pairs *and*
detects **essential H1 classes**: an uncleared edge column that reduces all
the way to empty represents a brand-new independent loop that nothing in
the filtration ever fills in.

### Representative cycles

For a **finite** H1 pair (edge $e$ born, triangle $t$ closes it), the fully
reduced column $R_t$ is guaranteed to be a genuine 1-cycle — a set of edges
with even degree at every vertex — because $R_t = \partial(\text{some
combination of triangles})$ and $\partial \circ \partial = 0$ is a theorem,
not a computation. That column, read back as point pairs, is exactly what
lights up on hover.

For an **essential** H1 class there is no triangle to reduce against, so the
reducer separately tracks a $V$-column for every edge (initialised to
$\{e\}$, updated with the same XORs as $R$): when edge $e$'s own reduction
vanishes, $V_e$ — the specific combination of earlier edges that cancelled
it — is itself a cycle with zero boundary, and becomes the representative.

### Connected components via union-find

H0 birth/death values fall out of the dimension-1 reduction pass above for
free, but recovering *which* points belong to a newly-merged cluster (for
highlighting) from the matrix isn't cheap. Instead, `UnionFind`
independently replays the filtration's edges in order with a standard
union-by-size structure, recording the absorbed side's point indices at
every merge. Because both the matrix reduction and union-find are computing
the persistent H0 of the same 1-skeleton, they are mathematically
guaranteed to produce identical birth/death pairs — a fact the test suite
cross-checks directly (`UnionFindH0_MatchesMatrixReductionH0`) rather than
assumes.

### Complexity, in practice

At the UI's 100-point slider cap (the underlying library allows up to 300),
edge enumeration is at most $\binom{100}{2} = 4{,}950$ pairwise distances,
comfortably inside the triangle budget with room to spare — the histogram
truncation exists for the library's higher ceiling, not for anything
reachable from the slider. Recomputation (build + reduce, via
`PersistenceEngine.Compute`) runs synchronously on every point-cloud edit;
it is never triggered by moving ε alone, which only changes which prefix of
the already filtration-sorted edge/triangle arrays gets drawn.

## Repository layout

```
src/PersistentHomologyCore/   topology library (no package dependencies)
  Models/                     Point2D, Simplex, PersistencePair, PresetKind
  Services/                   Rips construction, boundary-matrix reduction,
                               union-find, preset generators
src/PersistentHomologyWeb/    Blazor WebAssembly app (canvas rendering via
                               zero-copy [JSImport] MemoryView interop)
tests/PersistentHomologyCore.Tests  xUnit tests with known-answer persistence
                               computations (e.g. unit square -> H1 = [1, sqrt(2)])
```

## Getting started

```sh
dotnet test                                    # topology tests
dotnet run --project src/PersistentHomologyWeb # local dev server (slow, interpreted)
dotnet publish src/PersistentHomologyWeb -c Release -p:EnableAot=true -o publish
```

The development server runs the .NET IL interpreter and is an order of
magnitude slower than the published AOT build — judge interactivity from the
published output, not `dotnet run`.

Deployment to GitHub Pages is automated by
`.github/workflows/deploy-pages.yml` on every push to `main`.

## License

MIT — see [LICENSE.txt](LICENSE.txt).

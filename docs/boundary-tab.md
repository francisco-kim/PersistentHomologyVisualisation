# The boundary-operator tab

Design notes for `/boundary` and `/boundary/explainer`. The README says what the
tab *is*; this says how it is put together and which parts are load-bearing.

Files:

```
src/PersistentHomologyCore/
  Models/OrientedSimplex.cs        signed simplex, dimensions 0-3
  Models/SimplicialComplex.cs      explicit unfiltered complex, closed downwards
  Models/BoundaryPresetKind.cs     the two presets, and FillLevel
  Models/DoubleBoundaryExpansion.cs
  Services/BoundaryMatrixBuilder.cs
  Services/BoundaryPresets.cs
  Services/MatrixRank.cs           exact rank over Q (Bareiss) and over F_2
  Services/SimplicialHomology.cs   Betti numbers straight from the definition
  Services/DoubleBoundaryExpander.cs
src/PersistentHomologyWeb/
  Pages/BoundaryOperators.razor    the tab
  Pages/BoundaryExplainer.razor    its explanation page
  Components/BoundaryMatrix.razor  one matrix as a clickable table
  Services/BoundaryExplorerState.cs
  Interop/Complex3DInterop.cs
  wwwroot/js/phComplex3d.js        projection, rendering, picking
```

Nothing here shares code with the persistence path. `OrientedSimplex` is
deliberately not `Simplex`, and `SimplicialComplex` is deliberately not
`RipsFiltration`: this side needs a fourth dimension and *signs*, and needs no
filtration values at all. `SimplicialHomology` computes Betti numbers from
ranks rather than by reduction, because the whole point of the tab is that the
displayed matrices are all the Betti numbers ever were.

## What clearing the selection means

`BoundaryExplorerState` splits its recomputation in two, and the split is a
correctness rule rather than an optimisation:

- **Preset or fill level changes** → `Recompute()`, which rebuilds the complex
  and **clears the selection**. It has to: the selection is a
  `(dimension, index)` pair, and after the complex changes, index 3 in
  dimension 2 denotes a different triangle.
- **Coefficient changes** (Z vs Z/2) → `RecomputeMatrices()` only, and the
  selection **survives**. The complex is untouched, so the indices still mean
  what they meant — and keeping the selection is the point. Ticking Z/2 with a
  tetrahedron selected is how you see what the signs were doing; dropping the
  selection would take the comparison away at the moment it was asked for.
- **Transpose changes** → `RecomputeRelated()` only. By `rank(M) = rank(M^T)`
  it cannot move a Betti number; it only flips the highlight from faces to
  cofaces.

## Coefficients are a display concern too

Over Z/2 there is no sign to show, so the UI must not print one. Three places
follow the coefficient setting: `BoundaryMatrix.Format` (writes `1`, not `+1`),
the `∂∘∂ = 0` expansion terms, and that panel's prose — which otherwise claims
terms cancel "with opposite signs" while showing none. The panel is the one
place a reader can watch the mechanism, so the two modes say different things
on purpose.

## The canvas

`phComplex3d.js` owns the camera and caches the pushed geometry, unlike
`phCanvas.js`. Drag-rotation redraws locally at pointer rate; C# pushes only
the complex (when it changes) and the highlight (when the selection changes).

- **One coordinate system.** The backing store is sized to the element in
  device pixels and the context is scaled by `devicePixelRatio`, so every
  drawing coordinate and every hit radius is a CSS pixel. A `ResizeObserver`
  keeps it in step with orientation changes and the single-column breakpoint,
  neither of which goes through .NET.
- **Picking precedence** is vertex, then tetrahedron centre marker, then edge,
  then frontmost triangle — smaller targets first, because they are the harder
  ones to hit deliberately. The radii are a zero-sum budget: every pixel given
  to vertices and edges is taken from the face interiors, which are the only
  way to select a triangle. At a coarse-pointer boost of 1.8 nothing but
  vertices and edges could be selected at all; 1.35 is the measured ceiling.
- **Only front faces are selectable by clicking the shape**, and from a given
  angle usually just one. That is inherent to picking the frontmost face, not a
  defect — rotate, or use the matrix column headers, which reach every simplex.
- **Face fills are not depth-sorted.** Every face is the same colour at the
  same alpha, and source-over compositing of equal colours is order-independent,
  so a painter's-algorithm sort would cost a sort per frame and produce the
  identical image. Depth still decides picking. If faces ever get distinct
  colours, the sort has to come back — ascending depth, farthest first.
- **Colours are read from CSS custom properties at draw time**, so the module
  subscribes to `prefers-color-scheme` and calls `refresh()`. Without it a
  scheme change leaves the canvas painted in the old palette until something
  else happens to redraw, and nothing else would: the scene redraws only on
  rotation, on resize, or on a push from C#.

## KaTeX

Two constraints, both easy to trip over.

**Only seven font files are vendored** (`wwwroot/lib/katex/fonts`): AMS-Regular,
Main-Regular/Bold/Italic, Math-Italic, Size1, Size2. `katex.min.css` declares
sixteen families, so any macro reaching for one of the other nine — `\mathsf`,
`\mathcal`, `\mathfrak`, `\mathtt`, bold math — produces three failed font
requests per page load and silently falls back to a browser font. Transpose is
written `^{\top}` (Main-Regular) for exactly this reason; it was `^{\mathsf T}`
and it 404ed. Check the network panel after adding notation.

**Typesetting is manual and must happen on the render *after* the content
changes.** `window.renderKatex()` typesets every `[data-katex]` element, and a
Blazor re-render discards whatever KaTeX injected into a span it owns. Pages
therefore set a `_needsKatex` flag and typeset in the *next* `OnAfterRenderAsync`
pass. Anything that swaps maths in or out — selecting (the `∂∘∂` panel appears),
transposing (captions flip between `∂` and `δ`), changing coefficients — must
set that flag.

## Reading the Betti table

`SimplicialHomology.Compute` returns one row per dimension `0..MaxDimension`, so
at **solid** fill there are four rows and β = (1, 1, 0, 0). The trailing β₃ is
honest — it is a real Betti number, and its row shows `rank ∂₃ = 1` killing the
tetrahedron's cavity — but it means the summary caption cannot be the fixed
string "pieces, loops, cavities": at frame fill there are two entries and at
solid there are four. It is generated per entry, with the symbol standing in
above dimension 2, where there is no everyday word for what β counts.

Under that sits the Euler characteristic written twice — the simplex counts
alternating, then the Betti numbers alternating — because the equality is far
more convincing when both sides move under the fill-level control and keep
agreeing than when it is asserted. Both sums run to the complex's top populated
dimension, so they always have the same number of terms and can be read off
against each other; that also stops the frame level trailing a meaningless
`+ 0 − 0`. The line is `white-space: nowrap` inside its own scroll container,
which is why `.sim-side` and `.control-group` carry `min-width: 0` — without it
a grid or flex item refuses to shrink below a non-wrapping child and the whole
page scrolls sideways on a phone instead of the line scrolling inside itself.

## Verifying changes

There is no automated test for the Web layer — `PersistentHomologyCore.Tests`
covers the topology only, and the presets' expected values are worth restating
because a rank that is off by one turns a Betti number into a lie:

| preset | frame | surface | solid | χ (frame/surface/solid) |
| --- | --- | --- | --- | --- |
| Tetrahedron | (1, 3) | (1, 0, 1) | (1, 0, 0, 0) | −2 / 2 / 1 |
| Tetrahedron + open triangle | (1, 4) | (1, 1, 1) | (1, 1, 0, 0) | −3 / 1 / 0 |

Both agree over Z and over Z/2 (no torsion here). χ must equal the alternating
sum of the β column, and every row must satisfy
`dim ker ∂ₖ = nₖ − rank ∂ₖ` and `βₖ = dim ker ∂ₖ − rank ∂ₖ₊₁`. Multiplying two
adjacent rendered matrices must give the zero matrix.

**Restart the dev server after every build.** `dotnet run --no-build` serves
fingerprinted asset names from a manifest; rebuilding underneath a live server
leaves it advertising `dotnet.<oldhash>.js`, and the page dies with
`Failed to start platform … Failed to fetch dynamically imported module` plus a
404. The symptom looks like a code error and is not one.

# The boundary-operator tab

Design notes for `/boundary` and `/boundary/explainer`. [`tabs.md`](tabs.md)
says what the tab *is*; this says how it is put together and which parts are
load-bearing.

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
- **Coefficient changes** ($\mathbb{Z}$ vs $\mathbb{Z}/2$) → `RecomputeMatrices()`
  only, and the selection **survives**. The complex is untouched, so the indices
  still mean what they meant — and keeping the selection is the point. Ticking
  $\mathbb{Z}/2$ with a tetrahedron selected is how you see what the signs were
  doing; dropping the selection would take the comparison away at the moment it
  was asked for.
- **Transpose changes** → `RecomputeRelated()` only. By
  $\operatorname{rank}(M) = \operatorname{rank}(M^{\top})$ it cannot move a
  Betti number; it only flips the highlight from faces to cofaces.

## Coefficients are a display concern too

Over $\mathbb{Z}/2$ there is no sign to show, so the UI must not print one.
Three places follow the coefficient setting: `BoundaryMatrix.Format` (writes
`1`, not `+1`), the $\partial \circ \partial = 0$ expansion terms, and that
panel's prose — which otherwise claims
terms cancel "with opposite signs" while showing none. The panel is the one
place a reader can watch the mechanism, so the two modes say different things
on purpose.

## Where each matrix goes, and how it is labelled

$\partial_1$ and above live in `.matrix-strip`, one card each, wrapping.
$\partial_0$ does not: it
is the zero map at every preset and every fill level, so it never rewards a
glance and does not deserve a slot beside the matrices that do change. It also
has no table to show — only a sentence — so a card would waste its height. It
gets `.matrix-zero-bar`, a full-width bar trailing the strip, laid out along the
page rather than down it. It lived in the controls panel before that, which made
the panel taller than the canvas beside it.

The coboundary label is $\delta^{k-1} = \partial_k^{\top}$, **except at
degree 0**, which is labelled $\partial_0^{\top}$ alone. $\delta^{-1}$ is what
the formula gives and it is internally consistent, but $C^{-1}$ is a group this
app never introduces and the explainer
never names, so a negative superscript reads as a bug. Any future change to
`TitleTex` has to keep that special case.

## Layout

Two rules that are easy to break without noticing.

**Media-query overrides must follow the rule they override.** The single-column
override for `.lattice-panel` sat above the base rule, at equal specificity, so
`position: sticky` won at every width and the override was dead from the day it
was written. There is no ordering convention enforcing this — check source order
whenever a responsive override appears not to apply.

**Grid and flex items default to `min-width: auto`** and so refuse to shrink
below a non-wrapping descendant, pushing the whole page sideways rather than
scrolling the child. `.sim-side` and `.control-group` set `min-width: 0` for
exactly this reason.

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
- **`setComplex` drops the cached highlight**, mirroring `Recompute()` clearing
  the selection on the C# side and for the same reason: a `(dimension, index)`
  pair means nothing once the complex under it has changed. It has to happen
  there rather than being left to the next push, because `PushComplex` calls
  `SetComplex` *then* `PushHighlight` — so the frame drawn inside `setComplex`
  would otherwise highlight a simplex the new geometry no longer has. It did:
  selecting the tetrahedron and dropping to **frame** left `scene.tets` empty,
  `scene.tets[0]` read `undefined`, and `points[undefined].x` threw inside a
  canvas call, taking down the whole Blazor render tree — "An unhandled error
  has occurred", on every preset or fill-level change made with a selection.
  `drawSimplex` also bounds-checks the index now, so a stale highlight degrades
  to no highlight instead of tearing down the page.
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
pass. Anything that swaps maths in or out — selecting (the
$\partial \circ \partial$ panel appears), transposing (captions flip between
$\partial$ and $\delta$), changing coefficients — must set that flag.

## Reading the Betti table

`SimplicialHomology.Compute` returns one row per dimension `0..MaxDimension`, so
at **solid** fill there are four rows and $\beta = (1, 1, 0, 0)$. The trailing
$\beta_3$ is honest — it is a real Betti number, and its row shows
$\operatorname{rank} \partial_3 = 1$ killing the
tetrahedron's cavity — but it means the summary caption cannot be the fixed
string "pieces, loops, cavities": at frame fill there are two entries and at
solid there are four. It is generated per entry.

**$\beta_3$ is always zero here, structurally.** Nothing above the tetrahedra
can cancel a 3-chain, so $\beta_3 = \dim \ker \partial_3$, and a non-zero
element of that kernel
would be a solid lump of tetrahedra whose surface cancels away completely. No
arrangement of solids in ordinary space does that. It takes something that does
not embed in three dimensions — the boundary of a 4-simplex, five tetrahedra
glued into a 3-sphere — to get $\beta_3 = 1$. The entry is kept anyway, because
$\chi$'s alternating sum uses it and because dropping a row the table shows
would be worse, but it is captioned $\beta_3$ (3D voids) so a permanent zero
does not look like a defect. That names it by the dimension of the class,
matching the explainer's "$\beta_k$ counts $k$-dimensional holes"; the competing
convention names the enclosed region instead, under which "3D void" would mean
$\beta_2$.

## The $\chi$ identity block

Under the Betti summary, the Euler characteristic is written twice over — the
simplex counts alternating, then the Betti numbers alternating, then the value
they agree on. Both sides moving under the fill-level control and staying equal
argues the identity better than asserting it once.

Both sums run to the complex's top populated dimension, so they always have the
same number of terms, can be read off against each other, and the frame level
does not trail a meaningless `+ 0 − 0`. `.euler-identity` is a three-column grid
— symbol, equals, sum — so the equals signs form their own column and the two
sums sit digit above digit; monospace and `tabular-nums` are load-bearing for
that alignment, not decoration. The value uses a typographic minus (U+2212) to
match the one separating the terms, as does the $\chi$ readout beside the
simplex counts.

Each sum is `white-space: nowrap` inside a scroll container, which is why
`.sim-side` and `.control-group` carry `min-width: 0`. Without it a grid or flex
item refuses to shrink below a non-wrapping child, and the page scrolls sideways
on a phone instead of the sum scrolling inside itself.

## Verifying changes

Commands and build caveats are in [`repository.md`](repository.md). There is no
automated test for the Web layer, so the presets' expected values are worth
restating here because a rank that is off by one turns a Betti number into a
lie:

| preset | frame | surface | solid | $\chi$ (frame/surface/solid) |
| --- | --- | --- | --- | --- |
| Tetrahedron | (1, 3) | (1, 0, 1) | (1, 0, 0, 0) | $-2$ / $2$ / $1$ |
| Tetrahedron + open triangle | (1, 4) | (1, 1, 1) | (1, 1, 0, 0) | $-3$ / $1$ / $0$ |

Both agree over $\mathbb{Z}$ and over $\mathbb{Z}/2$ (no torsion here). $\chi$
must equal the alternating sum of the $\beta$ column, and every row must satisfy
$\dim \ker \partial_k = n_k - \operatorname{rank} \partial_k$ and
$\beta_k = \dim \ker \partial_k - \operatorname{rank} \partial_{k+1}$.
Multiplying two adjacent rendered matrices must give the zero matrix.

**Do not judge layout from a full-page screenshot.** `.lattice-panel` is sticky
above 900px, and a stuck element is composited into every stitched segment of a
full-page capture — which renders as the controls overlapping the canvas with a
tall blank gap above them, on a page that is in fact laid out correctly. Capture
viewport-sized shots at successive scroll offsets instead, or measure geometry
directly with `getBoundingClientRect`. Chasing that phantom is what turned up
the dead media query above, but it wasted a pass first.

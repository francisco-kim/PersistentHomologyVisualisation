# The tabs and their explainers

The app has two interactive tabs, and each links to its own explanation page.
The header's **Explanation** link opens whichever one matches the tab you are
on; once there, a strip of subtabs switches between the two without going back
through the app.

| Tab | Explanation |
| --- | --- |
| [`/`](https://francisco-kim.github.io/PersistentHomologyVisualisation/) — simulator | [`/explainer`](https://francisco-kim.github.io/PersistentHomologyVisualisation/explainer) |
| [`/boundary`](https://francisco-kim.github.io/PersistentHomologyVisualisation/boundary) — boundary operators | [`/boundary/explainer`](https://francisco-kim.github.io/PersistentHomologyVisualisation/boundary/explainer) |

## The simulator

- **Edit the point cloud**: click to add points, right-click to remove them,
  or load a **preset** (noisy circle, two circles, figure-eight, annulus,
  random clusters, spiral) with an adjustable noise level.
- **Grow the radius $\varepsilon$** with a slider (or hit **Play** to animate
  it) and watch balls around each point merge into edges and triangles in real
  time.
- Read the **barcode**: one bar per connected component ($H_0$) or loop
  ($H_1$), spanning the range of $\varepsilon$ where that feature exists.
  Short bars are noise; long bars are real shape.
- Hover a bar to **highlight the actual feature** — the cycle or cluster it
  represents — directly on the point cloud.
- Switch to the **advanced view** for the persistence diagram (birth vs.
  death scatter) alongside the barcode.

[`/explainer`](https://francisco-kim.github.io/PersistentHomologyVisualisation/explainer)
is the conceptual walkthrough: the Vietoris–Rips complex, the filtration,
homology and Betti numbers, and the persistence diagram's stability guarantee.
The numerical computation behind it is in the README.

## The boundary operators tab

[`/boundary`](https://francisco-kim.github.io/PersistentHomologyVisualisation/boundary)
takes the matrix the simulator reduces and puts it on screen. A small
hand-authored complex in 3D — a tetrahedron, optionally with a fifth vertex
closing a triangular loop whose face is never filled — is shown alongside its
boundary matrices $\partial_0$ through $\partial_3$, computed over
$\mathbb{Z}$ with signs rather than over $\mathbb{Z}/2$.

- **Click anything** — a row, a column, or the shape itself — to see which
  simplex it stands for. Selecting a column highlights that simplex together
  with the faces listed down its column.
- **Transpose** the matrices to get the coboundary operators
  $\delta^k = \partial_{k+1}^{\top}$. The same click then highlights
  *cofaces* instead of faces: a column of $\partial$ lists what a simplex is
  bounded by, a column of $\partial^{\top}$ lists what it is a face of.
- **Fill level** (frame / surface / solid) adds triangles and then tetrahedra
  without moving a point, so the Betti numbers change purely by columns
  appearing in $\partial_2$ and $\partial_3$ — one column in $\partial_3$
  visibly kills the tetrahedron's cavity.
- A live **rank / nullity / Betti** readout, and a panel expanding
  $\partial(\partial\sigma) = 0$ term by term with the cancelling pairs
  colour-matched.
- The **Euler characteristic** shown both ways at once —
  $\chi = \sum_k (-1)^k n_k = \sum_k (-1)^k \beta_k$ — so the alternating
  simplex count and the alternating hole count visibly stay equal as the fill
  level moves.

The rendering is hand-rolled canvas 2D — weak-perspective projection, depth-
ordered picking and drag rotation — so the tab adds no dependency. Its
explanation lives at
[`/boundary/explainer`](https://francisco-kim.github.io/PersistentHomologyVisualisation/boundary/explainer):
chains and orientation, the alternating-sign formula, why $\partial_0$ is
empty, $\partial \circ \partial = 0$, Betti numbers as ranks, and the
coboundary — including $\delta^0$ as a discrete gradient, where
$(\delta^0 f)([ab]) = f(b) - f(a)$ makes $H^0 \cong H_0$ concrete.

How that tab is built, and which parts are load-bearing, is in
[`boundary-tab.md`](boundary-tab.md).

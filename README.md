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

See the in-app explainer (below the simulation) for the full walkthrough:
the Vietoris–Rips complex, the filtration, homology and Betti numbers, the
boundary-matrix pairing algorithm, and the persistence diagram's stability
guarantee.

## Repository layout

```
src/PersistentHomologyCore/   topology library (no package dependencies)
  Models/                     Point2, Simplex, PersistencePair, PresetKind
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

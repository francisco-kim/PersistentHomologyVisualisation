# Repository layout and builds

```
src/PersistentHomologyCore/   topology library (no package dependencies)
  Models/                     Point2D, Simplex, PersistencePair, PresetKind
  Services/                   Rips construction, boundary-matrix reduction,
                               union-find, preset generators
src/PersistentHomologyWeb/    Blazor WebAssembly app (canvas rendering via
                               zero-copy [JSImport] MemoryView interop)
tests/PersistentHomologyCore.Tests  xUnit tests with known-answer persistence
                               computations (e.g. unit square -> H1 = [1, sqrt(2)])
docs/                         these notes
```

## Commands

```sh
dotnet test                                    # topology tests
dotnet run --project src/PersistentHomologyWeb # local dev server (slow, interpreted)
dotnet publish src/PersistentHomologyWeb -c Release -p:EnableAot=true -o publish
```

The development server runs the .NET IL interpreter and is an order of
magnitude slower than the published AOT build — judge interactivity from the
published output, not `dotnet run`.

**Restart the dev server after every build.** `dotnet run --no-build` serves
fingerprinted asset names from a manifest; rebuilding underneath a live server
leaves it advertising `dotnet.<oldhash>.js`, and the page dies with
`Failed to start platform … Failed to fetch dynamically imported module` plus a
404. The symptom looks like a code error and is not one.

There is no automated test for the Web layer — `PersistentHomologyCore.Tests`
covers the topology only.

## Deployment

Deployment to GitHub Pages is automated by
`.github/workflows/deploy-pages.yml` on every push to `main`.

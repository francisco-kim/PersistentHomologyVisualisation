# CLAUDE.md

```sh
dotnet test                                     # topology tests (Core only)
dotnet run --project src/PersistentHomologyWeb  # dev server, IL-interpreted and slow
```

Restart the dev server after every build — `--no-build` keeps serving the old
fingerprinted asset names and the page dies with `Failed to start platform`.

Only 7 of KaTeX's 16 font families are vendored. New notation reaching for
another (`\mathsf`, `\mathcal`, `\mathtt`, bold math) 404s and silently falls
back; check the network panel.

Deeper notes: [docs/boundary-tab.md](docs/boundary-tab.md). Topology and
algorithms: [README.md](README.md).

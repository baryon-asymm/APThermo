# API.md — .github

What the directory provides to the tree: two workflows, the composite actions and scripts
they share, and a diagnostics probe. It declares no C# surface; the probe's own contract
is in [diagnostics/IsaProbe/API.md](diagnostics/IsaProbe/API.md). Nodes: `.github` and
[diagnostics/IsaProbe](diagnostics/IsaProbe/API.md).

## Workflows ✅

| File | Triggers | Jobs, in the order of `needs` |
|---|---|---|
| `workflows/ci.yml` (name `CI`) | every push, every pull request, manual dispatch | `build`, once per hosted runner `windows-latest` and `ubuntu-latest`: protocol lint, fixtures bound to the generator (Windows only), build, fast suite, packing of both packages, samples against the package, the packed tool's approved example |
| `workflows/release.yml` (name `Release`) | a tag `v*`, manual dispatch | `check`, `matrix` (the hosted matrix), `cuda-windows`, `cuda-linux` (self-hosted, one after the other), `pack`, `publish` (nuget.org, environment `release`, on a push only), `github-release` (on a push only) |

A manual dispatch of `release.yml` is the rehearsal: it runs `check` to `pack` and never
`publish` or `github-release`. A tag `v<version>` must equal the packed version, checked by
`scripts/check-release.sh`.

## Composite actions ✅

| Action | Used by | What it does |
|---|---|---|
| `actions/preflight` | `cuda-windows`, `cuda-linux` | asserts what a self-hosted GPU runner must provide and names every missing item |
| `actions/runner-diagnostics` | every job except `publish` | prints the CPU model and logical core count, and, with the input `dotnet-available` set to `'true'`, runs the probe |

## Scripts ✅

| Script | Run by | What it does |
|---|---|---|
| `scripts/check-release.sh` | `check` | compares the tag with the packed version and extracts the notes of `CHANGELOG.md` |
| `scripts/preflight-windows.ps1` | `actions/preflight` on Windows | the preflight under Windows PowerShell 5.1 |
| `scripts/preflight-linux.sh` | `actions/preflight` on Linux | the preflight under bash |

## Tool ✅

| Path | What it provides |
|---|---|
| `diagnostics/IsaProbe` | the instruction-set facts .NET sees on a runner, printed by `actions/runner-diagnostics` |

`actionlint.yaml` declares the runner label `gpu` of the two self-hosted jobs to actionlint.

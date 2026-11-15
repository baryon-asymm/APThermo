# API.md — .github

What the directory provides to the tree: the workflows and the composite action and scripts
they share. It declares no C# surface. Its source subnodes are [scripts](scripts/API.md), [workflows](workflows/API.md)
and [preflight](actions/preflight/API.md).

⚠ 2026-10-05: was two workflows, their shared actions and scripts and a diagnostics probe
(the node `diagnostics/IsaProbe`, the `runner-diagnostics` action and its step), now without
the probe and the step → HISTORY.md#isaprobe-removed

## Workflows ✅

| File | Triggers | Jobs, in the order of `needs` |
|---|---|---|
| `workflows/ci.yml` (name `CI`) | every push, every pull request, manual dispatch | `build`, OS × core/cli/docs hosted lanes: protocol lint, build, exact generated test-project partition; core alone runs packing and package consumption on each OS and fixture binding on Windows |
| `workflows/trace-gas.yml` (name `Trace gas CPU scans`) | affected pushes/PRs, manual dispatch | `trace-scan`, hosted Windows/Linux CPU: documented scan discovery, both complete scans, nonempty discovery/matrix/TRX artifacts |
| `workflows/release.yml` (name `Release`) | a tag `v*`, manual dispatch | `check`, `matrix` (the hosted matrix), `cuda-windows`, `cuda-linux` (self-hosted, one after the other), `pack`, `publish` (nuget.org, environment `release`, on a push only), `github-release` (on a push only) |

A manual dispatch of `release.yml` is the rehearsal: it runs `check` to `pack` and never
`publish` or `github-release`. A tag `v<version>` must equal the packed version, checked by
`scripts/check-release.sh`.

## Composite actions ✅

| Action | Used by | What it does |
|---|---|---|
| `actions/preflight` | `cuda-windows`, `cuda-linux` | asserts what a self-hosted GPU runner must provide and names every missing item |

## Scripts ✅

| Script | Run by | What it does |
|---|---|---|
| `scripts/check-release.sh` | `check` | compares the tag with the packed version and extracts the notes of `CHANGELOG.md` |
| `scripts/preflight-windows.ps1` | `actions/preflight` on Windows | the preflight under Windows PowerShell 5.1 |
| `scripts/preflight-linux.sh` | `actions/preflight` on Linux | the preflight under bash |

`actionlint.yaml` declares the runner label `gpu` of the two self-hosted jobs to actionlint.

⚠ 2026-11-14: the former single-node claim omitted executable scripts, workflow YAML
and the action manifest. These now have their own sufficient BOOT/API pairs (A22).

⚠ 2026-11-14: the previous filters omitted both LongRunning CPU trace scans. The
separate hosted lane selects them on affected changes; actual hosted-run proof is
still open. No nightly or self-hosted PR lane was introduced.

⚠ 2026-11-15: CI previously ran one solution-wide test command per OS. Hosted
CI/release now use the disjoint exhaustive project lanes declared in
[scripts/API.md](scripts/API.md); the release CUDA/Pack/publication chain is
unchanged. Actual hosted acceptance is tracked in BOOT.md.

## GPU project serialization ⏳

Each self-hosted job runs the original CUDA-or-bit population in two disjoint
invocations: the solution remainder, then both original shared-sweep facts in one
fresh Execution.Tests process. Both use Release/no-build/-m:1 and distinct fresh
TRX paths. Their union preserves every original case identity and the shared sweep's
work, warm-ups, samples and gates. The pair still runs after remainder failure when
build succeeded and the job is not cancelled; the failed job remains failed.
Windows precedes Linux, both precede Pack; existing shells/dependencies/limits and
dispatch publication conditions stay. Actual runner acceptance is in workflows/BOOT.md.

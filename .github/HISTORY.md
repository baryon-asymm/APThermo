# HISTORY.md — .github

Append-only store of what `BOOT.md` no longer needs to state as current truth
(`AGENTS.md`, §15). Newest entry first. Every entry names the date it was moved, the
`BOOT.md` section it came from, and carries the original text in full, unedited. The
start procedure (`AGENTS.md`, §10) does not read this file; it is reached only by
following a dated pointer left in `BOOT.md` at the place the text used to stand.

The four entries after the first were moved here on 2026-10-01 from the root `HISTORY.md`,
with the bullets of the root `## Delivery` they belong to.

---

<a id="isaprobe-deviation"></a>

## 2026-10-01 — from "## Delivery", "Continuous integration" — the declared deviation for IsaProbe is lifted

Moved because `.github` is now a node (`AGENTS.md`, §1) and `.github/diagnostics/IsaProbe`
carries a pair of documents of its own, so the deviation of §1 it declared no longer
stands. The root `BOOT.md` read, in the "Continuous integration" bullet (a declaration of
2026-09-26):

> - **Continuous integration.** GitHub Actions under `.github/workflows`, which holds
>   configuration and is not a node.
>
>   ⚠ 2026-09-26, declared deviation from AGENTS.md §1 (a directory with a build
>   manifest is a node): `.github/diagnostics/IsaProbe` is a C# console project with no
>   `BOOT.md` or `API.md`. It prints the instruction sets .NET sees on a runner, for the
>   runner-diagnostics step of both workflows. It is configuration's tool, not the
>   product's.
>   - What replaces the pair: its header comment states its purpose.
>   - What still binds it: the Diagnostics constraint, since `Directory.Build.props`
>     covers it and `DiagnosticsTests` reads `.github`.
>   - What lifts the deviation: removing the step and the project once the runner
>     diagnostics are retired. → HISTORY.md#delivery-ci-condensed

What lifted it: the linter now reads `.github` as part of the tree (`tools/protocol-lint`,
2026-10-01), which is what made the directory's missing pair visible as a finding, and the
pair was written, here and in `diagnostics/IsaProbe`.

---

<a id="delivery-ci-condensed"></a>

## 2026-10-01 — from "## Delivery, "Continuous integration"" — condensed wording

Moved because `BOOT.md` was over its limit (`AGENTS.md`, §15); the text as it stood
before its wording was condensed:

> - **Continuous integration.** GitHub Actions under `.github/workflows`, which holds
>   configuration and is not a node.
>
>   ⚠ 2026-09-26, declared deviation from AGENTS.md §1 (a directory with a build
>   manifest is a node): `.github/diagnostics/IsaProbe` is a C# console project with no
>   `BOOT.md` or `API.md`. It prints the instruction sets .NET sees on a runner, for the
>   runner-diagnostics step of both workflows (`2bab62d`, the hosted-runner bit
>   investigation of 2026-09-18). It is configuration's tool, not the product's.
>   - What replaces the pair: its header comment states its purpose.
>   - What still binds it: the Diagnostics constraint, since `Directory.Build.props`
>     covers it and `DiagnosticsTests` reads `.github`.
>   - What lifts the deviation: removing the step and the project once the runner
>     diagnostics are retired.
>
>   Found by the guards audit of 2026-09-26: the linter's dot-directory exclusion left
>   it outside the tree with nothing saying so.
>   - Every push and pull request, on Windows and Linux hosted runners: the protocol lint,
>     the build, the fast suite with `APTHERMO_NO_CUDA=1` and without the bit snapshots
>     (`Category!=BitSnapshot`, the ⚠ of 2026-09-18 under the platform constraint), and
>     packing both packages. The release's self-hosted jobs on the reference machine run
>     the bit snapshots with the CUDA tests.
>     Then the samples run against the fresh `APThermo` package from a local feed, the
>     tool installed from that feed runs an approved example, and the docs tests run (the
>     ⚠ of 2026-09-17 under Documentation). The example's output is compared with the
>     approved record field by field, numbers within 1e-9 relative, the Documentation rule
>     of 2026-09-29 (a hosted runner's CPU is not the reference machine's).

---

<a id="ci-field-comparison"></a>

## 2026-10-01 — from "## Delivery", "Continuous integration" — the packed tool's example is compared field by field

Moved because `BOOT.md` was over its limit (`AGENTS.md`, §15); the text as it stood
(a correction of 2026-09-30):

> ⚠ 2026-09-30: the step compared byte for byte. The first run on the hosted Linux
> runner after the equilibrium change of 2026-09-28 failed on the last digits
> (about 1e-13 relative), the same platform difference that moved the bit snapshots.
> The comparison lives in the harness, once (`tests/Harness/BOOT.md`).

---

<a id="no-nightly-run"></a>

## 2026-10-01 — from "## Delivery", "Continuous integration" — the nightly run is dropped

Moved because `BOOT.md` was over its limit (`AGENTS.md`, §15); the text as it stood
(a correction of 2026-09-17):

> ⚠ 2026-09-17: stood "A nightly run adds the long-running tests on the CPU
> accelerator". Every long-running test of the tree is a CUDA test. Under
> `APTHERMO_NO_CUDA=1` it only checks the refusal and returns, so a nightly run on
> hosted runners added nothing (the CI audit of 2026-09-17, G1). The long-running CUDA
> tests run at every release on the self-hosted runners. The user decided to drop the
> nightly run rather than add a CPU-only long test.

---

<a id="rehearsal-first-release"></a>

## 2026-10-01 — from "## Delivery", "Rehearsal before the tag" — the first release took four tag pushes

Moved because `BOOT.md` was over its limit (`AGENTS.md`, §15); the text as it stood
(a correction of 2026-09-19):

> ⚠ 2026-09-19: the first release took four tag pushes (`6924aae`, `f603fd4`,
> `fa4d626`, each moved), each failing on a path that had never run before it: a
> context GitHub rejects in a job-level `env`, a script committed without the
> executable bit, a bit snapshot on a hosted CPU, and a `pwsh` shell absent from both
> self-hosted runners. The dispatch trigger that could have rehearsed all of them
> existed since `c3f5b6b` and was never used; the workflows were verified by reading
> and by a linter, which check syntax, not the host. A post-mortem (Fable 5.1, from the
> runners' own `_diag` logs) found the common cause and set this rule. The tag
> `v0.1.0` is moved one last time, after a green rehearsal, since nothing was ever
> published under it.

---

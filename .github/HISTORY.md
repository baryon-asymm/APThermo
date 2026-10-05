# HISTORY.md — .github

Append-only store of what `BOOT.md` no longer needs to state as current truth
(`AGENTS.md`, §15). Newest entry first. Every entry names the date it was moved, the
`BOOT.md` section it came from, and carries the original text in full, unedited. The
start procedure (`AGENTS.md`, §10) does not read this file; it is reached only by
following a dated pointer left in `BOOT.md` at the place the text used to stand.

The four entries below the entry `isaprobe` were moved here on 2026-10-01 from the root
`HISTORY.md`, with the bullets of the root `## Delivery` they belong to.

---

<a id="ci-end-to-end"></a>

## 2026-10-05 — from "## Constraints", "Continuous integration" — the end-to-end note shortened

Moved when the filter it explains changed (the entry `ci-bit-facts`); the note as it stood:

>     ⚠ 2026-10-03: was "the fast suite" alone, now with the end-to-end facts named: the filters
>     are unchanged, `Category!=BitSnapshot` already takes `Category=EndToEnd`, and the exact
>     process facts carry `Category=BitSnapshot`, so the self-hosted jobs run them

Since 2026-10-05 the hosted filter is `Category!=LongRunning`, which takes the end-to-end
facts and the exact process facts alike.

---

<a id="ci-bit-facts"></a>

## 2026-10-05 — from "## Constraints", "Continuous integration" — the bit snapshots run on the hosted runners

The text as it stood:

>   - Every push and pull request, on Windows and Linux hosted runners: the protocol lint,
>     the build, the fast suite and the end-to-end facts (`Category=EndToEnd`, the facts that
>     start a process) with `APTHERMO_NO_CUDA=1` and without the bit snapshots
>     (`Category!=BitSnapshot`, the ⚠ of 2026-09-18 under the platform constraint; the
>     workflows' filter `Category!=LongRunning&Category!=BitSnapshot` takes both sets), and
>     packing both packages. The release's self-hosted jobs on the reference machine run
>     the bit snapshots with the CUDA tests. Then the samples run against the fresh
>     `APThermo` package from a local feed, the tool installed from that feed runs an
>     approved example, and the docs tests run (the ⚠ of 2026-09-17 under Documentation).
>     The example's output is compared with the approved record field by field, numbers
>     within 1e-9 relative, the Documentation rule of 2026-09-29 (a hosted runner's CPU is
>     not the reference machine's). → HISTORY.md#delivery-ci-condensed

The filter of `ci.yml`'s `build` job and of `release.yml`'s `matrix` job read
`Category!=LongRunning&Category!=BitSnapshot`, and the comment of the packed tool's example
step in `ci.yml` said the exact comparison stayed with the reference machine.

Why it changed: the root lifted its declared deviation from "every test runs on both
platforms" on 2026-10-05 (the root's `HISTORY.md`, entry `platform-deviation-lifted`). The
bits depended on the runner's CPU only through the Windows C runtime's FMA3 or plain
variants of `exp`, `log` and `pow`; the tree's own correctly rounded functions read nothing
of the C runtime, the Windows and WSL2 records of every node are one file, and so the bit
facts run on every hosted runner. The field-by-field comparison of the packed tool's example
stays: it proves the tool runs from the package, and the exact comparison of the same
document is the docs tests node's bit fact in the fast suite. Accepted only on a run
(Invariants, "Evidence for workflow changes"; `## Acceptance criteria`).

---

<a id="preflight-toolkit"></a>

## 2026-10-05 — from "## Invariants", "Self-hosted runners" — the preflight checks no CUDA Toolkit

The text as it stood:

>   - What a runner must provide, checked by a preflight step that names the missing
>     item: git, the .NET SDK of `global.json`, an NVIDIA driver (`nvidia-smi`), libnvvm
>     and `libdevice.10.bc` where the execution node's discovery looks, and no
>     `APTHERMO_NO_CUDA`. Nothing else is assumed: no PowerShell 7, no Python, no Git
>     Bash. A step of a self-hosted job names its shell explicitly, `powershell` on
>     Windows and `bash` on Linux.

`scripts/preflight-windows.ps1` looked for `nvvm64_40_0.dll` and `libdevice.10.bc` under
`CUDA_PATH` and `Program Files\NVIDIA GPU Computing Toolkit\CUDA\v*`, and
`scripts/preflight-linux.sh` for `libnvvm.so` and `libdevice.10.bc` under
`/usr/local/cuda*/nvvm`; both blocks are removed, and the preflight action's description
with them.

Why it changed: the kernels call no libdevice function and the execution node no longer
looks for libnvvm or libdevice (the root's `HISTORY.md`, entry `ilgpu-libdevice-retired`);
the GPU path needs an NVIDIA driver alone. A preflight that still required the toolkit
would fail a runner the tree no longer needs it on.

---

<a id="isaprobe"></a>

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

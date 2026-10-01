# HISTORY.md — Execution.Tests

Append-only store of what `BOOT.md` no longer needs to state as current truth
(`AGENTS.md`, §15). Newest entry first. Every entry names the date it was moved, the
`BOOT.md` section it came from, and carries the original text in full, unedited. The
start procedure (`AGENTS.md`, §10) does not read this file; it is reached only by
following a dated pointer left in `BOOT.md` (or `ACCEPTANCE.md`) at the place the text
used to stand.

---

<a id="chunk-transfer-construction-differences-2026-10-01"></a>

## 2026-10-01 — from "## Acceptance criteria" — criterion: the transfers of Chunks, where the construction differs from the sketch

Moved because `BOOT.md` is over the §15 line limit for its kind of node; what it said stays at the pointer. The text as it stood:

>       The construction differs from the design's sketch in three points, none of which
>       weakens it:
>       - The download runs on a thread of its own, and the test thread holds `syncRoot` until
>         that thread's `ThreadState` shows it blocked on the lock (a timeout of 20 s), instead of
>         a helper thread holding the lock for a fixed time while the test thread downloads. The
>         collection is therefore forced exactly when the copy has taken its address and waits
>         for the lock, and the fact asserts that the thread did block there.
>       - The unpinned sibling array is allocated below the host array. With the host pinned
>         (the fixed code) a compacting collection cannot slide an array that lies above it
>         down past it: the first version of the fact, with the sibling above the host, saw the
>         sibling stay in place at every attempt on the fixed code and failed for that reason.
>         An attempt in which the collection moved nothing is repeated, up to five times.
>       - The first download of the element type runs before the attempts, so that the
>         downloading thread does not compile the transfer while the lock is held.

---

<a id="probe-allocation-replaced-2026-09-30"></a>

## 2026-10-01 — from "## Acceptance criteria" — criterion: the compile guard, a second allocation removed

Moved because `BOOT.md` is over the §15 line limit for its kind of node; what it said stays at the pointer. The text as it stood:

>       ⚠ 2026-09-30: the criterion named one allocation to remove, the 16 GB array of
>       `BatchConstructorsRefuseACountWhoseArrayOverflowsA32BitLength`. Measuring the project's
>       peak found a second: `ProbeMathRefusesAnInputCountWhoseOutputOverflowsA32BitOffset` built
>       a `double[153 391 690]` (1.2 GB) to make `Engine.ProbeMath` refuse it, and the test host
>       of `AcceleratorChoiceTests` alone peaked at 1.37 GiB. With it in place the whole
>       project's test host peaked at 1.81 to 2.11 GiB over the runs made, on both sides of the
>       criterion's bound, so a run could not be called below it. The fact is replaced by one on
>       `MathProbe.OutputLength`, the bound `ProbeMath` now calls, asked of the count just inside
>       and just over the limit. What the old fact proved and the new one does not: that
>       `ProbeMath` itself refuses (the refusal needs an input array of the size it refuses).
>       `ProbeMath` reaches the bound through one call, and the positive path is exercised by
>       `ProbeKernelTests`; a wiring that skipped the call would pass the new fact. Named here so
>       the owner can decide whether that trade stands.

---

<a id="compile-bound-red-once-list-2026-09-30"></a>

## 2026-10-01 — from "## Acceptance criteria" — criterion: the compile guard and the bound checks, red once

Moved because `BOOT.md` is over the §15 line limit for its kind of node; what it said stays at the pointer. The text as it stood:

>       - the attribute removed from `StationSolve.At`: the guard fails, ten of ten runs (five
>         Debug, five Release), and so does the attribute fact, which lives in the performance
>         tests node (`CompileSizeTests.TheStationSolveIsNotInlined`, moved there 2026-09-30
>         because `StationSolve` is internal to `Performance` and only its own tests node may
>         name it);
>       - `Launchers.Clear()` removed from `Engine.Dispose`: `ADisposedEngineHoldsNoLauncher`
>         fails on the count, and with the count assertion removed on the weak reference;
>       - `Context.ClearCache` removed from `KernelCache.Clear`: `ADisposedEngineKeepsNoCompiledProgram`
>         fails (the launcher fact stays green, which is why the second fact exists);
>       - the bound of `BatchLength.Of` turned from `>` into `>=`: the bound fact fails;
>       - the bound of `MathProbe.OutputLength` moved 14 counts down and 14 up, each alone: its
>         fact fails both times (`>` against `>=` is not observable there: no count multiplies
>         to exactly `int.MaxValue`, which is prime).

---

<a id="second-audit-race-and-evidence-2026-09-28"></a>

## 2026-10-01 — from "## Acceptance criteria" — criterion: the second audit's facts, the WSL race and the evidence

Moved because `BOOT.md` is over the §15 line limit for its kind of node; what it said stays at the pointer. The text as it stood:

>       A genuine WSL race, not part of the design: a full `-c Release` run of this
>       project on real CUDA hardware found 22 facts failing together with "CUDA device 0
>       was requested, but 0 device(s) exist", traced to `LaunchBudgetTests` running
>       outside `EngineFixture.CollectionName` and so able to touch the CUDA driver
>       (`CudaException`'s constructor) on a separate thread from `EngineFixture`'s own
>       lazy CUDA engine creation. Fixed by joining the collection; `Execution`'s own
>       criterion has the fuller account, since the fix could not be shown red-once in the
>       usual sense (the race was observed, not reliably reproducible on demand).
>
>       Evidence, on the reference machine: `dotnet build APThermo.sln` 0 warnings,
>       0 errors; `APTHERMO_NO_CUDA=1 dotnet test APThermo.sln --no-build --filter
>       "Category!=LongRunning"`, `Execution.Tests` 159/159, `Protocol.Tests` 32/32;
>       `dotnet
>       test tests/Execution.Tests -c Release` (no filter) 162/162 on Windows, run twice
>       (once before and once after the collection fix — the race never surfaced on
>       Windows), and 162/162 under WSL2 on the collection-fixed commit (a prior run of
>       the commit before it hit the race, 22 failures, all traced to the same cause and
>       resolved by the fix); no `Bits*.approved.txt`, `Throughput*.approved.txt` or
>       `Protocol.Tests/PublicSurface.approved.txt` differs from before this task's first
>       commit; the protocol lint 0 errors, 0 warnings. Six other test nodes
>       (`Equilibrium.Tests`, `Thermo.Tests`, `Performance.Tests`, `Problems.Tests`,
>       `Docs.Tests`, `Cli.Tests`) fail Linux bit or approved-output comparisons under
>       WSL; confirmed pre-existing for `Performance.Tests` by a direct check against two
>       earlier commits (`Execution`'s own criterion has the detail) and reported, not
>       fixed, since every one of those nodes is outside this task's subtree.

---

<a id="ptx-fixtures-stale-2026-09-28"></a>

## 2026-10-01 — from "## Constraints" — constraints: the PTX fixtures were stale

Moved because `BOOT.md` is over the §15 line limit for its kind of node; what it said stays at the pointer. The text as it stood:

>   ⚠ 2026-09-28: the second audit found the fixtures (`Ptx/probe.sm_120.ptx`) still
>   showed the pre-2026-09-27 probe (stepping by 10, three fewer outputs, no `Pow`
>   exponent variety, no `KernelMath` calls), while the inventory facts kept passing:
>   they assert only the wrapper-name relationship the math list and the regime imply,
>   never the literal output count, so a stale fixture is not caught by the tests it
>   feeds. The two files are regenerated on the reference machine from the current
>   `Kernels.Probe` (14 outputs, the F1 fix's two extra `KernelMath.Min`/`Max` orders
>   included), same method (a `PTXBackend` per architecture, before the post-link),
>   header dated 2026-09-28.

---

<a id="long-running-duration-2026-09-28"></a>

## 2026-10-01 — from "## Constraints" — constraints: the duration of the long-running category

Moved because `BOOT.md` is over the §15 line limit for its kind of node; what it said stays at the pointer. The text as it stood:

>   ⚠ 2026-09-28: stood "about three minutes, most of it the driver compiling the rocket
>   kernel once per architecture". The second audit's warm-up measurement (finding, "The
>   first audit's fixes", observation 3) found the driver's compute-cache JIT for a fresh
>   engine takes 55–59 s on a cache miss and the rocket kernel itself 13–22 s to compile,
>   and the architecture fact creates several engines and compiles every entry point for
>   eleven architectures: the reference machine measured 8 m 2 s on Windows and 11 m 3 s
>   under WSL, not about three minutes. The floor the fact needs stays unmeasured; this
>   is a corrected duration, not a new bound.

---

<a id="special-inputs-table-2026-09-27"></a>

## 2026-10-01 — from "## Acceptance criteria" — criterion: the probe's input domain, the special inputs' table

Moved because `BOOT.md` is over the §15 line limit for its kind of node; what it said stays at the pointer. The text as it stood:

>         `TheSpecialInputsAreRecordedAgainstCuda`'s own output, in full:
>
>         | Input | Exp | Log | Log10 | Pow(1.37) | Pow(1.4) | Pow(4.6) | Sqrt | Abs | Min | Max | Floor | Ceiling |
>         |---|---|---|---|---|---|---|---|---|---|---|---|---|
>         | 1, 0.5, 1.5, 2, 2.5, 1e-300, −0.5, −1.5, −2.5, 1e-300 | 0 | 0 | 0 | 0 or 1* | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 0 |
>         | 0, −0 | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 0 |
>         | +∞ | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 0 |
>         | −∞ | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 0 |
>         | NaN | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 0 |
>         | smallest/largest subnormal, smallest normal | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 0 |
>
>         \* `Pow(1.37)(0.5)`: cpu `0.38689124838559746`, cuda `0.3868912483855974`, 1
>         ULP — inside `GpuCpuTolerances.MathUlp` (4), a libdevice call already covered
>         by `CudaMatchesTheCpuAcceleratorWithinTheUlpBoundForEveryFunction`'s wider
>         domain; not a `Min`/`Max` finding. Every `Min`/`Max` row is 0 ULP throughout,
>         NaN included, so the audit's suspicion (`.NET`'s `Math.Max(NaN, x)` is NaN
>         while PTX `max.f64` returns `x`) is confirmed for `System.Math.Min`/`Max` and
>         closed by routing the probe, and every numerical node, through `KernelMath`.

---

<a id="hosted-runners-still-due-2026-09-27"></a>

## 2026-10-01 — from "## Acceptance criteria" — criterion: the post-link on every architecture, the hosted runners

Moved because `BOOT.md` is over the §15 line limit for its kind of node; what it said stays at the pointer. The text as it stood:

>       ⚠ 2026-09-26 to 2026-09-27: until that run this paragraph read "Still due: the
>       hosted runners of both platforms", with the tick standing for the reference
>       machine only. The coder had ticked the whole criterion with the Linux half argued
>       rather than run, and said so in its report; the orchestrator held it open at the
>       merge until a run existed.

---

<a id="probe-domain-audit-suspicion-2026-09-27"></a>

## 2026-10-01 — from "## Constraints" — constraints: the probe's domain, the audit's suspicion and the instruction to the coder

Moved because `BOOT.md` is over the §15 line limit for its kind of node; what it said stays at the pointer. The text as it stood:

>   - Suspected by the audit, not run: `.NET`'s `Math.Max(NaN, x)` is NaN, while PTX
>     `max.f64` returns the other operand. The coder records what CUDA returns for every
>     special input. If CUDA and the CPU accelerator differ on any, the coder stops and
>     reports the list; the decision on it is the owner's, since it bears on the root's
>     GPU-equals-CPU invariant. No tolerance is widened, and no input is dropped to pass.

---

<a id="audit-f2-f3-evidence-2026-09-26"></a>

## 2026-10-01 — from "## Acceptance criteria" — criterion: the audit's F2, F3, F4 and observations, evidence

Moved because `BOOT.md` is over the §15 line limit for its kind of node; what it said stays at the pointer. The text as it stood:

>       Evidence, on the reference machine, from a tree with every `bin` and `obj`
>       removed: `dotnet build APThermo.sln` 0 warnings, 0 errors;
>       `APTHERMO_NO_CUDA=1 dotnet test APThermo.sln --no-build --filter
>       "Category!=LongRunning"` 3191 total, 3190 passed, 0 skipped (the one failure is
>       `Protocol.Tests.DeclarationTests.EveryDeclarationUnderATickExists` on
>       `src/Performance/API.md`, outside this node's subtree and predating this change);
>       `dotnet test tests/Execution.Tests -c Release` (no filter) 140 of 140 (134 before
>       plus the six facts named above), the 100 000-case sweep and the throughput
>       tripwire included; no `Bits*.approved.txt`, `Throughput*.approved.txt` or the
>       protocol tests node's `PublicSurface.approved.txt` changed; the protocol lint
>       0 errors, 0 warnings. The red-once messages are recorded in `Execution`'s own
>       criterion, alongside the one fact (the upload-disposal fix) that has no dedicated
>       reproduction and is verified by inspection instead, as that criterion says.

---

<a id="architecture-red-once-2026-09-26"></a>

## 2026-10-01 — from "## Acceptance criteria" — criterion: the post-link on every architecture, red once

Moved because `BOOT.md` is over the §15 line limit for its kind of node; what it said stays at the pointer. The text as it stood:

>       Red-once proofs, both reverted before committing:
>       - `WrapperInventoryTests` against the pre-fix `WrapperCall` regex
>         (`__ilgpu__nv_[A-Za-z0-9_]+`, no `call`-site or comma requirement,
>         `WrappersCalled` reading the whole match instead of a capture group): 3 of 5
>         facts failed, `OnSm89EveryCalledWrapperIsAlreadyDefined` and
>         `BothArchitecturesCallTheSameWrappers` with "Assert.Equal() Failure: HashSets
>         differ … Expected: [\"__nv_exp\", \"__nv_exp_param_0\", \"__nv_log\", …] …
>         Actual: [\"__nv_exp\", \"__nv_log\", \"__nv_log10\", …]" and
>         `NoParameterNameIsReadAsACall` with "Assert.DoesNotContain() Failure: Filter
>         matched in collection … Collection: [\"__nv_exp\", \"__nv_exp_param_0\", …]".
>       - `ArchitectureTests`' algorithm, reproduced directly against `LibDevicePostLink`
>         as it stood at `9c33398` (a throwaway repro, not committed, compiling
>         `Kernels.Probe` for SM_75, SM_80, SM_86, SM_89 and SM_90 and calling the old
>         `Link`): every one threw `InvalidOperationException`, "the kernel calls the
>         libdevice wrapper __nv_exp_param_0, for which ILGPU 1.5.3.0 has no fragment.",
>         the message `Execution`'s criterion predicted.

---

<a id="architecture-evidence-2026-09-26"></a>

## 2026-10-01 — from "## Acceptance criteria" — criterion: the post-link on every architecture, evidence

Moved because `BOOT.md` is over the §15 line limit for its kind of node; what it said stays at the pointer. The text as it stood:

>       Evidence, on the reference machine (Windows, RTX 5070 Ti, driver 13.4, CUDA
>       toolkits 12.9/13.3/13.4), from a tree with every `bin` and `obj` removed:
>       - `dotnet build APThermo.sln`: 0 warnings, 0 errors;
>       - `APTHERMO_NO_CUDA=1 dotnet test APThermo.sln --no-build --filter
>         "Category!=LongRunning"`: 3185 of 3185, none skipped (3178 before this change
>         plus `WrapperInventoryTests`' 5 facts and the two new
>         `AcceleratorChoiceTests` bind-time facts; `ArchitectureTests`' one fact is
>         `Category=LongRunning` and so excluded here);
>       - `dotnet test tests/Execution.Tests -c Release` (no filter): 134 of 134, the
>         100 000-case sweep and the throughput tripwire included;
>       - no `Bits*.approved.txt`, `Throughput*.approved.txt` or the protocol tests
>         node's `PublicSurface.approved.txt` differs from `8dfe20f`;
>       - the protocol lint: 0 errors, 0 warnings;
>       - the protocol tests node's `ShapeTests`: 10 of 10 (the extraction of
>         `KernelCache.Load` to a static method and `AcceleratorChoice`'s new
>         `ProbeBinding` moved no type past its coupling or size limit).
>
>       `WrapperInventoryTests` is pure text and regex over the two committed fixtures,
>       with no OS-conditional code and no native call. The architecture and bind-time
>       facts are `Category=Cuda` and run only where a device exists, as every other CUDA
>       fact of this node does.

---

<a id="throughput-rerecord-red-once-2026-09-19"></a>

## 2026-10-01 — from "## Acceptance criteria" — criterion: the throughput re-measurement, red once and the runs

Moved because `BOOT.md` is over the §15 line limit for its kind of node; what it said stays at the pointer. The text as it stood:

>       - Shown red once on both platforms: the fact run in Debug (`dotnet test
>         tests/Execution.Tests --filter
>         "FullyQualifiedName~ThroughputIsRecordedAndNotBelowTheApprovedRatio"`)
>         against its platform's freshly re-approved, Release-measured file failed with
>         the message quoted in the ⚠ above, naming `Throughput.approved.txt` on Windows
>         and `Throughput.linux.approved.txt` on Linux.
>       - `protocol_lint` 0 errors, 0 warnings on both platforms both before and after;
>         the Windows fast suite (`APTHERMO_NO_CUDA=1 dotnet test APThermo.sln --filter
>         "Category!=LongRunning"`) 3101/3101 throughout, none skipped; every
>         `Bits*.approved.txt` and the protocol tests node's `PublicSurface.approved.txt`
>         unchanged.

---

<a id="throughput-build-configuration-2026-09-19"></a>

## 2026-10-01 — from "## Invariants" — the throughput tripwire: one build configuration

Moved because `BOOT.md` is over the §15 line limit for its kind of node; what it said stays at the pointer. The text as it stood:

>   ⚠ 2026-09-19: the approved files were Debug measurements (`dotnet test` without
>   `-c`) compared against a Release run, the configuration the release workflow and the
>   benchmarks node use. The release rehearsal (35439111934) failed the fact at 29.48×
>   against 80 % of the approved 56.28×, with no code regression: every other Cuda and
>   BitSnapshot fact was green (Fable 5.1's analysis of the run's tables). The mechanism:
>   the CPU accelerator executes the batch's kernels from the assemblies' IL — ILGPU's
>   CPU accelerator, not a native `System.Math` call path — so the host build
>   configuration changes its speed by roughly 2.8× (Debug ≈9.5 s, Release ≈3.4–3.7 s on
>   Windows for the 100 000-case sweep, the benchmarks node's 2026-09-15 figures); the
>   CUDA kernel is compiled once through libnvvm regardless of the host configuration, so
>   its own time (≈0.15–0.2 s) does not move. A Debug-vs-Release comparison therefore
>   compares two different CPU speeds under one name. `BuildConfiguration.Current`
>   (`#if DEBUG`) names the running configuration; the approved file now carries a
>   `configuration:` line, and the fact refuses to compare across configurations, naming
>   both in its message. Shown red once on each platform: the fact run in Debug against
>   the Release-approved file failed — "this run is Debug, but Throughput.approved.txt
>   was measured in Release; the CPU accelerator executes the kernels from the
>   assemblies' IL, so its speed depends on the build configuration (BOOT.md); run in
>   Release to compare against it" on Windows, the Linux run naming
>   `Throughput.linux.approved.txt` the same way. Both files are re-approved from Release
>   runs (the acceptance criterion below); the 80 % and the root's 5× floors are
>   unchanged. `SweepRun` also times each side as the median of three timed runs after
>   the warm-up instead of one, and repeats the CUDA warm-up five times (the GPU leaves
>   its idle P-state over several launches, not one), so a single slow or fast sample
>   does not move the tripwire; the CUDA determinism check still gets two independent
>   runs.

---

<a id="criterion-discovery-tried-empty-2026-09-17"></a>

## 2026-10-01 — from "## Acceptance criteria" — criterion: DiscoveryReportsTheToolkitPathsItExamined on a runner with no toolkit

Moved because `BOOT.md` is over the §15 line limit for its kind of node; what it said stays at the pointer. The text as it stood:

> - [x] 2026-09-17 — `DiscoveryReportsTheToolkitPathsItExamined` assumed every
>       machine offers the locator at least one candidate root, so `Assert.NotEmpty(tried)`
>       held unconditionally. The first CI run on the public repository (GitHub Actions
>       run 35258686217) failed it on `windows-latest`: no CUDA toolkit, no `CUDA_PATH`,
>       so `LibDeviceLocator.Locate` truly examined nothing and `tried` was empty; the
>       `ubuntu-latest` job passed only because its discovery unconditionally names the
>       fixed `<glob root>/cuda` candidate before checking whether it exists (root
>       `BOOT.md`'s Linux `ToolkitRoots`), so `tried` is never empty there. An empty list
>       is the honest answer on a bare Windows runner, not a defect of discovery, so the
>       fact now asserts only what holds everywhere: every path ever tried has the
>       platform's own library file name or the `.bc` suffix (`Assert.All`, unconditional,
>       still fails on a wrong-shape path), and `Assert.NotEmpty` applies only when a
>       candidate root exists — `CUDA_PATH` set, or (Windows) a versioned directory
>       already under the default toolkit base; Linux always has the fixed candidate, so
>       the new helper `ACandidateToolkitRootExists` returns `true` unconditionally there.
>       Shown red once: `LibDeviceLocator.Locate`'s internal `tried` list was seeded with
>       a bogus `"MUTATION-wrong-shape.txt"` entry before its early-return checks; `dotnet
>       test tests/Execution.Tests --filter
>       "FullyQualifiedName~DiscoveryReportsTheToolkitPathsItExamined"` failed —
>       `Assert.All() Failure: 1 out of 4 items in the collection did not pass` naming the
>       bogus entry — then the mutation was reverted and the file diffed byte-identical
>       against the pre-mutation copy. Reproduced the CI condition on the reference
>       machine (which has the toolkit, so the environment-driven fact itself cannot be
>       driven empty) through the internal seam instead: `LibDeviceLocator.Locate(new
>       EngineOptions(), LocatorPlatform.Windows, _ => null, @"C:\nonexistent-ci-toolkit-base")`
>       returned `(null, null, [])`, matching the CI failure exactly; with `CUDA_PATH` and
>       `CUDA_HOME` cleared but the real toolkit base left in place (`C:\Program
>       Files\NVIDIA GPU Computing Toolkit\CUDA`, versions v12.9/v13.3/v13.4) the same
>       overload still found `v13.4`'s dll and bitcode, `tried` non-empty, confirming
>       discovery falls back to the directory scan when only the environment variable is
>       missing. `LibDeviceDiscoveryTests.WindowsWithNoCudaPathAndNoToolkitBaseDirectoryExaminesNothing`
>       pins the same empty-tried case deterministically, alongside the existing
>       `AnUnsupportedPlatformDoesNoDiscovery`. Verified on the reference machine at
>       `a0d0ebf` (which has `CUDA_PATH` set, so the environment fact's non-empty branch
>       is exercised for real): `dotnet test tests/Execution.Tests` (CUDA included, the
>       new fact among them) 56/56; `APTHERMO_NO_CUDA=1 dotnet test APThermo.sln --filter
>       "Category!=LongRunning"` 3101/3101, none skipped; `protocol_lint` 0 errors, 0
>       warnings; every `Bits*.approved.txt`, `Throughput*.approved.txt` and the
>       protocol tests node's `PublicSurface.approved.txt` unchanged (`git status
>       --short` names only the three files this fix touched).

---

<a id="criterion-linux-throughput-file-2026-09-17"></a>

## 2026-10-01 — from "## Acceptance criteria" — criterion: the first Linux throughput record

Moved because `BOOT.md` is over the §15 line limit for its kind of node; what it said stays at the pointer. The text as it stood:

> - [x] 2026-09-17 — `Throughput.linux.approved.txt` recorded from a green run under
>       WSL2 on the reference machine (.NET SDK 10.0.112, this node's harness change on
>       top of `df0368d`): RTX 5070 Ti, 100 000 cases, 4 stations, 11 species, CUDA
>       0.237 s, CPU accelerator 12.350 s with 16 threads, 52.01×, comfortably above the
>       root's 5× floor though below the Windows file's 56.28× (WSL2's virtualization
>       overhead falls on both the CPU and the CUDA timings, per the tripwire
>       invariant's ⚠ above). Before approving, the rest of the same `dotnet test
>       tests/Execution.Tests` run was confirmed to need nothing else: 54/55, the one
>       failure the expected "no approved throughput file" case, the 100 000-case
>       correctness sweep (`TheSweepOf100000CasesOnCudaMatchesTheCpuAcceleratorAndIsDeterministic`)
>       already green in it. With the file in place, the same command gave 55/55; the
>       fast suite (`APTHERMO_NO_CUDA=1 dotnet test APThermo.sln --filter
>       "Category!=LongRunning"`) stayed 3098/3098 and `protocol_lint` gave 0 errors,
>       0 warnings, both unaffected by this node's own change.

---

<a id="throughput-file-per-platform-2026-09-17"></a>

## 2026-10-01 — from "## Invariants" — the throughput tripwire: one approved file per platform

Moved because `BOOT.md` is over the §15 line limit for its kind of node; what it said stays at the pointer. The text as it stood:

>   ⚠ 2026-09-17: this bullet assumed one approved file. The root's platform constraint
>   keeps a Windows and a Linux record for the bit snapshots (`Harness`'s `BOOT.md`), and
>   the same reasoning applies here: a benchmark run under WSL2 times a CPU accelerator
>   and a CUDA path that both include the hypervisor's virtualization overhead, so its
>   ratio is not comparable to a native Windows run. `ApprovedPathFor` picks
>   `Throughput.approved.txt` or `Throughput.linux.approved.txt` for the running
>   platform, the same one place `Bits.approved.txt`'s per-node counterpart uses; the
>   actual file is written beside whichever one is read, so `Throughput.linux.actual.txt`
>   on Linux. The root's 5× floor is not a per-platform figure and applies to both.

---

<a id="criterion-gpu-cpu-comparison-extracted-2026-09-15"></a>

## 2026-10-01 — from "## Acceptance criteria" — criterion: the GPU/CPU comparison logic extracted

Moved because `BOOT.md` is over the §15 line limit for its kind of node; what it said stays at the pointer. The text as it stood:

> - [x] 2026-09-15 — The GPU/CPU comparison logic `CudaTests.cs` carried alongside its `[Fact]`/
>       `[Theory]` methods — `MoleSample`, `CompareRocket`, `CompareMoles`, `Record`,
>       `Worst` — moved to a new file, `GpuCpuComparison.cs`: one stateful type,
>       `GpuCpuComparison`, built from the tolerance table once per test and holding the
>       worst deviation per field and the different-step count as it accumulates them,
>       with `Rocket(RocketBatchResult, RocketBatchResult, RocketFamily)` (was
>       `CompareRocket`, 3 parameters), `Moles(double[], double[], long, SpeciesTable,
>       bool, string)` (was `CompareMoles`, 6 parameters, its four `MoleSample` fields
>       unpacked back to plain parameters — `MoleSample` had one caller-supplied field
>       per call and was never kept, so it named no concept of its own), `Record` (the
>       `GpuCpuTolerances.Compare` callback), a new `CountSteps(bool)` and `Worst()` as
>       its methods. `MoleSample` is gone. The three CUDA test methods that owned a
>       `worst` dictionary, and either a `differentSteps` local incremented inline
>       (the equilibrium family test) or passed by `ref` into `CompareRocket` (the
>       rocket family and sweep tests), now own one `GpuCpuComparison` instead, call
>       `.CountSteps(sameSteps)` where they used to increment their own local, and read
>       `.DifferentSteps` back: the equilibrium test's step count moved from a local
>       variable to the same shared counter `Rocket` itself feeds, so all three tests
>       now count steps the same way. No behaviour change: the same comparisons, the
>       same tolerance calls, the same accumulation — `worst` shared across a test
>       method's rocket-then-transport phases
>       (`ARocketFamilyOnCudaMatchesTheCpuAccelerator`) is still one dictionary
>       shared the same way, now the one instance's private field instead of a local
>       passed to both phases. `ShapeTests.NoTypeSpansMoreThan400Lines` and
>       `ShapeTests.NoMethodSpansMoreThan60Lines` both hold for `CudaTests` and
>       `GpuCpuComparison`; `GpuCpuComparison`'s own Ce is 8, `CudaTests`' own Ce is 26,
>       unchanged from before the cut, both recorded by `CouplingMeasures` and not
>       limited, since the root's coupling rule holds for `src` types only.
>
>       This is a mechanical port: `Rocket`/`Moles`/`CountSteps` cannot be exercised
>       without a CUDA device, so the CPU-only fast suite (`APTHERMO_NO_CUDA=1`, which
>       makes `RequireCuda()` return null and every CUDA-marked test return before
>       reaching this code) proves only that it builds and that every other fact stays
>       green; the actual arithmetic is unchanged from the moved code, read side by
>       side at the move. Applies R-Execution.Tests-2 of the repair review.
>
>       Verified on the reference machine, `APTHERMO_NO_CUDA` unset, one run at a
>       time: `ProbeKernelTests.CudaMatchesTheCpuAcceleratorWithinTheUlpBoundForEveryFunction`
>       green (R-Execution-2's own guard, exercising the merged `CompileWrappers`);
>       `CudaTests.ARocketFamilyOnCudaMatchesTheCpuAccelerator` and
>       `AnEquilibriumFamilyOnCudaMatchesTheCpuAccelerator` together, 9 of 9
>       green (every rocket family plus the equilibrium family, through `Rocket`,
>       `Moles`, `CountSteps` and `Record` on real hardware); then
>       `TheSweepOf100000CasesOnCudaMatchesTheCpuAcceleratorAndIsDeterministic`
>       alone, green (400 000 stations through `Rocket`, both accelerators agreeing
>       within the tolerance table, CUDA deterministic across two runs).
>       `ThroughputIsRecordedAndNotBelowTheApprovedRatio` was not run, as the
>       decision records.

---

<a id="chemical-system-forwarding-properties-2026-09-15"></a>

## 2026-10-01 — from "## Acceptance criteria" — criterion: ChemicalSystem and the forwarding properties

Moved because `BOOT.md` is over the §15 line limit for its kind of node; what it said stays at the pointer. The text as it stood:

>       ⚠ 2026-09-15: "the old field names stay as forwarding properties, so every read
>       call site is unchanged" did not age well, for the same reason the equivalent
>       shortcut in the Performance.Tests node did not: `ChemicalSystem` mixed a
>       family's shared axis with one fixture's own. `RocketFamilies` groups fixtures by
>       `BatchKey`, and `BatchKey` never read `ElementMoles` — only `Elements`,
>       `Products` and the exit kinds — confirming `ElementMoles` was never part of what
>       a family shares; it is one fixture's own starting composition, exactly like
>       `ReactantEnthalpy`, which already sat outside `ChemicalSystem`. Found by the
>       repair review (R-Execution.Tests-1). `ChemicalSystem` narrowed to `Elements`,
>       `Products` (2 parameters); a new `Mixture` record holds `ElementMoles` and
>       `ReactantEnthalpy` (2 parameters); `RocketInputs` keeps `System`, `Mixture`,
>       `ChamberPressure`, `Flow`, `Exits`, `Transport`, still 6 parameters. The
>       forwarding properties (`Elements`, `ElementMoles`, `Products`, `ExitValues`,
>       `ExitKinds`) are gone; the four read call sites this document said were
>       unchanged (`RocketFamily.Batch`, `RocketFamilies`, `Sweep`, all in
>       `FixtureBatches.cs`) and the fifth this document did not mention
>       (`AcceleratorChoiceTests.InconsistentBatchesAreRefusedBeforeAnyKernelRuns`)
>       all name `.System.` or `.Mixture.` or `.Exits.` directly now. No behaviour
>       changed: the same fields, on the same two records, under new names one level
>       down.
>
>       By the same `CouplingMeasures` run, `FixtureBatches` itself moved from Ce=14 to
>       Ce=17 (`ChemicalSystem`, `Mixture` and `ExitPlan` newly named directly in
>       `RocketFamilies` and `Sweep`, for the same reason as `RocketCase` in the
>       Performance.Tests node); its test-fixture neighbours measure
>       `AcceleratorChoiceTests` Ce=32, `BatchTests` Ce=31, `CudaTests` Ce=26,
>       `HostSolves` Ce=28, `SpeciesFunctionTests` Ce=16, none touched by this cut. The
>       root limits the efferent coupling of the `src` types only, so a test type's
>       figure is recorded, not limited.
>
>       Verified: build clean, 0 warnings; 41 of 41 fast tests green; `protocol_lint`
>       0 errors, 0 warnings.

---

<a id="nesting-fixes-evidence-2026-09-15"></a>

## 2026-10-01 — from "## Acceptance criteria" — criterion: the two nesting-depth-4 fixes and their verification

Moved because `BOOT.md` is over the §15 line limit for its kind of node; what it said stays at the pointer. The text as it stood:

>       Two nesting-depth-4 violations found by the same review were fixed alongside:
>       `BatchTests.ARocketFamilyEqualsTheHostSolverBitForBit`'s
>       species-by-species mole loop, four levels deep inside the case loop, the
>       station loop and its own species loop, moved to `StationMoleDifferences`
>       (nesting 2 on its own); `SpeciesFunctionTests.CudaMatchesTheCpuAcceleratorWithinTheTable`'s
>       three-function comparison, four levels deep inside the family loop, the entry
>       loop and its own function loop, moved to `CompareFunctions` (nesting 2 on its
>       own). Neither method nests deeper than 3 now.
>
>       Verified: build clean, 0 warnings; 41 of 41 fast tests green
>       (`APThermo.Execution.Tests.dll`); `protocol_lint`
>       0 errors, 0 warnings; `Protocol.Tests` 9 of 9 green. This node keeps no
>       `Bits.approved.txt` of its own (its bit comparisons run the host call inside
>       the same test, not against a recorded snapshot), so there is no hash to
>       compare before and after.

---

<a id="mole-sample-moved-2026-09-15"></a>

## 2026-10-01 — from "## Acceptance criteria" — criterion: MoleSample and CompareRocket moved

Moved because `BOOT.md` is over the §15 line limit for its kind of node; what it said stays at the pointer. The text as it stood:

>       ⚠ 2026-09-15: `MoleSample` is no longer local to `CudaTests.cs`, and
>       `CompareRocket` is gone: both moved to `GpuCpuComparison.cs` the same day, the
>       criterion below.

---

<a id="nesting-depth-not-measured-2026-09-15"></a>

## 2026-10-01 — from "## Acceptance criteria" — criterion: nesting depth was not measured

Moved because `BOOT.md` is over the §15 line limit for its kind of node; what it said stays at the pointer. The text as it stood:

>       ⚠ 2026-09-15: "nested deeper than 3" was not measured: `inventory.py` counts
>       lines only, and `BatchTests.ARocketFamilyEqualsTheHostSolverBitForBit`
>       and `SpeciesFunctionTests.CudaMatchesTheCpuAcceleratorWithinTheTable`
>       nested 4 deep at this tick's commit (`7a3dedb`). Found by the repair review
>       (R-Execution.Tests-4); both were brought to 3 on 2026-09-15 (the criterion
>       below), where this document's earlier silence on the point is corrected.

---

<a id="criterion-support-code-in-shape-2026-09-14"></a>

## 2026-10-01 — from "## Acceptance criteria" — criterion: the support code in shape

Moved because `BOOT.md` is over the §15 line limit for its kind of node; what it said stays at the pointer. The text as it stood:

> - [x] 2026-09-14 — The support code in shape (the test review's F-TF-06 and F-TF-13):
>       `BatchBuilders` is gone, replaced by `FixtureBatches.cs` (`RocketInputs`,
>       `RocketFamily`, fixtures to families and batches), `HostSolves.cs` (one case
>       through the numerical nodes over the accelerator's own buffers, returning the
>       named record structs `HostRocketCase`, `HostEquilibriumCase`,
>       `HostTransportStation` instead of tuples), `BitEquality.cs` (`SameBits`,
>       `BitDifferences<T>`) and `SweepRun.cs` (the long-running sweep, not named by
>       F-TF-06 but sharing none of the three axes above); no method over 60 lines or
>       nested deeper than 3, covered by the protocol tests node's `ShapeTests`, all
>       ten facts green at `62cd99e`; every L2 fact green bit for bit after the split
>       (`APThermo.Execution.Tests.dll`: 41 passed) and the
>       node's mutations re-run alone and seen red where the touched code moved: the
>       rocket kernel's chamber pressure perturbed by a relative `1e-12`
>       (`batch.ChamberPressures[index] * (1.0 + 1e-12)` in `Kernels.Rocket`) reddened
>       `ARocketFamilyEqualsTheHostSolverBitForBit` for every family, and the
>       chunk bound with its memory clamp removed from `ChunkPlan.For` reddened
>       `ChunksAreBoundedByTheChunkSizeAndTheScratchMemory`; both reverted
>       and the suite green again before committing. The hand-typed fact counts left
>       the criteria above; the listed names are the list.

---

<a id="tolerance-table-not-in-one-file-2026-09-14"></a>

## 2026-10-01 — from "## Invariants" — the tolerance table: not in one file

Moved because `BOOT.md` is over the §15 line limit for its kind of node; what it said stays at the pointer. The text as it stood:

>   ⚠ 2026-09-14: "in one file" was not true: the front door tests node copied the
>   mole-fraction floor (1e-8) and the polish-threshold tier (1e-9) for its reordered
>   union batches, and the protocol forbids it to read this node's code. Found by the
>   clean-code review (F-TF-05). Resolved the same day: the two entries moved to the
>   fixtures node's tolerance table, which both nodes already depend on
>   (`tests/Fixtures/tolerances.json`, `moleFractionFloor` and
>   `polishThresholdRelative`, with their derivations); `GpuCpuTolerances.MoleFractionFloor`
>   and `MoleFractionRelative` now take that table and read the two entries from it, and
>   this node's own table keeps only the GPU-specific entries (temperature, moleFraction,
>   state, figures, transport, functions) that have no place in a table of comparisons
>   with the reference.

---

<a id="tolerance-two-tiers-2026-09-12"></a>

## 2026-10-01 — from "## Invariants" — the tolerance table: the second tier for mole fractions

Moved because `BOOT.md` is over the §15 line limit for its kind of node; what it said stays at the pointer. The text as it stood:

>   ⚠ 2026-09-12: the sketch had one tier, 1e-10 on every mole fraction above the
>   floor. The 100 000-case sweep showed 12 mole fractions at 2 of its 400 000
>   stations beyond it, by up to 3.3e-10, both at stations where CUDA had taken 3
>   Newton steps and the CPU accelerator 2 or the reverse. The equilibrium solver
>   polishes until its corrections are below 1e-11, the rounding floor of its linear
>   solves; a last-ULP difference between libdevice and .NET flips that threshold at
>   91 stations of the sweep, and the accelerator that takes one polish step more
>   moves by up to 1.4e-11 in temperature and, through `(H_j/RT) Δln T + Σ a_ij Δπ_i`,
>   by a few 1e-10 in the mole fraction of a minor species. Where the step counts
>   agree the worst deviations are 3.4e-13 on temperature, 9e-12 on a mole fraction
>   and 2e-12 on any other field. The second tier is derived from the polish
>   threshold, not from the measurement; the share of such stations is bounded so
>   that a systematic divergence (a single-precision or CORDIC function would flip
>   the count everywhere) cannot hide behind the second tier. The root's invariant
>   carries the same note.

---

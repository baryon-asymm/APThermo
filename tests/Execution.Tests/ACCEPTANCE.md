# ACCEPTANCE.md — tests/Execution.Tests

- [x] 2026-11-16 — The throughput fact explicitly verifies cold/hit/hit after writing its actual
  record, and rejects cold/hit/cold and cold/cold/hit with the affected sample and
  calibration reason/age before applying the unchanged original comparisons.
  (2026-11-16: `SweepModeTests` host controls and both actual c0 GPU records in
  [Release run 43861338174](https://github.com/baryon-asymm/APThermo/actions/runs/43861338174).)

- [x] The CPU bypass test compiles its kernel before seeding the real session cache;
  omitting bypass invalidation fails that test while the intact implementation passes.
  (2026-11-16: `CalibrationObservationTests.CpuRunInvalidatesAnOlderTransactionInItsActualSessionCache`.)
  ⚠ 2026-11-16: the previous control compiled after seeding and did not isolate the
  actual bypass. The corrected fact asserts the first compilation, a published entry in the same engine session, zero
  warm-up on the next run and invalidation of that transaction. The fresh-engine
  `CpuRunDoesNotMasqueradeAsACudaTransaction` remains separate. With only
  `BatchRun.Execute`'s bypass `Clear` removed, the corrected test failed (expected
  `Invalidated`, actual `Published`) and 12 other observation controls passed. After
  byte-exact restoration and rebuilding, 61 calibration host controls passed, no skips.
  Evidence: test names and `fix-evidence/cache-z5/z5-mutant.trx`, `fix-evidence/cache-z5/z5-final.trx`
  in the external audit archive; this is CPU evidence, not a CUDA run.
- [x] 2026-11-16 — CUDA refusal and actual-device paths carry explicit evidence, and the GPU job
  rejects refusal-only results; no permanently skipped test is introduced.
  (2026-11-16: `CudaEvidenceTests`, actual 451-identity GPU inventories and marker
  verification on both OSes in [Release run 43861338174](https://github.com/baryon-asymm/APThermo/actions/runs/43861338174).)

### CUDA evidence CPU controls — 2026-11-16

`CudaEvidenceTests` and the CUDA category with CUDA forbidden pass 94/94. TRX contains
one structured marker per CUDA case: 86 verified refusals, four driver-only cases and
one hardware case explicitly not run. The test's sorted method inventory is generated
from discovery traits. Compiling premature-refusal and false-GPU driver mutations fail.
External `fix-evidence/cuda-z12/index.json`. Both actual c0 CUDA jobs and the GPU
identity/marker gate pass on their reference device.
⚠ 2026-11-16: was “Actual CUDA and GPU-gate validation remain open”; now both actual
OS jobs pass with source-bound evidence in Release run 43861338174.

- [x] 2026-11-16 — `ColdThroughputTests` and `ThroughputRecordTests` pass 55/55 on
  CPU. Frozen control commit `e7112d46` compiles; with its no-guard stub, six
  cold-mode or missing-evidence cases fail while the known-answer positive passes.
  `cold-red.trx` SHA-256 D10C0EF01A558AF3A57835BA2B496AB90D0BBC19D641DB9604B38A7904868E73;
  `cold-green.trx` SHA-256 07506B8DB53BB83DD684F63651F7FC40F62892E04CB09858162D139C18743882.
  These host controls are not CUDA evidence; actual c0 device proof is separate below.
  ⚠ 2026-11-16: the complete previous criterion was:
  2026-11-16 — `ColdThroughputTests` and `ThroughputRecordTests` pass 55/55 on   CPU. Frozen control
  commit `e7112d46` compiles; with its no-guard stub, six   cold-mode or missing-evidence cases fail
  while the known-answer positive passes.   `cold-red.trx` SHA-256
  D10C0EF01A558AF3A57835BA2B496AB90D0BBC19D641DB9604B38A7904868E73;   `cold-green.trx` SHA-256
  07506B8DB53BB83DD684F63651F7FC40F62892E04CB09858162D139C18743882.   These host controls are not CUDA
  evidence; hardware acceptance remains open below.
  Its pending hardware statement was the pre-c0 handoff; the later c0 device
  proof is separate from these CPU controls and is cited below.

- [x] 2026-11-16 — The separate first-call cold throughput guard and its positive and
  compiling negative controls pass, and the unmodified original throughput, numerical
  and bit gates pass on the actual Windows and Linux reference CUDA runners at the
  integrated source. Source-bound receipts must identify the hardware and run.
  (2026-11-16: `ColdThroughputTests` red/green and both actual RTX 5070 Ti runs in
  [Release run 43861338174](https://github.com/baryon-asymm/APThermo/actions/runs/43861338174); Windows/Linux warm medians 1.342e-7/1.272e-7 and cold samples
  2.681e-7/2.686e-7 s per step pass their limits; ratios 30.46/25.98 pass original
  gates. Both are cold/hit/hit and cover the original 100,000 cases.)

- [x] 2026-11-16 — Recovery condensed-slot actual-CUDA evidence: `RecoverySlotsCudaTests.PhysicalRecoverySlotsOnActualCudaMatchCpuForColdAndSeededSolves` requires the actual-device marker and compares slots, moles, multipliers, every state field, status and iterations against the CPU kernel. The CPU companion `PhysicalRecoverySlotsOnCpuKernelAgreeWithTheWinningComposition` passed 2026-11-16; removing the bank-to-live slot restore compiled and failed that companion with positive species [38, 40] but live slots [38].
  (2026-11-16: `PhysicalRecoverySlotsOnActualCudaMatchCpuForColdAndSeededSolves`
  passes with actual-device markers on both platforms in [Release run 43861338174](https://github.com/baryon-asymm/APThermo/actions/runs/43861338174).)
  ⚠ 2026-11-16: the prior criterion stated:
  Recovery condensed-slot actual-CUDA evidence:
  `RecoverySlotsCudaTests.PhysicalRecoverySlotsOnActualCudaMatchCpuForColdAndSeededSolves` requires
  the actual-device marker and compares slots, moles, multipliers, every state field, status and
  iterations against the CPU kernel. The hardware run remains pending. The CPU companion
  `PhysicalRecoverySlotsOnCpuKernelAgreeWithTheWinningComposition` passed 2026-11-16; removing the
  bank-to-live slot restore compiled and failed that companion with positive species [38, 40] but live
  slots [38].
  The prior pending hardware claim is superseded by those exact-source device results;
  the earlier CPU and mutation evidence remains unchanged.

- [x] R10 decision observability: controlled histories and deliberate mutations
  prove accurate identity/presence, before/after-upload age/reason, generation and
  post-result publication outcomes without altered work, choices or cached timings.
  Actual original Linux throughput diagnostics retain all three current decisions
  and unchanged gates. This is diagnostic evidence; full performance acceptance
  stays separate and cannot be claimed from host controls or a discarded failure.
  (2026-11-15: source `a93074210a6c02646c2197b764334a83e221cefd` passes the
  one predeclared original Linux throughput fact with all three observations:
  identity-mismatch cold, then matched repeats aged 6.3453 and 7.3300 ms after
  upload. Original ratio 24.73 and per-step 1.326e-7 pass unchanged gates.
  The same 148 host identities pass Windows/Linux; compiling observation mutants
  are red and restored. External `fix-evidence/root/R10-calibration-observation-once/`
  `a93074210a6c02646c2197b764334a83e221cefd/root-diagnostic-summary-v1.json`
  retains source/binary hashes, raw TRX/stdout, fresh actual bytes and all outcomes.
  Full current Release acceptance remains pending. The old miss was not reproduced.)
  ⚠ 2026-11-16 (pre-c0 historical handoff): the pending statement above describes source `a9307421`; the later
  original Release run at `f21ac451` passed on both reference GPUs for that source.
  Its 2026-11-15 predecessor `1a8aca20` failed the Windows sweep: 449/450 facts,
  ratio 10.37 against the 14.816 minimum; an entry was observed expired after
  1.0437997 s. The cause of that interval is unknown. Re-audit fixes have since
  changed code; their own hardware acceptance remains open.

- [x] 2026-11-15 — CPU bypass with an older actual-session transaction:
  `CpuRunInvalidatesAnOlderTransactionInItsActualSessionCache` first establishes
  nonzero Published through the Engine accessor, then the real CPU Run reports
  Invalidated for the same transaction with increased current generation.
  Both fresh/seeded positives pass; omitting only observation invalidation leaves
  the fresh fact green and makes the seeded fact fail (Invalidated versus Published).
  `SCRATCH/audit/seeded-session-mutation-receipt.json` retains frozen/source hashes,
  the compiling mutation, assertion and byte-exact restoration to production 8bc.
  Corrected affected host set 148/148, Shape/Diagnostics 18/18 and Release build 0/0:
  `seeded-session-final-host`, `seeded-session-shape-diagnostics` and
  `seeded-session-final-build` logs/TRX in `SCRATCH/audit/`. No device claim is made.

- [x] 2026-11-15 — R10 host decision-observation controls: final affected host
  set passes 147/147, including the original lifecycle, weak-ownership, identity,
  prefix and current-computation facts plus controlled decision histories.
  `SCRATCH/audit/final-host.log`/`.trx`; five compiling diagnostic mutations fail
  precise assertions (7/3/10/6/5 failures out of 22 each), restored before final green;
  `behavioral-mutants.json` retains commands, source/control hashes and counters.
  Sweep retains exactly three value snapshots after its original elapsed assignments
  and prints them before unchanged gates. At that host-only handoff, actual Linux diagnosis remained open.
  ⚠ 2026-11-16: the full previous criterion was:
  2026-11-15 — R10 host decision-observation controls: final affected host   set passes 147/147,
  including the original lifecycle, weak-ownership, identity,   prefix and current-computation facts
  plus controlled decision histories.   `SCRATCH/audit/final-host.log`/`.trx`; five compiling
  diagnostic mutations fail   precise assertions (7/3/10/6/5 failures out of 22 each), restored before
  final green;   `behavioral-mutants.json` retains commands, source/control hashes and counters.
  Sweep retains exactly three value snapshots after its original elapsed assignments   and prints them
  before unchanged gates. Actual Linux diagnosis above remains open.
  Its final pending statement describes the earlier handoff; source a930 and
  the later c0 GPU results are separately attributed above.

⚠ 2026-11-15: was Linux diagnostics pending at handoff, now separately evidenced → HISTORY.md#r10-linux-handoff-2026-11-15

- [x] 2026-11-14 — Diagnostic budget override restoration: the CPU-only
  `DiagnosticOverrideRestoresTheOriginalBudgetOnFailure` control passed in
  `SCRATCH/audit/launch-plan-diagnostic-control.log`. The exact original session
  budget survives an injected callback failure. The opt-in CudaDiagnostic/LongRunning
  experiment is implemented/compiled, not hardware-verified here; it cannot
  satisfy the original throughput acceptance by disabling the bounded plan.

- [x] 2026-11-14 — R10 diagnostic guard: `CachedKernelDiagnosticsTests` and existing
  `ThroughputRecordTests` 13 passed in `SCRATCH/audit/r10-observability-controls.log`.
  A real cached CPU probe is described through public ILGPU APIs with zero new
  warm-up and unchanged cache count; an unrelated delegate reports unavailable.
  Synthetic PTX checks declaration labels, summary SHA/byte fields and no raw PTX
  output. Actual CUDA metadata/per-run logs require the root's later serial run;
  these diagnostics do not close the independently failed throughput gate.

- [x] 2026-11-14 — A15/A18: `PlanningBoundaryTests` and the platform driver-name table
  plus forbidden-CUDA and ordinary chunk controls: 20 passed; old source 10 failed/8 passed.
  Evidence: `SCRATCH/audit/planning-old-negative.log`, `planning-final.log`.
  No GPU executed; the actual module inventory uses the same Windows/Linux driver classifier.

- [x] 2026-11-14 — Conservative bounded sampling: `LaunchBudgetTests` host controls
  10 passed, including the real CPU batch-loop wiring, one-case start, 2x cap, immediate
  shrink, remainder and integer-maximum bounds. Old policy: four failed/one passed.
  Evidence: `SCRATCH/audit/sampling-old-negative.log`, `sampling-final.log`; no GPU run.

- [x] 2026-11-14 — T07: three `RocketCompileTests` passed with direct warm-up,
  cache and weak-launcher observations; allocation remains bounded by 7 GiB.
  Cache dictionary retention fails the launcher fact; retaining context IR fails
  the heap bound (476 490 640 bytes retained versus 119 129 548 allowed).
  Evidence: `SCRATCH/audit/compile-observations.log`, `cache-launcher-negative.log`,
  `cache-ir-negative.log`; mutations restored and final build 0 warnings/errors.

- [x] 2026-11-14 — A04 copy window: `InputCopyOwnershipTests` uses a real CPU copy
  fault and proves both actual allocation layers disposed. Restoring ILGPU allocate-and-copy
  makes both fail rollback. Execution targeted set 46 passed, Protocol Shape/Diagnostics
  18 passed, strict lint 0/0; evidence `SCRATCH/audit/probe-copy-negative.log`,
  `copy-window-execution-controls.log`, `copy-window-protocol.log`.

- [x] P1 frozen consumers: actual CPU/CUDA cold, seeded and rocket pairs are implemented
  in `AuditPhaseCudaTests`, with explicit selected-device provenance and a forbidden-CUDA
  refusal labeled as no hardware proof. The CPU companion passes after R08;
  actual Windows/WSL CUDA equality and the independent scientific obligations must pass.
  (2026-11-15: `AuditPhaseCudaTests.ActualCudaMatchesTheCpuOnEveryFrozenPhaseInput`
  passes both expanded rows on actual c0df Windows and Ubuntu-24.04 CUDA, including
  cold/seeded and rocket consumers. The five-result budget and full CUDA receipts
  record the selected device. Both CPU cohorts and independent phase/objective
  witnesses pass.  forbidden-CUDA CPU counts are not reported as hardware proof.)
  ⚠ 2026-11-15: was a pending handoff, now a hardware-proof requirement → HISTORY.md#p1-readiness-restatement-2026-11-15

⚠ 2026-11-14: was a red shared seeded CPU consumer, now warm candidate bank passes → HISTORY.md#p1-shared-consumer-red-2026-11-14

- [x] 2026-11-14 — T08: `TraceGasFamiliesTests` 13 passed after separating plain-host
  result acceptance from the named mechanism diagnostic, retaining the calibrated
  observable controls. Result acceptance now also checks exact states/compositions
  and the unchanged element-closure bound. Clearing only diagnostic scratch markers
  makes all five mechanism cases fail while all five trace-gas behavior cases pass
  (the command also ran five unchanged bracketed behavior cases, all passed).
  Evidence: `SCRATCH/audit/trace-role-split.log`, `trace-mechanism-negative.log`;
  mutation restored; final build 0 warnings/errors.

- [x] 2026-11-14 — R09 CPU controls: `LaunchObservationTests` 8 passed; both existing
  family outcome/budget controls also passed (10 total). Safe launches with a 900 ms
  sum pass a 500 ms per-launch target; one launch one tick above the target fails
  despite a safe mean. Actual BatchRun count/coverage/max identity, pre-download
  recording, failed-launch omission and unchanged four-phase public JSON are covered.
  Omitted recording, recording after download, last-instead-of-maximum and sum-instead-
  of-maximum mutants each failed the intended assertion; all restored. Evidence:
  `SCRATCH/audit/launch-observation-final-controls.log`, `launch-omitted-negative.log`,
  `launch-late-negative.log`, `launch-last-negative.log`, `launch-sum-negative.log`.

- [x] Re-verify the unchanged bracketing and trace-gas workload per-launch durations
  on actual CUDA under the documented bounded sampling policy: unknown or changed
  workloads start at one case; an identical workload may probe a live observed full
  initial prefix through Calibration. Every later launch grows by at most 2x.
  Every current synchronized launch meets the SAME 500 ms target, with nonzero
  launch count and covered cases equal to the unchanged 16,384-case workloads.
  Report current first-launch cases/reuse, aggregate, count, maximum duration and
  its chunk cases/ordinal. Preserve all cumulative throughput workloads, timing
  domains and 5x/80%/115% gates unchanged.
  (2026-11-15: actual c0df budget phases pass 5/5 on both platforms with complete
  current coverage. Windows KO2/trace maxima are 454.293/86.7546 ms.  Ubuntu-24.04
  422.3941/77.4743 ms, each below the same 500 ms target over 16,384 cases.
  Current cold/hit/changed-input first sizes are 1/15/1, coverage 31 each. Original
  quiet throughput passes separately at medians 1.269e-7/1.254e-7 s per Newton step.
  all original timing domains, 5x/80%/115% gates and earlier failures are retained.)
  ⚠ 2026-11-15: was first-one-case unconditionally, now identical-prefix probing → HISTORY.md#r09-identical-prefix-2026-11-15
  ⚠ 2026-11-14: was an aggregate ceiling, now a per-launch bound → HISTORY.md#r09-per-launch-correction-2026-11-14

⚠ 2026-11-14: was an aggregate obligation, now the per-launch F2 target → HISTORY.md#r09-aggregate-history-2026-11-14

The node's acceptance criteria (AGENTS.md §6, §15), moved here from `BOOT.md` on
2026-10-03 when the balance-remnant correction would have taken the leaf over its limit.

- [x] 2026-10-05 — CUDA equals the CPU accelerator bit for bit on the trace-gas families, and one launch of trace-gas hp and sp states stays within
      the launch budget (release 0.2.2, coder 17; the L2 row of `BOOT.md`; `TraceGasFamilies`, `TraceGasFamiliesTests`). The facts are
      `CudaTests.ATraceGasFamilyOnCudaMatchesTheCpuAccelerator` (a theory over the five families, `ExactComparison.Equilibrium`: statuses, iteration
      counts, every state field and every amount, bit for bit) and `CudaTests.AFamilyOfTraceGasStatesStaysWithinTheLaunchBudget`; on the CPU
      `TraceGasFamiliesTests` (8 facts, 5.9 s of the fast set, 303 facts in 50 s in Debug with `APTHERMO_NO_CUDA=1`) proves what each family holds
      and that its cases reach the pass. The families, built from the system and its excess, the grid and the host solver's own verdicts (nothing typed
      but the systems and the grids): `trace-gas-magnesite-walk` (Mg:C:O = 1:2:5, 1e7 Pa, tp 700 to 900 K every 5 K) 28 kept of 41, tp 28; `trace-gas-kcl`
      (+-1e-10 Cl at 1 and 100 kPa, 700 to 1500 K; +1e-6 Cl at 100 kPa, 950 to 1050 K, tp, hp and sp) 45 kept, 8 dropped, tp 23 (3 `NoGasPhase`), hp 11,
      sp 11; `trace-gas-calcite` (+1e-8 O at 100 kPa and 10 MPa, 300 to 900 K; -1e-7 O at 1 kPa, 830 to 870 K, the gas-mixture column band near 850 K)
      17 kept, tp 17 (4 `NoGasPhase`); `trace-gas-lithia` (+1e-10 O at 1 and 100 kPa, 600 to 1000 K, tp, hp and sp) 27 kept, 3 dropped, tp 10, hp 8, sp 9;
      `trace-gas-halite-cold` (+-1e-7 Cl, 1 kPa to 10 MPa, 250 to 300 K, tp, hp and sp) 43 kept, 127 dropped, tp 13, hp 10, sp 20 (25 `NoGasPhase`):
      160 cases, 22 068 Newton steps summed on CUDA. A tp case is kept when the host solver ends it `Ok` with the verdict's anchor in the scratch and more
      Newton steps than one attempt takes (the pass settled it) or `NoGasPhase`; an hp or sp case when the temperature bracket settled it at the state its
      target came from. What that proves and does not is `BOOT.md`, "The pass observable": an inference from documented writers, each condition shown
      false on a real state (`TheTwoConditionsOfThePassObservableAreEachNecessary`), the probed solve bit-identical to the plain one
      (`ThePassObservableTellsAStateTheOrdinaryAttemptSettlesFromOneThePassSettles`), and every kept case re-checked against the CPU accelerator's status
      and iteration count (`AFamilyHoldsTheCasesItStandsFor`). Evidence, 2026-10-05, Windows, Release, RTX 5070 Ti: the five families equal in every field
      of every case; the launch fact: 16 384 hp and sp cases of `trace-gas-kcl` in one launch, kernel 74.9 ms against the budget of 500 ms, 139 Newton
      steps per case on average; `dotnet test tests/Execution.Tests -c Release --filter "Category=Cuda"` 87 of 87 in 1 m 40 s, the sweep, the
      throughput fact and the architecture fact included. Red once, each
      mutation reverted: a kept row's temperature moved to 2000 K (`AFamilyHoldsTheCasesItStandsFor` red on all five families, "does not reach the pass: Ok
      in 14 steps, verdict anchor False"); `ReachesThePass` reduced to the status (`ThePassObservable...` red), to the status and the iteration bound
      (`TheTwoConditions...` red on the junction state), to the status and the anchor (`TheTwoConditions...` red on the 2000 K state); one unit in the
      last place added to the pressure of the first case of the CUDA batch only (the theory red on all five families, "Pressure: expected 10000000, actual
      10000000.000000002"); the launch limit lowered to 10 ms ("16384 trace-gas cases took 74.8 ms in one launch, above the budget of 10 ms"); the CPU
      check of the launch fact refuses a zero limit and a `NotConverged` case (`TheLaunchBudgetCheckRefusesALaunchOverTheLimitAndACaseThatFailed`, red with the status count multiplied by zero).
- [x] 2026-10-05 — The rocket compile bound is a property of the code, not of the machine (the L0 row of `BOOT.md`; release 0.2.2,
      coder 16). `RocketCompileTests.TheRocketKernelCompilesWithinItsAllocationBound` reads the bytes
      allocated by every thread of the process (`GC.GetTotalAllocatedBytes(precise: true)`) over the first rocket run of a fresh CPU
      engine and bounds them at 7 GiB; the calling thread's share is printed beside it and asserted on nothing. Evidence, 2026-10-05,
      the table of `BOOT.md`: the whole process allocated 3 575.9 to 3 577.6 MB in Release at 2, 4, 8 and 16 processors (spread 0.05 %)
      and 3 888.8 to 3 890.1 MB in Debug at 4 and 16, while the calling thread allocated 1.01 to 1.60 GB; the fast set (289 facts,
      Release, `APTHERMO_NO_CUDA=1`) is green and its figure for the fact differs from the fact alone by 0.5 MB. Hosted
      `windows-latest` had failed the calling-thread bound with 2.57 GB. ⚠ 2026-10-05: was the calling thread's allocation under
      2 GiB, now the process's under 7 GiB → HISTORY.md#compile-bound-process-wide-2026-10-05
- [x] 2026-10-05 — `TheRocketKernelCompilesWithinItsAllocationBound` seen red under the new measure with the attribute of
      `StationSolve.At` removed (a scratch edit in `src/Performance`, reverted; run by the orchestrator, Release,
      `APTHERMO_NO_CUDA=1`, 16 processors): 113 080 797 032 bytes in the process (44 785 589 192 on the calling thread),
      warm-up 263.22 s, against the bound of 7 516 192 768: red by a factor of 15.
- [x] 2026-10-05 — CUDA equals the CPU accelerator bit for bit on every family (`## Invariants`, item 13 of 0.2.2, owner
      decisions O1 and O6). `ExactComparison` (`Rocket`, `Equilibrium`, `Transport`, `Functions`) compares the bits of every
      field of every result, the statuses and iteration counts included, and returns at most 30 mismatches, each naming the
      case, the field and both values; the facts that run it on CUDA are the families of `CudaTests`: `ARocketFamilyOnCudaMatchesTheCpuAccelerator`,
      `AThroatFamilyOnCudaMatchesTheCpuAccelerator`, `AnEquilibriumFamilyOnCudaMatchesTheCpuAccelerator`, `ANamedEquilibriumFamilyOnCudaMatchesTheCpuAccelerator`,
      `AnEquilibriumTableFamilyOnCudaMatchesTheCpuAccelerator`, `ASeededFamilyOnCudaMatchesTheCpuAccelerator`,
      `AGasPlateauFamilyOnCudaMatchesTheCpuAccelerator`, `ABracketedFamilyOnCudaMatchesTheCpuAccelerator`, the 100 000-case sweep
      (`TheSweepOf100000CasesOnCudaEqualsTheCpuAcceleratorBitForBitAndIsDeterministic`), `SpeciesFunctionTests.CudaEqualsTheCpuAcceleratorBitForBit`
      and `ProbeKernelTests.CudaEqualsTheCpuAcceleratorBitForBitForEveryFunction`; the families are enumerated from the fixture
      directories and `FixtureBatches`, never typed. The CPU accelerator equals the host solver bit for bit as before (`BatchTests`).
      Evidence, 2026-10-05, the tree merged with coder 9's balancing-record rule: Windows, Debug, `Category=Cuda` 80 of 81 (the
      throughput fact refuses a Debug run, as designed), Release throughput and compile facts 4 of 4; WSL2 Ubuntu 24.04, Release,
      `Category=Cuda` 81 of 81 (the sweep, the architecture fact and the throughput fact included, 27 m 37 s). No tier, share bound, correction or case was needed to get there.
- [x] 2026-10-05 — The comparison is proven non-degenerate: `ExactComparisonTests` (7 facts: two runs compare clean; an
      equilibrium and a rocket result differing in one thing, a transport and a function result differing in one value, a signed
      zero and a NaN told apart, results of different shapes refused before any value is read, at most the shown mismatches
      returned). Red once, 2026-10-05, with `ExactComparison.cs` mutated and reverted: doubles compared by value in place of bits (`ASignedZeroAndANaNAreTold` red); the status and count comparison disabled (`ARocketResultDifferingInOneThingIsRefusedWithItsOwnMessage`, the equilibrium one and the transport and function one red); the cap raised from 30 to 3 000 000 (`AComparisonReturnsAtMostTheShownMismatches` red); a fourth mutation, the struct comparison removed, is refused by the analyzers (IDE0060) before it can run.
- [x] 2026-10-05 — The tolerance machinery is gone: no `GpuCpuTolerances`, `GpuCpuComparison`, `StepShareLedger`, `ComparisonRule`,
      balance-remnant or phase-onset type or fact remains in this node (a search of `tests/Execution.Tests` finds none), and the
      only bound that survives, `ElementBalance.ClosureBound` (1e-13), bounds one solution's element balance.
- [x] 2026-10-05 — Throughput (`## Invariants`, the approved throughput file): both records re-approved from Release runs, the
      median of three on each platform, on a machine shared with other test runs (the orchestrator re-measures on a quiet one).
      Windows: CUDA 0.360 s, CPU accelerator 7.217 s, ratio 20.07, kernel 1.426e-07 s per Newton step (runs 1.426e-07, 1.256e-07,
      1.433e-07; 22.403 steps per case, unchanged). WSL2: ratio 31.07, kernel 1.364e-07 s per step (runs 1.288e-07, 1.364e-07,
      1.537e-07), the file now carrying the per-step lines, so the Linux throughput fact passes. Before: 7.85e-08 s per step and
      5.3 s on the CPU accelerator (2026-10-04, quiet): the kernel +82 % and the CPU +36 %, the cost of correctly rounded `exp`,
      `log` and `pow` in double-double arithmetic on both accelerators. The 115 % tripwire reads the new figure. The compile guard
      (`RocketCompileTests`, 3 facts) is green in Release with the new kernel.

- [x] 2026-09-14 — The support code in shape (the test review's F-TF-06 and F-TF-13):
      `BatchBuilders` replaced by `FixtureBatches.cs`, `HostSolves.cs` (named record
      structs) and `SweepRun.cs`; no method over 60 lines or nested deeper than 3
      (`ShapeTests`, ten facts green at `62cd99e`); every L2 fact green bit for bit
      after the split and the node's mutations re-run alone and seen red where the
      touched code moved → HISTORY.md#criterion-support-code-in-shape-2026-09-14

      ⚠ 2026-09-15: was "nested deeper than 3" evidenced by `inventory.py` line counts
      alone, now measured: two methods were 4 deep and were brought to 3 →
      HISTORY.md#nesting-depth-not-measured-2026-09-15
- [x] 2026-09-15 — Two more constructions restructured to the root's parameter limit:
      `RocketInputs` (`FixtureBatches.cs`) fell
      from 9 to 6 parameters, split along the domain axes of a rocket fixture's
      chemical system (`ChemicalSystem`: elements, element moles, products - 3
      parameters) and its exit layout (`ExitPlan`: values, kinds - 2 parameters), the
      combustion conditions and the transport flag kept flat; the old field names
      stay as forwarding properties, so every read call site is unchanged and only
      the one construction site, in `RocketInputs.Of`, changed. `CudaTests.CompareMoles`
      fell from 8 to 5 parameters: the two mole arrays under comparison, the index
      into them and the species table that reads them became `MoleSample` (4
      parameters), a type local to `CudaTests.cs`; its two call sites (one in
      `AnEquilibriumFamilyOnCudaMatchesTheCpuAccelerator`, the other in the
      private `CompareRocket`, itself called from the rocket-family and the sweep
      tests) construct it in place of the four separate parameters.

      ⚠ 2026-09-15: was `MoleSample` local to `CudaTests.cs` and `CompareRocket` there,
      now both moved to `GpuCpuComparison.cs` the same day →
      HISTORY.md#mole-sample-moved-2026-09-15

      The two nesting-depth-4 fixes of the same review (`StationMoleDifferences`,
      `CompareFunctions`) and the run that verified them →
      HISTORY.md#nesting-fixes-evidence-2026-09-15

      ⚠ 2026-09-15: was `ChemicalSystem` holding `ElementMoles`, with forwarding
      properties for the old field names, now a `Mixture` record holds `ElementMoles`
      and `ReactantEnthalpy` and the forwarding properties are gone →
      HISTORY.md#chemical-system-forwarding-properties-2026-09-15

- [x] 2026-09-17 — Shown red once, on Windows: `Throughput.approved.txt`'s `ratio`
      line mutated from `56.28` to `999.00`, then `dotnet test tests/Execution.Tests
      --filter "FullyQualifiedName~ThroughputIsRecordedAndNotBelowTheApprovedRatio"`
      failed — "CUDA/CPU ratio 66.03 fell below 80 % of the approved 999.00
      (Throughput.approved.txt)" — naming the platform's own file, as
      `ApprovedPathFor` picks it. Reverted with `git checkout --
      tests/Execution.Tests/Throughput.approved.txt`; `dotnet test
      tests/Execution.Tests` confirmed 55/55 green again.
- [x] 2026-09-30 — The compile guard and the bound check of 2026-09-30 are proved (the execution node's
      `ACCEPTANCE.md`, criterion of that date, owns their design): each fact red once with what it guards
      undone, one process's run of the whole project below 2 GB of private memory at its
      peak on the reference machine, recorded here with the figure.

      Evidence: the facts are `RocketCompileTests` (`TheRocketKernelCompilesWithinItsAllocationBound`,
      `ADisposedEngineHoldsNoLauncher`,
      `ADisposedEngineKeepsNoCompiledProgram`) and, in `AcceleratorChoiceTests`,
      `TheBatchLengthBoundIsInclusiveOfTheLargestArrayLength` and
      `TheProbeOutputLengthBoundIsInclusiveOfTheLargestOffset`. Each was seen red with what
      it guards undone, applied alone:
      - The red-once record of each fact (the attribute removed from `StationSolve.At`,
        `Launchers.Clear()`, `Context.ClearCache`, the bounds of `BatchLength.Of` and of
        `MathProbe.OutputLength`) → HISTORY.md#compile-bound-red-once-list-2026-09-30

      ⚠ 2026-09-30: was one allocation named for removal (the 16 GB array), now a second
      removed too (`ProbeMath`'s 1.2 GB input, replaced by a fact on
      `MathProbe.OutputLength`); the owner may decide whether the trade stands →
      HISTORY.md#probe-allocation-replaced-2026-09-30

      The figures of the guard and the per-run table are in the execution node's `ACCEPTANCE.md`, criterion of that date.
      The peak private memory of one process's run of the whole project, `APTHERMO_NO_CUDA=1`,
      `Category!=LongRunning`, on this machine (60 GB, shared; the orchestrator confirms
      whether it is the reference machine), the largest process being the test host, the
      peak read from the process's own peak commit, sampled every 50 ms: Debug over five
      runs 0.92, 0.88, 0.91, 0.89 and 0.93 GiB; Release over three runs 0.84, 0.81 and
      0.81 GiB. Below 2 GiB with a wide margin. The process tree, the children of
      `AllCoresLayoutTests` included, peaked at 1.60 to 1.65 GiB in Debug and 1.48 to 1.50 GiB
      in Release. Before the inlining bound the same project exceeded 27 GB in one process
      tree (the root's Compile size constraint); with the bound and before the second
      allocation was removed, the test host peaked at 1.81 to 2.11 GiB.

- [x] 2026-10-01 — The transfers of the `Chunks` child node are proved (2026-10-01): the compacting
      collection fact and the guard fact of `src/Execution/ACCEPTANCE.md`'s criterion of
      that date live here, in `ChunkTransferTests`, on the CPU accelerator, and run in the
      fast suite under `APTHERMO_NO_CUDA=1`. The collection fact holds ILGPU's private
      `Accelerator.syncRoot` from a helper thread for at most the duration of one forced
      collection, with a timeout on every wait so that a broken construction fails in
      seconds rather than hanging the run.

      Evidence: `ChunkTransferTests` (3 facts: `AChunkDownloadSurvivesACompactingCollectionInsideItsTransfer`,
      `ADownloadThatLeftItsSliceUnwrittenIsRefused`, `OnlyASliceOfSentinelBytesHoldsOnlyTheSentinel`),
      green in Debug and in Release, 22 runs in a row, and in the fast suites of both
      configurations (Debug 170 of 170 here, the node's full count). Red once, 2026-10-01:
      with the `ref` overload restored in `DownloadChunk` and the guard removed, the host array
      holds `[0, 0, 0, 0]` and the guard fact fails too; with the `ref` overload restored and
      the guard kept, the downloading thread throws "left its host slice unwritten"; with only
      the check after the copy removed, the guard fact's first assertion fails.

      The three points where the construction differs from the design's sketch (a
      download thread of its own, the unpinned sibling array below the host array, the
      first download before the attempts) →
      HISTORY.md#chunk-transfer-construction-differences-2026-10-01
- [x] One launch of cases that all bracket stays within the launch budget (`## Purpose`, the same L2 row):
      `CudaTests.AFamilyOfCasesThatAllBracketStaysWithinTheLaunchBudget` green on the reference machine,
      the kernel time of one launch of 16 384 (or one wave, if smaller) hp and sp cases of `gasless-ko2`
      below a quarter of `LaunchBudget.DefaultRunTimeLimit`, the figure in the test output. Without CUDA the
      check (`RecoveryFamilies.LaunchViolation`) is proven on the CPU accelerator, 2026-10-04:
      `BracketedFamiliesTests.TheLaunchBudgetCheckRefusesALaunchOverTheLimitAndACaseThatDidNotBracket`
      (a limit of zero and a case that ended `Ok` are refused).
      On CUDA, 2026-10-04 (11fe71f6, Release): 16 384 bracketing cases in one launch, kernel 361.3 ms against the 500 ms budget, 151 Newton steps per case on average.
- [x] 2026-10-05 — The throughput tripwire reads the median kernel time (coder 15 of 0.2.2; the ⚠ 2026-10-05 of `BOOT.md`):
      `SweepRun` takes `cuda_kernel_seconds` and the per-iteration figure from the median of the three timed runs' kernel times
      (`CudaTiming`), proven without a GPU by `ThroughputRecordTests.TheKernelTimeOfTheFigureIsTheMedianOfTheTimedRunsInAnyOrder`
      (3 cases; red once with the median replaced by the first run: expected 0.349 s, actual 0.376 and 0.277 s).
      - Five quiet Release runs on Windows (`TheSweepOf100000` and `ThroughputIsRecorded`), per step in 1e-07 s, first timed run
        under the old rule against the median under the new: 1.237 / 1.236, 1.371 / 1.237, 1.359 / 1.237, 1.235 / 1.236,
        1.238 / 1.238 (old spread 11.0 %, new 0.2 %); ratios 18.54, 18.21, 18.35, 18.52, 18.56.
      - Five quiet Release runs in WSL2 (Ubuntu-24.04, a tree of this commit under `~`): 1.235 / 1.235, 1.234 / 1.234, 1.438 / 1.235,
        1.271 / 1.237, 1.233 / 1.233 (old spread 16.6 %, new 0.3 %); ratios 26.50, 25.74, 25.92, 25.77, 26.51.
      - `Throughput.approved.txt` re-approved at the median of the five Windows runs: ratio 18.52 (CUDA 0.355 s, CPU 6.570 s, the
        run of the median ratio), kernel 0.277 s, 1.237e-07 s per step. `Throughput.linux.approved.txt` at the run of the median
        ratio, which is also the median per step: ratio 25.92 (CUDA 0.369 s, CPU 9.570 s), kernel 0.277 s, 1.235e-07 s per step.
        The earlier records (1.426e-07 and 1.364e-07) were measured with the first-run rule on the tree before the own math.

- [x] 2026-10-04 — The throughput tripwire per Newton step (coder 7 of 0.2.2; the ⚠ 2026-10-04 of `BOOT.md`):
      `CudaTests.ThroughputIsRecordedAndNotBelowTheApprovedRatio` writes `iterations_per_case` (22.403) and
      `cuda_kernel_seconds_per_iteration` and fails above 115 % of the approved figure (`ThroughputRecord`); the rule and the
      failure of a record without the figure are proven without a GPU by `ThroughputRecordTests` (7 facts).
      - Shown red once, on the Windows reference machine, Release: with `EquilibriumSolver.cs` and `StationSolve.cs` at d38e3f89
        (the loop-live `IterationState` passed by `ref` to `TraceGasPass.Run`) the fact failed with "the CUDA kernel time per
        Newton step 1.023e-07 s is 130.3 % of the approved 7.849e-08 s", the ratio check alone passing at 17.65 (floor 17.03);
        with the fix (51c5515c, 1cee836d) green.
      - `Throughput.approved.txt` re-approved from two quiet Release runs of 2026-10-04: kernel 0.175 and 0.176 s, 7.820e-08 and
        7.849e-08 s per step, ratio 20.71 and 21.29 (25 % background CPU load); a third run inside the solution-wide Release run
        measured 7.834e-08 (kernel 0.176 s). The record of 2026-09-19 (ratio 23.58) does not reproduce, even at v0.1.0
        → HISTORY.md#throughput-per-iteration-2026-10-04
      - Windows, Release, `dotnet test APThermo.sln -c Release --filter "Category=Cuda|Category=BitSnapshot"`: green in every
        project, `Execution.Tests` 84 of 84 (the bits unchanged: no `Bits*.approved.txt` moved, `ByteVectorTests` included).

### Sweep-mode host controls — 2026-11-16

`SweepModeTests` known-answer cold/hit/hit passed; a no-op mode-check stub compiled
and failed 8/9 controls, including cold/hit/cold and cold/cold/hit. `SweepModeTests`,
`ColdThroughputTests` and `ThroughputRecordTests` passed 64/64 on the CPU host.
Actual CUDA verification remains pending.

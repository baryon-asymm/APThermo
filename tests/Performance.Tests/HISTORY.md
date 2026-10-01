# HISTORY.md — Performance.Tests

Append-only store of what `BOOT.md` no longer needs to state as current truth
(`AGENTS.md`, §15). Newest entry first. Every entry names the date it was moved, the
`BOOT.md` section it came from, and carries the original text in full, unedited. The
start procedure (`AGENTS.md`, §10) does not read this file; it is reached only by
following a dated pointer left in `BOOT.md` at the place the text used to stand.

---

<a id="crit-example13-measured"></a>

## 2026-10-01 — from "## Acceptance criteria" — measurement: the example-13 throat field by field

Moved because `BOOT.md` was over its limit (`AGENTS.md`, §15); a pointer stays at the
place it stood (original date 2026-09-28). The text as it stood:

>       ⚠ 2026-09-28, measured field by field (the coordinator's second review, which
>       asked for a number, not only the mechanism): a throwaway diagnostic (`ZzDiag.cs`,
>       deleted before this commit) printed the throat station's pressure, temperature,
>       c*, mass flux, `GammaS`, Mach and every non-zero mole fraction, once with
>       `ThroatSearch.At`'s `UpstreamChokeCheck.Verify` call skipped (the pre-`627f815`
>       path standing unchanged) and once with the tree's current code, both against the
>       real `rp1311-example13` mixture (not the synthetic chamber of F4's own fact).
>       Pressure (12694259.494254986), temperature (2851.0000144702376) and `GammaS`
>       (0.9978925188362665) are bit-identical between the two runs. Every other field
>       differs only at the rounding floor: the largest relative change of any field,
>       over pressure, temperature, c*, mass flux, `GammaS`, Mach and 38 non-zero mole
>       fractions (41 fields in all), is 4.378e-13, on the condensed `BeO(b)` mole
>       fraction (old 0.020208206802093842, new 0.02020820680210269); c*, mass flux and
>       Mach each move by about 3.4e-14 relative. Both figures are nine to eleven orders
>       below the fixture tolerance table's own rows and far below a ~1e-9 relative floor
>       a genuine (non-rounding) divergence would have to clear: "a re-solve's rounding"
>       is what the measurement shows, not only what the mechanism's description implies.

---

<a id="bits-reference-machine"></a>

## 2026-10-01 — from "## Invariants" — correction: the bits are a record of the reference machine

Moved because `BOOT.md` was over its limit (`AGENTS.md`, §15); a pointer stays at the
place it stood (original date 2026-09-19). The text as it stood:

>   ⚠ 2026-09-19: the root BOOT.md's platform constraint (⚠ 2026-09-18, the declared
>   deviation from "every test runs on both platforms") holds that the bits are a
>   record of the reference machine, not of the platform alone: hosted CI runners land
>   on CPUs whose C runtime rounds the last bit differently from the reference
>   machine's. `EveryRocketFixtureGivesTheRecordedBits` and
>   `EveryApprovedLineNamesARocketFixture` now carry
>   `[Trait("Category", "BitSnapshot")]`, so both run in every local run (`CLAUDE.md`'s
>   fast set) and in the release's self-hosted jobs
>   (`.github/workflows/release.yml`'s `cuda-windows` and `cuda-linux`, filter
>   `Category=Cuda|Category=BitSnapshot`), and are filtered out of the hosted fast
>   suite (`ci.yml`; `release.yml`'s `matrix` job; filter
>   `Category!=LongRunning&Category!=BitSnapshot`), where the L0/L1 rows' comparison
>   against the CEA reference holds correctness instead.

---

<a id="bits-per-platform"></a>

## 2026-10-01 — from "## Invariants" — correction: one snapshot file became a Windows and a Linux record

Moved because `BOOT.md` was over its limit (`AGENTS.md`, §15); a pointer stays at the
place it stood (original date 2026-09-17). The text as it stood:

>   ⚠ 2026-09-17: this bullet assumed one snapshot file. The root's platform constraint
>   now keeps a Windows and a Linux record, since the CPU accelerator's `System.Math`
>   calls the platform's C runtime and the two do not round the last bit alike;
>   `ApprovedPath` resolves through `Harness.ApprovedSnapshot.ApprovedPathFor`
>   (`tests/Harness/API.md`), which picks `Bits.approved.txt` or `Bits.linux.approved.txt`
>   for the running platform, so this node's own code names no platform. The first Linux
>   run (2026-09-17, WSL2 Ubuntu 24.04, `f67b1a9` plus this task's harness change) did not
>   reproduce the Windows bits, within the tolerance the root BOOT.md records for the
>   difference; `Bits.linux.approved.txt` was approved from that run.

---

<a id="crit-rocket-inputs"></a>

## 2026-10-01 — from "## Acceptance criteria" — correction: `ChemicalSystem`, `Mixture` and the forwarding properties

Moved because `BOOT.md` was over its limit (`AGENTS.md`, §15); a pointer stays at the
place it stood (original date 2026-09-15). The text as it stood:

>       ⚠ 2026-09-15: the forwarding properties did not hold. Two defects, found by the
>       repair review. First (R-Performance.Tests-2), `ChemicalSystem` mixed a batch's
>       shared axis with a case's own: `Elements` and `Products` are what `BatchKey`
>       groups cases by by construction, but `ElementMoles` is one case's own starting
>       composition and varies case to case inside a shared batch, exactly like
>       `ReactantEnthalpy`, which already sat outside `ChemicalSystem`. `BatchKey` itself
>       never read `ElementMoles` — a sign it was never part of the system a batch
>       shares. `ChemicalSystem` narrowed to `Elements`, `Products` (2 parameters), and
>       a new `Mixture` record holds `ElementMoles` and `ReactantEnthalpy` (2
>       parameters) as the case's own starting state; `RocketInputs` keeps `System`,
>       `Mixture`, `ChamberPressure`, `Flow`, `Exits`, still 5 parameters. Second
>       (R-Performance.Tests-3), keeping the flat names as forwarding properties was
>       the shortcut the decomposition should not have taken: it let the ~25 read call
>       sites stay unchanged only by hiding, behind a compatibility shim, which record
>       each field actually lives on. The forwarding properties on `RocketInputs`
>       (`Elements`, `ElementMoles`, `Products`, `ExitValues`, `ExitKinds`) and on
>       `RocketSolution` (`Stations`, `Moles`, `Multipliers`, `Figures`,
>       `StationStatus`, `Iterations`) are removed; every read call site in
>       `RocketCase.cs`, `RocketHost.cs` itself (`StationCount`, `TotalMoles`,
>       `MoleFraction`), `KernelEqualityTests.cs`, `StationComparison.cs`,
>       `SubsonicStationTests.cs`, `RocketInvariants.cs`, `RocketFixtureTests.cs`,
>       `InvariantTests.cs` and `BitSnapshotTests.cs` now names the sub-record it reads
>       (`.Outcome.`, `.System.`, `.Mixture.`, `.Exits.`) directly. No behaviour
>       changed: the same fields, on the same two records, under new names one level
>       down. Verified: 700/700 tests green (699 plus R-Performance.Tests-1's stale-key
>       fact), `Bits.approved.txt` hash unchanged
>       (`5aa32f2bbf679cdd0f47749b0780059ba89faa62`).

---

<a id="crit-invariants-one-type"></a>

## 2026-10-01 — from "## Acceptance criteria" — correction: the length bound of the invariant methods

Moved because `BOOT.md` was over its limit (`AGENTS.md`, §15); a pointer stays at the
place it stood (original date 2026-09-15). The text as it stood:

>       ⚠ 2026-09-15: "each under fifteen lines" was not true: `FrozenComposition` held
>       18 lines that were not blank, `AssignedExit` 17, `EnergyEquation` 15 — at or,
>       for two of the three, above the claimed bound. Found by the repair review
>       (R-Performance.Tests-6); the bullet now states a bound every method meets,
>       under twenty lines of code, re-measured on the merged tree by the protocol
>       tests node's own tool (`ShapeMeasures.MethodLines`, run through a temporary,
>       uncommitted test): `SonicThroat` 6, `ConstantEntropy` 14, `EnergyEquation` 15,
>       `AssignedExit` 17, `FrozenComposition` 18 lines of code, all under the bound.

---

<a id="kernel-struct-public"></a>

## 2026-10-01 — from "## Constraints" — correction (distribution phase): why the batch struct is public

Moved because `BOOT.md` was over its limit (`AGENTS.md`, §15); a pointer stays at the
place it stood (original date 2026-09-15). The text as it stood:

>   ⚠ 2026-09-15 (distribution phase): this bullet gave the reason "because ILGPU
>   compiles kernels only over public parameter types". Wrong: ILGPU 1.5.3 needs only
>   `[assembly: InternalsVisibleTo("ILGPURuntime")]` on the declaring assembly to load an
>   internal kernel parameter type, method or view element; public is only one way to
>   satisfy it (the API review of 2026-09-15, section 3, fixed here in `24156be`,
>   proved it on the CPU accelerator and on CUDA — Execution's own four views structs
>   are internal now, with that grant). The claim entered with `f2e5de7`/`53ec9fb` on
>   2026-09-12. The struct stays public here; nothing in this node's own scope required
>   the change.

---

<a id="crit-mutations"></a>

## 2026-10-01 — from "## Acceptance criteria" — criterion: the mutation runs, the re-dating and the correction of the loosened tolerances

Moved because `BOOT.md` was over its limit (`AGENTS.md`, §15); shorter wording stays at
the pointer (original date 2026-09-14). The text as it stood:

>       accepted by the solver (that fixture case red). (Re-dated from 2026-09-12: the
>       hand-typed counts are gone, F-TK-03; the sonic/area-ratio mutation below moved
>       to its own paragraph.)
>
>       ⚠ 2026-09-14: this criterion stood "the solver's sonic and area-ratio tolerances
>       loosened to `1e-2` and `4e-2` (88 of 89 fixture cases and 89 of 89 invariant
>       cases red)". Re-run today before any other change, it turns nothing red: every
>       fixture's throat and exit search now reaches `RocketSolver.TightTolerance`
>       (`1e-10`) within `MaxThroatIterations`/`MaxAreaRatioIterations` before the loop
>       ever consults the report-tolerance fallback — a refinement that postdates the
>       2026-09-12 run and that this session's decomposition carried over unchanged (the
>       Bits level below is the proof it moved no formula). `SonicTolerance` and
>       `AreaRatioTolerance` are therefore dead code for every fixture of the current
>       tree; the criterion below mutates `TightTolerance` instead, the constant that
>       actually gates convergence, and records what it found.

---

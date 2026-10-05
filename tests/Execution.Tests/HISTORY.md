# HISTORY.md — Execution.Tests

Append-only store of what `BOOT.md` no longer needs to state as current truth
(`AGENTS.md`, §15). Newest entry first. Every entry names the date it was moved, the
`BOOT.md` section it came from, and carries the original text in full, unedited. The
start procedure (`AGENTS.md`, §10) does not read this file; it is reached only by
following a dated pointer left in `BOOT.md` (or `ACCEPTANCE.md`) at the place the text
used to stand.

---

<a id="exact-comparison-2026-10-05"></a>

## 2026-10-05 — from "## Purpose", "## Invariants", "## Dependencies", "## Constraints" and "## Taboos" — the GPU/CPU tolerance machinery

Item 13 of release 0.2.2 gave the tree its own correctly rounded `Exp`, `Log` and `Pow` and made the post-link a rewrite that forbids contraction, with no libdevice. CUDA then equalled the CPU accelerator bit for bit on every fixture family, the 100 000-case sweep, the species functions and 123 135 probe values (owner decision O6, 2026-10-05), and the node's tolerance machinery went: `GpuCpuTolerances` (the table and its derivations), `GpuCpuComparison`, `ComparisonSupport` (the enthalpy rounding bound, the other accelerator's species data, the noise response), `BalanceSensitivities` (the balance-remnant correction), `StepShareLedger` with `LastFactOrderer`, `SpeciesFunctionSources`, and the facts `BalanceRemnantTests`, `ComparisonRuleTests`, `PhaseOnsetTests`, `StepShareLedgerTests` and the comparison facts of `BracketedFamiliesTests`. `ExactComparison` and `ExactComparisonTests` replaced them. The passages as they stood, all of the evidence they carry (measured figures, derivations) included:

> The definition of what "`Execution` is ready" means. This node owns the tolerance
> table for CUDA against the CPU accelerator and the approved throughput figures.

> | L0 | accelerator choice and the environment variable; libdevice discovery messages; ILGPU version and reflected members asserted; batch validation; chunk bounds; result layouts | documented behaviour; mutation of the assertion (`AcceleratorChoiceTests`) | ✅ |
> | L0 | the reason of an `Auto` fallback is on the accelerator description (`CudaSkippedBecause`), naming what was missing and the paths tried; a scratch bound of zero or less is refused at `Create`; the post-link's missing-definition guard names the wrapper whose definition is absent, driven without a GPU through a wrapper body with one definition removed (`PostLinkTests`) | the `API.md` of `Execution` (2026-09-14) | ✅ (2026-09-14) |
> | L1 | the probe kernel with every function of the root's math list loads through the post-link on CUDA and matches the CPU accelerator; the CPU accelerator reproduces `System.Math` bit for bit | the CPU accelerator and `System.Math`, the GPU/CPU tolerance table (`ProbeKernelTests`) | ✅ |
> | L0 | the post-link's wrapper inventory over ILGPU 1.5.3's own PTX of the probe kernel, one fixture with the wrappers defined (SM_89) and one without (SM_120): the called set from `call` sites only, the defined set from `.func` headers, the missing set, with LF and CRLF line ends (2026-09-26) | the text fixtures `Ptx/probe.sm_89.ptx` and `Ptx/probe.sm_120.ptx`, whose provenance is under Constraints | ✅ (2026-09-26) |
> | L1 | every architecture ILGPU 1.5.3 declares from SM_75 up: every entry point compiled for it passes the post-link and loads on the reference device, both paths of the post-link occur, the PTX equals the device's own up to ILGPU's generated names and the `.target` line, and the probe returns the device's own bits; an engine binds CUDA only after the probe kernel loads, and a post-link failure at bind is the `Auto` fallback's reason or the explicit request's exception (2026-09-26) | the engine's own CUDA kernels and probe, the CPU accelerator, the GPU/CPU tolerance table | ✅ (2026-09-26) |
> | L0 | the library is checked before the device: a bad libnvvm names both paths and never leaks device memory (`BadLibraryTests`); the CPU accelerator is sized for `Environment.ProcessorCount`, proven at 4, 16 and 64 in child processes, with identical batch results (`AllCoresLayoutTests`); a chunk stays within 32-bit offsets at the tree's own size limits (`AcceleratorChoiceTests.ChunksStayWithinInt32OffsetsAtTableLimits`); a NUL-padded log is trimmed of it (`PostLinkTests`); a half-given library path pair is refused (`AcceleratorChoiceTests.AHalfGivenExplicitLibraryPairIsRefused`) (2026-09-26) | `Execution`'s `BOOT.md` and `API.md`, the audit's F2, F3 and F4 | ✅ (2026-09-26) |

> | L2 | every rocket and throat fixture family; every equilibrium fixture table, tp, hp and sp, one family per table (`FixtureBatches.EquilibriumTableFamily`, 2026-10-04); the `seeded` fixtures, the tp fixtures of every table warm-started at half pressure and the bracketed calcite and magnesite states seeded 20 K above the plateau (`SeededFamilies`, 2026-10-04); the computed gas-plateau families of 0.2.2 (`GasPlateauFamilies`); and a 100 000-case sweep on CUDA equal the CPU accelerator; the CPU accelerator equals the numerical nodes called case by case, a seeded case with its seed; determinism of two runs; chunking gives the same result as one chunk, a seeded batch's included; the species-function batch against the host functions and across accelerators; the element balance of every compared station closes to 1e-13 and the equilibrium families are compared with the balance-remnant correction (2026-10-03, `BalanceRemnantTests`) | the CPU accelerator and the host calls; reflection-enumerated fields (`BatchTests`, `CudaTests`, `SpeciesFunctionTests`) | ✅ |
> | L2 | the 0.2.2 gasless verdict and temperature bracket on CUDA (`RecoveryFamilies`, 2026-10-04): tp, hp and sp states of KO2 and NaO2 at their exact 1:2 stoichiometry under 1e7 Pa where the gas vanishes, a gasless KO2(a)/KO2(L) melting plateau, gas plateaus of CaCO3 and MgCO3 at 1e5 Pa started cold at 0.1 to 0.5 of the transition, and AP/HTPB/Al hp states at 20 MPa below the water band, each family keeping the cases the CPU accelerator ends `NoGasPhase` or `Ok` as it stands for; equal statuses, `Ok` fields and `NoGasPhase` amounts within the table, a `NoGasPhase` state's pressure (and a tp case's temperature) exact and its other fields zero, one launch of cases that all bracket inside the launch budget (`CudaTests.ABracketedFamilyOnCudaMatchesTheCpuAccelerator`, `AFamilyOfCasesThatAllBracketStaysWithinTheLaunchBudget`; on the CPU `BracketedFamiliesTests` and `BatchTests.ABracketedFamilyEqualsTheHostSolverBitForBit`) | the CPU accelerator and the host calls; the species functions for the h and s targets | ⏳ (CUDA run pending) |

> - **The GPU/CPU tolerance table lives here, in one file** (`GpuCpuTolerances.cs`),
>   and every entry carries its derivation: relative 1e-10 on temperature; relative
>   1e-10 on mole fractions not below 1e-8 at stations where both accelerators stopped
>   after the same number of Newton steps, and 1e-9 where they did not; relative 1e-9
>   on the other state fields, the performance figures and the transport figures; 4 ULP
>   on the probe kernel's math functions (3 measured on the reference machine); at most
>   one station in a thousand may stop after different numbers of Newton steps; 1e-10
>   on the species functions, taken on the larger of 1 and the value, because H°/RT of a
>   reference element at 298.15 K cancels to zero by construction and the condensed fits
>   cancel by up to five decades (1.7e-11 measured on liquid water's Cp/R; the entry was
>   first written as 1e-12 relative and calibrated on that measurement the same day).
> 
>   ⚠ 2026-09-12: was one tier, 1e-10 on every mole fraction above the floor, now 1e-9
>   where the two accelerators took different numbers of Newton steps (derived from the
>   polish threshold) and a bound on the share of such stations →
>   HISTORY.md#tolerance-two-tiers-2026-09-12
> 
>   ⚠ 2026-09-14: was the whole table "in one file", now the mole-fraction floor and the
>   polish-threshold tier live in `tests/Fixtures/tolerances.json` and this node keeps the
>   GPU-specific entries → HISTORY.md#tolerance-table-not-in-one-file-2026-09-14
> 
>   ⚠ 2026-10-04: was "at most one station in a thousand" checked per family (a family of one
>   or a few cases failed on any single step difference, a share of one in one), now run-wide:
>   every CUDA family adds its stations and its differing stations to a ledger
>   (`StepShareLedger`, on `EngineFixture.Shared`), `CudaTests.TheStepShareOverTheWholeRun`,
>   ordered last in its class by `LastFactOrderer`, holds the share of the whole run at the
>   table's 1e-3 (it fails on an empty ledger), and a family keeps only a coarse guard of
>   `max(1, floor(1e-3 · stations))` differing stations. The table is not loosened. Facts
>   without CUDA: `StepShareLedgerTests`.
> 
>   **Bracketed cases** (0.2.2, 2026-10-04): the `Iterations` of an hp or sp case that ran the
>   temperature bracket sums every attempt and every tp probe (`Equilibrium`'s `API.md`), and
>   the batch result does not split it, so a flip of one probe or bisection decision moves the
>   total without moving the final solve. Measured on CUDA on the reference machine on the 18
>   stations of `bracket-calcite-1e5`, `bracket-magnesite-1e5` and `bracket-ap-htpb-al-20mpa`:
>   totals differ by up to 89 (14 of 18 differ) while the final states agree far inside the
>   first tier: temperature 2.9e-13, state fields 6.5e-13, condensed fractions 5e-13, gas
>   fractions 4e-12 (AP/HTPB/Al) and 3e-13 (calcite after the balance-remnant correction; raw
>   3e-9 on the O2 remnant, κ 1e6 times residuals of 1e-14). The rule for a family that runs
>   the bracket (`GpuCpuComparison.IterationsSumAttempts`): no difference of totals counts toward
>   the step share, and every gaseous mole fraction is held to the first tier (1e-10; a condensed one keeps its own 1e-9), stricter than the
>   second the totals would have bought. No tier moves and the share bound stays 1e-3.
> 
>   A third mole-fraction tier (the owner's decision of 2026-10-03): a **condensed**
>   species not below the floor is compared at relative 1e-9 whatever the Newton counts.
>   Derivation: a condensed amount on a phase plateau is ill-conditioned, with `d ln x/d ln
>   p` of 59 to 290 measured at the throat fixtures' plateau stations (`LiOH(L)`,
>   `AL2O3(a)`) against 2 to 6 for gases; the two accelerators' throat pressures differ by
>   up to 8.4e-13 in `ln p`, so 290 × 8.4e-13 = 2.4e-10, and the solve's own floor for such
>   an amount is about 1e-10 (1.31e-10 under injected noise on the CPU). On CUDA on
>   2026-10-03 the worst were `LiOH(L)` 9.5e-11 (0.95 of the old tier) and `AL2O3(a)`
>   4.0e-11; every other condensed value of the throat and rocket fixtures was at most
>   1.1e-12. Gaseous species keep the two tiers above.
> 
>   **Balance-remnant correction** (2026-10-03, the orchestrator under the owner's grant of
>   that day; no tier moves). Each accelerator closes the element balance only to a relative
>   residual `ρ_i = Σ_j a_ij n_j / b_i − 1` of about 1e-14 (4.9e-14 the worst over every
>   compared family), and a species the balance sets as a small difference of large amounts
>   carries that residual amplified. In `three-element_rp1311-example1` the remnant x(H2) has
>   `κ = Σ_i |∂ ln x/∂ ln b_i|` = 4.2e5; CUDA and the CPU differed in it by 3.8e-9 at equal
>   Newton counts, and the CPU alone moves it as far under ±1 ulp of `b` or a warm restart.
>   The difference equals the first-order prediction `Σ_i D_ij (ρ_i^cuda − ρ_i^cpu)`,
>   `D_ij = ∂ ln x_j/∂ ln b_i`, to 2.4e-15. Two rules follow, for the equilibrium families
>   (tp, hp and sp batches, whose input is `b`):
>   - a mole fraction is compared as `ln x_j − Σ_i D_ij ρ_i` on each side, at the tiers
>     above. `D` comes from central differences (`h` = 1e-8) through the CPU accelerator over
>     the same batch; a case gets no correction when a condensed species appears or vanishes
>     between `b(1 ± h)`, and a species gets none when the two one-sided differences of its
>     `ln x` disagree by more than 2e-2 of its largest `|D_ij|` (or of 1 where that is
>     smaller; raised for a large κ, "A curved remnant" below). The entry is `GpuCpuTolerances.Entries["sensitivityDisagreement"]`, measured
>     on the CPU on 2026-10-03 over the six equilibrium families (23 cases, 206 compared
>     species): the appear-or-vanish rule dropped 0 of 23 cases; the disagreement was at most
>     2.1e-3 on the smooth rows (the H2 remnant, `κh` = 4.2e-3: the two differ by about
>     `0.5 κh` from the curvature) and 0.12 to 0.60 at the kinks (the threshold-flip KClO4
>     and NaClO4 cases, 13 species), with 6 noise-dominated rows of HCHO in lox-rp1 (`κ` 6e-7,
>     9 to 11); the bound is 9.5 times above the worst smooth row and 5.8 below the smallest
>     kink, and the guard drops 19 of the 206 species, each with `κ` at most 1.35, whose
>     correction is at most 1.4e-13, a seven-hundredth of the tier;
>   - every compared station of every family asserts `ρ_i ≤ 1e-13` on both accelerators
>     (twice the worst measured), which also catches a defect that breaks conservation.
> 
>   Measured after the correction: H2 2.4e-15, the worst gas row at equal steps 3.98e-11.
>   The worst `ρ` on the CPU accelerator, 2026-10-03: 3.9e-15 to 4.9e-14 over the six
>   equilibrium families (the threshold-flip KClO4 the worst), 3.8e-15 to 4.0e-14 over the
>   rocket and throat families (`nto-udmh` the worst). Facts without CUDA
>   (`BalanceRemnantTests`): the corrected comparison accepts what the uncorrected one
>   refuses on `three-element-example1` (a second CPU run over `b` moved by 16 ULP stands for
>   the other accelerator, H2 3.3e-9 against 8.7e-15); the two agree to `κ` times the
>   residuals where `κ` is small; the guard keeps the remnant and drops a kink; a residual of
>   1e-12 injected into a result is reported, in the equilibrium and in the rocket comparison.
>   The appear-or-vanish rule at a phase onset (`PhaseOnsetTests`): a tp case of the example12 table built at the
>   boundary where C(gr) appears, found by bisection on the carbon moles through the CPU accelerator, has no derivative and
>   no corrected species, and its comparison passes uncorrected.
>   Rejected: a tier scaled by κ (it loosens); dropping the family (the only CUDA coverage
>   of rule A's tie); comparing the multipliers (`π_H` carries the same conditioning). The
>   probes: the orchestrator's scratchpad, `probe/` (2026-10-03).
> - **What a difference between the accelerators is not** (2026-10-04, coder 5 of 0.2.2, on the eight failures of the CUDA
>   run of de7cda2f; no tier moves, the step-share bound stays 1e-3, no case is dropped for these rules; `GpuCpuComparison`
>   with `ComparisonSupport`, proven without a GPU by `ComparisonRuleTests`). Every rule asks the CPU accelerator, case by case
>   and only for a case the plain comparison refused or whose field is a cancelling sum, what rounding or the other
>   accelerator's own species data do by themselves; a difference above that is still reported.
>   - **Enthalpy that cancels**: `si-in-argon_T298.15` and `li-in-argon_T298.15` are the elements at the reference temperature,
>     enthalpy 3e-6 J/kg out of terms of 1e5. Two accelerators sum at most `8 + S` terms per species, each bounded by
>     `|H/RT| + Cp/R`, so they differ by at most `2 (8 + S) u R T Σ n_j (|H_j/RT| + Cp_j/R)`
>     (`ComparisonSupport.EnthalpyBound`, `u` the unit roundoff); a difference above the tier within that bound is accepted for
>     the field Enthalpy only. Where the enthalpy does not cancel the bound is below a thousandth of the tier (checked for every
>     case of the water table).
>   - **Species data**: the two accelerators' G/RT of a species whose polynomial cancels by decades differ. H2O(L) cancels by about
>     five decades and its G/RT differs by 1e-11 to 6e-11 between CUDA and the CPU. A tp case on the liquid-gas boundary carries that
>     into x(H2O) at the species' own sensitivity: `rp1311-example14_T300`, `T304` and `T304.3` 1.31e-10, 1.52e-10 and 1.15e-10
>     (tier 1e-10, κ 10.9, balance residual 4e-15 to 1.2e-14), and `ap-htpb-al_pc1MPa_T420` x(CH4) 1.31e-10 (κ 12.1).
>     `ComparisonSupport.DataEffect` measures the directional derivative of the CPU solve of the case along the two accelerators'
>     measured difference of G/RT (central difference, largest move 1e-7, through the constant b2 of the entropy fit) and adds it
>     to the other side's correction; what remains is compared at the tier. Applied to tp cases only: an hp or sp case needs the
>     derivative of the temperature too, and none of the measured failures needs it.
>   - **Plateau states**: the states of `seeded-fixtures` on the AP/HTPB/Al reaction plateau (a pinned phase set) are
>     ill-conditioned: the CPU accelerator alone moves a mole fraction by up to 7e-9 under 1 to 16 ULP of the element moles. The
>     CUDA deviations measured: x(H2O(L)) 1.25e-9 and 3.11e-9 (κ 897 and 304), x(CH4) 1.0e-10 to 6.0e-10 (κ 13 to 50), x(HCL)
>     1.2e-10 and 2.3e-10 (κ 77 and 204), balance residual 7e-15 to 4.8e-14. `ComparisonSupport.NoiseResponse` replicates the case
>     16 times (1, 2, 3, 4, 6, 8, 12 and 16 ULP of every element's moles, two alternations) and takes for each species the largest
>     `|ln x_r − ln x_0|`; the tolerance of a species is raised to `NoiseFactor` times that where it is above the tier. The largest
>     ratio of the CUDA deviation to the replicates' maximum was 0.92 over 153 compared quantities (median 0.21), so the factor 2
>     leaves 2.2 above it; it is an empirical bound, not a derived one, and the one judgment call of this section. A replicate
>     whose condensed set differs makes the response null and the rule does not apply.
>   - **Newton counts that flip inside the noise**: a station whose two counts differ, the other accelerator's count lying in the
>     range the CPU's own replicates take (18 and 16 steps against 15 to 19; 14 stations of `seeded-fixtures`), is not counted in
>     the share and not in its denominator (`StepShareExcluded`). The ledger over the whole run: 35 of 400 725 stations differ,
>     share 8.73e-5 against 1e-3.
>   - **A curved remnant**: the guard on the one-sided differences of the balance-remnant correction was 2e-2 flat. A smooth
>     remnant's two differences disagree by one half of `κ h` (h = 1e-8); at κ 4.3e6 that is 2.15e-2, above 2e-2, and the
>     correction was dropped, leaving 1.9e-8 on x(CO) and x(O2) of `seeded-bracket-calcite-p1e4` (the 0.1 states). The guard is
>     now `min(6e-2, max(2e-2, κ h))`: `κ h`, twice the curvature share, admits the smooth case; the cap, half of the smallest kink
>     measured (0.12, the threshold-flip cases), keeps every kink out, so the 19 species dropped on 2026-10-03 stay dropped.
>     ⚠ 2026-10-04: was a flat 2e-2, now `max(entry, κ h)` capped at 6e-2 → HISTORY.md#guard-curvature
>   - Red once, 2026-10-04, `ComparisonRuleTests` with each rule alone broken (the enthalpy bound at zero, `DataEffect` answering
>     null, `NoiseFactor` at 0, the guard at its entry): one fact fails each time.
>   Open for the solver, not for this node: condensed amounts and some gas fractions in the interior of a reaction plateau converge
>   to about 1e-9 on the CPU accelerator alone; the rules above compare the accelerators to that floor and do not claim it is right.

> - `Execution.LibDevice` (`src/Execution/LibDevice/API.md`) — `LibDeviceLocator`,
>   `LibDevicePostLink` and `CudaWslDevices`, in the discovery, post-link and WSL facts (a
>   child node of `Execution`, 2026-10-01).

> Outside the tree: xunit; ILGPU 1.5.3; an NVIDIA GPU with driver, libnvvm and
> libdevice for the CUDA category.

> batches, and the mole-fraction floor and polish-threshold tier of the tolerance table.

> - The PTX fixtures (2026-09-26) are ILGPU 1.5.3's PTX of `Kernels.Probe`, taken before
>   the post-link from a `PTXBackend` for SM_89 and for SM_120 with libnvvm 13.4 on the
>   reference machine. They are text, generated once and committed with a header comment
>   naming ILGPU, libnvvm, the architecture and the date. They are inputs of the
>   inventory, not expected values: the facts assert only what the root's math list and
>   the regime imply (which wrappers are called, whether they are defined). They are
>   regenerated when ILGPU is upgraded, which the version assertion already forces to be
>   a deliberate act, or when `Kernels.Probe` itself changes shape.
> 
>   ⚠ 2026-09-28: was the PTX fixtures still showing the probe before 2026-09-27, now
>   regenerated from the current `Kernels.Probe` (14 outputs) →
>   HISTORY.md#ptx-fixtures-stale-2026-09-28

>   - The comparison counts NaN on both sides as equal and compares ±0 and ±∞ exactly,
>     with the sign.
>   - The CPU accelerator must still reproduce `System.Math` bit for bit on every input.

> - Do not loosen the GPU/CPU tolerance for green: a divergence is a finding about
>   math functions or code generation and gets a design session. The second tier of
>   the mole-fraction entry is not such a loosening: it is derived from the solver's
>   stopping rule and guarded by the bound on the share of stations it applies to.

---

<a id="throughput-per-iteration-2026-10-04"></a>

## 2026-10-04 - from "## Invariants", the approved throughput file - the per-iteration check and the record of 2026-09-19

Coder 7 of 0.2.2. The merged branch of coder 6 (d38e3f89) made the rocket sweep's CUDA kernel 0.222 s against 0.178 s: a loop-live `IterationState` passed by `ref` to the `NoInlining` `TraceGasPass.Run` made the local address-taken, and the whole Newton loop accessed it through a generic pointer that may alias every view store (PTX generic loads 12 669 to 20 200). The ratio fell to 18.09 to 18.60, below the floor of 0.8 times the approved 23.58 (18.86); but the check that held the file was a ratio, which moves with the CPU accelerator's load and with the transfer overhead (CUDA seconds 0.151 at the record, 0.249 today), so it cannot tell a slower kernel from a busier CPU, and a floor lowered by an honest re-approval would have let the regression through. The kernel time per Newton step does: with the fix (`EquilibriumSolver.Solve` and `StationSolve.At` pass copies) the kernel was 0.175 and 0.176 s over two quiet runs, 7.820e-08 and 7.849e-08 s per step at 22.403 steps per case (sum of `RocketBatchResult.Iterations` over the stations, per case; deterministic), and d38e3f89 measured 1.023e-07 s per step, 130.3 % of the approved figure, where 0.2.0 to 0.2.1 differed by 4 % and run-to-run variation is under 2 %.

The Windows record of 2026-09-19 (ratio 23.58, CUDA 0.151 s, CPU accelerator 3.557 s, kernel 0.132 s) does not reproduce even at its own commit: v0.1.0 measures 19.7 to 21.2 on the same machine today (the investigator's runs, 2026-10-04), because the transfer overhead around the kernel grew. `Throughput.approved.txt` was re-approved from the second of two quiet Release runs of 2026-10-04 (ratio 21.28 and 21.29, 20.71 in the first, with 25 % background CPU load from other sessions). The record stood:

> cuda_seconds: 0.151
> cpu_seconds: 3.557
> ratio: 23.58
> cuda_kernel_seconds: 0.132
> date: 2026-09-19

The bullet stood:

> - **The approved throughput file is a tripwire**: a run writes `Throughput.actual.txt`
>   next to it; the test fails when the ratio falls below the approved one by more than
>   20 % or below 5×.

`Throughput.linux.approved.txt` was not re-measured (no WSL measurement from the coder's session): it carries no per-iteration line, so the fact fails there with a message naming the file until the orchestrator re-approves it under WSL.

---

<a id="left-out-states"></a>

## 2026-10-04 - from "## Invariants" - states left out of the families for their element balance

Moved by the leftovers commit of 0.2.2 (coder 6): the solver's close is relative (`1e-13 · b_i`, Equilibrium `BOOT.md`), the CPU accelerator closes every one of the states below to the table's bound, `BatchTests.TheLeftOutStatesStillExceedTheClosureBound` went red as it was written to, and the states went back into their families (`FixtureBatches.LeftOutTables` and `GasPlateauFamilies`' `LeftOut` are gone, the fact is retired). The solver fix was chosen, not a bound by conditioning. The bullet stood:

> - **States left out of the families for their element balance** (2026-10-04, the findings of the
>   seeding work; the closure bound of 1e-13 is not loosened). The CPU accelerator ends `Ok` and
>   closes the balance only to 7.5e-13 (the 17-element table's one case, 1.2e-12 at half pressure; 17
>   elements, many condensed phases) and to between 1e-11 and 1.5e-11 (carbon, `ρ`; 4e-12 to 6e-12 for
>   oxygen) on the hp states of magnesite at 1e4 and 1e6 Pa, cold as well as seeded (sp states and
>   calcite close to 1.5e-14). They are left out by name (`FixtureBatches.LeftOutTables`,
>   `GasPlateauFamilies`' `LeftOut`) and `BatchTests.TheLeftOutStatesStillExceedTheClosureBound`
>   keeps each a finding: it goes red when the solver closes it, the time to put it back. The owner
>   decides between a solver fix and a bound by conditioning; the magnesite band is the trace-gas
>   work of `Recovery`'s `BOOT.md`.

---

<a id="guard-curvature"></a>

## 2026-10-04 - from "## Invariants" - the guard of the balance-remnant correction

The wording before the change, in the paragraph of the balance-remnant correction: "a species gets none when the two one-sided
differences of its `ln x` disagree by more than 2e-2 of its largest `|D_ij|` (or of 1 where that is smaller). The bound is
`GpuCpuTolerances.Entries["sensitivityDisagreement"]`". Why it was wrong: a smooth remnant's two one-sided differences disagree
by one half of `kappa h` of its largest derivative, which at kappa 4.3e6 and h 1e-8 is 2.15e-2, above the flat 2e-2, so the
correction of the remnant x(CO) and x(O2) of `seeded-bracket-calcite-p1e4` was dropped and 1.9e-8 stayed in the comparison. Found
by the CUDA run of de7cda2f; the measurement is in `## Invariants`, "A curved remnant".

---

<a id="l2-every-family-2026-10-04"></a>

## 2026-10-04 — from "## Purpose" — the L2 row's claim "every fixture family"

The claim was wider than the code: the `seeded` kind (30 cases, 2026-10-03) had never run in a batch, because no batch could carry a seed, and of 151 tp, hp and sp fixtures only the `lox-lh2_of6_pc7MPa` family (CPU), the `lox-rp1_of2.6_pc10MPa` family (CUDA) and the five named 0.2.1 families were batched. Now every equilibrium fixture table is a family on both accelerators and the seeded kinds run through seeded batches. The row as it stood:

> | L2 | every fixture family, the computed gas-plateau families of 0.2.2 (`GasPlateauFamilies`) and a 100 000-case sweep on CUDA equal the CPU accelerator; the CPU accelerator equals the numerical nodes called case by case; determinism of two runs; chunking gives the same result as one chunk; the species-function batch against the host functions and across accelerators; the element balance of every compared station closes to 1e-13 and the equilibrium families are compared with the balance-remnant correction (2026-10-03, `BalanceRemnantTests`) | the CPU accelerator and the host calls; reflection-enumerated fields (`BatchTests`, `CudaTests`, `SpeciesFunctionTests`) | ✅ |

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

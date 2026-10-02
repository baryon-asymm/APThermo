# HISTORY.md — src/Equilibrium

Append-only store of what `BOOT.md` no longer needs to state as current truth
(`AGENTS.md`, §15). Newest entry first. Every entry names the date it was moved, the
`BOOT.md` section it came from, and carries the original text in full, unedited. The
start procedure (`AGENTS.md`, §10) does not read this file; it is reached only by
following a dated pointer left in `BOOT.md` at the place the text used to stand.

---

<a id="crit-greps-split-2026-10-02"></a>

## 2026-10-02 — from "ACCEPTANCE.md" — the two grep criteria before the child nodes

Moved because the node was split into the child nodes `Newton`, `Condensed` and `StateRecord` (`AGENTS.md`, §15; the arbiter's verdict D of 2026-10-01): `src/Equilibrium/*.cs` no longer reaches the files of the children, so both greps were re-run over `src/Equilibrium/**/*.cs` and the wording of the two criteria changed to say so. The original lines of both criteria follow.

> - [x] 2026-09-14 — The node decodes none of `Thermo`'s interval layout (F-AR-01): no
>       `IntervalStart`, `IntervalCount` or `IntervalBounds` in `src/Equilibrium/*.cs`
>       (grep empty), the record bounds asked of `SpeciesFunctions.RecordLow` and
>       `RecordHigh`; `BitSnapshotTests.EveryFixtureCaseGivesTheRecordedBits` unmoved. Red
>       once: `RecordHigh` returning the lower bound turned 29 fixture cases red.
>       → HISTORY.md#crit-interval
>
> - [x] 2026-09-15 — A record stood down by the anti-cycling rule stays out of play "for
>       the rest of it": `PhaseGeometry.Adjacent` and `PhaseGeometry.PhaseAt` test
>       `!SpeciesMarks.InPlay(scratch, k)`, not the raw `scratch.SpeciesActive[k] == 0`
>       (R-Equilibrium-1); no `SpeciesActive[` remains outside `SpeciesMarks.Of` and
>       `.Set`. Red before the fix, green after:
>       `PlateauTests.AStoodDownRecordIsNeitherAdjacentToNorFoundBesideItsInPlayPartner`;
>       `Bits.approved.txt` unchanged. → HISTORY.md#crit-stood-down

---
<a id="structure-split-2026-10-02"></a>

## 2026-10-02 — from "## Structure" — the stage list before the split into child nodes

Moved because the node was split into the child nodes `Newton`, `Condensed` and `StateRecord` (`AGENTS.md`, §15; the arbiter's verdict D of 2026-10-01): the three paragraphs below named every stage class of the node as standing in this directory and namespace, and were rewritten to name the classes that stay.

> Decided 2026-09-14 (the clean-code pass; the root's code-shape constraint). The node
> is one public facade over internal stage classes, all static and kernel-compatible,
> all in this directory and namespace, one class per file, sharing the existing view,
> scratch and result structs. Every floating-point expression keeps its present form
> and its present order of evaluation: the decomposition moves code, it does not
> rewrite formulas, and the bit snapshot of the tests node (the acceptance criteria
> below) is the proof.
>
> The other stage classes (`CaseSetup`, `Composition`, `IterationMatrix`, `DampedStep`,
> `ConvergenceTests`, `SingularRemedies`, `CondensedSet`, `PhaseGeometry`, `SpeciesMarks`,
> `ElementBalance`, `DerivativeSystem`, `MixtureProperties`, `FrozenTemperature`,
> `DenseSolver`, `TieSnapshot`) are internal, each described by the summary of its
> declaration → HISTORY.md#structure-table-rows
>
> The carriers of `Carriers.cs` (`IterationState` passed by `ref`, `SystemLayout`,
> `MixtureSums`, `Derivatives`, the enums `EstimateSource`, `DerivativeKind`, `SpeciesMark`,
> `ConvergenceVerdict`, and `SpeciesMarks`) are described by the summaries of their
> declarations → HISTORY.md#structure-table-rows

---

<a id="constraints-newton-split-2026-10-02"></a>

## 2026-10-02 — from "## Constraints" — the convergence-tests bullet before the split

Moved because the node was split into the child nodes `Newton`, `Condensed` and `StateRecord` (`AGENTS.md`, §15; the arbiter's verdict D of 2026-10-01): lines 105 to 138 of the document mixed rules of the Newton loop (the tests, the singular matrix's threshold, the polish) with the retention threshold and the change cap, which stay in the parent, and three of their lines straddled the boundary. The unchanged lines went to the child or stayed; the text below is the original of the whole range.

> - Convergence tests and control factor as RP-1311 chapter 3: the `λ` damping of
>   equations (3.1)–(3.3) with the two branches for species above and below the trace
>   threshold, the tests (3.5) and (3.6) on `Δln n_j`, `Δln n`, `Δln T`, the
>   condensed-species mole numbers and the element residuals. The retention threshold
>   has two stages, as the reference's `tsize`/`xsize` (2026-09-28; cea 3.3.4
>   `equilibrium.f90:60-64`, switched at 1293-1304): `ln(n_j/n) = −18.420681`
>   (`n_j/n = 1e-8`, the report's) until the first convergence of the case, then
>   `ln(n_j/n) = −25.328436` (`1e-11`) for the rest of the solve. Below the threshold a
>   gaseous species is held at zero in the sums and keeps its logarithm. The switch
>   recomputes the retained amounts and counts as a change of the retained set: the loop
>   must converge once more under the second stage before it may exit, so every `Ok`
>   has been converged under 1e-11. The switch happens once per solve, including a
>   warm start. The report stands for the last `Composition.Refresh` under the
>   second-stage threshold: since an `Ok` exit is never reached before the switch (the
>   paragraph above), every reported composition is the second-stage one, and a gaseous
>   species between 1e-11 and 1e-8 of the gas is reported at its converged amount, not
>   zeroed. `Composition` stays the one place the retention rule is applied, and the
>   stage is per-case state (`IterationState.RetentionSecondStage`, not the loop's own
>   bookkeeping struct: the flag must survive across the several `Converge` calls one
>   `Solve` attempt can make, and `NewtonLoopState` is rebuilt fresh at each of them).
>
>   ⚠ 2026-09-28: was the report zeroing species below 1e-8 in a separate step, now the
>   report stands for the last `Composition.Refresh` → HISTORY.md#report-zeroing
>
>   A singular matrix does not widen the threshold; the reference's widening to 80
>   (`1994-1995`) was measured by the second audit to add warm-versus-cold disagreements
>   and is not copied. Iteration cap: 50 Newton steps after the last change of the
>   condensed species set, and at most `MaxCondensedSetChanges` changes of that set per
>   case: three per slot of the condensed set, an inclusion, a forgiveness and a
>   stand-down for each of the `ScratchLayout.MaxCondensedInSolution` slots (24 today;
>   the constant is the number, this document only names it). After the
>   report's tests pass, up to six further steps polish the iterate until the largest
>   correction is below `1e-11`, so that the reported state is at rounding level and the
>   tolerance table measures the reference's convergence, not this node's.

---

<a id="warm-evidence"></a>

## 2026-10-01 — from "## Constraints" — condensed wording

Moved because `BOOT.md` was over its limit (`AGENTS.md`, §15, rules 2, 4 and 5 of the owner's decision of 2026-10-01): the inventory of the scratch (`ScratchLayout` holds it) and the evidence of the third pass of 2026-09-28 behind two sentences (the observation O1 figures, and the code testing only the Newton loop's status). The text as it stood:

> - Kernel-compatible C#: the per-case solve is a static method over views; the
>   per-case scratch (the four species functions, the logarithms and corrections of the
>   gaseous mole numbers, the iteration matrix, its right-hand side and row scales, the
>   species and element masks, the condensed set) is passed in as views sliced from
>   batch-sized buffers by the caller (`EquilibriumScratch.Slice`, `ScratchLayout`).
>
>   estimate (the nozzle does). A temperature estimate that is given for hp or sp and is
>   not finite and positive is `InvalidInput`, as for the frozen solve (the third pass
>   of 2026-09-28: +∞ and 1e-300 K gave `SingularMatrix` after no iteration, part 1,
>   observation O1).
>
>   A warm start that fails, with any status other than `InvalidInput`, falls back once
>   to the cold start of section 3.1, with the iterations of both attempts counted in the
>   case's total (2026-09-28). A failure found at the close counts as well: the
>   mixture window, the element invariant, the exit guard, a singular derivative system
>   and the state guard (the third pass of 2026-09-28; the code tested only the Newton
>   loop's status, while this sentence and `API.md` said "any status"). A cold start never falls back. The cold start takes no part
>   of the seed: for hp and sp it starts at 3 800 K, not at the previous solution's
>   temperature, since that temperature is part of the seed.

---

<a id="structure-decisions-2026-10-01"></a>

## 2026-10-01 — from "## Structure" — condensed wording

Moved because `BOOT.md` was over its limit (`AGENTS.md`, §15, rules 4 and 5 of the owner's decision of 2026-10-01). The decisions below keep their lead-ins and their rules; what the code's declarations hold (the values of the species mark, where the small pieces landed) is not restated. The text as it stood:

> - **The species mark.** `SpeciesActive` keeps its slot and gains named values,
>   `SpeciesMark { Absent = 0, Active = 1, ForgivenOnce = 2, StoodDown = 3 }`: a
>   stood-down record is `StoodDown`, no longer `Absent`, so the honesty guard reads
>   the mark instead of re-deriving element presence, and "in play" is one predicate
>   (`Active` or `ForgivenOnce`) instead of three spellings. The scratch layout is
>   unchanged; `API.md` records the domain.
>
> - **The flag arguments.** `isTp` and `isHp` come from `SystemLayout.Kind`; the
>   derivative flag becomes `DerivativeKind`; the element-balance flag becomes the two
>   named tests. `useMolesAsEstimate` stays on the public entry point: it is the
>   contract, and `EstimateSource` is its internal translation.
>
> - **The Newton loop holds no formula.** `NewtonIteration` keeps `Converge`: the step and
> polish counts, the order of the calls, the status. What it computes lives in three
> stages named after the sections of RP-1311 chapter 3 they implement: `DampedStep` (the
> multipliers and the gaseous corrections of (2.18), the control factor of (3.1)–(3.3),
> its application (3.4) with the temperature window), `ConvergenceTests` ((3.5) on the
> undamped corrections, (3.6) on Δln T with the element balance, and the polish test, as
> one verdict the loop reads) and `SingularRemedies` (section 3.6). Each named constant
> moves with the stage that uses it; the polish-step cap stays with the loop. The loop's
> coupling is the width of the data it carries and of the stages it calls, which no split
> removes, so `NewtonIteration` is this node's second composition root, its measured
> figure in its row. Every expression keeps its form and its order of evaluation, so the
> bit snapshot may not move. → HISTORY.md#s-newton
>
> - **The mark accessors moved to `SpeciesMarks`.** `CaseSetup` is "what a case needs
> before its first Newton step", but its mark accessors were used by every stage of the
> iteration and `CondensedSet` wrote through them (`StandDown`). `Of`, `Set` and `InPlay`
> (the renamed `Mark`, `Mark` and `InPlay`) now sit in `Carriers.cs` beside `SpeciesMark`;
> `CaseSetup` keeps the input validation, the initial marks and the two reductions of the
> input. → HISTORY.md#s-marks
>
> - **The `ref` carrier holds.** `IterationState` is passed by `ref` through every stage
> and the kernel compiler takes it (`KernelEqualityTests` of this node and of
> `Performance.Tests`). → HISTORY.md#s-settled
>
> - **The composition root fits.** `Solve` and `SolveFrozen` are plain sequences of stage
> calls under the root's 60 lines, so no exception is claimed for them.
> → HISTORY.md#s-settled
>
> - **The carriers are filled by name, not by position.** `MixtureSums` and `Derivatives`
> are structs whose fields are written at the one place that computes them and read
> through `in` afterwards, not readonly structs with a nine- and a five-parameter
> constructor: a carrier whose purpose is to remove the parameter hazard may not
> reintroduce it in its own constructor. `SystemLayout` stays readonly: its arguments are
> the shape of the system and it derives the rest. → HISTORY.md#s-settled
>
> - **Where the small pieces landed** (the last term of (2.59), `InSolution`,
> `LogPressure`, `InitialTemperature`, the frozen sums): the table above and the code hold
> it. → HISTORY.md#s-settled
>
> - **One behaviour changed, deliberately and invisibly.** `DerivativeSystem` restores the
> caller's condensed order before returning on every path, the singular one included;
> nothing in the tree reads that order afterwards (`Solve` rebuilds the set from
> `result.Moles` at every entry), so no result moves. → HISTORY.md#s-settled

---

<a id="structure-table-rows"></a>

## 2026-10-01 — from "## Structure" — condensed wording

Moved because `BOOT.md` was over its limit (`AGENTS.md`, §15, rule 4 of the owner's decision of 2026-10-01: no inventory the code holds). The responsibility column of the type table, except the two composition roots, and the carriers paragraph are replaced by pointers to the summaries of the declarations. The text as it stood:

> | `CaseSetup` | input validation, the element mask, the initial species marks, the active-gas count, the initial estimates (the defaults or a previous solution) | internal |
>
> | `Composition` | the four species functions at the case temperature; the retained gaseous moles (the trace rule, one place); the mixture sums the system and the state need (`MixtureSums`) | internal |
>
> | `IterationMatrix` | the reduced Newton system of RP-1311 tables 2.1 and 2.2, one method per row family (the gaseous contributions, the total-moles row, the element rows, the condensed rows, the temperature row), accumulated in the present order | internal |
>
> | `DampedStep` | the multipliers and the gaseous corrections of (2.18), the control factor of (3.1)–(3.3), the application (3.4), the temperature update and its range check | internal |
>
> | `ConvergenceTests` | the tests (3.5) and (3.6) with the element balance, and the polish test, as one verdict | internal |
>
> | `SingularRemedies` | the remedies of section 3.6: the reset of vanished gaseous species, then the removal of the last condensed record | internal |
>
> | `CondensedSet` | membership of the condensed records between convergences: removal of a negative record, the range rule with pinned pairs, switching and stand-down, the inclusion test with the anti-cycling skip, the honesty guard of an `Ok` exit; `InclusionGain` is the one source of the section 3.4 gain, used by the test and by the guard | internal |
>
> | `PhaseGeometry` | where two records of one formula meet: the record bounds as `Thermo` answers them, adjacency, the crossing `T*`, the effective range, the partner in the solution | internal |
>
> | `SpeciesMarks` | the mark accessors (`Of`, `Set`, `InPlay`), used by every stage that reads or writes a species' mark, `CondensedSet` and `CaseSetup` included | internal |
>
> | `ElementBalance` | the abundance `Σ a_ij n_j` of an element in the composition (one place, used by the matrix's residual `b_i° − Σ a_ij n_j` and by both tests) and its two tolerance tests, as two named methods | internal |
>
> | `DerivativeSystem` | the derivative system of section 2.5 at the converged composition, the two right-hand sides (`DerivativeKind`: temperature, pressure), the pinned-pair representative, the reaction sum of (2.59); returns `Derivatives` | internal |
>
> | `MixtureProperties` | the state record: the assignments common to both paths written once, then the frozen closure or the equilibrium or pinned closure | internal |
>
> | `FrozenTemperature` | Newton on the temperature at a fixed composition, to the frozen test, with its own step cap | internal |
>
> | `DenseSolver` | contract unchanged; `Solve` split into scaling, elimination and back substitution | internal (2026-09-15, distribution phase), contract unchanged |
>
> | `TieSnapshot` | rule A's way back (the third pass of 2026-09-28, finding F1): saves and restores the gaseous logarithms, the condensed set with its mole numbers, and the Lagrange multipliers of the tied converged iterate a release starts from | internal |
>
> ⚠ 2026-09-15: was six types public, now internal with grants → HISTORY.md#visibility
>
> Carriers (`Carriers.cs`): `IterationState`, the per-case state carried between the
> stages (temperature, `ln n`, the condensed count, the temperature the functions were
> evaluated at, the step and set-change counts, the switched-out and removed-for-range
> memories), passed by `ref`, or returned by value should the kernel compiler refuse a
> `ref` struct, which the kernel-equality test decides; `SystemLayout` (the unknown
> count, the stride, the rows of the total-moles and temperature equations, the
> problem kind); `MixtureSums`; `Derivatives`; the enums `EstimateSource`,
> `DerivativeKind`, `SpeciesMark` and `ConvergenceVerdict` (added 2026-09-14 with the
> Newton-loop split below, the verdict `ConvergenceTests` returns and `NewtonIteration`
> reads); `SpeciesMarks` beside `SpeciesMark` (added 2026-09-15, the repair review's
> R-Equilibrium-6, below).

---

<a id="pointers-one-line-2026-10-01"></a>

## 2026-10-01 — from "the ⚠ pointer paragraphs of all sections" — condensed wording

Moved because `BOOT.md` was over its limit (`AGENTS.md`, §15, and the owner's rule of 2026-10-01 that a pointer takes one line). Each pointer paragraph below kept both wordings, shorter, and its anchor. The text as it stood:

> ⚠ 2026-09-15: was "section 2.6" for every derivative of the matrix solutions, now
> sections 2.5 and 2.6 → HISTORY.md#sec-2-5
>
>   ⚠ 2026-09-12: was a zero abundance `InvalidInput`, now an absent element is a mask
>   → HISTORY.md#absent-element
>
>   ⚠ 2026-09-28: was one retention threshold for the whole solve, now two stages, 1e-8
>   then 1e-11 → HISTORY.md#two-stage
>
>   ⚠ 2026-09-14: was at most 10 changes of the condensed set, now the constant
>   `MaxCondensedSetChanges` → HISTORY.md#set-changes
>
>   ⚠ 2026-09-12: was equation (3.1) read symmetrically, now only growing species enter
>   the maximum → HISTORY.md#lambda-growing
>
>   ⚠ 2026-09-26: was no rule for a record at the gas data floor, now the open-below rule
>   → HISTORY.md#open-below-missing
>
>   ⚠ 2026-09-28: was ice held at any temperature below its range, now bounded by the
>   mixture window → HISTORY.md#ice-window
>
>   ⚠ 2026-09-28: was removing the last condensed species, now the species the failed row
>   picks → HISTORY.md#singular-removal
>
>   ⚠ 2026-09-28: was the targeted removal alone, now rules B and A before it
>   → HISTORY.md#rules-ab
>
>   ⚠ 2026-09-13: was the range rule keeping both records within 50 K and removing after
>   convergence, now pinned pairs at `T*`, the switch memory and anti-cycling
>   → HISTORY.md#range-rule
>
>   ⚠ 2026-09-28: was only the mole numbers of a frozen case validated, now the
>   temperature too → HISTORY.md#frozen-temperature
>
>   ⚠ 2026-09-26: was only the gaseous mole numbers of a frozen case checked, now the
>   condensed ones too → HISTORY.md#frozen-moles
>
> ⚠ 2026-09-15: was six types public, now internal, with `InternalsVisibleTo` grants
> → HISTORY.md#visibility
>
> ⚠ 2026-09-14: was `NewtonIteration.Converge` named the largest method at 56 lines, now
> the Newton-loop split left it at 55 → HISTORY.md#s-largest
>
> ⚠ 2026-09-28: was the `EquilibriumScratch` row at 12 parameters, now 16 (rule A's tie
> snapshot) → HISTORY.md#ce-scratch16
>
> ⚠ 2026-09-28: was the rest "10 or below", now 11 (`DerivativeSystem`, rules A and B)
> → HISTORY.md#ce-rules-ab
>
> ⚠ 2026-09-28: was `DerivativeSystem` at 12, now 11 (no `ElementCoupling.Coupled` call)
> → HISTORY.md#ce-third-pass
>
> ⚠ 2026-09-26: was `NewtonIteration` at 17, now 18 (`NewtonLoopState`)
> → HISTORY.md#ce-newton-18
>
> ⚠ 2026-09-28: was the roots at 22 and 19, now 24 and 20, the rows above
> → HISTORY.md#ce-roots

---

<a id="crit-second-audit"></a>

## 2026-10-01 — from "## Acceptance criteria" — the second audit (condensed wording)

Moved because `BOOT.md` was over its limit (`AGENTS.md`, §15); the paragraph was rewritten shorter in `BOOT.md` with its meaning unchanged, and the text as it stood:

> - [x] 2026-09-30 — The second hidden-defect audit of 2026-09-28 (Thermo and Equilibrium, findings
>       F1 to F5, and the guards part's O8 and F9) is closed by the rules of that date
>       under Constraints.
>
>       **Done, this pass:** the two-stage threshold's fixtures and unit fact (F1,
>       below), the report-stands-for-`Composition.Refresh` correction (F1, `API.md`),
>       the audit's sixteen regression states and the eleven of example 5 named as
>       failing at both commits (F1, below), the targeted singular remedy's
>       dense-solver entry and anti-cycling fact (F5), the mixture window and state
>       guard (F2), the fallback on any failure (F3), frozen validation (F4), the NaN
>       element guard (guards O8), and the bit and approved-output re-approvals across
>       every node the change touches (Bits, below). The NaClO4 and AP/HTPB/Al fixtures
>       of F1, and the AP/HTPB/Al convergence claim of F5, were open on 2026-09-28 and are
>       closed by the criterion of rules A and B below, with the third pass's criterion
>       after it.
>
>       ⚠ 2026-09-30: the criterion stood unticked with an "Open" list after the rules
>       A and B criterion below closed that list, so nothing showed the audit closed.
>       Evidence at `9284418` and after, on the reference machine: `dotnet build
>       APThermo.sln` 0 warnings, 0 errors; the fast suite green on Windows and under
>       WSL2 (5 436 facts under WSL2, Equilibrium 944 of 944); the protocol lint 0 and 0;
>       `dotnet test tests/Execution.Tests -c Release` on CUDA 171 of 171 on Windows and
>       170 of 170 under WSL2, the 100 000-case sweep and every architecture included;
>       the release job's filter (`Category=Cuda|Category=BitSnapshot`) green on Windows
>       in Release. Known and outside the criterion, by the owner's decision of
>       2026-09-28: the three classes at the end of this section, for 0.2.1.
>       - **The two-stage threshold (F1).**
>         - New tp fixtures from cea 3.3.4 through the fixtures node's generator: RP-1311
>           example 5's table at 300 K, 1 bar and 70 bar, and 305 K, 1 MPa. Covered by
>           `AssignedTemperatureCasesReproduceTheReference` through its directory
>           listing; each red at `5a732f0`; all three `Ok` and green (`tests/Fixtures/cases/tp/rp1311-example5_T300_p1bar.json`,
>           `..._T300_p70bar.json`, `..._T305_p10bar.json`, `tests/Fixtures/generate/retention_threshold.py`).
>         - **NaClO4 and AP/HTPB/Al: under investigation by the orchestrator
>           (2026-09-28).** The orchestrator's task also asked for a NaClO4
>           decomposition (Na:Cl:O = 1:1:4 from pure elements, since NaClO4 is not a
>           thermo.inp reactant) at 500 K and 800 K, 1 bar, and AP/HTPB/Al at
>           7 MPa/430 K and 1 MPa/420 K. Both were generated with cea (which converges
>           on every one of the four) and tried against this node; neither is
>           committed, and both stay out of the fixtures while the orchestrator has
>           them investigated separately against the reference (2026-09-28) and
>           decides after that. What this pass found, for that investigation:
>           - NaClO4 reduces almost entirely to `NaCL(cr)` + `O2` at both temperatures
>             (cea's own mole fractions: 0.667 `O2`, 0.333 `NaCL(cr)`, every gaseous
>             trace at 1e-17 or below). `CaseSetup.FromDefaults`'s cold start is
>             gas-only (RP-1311 section 3.1, unchanged by this audit), and the
>             condensed-species inclusion test (`CondensedSet.Update`) runs only after
>             `NewtonIteration.Converge` returns `Ok`; here the all-gas trial never
>             converges (`SingularMatrix` at iteration 22, `failedRow` an element row,
>             `state.CondensedCount == 0`, before `RetentionSecondStage` is ever
>             reached), so no condensed candidate is ever tried. Whether the inclusion
>             test should run on a stalled or singular gas-only trial, and if so under
>             what rule, is a design question above this task.
>           - AP/HTPB/Al at 430 K/7 MPa and 420 K/1 MPa converges at the Newton level
>             repeatedly (11 changes of the condensed set, oscillating between 3 and 4
>             species in solution, `RetentionSecondStage` already true throughout) and
>             `CondensedSet.Update` settles (no further change), but
>             `CondensedSet.ExitGuardFindsAPositiveCandidate` then finds a `StoodDown`
>             candidate still showing a positive inclusion gain in the settled
>             composition, so `Close` returns `NotConverged`. The exit guard is
>             written to count a stood-down candidate on purpose (the first audit's
>             finding 2, `## Structure` above), so this is not the guard
>             double-counting; it is the inclusion test (`Update`, during the loop) and
>             the exit guard (`ExitGuardFindsAPositiveCandidate`, at `Close`) reaching
>             different verdicts on the same settled composition after a candidate has
>             cycled in and out enough times to be marked `StoodDown`. Reconciling them
>             needs a decision on what a permanently excluded but still-wanted
>             candidate means for the case's status, which is beyond a targeted fix.
>
>           Both symptoms are reproducible from `tests/Equilibrium.Tests/HostSolver`
>           on the compositions above (Custom pure-element Na/Cl/O 1:1:4 for the first;
>           `plateaus.REACTANTS`/`MASS_FRACTIONS` at the stated tp for the second); the
>           orchestrator or a design session should decide whether to accept the gap,
>           narrow the fixture request, or open a follow-up task naming the inclusion
>           test's and the exit guard's relation to a stood-down candidate as its own
>           design question.
>         - A unit fact on the loop's struct: the switch to the second stage counts as a
>           change of the retained set, and an exit needs a convergence after it.
>         - The report reflects the case's own last `Composition.Refresh` (the ⚠
>           2026-09-28 correction above): a species between 1e-11 and 1e-8 of the gas,
>           previously zeroed by a separate 1e-8 report step, is now reported at its
>           converged second-stage amount, which is why the Bits bullet below expects
>           this node's snapshot to move on existing fixtures, not stay at their zeros.
>
>         - **The audit's sixteen regression states.** The orchestrator supplied the
>           states directly (2026-09-28: `Z2Scan2.cs`/`Z2Verify.cs` of the audit's own
>           harness, read at the orchestrator's word that they are data for this node's
>           tests, not a foreign node's code) — four states on example 5's own table
>           (its committed `hp` fixture's table and element moles, solved as tp: 1 bar
>           at 300 and 310 K, 1 MPa at 320 K, 7 MPa at 340 K) and twelve on the
>           AP/HTPB/Al chamber's own table (its committed `tp` fixture's: 1 MPa at 300,
>           305 and 310 K; 7 MPa at 300 to 330 K every 5 K, plus 335 and 350 K), none of
>           them a committed fixture file of its own.
>           `tests/Equilibrium.Tests/RegressionStateTests.cs`,
>           `TheAuditsRegressionStateConvergesAndHoldsTheEquilibriumConditions`, checks
>           every one of the sixteen with this node's own equilibrium conditions, not
>           the reference: element conservation at the invariant tolerance, every
>           retained gas at its own chemical potential (Σ a_ij π_i), and no absent
>           condensed candidate in its effective range with a positive inclusion gain
>           (`PlateauTests`' own rule, run here on states no fixture covers). Confirmed
>           red at `5a732f0`: a temporary probe against a `git worktree add … 5a732f0`
>           checkout, discarded after the reading, found all sixteen `NotConverged`
>           there; all sixteen are `Ok` and clear of every condition after the fix.
>           `TheAuditsElevenExample5StatesThatFailedAtBothCommitsAreOkNow` covers the
>           eleven of example 5's states the audit's own scan (`scan2_old.txt`) named
>           as failing at both commits it compared (1 MPa at 300, 305 and 310 K; 7 MPa
>           across the whole 300–335 K band, every 5 K): every one is `Ok` today: none
>           is left unasserted or merely named.
>       - **The targeted singular remedy (F5).** The dense solver's new entry returns the
>         failed row, a unit fact on a constructed singular matrix
>         (`tests/Equilibrium.Tests/DenseSolverTests.cs`,
>         `TheFailedRowOverloadNamesTheRowWhosePivotVanished` and
>         `TheFailedRowOverloadReportsNoFailureOnARegularMatrix`). The claim "the
>         AP/HTPB/Al states at 420–450 K, 1–7 MPa converge" is not demonstrated by this
>         pass: see the F1 escalation above, which found two of those states
>         (430 K/7 MPa, 420 K/1 MPa) do not converge with the remedy as implemented. A
>         singular removal marks its record, a fact on the anti-cycling skip
>         (`NewtonLoopStateTests.ASingularRemovalOfACondensedSpeciesRestartsTheStepCountAndCountsTowardTheChangeCap`).
>       - **The mixture window and the state guard (F2).** A tp of the `h2-o2-of4` table
>         at 1 bar, 60 K to 159 K: `TemperatureOutOfRange`. At 160 K and above: `Ok`,
>         with finite positive Cp, Cv, `γ_s` and sound speed. The fixtures at 165–199 K
>         stay green. A fact over every `Ok` of the fixtures and of the audit's grids:
>         the guard's quantities are finite and positive, the pinned pair's
>         `Cp_eq = Cv_eq = 0` excepted.
>       - **The fallback on any failure (F3).** `WarmStartTests` extended to P/10, P/2
>         and T×1.1 over the fixture tables at 300 K and 600 K (the audit's six named
>         cases among them): every warm start whose cold solve is `Ok` ends `Ok` and
>         agrees with it. The step-cap trigger has a fact of its own (guards F9: no test
>         reached it). The hp and sp cold retry starts at 3 800 K.
>       - **Frozen validation (F4).** `InvalidInputTests` gains frozen tp at +∞, NaN and
>         1e-300 K (`InvalidInput` for the first two; `TemperatureOutOfRange` for the
>         third, below 0.8 of the gas floor), and `Solve` tp at +∞ (`InvalidInput`).
>       - **NaN in the element guard (guards O8).** `ElementBalance.WithinInvariant`
>         reads a NaN abundance as outside the invariant; a fact drives the method itself,
>         not the test node's own `Violations`.
>       - **Bits.** The two-stage threshold changes the final iterates, so bit snapshots
>         of this node and of the nodes that consume it (`Performance`, `Transport`,
>         `Problems`, `Cli`) move. The orchestrator decided on 2026-09-28 that this
>         coder re-approves them in the same commit, with a field-by-field report of the
>         largest relative change per field, every CEA tolerance test of the tree green,
>         and no status or iteration count changed except where the audit named the
>         case. The Linux files are recorded by the orchestrator under WSL. The
>         execution tests node's CUDA sweep and throughput tripwire run again on the
>         reference machine.
>       - `API.md` states the changes: the retention stages, the targeted remedy, the
>         window, the guard, the non-finite temperatures under `InvalidInput`, the
>         warm-start retry on any failure.

---

<a id="crit-third-pass"></a>

## 2026-10-01 — from "## Acceptance criteria" — the third audit pass (condensed wording)

Moved because `BOOT.md` was over its limit (`AGENTS.md`, §15); the paragraph was rewritten shorter in `BOOT.md` with its meaning unchanged, and the text as it stood:

> - [x] 2026-09-29 — The third audit pass of 2026-09-28 (part 1: findings F1 to F3,
>       observation O1) is closed by the rules of that date.
>       - **The way back from a release (F1).** The six salt states named in the ⚠ under
>         rule A's release: each `Ok`, with the audit's independent checks (element
>         residual ≤ 1e-12, gas chemical potentials ≤ 1e-6, no absent condensed candidate
>         in range with a gain above 1e-9). Red at `c02e14d` (`NotConverged`). The states
>         are inputs from the fixtures' own tables, not fixture files, unless the
>         generator can produce them; cea's own verdict on each is recorded beside the
>         fact. No state of the audit's salt sweep (the fixtures' salt and AP/HTPB/Al
>         tables and example 5, 300 to 1500 K in 10 K steps at 1e4, 1e5, 1e6 and 7e6 Pa,
>         cold and warm from each neighbour) goes from `Ok` to a failure; the count of
>         `NotConverged` falls from 36 by at least the six. The audit's probe:
>         `scratchpad/audit3/repo/tests/Equilibrium.Tests/ZAudit3*.cs`, outputs
>         `scratchpad/audit3/*.txt` (kept out of the tree).
>       - **The fallback covers the close (F2).** A warm start whose failure is found at
>         the close is retried cold; a fact with a seed that fails only at the close (the
>         audit found 31 `TemperatureOutOfRange` among 1147 warm starts over the 140
>         tp/hp/sp fixtures) shows the retry taken. Red at `c02e14d`.
>       - **No state on failure (F3).** The state guard decides before `State` is written:
>         a fact that a guarded failure leaves `State` untouched, as `API.md` states. The
>         stale comment of `Composition.cs` (a first-stage re-apply no caller makes) is
>         corrected.
>       - **The hp/sp estimate (O1).** +∞, NaN and −1 K as a *given* (nonzero) estimate
>         are `InvalidInput`; `API.md`'s `InvalidInput` clause lists it. A tp temperature
>         is always assigned and follows the same rule with no sentinel: 0 K and every
>         other non-positive or non-finite value are `InvalidInput` there too.
>
>         ⚠ 2026-09-28: this bullet, written the same day as the rest of the criterion,
>         listed "0" among the values an hp/sp estimate must refuse. `ColdRetryProblem`'s
>         warm-start fallback (`API.md`, "the fallback ... retries once from the cold
>         start") already relies on 0 as the documented sentinel for "no estimate given",
>         defaulting to 3 800 K (`CaseSetup.InitialTemperature`); refusing it would break
>         that fallback's own cold retry, which passes 0 on purpose. The implemented rule
>         checks the nonzero values only (`CaseSetup.Begin`'s `badEstimate` guard, gated
>         on `problem.Temperature != 0.0` for hp/sp), and `API.md`'s clause already states
>         the sentinel this way. Found while ticking this criterion, before any test used
>         the wrong wording as its expected behaviour.
>       - **Bits.** A moved snapshot is re-approved with the cases named and the largest
>         relative change per field; statuses move only on the states named here.
>
>       Evidence: `TieSnapshot.Save`/`Restore` (F1), the reordered `Solve`/`Close` and the
>       loop-local `awaitingRelease` restore-and-force-`Ok` path of `RunToConvergence` (F2),
>       the `bool`-returning `MixtureProperties.WriteEquilibrium`/`WriteFrozen` deciding
>       the state guard before `State[0]` is written (F3), and `CaseSetup.Begin`'s
>       nonzero-estimate check (O1) — every fix shown red once against `c02e14d` before
>       being accepted, then green:
>       - `tests/Equilibrium.Tests/TiedReleaseTests.cs`,
>         `TheReleasedTieRestoresAndClosesOk`: the six named salt states, each `Ok` and
>         clear of every independent equilibrium condition (element residual, gas
>         chemical potentials at 1e-6, no absent condensed candidate with gain above
>         1e-9 — `EquilibriumConditions.Violations`), with cea 3.3.4's own converged `G`
>         for each recorded in the test's own doc comment.
>       - `tests/Equilibrium.Tests/WarmStartTests.cs`,
>         `AFailureFoundAtTheCloseRetriesFromTheColdStart`: a warm start whose failure is
>         found only at the close (the mixture window, below `c02e14d`'s own Newton-loop
>         status) reports at least the fresh cold solve's own iteration count, proving the
>         retry ran.
>       - `tests/Equilibrium.Tests/MixturePropertiesTests.cs`: three facts against a
>         sentinel `MixtureState` and a NaN-`Cp` `MixtureSums`, proving `WriteEquilibrium`
>         and `WriteFrozen` leave `State` untouched on a guarded failure rather than
>         writing it and then discarding it.
>       - `tests/Equilibrium.Tests/InvalidInputTests.cs`,
>         `AGivenHpOrSpEstimateThatIsNotFiniteAndPositiveIsInvalidInput` (+∞, NaN and a
>         negative value, hp and sp, 6 cases) and
>         `AZeroHpOrSpEstimateStaysTheNoEstimateSentinel` (hp and sp, 2 cases): the given
>         estimate is refused, the sentinel is not.
>       - The audit's own salt sweep (its `Z2Scan3`-style harness over the salt tables,
>         300 to 1500 K by 10 K at 1e4, 1e5, 1e6 and 7e6 Pa, cold and warm from each
>         neighbour, copied in, run, then deleted, never committed): cold `NotConverged`
>         30 (down from the pre-fix baseline of 36 by exactly the six named states),
>         cold `Ok` 2390, warm `NotConverged` 2, warm `Ok` 4739, zero
>         equilibrium-condition violations over the whole sweep
>         (`scratchpad/audit3_verify/third_pass_sweep.txt`, kept out of the tree). The
>         thirty remaining `NotConverged` states are the already-declared threshold-flip
>         band (the ⚠ above under "The threshold flip", 29 of the same 968 states on the
>         same two fixture compositions) — none of them among the six named states, which
>         this sweep's own list of failures confirms by name.
>       - `APTHERMO_NO_CUDA=1 dotnet test APThermo.sln --filter "Category!=LongRunning"`,
>         run project by project in the foreground (the combined run exceeds the session's
>         tool-level time budget before completing, unrelated to this fix): `Data.Tests`
>         43/43, `Thermo.Tests` 1192/1192, `Equilibrium.Tests` 944/944, `Performance.Tests`
>         1418/1418, `Transport.Tests` 167/167, `Execution.Tests` 159/159 (CUDA forbidden),
>         `Problems.Tests` 1251/1251, `Fixtures.Tests` 34/34, `Protocol.Tests` 35/35 —
>         5243 of 5243, none skipped. `Cli.Tests` and `Docs.Tests` were not run to
>         completion this pass: both drive the command line through real `dotnet` process
>         launches per example (`CliFixture.Invoke`), which the session's tools could not
>         finish within a single foreground call; neither node's code, fixtures or
>         approved output is touched by this change (`git status` confined to
>         `src/Equilibrium` and `tests/Equilibrium.Tests`), and `Problems.Tests`' own
>         bit-for-bit fact — the layer `Cli` serializes — is unchanged, so no mechanism is
>         known by which either node's result would move. Left for the next session that
>         touches this node to confirm directly.
>       - `dotnet build APThermo.sln`: 0 warnings, 0 errors. Protocol lint: 0 errors,
>         0 warnings. No `Bits*.approved.txt` or `Throughput*.approved.txt` differs from
>         `main` anywhere in the tree (`git status`: only the files named in this pass's
>         commits changed).

---

<a id="warm-fallback"></a>

## 2026-10-01 — from "## Constraints" — the warm-start fallback on any failure

Moved because `BOOT.md` was over its limit (`AGENTS.md`, §15); the text as it stood:

>   ⚠ 2026-09-28: the fallback of 2026-09-27 (the acceptance criteria, the warm-start
>   criterion) fired only when a seeded condensed species was still negative at the
>   failure. A warm start also fails in two other ways the sign test does not see: the
>   iterate diverges with the seeded liquid positive (`ln n` growing by the damping cap to
>   the step cap, the liquid at thousands of kmol/kg), or the liquid goes negative and
>   comes back positive before the cap. The audit counted 29 warm tp restarts at P/10,
>   P/2 and T×1.1 of fixture tables at 300 K and 600 K ending `NotConverged` where a
>   fresh cold solve is `Ok` in 12 to 50 steps; retrying on any failure recovered all 29
>   and moved no other count. Its observation O3: the "cold" retry of an hp or sp kept
>   the caller's temperature estimate, which for a warm start is the previous
>   solution's. Found by the second hidden-defect audit of 2026-09-28 (Thermo and
>   Equilibrium, finding F3 and observation O3).

---

<a id="report-zeroing"></a>

## 2026-10-01 — from "## Constraints" — the report's own zeroing

Moved because `BOOT.md` was over its limit (`AGENTS.md`, §15); the text as it stood:

>   ⚠ 2026-09-28: stood "The report is unchanged: a gaseous species below 1e-8 of the gas
>   is reported with zero moles, a step applied to the final state only and nowhere in
>   the iteration." Implemented literally, as a `Composition.Retain` call at 1e-8 added
>   after `Close` had already computed the sums, derivatives and state from the
>   second-stage (1e-11) composition, it zeroed every trace species between 1e-11 and
>   1e-8 out of the *reported* moles only, while the sums, derivatives and mixture state
>   above them stayed the ones the finer composition produced. The two were then
>   inconsistent with each other: `ElementConservationTests` failed on 27 fixtures with
>   residuals of 9e-11 to 1.5e-9 (kmol/kg), matching the zeroed species' own mass, while
>   `ElementBalance.WithinInvariant` (evaluated on the pre-zeroing composition inside
>   `Close`, which is what an `Ok` status actually gates) never flagged the same cases,
>   and no fixture's `CaseStatus` or CEA-comparison result moved. A separate report step
>   the element-conservation invariant does not itself cover is a defect of the step, not
>   of the invariant's tolerance: the taboo against loosening a tolerance forbids
>   widening `ElementConservationTests`' 1e-12 to hide it. The report now stands for
>   whatever the last `Composition.Refresh` produced, with no separate zeroing step;
>   since that call is always the case's own active (second-stage, for any `Ok`)
>   threshold, a fixture's reported trace composition can only move toward the finer
>   value already used for its status and its derivatives, matching the reference (which
>   reports at the same threshold it converges to, to the printed digits). Found while
>   implementing this paragraph, second hidden-defect audit of 2026-09-28.

---

<a id="two-stage"></a>

## 2026-10-01 — from "## Constraints" — the two-stage retention threshold

Moved because `BOOT.md` was over its limit (`AGENTS.md`, §15); the text as it stood:

>   ⚠ 2026-09-28: stood "trace threshold `ln(n_j/n) = −18.420681` … as in the report",
>   one threshold for the whole solve. Together with the two rules above it turned
>   converging states into failures. In ammonium perchlorate products below about 350 K,
>   NH4CL(II) holds all of the N and Cl, leaving HCL, NH3 and N2 between 1e-11 and 2e-7,
>   on the threshold:
>   - retaining one carrier pushes another across 1e-8, so every step is a crossing and
>     the crossing rule overrides every passing verdict to the step cap (RP-1311
>     example 5 at 300 K, 1 bar);
>   - when the last carrier drops out of the sums, the N and Cl rows see only
>     NH4CL(II), the matrix is singular, the resets re-seed the carriers, and the
>     cleared polish count lets them fall back through the threshold, until the change
>     cap (AP/HTPB/Al at 7 MPa, 300 K, up to 1 907 steps).
>
>   16 tp states that were `Ok` at `9c33398` ended `NotConverged`; for example 5 the old
>   answer matches cea 3.3.4 to five digits. The reference retains down to 1e-11 after
>   its first convergence, where no carrier sits; the audit's clone with only that
>   change converged all 16 and 11 older failures, matched cea to the printed digits,
>   and lost no `Ok` in its fuzz. Found by the second hidden-defect audit of 2026-09-28
>   (Thermo and Equilibrium, finding F1); the two-stage rule was checked against
>   `equilibrium.f90` by the orchestrator.

---

<a id="ice-window"></a>

## 2026-10-01 — from "## Constraints" — ice below 200 K and the mixture window

Moved because `BOOT.md` was over its limit (`AGENTS.md`, §15); the text as it stood:

>   ⚠ 2026-09-28: stood "at any temperature the solver allows below its range". A tp has
>   no iterate window, so ice was held at any temperature. The `H2O(cr)` fit evaluated
>   below its 200 K bound gives `Cp°/R` −3.53 at 100 K and −44.5 at 60 K. With ice in the
>   solution a tp returned `Ok` with negative mixture Cp and Cv at 100 K and below, and
>   `γ_s < 0` with a NaN sound speed at 102.6 and 105 K. cea 3.3.4 returns "not
>   converged" at 60 to 150 K by the window above, whose floor the rule did not know.
>   Found by the second hidden-defect audit of 2026-09-28 (Thermo and Equilibrium,
>   finding F2); the window checked against `equilibrium.f90` by the orchestrator.

---

<a id="singular-removal"></a>

## 2026-10-01 — from "## Constraints" — the targeted singular remedy

Moved because `BOOT.md` was over its limit (`AGENTS.md`, §15); the text as it stood:

>   ⚠ 2026-09-28: stood "removing the last condensed species". On the products of
>   AP/HTPB/Al at 420–450 K and 1–7 MPa, `H2O(L)` is included beside `AL2O3(a)` and
>   `AL(OH)3(a)`, which are linearly dependent with it (2 Al(OH)3 = Al2O3 + 3 H2O). The
>   matrix is singular, the remedy removes `H2O(L)`, and the next inclusion puts it back,
>   every 9 steps, until the change cap: `NotConverged` in 8 states where cea 3.3.4
>   converges (7 MPa, 430 K: AL2O3(a), C(gr), H2O(L), NH4CL(II), no AL(OH)3(a)). The same
>   last-species rule removed `C(gr)`, which had nothing to do with the dependency, in
>   the singular cycle of the ⚠ above. Present since the first version; found by the
>   second hidden-defect audit of 2026-09-28 (Thermo and Equilibrium, finding F5).

---

<a id="release-way-back"></a>

## 2026-10-01 — from "## Constraints" — rule A's way back

Moved because `BOOT.md` was over its limit (`AGENTS.md`, §15); the text as it stood:

>       ⚠ 2026-09-28, the third pass: the release had no way back. On six salt states
>       (KClO4 at 930 K and 0.1 bar, 980 and 990 K at 1 bar, 1080 K at 10 bar, 1070 K at
>       70 bar; NaClO4 at 1050 K and 1 bar) the tie converged and polished with
>       {KCL(cr)} or {NaCL(cr)}. After the release, CL, K or Na and KO crossed the 1e-11
>       threshold on every step until the step cap: `NotConverged`, where 5a732f0 gave
>       `SingularMatrix`. With the release turned off all six end `Ok` with the same
>       composition, and pass the independent checks (element residual ≤ 1e-12, gas
>       chemical potentials ≤ 1e-6, no absent condensed candidate in range with a gain
>       above 1e-9). Found by the third audit pass (part 1, finding F1).

---

<a id="rules-ab"></a>

## 2026-10-01 — from "## Constraints" — the targeted removal did not close the AP/HTPB/Al states

Moved because `BOOT.md` was over its limit (`AGENTS.md`, §15); the text as it stood:

>   ⚠ 2026-09-28, the same day: the targeted removal above did not close the AP/HTPB/Al
>   states it was written for. At 7 MPa and 430 K, `H2O(L)` enters with gain +0.2105
>   beside `AL2O3(a)` and `AL(OH)3(a)`, and the matrix fails on its row. The removal takes
>   the smallest species sharing an element with it, `AL2O3(a)`, which is a product of the
>   favourable reaction. It does so twice, stands it down, and settles on
>   {AL(OH)3(a), C(gr), NH4CL(II)}. That set's Gibbs energy is 64.8 kJ/kg above cea's at
>   430 K and 57.9 above at 420 K, and the exit guard rightly returned `NotConverged`.
>   cea's state is the equilibrium: `AL(OH)3(a)` gains −0.3158 there, stable only below
>   415.948 K. Separately, NaClO4 and KClO4 (Na or K : Cl : O = 1:1:4, tp at 500 and
>   800 K) ended `SingularMatrix` in the all-gas trial. The polish drove the last
>   alkali carrier without Cl, `Na2O2`/`K2O2`, across the threshold, so the alkali and
>   Cl rows became identical, with no condensed species to remove. cea never meets a
>   singular matrix there. The last carrier sits at e^−30 to e^−70 at equilibrium, and a
>   switch of the threshold before the polish only moves the crossing (measured). Both
>   rules measured on a prototype:
>   - all six states `Ok`, within 7e-9 of cea;
>   - the audit's salt scan went from 1 132 to 1 436 `Ok` of 1 452, with no
>     `SingularMatrix` left;
>   - the AP scan went from 842 to 850 of 850;
>   - warm-start failures in the fuzz went from 13 to 0, with 6 warm/cold disagreements
>     at trace level, all inside `WarmStartTests`' tolerance;
>   - no committed fixture reaches either rule, so no bit moves.
>
>   A bare widening of the threshold to 80, the reference's remedy, fixed none of the six
>   states and added hundreds of warm/cold disagreements; it stays rejected.

---

<a id="frozen-temperature"></a>

## 2026-10-01 — from "## Constraints" — frozen mode: the temperature

Moved because `BOOT.md` was over its limit (`AGENTS.md`, §15); the text as it stood:

>   ⚠ 2026-09-28: only the mole numbers were validated. A frozen tp at +∞ or 1e-300 K
>   returned `Ok` with h, s, g, Cp, Cv, `γ_s` and the sound speed all NaN, and at 1e6 K
>   `Ok` with Cp −1.89e12 J/(kg·K). No caller of the tree passes a frozen tp, so nothing
>   reached it. Found by the second hidden-defect audit of 2026-09-28 (Thermo and
>   Equilibrium, finding F4). `Solve`'s tp check (`CaseSetup`) had the same gap and ended
>   in `SingularMatrix`; it now answers `InvalidInput` for a non-finite temperature too.

---

<a id="ce-scratch16"></a>

## 2026-10-01 — from "## Shape exceptions" — the scratch constructor row

Moved because `BOOT.md` was over its limit (`AGENTS.md`, §15); the text as it stood:

> ⚠ 2026-09-28 (the third pass, finding F1): this row stood at 12. Rule A's way back
> (the "Release" paragraph under Constraints) added four more slices — the tie
> snapshot's gaseous logarithms, condensed moles, multipliers and condensed set —
> raising the constructor's own parameter count to 16; `Slice` still names every
> argument, its one construction site. `ShapeTests.NoMethodTakesMoreThan6Parameters`
> and `EveryShapeExceptionIsMeasuredAndStillNeeded` found the stale row red;
> re-measured the same day.

---

<a id="ce-rest"></a>

## 2026-10-01 — from "## Shape exceptions" — the types below the limit (condensed wording)

Moved because `BOOT.md` was over its limit (`AGENTS.md`, §15); the paragraph was rewritten shorter in `BOOT.md` with its meaning unchanged, and the text as it stood:

> Every other type of the node measures 11 or below by the dependency check's walk
> (`DerivativeSystem` the highest of the rest, at 11 since it reads `state.Tie` for
> rule A's release, up from 10; `CaseSetup`, `CondensedSet`, `ConvergenceTests` and
> `SingularRemedies` tied at 10, the first two since the repair review moved the mark
> accessors into `CaseSetup`'s own dependencies 2026-09-15, R-Equilibrium-6), well below
> the root's limit of 14.

---

<a id="ce-rules-ab"></a>

## 2026-10-01 — from "## Shape exceptions" — rules A and B and the coupling figures

Moved because `BOOT.md` was over its limit (`AGENTS.md`, §15); the text as it stood:

> ⚠ 2026-09-28 (rules A and B): stood "10 or below" with `CaseSetup` and `CondensedSet`
> named as the highest of the rest. `DerivativeSystem` now reads rule A's tie from
> `IterationState` and calls `ElementCoupling.Coupled` to test whether it still holds,
> raising its own count from 10 to 12; `SingularRemedies` gained `CondensedDependency`
> and `ElementCoupling` (rule B and rule A's remedies), reaching 10, tied with `CaseSetup`
> and `CondensedSet`. All four stay well below the root's limit of 14; measured by the
> protocol tests node's own coupling walk the same day.

---

<a id="ce-third-pass"></a>

## 2026-10-01 — from "## Shape exceptions" — the third pass: DerivativeSystem's coupling

Moved because `BOOT.md` was over its limit (`AGENTS.md`, §15); the text as it stood:

> ⚠ 2026-09-28 (the third pass, finding F1): the paragraph above raised `DerivativeSystem`
> to 12 for its read of `state.Tie` together with its call into `ElementCoupling.Coupled`,
> which re-derived whether the tie still held. Rule A's way back (the "Release" paragraph
> under Constraints, and `## Structure`'s note on `DerivativeSystem.Solve` above) trusts
> `IterationState.Tie`'s own `Active` flag instead: the caller's release-and-restore keeps
> a tie active exactly at the composition the coupling test found untied, so the recheck
> undid the restore for no gain. Removing the call drops `DerivativeSystem`'s own count
> back to 11 (`SpeciesTableView`, `EquilibriumScratch`, `EquilibriumResult`,
> `IterationState`, `Derivatives`, `SystemLayout`, `ProblemKind`, `DerivativeKind`,
> `PhaseGeometry`, `ElementTie`, `DenseSolver`, by the same walk applied to the file by
> hand); it stays the highest of the rest, since `CaseSetup`, `CondensedSet`,
> `ConvergenceTests` and `SingularRemedies` are untouched by this fix and stay tied at 10.
> `ShapeTests.NoSrcTypeNamesMoreThan14TypesOfTheTree` was green both before and after,
> since 11 and 12 both stay well below 14; this correction is about the prose figure, not
> about a check moving.

---

<a id="ce-roots-22"></a>

## 2026-10-01 — from "## Shape exceptions" — the composition roots at 22 and 19

Moved because `BOOT.md` was over its limit (`AGENTS.md`, §15); the text as it stood:

> ⚠ 2026-09-28: `EquilibriumSolver`'s row stood at 19 and `NewtonIteration`'s at 18. The
> second hidden-defect audit's fixes named new types directly at both call sites:
> `EquilibriumSolver` now constructs the cold-retry problem and calls `FrozenTemperature`
> and `MixtureProperties.IsPhysical` from `SolveFrozen`, and reads `IterationState`'s new
> `RetentionSecondStage` flag from `Solve`'s retry loop, raising its count to 22;
> `NewtonIteration` now calls `EquilibriumSolver.RetentionThreshold` and reads
> `IterationState.RetentionSecondStage` directly, raising its count to 19. Both stay
> composition roots that hold no formula of their own; `ShapeTests.NoSrcTypeNamesMoreThan14TypesOfTheTree`
> and `EveryShapeExceptionIsMeasuredAndStillNeeded` found the stale rows red; re-measured
> the same day.

---

<a id="ce-roots"></a>

## 2026-10-01 — from "## Shape exceptions" — the composition roots at 24 and 20

Moved because `BOOT.md` was over its limit (`AGENTS.md`, §15); the text as it stood:

> ⚠ 2026-09-28, the same day (rules A and B): `EquilibriumSolver`'s row stood at 22 and
> `NewtonIteration`'s at 19. `EquilibriumSolver.RunToConvergence` now names `ElementCoupling`
> directly at the tie's release check, raising its count to 24; `NewtonIteration.Converge`
> now passes `state.Tie` into `SystemLayout`'s five-argument constructor, naming `ElementTie`
> where it did not before, raising its count to 20. Both stay composition roots that hold no
> formula of their own; `ShapeTests.NoSrcTypeNamesMoreThan14TypesOfTheTree` and
> `EveryShapeExceptionIsMeasuredAndStillNeeded` found the stale rows red; re-measured the
> same day.

---

<a id="crit-rules-ab"></a>

## 2026-10-01 — from "## Acceptance criteria" — rules A and B (condensed wording)

Moved because `BOOT.md` was over its limit (`AGENTS.md`, §15); the paragraph was rewritten shorter in `BOOT.md` with its meaning unchanged, and the text as it stood:

> - [x] 2026-09-28 — Rules A and B (Constraints, the orchestrator's investigation 6 of 2026-09-28) close
>       the open items of the criterion above.
>       - [x] 2026-09-28 — **Fixtures through the fixtures node's generator**
>         (`tests/Fixtures/generate/retention_threshold.py`, driven by `regenerate.py`):
>         - NaClO4 and KClO4 at 500 K and 800 K, 1 bar, from pure elements (the
>           generator's new `Custom` pure-element reactants):
>           `tests/Fixtures/cases/tp/naclo4_T500.json`, `naclo4_T800.json`,
>           `kclo4_T500.json`, `kclo4_T800.json`.
>         - AP/HTPB/Al tp at 7 MPa and 430 K, and at 1 MPa and 420 K, on the chamber
>           fixture's table and element moles: `tests/Fixtures/cases/tp/ap-htpb-al_pc7MPa_T430.json`,
>           `ap-htpb-al_pc1MPa_T420.json`.
>         - All six shown red without rules A/B and green with them, by disabling the
>           two blocks of `SingularRemedies.Recover` in turn (`if (false && …)`),
>           rebuilding and rerunning `tests/Equilibrium.Tests`, then restoring: the salt
>           cases fail `SingularMatrix`, the AP/HTPB/Al cases fail `NotConverged`,
>           confirmed at this commit.
>         - Re-running `regenerate.py` re-provenanced the 322 existing fixture files
>           (`generatorSha256`, `scriptSha256`, `generatedOn`) with no output field
>           moved, confirmed by a `git diff` restricted to non-provenance keys.
>       - [x] 2026-09-28 — **Unit facts**, all green in `tests/Equilibrium.Tests`
>         (926/926): `SingularRemedyRulesTests.cs` —
>         - `ElementCoupling.Find`/`Coupled`: a coupled pair (N/Cl through NH4CL(II)
>           alone, ratio 1), an uncoupled pair (once `HCL` carries a nonzero mole), and
>           `HeldByCondensed` true only while the tying condensed species is in the
>           solution.
>         - `CondensedDependency.LeavingPosition`: the ratio test chooses `AL(OH)3(a)`
>           over `AL2O3(a)` at the AP/HTPB/Al products of 430 K (`H2O(L)` entering
>           last), and returns no leaving position for an independent three-species set.
>         - `WarmStartTests.AWarmStartFromExample5sTenBarSolutionTiesNAndClThroughNH4CLAndEqualsItsColdSolve`:
>           a warm start from example 5's 10-bar solution, which ties N/Cl through
>           NH4CL(II) at step 0, equals its cold solve. Honestly recorded: rule A's row
>           is exercised on this path but disabling it does not turn this particular
>           fact red (its doc comment says so); the fixture-level facts above are rule
>           A's and B's red-once evidence.
>       - [x] 2026-09-28 — **The scans as measurements**, recorded here, not asserted.
>         Run as temporary facts inside `tests/Equilibrium.Tests` (never committed: the
>         audit's own `Z2Runner.cs`, `Z2Check.cs`, `Z2Verify.cs`, `Z2Scan2.cs`,
>         `Z2Scan3.cs`, `Z2Fuzz.cs`, copied in, built at the tree's Diagnostics maximum,
>         run, then deleted) and independent equilibrium checks (element conservation,
>         every retained species at its own chemical potential, no excluded candidate
>         with a positive inclusion gain), not the reference:
>         - **The AP scan** (`Z2Scan2.ApTables`, example 5's and the AP/HTPB/Al
>           chamber's own tables, 280–700 K by 5 K at five pressures 1 kPa–7 MPa):
>           850/850 `Ok`, zero equilibrium-condition violations.
>         - **The audit's salt scan** (`Z2Scan3.SaltScans`, kclo4-rich, kclo4-lean,
>           naclo4 and ap-htpb tables, 300–1500 K by 10 K at three pressures):
>           1436 `Ok`, 16 `NotConverged`, 0 `SingularMatrix`, zero equilibrium-condition
>           violations. The 16 `NotConverged` states match the threshold-flip
>           limitation named below exactly (KClO4 610–680 K, NaClO4 490–500 K): this is
>           the same, already-known and already-declared gap, not a new one. Against
>           the audit's own pre-fix baseline (`scan3_old.txt`, kept with the audit's
>           reports): 332 `NotConverged` + 122 `SingularMatrix` of 1452 states: rules
>           A/B take the salt tables from a 31 % failure rate to 1.1 %, all sixteen
>           remaining failures inside the declared plateau bands.
>         - **The fuzz counts** (`Z2Fuzz.Fuzz`, every tp/hp/sp fixture, cold at varied
>           P/T/target/element moles and warm-started from each cold `Ok`): 40 985
>           cold and warm solves; every `Ok` clear of every equilibrium condition
>           except the diagnostic's own trace-threshold note (a converged species
>           reported below 1e-8 mole fraction rather than dropped to zero — not a
>           violation of an equilibrium or conservation condition, the check's own
>           margin), 0 Gibbs-residual, element-conservation, non-finite or
>           left-out-with-gain violations anywhere in the sweep. `tp:cold:SingularMatrix`
>           is 77, matching the three-element-coupling limitation named below exactly
>           (same count as the pre-fix baseline: unrelated to rules A/B, RP-1311
>           example 1/12 tables at 300 and 600 K). 41 warm/cold disagreements at
>           |Δx| just above the 1e-6 threshold (baseline: 34), every one of them
>           individually a valid equilibrium on both sides — a multiple-local-solution
>           artifact near a degenerate composition, not a violation.
>       - [x] 2026-09-28 — **No bit snapshot moves on an existing fixture.** Verified for
>         all three nodes whose `BitSnapshotTests` walk the fixture tree
>         (`tests/Equilibrium.Tests`, `tests/Thermo.Tests`, `tests/Problems.Tests`): a
>         sorted, CRLF-normalized diff of each node's `Bits.approved.txt` against its
>         freshly generated `Bits.actual.txt` shows six added lines only (the new fixture
>         keys above), and not one existing hash moved. The coder approved
>         `tests/Equilibrium.Tests/Bits.approved.txt`. The environment's permission layer
>         refused the coder's write to the other two files, which lie outside its subtree.
>         With the owner's word of 2026-09-28, the orchestrator approved them at the merge
>         (`74d0715`) from its own run, after the same additions-only comparison. Both
>         facts are green (`Thermo.Tests` bit facts 239/239, `Problems.Tests`
>         `EveryFixtureGivesTheRecordedBits`).
>       - [x] 2026-09-28 — **Shape.** No method over 6 parameters; the declared Ce rows
>         are re-measured (the ⚠ notes of this date under the Constraints above).
>       - [x] 2026-09-28 — `API.md`'s `SingularMatrix` sentence lists the remedies, with
>         a ⚠.
>
>       Open and known, measured by the investigation, outside this criterion. The owner
>       decided on 2026-09-28 that they do not block 0.2.0: `CHANGELOG.md` names them as
>       known limitations of 0.2.0, and they are designed and fixed for 0.2.1:
>       - **The threshold flip.** Two carriers cross the threshold alternately every
>         step, so the polish never completes: KClO4 at 610–680 K, NaClO4 at 490–500 K,
>         16 salt-scan states `NotConverged`. The matrix is never singular.
>
>         ⚠ 2026-09-28, the third pass (part 1, F1): on the fixtures' own compositions
>         (`naclo4_T500`, `kclo4_T500`) those bands are `Ok` at nine pressures from 1e3 to
>         2e7 Pa, while the flip strikes in the all-gas first stage at 1050 to 1360 K:
>         29 of 968 states over 300 to 1500 K at 1e4, 1e5, 1e6 and 7e6 Pa, failing at
>         `5a732f0` too. The limitation is the mechanism, not a band: `CHANGELOG.md`
>         states it for stoichiometric perchlorates between about 500 and 1400 K,
>         depending on pressure and composition.
>       - **The three-element coupling.** With only CO2, H2O and N2 retained, row O
>         equals 2·C + ½·H. 77 fuzz tp states on example 1 and example 12 tables at
>         300 K and 600 K end `SingularMatrix`, which a pair tie cannot express.
>       - **The reaction plateau.** hp inside the Al(OH)3/Al2O3/H2O(L) reaction plateau
>         (T* = 415.948 K, 157 kJ/kg wide at 7 MPa) ends `SingularMatrix` in the
>         derivative system: the pinned-pair convention covers two records of one
>         formula only.

---

<a id="condensed-limit"></a>

## 2026-10-01 — from "## Constraints" — the limit on condensed species in the solution

Moved because `BOOT.md` was over its limit (`AGENTS.md`, §15); the text as it stood:

>   ⚠ 2026-09-26: stood "at most 8 condensed species … a matrix of at most 30 × 30". The
>   limit sized the scratch and was below what a table of 20 elements can require. A
>   full set made the inclusion test return "no change", and the case closed `Ok` with
>   stable phases missing. The audit's case: 17 elements at 350 K, with `BaO(cr)`,
>   `CuO(cr)`, `KCL(cr)` and `NaCL(cr)` left out at gains from +52 to +221, and Ba and Cu
>   vapour at x = 0.0032. Found by the hidden-defect audit of 2026-09-26 (finding 2).
>   The scratch grows by at most `(42² − 30²) + 2·12` doubles and 12 ints per case, at
>   20 elements. The budget of set changes grows with the slots (`MaxCondensedSetChanges`
>   is three per slot, as before).

---

<a id="loop-bookkeeping"></a>

## 2026-10-01 — from "## Constraints" — the loop's bookkeeping

Moved because `BOOT.md` was over its limit (`AGENTS.md`, §15); the text as it stood:

>   ⚠ 2026-09-26: the verdict covered only the gases retained at the start of the step.
>   The mark and the polish count were never cleared, and the singular remedy's removal
>   did not restart the step count, although this bullet counts steps "after the last
>   change of the condensed species set". Found by the hidden-defect audit of 2026-09-26
>   (findings 3 and 4):
>   - A warm start from a converged solution at half the pressure ended `NotConverged`
>     after one step. On the `rp1311-example1` table at 1000 K, atomic H crossed the
>     threshold during an already-polished step, and the element guard then rejected
>     the state by exactly its 5.76e-10 kmol/kg. The case solves `Ok` from a cold start.
>   - A fixture case at tp 300 K (`rp1311-example5`'s table) passed the tests at step
>     19, lost them at 24 to a vanishing HCL, and exited unpolished when they passed
>     again at 38.
>
>   `API.md`'s sentence that a trace species' logarithm "stays in the scratch for the
>   next estimate" was true, but the next estimate never read it; it is corrected there.

---

<a id="open-below-measure"></a>

## 2026-10-01 — from "## Constraints" — the open-below rule, the measurement (condensed wording)

Moved because `BOOT.md` was over its limit (`AGENTS.md`, §15); the paragraph was rewritten shorter in `BOOT.md` with its meaning unchanged, and the text as it stood:

>   In the committed file this is `H2O(cr)` alone. Measured the same day, a tp of
>   H2/O2 (O/F 4) at 1 bar:
>   - cea 3.3.4 holds ice at 165, 180, 190 and 199 K, with vapour below 1e-6;
>   - this node reported supersaturated vapour as `Ok`, −12.5 MJ/kg against −14.9 with
>     ice.

---

<a id="open-below-missing"></a>

## 2026-10-01 — from "## Constraints" — the open-below rule was missing

Moved because `BOOT.md` was over its limit (`AGENTS.md`, §15); the text as it stood:

>   ⚠ 2026-09-26: the rule was missing. The hidden-defect audit did not find it; it came
>   up while its finding 1 was being checked against the same line of the reference.

---

<a id="frozen-moles"></a>

## 2026-10-01 — from "## Constraints" — frozen mode: the mole numbers

Moved because `BOOT.md` was over its limit (`AGENTS.md`, §15); the text as it stood:

>   ⚠ 2026-09-26: only the gaseous mole numbers were checked. A NaN condensed mole number
>   gave a tp `Ok` with h, cp and MW all NaN, and −0.01 kmol/kg of `H2O(L)` gave `Ok`
>   with MW 24.39. Found by the hidden-defect audit of 2026-09-26 (finding 5). The only
>   in-tree caller passes an `Ok` composition, so no result of the tree moved. Its state carries
>   `CpEquilibrium = CpFrozen`, `CvEquilibrium = CvFrozen`, the derivatives 1 and −1 and
>   `γ_s = Cp/Cv`.

---

<a id="ce-newton-18"></a>

## 2026-10-01 — from "## Shape exceptions" — NewtonIteration's row at 18

Moved because `BOOT.md` was over its limit (`AGENTS.md`, §15); the text as it stood:

> ⚠ 2026-09-26: `NewtonIteration`'s row stood at 17. The hidden-defect audit's loop
> bookkeeping fix (the audit's finding 3) added `NewtonLoopState` (Carriers.cs), a small
> kernel-compatible struct tracking steps-since-last-set-change, the converged mark and
> the polish-step count, and `Converge` now names it directly (`ref NewtonLoopState loop`)
> instead of holding that bookkeeping in loose locals; the walk counts the new type,
> raising the measurement to 18. `ShapeTests.NoSrcTypeNamesMoreThan14TypesOfTheTree`
> found the stale row red; re-measured the same day.

---

<a id="crit-audit-2026-09-26"></a>

## 2026-10-01 — from "## Acceptance criteria" — the audit's findings 1 to 5 (condensed wording)

Moved because `BOOT.md` was over its limit (`AGENTS.md`, §15); the paragraph was rewritten shorter in `BOOT.md` with its meaning unchanged, and the text as it stood:

> - [x] 2026-09-26 — The audit's findings 1 to 5 and the open-below rule (the ⚠ notes of
>       this date under Constraints, and the Thermo node's criterion of the same date for
>       finding 1).
>       - **Below 300 K and below 200 K.** Equilibrium fixtures computed by cea 3.3.4,
>         each covered by `Problems.Tests.EquilibriumTests.AssignedTemperatureCasesReproduceTheReference`
>         through its directory listing:
>         - Si and Li in argon at 298.15, 299, 299.99, 300 and 301 K (the Thermo
>           criterion);
>         - H2/O2 at O/F 4 and 1 bar at 165, 180, 190 and 199 K, with products H2, O2,
>           H2O, `H2O(cr)` and `H2O(L)`. Shown red on the unpatched open-below rule: 4 of
>           64 tp cases mismatch, `h2-o2-of4_T180`/`T190`/`T199` reporting supersaturated
>           vapour where the reference holds ice (`x(H2O(cr))` reference 0.504, tree 0;
>           `enthalpy` reference −14.998 MJ/kg, tree −12.453 MJ/kg).
>       - **The full set.**
>         - The audit's 17-element case at 350 K returns `Ok` with every stable phase in
>           the solution: cea 3.3.4 solves it whole, so it is a fixture too
>           (`tests/Fixtures/cases/tp/seventeen-elements-many-condensed-phases_T350.json`),
>           covered by the same reference test.
>         - `PlateauTests.AnOkSolutionLeavesNoCondensedCandidateWithPositiveInclusionGain`
>           ("an `Ok` solution leaves no condensed candidate with a positive inclusion
>           gain") now runs over every tp, hp and sp fixture (130 cases), not the hp ones
>           alone (44).
>         - The exit guard is shown red once: with `MaxCondensedInSolution` set back to 8,
>           `AssignedTemperatureCasesReproduceTheReference("seventeen-elements-many-condensed-phases_T350")`
>           ends `status NotConverged`, not `Ok`.
>       - **Warm starts.** `WarmStartTests.AWarmSolveAtHalfPressureAgreesWithAColdSolveAtThatPressure`
>         solves every tp fixture that converges `Ok` from a cold start again from its own
>         solution at half its pressure; the warm solve is `Ok` and agrees with a cold
>         solve at that pressure (65 cases, `ANamedFixtureCompletesTheFullWarmStartComparison`
>         pinning one by name so the theory cannot quietly skip every case). A cold solve
>         that itself fails at the arbitrary half pressure (no baseline to compare against,
>         4 of the `rp1311-example14` water-plateau cases) or a warm solve a plateau's
>         seeded pair makes singular (the ⚠ below) is skipped, not forced.
>
>         `TheAuditsExactCasesWarmStartOkAndAgreeWithAFreshColdSolve` reproduces the
>         audit's own three probe cases exactly, over the fixture's own table and element
>         moles, cold at the fixture's pressure times a factor and a given temperature
>         (the fixture's own enthalpy target for hp), warm from that solution at half that
>         pressure:
>         - `("tp", "rp1311-example1_r1.5_p0.01atm_T2000", 1.0, 1000 K)`;
>         - `("tp", "rp1311-example8_exit5", 0.1, 1000 K)`;
>         - `("hp", "rp1311-example8_exit3", 100.0, the fixture's own enthalpy target)`,
>           the warm temperature estimate being the cold solution's own converged
>           temperature, since hp assigns none of its own.
>
>         All three: `Ok` cold (24, 16, 12 iterations, matching the audit's own run),
>         `Ok` warm, agreeing with a fresh cold solve at the halved pressure. Shown red
>         once: with `NewtonIteration.cs`'s `if (verdict != NotConverged &&
>         RetentionCrossed(...))` replaced by `&& false` (the retention-crossing rule
>         off), all three warm solves end `NotConverged` (`Assert.Equal() Failure:
>         Expected: Ok, Actual: NotConverged`) — the audit's own run recorded the same
>         outcome, atomic H crossing the trace threshold by 5.757753e-10, 9.312757e-10
>         and 9.301270e-10 kmol/kg respectively after one iteration
>         (`scratchpad/audit/repro1.txt`, not committed).
>
>         ⚠ 2026-09-26: this criterion named "the polish-threshold tier of the fixtures
>         node's tolerance table". That did not survive implementation. The fixtures
>         node's `ToleranceTable` compares a tree value against the cea reference and
>         derives every entry from the reference's own print precision and convergence
>         tests (`tests/Fixtures/tolerances.json`); a warm-versus-cold comparison has no
>         reference to ask, and `Tolerances.cs`'s own doc comment already says so: "a
>         comparison of two paths of this tree against each other has no reference to
>         ask, so the number lives here". The warm-start comparison uses
>         `Tolerances.SelfConsistency`, not a new fixtures-node entry.
>
>         ⚠ 2026-09-26, corrected on review: this ⚠ first said the audit's probe
>         parameters "were not recorded in the design text" and that reconstructing the
>         case from the committed `rp1311-example1` fixtures at 1000 K did not reproduce
>         the crossing. Both were true of the design text alone, not of the audit's own
>         working files: its harness (`scratchpad/audit/harness/ZzAuditRepro.cs`, not
>         committed — a fixture's own table and element moles, the fixture's pressure
>         times a named factor, a given temperature, warm at half that pressure) and its
>         recorded run (`scratchpad/audit/repro1.txt`) name the exact three cases above.
>         `TheAuditsExactCasesWarmStartOkAndAgreeWithAFreshColdSolve` reproduces them
>         directly; the review that found this also found the general theory's coverage
>         of them degenerate (it warm-starts at the fixtures' own committed temperatures
>         and pressures, none of which crosses the trace threshold).
>
>         ⚠ 2026-09-26, found implementing this criterion, not one of the audit's five
>         findings: four `rp1311-example14` cases (water pinned at its own melting
>         plateau) end `SingularMatrix` when warm-started at half pressure.
>         `CaseSetup.FromPreviousSolution` seeds both pieces of the cold solution's
>         pinned pair into the warm start's condensed set without checking whether they
>         are still a valid pair at the new pressure, and the first Newton step's matrix
>         is then singular in a way `SingularRemedies` does not recover from. Left open
>         for a design session on the plateau-pinning geometry; `WarmStartTests` skips a
>         case in this state rather than asserting it.
>
>         ⚠ 2026-09-27, the explanation above is wrong (checked by the orchestrator with a
>         scratch trace at `89bb619`): the four cold solutions hold `H2O(L)` alone, no pair.
>         At half the pressure (0.0253 bar) the total pressure lies below water's saturation
>         pressure at 300–304.3 K, so no state with the liquid exists. The seeded liquid goes
>         negative at step 4, the loop never removes it (removal is tested only after
>         convergence, as the reference does, `equilibrium.f90:2602`), `ln n` grows by the
>         damping cap of 0.4 per step with λ shrinking geometrically, and at step 41–44 the
>         matrix is singular; the remedies then drop the liquid from a destroyed gas state
>         and fail again. Seeded without the liquid, the same warm starts are `Ok` in 5–11
>         steps; at ×0.9 to ×0.99 of the pressure they are `Ok`.
>
>         Design (2026-09-27), implemented the same day: a warm start whose convergence
>         fails (the singular remedies exhausted, or the step cap) while a condensed
>         species seeded from the previous solution holds negative moles falls back once
>         to the cold start of section 3.1, with its iterations counted in the case's
>         total. Nothing else changes; a cold start never falls back
>         (`EquilibriumSolver.Solve`'s own outer loop only offers the fallback when
>         `useMolesAsEstimate` was true, and only once).
>
>         The negative mole is not always there to read at the point of failure: the
>         singular remedies' last resort drops the last condensed record unconditionally
>         and zeroes its mole number (`CondensedSet.Remove`) before `Converge` returns, so
>         by the time `Solve` sees `SingularMatrix` the seed's own negative value is
>         already gone. `IterationState.CondensedWentNegative` (`Carriers.cs`) is the one
>         place that still sees the sign: `SingularRemedies.Recover` sets it, immediately
>         before that removal, when the record being dropped was negative; `Converge`
>         resets it to `false` at the start of every call, so a caller reads only what the
>         call that just returned did. `EquilibriumSolver.FallsBackToColdStart` triggers on
>         either this flag or, for the plain step-cap path where nothing was ever removed,
>         a direct read of `scratch.CondensedInSolution`.
>
>         Evidence: `WarmStartTests.AWarmSolveAtHalfPressureAgreesWithAColdSolveAtThatPressure`'s
>         skip for this state is removed (`Assert.Equal(CaseStatus.Ok, warm.Status)`
>         unconditional); all four `rp1311-example14` cases (T300, T304, T304.2, T304.3),
>         warm-started at ×0.5 pressure from their cold solution, end `Ok` and agree with a
>         fresh cold solve at that pressure (`Equilibrium.Tests`, 68/68 of that class).
>         Shown red once with the fallback disabled (`canFallBack` forced `false`
>         regardless of `useMolesAsEstimate`): the same four cases end `SingularMatrix`,
>         the rest of the class unaffected (4 failed of 68).
>
>         A temporary `Console.Error.WriteLine` at the fallback's trigger (reverted before
>         this commit; `HostSolver.Solve` calls `EquilibriumSolver.Solve` directly, not
>         through a compiled kernel, so the print did not disturb that path) counted its
>         firings: `dotnet test tests/Equilibrium.Tests --filter
>         "FullyQualifiedName!~KernelEqualityTests"` fired it exactly 4 times (the four
>         `rp1311-example14` cases above, and no other of the 682 remaining tests), and
>         `dotnet test tests/Performance.Tests --filter
>         "FullyQualifiedName!~KernelEqualityTests"` (the rocket stations' own warm starts
>         across every rocket fixture) fired it 0 times. `KernelEqualityTests` of both
>         nodes were excluded only for this temporary print, which ILGPU's kernel compiler
>         cannot compile (a `NullReferenceException` inside `IRContext.Optimize`, confirmed
>         and then reverted); with the print removed, both nodes' full suites pass,
>         `Equilibrium.Tests` 695/695 and `Performance.Tests` 700/700, `KernelEqualityTests`
>         included. No `Bits.approved.txt` or `Bits.linux.approved.txt` of `Equilibrium`,
>         `Thermo`, `Performance` or `Problems` moved (`git status` before and after,
>         unchanged), matching the counter: no fixture besides the four deliberate
>         half-pressure probes ever reaches the fallback.
>       - **The bookkeeping.** `NewtonLoopStateTests` drives the loop's struct on the
>         host, without a table where the rule does not need one:
>         - `ANotConvergedVerdictClearsTheConvergedMarkAndThePolishCount` and
>           `ReportTestsMetCountsAPolishStepAndPolishedDoesNotCountAnother`: a failed
>           verdict after a pass clears the mark and the polish count;
>         - `ASpeciesCrossingTheTraceThresholdDuringTheStepIsReportedAsACrossing` and
>           `NoCrossingWhenEveryGasSpeciesKeepsItsSideOfTheTraceThreshold`: a crossing of
>           the trace threshold fails the verdict;
>         - `ASingularRemovalOfACondensedSpeciesRestartsTheStepCountAndCountsTowardTheChangeCap`
>           and `RecordSetChangeResetsTheStepCount`: a singular removal restarts the step
>           count and counts as a change.
>
>         Each fact shown red once against the rule it guards, by mutating the guarded
>         line alone and confirming a fresh build fails the fact (`NotConverged`'s branch
>         no longer clearing `PolishSteps`; `RecordSetChange` incrementing instead of
>         resetting `Steps`; `RetentionCrossed` never returning `true`).
>       - **Frozen mode.** `InvalidInputTests.FrozenModeRejectsAnInvalidMoleNumberGaseousOrCondensed`
>         refuses a NaN, an infinite and a negative mole number on a gaseous (`H2`) and a
>         condensed (`H2O(L)`) species in `SolveFrozen`, for tp, hp and sp (18 cases: the
>         validation runs before any kind-specific branch, so all three take the same
>         path). Shown red on the unpatched gas-sum-only check: 4 of 6 of the original
>         hp-only cases reported `Ok` or `NotConverged` instead of `InvalidInput`.
>       - **Bits.** No `Bits.approved.txt` of `Equilibrium`, `Thermo` or `Problems` moves
>         on an existing case: the fifteen new tp fixtures and eleven new thermo species
>         add lines, checked field by field against `Bits.actual.txt`, not by eye; no
>         existing case's hash differs. `Performance`, `Transport` and `Cli` are outside
>         this node's subtree and are unaffected (their own fixtures and code are
>         untouched). The four reasons this criterion anticipated (a late re-convergence
>         polished, a crossing iterated, a set-change budget widened, a phase admitted)
>         do not fire on the committed fixture set: none of them reaches the trace
>         threshold at a crossing, a set-change count near the cap, or a ninth stable
>         condensed phase. They are exercised by the new fixtures and the unit facts
>         above instead of by an existing case's bits moving.
>
>         Every CEA tolerance test stays green (`Problems.Tests` 1186/1186). The
>         execution tests node is green on CUDA in Release (`dotnet test
>         tests/Execution.Tests -c Release`, 126/126, the sweep and throughput tripwire
>         included), because the scratch layout grows.
>
>       Evidence: `APTHERMO_NO_CUDA=1 dotnet test APThermo.sln --filter
>       "Category!=LongRunning"` 4278/4278, none skipped (`Equilibrium.Tests` 695);
>       `dotnet test tests/Execution.Tests -c Release` 126/126 on CUDA; the protocol
>       lint 0 errors/0 warnings; no `Bits*.approved.txt` differs from `main` outside
>       the fifteen new tp lines and eleven new thermo lines named above.

---

<a id="sec-2-5"></a>

## 2026-10-01 — from "## Purpose" — the derivatives are sections 2.5 and 2.6

Moved because `BOOT.md` was over its limit (`AGENTS.md`, §15); the text as it stood:

> ⚠ 2026-09-15: this paragraph, the "Property definitions" bullet of the Constraints
> below and the `## Structure` row of `DerivativeSystem` all read "section 2.6" for the
> derivatives the matrix solutions produce. RP-1311's section 2.5, "Thermodynamic
> Derivatives From Matrix Solutions", holds the system (2.56)–(2.58), cp by (2.59) and
> the pressure system (2.64)–(2.66); section 2.6, "Other Thermodynamic Derivatives",
> holds cv, γ_s (2.71, 2.73) and the sound speed (2.74) — figures read off the converged
> state, not solved for. `API.md` and the solver's own summary (`EquilibriumSolver.cs`),
> which already said 2.5, disagreed with them; the repair review found the disagreement
> and the design session checked both sections against the report. Corrected at every
> place named above.

---

<a id="visibility"></a>

## 2026-10-01 — from "## Structure" — the visibility column

Moved because `BOOT.md` was over its limit (`AGENTS.md`, §15); the text as it stood:

> ⚠ 2026-09-15 (distribution phase): the Visibility column read "public" for
> `EquilibriumSolver` and `DenseSolver`, and `API.md` published `EquilibriumProblem`,
> `EquilibriumScratch`, `EquilibriumResult` and `ScratchLayout` too. The API review of
> that day (fixed in `4344652`) found no consumer scenario for any of the
> six: every use is a neighbour numerical node composing the kernel layer, or this
> node's own tests, and `DenseSolver`'s public status also clashed with `Problems`'
> same-named `EquilibriumProblem` (CS0104). All six became `internal`, with
> `InternalsVisibleTo` grants to `Performance`, `Transport`, `Execution` and their
> mirroring test nodes (`APThermo.Equilibrium.csproj`; `API.md`'s tree-contract section
> lists them); `ProblemKind` stays public.

---

<a id="s-marks"></a>

## 2026-10-01 — from "## Structure" — the mark accessors moved to SpeciesMarks (condensed wording)

Moved because `BOOT.md` was over its limit (`AGENTS.md`, §15); the paragraph was rewritten shorter in `BOOT.md` with its meaning unchanged, and the text as it stood:

> - **The mark accessors moved to `SpeciesMarks`** (2026-09-15, the repair review's
>   R-Equilibrium-6). `CaseSetup` is "what a case needs before its first Newton step",
>   but its mark accessors were used by every stage of the iteration, and `CondensedSet`
>   wrote through them too (`StandDown`): the type's name covered one job and did
>   another. `Of`, `Set` and `InPlay` (the renamed `Mark`, `Mark` and `InPlay`) now sit
>   in `Carriers.cs` beside `SpeciesMark`; `CaseSetup` keeps the input validation, the
>   initial marks and the two reductions of the input. Measured by the dependency
>   check's walk: `CaseSetup` 9 → 10 (it now names `SpeciesMarks` where it used to name
>   only itself); every other caller (`DampedStep`, `Composition`, `CondensedSet`,
>   `SingularRemedies`) unchanged, since a call to `CaseSetup` became a call to
>   `SpeciesMarks` in the same position. `PhaseGeometry`'s own raw scratch read is a
>   defect, not a naming choice (R-Equilibrium-1, fixed separately below).

---

<a id="crit-stood-down"></a>

## 2026-10-01 — from "## Acceptance criteria" — a stood-down record (condensed wording)

Moved because `BOOT.md` was over its limit (`AGENTS.md`, §15); the paragraph was rewritten shorter in `BOOT.md` with its meaning unchanged, and the text as it stood:

> - [x] 2026-09-15 — A record stood down by the anti-cycling rule stays out of play "for
>       the rest of it" (the condensed-species rule above): `PhaseGeometry.Adjacent` and
>       `PhaseGeometry.PhaseAt` test `!SpeciesMarks.InPlay(scratch, k)`, not the raw
>       `scratch.SpeciesActive[k] == 0` the clean-code pass carried over unchanged from
>       the bodies of `Adjacent` and `PhaseAt` in the pre-decomposition
>       `EquilibriumSolver.cs`, at `7661ea9` (a test that was correct only
>       while `Absent` was the sole value skipped, before `SpeciesMark.StoodDown` existed;
>       found by the repair review, R-Equilibrium-1). No `SpeciesActive[` remains outside
>       `SpeciesMarks.Of` and `.Set` (`grep` over `src/Equilibrium/*.cs`, two matches, both
>       in `Carriers.cs`).
>
>       Seen red on the code before the fix, then green after it:
>       `PlateauTests.AStoodDownRecordIsNeitherAdjacentToNorFoundBesideItsInPlayPartner`
>       stands one piece of `ALN(L)` down next to its in-play partner (the fixture and
>       pair of the ALN-gap tests above) and asserts `Adjacent` and `PhaseAt` return −1
>       for it; before the fix `Adjacent` returned the stood-down piece's own table index
>       (231) instead of −1 (`Assert.Equal() Failure: Expected: -1, Actual: 231`, the
>       fact's first assertion, on `Adjacent`) — `PhaseAt` was not reached, the same
>       defect the report names for both methods.
>
>       No fixture reaches the buggy path (BOOT.md's defect note on the condensed-species
>       rule, ⚠ 2026-09-13, and the review's own check): `tests/Equilibrium.Tests/Bits.approved.txt`
>       unchanged through the fix (hash `65788e23f4390305763c80ab1f66b2054ff1907a`, same
>       before and after), `Equilibrium.Tests` 464/464 green (463 plus the new fact).

---

<a id="set-changes"></a>

## 2026-10-01 — from "## Constraints" — the budget of condensed set changes

Moved because `BOOT.md` was over its limit (`AGENTS.md`, §15); the text as it stood:

>   ⚠ 2026-09-14: stood "at most 10 changes of that set per case". The plateau rules of
>   2026-09-13 need up to three changes per slot, and the code's constant became
>   `3 * MaxCondensedInSolution` that day while this sentence kept the old number;
>   found by the clean-code review of 2026-09-14 (AGENTS.md §8: a number repeating a
>   constant diverges at the constant's first change, so the document now names the
>   constant).

---

<a id="s-bounds"></a>

## 2026-10-01 — from "## Structure" — the record bounds are asked of Thermo (condensed wording)

Moved because `BOOT.md` was over its limit (`AGENTS.md`, §15); the paragraph was rewritten shorter in `BOOT.md` with its meaning unchanged, and the text as it stood:

> - **The record bounds are asked of `Thermo`** (added 2026-09-14, after `Thermo`'s
>   range questions were merged; the architecture review's F-AR-01).
>   `PhaseGeometry.RecordLow` and `RecordHigh` decode `Thermo`'s interval layout a
>   second time (`IntervalStart`, `IntervalCount`, `IntervalBounds` and its stride of
>   two), and `Thermo` now answers the same two questions, kernel-compatible, as
>   `SpeciesFunctions.RecordLow` and `RecordHigh` (its `API.md`, range questions).
>   `PhaseGeometry` asks those and keeps no copy of the arithmetic, and no stage of
>   this node reads the three layout arrays. The bounds are table reads, not computed
>   values, so the tests node's bit snapshot may not move.

---

<a id="s-newton"></a>

## 2026-10-01 — from "## Structure" — the Newton loop holds no formula (condensed wording)

Moved because `BOOT.md` was over its limit (`AGENTS.md`, §15); the paragraph was rewritten shorter in `BOOT.md` with its meaning unchanged, and the text as it stood:

> - **The Newton loop holds no formula** (added 2026-09-14, after the efferent coupling
>   was measured by the dependency check's walk: `NewtonIteration` 16 against the root's
>   recalibrated limit of 14). `NewtonIteration` keeps `Converge`: the step and polish
>   counts, the order of the calls, the status. What it computes moves to three stages
>   named after the sections of RP-1311 chapter 3 they implement: `DampedStep`, the
>   multipliers and the gaseous corrections of equation (2.18), the control factor of
>   (3.1)–(3.3) and its application (3.4) with the temperature window (today
>   `ControlFactor` and `Apply`); `ConvergenceTests`, equation (3.5) on the undamped
>   corrections, (3.6) on Δln T with the element balance, and the polish test, as one
>   verdict the loop reads (today `Worst` and the conditions inside `Converge`);
>   `SingularRemedies`, the remedies of section 3.6 (today `Recover`). Each named
>   constant moves with the stage that uses it; the polish-step cap stays with the loop.
>   The loop's coupling is the width of the data it carries (the table, the problem, the
>   scratch, the result, the iteration state, the sums, the layout) and of the stages it
>   calls, which no split removes: should it stay above the limit, `NewtonIteration` is
>   this node's second composition root, its measured figure written into its row. Every
>   expression keeps its form and its order of evaluation, so the tests node's bit
>   snapshot may not move.

---

<a id="s-settled"></a>

## 2026-10-01 — from "## Structure" — what the implementation settled (condensed wording)

Moved because `BOOT.md` was over its limit (`AGENTS.md`, §15); the paragraph was rewritten shorter in `BOOT.md` with its meaning unchanged, and the text as it stood:

> - **The `ref` carrier holds.** `IterationState` is passed by `ref` through every
>   stage; the kernel compiler takes it, so the fallback of returning it by value is not
>   needed. The judges were `KernelEqualityTests` of this node's tests node and the one
>   of `Performance.Tests`, which runs `SolveFrozen` - the first stage to take the
>   carrier - inside a CPU-accelerator kernel.
> - **The composition root fits.** `Solve` is 45 physical lines and `SolveFrozen` 54,
>   both under the root's 60, so the exception this section reserved for `Solve` is not
>   claimed. The node's largest type is `CondensedSet` at 250 lines, against the root's
>   400.
>
>
> (the entry continues with a later passage of the same text)
>
> - **The carriers are filled by name, not by position.** `MixtureSums` and
>   `Derivatives` are structs whose fields are written at the one place that computes
>   them and read through `in` afterwards, rather than readonly structs with a nine- and
>   a five-parameter constructor: a carrier whose purpose is to remove the root's
>   parameter hazard may not reintroduce it in its own constructor. `SystemLayout` stays
>   readonly - its four arguments are the shape of the system and it derives the rest.
> - **Where three small pieces landed.** The last term of equation (2.59),
>   the sum of `n_j (h_j/RT)^2`, belongs to `DerivativeSystem` with the rest of that
>   equation rather than to `MixtureSums`, which does not carry it. The membership test
>   `InSolution` sits in `PhaseGeometry` beside the partner lookup that needs it. The
>   two reductions of the input (`LogPressure`, `InitialTemperature`) sit in
>   `CaseSetup`, which reads the problem; the mark accessors moved to `SpeciesMarks` in
>   the repair review (above, 2026-09-15). `Composition` also holds the frozen sums,
>   whose gaseous logarithms come from the mole numbers because the frozen path has no
>   `LogMoles`.
> - **One behaviour changed, deliberately and invisibly.** `DerivativeSystem` restores
>   the caller's condensed order before returning on every path, the singular one
>   included; the code before the decomposition returned from that path with the scratch
>   still permuted. Nothing in the tree reads that order afterwards (`Solve` rebuilds
>   the set from `result.Moles` at every entry), so no result moves - the point is that
>   a stage may not hand the caller's scratch back reordered.

---

<a id="s-largest"></a>

## 2026-10-01 — from "## Structure" — the largest method

Moved because `BOOT.md` was over its limit (`AGENTS.md`, §15); the text as it stood:

>   ⚠ 2026-09-14: this bullet named `NewtonIteration.Converge` at 56 lines as the node's
>   largest method. The Newton-loop split below moved its formulas out: `Converge` is now
>   55 lines, tied with the new `DampedStep.ControlFactor` (also 55); both stay under the
>   root's 60, as does the next-longest, `IterationMatrix.AccumulateGaseous` (54,
>   unmoved by this split).

---

<a id="crit-decomposition"></a>

## 2026-10-01 — from "## Acceptance criteria" — the decomposition (condensed wording)

Moved because `BOOT.md` was over its limit (`AGENTS.md`, §15); the paragraph was rewritten shorter in `BOOT.md` with its meaning unchanged, and the text as it stood:

> - [x] 2026-09-14 - The decomposition of `## Structure` is in place and changed no
>       number. Shape: no type or method of this node over the root's size limits, no
>       control flow deeper than 3 and no method over six parameters, no exception
>       declared or needed; covered by the protocol tests node's `ShapeTests`, all ten
>       facts green at `62cd99e`. Surface: `Protocol.Tests.SurfaceTests`
>       against `tests/Protocol.Tests/PublicSurface.approved.txt`, which this work did
>       not touch - every new type is internal. Numbers: the tests node's
>       `BitSnapshotTests.EveryFixtureCaseGivesTheRecordedBits` over every
>       enumerated tp, hp and sp fixture case against `Bits.approved.txt`, recorded from
>       the code of `8e36a27` before the first line moved and unmoved after the last;
>       `KernelEqualityTests` green; and the whole fast suite (2142 tests that day)
>       green after each of the six extraction steps. The execution tests node's CUDA
>       sweep and throughput benchmark are long-running and belong to the root's own
>       criteria; they are run on the merge, not here, and this node's evidence is of
>       the CPU accelerator.
>
>       ⚠ 2026-09-15: this criterion named `NewtonIteration.Converge` at 56 lines and
>       said no shape exception was declared or needed. Both went stale the same day,
>       after this tick was written: the Newton-loop split (`29c2200`) moved `Converge`'s
>       formulas into `DampedStep`, `ConvergenceTests` and `SingularRemedies`, leaving it
>       at 55 lines (the `## Structure` warning above already says so); and the coupling
>       recalibration (`9facd7f`) and the named-construction rule (`0c33d1e`) produced the
>       three rows `## Shape exceptions` now declares. Found by the repair review of
>       2026-09-15 (R-Equilibrium-4). The line figures named that day were physical,
>       not lines of code; the criterion above now cites the protocol tests node's
>       `ShapeTests` instead, which holds this by machine at `62cd99e`.

---

<a id="crit-once-each"></a>

## 2026-10-01 — from "## Acceptance criteria" — rules written once (condensed wording)

Moved because `BOOT.md` was over its limit (`AGENTS.md`, §15); the paragraph was rewritten shorter in `BOOT.md` with its meaning unchanged, and the text as it stood:

> - [x] 2026-09-14 - The rules the review of 2026-09-14 found written twice exist once
>       each: the inclusion gain of section 3.4 (`CondensedSet.InclusionGain`, called by
>       the inclusion test and by the honesty guard), the element abundance
>       (`ElementBalance.Abundance`, called by the element rows of `IterationMatrix` and
>       by both tolerance tests), the trace retention (`Composition.Retain`, called by
>       the sums of every step and by the final iterate of every convergence), the state
>       record (`MixtureProperties.Common`, called by the equilibrium and the frozen
>       closure). The dead conditional of the frozen target is gone, its unit difference
>       now a comment in `FrozenTemperature`. Every number of the report is a named
>       constant in the stage that uses it: the control-factor weight and limit of
>       equation (3.1), the small-species bound of (3.2), the tests of (3.5) and (3.6),
>       the polish threshold and step count, the reset moles and reset count of section
>       3.6 (`NewtonIteration`); the step limit, test and step cap of the frozen Newton
>       (`FrozenTemperature`); the initial gaseous moles, the default temperature and
>       the unestimated offset of section 3.1 (`CaseSetup`); the transition window and
>       the residual gain limit (`CondensedSet`); the crossing limit and the range
>       tolerance (`PhaseGeometry`); the two element-balance tolerances
>       (`ElementBalance`). Checked by reading at the close of the decomposition; the
>       bit snapshot proves the reading moved no number.

---

<a id="crit-interval"></a>

## 2026-10-01 — from "## Acceptance criteria" — the interval layout (condensed wording)

Moved because `BOOT.md` was over its limit (`AGENTS.md`, §15); the paragraph was rewritten shorter in `BOOT.md` with its meaning unchanged, and the text as it stood:

> - [x] 2026-09-14 — The node decodes none of `Thermo`'s interval layout (F-AR-01): no
>       `IntervalStart`, `IntervalCount` or `IntervalBounds` in its source files (grep
>       over `src/Equilibrium/*.cs` empty), the record bounds asked of
>       `SpeciesFunctions.RecordLow` and `RecordHigh` from `PhaseGeometry` (`Adjacent`,
>       `EffectiveLow`, `EffectiveHigh`) and from `CondensedSet.Pinnable`; the tests
>       node's `BitSnapshotTests.EveryFixtureCaseGivesTheRecordedBits` unchanged
>       (463 tests green, the hash of `Bits.approved.txt` unmoved) and
>       `KernelEqualityTests` green in the same run. Non-degeneracy, applied alone in
>       the worktree and restored: `SpeciesFunctions.RecordHigh` made to return the
>       record's lower bound turned 29 fixture cases' recorded bits red
>       (`BitSnapshotTests.EveryFixtureCaseGivesTheRecordedBits`), which a node
>       still holding its own copy would not.

---

<a id="crit-newton"></a>

## 2026-10-01 — from "## Acceptance criteria" — the Newton loop (condensed wording)

Moved because `BOOT.md` was over its limit (`AGENTS.md`, §15); the paragraph was rewritten shorter in `BOOT.md` with its meaning unchanged, and the text as it stood:

> - [x] 2026-09-14 — The Newton loop holds no formula (`## Structure`, the decision of
>       that name): `NewtonIteration`, `DampedStep`, `ConvergenceTests` and
>       `SingularRemedies` as the table says, each within the root's code shape
>       (`NewtonIteration` 65 lines, `Converge` 55; `DampedStep` 98 lines,
>       `ControlFactor` 55, `Apply` 25; `ConvergenceTests` 59 lines, `Evaluate` 15,
>       `Worst` 25; `SingularRemedies` 39 lines, `Recover` 27; nesting at most 3, no
>       method over 6 parameters, against the root's 400/60/3/6). The efferent coupling
>       of the four, measured by the dependency check's walk of 2026-09-14:
>       `NewtonIteration` 17, `DampedStep` 7, `ConvergenceTests` 8, `SingularRemedies` 5
>       (`PhaseGeometry`, unaffected by this split, 3). `NewtonIteration`'s 17 is above
>       the root's limit of 14, so it is named as this node's second composition root in
>       its `## Structure` row above, as the decision foresaw (measured 16 there, on the
>       code before the split; the split itself adds the coupling of naming the four
>       stages it now calls, which the decision's own reasoning already accounted for).
>       The tests node's `BitSnapshotTests.EveryFixtureCaseGivesTheRecordedBits`
>       unchanged (`Bits.approved.txt` hash unmoved) and `KernelEqualityTests` green in
>       the same 463-test run; `Performance.Tests` green (699 tests, `SolveFrozen`'s
>       kernel test included); the execution tests node's fast set green on CUDA (41
>       tests, no `APTHERMO_NO_CUDA`).

---

<a id="crit-named"></a>

## 2026-10-01 — from "## Acceptance criteria" — named construction (condensed wording)

Moved because `BOOT.md` was over its limit (`AGENTS.md`, §15); the paragraph was rewritten shorter in `BOOT.md` with its meaning unchanged, and the text as it stood:

> - [x] 2026-09-14 — Every creation of `EquilibriumScratch` in the tree names its
>       arguments (the decision "The scratch descriptor keeps its constructor"), the
>       protocol tests node's named-construction fact green once it exists; the tests
>       node's bit snapshot unchanged. A scan of every `new T(…)` and `T x = new(…)` of
>       the name in `src/` and `tests/` (a script outside the tree) finds the one site, in
>       `EquilibriumScratch.Slice`, every argument named; the build of `Equilibrium` after
>       the change carries the IL of the build before it, method by method, so no
>       argument binds to another parameter; `Equilibrium.Tests` (463) green;
>       `tests/Equilibrium.Tests/Bits.approved.txt` unchanged (blob `65788e23` before and
>       after). The fact,
>       `ShapeTests.EveryWideConstructorIsCalledWithNamedArguments`, is designed
>       and not yet written; it takes over as the evidence when it is.

---

<a id="range-rule"></a>

## 2026-10-01 — from "## Constraints" — the condensed-species range rule

Moved because `BOOT.md` was over its limit (`AGENTS.md`, §15); the text as it stood:

>   ⚠ 2026-09-13: until this date the rule read "a condensed species outside its
>   temperature range is not a candidate at that temperature … when the temperature is
>   a variable and the range is missed by less than 50 K both records stay, the
>   temperature settles at the transition and the record that turns negative is
>   removed after the next convergence". Wrong three ways, found by tracing the
>   published verification cases and verified against a scratchpad prototype and
>   cea 3.3.4 (sessions of 2026-09-13): the pair settles at `T*`, not at the printed
>   bound, and the exact range test removed the returning record at every convergence,
>   so every state on a melting plateau ended `NotConverged` (RP-1311 example 13's
>   throat at BeO's 2851 K; the AP/Al verification record's exits on AL2O3's 2327 K
>   plateau); a state that overshoots a transition by more than the window switched
>   records forever instead of pairing (the same throat search, 2794 ↔ 3112 K), hence
>   the switch memory, which the reference's code keeps too; and the inclusion test
>   could re-add a record just removed for its range forever (AL4C3(cr), whose range
>   ends at 2500 K with no record above), hence the anti-cycling rule. The reference
>   avoids the last cycle by ranking inclusion per unit mass and by letting a record
>   live up to 1.2 × its upper bound — an evaluation of the fit outside its range this
>   node does not copy. With these rules the prototype converged every lost case and
>   matched the reference's direct solves (plateau `Isp` to 0.001 m/s); the
>   reference's own multi-station rocket runs stay unreliable past a plateau (the
>   fixtures node records the guard).

---

<a id="crit-plateau"></a>

## 2026-10-01 — from "## Acceptance criteria" — plateau states (condensed wording)

Moved because `BOOT.md` was over its limit (`AGENTS.md`, §15); the paragraph was rewritten shorter in `BOOT.md` with its meaning unchanged, and the text as it stood:

> - [x] 2026-09-13 — Plateau states converge and match the reference: the
>       melting-plateau fixture cases (RP-1311 example 13 generated with its insert
>       list; the direct plateau stations of AP/HTPB/Al; the latent-heat-band hp
>       cases) return `Ok` with both records of the pair in the solution, the
>       temperature at the pair's `T*`, and every compared field — `γ_s`, the sound
>       speed and the plateau zeros included — within the fixtures node's tolerance
>       table: `FixtureSolveTests` over every tp and hp file of that day,
>       `Performance.Tests.RocketFixtureTests` and
>       `Problems.Tests.RocketTests.TheRocketCaseReproducesTheReferenceEndToEnd`
>       over `rp1311-example13` and the eight `ap-htpb-al-plateau` rocket files.

---

<a id="crit-anti-cycling"></a>

## 2026-10-01 — from "## Acceptance criteria" — the anti-cycling rule (condensed wording)

Moved because `BOOT.md` was over its limit (`AGENTS.md`, §15); the paragraph was rewritten shorter in `BOOT.md` with its meaning unchanged, and the text as it stood:

> - [x] 2026-09-13 — The anti-cycling rule closes the include/remove cycle: an
>       assigned enthalpy inside the `ALN(L)` gap of the fuel-rich AP/HTPB/Al chamber
>       — where `AL4C3(cr)` near its 2500 K upper bound was included and lost every
>       round, seen red before the stand-down rule was added — converges onto the
>       pinned pieces
>       (`PlateauTests.AnEnthalpyInsideTheALNGapPinsThePiecesAtTheCut`); a
>       record removed for range re-enters when it is the only positive candidate, a
>       second escape stands it down, and an `Ok` exit never hides a positive-gain
>       candidate
>       (`PlateauTests.AnEnthalpyNoAdmissibleSetCanHoldIsRefusedRatherThanLiedAbout`
>       walks exactly that path to an honest `NotConverged`, and
>       `AnOkSolutionLeavesNoCondensedCandidateWithPositiveInclusionGain`
>       holds over every hp fixture).

---

<a id="crit-sweeps"></a>

## 2026-10-01 — from "## Acceptance criteria" — sweeps across a plateau (condensed wording)

Moved because `BOOT.md` was over its limit (`AGENTS.md`, §15); the paragraph was rewritten shorter in `BOOT.md` with its meaning unchanged, and the text as it stood:

> - [x] 2026-09-13 — Sweeps across a plateau lose no station: the pressure-ratio band
>       across the AL2O3 plateau solves sequentially and one exit at a time onto the
>       same stations, on the chamber isentrope throughout
>       (`Problems.Tests.SplitRecordTests.ASweepAcrossTheAluminaPlateauStaysOnTheIsentropeByEitherPath`);
>       example 13's four exits cross the BeO plateau end to end
>       (`RocketTests.TheRocketCaseReproducesTheReferenceEndToEnd` over
>       `rp1311-example13`); and tp solves at the printed bounds pick the record the
>       reference picks (`FixtureSolveTests` over `ap-htpb-al-plateau_T2327`,
>       `rp1311-example13-mixture_T2851` and `_T2373`), one kelvin beside the `ALN(L)`
>       cut the gap test picking each side. The original wording asked for "fine"
>       sweeps of both plateaus from both starts; the eight-ratio band and the
>       four-exit example are that promise's committed form.

---

<a id="absent-element"></a>

## 2026-10-01 — from "## Invariants" — an absent element is a mask, not an error

Moved because `BOOT.md` was over its limit (`AGENTS.md`, §15); the text as it stood:

>   ⚠ 2026-09-12: the first draft of this document, written the same day, made a zero
>   abundance `InvalidInput`. Wrong because the batch inputs of the front door come from
>   other simulations as element abundances per kilogram, where an element is often
>   absent from some records of one batch; refusing them would force one table per
>   record and defeat batching.

---

<a id="lambda-growing"></a>

## 2026-10-01 — from "## Constraints" — the control factor of equation (3.1)

Moved because `BOOT.md` was over its limit (`AGENTS.md`, §15); the text as it stood:

>   ⚠ 2026-09-12: the report's wording of equation (3.1) does not say that only growing
>   species enter the maximum. Read symmetrically, the nozzle exits of NTO/UDMH and
>   AP/HTPB/Al and every sp case of LOX/RP-1 spent their 50 steps at `λ ≈ 0.02–0.05`,
>   dozens of hydrocarbons shrinking by e⁻⁴⁰ two units at a time. CEA's code limits only
>   positive corrections (a species on its way out may fall by any factor in one step);
>   so does this node, and the same cases converge in 24 to 44 steps.

---

<a id="crit-condensed"></a>

## 2026-10-01 — from "## Acceptance criteria" — condensed species (condensed wording)

Moved because `BOOT.md` was over its limit (`AGENTS.md`, §15); the paragraph was rewritten shorter in `BOOT.md` with its meaning unchanged, and the text as it stood:

> - [x] 2026-09-12 — Condensed species: the AP/binder/aluminium case includes `AL2O3(L)`
>       in the chamber with the reference mass fraction, and the low-temperature RP-1311
>       example (example 14, water condensation) reproduces the reference phase changes.
>       `CondensedSpeciesTests`: `TheAluminizedPropellantBurnsToLiquidAluminaInTheChamber`
>       (mole fraction 0.07645 as the reference; the reference's mass fraction, 0.304,
>       follows from that mole fraction and its molar mass, so it is not compared a
>       second time),
>       `WaterCondensesBelowItsDewPointInTheLowTemperatureExample` (liquid at
>       300 to 304.3 K, none from 305 K, as the reference), and
>       `TheCondensedSpeciesInTheSolutionAreThoseOfTheReference` over every
>       fixture case with condensed candidates (96 cases), including `AL2O3(a)` at the
>       AP/HTPB/Al exits below the melting point.
>
>       ⚠ 2026-09-14: until this date the same test also asserted the derived mass
>       fraction against an absolute `1e-4` bound typed into the test itself — five
>       times looser than the fixtures node's tolerance table applied to the mole
>       fraction that mass fraction is built from. Removed: the mole fraction and the
>       molar mass are already compared through the table two lines above in the test,
>       and a mass fraction computed from both states adds no fact the reference can
>       settle beyond them. Found by the test review of 2026-09-14 (F-TK-01).

---

<a id="crit-absent"></a>

## 2026-10-01 — from "## Acceptance criteria" — absent element (condensed wording)

Moved because `BOOT.md` was over its limit (`AGENTS.md`, §15); the paragraph was rewritten shorter in `BOOT.md` with its meaning unchanged, and the text as it stood:

> - [x] 2026-09-12 — A case with an absent element gives the same result as the same
>       case solved on a table without that element's species (bit for bit on the same
>       accelerator): `AbsentElementTests.AZeroAbundanceEqualsATableWithoutTheElement`
>       (carbon removed from RP-1311 example 1, argon from example 3, carbon from the
>       LOX/RP-1 throat; moles, multipliers, state and iteration count bit for bit). A case
>       with an empty table or with every abundance zero returns `InvalidInput` and writes
>       nothing else: `InvalidInputTests` (empty table, zero abundances, negative
>       abundance, zero and negative pressure, tp without temperature).

---

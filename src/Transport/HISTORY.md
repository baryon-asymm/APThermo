# HISTORY.md — Transport

Append-only store of what `BOOT.md` no longer needs to state as current truth
(`AGENTS.md`, §15). Newest entry first. Every entry names the date it was moved, the
`BOOT.md` section it came from, and carries the original text in full, unedited. The
start procedure (`AGENTS.md`, §10) does not read this file; it is reached only by
following a dated pointer left in `BOOT.md` at the place the text used to stand.

---

<a id="crit-components-settled"></a>

## 2026-10-01 — from "## Acceptance criteria" — criterion: the components are settled before the set is seeded

Moved because `BOOT.md` was over its limit (`AGENTS.md`, §15); the original date is
2026-09-28. What stays at the pointer is the current rule. The text as it stood:

> - [x] 2026-09-28 — The components are settled before the set is seeded, and every
>       component is in the set (the ⚠ of 2026-09-28 under Constraints).
>       - **The code.** A new stage `ComponentBasis` runs between
>         `TransportComponents.Select` and `TransportSetSelection.Select` in
>         `StationEvaluation`: it reduces a compact matrix of every active row's default
>         and component columns, in element order, with `ReactionBasis`'s own cleaning
>         threshold, and reverts a vanished pivot to the row's default before anything is
>         seeded, exactly as `equilibrium.f90:2264-2291` does before
>         `equilibrium.f90:5221-5236` seeds the transport set from the settled list.
>         `TransportSetSelection.Seed` was already seeding whatever a component's moles
>         are; `ReactionBasis` no longer reverts, and a zero pivot there now simply
>         leaves its row unreduced (the reference's `tem == 0`).
>       - **The audit's restricted-list case (conservation), seen red against `5a732f0`:**
>         `ReactionConservationTests.TheAuditsRestrictedProductListConservesEveryReactionWhenTheRevertedDefaultCarriesNoMoles`:
>         `[NO2, N2O4, N, O, N2, O2, NO]` at 400 K with N and O at zero moles — the exact
>         scenario of the ⚠ above. Every reaction of the settled set conserves every
>         element; the set carries every product but the atomic nitrogen the N row never
>         needed (`SpeciesCount` 6, one below the product count), and the atomic oxygen
>         the O row reverts to, seeded with zero moles, is immediately trace-eliminated
>         (`TraceEliminations` 1). Seen red against the code before this fix (still
>         seeding before the revert): `SpeciesCount` 5, the reverted default missing from
>         the set entirely, one conservation violation reported.
>       - **The `InternalsVisibleTo` grant.** The remaining three facts need
>         `tests/Transport.Tests` to call `Equilibrium`'s internal solver for an
>         independently solved state to cross-check against. This coder escalated it
>         (`AGENTS.md` §11) rather than editing a neighbour's `.csproj`/`API.md` itself;
>         the orchestrator decided the same day, at the root level that owns both nodes,
>         to grant it under the root `BOOT.md`'s "Tree contracts" rule ("the test … nodes
>         that use it"). `src/Equilibrium/APThermo.Equilibrium.csproj` gains
>         `<InternalsVisibleTo Include="APThermo.Transport.Tests" />`, and
>         `src/Equilibrium/API.md`'s tree-contract sentence names it; this node's own
>         `## Dependencies` links `Equilibrium`'s `API.md`. Only the types
>         `Equilibrium`'s tree contract already declares are used
>         (`EquilibriumProblem`, `EquilibriumScratch`, `EquilibriumResult`,
>         `EquilibriumSolver`, `ScratchLayout`), through the new `EquilibriumHost` host
>         helper (`tests/Transport.Tests/EquilibriumHost.cs`), which mirrors this node's
>         own `TransportHost` pattern.
>       - **The restricted list's heat capacity against `Equilibrium`'s `CpEquilibrium`.**
>         `ReactionConservationTests.TheAuditsRestrictedListMatchesEquilibriumsHeatCapacity`:
>         the audit's own six-species list (`NO2, N2O4, NO, O2, N, O`, no N2, so the
>         equilibrium cannot collapse to N2 + O2), solved by `Equilibrium` itself from
>         pure N2O4's element ratio at 400 K, 0.1 MPa. `Equilibrium`'s own converged state
>         gives atomic N and O zero moles (not hand-picked); feeding that composition to
>         `Transport` reproduces the revert and trace elimination, and
>         `EquilibriumHeatCapacity` (1998.469288525126 J/(kg·K), read fresh from the
>         solve, not typed) matches `Equilibrium`'s own `CpEquilibrium`
>         (1998.469288525117) to 4.5e-18 relative — the audit's own figure and its own
>         "equal to Equilibrium's to 4e-15" finding, reproduced without typing either
>         number into the test. Seen red against `5a732f0` (conservation violations, the
>         reacting conductivity and heat capacity off by two to three orders of
>         magnitude, matching the audit's own numbers).
>       - **H2 + HF over the audit's grid, both element orders.**
>         `EquilibriumConsistencyTests.HydrogenFluorideAgreesBetweenElementOrdersOverTheAuditsGrid`:
>         H:F = 2:1 by atoms over 300 to 3 000 K (11 points, 270 K apart) at 1 kPa,
>         0.1 MPa and 10 MPa, both element orders, each solved by `Equilibrium`. Every
>         converging reaction conserves, and the two orders agree field by field
>         (viscosity, both conductivities, both Prandtl numbers, both heat capacities)
>         within `ConsistencyTolerance` (below). Seen red against `5a732f0`: with H
>         listed first, every converging state at and above 570 K had a conservation
>         violation and reacting figures off by up to three orders of magnitude from the
>         F-first order, reproducing the audit's own "20 of 29 records" finding in kind.
>       - **Consistency over sweeps, both element orders.**
>         `EquilibriumConsistencyTests.TransportsEquilibriumHeatCapacityMatchesEquilibriumOverStateSweeps`:
>         the H2 + HF grid above (both orders), the N2O4/NO2 system's full seven-species
>         list over the same grid, and the chamber state (an hp solve) of one fixture per
>         verification propellant (LOX/LH2, LOX/RP-1, N2O4/UDMH, AP/HTPB/Al) in both
>         element orders — a generated list, asserted non-empty, then filtered to the
>         states with no trace elimination and no condensed species present (also
>         asserted non-empty). On that filtered list, `Transport`'s
>         `EquilibriumHeatCapacity` equals `Equilibrium`'s `CpEquilibrium` within
>         `ConsistencyTolerance`. Seen red against `5a732f0` together with the H2+HF
>         fact above (the same underlying states).
>
>         `ConsistencyTolerance` (in `EquilibriumConsistencyTests`) is
>         `100 · (1 − CoverageFraction + CoverageTolerance)` ≈ 1.0e-4: the transport
>         set's own coverage rule may leave out a fraction of the gaseous moles of about
>         `1 − CoverageFraction + CoverageTolerance` ≈ 1e-6 at the moment it stops adding
>         species, and the decade-stepped selection can overshoot that bound by roughly
>         two more decades of the same slack before the next pass would have caught up —
>         the mechanism a reacting figure's own sensitivity to a minor species (the
>         Butler–Brokaw weighting) can amplify. Derived from the two named constants
>         before the sweep was run, not adjusted afterwards: the observed worst-case
>         relative difference over every comparable state is 4.4e-5, comfortably under
>         the 1.0e-4 the formula gives.
>       - **Conservation everywhere**
>         (`ReactionConservationTests.EveryReactionOfEveryStationsSetConservesTheElements`)
>         stays green over every rocket fixture with transport; no committed fixture's
>         basis has a vanished pivot, so this fact does not exercise the revert (the
>         ⚠ of 2026-09-27 above), but it is unaffected by the change. The new sweeps'
>         own conservation is checked by every fact above through the same
>         `ReactionConservationTests.EvaluateAndCheckConservation` helper.
>       - **Bits.** The fixtures never revert, so every `Bits*.approved.txt` of the tree
>         stays unchanged; confirmed by `git status` over the whole tree after every
>         change of this task, not only this node's own snapshot.
>       - **Evidence.** `tests/Transport.Tests` 167/167 (164 before this criterion's new
>         facts), the full fast suite green (`APTHERMO_NO_CUDA=1 dotnet test APThermo.sln
>         --filter "Category!=LongRunning"`), the protocol lint 0 errors/0 warnings, at
>         the commit this criterion's tick names.

---

<a id="crit-reaction-basis-seeding"></a>

## 2026-10-01 — from "## Acceptance criteria" — correction: the revert held for the reduction, not for the seeding

Moved because `BOOT.md` was over its limit (`AGENTS.md`, §15); the original date is
2026-09-28. What stays at the pointer is the current rule. The text as it stood:

>       ⚠ 2026-09-28: "reverts … as the reference does" held for the reduction and not
>       for the seeding, and the restricted-list fact gave N and O 0.01 mol each, so the
>       reverted default was always in the set; with a zero-mole default the fix broke
>       conservation by orders of magnitude (the ⚠ of that date under Constraints). The
>       criterion below replaces this one's first bullet as the evidence.

---

<a id="shape-row-ce15"></a>

## 2026-10-01 — from "## Shape exceptions" — correction: the StationEvaluation row of the shape table

Moved because `BOOT.md` was over its limit (`AGENTS.md`, §15); the original date is
2026-09-28. What stays at the pointer is the current rule. The text as it stood:

> ⚠ 2026-09-28: this table carried no row for `StationEvaluation`, and the paragraph
> below it read "measures 14 by the dependency check's walk: at, not above, the root's
> limit of 14, so it claims no exception and this node needs no efferent-coupling row".
> Adding the call to `ComponentBasis` (the fix of that date) raised the measured figure
> to 15, over the limit; the protocol tests node's `ShapeTests` (fact
> `NoSrcTypeNamesMoreThan14TypesOfTheTree`) failed with exactly that figure and named the
> missing row. `StationEvaluation` was already named as the composition root in
> `## Structure` above, which the root's Ce rule exempts from the numeric limit, so the
> row above records the reason rather than opening a new one.

---

<a id="components-settled"></a>

## 2026-10-01 — from "## Constraints" — correction: the components are settled before the seeding

Moved because `BOOT.md` was over its limit (`AGENTS.md`, §15); the original date is
2026-09-28. What stays at the pointer is the current rule. The text as it stood:

>   ⚠ 2026-09-28: the fix of 2026-09-26 put the revert in the reduction of the set's
>   basis (stage 7), after the set had been seeded (stages 4 and 5). A default species
>   with zero moles was then never in the set: the reverted row had no column, stayed
>   unreduced, and its element had no component, so the reactions did not conserve it
>   ("HF − F", "2HF − F2"). The status stayed `Ok` and the reacting figures were off by
>   one to three orders of magnitude: `rocket` H2(L)/F2(L) at O/F 22, ε 150, gave an
>   equilibrium heat capacity of 92 308 J/(kg·K) against `Equilibrium`'s 3 334, and
>   H2 + HF with H listed first 20 of 29 states wrong, up to 1 660× in the heat capacity
>   and 2 938× in the reacting conductivity; the restricted N2O4/NO2 list +3.7 %. Before
>   that fix the same stations were off by 0.05 to 0.35 %. The four verification
>   propellants never revert, which is why no fixture saw it. The sentence read "the row
>   takes back its default species before it is reduced", true of the reduction and
>   silent on the seeding, which the reference does after the revert. Found by the
>   second hidden-defect audit of 2026-09-28 (Performance and Transport, finding F2),
>   confirmed against `equilibrium.f90` by the orchestrator.

---

<a id="crit-reaction-basis"></a>

## 2026-10-01 — from "## Acceptance criteria" — criterion: the reaction basis reverts a vanished pivot

Moved because `BOOT.md` was over its limit (`AGENTS.md`, §15); the original date is
2026-09-27. What stays at the pointer is the current rule. The text as it stood:

> - [x] 2026-09-27 — The reaction basis reverts a vanished pivot to its default species,
>       as the reference does, and every estimated species is counted (the ⚠ notes of
>       2026-09-26).
>       - **The audit's stage run as a fact.**
>         `Transport.Tests.ReactionConservationTests.TheAuditsRestrictedProductListConservesEveryReaction`:
>         table `[N, O]`, products `[NO2, N2O4, N, O, N2, O2, NO]`, a composition
>         dominated by NO2 then N2O4, at 400 K. Every reaction of the set conserves
>         every element (computed from the reaction coefficients and the species
>         stoichiometry, not typed). Seen red against the pre-fix `ReactionBasis.cs`
>         (the code of `9c33398`): all five reactions failed to conserve, the residuals
>         matching the audit's own trace exactly ("+2 NO2 −4 N2O4 −1 N2" off by N −8,
>         O −12 reproduced verbatim as reaction 2's residuals).
>       - **Conservation everywhere.**
>         `ReactionConservationTests.EveryReactionOfEveryStationsSetConservesTheElements`
>         over every rocket fixture with transport (the enumerated directory), every
>         station, every reaction of the set: green before and after the fix, since no
>         committed fixture's basis has a vanished pivot (the Bits confirmation below).
>       - **Estimates.** `FitTests.ASpeciesWithViscosityFitsButNoConductivityFitIsCountedAsEstimated`:
>         `UF6`, the one species in `trans.inp` with viscosity fits and no conductivity
>         fit, is counted in `EstimatedSpeciesCount` (1) and its mole fraction (1.0, the
>         only species of a one-species table) in `EstimatedMoleFraction`. Seen red
>         against the pre-fix `SetSpeciesProperties.cs`: `EstimatedSpeciesCount` was 0.
>       - **Bits.** No bit snapshot moved: the fast suite (3163 tests) and
>         `tests/Transport.Tests/Bits.approved.txt` are unchanged by the fix. Confirmed
>         with a temporary counter (`DiagnosticRevertCounter`, removed before the commit)
>         incremented by the revert branch of `ReactionBasis.Eliminate` and by the
>         viscosity-only branch of `SetSpeciesProperties.Fits`, run over every station of
>         every rocket fixture with transport (168 stations, 39 fixtures): both counters
>         read 0, so no committed fixture's basis has a vanished pivot and none holds
>         `UF6`.

---

<a id="taboo-estimated-count"></a>

## 2026-10-01 — from "## Taboos" — correction: an estimated species is counted for either fit

Moved because `BOOT.md` was over its limit (`AGENTS.md`, §15); the original date is
2026-09-26. What stays at the pointer is the current rule. The text as it stood:

>   ⚠ 2026-09-26: only a missing viscosity was counted. `UF6`, which has viscosity fits
>   and no conductivity fits, had its conductivity estimated and was counted nowhere
>   (the hidden-defect audit, its notes).

---

<a id="reduction-revert"></a>

## 2026-10-01 — from "## Constraints" — correction: a vanished pivot reverts to the default

Moved because `BOOT.md` was over its limit (`AGENTS.md`, §15); the original date is
2026-09-26. What stays at the pointer is the current rule. The text as it stood:

>   ⚠ 2026-09-26: the rows were reduced with the component kept even when its pivot had
>   vanished. The row was skipped, but the species still counted as a component, and
>   every reaction took coefficients from an unreduced row. On a restricted product list
>   (`N2O4 ⇌ 2 NO2` at 400 K) all five reactions failed to conserve the elements, and
>   the status was `Ok`. The reaction terms stayed plausible: the conductivity's was 0.17
>   of the reference rule's, and the heat capacity's 0.10. Found by the hidden-defect
>   audit of 2026-09-26 (finding F4). The sentence above said the components were chosen
>   "as the reference does", which held for the choice and not for the reduction.

---

<a id="set-selection-wording"></a>

## 2026-10-01 — from "## Constraints" — the transport set: the wording around the pass bound and ng

Moved because `BOOT.md` was over its limit (`AGENTS.md`, §15); the original date is
2026-09-26. What stays at the pointer is the current rule. The text as it stood:

>   below 1e-11 n, or ng passes have run (the reference's bound, cea 3.3.4
>   `equilibrium.f90:5278`; this bound was missing from the sentence until 2026-09-26,
>   while the code had it) (ng is the number of gaseous species of the case: those of the table
>   whose every element the case holds, which in a table built for the case alone is
>   the table's gas count, the reference's product list; see the Invariants for the
>   correction of 2026-09-13). Within a pass the
>   species are taken in table order, which matters only when the set fills: the
>   AP/HTPB/Al chamber needs 56 species for the coverage and takes the first 40. Mole
>   fractions x_s are relative to the set.

---

<a id="crit-slice-size"></a>

## 2026-10-01 — from "## Acceptance criteria" — correction: the figure of TransportScratch.Slice

Moved because `BOOT.md` was over its limit (`AGENTS.md`, §15); the original date is
2026-09-15. What stays at the pointer is the current rule. The text as it stood:

>       ⚠ 2026-09-15: this criterion named `TransportScratch.Slice` at 53 lines. The
>       `## Structure` section's own warning above (superseded by the named-construction
>       fix) already corrected it to 58 once the return statement was rewritten to name
>       its arguments, but only there, not beside this criterion; `TransportScratch.Slice`
>       itself still measured 58 physical lines. Found by the repair review
>       (R-Transport-4). The figure was physical, not lines of code; the criterion above
>       now cites the protocol tests node's `ShapeTests` instead, which holds this by
>       machine at `62cd99e`.

---

<a id="stationinputs-count"></a>

## 2026-10-01 — from "## Structure" — correction: the justification of StationInputs

Moved because `BOOT.md` was over its limit (`AGENTS.md`, §15); the original date is
2026-09-15. What stays at the pointer is the current rule. The text as it stood:

> ⚠ 2026-09-15: this paragraph used to justify `StationInputs` by "every stage takes it,
> so no stage signature exceeds four parameters". `SetProperties.Fill` and
> `TransportSetSelection.Passes` already take five parameters each; the count was wrong
> and, being a number repeating a property of the code rather than a fact the machine
> checks, could only drift further. Reworded to say what `StationInputs` is instead of a
> figure the code does not hold to. Found by the repair review of 2026-09-15
> (R-Transport-3).

---

<a id="visibility-internal"></a>

## 2026-10-01 — from "## Structure" — correction: TransportSolver and the tables are internal

Moved because `BOOT.md` was over its limit (`AGENTS.md`, §15); the original date is
2026-09-15. What stays at the pointer is the current rule. The text as it stood:

> ⚠ 2026-09-15 (distribution phase): "one public entry" and `TransportSolver`'s row
> below stood before the API review of that day (fixed in `c11e02b`) found
> no consumer scenario for it, `TransportTable`, `TransportTableArrays`,
> `TransportTableView`, `TransportTableBuffers`, `TransportLayout` or
> `TransportScratch`: every use is `Execution` composing the kernel, `Problems`
> building a table, or this node's own tests. All seven moved into `API.md`'s
> tree-contract sections; `APThermo.Transport.csproj` grants `InternalsVisibleTo` to
> `Execution`, `Problems`, `Execution.Tests`, `Problems.Tests`, `Benchmarks` and, for
> `TransportTableView`, `ILGPURuntime`. `TransportFigures` stays public. The entry
> point is internal now, reached only through the grant; the decomposition itself (one
> class per stage) is unaffected.

---

<a id="crit-named-arguments"></a>

## 2026-10-01 — from "## Acceptance criteria" — criterion: every creation of the wide constructors names its arguments

Moved because `BOOT.md` was over its limit (`AGENTS.md`, §15); the original date is
2026-09-14. What stays at the pointer is the current rule. The text as it stood:

> - [x] 2026-09-14 — Every creation of `TransportScratch`, `TransportTableView` and
>       `TransportTableArrays` in the tree names its arguments (the decision "The scratch
>       descriptor stays"), the protocol tests node's named-construction fact green once
>       it exists; the tests node's bit snapshot unchanged. A scan of every `new T(…)` and
>       `T x = new(…)` of the three names in `src/` and `tests/` (a script outside the
>       tree) finds four sites, in `Descriptors`, twice in `TransportTable` and in
>       `Transport.Tests`' `StatusTests`, every argument named (the parameter `default`
>       as `@default:`); the builds of `Transport` and `Transport.Tests` after the change
>       carry the IL of the builds before it, method by method, so no argument binds to
>       another parameter; `Transport.Tests` (157) green;
>       `tests/Transport.Tests/Bits.approved.txt` unchanged (blob `3e4000db` before and
>       after). The fact,
>       `ShapeTests.EveryWideConstructorIsCalledWithNamedArguments`, is designed
>       and not yet written; it takes over as the evidence when it is.

---

<a id="crit-singular-matrix"></a>

## 2026-10-01 — from "## Acceptance criteria" — criterion: SingularMatrix writes the frozen figures

Moved because `BOOT.md` was over its limit (`AGENTS.md`, §15); the original date is
2026-09-14. What stays at the pointer is the current rule. The text as it stood:

> - [x] 2026-09-14 — `SingularMatrix` writes the frozen figures and reacting figures
>       equal to them, as `API.md` promises:
>       `Transport.Tests.StatusTests.AReactionSystemThatCannotBeSolvedKeepsTheFrozenFigures`
>       drives `ReactionTerms` (through `InternalsVisibleTo`) over a set of three species
>       and two reactions whose second system is singular while the first is not — the
>       third species weighs nothing, so RT/(pD) vanishes for both pairs that hold it and
>       the one pair left gives the two reactions the same difference vector — and
>       asserts the three equalities and the status. Seen red against the code of
>       `8e36a27` (moved here unchanged before the fix): the status and the conductivity
>       were right and the equilibrium heat capacity was 10441.86 against the frozen
>       5001.70. `TheSameSetIsSolvedWhenEveryPairCarriesADiffusionWeight`
>       keeps the first test from passing because both systems fail.

---

<a id="crit-decomposition"></a>

## 2026-10-01 — from "## Acceptance criteria" — criterion: the decomposition of the Structure

Moved because `BOOT.md` was over its limit (`AGENTS.md`, §15); the original date is
2026-09-14. What stays at the pointer is the current rule. The text as it stood:

> - [x] 2026-09-14 — The decomposition of `## Structure`: every type of the node within
>       the root's code-shape constraint, `TransportScratch.Slice` (the scratch
>       descriptor) the declared exception to the parameter rule, no control flow
>       nested deeper than 3, no method with more than six parameters — the public
>       surface unchanged (`Protocol.Tests.SurfaceTests` green against a
>       `PublicSurface.approved.txt` that did not move a line, every new type
>       internal), and every station's figures bit for bit those of `8e36a27` on the
>       CPU accelerator: the tests node's `Bits.approved.txt`, recorded before the
>       first line of code moved, unchanged through all eight steps, with
>       `KernelEqualityTests`, `AbsentElementTests` and every criterion above green
>       after each of them, and the whole fast suite green at the end (2144 tests).
>       Covered by the protocol tests node's `ShapeTests`, all ten facts green at
>       `62cd99e`.

---

<a id="crit-station-redated"></a>

## 2026-10-01 — from "## Acceptance criteria" — criterion: the station figures against the reference, re-dated

Moved because `BOOT.md` was over its limit (`AGENTS.md`, §15); the original date is
2026-09-14. What stays at the pointer is the current rule. The text as it stood:

> - [x] 2026-09-14 — Every rocket fixture run with transport (enumerated by the tests
>       node) reproduces viscosity, frozen and reacting conductivity, both Prandtl
>       numbers and the reference's `cpFrozen` on the reference composition within the
>       tolerance table; the reacting fields are skipped at the nine defective stations,
>       where the test asserts the defect is still visible (`Transport.Tests`,
>       `StationTests.StationFiguresMatchTheReference`,
>       `StationTests.TheReferenceCpFrozenIsTheTransportSetHeatCapacity`,
>       `StationTests.ReactingConductivityIsNeverBelowTheFrozenOne`,
>       `StationTests.TheTraceComponentStationsCarryTheDocumentedReferenceDefect`;
>       green on the decomposed code at `5cb2664`). Re-dated from 2026-09-12, when the
>       evidence was one test, `StationTests.Stations_match_the_reference`, over 39
>       files of 4 to 11 stations with the worst deviation 3.4e-8 relative: the tests
>       node split that test (its F-TK-05) and left the typed counts out (F-TK-03), and
>       this node's code was decomposed on this date, which a tick of an earlier date
>       cannot prove (AGENTS.md §6).

---

<a id="as-built-figures"></a>

## 2026-10-01 — from "## Structure" — correction: the as-built figures of Run and Slice

Moved because `BOOT.md` was over its limit (`AGENTS.md`, §15); the original date is
2026-09-14. What stays at the pointer is the current rule. The text as it stood:

>   ⚠ 2026-09-14, superseded by the named-construction fix below: `Descriptors.cs`'s
>   `TransportScratch.Slice` return statement was rewritten from positional to named
>   arguments after this paragraph was written, which lengthened it to 58 lines (measured
>   by the protocol tests node's `ShapeMeasures`); still well under the root's 60.
>   `StationEvaluation.Run` measures 28 lines by the same tool, not 32; unchanged since,
>   the difference is this paragraph's own figure, not a later edit.

---

<a id="as-built"></a>

## 2026-10-01 — from "## Structure" — as built: the files, the predicates and the measured sizes

Moved because `BOOT.md` was over its limit (`AGENTS.md`, §15); the original date is
2026-09-14. What stays at the pointer is the current rule. The text as it stood:

> As built, 2026-09-14. One file per stage class, named after it; the three carriers
> share `Carriers.cs`, as decided above, and the two collectors of the host-side table
> build (`SpeciesRuns`, `PairRuns`: the two sections of the table as the build collects
> them: the per-species runs with the lists of species with and without data, and the
> pair index with the pair runs) share `TableRuns.cs` beside the other descriptors
> (`Descriptors.cs`). Two predicates are called from a stage other than the one that
> owns them, and are internal for it: `TransportComponents.OfCase` from
> `TransportSetSelection` (the case's gas count asks the same question as the default
> species of a row) and `ReactionBasis.LocalIndex` from `ReactionSet`. The stages carry
> the bookkeeping into the figures where they count it — `TransportSetSelection` the
> species count and `Capped`, `SetSpeciesProperties.Fits` the estimated species and
> their fraction, `ReactionSet` the reaction count and the trace eliminations — and the
> composition root reads the species count back out of them, so that no stage returns a
> tuple. `StationEvaluation.Run` is 32 lines and holds no formula; the largest type of
> the node is now `TransportComponents` at 244 lines and the largest method
> `TransportScratch.Slice` at 53, against 794 and 640 before.

---

<a id="decision-singular-matrix"></a>

## 2026-10-01 — from "## Structure" — decision of the review: SingularMatrix means the frozen figures

Moved because `BOOT.md` was over its limit (`AGENTS.md`, §15); the original date is
2026-09-14. What stays at the pointer is the current rule. The text as it stood:

> - **`SingularMatrix` means the frozen figures.** The contract (`API.md`, Errors) says
>   that when a reaction system cannot be solved the frozen figures are written and the
>   reacting ones equal them. The code obeyed it for the conductivity and not for the
>   heat capacity when only the second solve failed (the review's F-TP-01):
>   `ReactionTerms` zeroes both contributions on either failure, and the tests node
>   exercises the status through the stage. No fixture reaches the path, so the bit
>   snapshot does not move.

---

<a id="set-gas-count"></a>

## 2026-10-01 — from "## Invariants" — correction: the set's thresholds count the case's gases

Moved because `BOOT.md` was over its limit (`AGENTS.md`, §15); the original date is
2026-09-13. What stays at the pointer is the current rule. The text as it stood:

>   ⚠ 2026-09-13: the set's rule (Constraints) counted "the gaseous species in the
>   table", as the reference counts the gaseous products of its problem, and the two
>   agree only in a table built for one case. In the front door's batch over a union
>   of elements the larger count lowered every threshold: the LOX/RP-1 throat set took
>   a fourteenth species in the table shared with AP/HTPB/Al, and its figures moved by
>   up to 4e-7 relative against the single solve. Found by the front door's batch test;
>   the count is now the case's, and the test node proves the invariant bit for bit.

---

<a id="reference-defect-continue"></a>

## 2026-10-01 — from "## Constraints" — the reference's trace-elimination defect: measurement and provenance

Moved because `BOOT.md` was over its limit (`AGENTS.md`, §15); the original date is
2026-09-12. What stays at the pointer is the current rule. The text as it stood:

> ⚠ 2026-09-12, a defect of the reference: cea 3.3.4 writes `continue` (a no-op in
> Fortran) where CEA2 had `GOTO 260` in the elimination of a trace species, so the
> reaction through the trace species is kept while its pairs are dropped, and the
> reacting conductivity of a station whose component species is a trace is inflated:
> 170 to 440 times the frozen conductivity at the LOX/LH2 exits below 1020 K, where `OH`
> seeds the oxygen row at x_s < 1e-10. Found by the Python mirror of the routine
> (scratch work of the session), which reproduces those values to 1e-16 with the no-op
> and gives λ_eq = λ_fr with the rule of CEA2. This node keeps the rule of CEA2; the
> fixtures node records the defective stations, and the test node skips the reacting
> fields where the solver reports a trace elimination and asserts that the defect is
> still visible there.

---

<a id="reaction-inputs"></a>

## 2026-10-01 — from "## Dependencies" — correction: the reaction term needs no multipliers or derivatives

Moved because `BOOT.md` was over its limit (`AGENTS.md`, §15); the original date is
2026-09-12. What stays at the pointer is the current rule. The text as it stood:

> ⚠ 2026-09-12: the sketch took "the mole numbers and Lagrange multipliers of the
> station, and the equilibrium derivatives" from `Equilibrium` for the reaction term.
> The reaction contribution needs neither the multipliers nor the derivatives: it is
> built from the stoichiometry of the set (independent reactions among its species) and
> the species enthalpies, as the report does; the multipliers left the signature.

---

<a id="species-no-data"></a>

## 2026-10-01 — from "## Invariants" — correction: species without data are estimated, not excluded

Moved because `BOOT.md` was over its limit (`AGENTS.md`, §15); the original date is
2026-09-12. What stays at the pointer is the current rule. The text as it stood:

> ⚠ 2026-09-12: the second invariant stood "Only species with data take part. A species
> without a transport entry is excluded from the mixture rules and the mole fractions of
> the remaining species are renormalized, as CEA does; … a station where [the excluded
> fraction] exceeds the threshold below is `NoTransportData`", with a taboo "No default
> transport properties for species without data: absence is reported, never guessed".
> Wrong: the reference (cea 3.3.4, `compute_transport_properties` in
> `source/equilibrium.f90`, as CEA2's `TRANIN` before it) keeps such species and
> estimates their viscosity by hard spheres and their conductivity by the modified Eucken
> relation. Found when the AP/HTPB/Al chamber, where 2.8 % of the gaseous moles belong to
> species without data (`ALCL`, `CL`, `ALOH`, `ALOHCL2`, …), could not be reproduced by
> exclusion (−0.9 % viscosity, +3.3 % frozen conductivity, −5.6 % reacting Prandtl in the
> Python prototype of this node) and was reproduced to 3e-8 with the estimate. The
> threshold and the exclusion are dropped; the number of estimated species and their mole
> fraction are reported instead.

---

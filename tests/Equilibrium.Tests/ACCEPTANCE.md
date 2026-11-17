# ACCEPTANCE.md — Equilibrium.Tests


- [x] 2026-11-16 — `LoopLiveBoundaryTests` passes 5/5. Both the compiled original
  direct `ref plan` and an alias mutant `ref var copy = ref plan` fail the actual-call
  guard; four syntax controls pass, including a fresh copy passed by reference. The
  syntax guard checks this specific call, not arbitrary NoInlining methods.

- [x] 2026-11-16 — `RecoveryCandidateTests.PhysicalRecoveryReturnsCondensedSlotsConsistentWithItsWinningComposition`
  checks the Mg-H-O-S SP case on cold and seeded host solves; both fail when
  bank-to-live condensed-slot restoration is removed and pass with it.
  `RestoringAnEarlierWinnerRestoresItsCondensedSlots` isolates selection and transfer.
  `RecoveryStorageTests.CondensedBanksStayWithinTheirCasesAndLeaveEarlierRegionsUnchanged`
  checks two adjacent banks and guards; the original C0 fixture is unchanged. CUDA
  evidence remains pending.
  ⚠ 2026-11-17: the 2026-11-16 record ended "CUDA evidence remains pending."
  `RecoverySlotsCudaTests.PhysicalRecoverySlotsOnActualCudaMatchCpuForColdAndSeededSolves`
  passed on both reference platforms at `c0ebe704` in Release run 43861338174.
  That proof is historical for c0; the current follow-up source needs new CUDA runs.

- [x] 2026-11-14 — R10: RecoveryStorageTests checks generated state size/alignment,
  the frozen RecoveryLayoutC0.json region inventory and isolated typed case banks in
  a CPU kernel. RecoveryCandidateTests restores all state bits from a poisoned bank;
  omitting the save makes both HP/SP controls red. Dedicated typed storage replaces
  the failed Cast experiment (Recovery/BOOT.md rationale); hardware speed is unproven.

- [x] 2026-11-14 — R08: initial Warm HP/SP entry, bank restoration, no-floor completion, failed
  warm fallback and nonrecursive final routing are checked by RecoveryCandidateTests;
  the shared Execution CPU companion retains independent same-H/S objectives.
  Old lower controls: 6 red/10 green; fixed: 16/16. Shared CPU: 1 red/1 green to 2/2.
  Full CPU Equilibrium: 2197/2197, no skips. Evidence `SCRATCH/audit/r08-*.trx` in
  audit-seeded-numerical, with machine-readable seeded rows in `r08-fixed-seeded-rows.json`.

- [x] 2026-11-14 — `BitSnapshotTests.EveryFixtureCaseGivesTheRecordedBits` preserves the approved
  bytes; an actual one-ULP mole mutation yields a readable named per-case field dump.

- [x] 2026-11-14 — `FiniteStateTests` freezes lower-level pressure controls: false Ok at the smallest
  pressure and infinity must be refused; representable extreme finite pressure remains
  admitted. The full public state property list is checked by reflection.

- [x] 2026-11-14 — `RecoveryCandidateTests` covers the bounded best-candidate bank
  and all generated state properties; `AuditPhaseBranchTests` and the generated
  `OverlappingPhaseInventoryTests` check the separate physical/phase witnesses.

- [x] 2026-11-14 — CPU audit A01/A35 (`AuditPhaseBranchTests`): frozen inputs and independent witnesses in
  AuditCases.json are checked by AuditPhaseBranchTests; old-source controls
  fail phase stability and HP/SP objective ordering, positive TP witnesses pass.

- [x] 2026-09-14 — L0 green: `DenseSolverTests` (5 tests), `InvalidInputTests` (8),
      `AbsentElementTests` (3 cases), `ElementConservationTests` over the enumerated
      tp, hp and sp files.
- [x] 2026-09-14 — L1 green for every fixture case of kinds tp, hp, sp, including the
      condensed cases: `FixtureSolveTests` over the enumerated tp, hp and sp
      directories; `CondensedSpeciesTests` over the fixture cases with condensed
      candidates (the alumina and the water-condensation tests); `FrozenModeTests`:
      `FrozenStationsOfTheReferenceAreReproducedFromTheFrozenComposition`
      over the reference rocket cases with a frozen station, and the self-consistency
      cases, one per problem kind, over
      `FrozenModeAtTheEquilibriumCompositionRecoversTheEquilibriumState`,
      `AFrozenStateReportsTheFrozenHeatCapacitiesAsTheEquilibriumOnes` and
      `AFrozenStateCarriesTheIdealGasDerivatives`; `KernelEqualityTests` over
      the table families of the enumerated directory.

      ⚠ 2026-09-14: until this date these two criteria carried hand-typed fixture
      counts (106 tp/hp/sp files, 96 condensed cases, 51 frozen-station fixtures, 8
      table families) that had fallen behind the fixtures node's directories, in one
      case since before the tick was written (AGENTS.md §8: a number repeating the
      length of a list diverges at the list's first change). Dropped in favour of the
      enumerated directory itself, which is the list; found by the test review of
      2026-09-14 (F-TK-03). `FrozenModeTests`' single named test is replaced by the
      three tests its split into the same day (F-TK-11).
- [x] 2026-09-14 — L2 green: `PlateauTests.AnEnthalpyInsideTheALNGapPinsThePiecesAtTheCut`,
      `AnEnthalpyNoAdmissibleSetCanHoldIsRefusedRatherThanLiedAbout`, and
      `AnOkSolutionLeavesNoCondensedCandidateWithPositiveInclusionGain` (a
      theory over every hp fixture case). The class named itself L2 in its own comment
      since 2026-09-14 while this node's level table and criteria did not; both now
      say the same thing (F-TK-02).
- [x] 2026-09-12 — Every check proven non-degenerate once, by mutation runs on the
      reference machine, each restored afterwards: a reference `cpEquilibrium` raised by
      1 % in a tp fixture (1 red); the solver's convergence tolerances loosened to
      `0.5e-2` and `1e-2` (3 red of 106); `AL2O3(L)` omitted from the products of the AP
      chamber hp fixture while the reference keeps it (the condensed-set test and the
      solve comparison red); the solver's inclusion test disabled (23 red of 96); the
      kernel given a different estimate flag (8 red); the conservation invariant
      tightened to `1e-20` (106 red); a frozen-station reference temperature raised by
      1 K (1 red); negative abundances accepted by the solver (1 red).
- [x] 2026-09-14 — Bits level green:
      `BitSnapshotTests.EveryFixtureCaseGivesTheRecordedBits` over the
      enumerated tp, hp and sp directories against `Bits.approved.txt`, recorded from
      the code of `8e36a27` before any code of the decomposition moved (one line per
      enumerated fixture file, the directories being the list). Seen red three times,
      each mutation applied alone and restored: the solver's `StandardPressure`
      perturbed by one ULP (`1.0e5` → `100000.00000000001`), which reported 22 cases
      with moved hashes; one line deleted from the approved file, which reported that
      case with the instruction to approve; and, 2026-09-15, a line added for a tp
      fixture that does not exist (`tp/does-not-exist_pc1MPa_shiftingEquilibrium.json`
      with a zero hash), which reported "1 fixture case(s) no longer give the recorded
      bits:\ntp/does-not-exist_pc1MPa_shiftingEquilibrium.json: recorded in the
      approved snapshot, but no longer a fixture case" — the check `AGENTS.md` §13
      requires every check be shown red, not run before on this node's own
      `Bits.approved.txt` (R-Equilibrium.Tests-4). Reverted immediately after; the
      approved file's hash unmoved (`65788e23f4390305763c80ab1f66b2054ff1907a`).

      The failure message listed stale keys after the hash mismatches, capped at 20
      entries, so a run with 20 or more hash mismatches could hide an orphan key behind
      "… and N more"; the message now lists stale keys first, so the cap cannot hide
      them (the fact above shows the reworded message). A change of a test's own
      failure message, not of what it checks.

      ⚠ 2026-09-14: this criterion was written the same day predicting "every case
      red" for the perturbed constant. Wrong: the Newton iteration polishes until its
      corrections fall below `1e-11` and its fixed point absorbs a last-ULP change of
      an input in 93 of the 115 cases — a one-ULP shift of `ln(p/p°)` is below the
      rounding of the sums it enters. The 22 that did move are what makes the check
      non-degenerate; the figure is recorded rather than the quantifier
      (`AGENTS.md` §8: an absolute word needs proof or a caveat).
- [x] 2026-09-15 — The creation of `BatchViews` in `KernelEqualityTests` names its
      arguments, in the order of the parameters (the root's condition on a declared
      wide constructor, the row of `## Shape exceptions`); one site,
      `KernelEqualityTests.FillAndLaunch`, fully named; covered by
      `ShapeTests.EveryWideConstructorIsCalledWithNamedArguments`, green at
      `62cd99e`; the node's bit snapshot unchanged (`Bits.approved.txt` hash
      `65788e23f4390305763c80ab1f66b2054ff1907a`, the fast suite 463/463 green).

- [x] 2026-09-15 — `HostSolution` (`HostSolver.cs`) restructured from 7 to 3
      parameters, within the root's limit, along the domain axis of input against
      converged output: `Moles`, `Multipliers`, `State`, `Status` and `Iterations`
      moved into a new `Convergence` record (5 parameters, also within the limit),
      read back through forwarding properties (`HostSolution.Moles` and the rest) so
      every existing read call site is unchanged; only the one construction site, in
      `HostSolver.Run`, changed. Not a declared exception: no row added to
      `## Shape exceptions`. 463/463 tests green, `Bits.approved.txt` hash unchanged
      (`65788e23f4390305763c80ab1f66b2054ff1907a`).

      ⚠ 2026-09-15, the same day: this tick recorded a cut made to fit the root's
      parameter limit, not the domain's axes — `Convergence` was never read as a value
      anywhere in the tree, only through the five forwarding properties, and it also
      held `InvalidInput` results with zero iterations and every `SolveFrozen` result,
      for which "what the solver converged to" was false. Found by the repair review
      (R-Equilibrium.Tests-1). Reformulated below rather than deleted, since the
      history of a criterion is part of the context (AGENTS.md §6).
- [x] 2026-09-15 — `HostSolution` re-cut along the domain axis the first attempt
      missed: the case solved (`EquilibriumCase Case`) plus the five views of
      `EquilibriumResult` copied out directly (`Moles`, `Multipliers`, `State`,
      `Status`, `Iterations`), 6 parameters, within the root's limit without an
      exception. `Convergence` and the five forwarding properties deleted; every
      existing output read (`.Moles`, `.Multipliers`, `.State`, `.Status`,
      `.Iterations`) is unchanged, and the 14 `.Table` reads and 2 `.ElementMoles`
      reads become `.Case.Table` and `.Case.ElementMoles` (`AbsentElementTests.cs`,
      `CondensedSpeciesTests.cs`, `ElementConservationTests.cs`, `FrozenModeTests.cs`,
      `StateComparison.cs`, and `HostSolver.cs`'s own `MoleFraction`); only the one
      construction site, in `HostSolver.Run`, changed. 464/464 tests green (463 plus
      the defect fact below), `tests/Equilibrium.Tests/Bits.approved.txt` unchanged
      (`65788e23f4390305763c80ab1f66b2054ff1907a`); the public surface does not move,
      `HostSolution` and `EquilibriumCase` both internal.
- [x] 2026-09-15 — L2: `PlateauTests.AStoodDownRecordIsNeitherAdjacentToNorFoundBesideItsInPlayPartner`
      proves the node's own defect fix (`src/Equilibrium/ACCEPTANCE.md`, the
      criterion of the same date): seen red on the code before the fix
      (`PhaseGeometry.Adjacent` returned the stood-down piece's table index, 231,
      instead of −1) and green after it, with no bit of `Bits.approved.txt` moved.

- [x] 2026-09-27 — The NaN and cv guards (Constraints).
      - `ElementConservationTests` scanned for a bound comparison the manual way
        (`grep -n "if (" *.cs | grep -E "[<>]="`, then read); the one predicate
        recording a violation from a solver-computed value was
        `ElementsAreConservedAtTheInvariantTolerance`'s `if (residual > bound)`,
        rewritten `if (!(residual <= bound))`. Every other comparison found the same
        way (`CondensedSpeciesTests`, `PlateauTests`) is either already the NaN-safe
        negated form or a direct `Assert.True(condition)` on the "must hold" condition,
        which a NaN condition already fails correctly; none records a violation into a
        list under an un-negated `>`/`<` test, so none needed rewriting.
        `ElementConservationTests.ANaNMoleNumberIsAConservationViolation` sets one mole
        of a real `Ok` solution (`rp1311-example1_r1.0_p1.0atm_T3000`) to `NaN` and
        asserts the (now factored-out) `Violations` helper reports it. Shown red with
        the old form restored: `Assert.NotEmpty() Failure: Collection was empty`.
      - `FrozenModeTests`' `NotFrozenFields` no longer carries `cvFrozen` or
        `cvEquilibrium`; a new `ShowsTheFrozenCvDefectSignature(station,
        freezingStation)` reads both fields of the current and the freezing station
        from the fixture itself and returns whether they show the defect (both zero, or
        both equal to the freezing station's own), and `CompareStation` skips the two
        fields only where it does. `AFrozenExitOutsideTheSignatureIsCompared` writes a
        copy of `lox-lh2_of4_pc10MPa_frozenAtChamber.json` to a temporary directory (never
        the committed file, deleted afterwards) with `exit1`'s `cvFrozen`/`cvEquilibrium`
        set to 2500/9999 — neither zero nor the freezing (`chamber`) station's own
        3825.2/4819.6 — and asserts the comparison now reports a mismatch on one of
        the two fields; the same copy with both set to zero (the defect's own
        signature) reports none. Shown red with the old, unconditional skip restored
        (`NotFrozenFields` carrying `"cvFrozen"`, `"cvEquilibrium"` again):
        `Assert.Contains() Failure: Filter not matched in collection` against an empty
        mismatch list — the 2500/9999 copy passed silently, exactly the defect BOOT.md
        records (a fixture copy with cv 2500/9999 left this node's suite green).
      - Bits: no `Bits.approved.txt` or `Bits.linux.approved.txt` of any node moved
        (`git status` before and after, unchanged); the two new facts run against real
        or temporary-copy fixtures only, writing nothing into the committed tree.

      Evidence: `dotnet test tests/Equilibrium.Tests`, 697/697 (695 plus the two new
      facts), none skipped; the full fast suite of the tree green
      (`APTHERMO_NO_CUDA=1 dotnet test APThermo.sln --filter "Category!=LongRunning"`).

- [x] 2026-10-03 — The pinned set (`src/Equilibrium/StateRecord/BOOT.md`, `## Constraints`; the
      orchestrator's investigation B3 for 0.2.1), on the CPU accelerator, each fact red on the old
      pair rule (`PhaseGeometry.SameFormula` restored in `DerivativeSystem` once: 25 red of 39 facts,
      the rest green as designed) and green on the linear-dependence rule:
      - `ReactionPlateauTests.ASeededCaseReproducesTheReference` over the 30 `seeded` fixtures:
        `Ok`, every first-order field and mole fraction within the tolerance table, clear of
        `EquilibriumConditions.Violations`; the 18 band cases red (`TemperatureOutOfRange`), the 12
        edge cases green on both rules.
      - `TheBandCasesShareOnePlateauTemperatureAndHoldTheReferencesCondensedSet` (one temperature
        at 1, 7 and 20 MPa and in both problems, the reference's condensed count),
        `ThePlateauConventionHoldsInTheBand` and
        `GammaSAndTheIsothermalDerivativeMatchTheirFiniteDifferences` (three pressures,
        `Tolerances.FiniteDifference`).
      - `PinnedSetTests`: three dependent species `Solved` and pinned, two independent ones not, a
        pair still pinned with the order restored, and the representative the first species that is a
        combination of those before it.
      - `StateComparison.SecondOrderIsSingular` skips the six second-order fields on the reference's
        singular signature, as the singular tp case does. No line of `Bits.approved.txt` moved;
        `dotnet test tests/Equilibrium.Tests`: 1077/1077.
      The StateRecord criterion that names these facts is ticked by the orchestrator after CUDA and Linux.

- [x] 2026-10-04 — The trace-gas pass, the close guard and the trace-gas finals
      ([TraceGas/BOOT.md](../../src/Equilibrium/TraceGas/BOOT.md), [Recovery/BOOT.md](../../src/Equilibrium/Recovery/BOOT.md)),
      on the CPU accelerator, every fact asserting `EquilibriumConditions` at 1e-9 with every gas of any share
      (`TraceGasChecks`, `EquilibriumConditions.EveryGasViolations`):
      - `TraceCarrierWalkTests`, `TraceExcessTests`, `ResidueVerdictTests`, `TraceGasUnitTests`, `GasBasisStartTests`
        (2026-10-05, the gas basis), `GasMixtureStartTests` (2026-10-05, its mixture column), `TraceGasRoundTests` (2026-10-05, the second round): the families
        `TraceGasCases` generates from their parameters, none typed;
      - `NoHiddenStateTests` (2026-10-04, the nondeterminism investigation): 376 cases (every tp, hp and sp
        fixture, the magnesite band, the declared leftovers, the nine states the gas basis settles, the 15 states
        of the band its mixture column settles (2026-10-05), CaCO3 +
        1e-7 O over a table of CaCO3(cr) and O2 alone, built to end `SingularMatrix` after every start of
        the pass since 2026-10-05, KO2 − 1e-10 O, the junction states warm) run with
        every buffer the solver owns or writes zeroed, filled with NaN and filled with 1e300
        (`HostSolver.SolveFilled`, `BufferFill`): the same status, iterations, moles and multipliers bit for
        bit, and the same state for `Ok` and `NoGasPhase`; a failure writes no state and the harness zeroes
        it. `HostSolver` clears every buffer before a run (ILGPU's `Allocate1D` does not), and
        `UnivariantRig` refuses a failed tp solve as the source of its targets (it had taken them from the
        unwritten state of one). Red with the estimate of the gaseous moles left unwritten by the cold branch of
        `CaseSetup.Begin`: 180 differences in the 360 cases;
      - `BalancingRecordTests` (2026-10-05, the balancing record, [Condensed/BOOT.md](../../src/Equilibrium/Condensed/BOOT.md)):
        the 45 states of CaCO3 + 1e-7 to 1e-9 O at 100 kPa to 10 MPa and 300 to 500 K and the three the rule
        settles, each `Ok` and clear, and the 11 states of MgCO3 + O whose set alternated to the cap (the last
        change, 2026-10-05); `CondensedSet.Update` on a rig keeps the record at zero when its amount
        is rounding and its removal singular, or at the cap, and removes it when it is not rounding, when the element rows are
        independent without it, and when they are dependent with it too (each test shown red by removing it);
      - `TraceGasClosureTests` (2026-10-04, the relative invariant `EquilibriumConditions.ElementInvariant`,
        `1e-13 · b_i`; a gasless state is held to `EquilibriumConditions.GaslessElementResidual`, the
        verdict's absolute 1e-12): the 72 plateau states of calcite and magnesite and Al(OH)3 + 1e-6 O,
        each `Ok` and clear with every element within the invariant;
      - `GasStationarityTests` (the guard's bound, and the states it refused through the bracket),
        `TraceGasFinalTests` (the ownership of the finals, cold calcite states, the retried final),
        `TemperatureBracketTests` (the arms of seam (b)), `TiedDerivativesTests` and `ScaledDerivativeTests`
        (the two derivative retries against a central difference), `SupercooledVapourSpTests` (the sp states
        of the vapour below the floors), `BracketedStateTests` (the magnesite band no longer excluded);
      - `TraceGasScanTests`, `LongRunning`: the 4 158 tp states of `TraceGasCases.ScanFamilies` against
        `TraceGasScanBaseline.txt` (the status of each before the pass, recorded once with the pass, the guard
        and the finals switched off) and `TraceGasLeftovers.txt` (the declared states, each asserted as
        declared, so a settled one makes the list stale).
      Each rule shown red once by removing it (counts in the nodes' own criteria); no line of a
      `Bits*.approved.txt` moved on Windows. `dotnet test tests/Equilibrium.Tests`: 1 798 facts (1 797 of the fast
      set and the scan fact), green.

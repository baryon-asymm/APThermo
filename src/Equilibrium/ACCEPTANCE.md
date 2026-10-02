# ACCEPTANCE.md — Equilibrium

## Acceptance criteria

- [x] 2026-09-12 — tp problems: for the product mixtures of the four reference
      propellants and the RP-1311 examples in the fixtures node, the mole fractions of
      every species the reference prints agree within the fixtures node's tolerance
      table; the list of compared species is generated from the fixture, not typed.
      `Equilibrium.Tests`, `FixtureSolveTests.AssignedTemperatureAndPressureReproducesTheReference`
      over the enumerated `cases/tp` directory (46 files): every state field the fixture
      carries and every listed species, within the table.
- [x] 2026-09-12 — hp problems: the adiabatic flame temperature and the composition of
      the same cases agree within the tolerance table.
      `FixtureSolveTests.AssignedEnthalpyAndPressureReproducesTheReference` over
      `cases/hp` (34 files).
- [x] 2026-09-12 — sp problems: the temperature and composition at given entropy and
      pressure agree within the tolerance table (the nozzle stations of the reference
      rocket cases serve as fixtures).
      `FixtureSolveTests.AssignedEntropyAndPressureReproducesTheReference` over
      `cases/sp` (26 files).
- [x] 2026-09-12 — Condensed species: the AP/binder/aluminium case includes `AL2O3(L)`
      in the chamber with the reference mole fraction (0.07645), and the low-temperature
      RP-1311 example 14 reproduces the reference phase changes.
      `CondensedSpeciesTests`:
      `TheAluminizedPropellantBurnsToLiquidAluminaInTheChamber`,
      `WaterCondensesBelowItsDewPointInTheLowTemperatureExample` (liquid at 300 to 304.3
      K, none from 305 K) and `TheCondensedSpeciesInTheSolutionAreThoseOfTheReference`
      over every fixture case with condensed candidates, `AL2O3(a)` at the AP/HTPB/Al
      exits included.
      ⚠ 2026-09-14: was a derived mass fraction asserted against a typed 1e-4, now
      removed → HISTORY.md#crit-condensed
- [x] 2026-09-12 — Derivatives: `Cp_eq`, `γ_s` and the sound speed agree with the
      reference within the tolerance table for every converged fixture case: part of
      the `FixtureSolveTests` comparison above (`cpEquilibrium`, `cvEquilibrium`,
      `gammaS`, `dlnVdlnT`, `dlnVdlnP`, `soundSpeed` for all 106 cases).
- [x] 2026-09-12 — Element conservation holds for every converged fixture case at
      the invariant's tolerance:
      `ElementConservationTests.ElementsAreConservedAtTheInvariantTolerance` over
      the machine-generated list of the 106 tp, hp and sp cases.
- [x] 2026-09-12 — A case with an absent element gives the same result as the same case
      solved on a table without that element's species, bit for bit on the same
      accelerator: `AbsentElementTests.AZeroAbundanceEqualsATableWithoutTheElement`; an
      empty table or every abundance zero returns `InvalidInput` and writes nothing
      else: `InvalidInputTests`. → HISTORY.md#crit-absent
- [x] 2026-09-12 — The solve runs unchanged inside an ILGPU kernel on the CPU
      accelerator with the same results as the host call:
      `KernelEqualityTests.KernelAndHostGiveTheSameBits` over the 8 table families
      of the 106 cases (moles, multipliers, state, status and iterations bit for bit).
- [x] 2026-09-13 — Plateau states converge and match the reference: the melting-plateau
      fixture cases (RP-1311 example 13, the direct plateau stations of AP/HTPB/Al, the
      latent-heat-band hp cases) return `Ok` with both records of the pair in the
      solution at the pair's `T*`, every compared field (`γ_s`, the sound speed and the
      plateau zeros included) within the tolerance table: `FixtureSolveTests` over every
      tp and hp file, `Performance.Tests.RocketFixtureTests` and
      `Problems.Tests.RocketTests.TheRocketCaseReproducesTheReferenceEndToEnd` over
      `rp1311-example13` and the eight `ap-htpb-al-plateau` rocket files.
      → HISTORY.md#crit-plateau
- [x] 2026-09-13 — The anti-cycling rule closes the include/remove cycle: an assigned
      enthalpy inside the `ALN(L)` gap of the fuel-rich AP/HTPB/Al chamber converges
      onto the pinned pieces
      (`PlateauTests.AnEnthalpyInsideTheALNGapPinsThePiecesAtTheCut`); a record removed
      for range re-enters when it is the only positive candidate, a second escape stands
      it down, and an `Ok` exit never hides a positive-gain candidate
      (`PlateauTests.AnEnthalpyNoAdmissibleSetCanHoldIsRefusedRatherThanLiedAbout`,
      `AnOkSolutionLeavesNoCondensedCandidateWithPositiveInclusionGain` over every hp
      fixture). → HISTORY.md#crit-anti-cycling
- [x] 2026-09-13 — Sweeps across a plateau lose no station: the pressure-ratio band
      across the AL2O3 plateau solves sequentially and one exit at a time onto the same
      stations, on the chamber isentrope
      (`Problems.Tests.SplitRecordTests.ASweepAcrossTheAluminaPlateauStaysOnTheIsentropeByEitherPath`);
      example 13's four exits cross the BeO plateau end to end
      (`RocketTests.TheRocketCaseReproducesTheReferenceEndToEnd` over
      `rp1311-example13`); tp solves at the printed bounds pick the reference's record
      (`FixtureSolveTests` over `ap-htpb-al-plateau_T2327`,
      `rp1311-example13-mixture_T2851` and `_T2373`).
      ⚠ 2026-09-13: was fine sweeps of both plateaus from both starts, now the
      eight-ratio band and the four-exit example → HISTORY.md#crit-sweeps
- [x] 2026-09-14 - The decomposition of `## Structure` is in place and changed no
      number. Shape: `ShapeTests`, all ten facts green at `62cd99e`. Surface:
      `Protocol.Tests.SurfaceTests` against
      `tests/Protocol.Tests/PublicSurface.approved.txt`, untouched, every new type
      internal. Numbers: `BitSnapshotTests.EveryFixtureCaseGivesTheRecordedBits` against
      `Bits.approved.txt`, recorded from `8e36a27` before the first line moved, unmoved
      after the last; `KernelEqualityTests` green; the fast suite green after each of
      the six extraction steps (2142 tests that day). This node's evidence is of the CPU
      accelerator.
      ⚠ 2026-09-15: was `Converge` at 56 lines and no shape exception, now `ShapeTests`
      → HISTORY.md#crit-decomposition
- [x] 2026-09-14 - The rules found written twice exist once each: the inclusion gain of
      section 3.4 (`CondensedSet.InclusionGain`), the element abundance
      (`ElementBalance.Abundance`), the trace retention (`Composition.Retain`), the
      state record (`MixtureProperties.Common`); every number of the report is a named
      constant in the stage that uses it. Checked by reading at the close of the
      decomposition; the bit snapshot proves the reading moved no number.
      → HISTORY.md#crit-once-each
- [x] 2026-09-14 — The node decodes none of `Thermo`'s interval layout (F-AR-01): no
      `IntervalStart`, `IntervalCount` or `IntervalBounds` in `src/Equilibrium/**/*.cs`
      (grep empty, re-run 2026-10-02 over the child nodes too), the record bounds asked of `SpeciesFunctions.RecordLow` and
      `RecordHigh`; `BitSnapshotTests.EveryFixtureCaseGivesTheRecordedBits` unmoved. Red
      once: `RecordHigh` returning the lower bound turned 29 fixture cases red.
      → HISTORY.md#crit-interval
      ⚠ 2026-10-02: was `src/Equilibrium/*.cs`, now `src/Equilibrium/**/*.cs` → HISTORY.md#crit-greps-split-2026-10-02
- [x] 2026-09-14 — The Newton loop holds no formula (`## Structure`): `NewtonIteration`,
      `DampedStep`, `ConvergenceTests` and `SingularRemedies` as the table says, each
      within the root's code shape, `NewtonIteration` named the second composition root;
      `BitSnapshotTests.EveryFixtureCaseGivesTheRecordedBits` unchanged and
      `KernelEqualityTests` green in the same run. → HISTORY.md#crit-newton
- [x] 2026-09-14 — Every creation of `EquilibriumScratch` in the tree names its
      arguments (the decision "The scratch descriptor keeps its constructor"): the one
      site, `EquilibriumScratch.Slice`, names every argument;
      `ShapeTests.EveryWideConstructorIsCalledWithNamedArguments` holds it;
      `Bits.approved.txt` unchanged. → HISTORY.md#crit-named
- [x] 2026-09-15 — A record stood down by the anti-cycling rule stays out of play "for
      the rest of it": `PhaseGeometry.Adjacent` and `PhaseGeometry.PhaseAt` test
      `!SpeciesMarks.InPlay(scratch, k)`, not the raw `scratch.SpeciesActive[k] == 0`
      (R-Equilibrium-1); no `SpeciesActive[` remains outside `SpeciesMarks.Of` and
      `.Set`, grep re-run 2026-10-02 over `src/Equilibrium/**/*.cs`, the child nodes included. Red before the fix, green after:
      `PlateauTests.AStoodDownRecordIsNeitherAdjacentToNorFoundBesideItsInPlayPartner`;
      `Bits.approved.txt` unchanged. → HISTORY.md#crit-stood-down
      ⚠ 2026-10-02: was the grep over the node's directory, now over its children too → HISTORY.md#crit-greps-split-2026-10-02
- [x] 2026-09-15 — Every ticked criterion above re-verified on the decomposed and
      repaired code at `62cd99e`: its tests green in the full suite
      (`APTHERMO_NO_CUDA=1`, every category, 3037 tests, none skipped), and
      CUDA-category evidence on the reference machine (`tests/Execution.Tests`, 41,
      and the long-running sweep and throughput tests).
- [x] 2026-09-26 — The audit's findings 1 to 5 and the open-below rule (the ⚠ notes of
      this date under Constraints, and the Thermo node's criterion of the same date for
      finding 1).
      - **Below 300 K and below 200 K.** Fixtures computed by cea 3.3.4, covered by
        `Problems.Tests.EquilibriumTests.AssignedTemperatureCasesReproduceTheReference`
        through its directory listing: Si and Li in argon at 298.15 to 301 K; H2/O2 at
        O/F 4 and 1 bar at 165, 180, 190 and 199 K, red on the unpatched open-below
        rule.
      - **The full set.** The audit's 17-element case at 350 K is `Ok` with every stable
        phase
        (`tests/Fixtures/cases/tp/seventeen-elements-many-condensed-phases_T350.json`,
        the same reference test);
        `PlateauTests.AnOkSolutionLeavesNoCondensedCandidateWithPositiveInclusionGain`
        runs over every tp, hp and sp fixture; the exit guard red once with
        `MaxCondensedInSolution` back at 8.
      - **Warm starts.**
        `WarmStartTests.AWarmSolveAtHalfPressureAgreesWithAColdSolveAtThatPressure` over
        every tp fixture that converges
        (`ANamedFixtureCompletesTheFullWarmStartComparison` pins one by name), and
        `TheAuditsExactCasesWarmStartOkAndAgreeWithAFreshColdSolve` for the audit's
        three probe cases; the four `rp1311-example14` cases end `Ok` through the
        fallback to the cold start of 2026-09-27, `SingularMatrix` with it disabled.
      - **The bookkeeping.** `NewtonLoopStateTests`:
        `ANotConvergedVerdictClearsTheConvergedMarkAndThePolishCount`,
        `ReportTestsMetCountsAPolishStepAndPolishedDoesNotCountAnother`,
        `ASpeciesCrossingTheTraceThresholdDuringTheStepIsReportedAsACrossing`,
        `NoCrossingWhenEveryGasSpeciesKeepsItsSideOfTheTraceThreshold`,
        `ASingularRemovalOfACondensedSpeciesRestartsTheStepCountAndCountsTowardTheChangeCap`,
        `RecordSetChangeResetsTheStepCount`, each red once against the line it guards.
      - **Frozen mode.**
        `InvalidInputTests.FrozenModeRejectsAnInvalidMoleNumberGaseousOrCondensed` (NaN,
        infinite, negative; gaseous and condensed; tp, hp, sp).
      - **Bits.** No `Bits.approved.txt` of `Equilibrium`, `Thermo` or `Problems` moves
        on an existing case.
      Evidence: `APTHERMO_NO_CUDA=1 dotnet test APThermo.sln --filter
      "Category!=LongRunning"` 4278/4278, none skipped (`Equilibrium.Tests` 695);
      `dotnet test tests/Execution.Tests -c Release` 126/126 on CUDA; the protocol lint
      0 errors/0 warnings.
      ⚠ 2026-09-26: was the warm-start comparison named the fixtures node's polish tier,
      now `Tolerances.SelfConsistency` → HISTORY.md#crit-audit-2026-09-26
      ⚠ 2026-09-26: was the audit's probe parameters "not recorded", now its three exact
      cases reproduced → HISTORY.md#crit-audit-2026-09-26
      ⚠ 2026-09-26: was four `rp1311-example14` warm starts left open as a plateau-pair
      defect, now the cold-start fallback → HISTORY.md#crit-audit-2026-09-26
      ⚠ 2026-09-27: was that explanation (a seeded plateau pair), now a seeded liquid
      above its saturation pressure → HISTORY.md#crit-audit-2026-09-26
- [x] 2026-09-30 — The second hidden-defect audit of 2026-09-28 (Thermo and Equilibrium,
      findings F1 to F5, and the guards part's O8 and F9) is closed by the rules of that
      date under Constraints.
      Evidence at `9284418` and after, on the reference machine: `dotnet build
      APThermo.sln` 0 warnings, 0 errors; the fast suite green on Windows and under WSL2
      (Equilibrium 944 of 944 under WSL2); the protocol lint 0 and 0; `dotnet test
      tests/Execution.Tests -c Release` on CUDA 171 of 171 on Windows and 170 of 170
      under WSL2; the release job's filter green on Windows in Release. Known and
      outside the criterion, by the owner's decision of 2026-09-28: the three classes at
      the end of the rules A and B criterion below, for 0.2.1.
      ⚠ 2026-09-30: was the criterion unticked with an "Open" list, now ticked, the list
      closed by rules A and B → HISTORY.md#crit-second-audit
      - **F1, the two-stage threshold.** Fixtures from cea 3.3.4 through the fixtures
        node's generator, covered by `AssignedTemperatureCasesReproduceTheReference`,
        each red at `5a732f0`:
        `tests/Fixtures/cases/tp/rp1311-example5_T300_p1bar.json`,
        `..._T300_p70bar.json`, `..._T305_p10bar.json`
        (`tests/Fixtures/generate/retention_threshold.py`); a unit fact on the loop's
        struct (the switch counts as a change of the retained set);
        `tests/Equilibrium.Tests/RegressionStateTests.cs`:
        `TheAuditsRegressionStateConvergesAndHoldsTheEquilibriumConditions` (sixteen
        states, red at `5a732f0`) and
        `TheAuditsElevenExample5StatesThatFailedAtBothCommitsAreOkNow`.
      - **F5.** `tests/Equilibrium.Tests/DenseSolverTests.cs`:
        `TheFailedRowOverloadNamesTheRowWhosePivotVanished`,
        `TheFailedRowOverloadReportsNoFailureOnARegularMatrix`;
        `NewtonLoopStateTests.ASingularRemovalOfACondensedSpeciesRestartsTheStepCountAndCountsTowardTheChangeCap`.
      - **F2.** A tp of the `h2-o2-of4` table at 1 bar, 60 K to 159 K:
        `TemperatureOutOfRange`; 160 K and above `Ok` with finite positive Cp, Cv, `γ_s`
        and sound speed; the fixtures at 165–199 K stay green; a fact over every `Ok` of
        the fixtures and of the audit's grids, the pinned pair's zeros excepted.
      - **F3.** `WarmStartTests` extended to P/10, P/2 and T×1.1 over the fixture tables
        at 300 K and 600 K; the step-cap trigger has a fact of its own (guards F9); the
        hp and sp cold retry starts at 3 800 K.
      - **F4.** `InvalidInputTests`: frozen tp at +∞, NaN and 1e-300 K, and `Solve` tp
        at +∞.
      - **O8.** `ElementBalance.WithinInvariant` reads a NaN abundance as outside the
        invariant; a fact drives the method itself.
      - **Bits.** Moved by the two-stage threshold in this node and in `Performance`,
        `Transport`, `Problems` and `Cli`; re-approved on the owner's decision of
        2026-09-28 with the field-by-field report of the largest relative change per
        field, every CEA tolerance test green; `API.md` states the changes.
        → HISTORY.md#crit-second-audit
- [x] 2026-09-28 — Rules A and B (Constraints, the orchestrator's investigation 6 of
      2026-09-28) close the open items of the criterion above.
      - [x] 2026-09-28 — **Fixtures** through the fixtures node's generator
        (`tests/Fixtures/generate/retention_threshold.py`):
        `tests/Fixtures/cases/tp/naclo4_T500.json`, `naclo4_T800.json`,
        `kclo4_T500.json`, `kclo4_T800.json`, `ap-htpb-al_pc7MPa_T430.json`,
        `ap-htpb-al_pc1MPa_T420.json`; all six red without rules A and B (the two blocks
        of `SingularRemedies.Recover` disabled in turn) and green with them.
      - [x] 2026-09-28 — **Unit facts** in `tests/Equilibrium.Tests`:
        `SingularRemedyRulesTests.cs` (`ElementCoupling.Find`/`Coupled`,
        `CondensedDependency.LeavingPosition`) and
        `WarmStartTests.AWarmStartFromExample5sTenBarSolutionTiesNAndClThroughNH4CLAndEqualsItsColdSolve`.
      - [x] 2026-09-28 — **The scans** as measurements, recorded, not asserted: the AP
        scan 850/850 `Ok`; the audit's salt scan 1436 `Ok`, 16 `NotConverged`, 0
        `SingularMatrix` against 332 and 122 of 1452 before; the fuzz of 40 985 solves
        with no equilibrium-condition violation.
      - [x] 2026-09-28 — **No bit snapshot moves on an existing fixture**
        (`BitSnapshotTests` of `Equilibrium.Tests`, `Thermo.Tests`, `Problems.Tests`:
        six added lines, no existing hash moved).
      - [x] 2026-09-28 — **Shape.** No method over 6 parameters; the declared Ce rows
        are re-measured.
      - [x] 2026-09-28 — `API.md`'s `SingularMatrix` sentence lists the remedies, with a
        ⚠.
      Open and known, outside this criterion; the owner decided on 2026-09-28 that they
      do not block 0.2.0 (`CHANGELOG.md`), designed and fixed for 0.2.1:
      - **The threshold flip.** Two carriers cross the threshold alternately every
        step, so the polish never completes: KClO4 at 610–680 K, NaClO4 at 490–500 K,
        16 salt-scan states `NotConverged`. The matrix is never singular.
        ⚠ 2026-09-28: was the threshold flip a band of two perchlorate compositions, now
        a mechanism of the all-gas first stage (29 of 968 states)
        → HISTORY.md#crit-rules-ab
      - **The three-element coupling.** With only CO2, H2O and N2 retained, row O
        equals 2·C + ½·H. 77 fuzz tp states on example 1 and example 12 tables at
        300 K and 600 K end `SingularMatrix`, which a pair tie cannot express.
      - **The reaction plateau.** hp inside the Al(OH)3/Al2O3/H2O(L) reaction plateau
        (T* = 415.948 K, 157 kJ/kg wide at 7 MPa) ends `SingularMatrix` in the
        derivative system: the pinned-pair convention covers two records of one
        formula only.
- [x] 2026-09-29 — The third audit pass of 2026-09-28 (part 1: findings F1 to F3,
      observation O1) is closed by the rules of that date.
      - **F1, the way back from a release.**
        `tests/Equilibrium.Tests/TiedReleaseTests.cs`,
        `TheReleasedTieRestoresAndClosesOk`: the six named salt states `Ok` and clear of
        every independent equilibrium condition (`EquilibriumConditions.Violations`),
        red at `c02e14d`; the audit's salt sweep, cold `NotConverged` 36 before and 30
        after, the thirty the declared threshold flip, zero violations.
      - **F2, the fallback covers the close.**
        `WarmStartTests.AFailureFoundAtTheCloseRetriesFromTheColdStart`, red at
        `c02e14d`.
      - **F3, no state on failure.**
        `tests/Equilibrium.Tests/MixturePropertiesTests.cs`, three facts: the state
        guard decides before `State` is written.
      - **O1, the hp/sp estimate.**
        `InvalidInputTests.AGivenHpOrSpEstimateThatIsNotFiniteAndPositiveIsInvalidInput`
        and `AZeroHpOrSpEstimateStaysTheNoEstimateSentinel`; a tp temperature follows
        the same rule with no sentinel.
      - **Bits.** No `Bits*.approved.txt` or `Throughput*.approved.txt` differs from
        `main`.
      Evidence: `dotnet build APThermo.sln` 0 warnings, 0 errors; the protocol lint 0
      and 0; `APTHERMO_NO_CUDA=1 dotnet test APThermo.sln --filter
      "Category!=LongRunning"` run project by project, 5243 of 5243, none skipped.
      `Cli.Tests` and `Docs.Tests` were not run to completion that pass: neither node's
      code, fixtures or approved output is touched, and `Problems.Tests`' own
      bit-for-bit fact is unchanged; left for the next session that touches this node to
      confirm.
      ⚠ 2026-09-28: was an hp/sp estimate of 0 refused, now a given nonzero estimate
      refused, 0 the sentinel → HISTORY.md#crit-third-pass
- [ ] 2026-10-02 — The split of this node into the child nodes `Newton`, `Condensed` and `StateRecord`
      (the arbiter's verdict D of 2026-10-01, `## Structure`) changes no behaviour:
      - [x] 2026-10-02 — on the CPU path: `git diff -M` shows the moved files differing only in the
        namespace, the `using` lines and the doc-comment references (`Carriers.cs` lost
        `ConvergenceVerdict` and `NewtonLoopState`, which `Newton/NewtonLoopState.cs` holds verbatim),
        `dotnet build APThermo.sln` gives 0 warnings and 0 errors, the protocol lint `--strict` gives 0
        and 0, `APTHERMO_NO_CUDA=1 dotnet test APThermo.sln --no-build --filter "Category!=LongRunning"`
        is green, 5442 of 5442 and none skipped, the bit snapshots (`Equilibrium.Tests`'
        `BitSnapshotTests.EveryFixtureCaseGivesTheRecordedBits` among them), `Protocol.Tests` (37,
        `DependencyTests`, `SurfaceTests`, `TreeContractTests`, `LintTests`) and `Equilibrium.Tests` (944)
        included, and no `Bits*.approved.txt`, `Throughput*.approved.txt`,
        `PublicSurface.approved.txt` or `TreeContract.approved.txt` changed;
      - [ ] on CUDA, on the reference machine: `dotnet test APThermo.sln -c Release --filter
        "Category=Cuda|Category=BitSnapshot"` and `dotnet test tests/Execution.Tests -c Release`, since the
        kernels compile the moved methods.
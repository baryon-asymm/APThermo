# ACCEPTANCE.md — Transport

## Acceptance criteria

- [x] 2026-09-14 — Every rocket fixture run with transport (enumerated by the tests
      node) reproduces viscosity, frozen and reacting conductivity, both Prandtl
      numbers and the reference's `cpFrozen` on the reference composition within the
      tolerance table; the reacting fields are skipped at the nine defective stations,
      where the test asserts the defect is still visible (`Transport.Tests`,
      `StationTests.StationFiguresMatchTheReference`,
      `StationTests.TheReferenceCpFrozenIsTheTransportSetHeatCapacity`,
      `StationTests.ReactingConductivityIsNeverBelowTheFrozenOne`,
      `StationTests.TheTraceComponentStationsCarryTheDocumentedReferenceDefect`;
      green on the decomposed code at `5cb2664`). Re-dated from 2026-09-12 (the
      decomposition of this date, AGENTS.md §6). → HISTORY.md#crit-station-redated
- [x] 2026-09-12 — The 27 fit fixtures (pure species and pairs) reproduce the
      independent Python evaluation within `transportFit`, the fit intervals of the
      table equal the file's, the fit rule agrees with the fixture on the shared bounds
      (`FitTests.FitValuesMatchTheIndependentEvaluation`,
      `FitTests.FitIntervalsAreThoseOfTheFile`).
- [x] 2026-09-12 — The estimate for species without data is exercised by the
      AP/HTPB/Al stations (26 of the 40 species of the chamber set) and the reported
      count and mole fraction are positive there; the station comparison above proves
      the estimate right to 3e-8
      (`StationTests.SpeciesWithoutDataAreEstimatedOnTheAluminizedPropellant`,
      `FitTests.SpeciesWithoutAnEntryHaveNoFitsAndAreListed`). The
      criterion stood "the exclusion policy is exercised … and the reported excluded
      fraction equals the independently computed one"; rewritten with the ⚠ above.
- [x] 2026-09-12 — Runs unchanged inside an ILGPU kernel on the CPU accelerator with
      the same bits as the host call (`KernelEqualityTests`, five batches).
- [x] 2026-09-13 — Every station of a case evaluated in a table that also holds the
      species of elements the case lacks gives the same bits as in the case's own
      table (`AbsentElementTests.ATableWithTheSpeciesOfAbsentElementsGivesTheSameBits`:
      LOX/RP-1 in the table with AP/HTPB/Al, LOX/LH2 with N2O4/UDMH, N2O4/UDMH with
      AP/HTPB/Al, every station with transport, every field of the figures); seen red
      with the table's gas count in the thresholds on two of the three pairs (the
      LOX/RP-1 throat set of 14 species against 13, the N2O4/UDMH sets of 21 against
      26, every figure moved).
- [x] 2026-09-14 — The decomposition of `## Structure`: every type of the node within
      the root's code-shape constraint (`TransportScratch.Slice`, the scratch
      descriptor, the declared exception to the parameter rule), the public surface
      unchanged (`Protocol.Tests.SurfaceTests` green against a
      `PublicSurface.approved.txt` that did not move a line, every new type internal),
      and every station's figures bit for bit those of `8e36a27` on the CPU
      accelerator: the tests node's `Bits.approved.txt` unchanged through all eight
      steps, `KernelEqualityTests`, `AbsentElementTests` and every criterion above
      green, the whole fast suite green (2144 tests). Covered by the protocol tests
      node's `ShapeTests`, all ten facts green at `62cd99e`.
      → HISTORY.md#crit-decomposition

      ⚠ 2026-09-15: was `TransportScratch.Slice` at 53 lines, now `ShapeTests` holds
      the size by machine (the figure was physical) → HISTORY.md#crit-slice-size
- [x] 2026-10-02 — The execution tests node's CUDA sweep green once after the decomposition (the
      long-running `CudaTests`, which this node's session does not run: the criterion
      above was split on 2026-09-14 so that what is proven and what is still owed are
      not one tick): `dotnet test tests/Execution.Tests -c Release` on the reference machine,
      173/173 with the three `LongRunning` facts, the sweep
      `TheSweepOf100000CasesOnCudaMatchesTheCpuAcceleratorAndIsDeterministic` among them, on `6dc2370`.
- [x] 2026-09-14 — `SingularMatrix` writes the frozen figures and reacting figures
      equal to them, as `API.md` promises:
      `Transport.Tests.StatusTests.AReactionSystemThatCannotBeSolvedKeepsTheFrozenFigures`
      drives `ReactionTerms` (through `InternalsVisibleTo`) over a set of three species
      and two reactions whose second system is singular while the first is not, and
      asserts the three equalities and the status; red against the code of `8e36a27`
      (the equilibrium heat capacity 10441.86 against the frozen 5001.70).
      `TheSameSetIsSolvedWhenEveryPairCarriesADiffusionWeight` keeps the first test
      from passing because both systems fail. → HISTORY.md#crit-singular-matrix
- [x] 2026-09-14 — Every creation of `TransportScratch`, `TransportTableView` and
      `TransportTableArrays` in the tree names its arguments (the decision "The scratch
      descriptor stays"): a scan of every `new T(…)` and `T x = new(…)` of the three
      names in `src/` and `tests/` found four sites, every argument named, the IL of
      the builds before and after identical, `Transport.Tests` (157) green and
      `tests/Transport.Tests/Bits.approved.txt` unchanged. The fact
      `ShapeTests.EveryWideConstructorIsCalledWithNamedArguments`, designed that day,
      takes over as the evidence. → HISTORY.md#crit-named-arguments
- [x] 2026-09-15 — Every ticked criterion above re-verified on the decomposed and
      repaired code at `62cd99e`: its tests green in the full suite
      (`APTHERMO_NO_CUDA=1`, every category, 3037 tests, none skipped), and
      CUDA-category evidence on the reference machine (`tests/Execution.Tests`, 41,
      and the long-running sweep and throughput tests).
- [x] 2026-09-27 — The reaction basis reverts a vanished pivot to its default species,
      as the reference does, and every estimated species is counted (the ⚠ notes of
      2026-09-26).
      - `Transport.Tests.ReactionConservationTests.TheAuditsRestrictedProductListConservesEveryReaction`:
        table `[N, O]`, products `[NO2, N2O4, N, O, N2, O2, NO]` at 400 K; every
        reaction of the set conserves every element (computed, not typed); red against
        the pre-fix `ReactionBasis.cs` (`9c33398`), where all five reactions failed.
      - `ReactionConservationTests.EveryReactionOfEveryStationsSetConservesTheElements`
        over every rocket fixture with transport, every station: green before and after.
      - `FitTests.ASpeciesWithViscosityFitsButNoConductivityFitIsCountedAsEstimated`:
        `UF6` counted in `EstimatedSpeciesCount` (1) and `EstimatedMoleFraction` (1.0);
        red against the pre-fix `SetSpeciesProperties.cs` (0).
      - No bit snapshot moved: the fast suite (3163 tests) and
        `tests/Transport.Tests/Bits.approved.txt` unchanged; a temporary counter over
        168 stations of 39 fixtures read 0 reverts and no `UF6`.
      → HISTORY.md#crit-reaction-basis

      ⚠ 2026-09-28: was the first bullet the proof of the revert, now the criterion
      below (the seeding follows the revert) → HISTORY.md#crit-reaction-basis-seeding
- [x] 2026-09-28 — The components are settled before the set is seeded, and every
      component is in the set (the ⚠ of 2026-09-28 under Constraints).
      - The new stage `ComponentBasis` runs between `TransportComponents.Select` and
        `TransportSetSelection.Select`, as `equilibrium.f90:2264-2291` reverts before
        `5221-5236` seeds; `ReactionBasis` no longer reverts, a zero pivot leaving its
        row unreduced.
      - `ReactionConservationTests.TheAuditsRestrictedProductListConservesEveryReactionWhenTheRevertedDefaultCarriesNoMoles`:
        `[NO2, N2O4, N, O, N2, O2, NO]` at 400 K with N and O at zero moles; every
        reaction conserves every element, `SpeciesCount` 6, `TraceEliminations` 1.
      - `ReactionConservationTests.TheAuditsRestrictedListMatchesEquilibriumsHeatCapacity`:
        the six-species list solved by `Equilibrium`, `EquilibriumHeatCapacity` equal to
        `Equilibrium`'s `CpEquilibrium` to 4.5e-18 relative, neither number typed.
      - `EquilibriumConsistencyTests.HydrogenFluorideAgreesBetweenElementOrdersOverTheAuditsGrid`:
        H:F = 2:1, 300 to 3 000 K, 1 kPa, 0.1 MPa and 10 MPa, both element orders; every
        reaction conserves and the two orders agree within `ConsistencyTolerance`.
      - `EquilibriumConsistencyTests.TransportsEquilibriumHeatCapacityMatchesEquilibriumOverStateSweeps`:
        the H2 + HF grid, the N2O4/NO2 list and the chamber state of one fixture per
        verification propellant, both element orders, over a generated non-empty list
        filtered to states with no trace elimination and no condensed species;
        `EquilibriumHeatCapacity` equals `CpEquilibrium` within `ConsistencyTolerance` =
        `100 · (1 − CoverageFraction + CoverageTolerance)` ≈ 1.0e-4, derived from the
        two named constants before the sweep, the observed worst case 4.4e-5.
      - The four new facts above red against `5a732f0`; the conservation fact over
        every fixture stays green and no `Bits*.approved.txt` of the tree moved.
      - The `InternalsVisibleTo` grant from `Equilibrium` to `APThermo.Transport.Tests`
        was decided at the root level (`AGENTS.md` §11); its `API.md` names it.
      Evidence: `tests/Transport.Tests` 167/167, the fast suite and the protocol lint
      green at the commit the tick names. → HISTORY.md#crit-components-settled

- [x] 2026-11-14 — A14: InputTests rejects nonfinite temperature and verifies
  finite-overflow refusal with zero figures; AuditGuardTests independently mutates
  every numeric output property. Removing the final guard yields Ok on the
  overflow control and fails the test. Normal pure-gas and fixture controls pass.
- [x] 2026-11-14 — A04 (this node's upload): BufferOwnershipTests exercises the
  production allocation transaction over every prefix of the generated view-buffer
  inventory, both failing cleanup resources, primary-error preservation and repeat
  disposal. Disabling rollback fails eight nonempty-prefix controls. An injected
  child OutOfMemoryException is aggregated while remaining resources are attempted;
  only the separately declared unrecoverable CLR exception classes propagate.
- [ ] `RollbackDiagnosticTests` preserves exact primary identity, attempts every owned
  resource and remains idempotent for ordinary/read-only/throwing Data, including
  injected OOM diagnostic payloads. Writable storage retains secondary diagnostics.

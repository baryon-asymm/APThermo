# BOOT.md — Problems.Tests

## Purpose

The definition of what "`Problems` is ready" means, and the front door of the whole
tree's acceptance: the end-to-end comparison with the reference implementation runs here.

| Level | What it checks | Against what (source of truth) | State |
|---|---|---|---|
| L0 | mass fractions, element moles and reactant enthalpy per kilogram; the oxidizer-to-fuel split; mole amounts; custom reactants; candidate species selection and order; input validation by name; the mass of a composition against one kilogram | the fixtures' recorded mass fractions, `elementMoles`, `reactantEnthalpy` / `enthalpy` and `products` (`PropellantTests`); documented behaviour, and the database's atomic weights for the mass a message reports (`RejectionTests`) | ✅ |
| L1 | every rocket, tp, hp and sp fixture solved singly from its propellant through the library | the fixtures node's reference outputs and its tolerance table (`RocketTests.TheRocketCaseReproducesTheReferenceEndToEnd`, the three `EquilibriumTests` theories) | ✅ |
| L2 | end to end over every rocket fixture with transport, in shifting and frozen flow; a sweep as one batch against its cases one by one; an elemental mixture against its propellant; identical problems alone and in one call; mixed exit layouts in one call; state batches over unions of elements; a failing station as a status | the fixtures; the single-case results of the same code, bit for bit, or to rounding where a union reorders a case's elements (`RocketTests`, `EquilibriumTests`) | ✅ |
| L2 | the melting-plateau states through the front door: a cut record reported once under its database name; an assigned enthalpy inside the `ALN(L)` gap solves; a sweep across the alumina plateau stays on the isentrope by either path | the node's own rules where the reference cannot follow: the join-and-cut of the `Thermo` node, the plateau of the `Equilibrium` node, the isentrope of the station's own chamber (`SplitRecordTests`) | ✅ 2026-09-13 (the row written 2026-09-14, the ⚠ below) |
| L2 | the contract of 2026-09-14: a state record with exits against its case through the batch over mixtures; the refusals of the record's shape; a batch mixing transport and none against each problem alone; a ratio and pressure product as one batch against its cases one by one; every public method of a disposed solver; the tolerance rule against `Create` | the same code's single-case results, bit for bit; the `Problems` `API.md`; reflection over the solver's methods (`RocketTests`, `RejectionTests`) | ✅ 2026-09-14 |
| Bits | the front door's result of every rocket, tp, hp and sp fixture solved singly from its propellant, as the L1 theories solve it, gives the recorded bits: one line per fixture in `Bits.approved.txt`, the fixture path and the SHA-256 of the raw bits of the mixture's element moles in element order, its enthalpy and mass, then per station the state, the performance figures, the transport figures, the mole fractions and condensed mass fractions in the result's species order and the statuses, then the case status, in that order; and the reverse, an approved line no enumerated fixture produces, fails the test naming the stale key | the approved snapshot, recorded before any code of the front door's decomposition of 2026-09-14 moved | ✅ 2026-09-14 |
| Protocol | the tree invariant, documents against code | `AGENTS.md`, the surface snapshot | ✅ (2026-09-13, the Protocol.Tests node) |

⚠ 2026-09-14: was a level table without the plateau row, now the L2 row above
→ HISTORY.md#purpose-plateau-row

## Invariants

- Reference is files; tolerances from the fixtures node; every fixture case is
  enumerated from the directory listing at run time (the theories' member data), so
  the root's first criterion is checked against a generated list.
- The reference's documented caveats (Fixtures BOOT.md) are applied by one comparison
  (`ReferenceComparison`, the caveats themselves and their field sets in
  `ReferenceCaveats`; both lived in `Comparison.cs` until the decomposition of
  2026-09-14), never by a test of its own: no `cv` at a frozen station; with
  transport on and condensed species present, the frozen `cp` against the transport
  set's; the reacting conductivity and Prandtl number not compared where the
  reference's value is defective, and at such a station the defect must be visible
  (the tree's reacting conductivity must not agree with the inflated one), so that the
  skip cannot hide a real disagreement; mole fractions by name, with the trace rule
  (`moleFractionTrace` below a printed 5e-6).
- A defective station is identified on the reference's own composition: the tree's
  transport solver, run through a CPU engine of the execution node on the reference's
  mole fractions, eliminates a trace component there (the signature the Transport
  tests use). On the tree's own composition the basis may differ and the signature
  vanish (the LOX/LH2 O/F 4 exits); identification there alone would compare the tree
  against the inflated value and fail.
- The reference rounds an oxidizer-to-fuel ratio to single precision before splitting
  the kilogram (Fixtures BOOT.md): L0 checks the exact path (the propellant given by
  the mass fractions the reference recorded) at 1e-10 relative and the ratio path at
  1e-7, derived from single precision's 6e-8.
- L2 runs on the CPU accelerator in the default command; the same cases on CUDA are
  the execution tests node's business.
- No expected value is typed into a test: everything comes from the fixture files or
  from another solve of the same code; `b_i` and `h_0` are the fixture's. The trace
  threshold of the mole-fraction comparison is the fixtures node's
  `ToleranceTable.MoleFractionField`. → HISTORY.md#invariants-trace-threshold
- **This node keeps its own reader of a fixture's outputs** (2026-09-14, the
  architecture review's F-AR-03): the field-name mapping (`ReferenceComparison.FieldValue`)
  and the set of fields that carry transport (`ReferenceCaveats.TransportFields`) stay
  here, not in the harness, which holds no formula and no tolerance. This node reads a
  station with transport figures on top of the state `Equilibrium.Tests` reads alone
  and the performance figures `Performance.Tests` reads on top of that; a shared
  reader would have to know all three shapes, which would put it above the nodes its
  readers' own consumers test.
- **The node owns the tolerances of comparisons that are not with the reference**
  (2026-09-14): two solves of the tree's own code that agree to rounding, a station on
  its chamber's isentrope, a pinned temperature on its transition bound. They are named
  constants of the node with their origin in a comment, never literals in an assertion
  (the review's F-TF-10 found thirteen such literals).
- **The bits are a tripwire, not a contract** (2026-09-14): the Bits level guards the
  front door's orchestration against unnoticed change the way the surface snapshot
  guards the contract (`AGENTS.md` §13). A moved line in `Bits.approved.txt` is
  legitimate only with the numerical change that moved it named in the same commit; a
  decomposition, a regrouping of batches, a renaming or a reordering of code moves no
  line. The snapshot is of the CPU accelerator on the reference machine's runtime; a
  runtime update that moves lines is re-approved with that reason recorded here. A
  fixture absent from the snapshot fails the test with instructions, as the surface
  snapshot does; and the reverse, an approved line no enumerated fixture produces
  (a deleted or renamed fixture), fails the test naming the stale key
  (`ApprovedSnapshot.StaleKeys`, the harness's own contract), so a fixture cannot
  drop out of the directory listing and out of this level's coverage unnoticed.

  The snapshot is a record of the reference machine, one for every platform (2026-10-05):
  `ApprovedPath` resolves through `Harness.ApprovedSnapshot.RecordPathFor`
  (`tests/Harness/API.md`) to `Bits.approved.txt`, so this node's own code names no
  platform. `EveryFixtureGivesTheRecordedBits` carries
  `[Trait("Category", "BitSnapshot")]`: it runs in every local run and in the release's
  self-hosted jobs (`release.yml`'s `cuda-windows` and `cuda-linux`, filter
  `Category=Cuda|Category=BitSnapshot`), and on every hosted runner too: the hosted runs
  (`ci.yml`; `release.yml`'s `matrix` job) filter `Category!=LongRunning` only.
  ⚠ 2026-10-05: was "filtered out of the hosted fast suite" (filter
  `Category!=LongRunning&Category!=BitSnapshot`, the front door's own tolerance tests
  holding correctness there), now run on every runner against the one record (root
  BOOT.md, Platform)
  ⚠ 2026-10-05: was one record per platform, now one record, the tree's own `Exp`, `Log`
  and `Pow` making the platforms equal → tests/Harness/HISTORY.md#one-record-2026-10-05
  ⚠ 2026-09-17: was one snapshot file, now one per platform; ⚠ 2026-09-19: was the
  bits a record of the platform, now of the reference machine
  → HISTORY.md#invariants-bits-platform
- **Bit comparison goes through the harness** (2026-09-14): `StationEquality`'s
  internal field-by-field bit comparison of `MixtureState`, `PerformanceFigures` and
  `TransportFigures` was, field for field, the harness's `Bits.Differences<T>`; its
  own `SameBits` was `Bits.Same`. `StationEquality.BitDifferences` and
  `RelativeDifferences` keep their signatures (a `Station` is not a flat struct: mole
  fractions and condensed mass fractions are dictionaries, and performance and
  transport figures are optional), but read the harness for the bit-exact leaves
  instead of repeating the comparison locally.
- The two union-batch facts that reorder a case's elements
  (`RocketProblemsOverSeveralMixturesAreOneBatchOverTheUnionOfElements`,
  `EquilibriumProblemsOverSeveralMixturesAreOneBatchOverTheUnionOfElements`) read
  `polishThresholdRelative` and `moleFractionFloor` from the fixtures node's tolerance
  table, not from a copy of the execution tests node's.
  ⚠ 2026-09-14: was a declared duplication (`AGENTS.md` §12) of those two numbers,
  now resolved (F-TF-05) → HISTORY.md#invariants-declared-duplication

## Dependencies

- [Problems](../../src/Problems/API.md) — what is being checked.
- [Data](../../src/Data/API.md) — the database.
- [Fixtures](../Fixtures/API.md) — reference cases and the tolerance table.
- [Execution](../../src/Execution/API.md) — the engine options, and a CPU engine for the defect signature on the reference's composition.
- [Thermo](../../src/Thermo/API.md) — `SpeciesTable` for that evaluation, `MixtureState`, `CaseStatus`.
- [Transport](../../src/Transport/API.md) — `TransportTable` for that evaluation, `TransportFigures`.
- [Performance](../../src/Performance/API.md) — `FlowModel`, `PerformanceFigures`.
- [Equilibrium](../../src/Equilibrium/API.md) — `ProblemKind`.
- [Harness](../Harness/API.md) — bit comparison (`Bits.Same`, `Bits.Differences`).

Outside the tree: xunit.

## Constraints

- Part of the default test command; no CUDA.
- Paths from the repository root; the only writes into the working directory are the
  `Bits.actual.txt` of a failed bit comparison, next to the approved file and
  git-ignored, and one `Bits.actual.<sanitized fixture path>.fields.txt` per differing
  fixture, beside it and git-ignored too (`tests/Harness/BOOT.md`, "a field dump is a
  caller's opt-in"). → HISTORY.md#constraints-writes
- One solver and one engine on the CPU accelerator are shared by the collection.

- **No NaN-blind predicate** (2026-09-26, the guards audit of 2026-09-26 (`Audit 5`, the hidden-defect audit's fifth part), F5 and F6). The rules are those of
  the Performance tests node:
  - `StationEquality` fails when a mole fraction is NaN on either side; `Math.Max(p, q)`
    made it skip one.
  - The comparisons of `ReferenceComparison` fail on NaN.
  - The frozen cv skip is keyed on the reference's signature.

## Acceptance criteria

- [x] 2026-09-12 — L0 green: `PropellantTests`
      (`ElementMolesAndEnthalpyEqualTheReferenceFromItsMassFractions`,
      `ARatioSplitReproducesTheReferenceMassFractionsWithinItsSinglePrecision`,
      `CandidateSpeciesEqualTheReferenceProductList`, each over every rocket,
      tp, hp and sp file; `MoleAmountsAreConvertedWithTheRecordMolarMass`,
      `ACustomReactantDerivesItsMolarMassFromTheFormulaAndTheAtomicWeights`,
      `CandidatesAreGasesThenCondensedSpeciesInDatabaseOrder`,
      `AnElementalMixtureNormalizesSymbolsAndKeepsTheOrder`); `RejectionTests`
      (the facts: unknown reactant, temperature out of range, mixture rules, custom
      reactant with an unknown element, the `Only` list, state records, problems
      without the data they need, a disposed solver; 2026-09-13, two more: the mass
      of a composition against one kilogram through every front door, with the grams
      of the message checked against the database's atomic weights and the tolerance
      pinned by a record 0.9 % and one 1.1 % heavy,
      `ACompositionThatDoesNotWeighOneKilogramIsRejectedWithItsMassAndTheTolerance`;
      the propellant path through the committed file's `ADN` record, whose molar
      mass contradicts its formula,
      `AReactantRecordWhoseMolarMassContradictsItsFormulaIsCaughtAtTheSolve`;
      that fact goes when the record is corrected upstream).

  ⚠ 2026-09-14: was "nine facts" over a list of eight, now no count
  → HISTORY.md#criterion-l0-nine-facts
- [x] 2026-09-12 — L1 green for every fixture case, the list generated from the
      directory listing: `RocketTests.TheRocketCaseReproducesTheReferenceEndToEnd`
      over `cases/rocket` (89 files that day),
      `EquilibriumTests.AssignedTemperatureCasesReproduceTheReference`,
      `AssignedEnthalpyCasesReproduceTheReference`,
      `AssignedEntropyCasesReproduceTheReference` over `cases/tp`, `cases/hp`,
      `cases/sp` (106 files).
- [x] 2026-09-12 — L2 green: `RocketTests` (`A_sweep_equals_its_cases_solved_one_by_one`,
      `AnElementalMixtureReproducesItsPropellantBitForBit`,
      `IdenticalProblemsGiveIdenticalResultsAloneAndInOneCall`,
      `ProblemsWithDifferentExitLayoutsAreSolvedInOneCallInOrder`,
      `AFailingStationIsAStatusAndNotAnException`,
      `CompositionsAreReportedByNameOverAllSpecies`; 2026-09-13:
      `Rocket_and_equilibrium_problems_over_several_mixtures_are_one_batch_over_the_union_of_elements`);
      `EquilibriumTests`
      (`StateBatchesOverTheUnionOfElementsReproduceTheReference`,
      `TheDefaultEnthalpyOfAnAssignedEnthalpyProblemIsThePropellants`,
      `TransportFiguresAreAttachedToAnEquilibriumStateWhenRequested`).
- [x] 2026-09-12 — Every check proven non-degenerate once, each mutation applied
      alone and seen red: the oxidizer share of the ratio split perturbed by 1e-6 (the
      ratio-split test and the end-to-end test); a reactant enthalpy per kilogram
      perturbed by 1e-9 (the element-moles-and-enthalpy test); a reference chamber
      temperature altered in a fixture file (the end-to-end test); one species dropped
      from the selection rule (the candidate-species test and the end-to-end test);
      the ion rule reverted to the name's trailing sign (the candidate-species test);
      the temperature margin set to zero (the temperature-range test on `AL(cr)` at
      298.15 K); the defect signature disabled (the end-to-end test at the LOX/LH2
      O/F 4 exits with transport); mole fractions taken over the gaseous phase (the
      end-to-end test on the aluminized propellant).
- [x] 2026-09-13 — The mass check proven non-degenerate: the check removed from the
      solver (its comparison made never true), and
      `RejectionTests.ACompositionThatDoesNotWeighOneKilogramIsRejectedWithItsMassAndTheTolerance`
      and `AReactantRecordWhoseMolarMassContradictsItsFormulaIsCaughtAtTheSolve`
      seen red, together with the Cli tests node's four unit-error documents and its
      line-naming test (that node's BOOT.md).
- [x] 2026-09-13 — The declared tolerance and the mass report (the `Problems`
      BOOT.md's design of the same day): `RejectionTests.TheToleranceAMixtureDeclaresIsTheOneApplied`
      on the record made 2 % heavy (refused at the default, solved at 3 % through
      `Create` and through `StateBatchOptions` for every record of a batch; made 5 %
      heavy, refused at 3 % with the message naming `3 %`; the propellant path at the
      default; an invalid tolerance refused by name); `PropellantTests` on the report
      (`TheRecordedElementMolesOfEveryFixtureWeighOneKilogramWithinTheDerivationFigure`:
      `Solver.MassOf` against `Σ n_i A_i` from `SpeciesDatabase.AtomicWeight` and
      within 1.7e-5 of one kilogram over the directory listing;
      `ResultsCarryTheMassOfTheirMixture`: `MixtureMass` of every result against
      `MassOf`, and a mixture made 0.5 % heavy reporting 1.005). Heavy, not light:
      the record made 1 % to 10 % light does not converge as an hp state at 6.5 MPa
      (the `ALN(L)` record's 2700 K interval boundary, the front door's BOOT.md), and a
      case that fails numerically would not show that the check let it through.
- [x] 2026-09-13 — The declared tolerance and the mass report proven non-degenerate,
      each mutation alone and seen red: the check reading the default instead of the
      mixture's tolerance (`TheToleranceAMixtureDeclaresIsTheOneApplied`, and
      the Cli tests node's option test); the equilibrium results reporting one
      kilogram instead of the measured mass, and the rocket results likewise
      (`ResultsCarryTheMassOfTheirMixture`, each; the first also the Cli tests
      node's option test through the reported mass).
- [x] 2026-09-13 — The melting-plateau states through the front door (the level
      table's plateau row, written 2026-09-14): `SplitRecordTests`
      (`ACutSpeciesReportsOneEntryUnderItsDatabaseName`,
      `AnEnthalpyInsideTheALNGapSolvesThroughTheFrontDoor`,
      `ASweepAcrossTheAluminaPlateauStaysOnTheIsentropeByEitherPath`), the
      evidence the `Problems` node's criterion of that day cites. Their non-degeneracy
      was never recorded; the last criterion below records it.
- [x] 2026-09-14 — Bits level green: `BitSnapshotTests.EveryFixtureGivesTheRecordedBits`
      over the enumerated rocket, tp, hp and sp directories against `Bits.approved.txt`
      (213 fixtures), recorded before any code of the front door's decomposition moved
      and confirmed unchanged after every step of it (`git diff 8f8263c HEAD --
      tests/Problems.Tests/Bits.approved.txt` empty). Seen red once, at `8f8263c`: a
      reactant enthalpy per kilogram perturbed by a relative 1e-9 turned every one of
      the 213 fixtures red, a fixture line removed turned only that fixture red, naming
      it as missing. 2026-09-15 (R-Problems.Tests-1): the reverse direction is closed,
      an approved line no enumerated fixture produces fails the test naming the stale
      key (`ApprovedSnapshot.StaleKeys`); seen red with a fabricated line appended.
      → HISTORY.md#criterion-bits-level
- [x] 2026-09-14 — The contract facts of 2026-09-14 (the level table's second new row):
      `RocketTests.AStateRecordWithExitsEqualsItsCaseThroughTheBatchOverMixtures` (bit
      for bit, transport figures included);
      `RejectionTests.AStateRecordThatBreaksARuleOfItsShapeIsRefusedWithItsIndex` (one
      case per rule: no target, two targets, exits without an enthalpy, a flow without
      exits, a record with exits given to `SolveStates`, one without given to
      `SolveRocketStates`, a negative abundance, a duplicated symbol; each a
      `StateRecordException` whose `Index` is the record's and whose `Reason` names the
      rule); `RocketTests.ABatchMixingTransportAndNoneEqualsEachProblemSolvedAlone` (bit
      for bit, and `TransportStatus` null where none was asked) with
      `CasesAreGroupedByExitLayoutAndTransportFlag` over the runner's internal grouping;
      `RocketTests.ARatioAndPressureProductAsOneBatchEqualsItsCasesSolvedOneByOne`, the
      sweep's fact on the batch over mixtures, replacing
      `A_sweep_equals_its_cases_solved_one_by_one`;
      `RejectionTests.EveryPublicMethodOfADisposedSolverThrows` with
      `TheDisposalFactsCoverEveryPublicMethodOfTheSolver` (the list of methods from
      reflection); `RejectionTests.TheToleranceRuleIsTheOneCreateApplies`
      (`IsValidMassTolerance` false exactly where `Create` refuses). Each seen red once
      and reverted. → HISTORY.md#criterion-contract-facts
- [x] 2026-09-14 — The support code in shape (the review's F-TF-01, F-TF-09, F-TF-10,
      F-TF-11, F-TF-14): `Comparison` becomes `SpeciesList` (`SpeciesList.cs`),
      `ReferenceCaveats` (`ReferenceCaveats.cs`), `ReferenceComparison` (split into
      `TransportMismatches`, `StateAndPerformanceMismatches` and
      `MoleFractionMismatches`, the messages byte for byte as before,
      `ReferenceComparison.cs`) and `StationEquality` (`StationEquality.cs`); the union
      test becomes three facts:
      `RocketTests.RocketProblemsOverSeveralMixturesAreOneBatchOverTheUnionOfElements`,
      `EquilibriumProblemsOverSeveralMixturesAreOneBatchOverTheUnionOfElements` and
      `RejectionTests.ABatchOverMismatchedMixturesOrProblemCountsIsRejected`; every
      tolerance of a comparison with the tree's own code a named constant with its
      origin (`MoleFractionSumTolerance`, `MassBitRoundingTolerance`,
      `ReportedMassPrintTolerance`, `FormulaMassRoundingTolerance`,
      `MassOfSummationTolerance`, `ScaledMassSummationTolerance`,
      `TransitionBoundTolerance`, `OwnCodeIsentropeTolerance`); no method over 60 lines
      or nested deeper than 3. The recorded mutations "the defect signature disabled"
      and "mole fractions taken over the gaseous phase" are red again after the split on
      `TheRocketCaseReproducesTheReferenceEndToEnd`, and each plateau fact of
      `SplitRecordTests` was seen red once (the cut-species collapsing disabled, the
      state record's pressure doubled, `TransitionBoundTolerance` tightened from 0.01
      to 0). → HISTORY.md#criterion-support-code
- [x] 2026-09-18 — The Bits level's per-case field dump (bits-diagnostics task): `Record`
      passes its `BitHash`'s `Fields` to `ApprovedSnapshot.Problem`
      (`tests/Harness/BOOT.md`, "a field dump is a caller's opt-in"), so a fixture that
      disagrees with `Bits.approved.txt` also gets its own
      `Bits.actual.<sanitized path>.fields.txt`, every hashed field as a round-trip
      double or an int/bool, one per line, in the order `HashOf` adds them. Shown red
      once and the dump inspected: the last hex digit of the line of
      `tests/Fixtures/cases/rocket/lox-lh2_of4_pc5MPa_frozenAtThroat.json` changed, the
      test red on that one key and the dump written with the case's 173 fields. No
      approved file moved by this criterion. → HISTORY.md#criterion-field-dump

- [x] 2026-09-27 — The NaN and cv guards: a NaN mole fraction on one side, and a
      frozen exit's cv edited in a fixture copy, each red.
      `StationEquality.RelativeDifferences`'s mole-fraction loop fires its floor test
      also when either side is NaN
      (`double.IsNaN(p) || double.IsNaN(q) || Math.Max(p, q) >= moleFractionFloor`), so
      `Close`'s NaN-safe form is reached; `BitDifferences` was never blind (`Bits.Same`
      compares raw bits). `ReferenceComparison`'s frozen-station cv skip is keyed on the
      reference's defect signature as `Performance.Tests`' `StationComparison` is:
      `StationCaveats` carries `FreezingStationReference`
      (`RocketTests.FreezingStationReferenceOf`, from the case's `FlowModel`), and
      `IsFrozenCvDefectSignature` requires the reference's `cvFrozen`/`cvEquilibrium` at
      the frozen station to be exactly zero or exactly that station's own recorded value
      before skipping; `EquilibriumTests`' two callers pass `Frozen: false`. Evidence,
      each mutation through a temporary fact, seen red and not kept: a pair of mole
      fractions `NaN` against `0.5` is reported; `lox-lh2_of4_pc5MPa_frozenAtThroat` with
      its frozen exit's `cvFrozen`/`cvEquilibrium` edited from `0`/`0` to `2500`/`9999`
      reports a mismatch. `APTHERMO_NO_CUDA=1 dotnet test tests/Problems.Tests --filter
      "Category!=LongRunning"`: 1191 of 1191, none skipped; `Bits.approved.txt`
      unchanged; the protocol lint 0 and 0. → HISTORY.md#criterion-nan-cv-guards

- [x] 2026-09-27 — A fixture's reactant roles come from the fixture (its `role`, the
      fixtures node's document of 2026-09-27), never from a list typed in this node:
      `FixtureCases.Oxidizers` is removed. Every ratio case of the fixtures, the sodium
      case included, builds its propellant from the recorded roles, and the four facts
      that threw on the sodium case pass.

      Evidence: `FixtureCases.Oxidizers` deleted; `PropellantOf` reads each reactant's
      role from the fixture (`RoleOf`, mapping `"oxidizer"`/`"fuel"` to `ReactantRole`,
      throwing by name on anything else) instead of testing the reactant's name against
      the removed set. `dotnet test tests/Problems.Tests --filter "Category!=LongRunning"`:
      1191/1191, none skipped, the four facts that threw on the sodium case
      (`PropellantTests.CandidateSpeciesEqualTheReferenceProductList`,
      `PropellantTests.ARatioSplitReproducesTheReferenceMassFractionsWithinItsSinglePrecision`,
      `EquilibriumTests.AssignedEnthalpyCasesReproduceTheReference`,
      `BitSnapshotTests.EveryFixtureGivesTheRecordedBits`) included. `Bits.approved.txt`
      gains exactly the sodium case's key (`git diff --stat`: 1 insertion, 0 deletions;
      `diff` of the file against `Bits.actual.txt` with that one line excluded: no other
      difference); no key of any other fixture moved.
- [x] 2026-09-28 — The reacting fields' skip is pinned (the second hidden-defect audit
      of 2026-09-28, guards part, observation O1). `ReferenceComparison` skips the
      reacting conductivity and heat capacity wherever the tree's own run eliminates a
      trace species (`TraceEliminations > 0`), and no end-to-end station eliminates one
      today. A fact asserts that list, generated over every rocket fixture with
      transport, equals a pinned list (empty today). A future elimination then turns
      the fact red and is looked at, instead of being skipped silently.

      Evidence: `ReactingFieldsPinningTests.NoEndToEndRocketStationEliminatesATraceSpeciesToday`
      solves every rocket fixture with `transport: true` (`FixtureFiles.Enumerate("rocket")`,
      the fixture's own flag, not a typed list), collects every
      `"fixtureName[stationName]"` whose tree-solved `Station.Transport.TraceEliminations`
      is above zero, and asserts the list equals `[]`. This is independent of
      `FixtureCases.DefectiveStationsOf`, which evaluates the reference's own
      composition (the nine known stations of the Fixtures node's documented defect)
      and stays a separate trigger of the same skip.

      Red-once: a one-line, uncommitted mutation inside the fact's own collection
      method (`eliminating.Add("RED-ONCE-MUTATION[forced]")` appended unconditionally
      before the return) turned the fact red
      (`Assert.Equal() Failure: … Expected: [] Actual: ["RED-ONCE-MUTATION[forced]"]`);
      reverted, the fact and the rest of the node are green again:
      `APTHERMO_NO_CUDA=1 dotnet test tests/Problems.Tests --filter
      "Category!=LongRunning"`: 1206/1206, none skipped (1205 before this fact).
      `Bits.approved.txt` unchanged; the protocol lint: 0 errors, 0 warnings.
- [x] 2026-09-28 — The second hidden-defect audit's F1/F3/F4/F5/O1/O2 fixes (`627f815`)
      moved this node's own `Bits.approved.txt` line for `rp1311-example13`, the same
      case and the same commit as the Performance.Tests node's own record of the
      move, which this entry points to rather than repeats: the oracle's two maxima
      (there is only one; the audit's "no fixture has two maxima" holds for this
      case), the old and new throat (p/p_c, c*), the CEA reference's own throat
      pressure ratio and c* against the fixture tolerance table, and the actual
      mechanism (the new, unconditional `UpstreamChokeCheck.Verify` re-solving the
      throat row across a melting-plateau boundary it crosses between the chamber and
      the momentum search's own candidate, not a second flux maximum).

      Evidence: `RocketTests.TheRocketCaseReproducesTheReferenceEndToEnd` green over
      `rp1311-example13.json` throughout the investigation (no reference field moved
      outside tolerance); `git hash-object tests/Problems.Tests/Bits.approved.txt`
      differs from `main` only in this one case's line, matching the diff recorded at
      `627f815` (`tests/Performance.Tests/BOOT.md`'s 2026-09-28 entry has the field
      values).

## Taboos

- Do not loosen a tolerance for green; do not exclude a failing propellant or station.
- Do not skip a reacting field without proving the reference's defect visible at that station.
- Do not compute `b_i` or `h_0` in the test with the code under test.

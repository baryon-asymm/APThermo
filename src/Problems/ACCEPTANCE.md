# ACCEPTANCE.md — Problems

## Acceptance criteria

- [x] 2026-09-12 — For the RP-1311 examples and the four reference propellants, the
      element moles per kilogram and the reactant enthalpy per kilogram computed here
      equal the reference's (the fixtures record the mass fractions, `elementMoles`
      and `reactantEnthalpy`) within 1e-10 relative:
      `PropellantTests.ElementMolesAndEnthalpyEqualTheReferenceFromItsMassFractions`
      over every rocket, tp, hp and sp file (the list from the directory listing, 195
      that day), the propellant given by the mass fractions the reference recorded.
      The ratio path (`ARatioSplitReproducesTheReferenceMassFractionsWithinItsSinglePrecision`)
      holds at 1e-7: the reference rounds the ratio to single precision before
      splitting the kilogram (Fixtures BOOT.md), so its own mass fractions carry that
      rounding; mole amounts: `MoleAmountsAreConvertedWithTheRecordMolarMass`.
- [x] 2026-09-12 — The candidate species list for each fixture case equals the
      reference's product list under the same `Omit` list, or the `Only` list the
      reference was given (RP-1311 examples 1 and 12), compared as sets and by count:
      `PropellantTests.CandidateSpeciesEqualTheReferenceProductList` over the
      same files; the order rule: `CandidatesAreGasesThenCondensedSpeciesInDatabaseOrder`.
- [x] 2026-09-12 — A custom reactant (the AP/binder case's binder) produces the
      reference `b_i` and `h_0`: the AP/HTPB/Al files of the first criterion, and
      `ACustomReactantDerivesItsMolarMassFromTheFormulaAndTheAtomicWeights`.
- [x] 2026-09-12 — An `ElementalMixture` built from the `b_i` and `h_0` of a fixture
      propellant gives the same rocket and equilibrium results as the propellant itself,
      bit for bit on the same accelerator
      (`RocketTests.AnElementalMixtureReproducesItsPropellantBitForBit`); a
      state batch of the fixture stations reproduces the fixtures within the tolerance
      table, including records where an element of the batch is absent
      (`EquilibriumTests.StateBatchesOverTheUnionOfElementsReproduceTheReference`;
      2026-09-13 for the batch over several mixtures:
      `RocketTests.Rocket_and_equilibrium_problems_over_several_mixtures_are_one_batch_over_the_union_of_elements`).
- [x] 2026-09-12 — End-to-end: every rocket fixture (the four reference propellants in
      shifting and frozen flow, with and without transport, and the RP-1311 rocket
      examples) and every tp, hp and sp fixture through this node match the fixtures
      within the tolerance table: `RocketTests.TheRocketCaseReproducesTheReferenceEndToEnd`
      and the three `EquilibriumTests` theories of the front door tests node, over the
      directory listings.
- [x] 2026-09-12 — A reactant temperature outside its record's range, an unknown
      reactant, a mixture with a zero-mass group, or an element without an atomic
      weight are rejected with the reactant's name in the exception, before any kernel
      runs: `RejectionTests` (`AnUnknownReactantIsRejectedByName`,
      `ATemperatureOutsideTheRecordRangeIsRejectedByName`,
      `MixtureRulesThatLeaveAGroupEmptyOrAmbiguousAreRejected`,
      `ACustomReactantWithAnUnknownElementIsRejectedByName`,
      `AnOnlyListBeyondTheElementsIsRejectedAndAValidOneIsUsedAsGiven`,
      `InvalidStateRecordsAreRejectedByIndexOrElement`,
      `ProblemsWithoutTheDataTheyNeedAreRejected`, `ADisposedSolverRefusesWork`).
- [x] 2026-09-12 — Two identical batches produce identical results (statuses and
      numbers): `RocketTests.IdenticalProblemsGiveIdenticalResultsAloneAndInOneCall`,
      `A_sweep_equals_its_cases_solved_one_by_one`,
      `ProblemsWithDifferentExitLayoutsAreSolvedInOneCallInOrder`.
- [x] 2026-09-13 — A composition that does not weigh one kilogram is refused with
      its mass and the tolerance, and one that does is solved: the record of the
      invariant above passes as a state record; doubled, in mol/g or kmol/kg and in
      mmol/kg it is refused through `SolveStates` (`state record 0`), through
      `Solve(ElementalMixture, …)` for a rocket and for an equilibrium problem and
      through the batch over mixtures (`mixture 1`); the grams in the message equal
      `Σ n_i A_i` with the database's atomic weights; a record 0.9 % heavy solves and
      one 1.1 % heavy is refused
      (`RejectionTests.ACompositionThatDoesNotWeighOneKilogramIsRejectedWithItsMassAndTheTolerance`);
      the propellant path is covered by the committed file's `ADN` record
      (`AReactantRecordWhoseMolarMassContradictsItsFormulaIsCaughtAtTheSolve`).
      Every fixture keeps passing through the end-to-end theories, unchanged (their
      element moles were measured within 1.65e-5 of one kilogram, see the invariant).
- [x] 2026-09-13 — The tolerance a mixture declares is the one the check applies,
      through every front door: the record of the invariant made 2 % heavy is refused
      at the default and solved at 3 %, through `StateBatchOptions.MassTolerance` for
      every record of a batch and through `Create` for the direct overloads; made 5 %
      heavy it is refused at 3 % with the message naming `3 %`; the propellant path
      and a mixture naming no tolerance declare the default; a negative, NaN or
      infinite tolerance is refused by `Create` naming `massTolerance`
      (`RejectionTests.TheToleranceAMixtureDeclaresIsTheOneApplied`). Heavy,
      not light, for the reason recorded under the invariant.
- [x] 2026-09-13 — The mass is reported: `Solver.MassOf` equals `Σ n_i A_i` from
      `SpeciesDatabase.AtomicWeight`, and over every fixture file (the directory
      listing, 195 files) the recorded element moles lie within 1.7e-5 of one
      kilogram, the figure the derivation above rests on
      (`PropellantTests.TheRecordedElementMolesOfEveryFixtureWeighOneKilogramWithinTheDerivationFigure`);
      every result's `MixtureMass` equals `MassOf` of its mixture on both front doors
      and through `SolveStates`, and a mixture made 0.5 % heavy reports 1.005, not one
      (`PropellantTests.ResultsCarryTheMassOfTheirMixture`).
- [x] 2026-09-13 — A cut condensed record is one name in every result: for a mixture
      holding `ALN(L)` the stations' mole fractions and condensed mass fractions
      carry `ALN(L)` once with the sum of its pieces and `Species` lists it once
      (`SplitRecordTests.ACutSpeciesReportsOneEntryUnderItsDatabaseName`);
      an hp state whose assigned enthalpy lies inside the record's 2700 K gap (the
      mass-tolerance invariant's record, made light) converges to the pinned pair at
      the crossing instead of `NotConverged`
      (`SplitRecordTests.AnEnthalpyInsideTheALNGapSolvesThroughTheFrontDoor`);
      and the sweep across the alumina plateau stays on the isentrope by either path
      (`SplitRecordTests.ASweepAcrossTheAluminaPlateauStaysOnTheIsentropeByEitherPath`).
- [x] 2026-09-14 — The decomposition of `## Structure` (2026-09-14): every type within
      the root's code-shape constraint (`Solver` and the two runners the declared
      composition roots, their Ce in `## Shape exceptions`); the tests node's front-door
      bit snapshot unchanged, recorded before any code moved; every fixture theory green
      unchanged; the surface moved only by the members `API.md` plans under 2026-09-14,
      in one contract commit after the internal moves, with `PublicSurface.approved.txt`
      moved in it; the command line's call sites adapted to the renames and to
      `CustomReactantDefinition`, nothing else of it touched. Shape measured on the
      build of `a3b7d05`, merged as `765f4e2`, the fast suite green (3007 tests,
      `Problems.Tests` 1111). → HISTORY.md#criterion-decomposition-2026-09-14
- [x] 2026-09-14 — The state record with exits: a rocket record through
      `SolveRocketStates` equals the same mixture and problem through
      `Solve(mixtures, problems)` bit for bit, transport included
      (`RocketTests.AStateRecordWithExitsEqualsItsCaseThroughTheBatchOverMixtures`);
      `SolveStates` refuses a record with exits and `SolveRocketStates` one without; a
      record with two targets, with exits and no enthalpy, or with a flow and no exits
      is refused; each refusal a `StateRecordException` whose `Index` is the record's
      and whose `Reason` names the rule
      (`RejectionTests.AStateRecordThatBreaksARuleOfItsShapeIsRefusedWithItsIndex`,
      the `ShapeViolations` theory data, eight rows — correcting this line's citation
      of `EquilibriumTests`, which carries no fact of this criterion).
- [x] 2026-09-14 — The narrowed transport pass and the retired sweep: a batch of two
      rocket problems with transport on one of them gives, for each, the result of
      that problem solved alone bit for bit, transport figures included, and the
      other's `TransportStatus` null
      (`RocketTests.ABatchMixingTransportAndNoneEqualsEachProblemSolvedAlone`,
      with `CasesAreGroupedByExitLayoutAndTransportFlag` over a four-case
      interleaved batch for the grouping itself); a ratio and pressure product given as
      a list of mixtures and problems equals its cases solved one by one bit for bit,
      the fact that replaced the sweep
      (`RocketTests.ARatioAndPressureProductAsOneBatchEqualsItsCasesSolvedOneByOne`);
      every public method of a disposed solver throws, checked against the list of
      methods reflection gives so a new overload cannot be missed
      (`RejectionTests.EveryPublicMethodOfADisposedSolverThrows` with
      `TheDisposalFactsCoverEveryPublicMethodOfTheSolver`).
- [x] 2026-09-15 — Every ticked criterion above re-verified on the decomposed and
      repaired code at `62cd99e`: its tests green in the full suite
      (`APTHERMO_NO_CUDA=1`, every category, 3037 tests, none skipped), and
      CUDA-category evidence on the reference machine (`tests/Execution.Tests`, 41,
      and the long-running sweep and throughput tests).
- [x] 2026-09-15 — Packing (`## Structure`, Packing): `dotnet pack
      APThermo.sln -c Release -o <feed>` from a clean build produces exactly
      `APThermo.0.1.0.nupkg`/`.snupkg` and `APThermo.Cli.0.1.0.nupkg`/`.snupkg`, no
      warning; the nupkg holds the seven merged assemblies in `lib/net10.0`, `README.md`,
      `NOTICE`, and a nuspec dependency list of exactly `ILGPU`; the snupkg the matching
      seven `.pdb`. `dotnet tool install APThermo.Cli --tool-path <dir> --add-source
      <feed> --version 0.1.0` then `apthermo --version` prints `0.1.0`, and running an
      approved Cli-node example (`rocket documents/rocket-lox-lh2.json --format csv
      --accelerator cpu`, no `--database`) from an empty directory reproduces
      `tests/Cli.Tests/documents/rocket-lox-lh2.approved.csv` byte for byte (the CSV
      form carries no database path field to differ). A throwaway console project
      referencing `APThermo` 0.1.0 from the feed solves the LOX/LH2 rocket case through
      `Solver` on the CPU accelerator and prints the chamber temperature the library
      gives directly (3485.023295679567 K). Verified by hand (packing is not part of
      `dotnet test`), not by a committed test.
      → HISTORY.md#criterion-packing-2026-09-15

- [x] 2026-09-26 — The audit fixes of that date (Constraints), each fact red once
      against the code of `9c33398` and green after (ticked 2026-09-27), in
      `tests/Problems.Tests/AuditFixTests.cs`:
      - `ARuleProblemValidationAppliesIsRefusedByTheRecordsOwnIndexNotABatchLocalOne`:
        a batch of three records with the third's pressure 0 is refused with `Index` 2;
      - `AnElementThatSurvivesOnlyInIonizedOrInertRecordsIsRefusedByName` and
        `AnElementWithNoMonatomicRecordIsRefusedNamingWhatIsMissing`: `InertH2(L)` and a
        record with `E` refused naming the element, `InertRP-1` naming `IC` and the
        missing monatomic record;
      - `MixedAmountKindsInOneRoleGroupAreRejected`: a fuel group of one mass-fraction
        and one mole reactant is refused;
      - `AMultiRecordProductNamesRangeIsTheUnionOfItsRecordsAndItsEnthalpyEqualsTheJoinedTable`:
        `Fe2O3(cr)` accepted at 1000 K, its enthalpy equal to the joined table species'
        (through `SolverFixture.Shared.Engine`, not a typed value);
      - `SeveralReactantOnlyRecordsOfOneNameResolveToTheLast`: `n-Butanol`'s enthalpy
        per kilogram equal to the database record's own field;
      - a failed station with transport requested has a null `TransportStatus`, which
        `RocketTests.AFailingStationIsAStatusAndNotAnException` already pinned.
      `dotnet test tests/Problems.Tests`: 1117 of 1117, none skipped; the public surface
      and every bit snapshot unmoved. → HISTORY.md#criterion-audit-0926

      ⚠ 2026-09-28: was three of these facts trying only one side of their rule, now the
      second audit's facts below → HISTORY.md#criterion-audit-0926-one-sided
- [x] 2026-09-28 — The audit fixes of 2026-09-28 (Constraints). Each fact is red once
      against `5a732f0`, then green, in `tests/Problems.Tests/SecondAuditFixTests.cs`:
      - `AnElementAtZeroAbundanceEverywhereIsMaskedNotRefused`: `"E": 0.0` beside the
        approved AP/Al record solves bit for bit as without `E`, `"e": 1e-6` is refused;
      - `AnOnlyListNamesItsOwnCauseAndExcludesIonsAndInertRecords`:
        `"only": ["H2", "H"]` on LOX/LH2 refused naming the list, `e-`, `H+` and
        `InertH` refused by name;
      - `OneAmountKindPerUnitOfNormalizationAppliesAcrossGroupsOnlyWithoutARatio`:
        refused across groups without a ratio, unchanged with one;
      - `ARecordIsNamedForItsOwnElementsABatchForItsOptions`: each bad record of a batch
        a `StateRecordException` with its own index, a transport request against a
        database without `trans.inp` and a NaN `StateBatchOptions.MassTolerance` an
        `ArgumentException` naming the option;
      - `EstimatesMustBeFinite` (+∞ refused), `AmountsMustSumToAFiniteValue` (two fuels
        of 1e308 refused naming the group);
      - `Br2ResolvesAt298Point15KAgainstTheGeneratedReference`: enthalpy equal to cea
        3.3.4's (`tests/Fixtures/cases/reactant/Br2_cr__298.15K.json`, never typed);
      - `TwoOmitListsThatJoinToTheSameTextGiveTwoTables`.
      `API.md`'s errors table states every refusal above. `dotnet test
      tests/Problems.Tests/APThermo.Problems.Tests.csproj`: 1206 of 1206, none skipped;
      no `Bits*.approved.txt` or `PublicSurface.approved.txt` moved.
      → HISTORY.md#criterion-audit-0928
- [x] 2026-09-28 — The third audit pass of 2026-09-28 (part 2, finding 3) is closed: the
      refusal of an element with no candidate species does not depend on what the solver
      solved before. `ChemicalSystemCache.Get(IReadOnlyList<string>, …)` calls
      `ValidatedCandidates` before the cache's `_systems.TryGetValue` lookup, so every
      call validates its own mixture's abundances; `Union` and the single-mixture
      `Solve` overloads go through the same `Get`. Fact:
      `ThirdPassFixTests.TheNoCandidateRefusalDoesNotDependOnTheCachesHistory`, red at
      `c02e14d`, green after. `dotnet test tests/Problems.Tests`: 1252 of 1252; the
      protocol lint 0 and 0; no `Bits*.approved.txt` or `PublicSurface.approved.txt`
      moved. → HISTORY.md#criterion-third-pass-0928

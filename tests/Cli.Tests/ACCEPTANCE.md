# ACCEPTANCE.md — Cli.Tests

- [ ] Input-schema/reader controls independently require the documented sweep axes and
  custom-reactant fields. An owned process starts the real entry point with a .NET
  output writer using code page 866; it emits UTF-8 without a BOM and preserves a
  non-ASCII input path. Separate physical Windows-console evidence is recorded.
  Existing output and numerical snapshots remain unchanged.

  ⚠ 2026-11-17: was "Input-schema/reader controls independently require the documented
  sweep axes and custom-reactant fields. An actual redirected process under a Windows
  OEM code page emits UTF-8 without a BOM and preserves a non-ASCII input path. Existing
  output and numerical snapshots remain unchanged." The original Windows test process
  escaped the owned runner, so the automated control now uses the owned writer probe;
  the separate physical console check retains the original operating-system evidence.
- [ ] The reported O/F 1, 10 kPa, pressure-ratio 1.0000000000001 rocket input is run
  through a real process. An `ok` exit has finite area and vacuum impulse; a numerical
  refusal reports a failed station and exit 1. The report's baseline output is retained.

- [x] 2026-11-16 — `FailedStationOutputTests.InventoryRejectsNewUnsupportedMembers`
  detects omitted nonnumeric properties and fields; both omission mutants are red.

- [x] 2026-11-16 — `FailedStationOutputTests.FrozenExitFailureReportsZeroStateAndPerformance`
  passes through the real in-process command. It checks the generated numeric-property
  inventory in JSON and retains earlier successful stations.

- [x] 2026-11-16 — `StateSchemaGuardTests` checks independent state-shape
  expectations against the published schema and real `states` command, including
  empty arrays, nonempty exits, multiple targets and flow. Before repair eight
  invalid rows failed the schema assertion while seven valid rows passed.

- [x] 2026-11-16 — `JsonSchemaTypeAuditTests` passes 4/4; the two unvisited
  unknown-type controls were red against the pre-fix parser.

- [x] 2026-11-14 — `BitSnapshotTests` preserves JSON and CSV approved digests on both paths;
  separate actual JSON and CSV value mutations produce named raw-document dumps.

- [x] 2026-11-14 — `SchemaShapeParityTests` proves both positive and negative TP/HP/SP shapes,
  zero estimates, and the emitted zero-mass diagnostic with negative output controls.

- [x] 2026-11-14 — `SweepBoundaryTests` admits exactly the inclusive axis bound for lists and ranges,
  refuses one more before allocating an expanded numeric array, and preserves the
  exact overall case bound and its first refused product.

  ⚠ 2026-11-14: the first A25 boundary evidence covered integer/list endpoints only.
  The independent public/CLI review at effc89f2 found the missing decimal maximum:
  raw steps999999.0000000001 must round to999999 under the existing StepTolerance.
  `ADecimalRangeAtTheRoundedAxisLimitIsAccepted` was red before the follow-up; the
  rounded-count guard retains both exact-integer and decimal oversized refusals.

- [x] 2026-09-13 — L0 green: `CommandLineTests` (`NoCommandIsExit2WithTheUsage`,
      `HelpPrintsTheUsageAndExits0`, `InvalidCommandLinesAreExit2NamingTheOffender`
      over the command lines of its theory, `TheUsageNamesEveryCommandAndOption`,
      `OptionsMayBeGivenWithAnEqualsSign`); `InputDocumentTests`
      (`AnInvalidDocumentIsExit2WithTheDocumentedMessageAndNoOutput` over
      `documents/invalid/` (2026-09-13: four more documents there, a state record
      doubled, in mol/g, one in kmol/kg and a doubled `propellant.elementMoles`, each
      refused naming the record and the mass), `EveryExampleDocumentValidatesAgainstTheInputSchema`,
      `EveryStatesDocumentValidatesAgainstTheStatesSchema`,
      `EveryExampleOfTheApiDocumentIsReadOrValidatesAndItsRecordsSolve`
      (2026-09-13: the record examples are solved, not only read, since one of them
      weighed 706 g), `StateRecordsMayComeAsAnArrayASingleObjectOrLines`,
      `ARangeExpandsInclusivelyAndLandsOnItsEnd`); `ExitCodeTests` (six facts;
      2026-09-13, a seventh: the record of another simulation is exit 0, and the same
      record doubled behind it in a JSON Lines file is named by file and line,
      `ARecordThatWeighsOneKilogramIsExit0AndOneThatDoesNotIsNamedByItsLine`).

  ⚠ 2026-09-14: stood "over sixteen command lines"; the theory had nineteen rows since
  the three of `--mass-tolerance` (the review's F-TF-16). A number repeating the length
  of a list, dropped: the theory's rows are the list.
- [x] 2026-09-13 — L1 green: `OutputDocumentTests`
      (`EveryExampleDocumentRunsAndItsResultValidatesAgainstTheOutputSchema`
      over the directory listing, `TheStatesResultValidatesAndEchoesEveryRecordInOrder`,
      `StatesFromAnArrayALinesFileAndSeveralFilesGiveTheSameCases`,
      `ARocketSweepExpandsRatioMajorThenPressureInOneDocument`,
      `AnEquilibriumSweepExpandsPressureThenTemperature`,
      `TheThresholdOmitsSmallMoleFractionsAndTheDefaultIsTheReferencePrintThreshold`,
      `TransportFiguresArePresentOnlyWhenRequested`,
      `TheSpeciesListingValidatesAndFindsNamesCaseInsensitively`,
      `TheDevicesListingValidates`, `TheSchemaListsEveryFieldOfTheLibraryResultStructs`);
      `CsvTests` (`TheCsvOfTheRocketExampleMatchesTheApprovedFile`,
      `TheCsvHasOneRowPerCaseAndStationAndNoCompositions`,
      `AnEquilibriumCsvHasOneRowPerCaseWithEmptyPerformanceAndTransportCells`).
- [x] 2026-09-13 — L2 green: `LibraryEqualityTests.TheRocketExampleEqualsTheLibraryFieldByField`,
      `TheEquilibriumExamplesEqualTheLibraryFieldByField`; process level:
      `ProcessTests` (exit codes 0, 1, 2 and 3, the last with `APTHERMO_NO_CUDA=1` and
      `--accelerator cuda`).
- [x] 2026-09-13 — Every check proven non-degenerate once, each mutation applied
      alone and seen red: a field renamed in the output schema (the schema tests and
      the field-list test); the exit code of an invalid document swapped for 3 (the
      exit code tests); the chamber pressure of the LOX/LH2 document changed to bar
      (the library equality and the approved CSV); g0 changed (the library equality);
      a CSV column dropped (the approved CSV); the threshold no longer applied (the
      threshold test and the library equality); the sweep's ratio order reversed (the
      sweep order test).
- [x] 2026-09-13 — The mass check and its naming proven non-degenerate, each mutation
      alone: the front door's check removed (its comparison made never true): the
      invalid-document theory over the four unit-error documents and the line-naming
      test red; the record's source no longer substituted for the library's subject
      in `states`: the same theory over the three state-record documents (`record 0:`
      absent from the messages) and the line-naming test red.
- [x] 2026-09-13 — `--mass-tolerance` and the mass report (the `Cli` BOOT.md's design
      of the same day): `CommandLineTests` (the usage names the option; a negative
      value, `inf` and the option on a listing command are exit 2; `=` and the
      default); `OutputDocumentTests.TheMassToleranceIsEchoedAndEveryCaseReportsTheMassOfItsMixture`
      (`run.massTolerance` echoes the option and its default, `mixture.mass` in every
      case of the states and sweep documents; both fields in `schemas/output.schema.json`
      and `run.massTolerance` in `species.schema.json`, since the listing writes the
      same run section); `LibraryEqualityTests` (`mixture.mass` exactly the library's
      `MixtureMass`); `ExitCodeTests.TheMassToleranceOptionIsTheToleranceTheRunDeclares`
      (the record of another simulation made 2 % heavy: exit 2 without the option,
      exit 0 with `--mass-tolerance 0.03` and the mass 1.02 reported; made 5 % heavy,
      exit 2 naming `3 %`).
- [x] 2026-09-13 — Proven non-degenerate: the option not passed to the solver (the
      state records built at the library's default), applied alone, and
      `ExitCodeTests.TheMassToleranceOptionIsTheToleranceTheRunDeclares` seen
      red (the echo test stays green, as it should: the echo is not the check); the
      same test also red under the front door's own mutations (that node's BOOT.md).
- [x] 2026-09-14 — Bits level green: `BitSnapshotTests.EveryExampleGivesTheRecordedOutput`
      over the examples the level table lists (enumerated from `documents/` and from
      the `Cli` API, not typed), against `Bits.approved.txt`, recorded before any code
      of the decomposition moved (`f795f3c`) and unchanged after it (empty diff at the
      close); seen red once by a CSV column moved (that example's CSV line red) and by
      an example absent from the snapshot (red with the instruction to approve), both
      at the recording commit.
- [x] 2026-09-15 — The Bits level's JSON half hashes the delivered bytes with `run` cut
      by span, not the review's compact re-serialization (R-Cli.Tests-2): the bytes
      hashed are the bytes captured in process, proven equal to what `--output` writes
      to a file for the LOX/LH2 rocket example
      (`BitSnapshotTests.TheCapturedTextMatchesTheBytesDeliveredToTheOutputFile`,
      cutting `run` from both before comparing, since `run.timings` alone differs run to
      run); the cut is exact with `run` first, in the middle and last, each against the
      same document written without it
      (`RunPropertyCutTests.TheTopLevelRunPropertyIsCutWhereverItAppears`),
      seen red once with the cut's span shifted by one byte
      (`RunPropertyCut.Cut`, `document.AsSpan((int)cutEnd + 1)`): all three cases failed
      on `Assert.Equal() Failure: Strings differ`, each missing exactly the one byte
      immediately after the removed span, reverted; a document with no top-level `run`
      property or with more than one fails instead of hashing
      (`RunPropertyCutTests.AMissingTopLevelRunPropertyFailsInsteadOfHashing`,
      `ADuplicatedTopLevelRunPropertyFailsInsteadOfHashing`). Every line of
      `Bits.approved.txt` re-approved once, with two proofs, both over all 18 examples:
      the same run computed the old, compact-reserialization hash and the new one for
      every example from the same captured text, and every old hash still equalled its
      approved line (the values, keys and order were still the approved ones at the
      moment of re-approval; a temporary fact, never committed); the JSON document of
      every example produced at `f795f3c`, before the clean-code decomposition, in a
      scratch worktree of that commit removed afterward, hashes under the new
      definition to the same 18 new lines, no example present on only one side (a
      temporary fact, never committed). The CSV half of every line is unchanged in both
      proofs, since its hash definition did not change. `git hash-object
      tests/Cli.Tests/Bits.approved.txt`: `483c979b75b5c98b5e11ddc4359f28812e225e15`
      (18 lines) before, `b6e7844aa27ee16b9e2c1cbb84396d043e44386d` (18 lines) after.
- [x] 2026-09-14 — The facts of 2026-09-14 (the level table's new L0 and L2 rows):
      `LibraryEqualityTests.TheStatesExampleEqualsTheLibraryFieldByField` (a
      records file with and without exits); `ExitCodeTests.AnExceptionMapsToItsDocumentedExitCode`
      (an input refusal 2, an accelerator failure 3, an unexpected exception 3);
      `CommandLineTests.TheUsageStatesTheLibraryDefaults` and
      `EveryCommandOfTheTableHasAHandler`;
      `OutputDocumentTests.AnAutoRunThatFellBackSaysWhy` (with
      `APTHERMO_NO_CUDA=1`: `run.accelerator.cudaSkippedBecause` and the devices
      listing name the variable, the schema files list the field); the pinned messages
      of `states-two-targets.json` and `states-rocket-without-enthalpy.json` the front
      door's reasons behind the record's source. Each of the five facts seen red once
      and reverted: `StatesCommand.SolveRockets` writing a with-exits case to the
      batch's first slot instead of its own index (the states example's rocket case
      overwrites its equilibrium one); an unexpected exception mapped to 2; the
      threshold's usage line typed as a literal instead of read from
      `CommandOptions.DefaultThreshold`; `states` dropped from `CommandRegistry`'s
      table (`unknown command 'states'`, exactly the drift the test guards against);
      the fallback reason not written.
- [x] 2026-09-14 — The support code in shape (the review's F-TF-03, F-TF-07, F-TF-10,
      F-TF-12, F-TF-13): `JsonSchema.Check` split into `CheckKeywords`, `CheckType`,
      `CheckValue`, `CheckNumber`, `CheckObject` (with `CheckProperty`), `CheckArray`
      and `CheckCombinators`, the keyword whitelist unchanged;
      `LibraryEqualityTests.AssertCase` split into `AssertMixture`, `AssertStation` and
      `AssertComposition`; the schema-declaration `AssertFields<T>` of
      `OutputDocumentTests` renamed `AssertSchemaListsFields<T>` so only one method of
      the node keeps that name, both it and `LibraryEqualityTests` reading one
      camel-case rule of this node (`CliFixture.Camel`); the grams of the four refusal
      messages that report one derived from the document's own composition
      (`CliFixture.GramsOf`), compared numerically rather than as text since the message
      itself rounds the figure it reports (`InputDocumentTests.AssertMassReported`); the
      tolerance literals of `OutputDocumentTests`, `ExitCodeTests` and
      `InputDocumentTests` named constants with their origin in a comment; no method
      over 60 lines or nested deeper than 3 (the protocol tests node's `ShapeMeasures`
      over the tree with this pass merged). The
      recorded mutations still red, each proven again and reverted: a field renamed in
      the output schema (both the schema validator and the reflection-based field-list
      test red) and g0 changed (both rocket-example tests red); and, for the camel-case
      rule, `Names.Camel` returning its argument unchanged (the three
      `LibraryEqualityTests` facts red).
- [x] 2026-09-17 — `apthermo schema` (the audit's C1, C2, C3, C4, C5): `SchemaCommandTests`
      (`StandardOutputIsExactlyTheFileBytes`, `TheOutputOptionWritesExactlyTheFileBytes`
      over a directory listing of `src/Cli/Schemas/`, `TheEmbeddedNamesEqualTheDirectoryListing`,
      `AMissingNameIsExit2ListingEveryEmbeddedName`,
      `AnUnknownNameIsExit2ListingEveryEmbeddedName`). Each fact seen red once and
      reverted: the assembly's glob narrowed to `devices.schema.json` alone turned nine of the
      thirteen facts red at once (both byte-equality theories over the four other names, and
      the embedded-names-equal-the-listing fact, since `SchemaResources.Names` then read from
      the manifest a set the directory listing no longer matched); a name filtered out of
      `SchemaResources.Names` by hand turned the embedded-names fact red on its own,
      `["devices","output","species","states"]` against the directory's five; the missing-name
      and the unknown-name branches of `SchemaCommand` each written to `output.WriteLine` and an
      ordinary return instead of `throw new InputException(...)` turned their own fact red,
      `Assert.Empty()` finding the message on standard output instead of standard error.

- [x] 2026-10-03 — The test pyramid (root `BOOT.md`, Test time budgets): the node's
      in-process facts run on warm solvers and its process facts are the end-to-end set.
      - Warm solvers: `SolverCacheTests` (one solver for invocations on one database
        content, a database of another content gets its own, a warm invocation gives the
        bytes of a fresh one with `run` cut, disposing releases the solvers). Red once:
        the cache key replaced by a constant, `ADifferentDatabaseContentGetsItsOwnSolver`
        red (expected 2, actual 1), reverted. The accelerator half of the key has no fact
        here: a second accelerator is an `auto` run that binds CUDA where a device exists,
        and this node is CPU only.
      - Tagging: the eight facts of `ProcessTests` and `AnAutoRunThatFellBackSaysWhy` carry
        `Category=EndToEnd`; `Protocol.Tests.EndToEndTests` holds it for every process fact.
        Red once: the trait removed from `AnAutoRunThatFellBackSaysWhy`, the check named it.
      - Process bits: `EveryExampleGivesTheRecordedOutputThroughTheProcess` is green against
        the Windows `Bits.approved.txt`, unmoved (`git status`: no `Bits*.approved.txt`
        touched), so the warm in-process output, the cold process output and the record are
        the same bytes on this platform. Red once, a process-only change: `Program.Main`
        writing one byte to standard output before running turned that fact and the two
        process facts of `Docs.Tests` red while the in-process bit fact stayed green; the
        runner's `Output` and `Error` swapped turned all ten end-to-end facts red; both
        reverted.
      - Evidence, Windows, `APTHERMO_NO_CUDA=1`, a machine other agents also used: this node's
        137 facts of the fast set in 46 s inside the whole solution's fast run, against 155 s
        before (the orchestrator's measurement, `HISTORY.md#test-budgets` of the root); its
        10 end-to-end facts in 46 s.

# ACCEPTANCE.md — Cli

## Acceptance criteria

- [x] 2026-09-13 — Every example document in `API.md` and every document of the
      tests node's `documents/` directory runs end to end against the committed data
      files and produces a document that validates against the output schema (schema
      files kept next to the tests): `Cli.Tests.InputDocumentTests.EveryExampleOfTheApiDocumentIsReadOrValidatesAndItsRecordsSolve`
      (2026-09-13: the record examples of `API.md` are solved too, since the earlier
      example weighed 706 g),
      `OutputDocumentTests.EveryExampleDocumentRunsAndItsResultValidatesAgainstTheOutputSchema`
      (over the directory listing), `TheStatesResultValidatesAndEchoesEveryRecordInOrder`,
      `TheSpeciesListingValidatesAndFindsNamesCaseInsensitively`, `TheDevicesListingValidates`.
- [x] 2026-09-13 — The example rocket document for LOX/LH2 gives the same numbers as
      the library call in the front door tests: the CLI test builds the library call
      from the fixture the document encodes and compares the output document field by
      field over a reflection-generated list, exactly
      (`LibraryEqualityTests.TheRocketExampleEqualsTheLibraryFieldByField`,
      `TheEquilibriumExamplesEqualTheLibraryFieldByField`).
- [x] 2026-09-13 — Exit codes 0, 1, 2, 3 are each produced by a test, in-process and
      as a process: a good document, a document with a failing case, a malformed
      document, `--accelerator cuda` with `APTHERMO_NO_CUDA=1`
      (`ExitCodeTests`, `InputDocumentTests.AnInvalidDocumentIsExit2WithTheDocumentedMessageAndNoOutput`,
      `ProcessTests`: the four facts).
- [x] 2026-09-13 — CSV output has one row per case and station and the documented
      columns, checked against the approved file `documents/rocket-lox-lh2.approved.csv`
      of the tests node (`CsvTests`: the header exactly, every number as a number).
- [x] 2026-09-13 — A record that weighs one kilogram (the record of another
      simulation, `documents/states-ap-al-record.json`) is exit code 0; the same
      record doubled, in mol/g, a record in kmol/kg and a document with a doubled
      `propellant.elementMoles` are exit code 2 with the documented message naming
      the record and the mass, and no document
      (`InputDocumentTests.AnInvalidDocumentIsExit2WithTheDocumentedMessageAndNoOutput`
      over `documents/invalid/states-two-kilograms.json`, `states-mol-per-gram.json`,
      `states-kmol-per-kg.json`, `elemental-two-kilograms.json`); a doubled record
      behind a good one in a JSON Lines file is named by file and line, not by its
      position in the batch
      (`ExitCodeTests.ARecordThatWeighsOneKilogramIsExit0AndOneThatDoesNotIsNamedByItsLine`).
- [x] 2026-09-13 — `--mass-tolerance` is parsed like `--threshold`: a negative or
      infinite value and the option on a listing command are exit code 2, the usage
      names it, `=` works and the default is the library's
      (`CommandLineTests.InvalidCommandLinesAreExit2NamingTheOffender`,
      `TheUsageNamesEveryCommandAndOption`, `OptionsMayBeGivenWithAnEqualsSign`);
      it is echoed as `run.massTolerance` and every case of every solving command
      carries `mixture.mass`, the schema files of the tests node listing both
      (`OutputDocumentTests.TheMassToleranceIsEchoedAndEveryCaseReportsTheMassOfItsMixture`,
      `EveryExampleDocumentRunsAndItsResultValidatesAgainstTheOutputSchema`),
      and the mass is the library's exactly (`LibraryEqualityTests`); the record of
      another simulation made 2 % heavy is exit code 2 without the option and exit
      code 0 with `--mass-tolerance 0.03`, made 5 % heavy exit code 2 naming `3 %`
      (`ExitCodeTests.TheMassToleranceOptionIsTheToleranceTheRunDeclares`;
      heavy, not light: the front door's BOOT.md records why).
- [x] 2026-09-14 — The decomposition of `## Structure`: every type within the root's
      code-shape constraint except the rows of `## Shape exceptions`, measured by the
      protocol tests node's measurements (`ShapeMeasures`, `CouplingMeasures`) over the
      tree with this decomposition merged, 66 types and 134 methods of the node: no type
      over 400 lines, no method over 60, no nesting deeper than 3, no method over 6
      parameters; the tests node's snapshot of the example outputs unchanged from before
      any code moved (`tests/Cli.Tests/Bits.approved.txt`, empty diff against the
      version recorded by `f795f3c`, before the decomposition); every L0, L1, L2 and
      process fact green (`dotnet test tests/Cli.Tests`: 91 passed, 0 failed); the
      public surface unchanged (`Program`, `ExitCode` the only public types of the
      assembly; `Protocol.Tests.SurfaceTests` green against
      `PublicSurface.approved.txt`).
- [x] 2026-09-14 — The documents follow the front door's contract: the `states`
      example gives the library's numbers field by field through `SolveStates` and
      `SolveRocketStates` (`LibraryEqualityTests.TheStatesExampleEqualsTheLibraryFieldByField`,
      a records file with and without exits); an invalid record is exit code 2 naming
      its file and position with the front door's reason (the pinned fragments of
      `InputDocumentTests`, `states-two-targets.json` and `states-rocket-without-enthalpy.json`);
      the exception → exit code rule maps an input refusal to 2 and an accelerator
      failure or any other exception to 3
      (`ExitCodeTests.AnExceptionMapsToItsDocumentedExitCode`); an `auto` run
      with CUDA forbidden writes the reason in `run.accelerator.cudaSkippedBecause` and
      the `devices` listing names the variable too, both schema files listing the field
      (`OutputDocumentTests.AnAutoRunThatFellBackSaysWhy`, a separate process
      with `APTHERMO_NO_CUDA=1`). Each fact seen red once and reverted: the fallback
      reason not written, an unexpected exception mapped to 2.
- [x] 2026-09-15 — Every ticked criterion above re-verified on the decomposed and
      repaired code at `62cd99e`: its tests green in the full suite
      (`APTHERMO_NO_CUDA=1`, every category, 3037 tests, none skipped), and
      CUDA-category evidence on the reference machine (`tests/Execution.Tests`, 41,
      and the long-running sweep and throughput tests).
- [x] 2026-09-15 — `apthermo --version` prints `Program.Version` and exits 0, in
      process and as a separate process
      (`CommandLineTests.VersionPrintsTheToolVersionAndExits0`,
      `ProcessTests.TheExecutablePrintsItsVersionWithExit0`). Without
      `--database`, a run's `run.database.thermoPath`/`.transPath` are the embedded
      markers `"embedded:thermo.inp"`/`"embedded:trans.inp"` with 64-character SHA-256
      hashes, in process and from an empty working directory as a separate process
      (`CommandLineTests.WithoutDatabaseTheRunUsesTheEmbeddedDatabase`,
      `ProcessTests.TheExecutableUsesTheEmbeddedDatabaseFromAnEmptyWorkingDirectory`).
      Both facts seen red once (AGENTS.md §13): making `DatabaseFiles.Load` call
      `LoadFromDirectory` on the current directory instead of `LoadEmbedded` when no
      `--database` is given turned the embedded-database fact red, exit code 2, `no
      thermo.inp in the database directory '…'`; disabling the `--version` branch of
      `Program.Run` turned the version fact red, exit code 2, `no command given`
      (`--version` alone then falls through to the ordinary "no command" refusal).
      Reverted, nothing of either mutation committed. `Bits.approved.txt` unchanged: the
      JSON hash of every example is taken with its top-level `run` property cut out
      first (`RunPropertyCut`, above), and the CSV form never carries `run` at all
      (`Output.CsvOutput`), so the database path this criterion moves from a directory
      to the embedded markers reaches neither hash. Proved directly too: the packing
      proof of `Problems`' BOOT.md ran an approved CSV example
      (`documents/rocket-lox-lh2.approved.csv`) from an empty directory without
      `--database`, through the packed tool, and found it byte-for-byte unchanged.
- [x] 2026-09-17 — The schema files of `Schemas/` are embedded in the assembly and
      served by `apthermo schema [name]` byte for byte to standard output, or to
      `--output`, with exit code 0; a missing or an unknown name is exit code 2 naming
      every embedded name, read from the assembly manifest rather than typed; no copy of
      a schema lives outside this node. `tests/Cli.Tests` reads them directly through
      `SchemaResources`, the type under test; `tests/Docs.Tests` reads them through the
      command, the only test node that has no other way to reach this node's contract
      (`tests/Cli.Tests/SchemaCommandTests`, the facts and their mutations recorded in
      that node's `BOOT.md`, criterion of 2026-09-17;
      `tests/Docs.Tests/SchemaValidationTests`). Found and fixed from
      the CLI audit's findings C1 through C5, in `68f540a`.
- [x] 2026-09-24 — The exception → exit code rule holds under the root's Diagnostics
      constraint without a `catch (Exception)` in `Program.Run` (API.md, the ⚠ of
      2026-09-24): `Program.Run` catches only `InputException` (2),
      `AcceleratorUnavailableException`, `IOException` and `UnauthorizedAccessException`
      (3); every other exception leaves it, proved directly
      (`ExitCodeTests.AnExceptionOutsideTheFourDocumentedTypesLeavesRunInProcess`, a
      `TextWriter` that throws from inside `Run`'s try block) and the mapping itself
      unit-tested (`ExitCodeTests.AnExceptionMapsToItsDocumentedExitCode`,
      `AnUnnamedExceptionIsExit3ThroughTheUnhandledExceptionRule`). The process-level
      half — `Program.Main` installing an `AppDomain.CurrentDomain.UnhandledException`
      handler that reports the same `Type: message` line and calls
      `Environment.Exit(3)` — is proved once by a scratch console program outside the
      tree, the same shape as `Program.Main`'s handler, throwing after installing it:
      exit code 3, `InvalidOperationException: a defect of this node` on standard
      error, `dotnet run -c Release` on 2026-09-24.

- [x] 2026-09-26 — The audit fixes of that date (Constraints). Evidence due, each fact
      red once against the code of `9c33398`:
      - every CSV header this node writes, over every command, format and approved
        example, has unique names;
      - a document with a repeated member is refused (exit 2, with its path), in a
        problem document and in a state record;
      - `--output=` and `--database=` are exit 2 naming the option;
      - the audit's `misname.jsonl` refusal names line 3;
      - `--mass-tolerance` with a reactant document is exit 2;
      - `run.massTolerance` of a reactant document is 0.01;
      - a malformed database line is reported with one prefix.

      Every approved CSV output (the tests node's, the docs tests node's, the bit
      snapshots) is re-approved for the new header in the same commit, and nothing
      else in them moves. The schemas are unchanged: the JSON documents did not change
      shape.

      Ticked 2026-09-27, each fact shown red once against the pre-fix code:
      - `CsvTests.TheStatesCsvHeaderNamesAreUniqueEvenWhenAnInputCollidesWithAStationField`:
        every CSV header column name is unique even when an input collides with a
        station field name; red before `CsvOutput.HeaderOf`'s `inputs.` prefix (48
        distinct names expected, 45 actual, the collision);
      - `InputDocumentTests.AnInvalidDocumentIsExit2WithTheDocumentedMessageAndNoOutput`
        over `duplicate-field.json` and `states-duplicate-field.json`: a document
        with a repeated member is exit 2 naming the field and its path, red before
        `StrictObject`'s duplicate check (exit 0, the last value silently used);
      - `ProcessTests.AnEmptyOutputValueIsExit2AsARealProcess`,
        `ProcessTests.AnEmptyDatabaseValueIsExit2EvenWhenTheWorkingDirectoryHoldsAThermoInpFile`
        and two `CommandLineTests.InvalidCommandLinesAreExit2NamingTheOffender`
        rows (`--output=`, `--database=`): both empty-valued options are exit 2
        naming the option, red before `OptionValues.ParsePath` (exit 3 unhandled,
        or the working directory's `thermo.inp` silently read);
      - `ExitCodeTests.ARuleProblemValidationAppliesIsNamedByTheRecordsFileAndLineNotABatchLocalIndex`
        (the audit's own `misname.jsonl` reproduction: three lines, the third
        offending): the refusal names line 3, red before the front door's
        `noun`/`StateRecordException` change (it named line 2, the batch-local
        index misread as the line);
      - `ExitCodeTests.MassToleranceDoesNotApplyToAReactantPropellant` and
        `ExitCodeTests.AReactantPropellantsMassRefusalNamesThePropellantsMixture`:
        `--mass-tolerance` with a reactant document is exit 2, and a reactant
        propellant's own mass refusal names `the propellant's mixture (case i)`
        rather than `mixture i`; both red before `ProblemCommand`'s
        `CheckMassToleranceApplies` and its `MixtureMassException` catch (exit 0
        with the option silently ignored, and the front door's own wording);
      - `ExitCodeTests.AMalformedDatabaseLineIsReportedWithOnePrefix`: a malformed
        database line is reported with exactly one `file:line:` prefix, red before
        `DatabaseFiles.ReadDatabase` dropped the redundant one (`thermo.inp:5758:
        thermo.inp:5758: …` doubled).

      Every approved CSV output this node's own tests carry is re-approved for the
      new header in the same commit as the fix, and the bit snapshot moves on no
      other line (`Cli.Tests/Bits.approved.txt`'s diff: the JSON-hash half of every
      line is unchanged, the CSV-hash half of every line that has a CSV form moved,
      `species` — which has none — unchanged; `Bits.linux.approved.txt` is re-approved
      by the owner under WSL, outside this coder's evidence). The docs tests node
      carries no approved CSV output (`tests/Docs.Tests` stayed 29/29 unchanged
      through this fix), so it needed no re-approval. The schemas are unchanged: the
      JSON documents did not change shape.

      `dotnet test tests/Cli.Tests`: 129/129, none skipped.
      `APTHERMO_NO_CUDA=1 dotnet test APThermo.sln --filter "Category!=LongRunning"`:
      green on the merged tree (the merge commit's own record has the final count).
      `dotnet build APThermo.sln`: 0 warnings, 0 errors. The protocol lint: 0 errors,
      0 warnings. The public surface is unchanged.
- [x] 2026-09-28 — The audit fixes of 2026-09-28 (Constraints). Each fact is red once
      against `5a732f0`, as a process or through `Program.Run`, asserting the exit
      code and the path in the message
      (`tests/Cli.Tests/SecondAuditFixTests.cs`):
      - the audit's three range documents (`range-inf`: `{"from": 1.0, "to": 1e308,
        "step": 1e-300}`, `range-intmax`: `{"from": 1.0, "to": 2147483648.0, "step":
        1.0}`, `range-overflow`: `{"from": -1e308, "to": 1e308, "step": 1e300}`) and
        `range-3e9` (`{"from": 0.0, "to": 3e9, "step": 1.0}`): exit 2 naming
        `$.sweep.pressure` and the limit, `range-3e9` by the true reason, not the old
        false "not an integer" one
        (`ARangeWithANonFiniteStepCountIsRefusedNamingThePathAndTheLimit`,
        `ARangeOfTwoToTheThirtyOneStepsIsRefusedNamingThePathAndTheLimit`,
        `ARangeSpanningTheWholeDoubleRangeIsRefusedNamingThePathAndTheLimit`,
        `ARangeOfThreeBillionStepsIsRefusedByTheAxisLimitNotAFalseStepReason`);
      - a product of three axes above the case limit (300 x 300 x 300 = 27 000 000):
        exit 2 naming `$.sweep` and the limit, before any solve
        (`ASweepWhoseCartesianProductExceedsTheCaseLimitIsRefusedBeforeAnySolve`);
      - a lone surrogate in a reactant name, a composition key, an unknown record
        member and an unknown root member: exit 2 naming the path
        (`ALoneSurrogateInAReactantNameIsExit2NamingThePath`,
        `ALoneSurrogateInACompositionKeyIsExit2NamingThePath`,
        `ALoneSurrogateInAnUnknownRecordMemberIsExit2NamingThePath`,
        `ALoneSurrogateInAnUnknownRootMemberIsExit2NamingThePath`);
      - `--output " "` and `--output "  "`: exit 2 naming the option
        (`AWhitespaceOnlyOutputValueIsExit2NamingTheOption`);
      - `equilibrium <reactant document> --mass-tolerance 0.01`: exit 2, as `API.md`
        now says; a reactant document's mass refusal starts with the document's path
        (`MassToleranceAgainstAReactantDocumentIsExit2AsApiNowSays`,
        `ExitCodeTests.AReactantPropellantsMassRefusalNamesThePropellantsMixture`);
      - the `species` listing's `run` section records neither `threshold` nor
        `massTolerance` (`TheSpeciesListingsRunSectionRecordsNoThresholdOrMassTolerance`,
        observation 8).

      Red-once: `src/Cli` checked out to `5a732f0`
      (`git checkout 5a732f0 -- src/Cli`), the file's constant references pinned to
      their literal values for the run (the constants do not exist at that commit),
      12 of the 13 facts failed for the documented mechanism (an unhandled
      `OverflowException` on the two step-count facts, an unhandled
      `OutOfMemoryException` on the case-count fact against the old, unbounded
      allocation, an unhandled `ArgumentException` on the blank-output facts, an
      unhandled `InvalidOperationException` on the four surrogate facts, and the old
      false "not an integer" reason on the three-billion-step fact); the thirteenth
      (the reactant document's `--mass-tolerance` exit code, whose refusal already
      existed at `5a732f0`, only its message's path prefix being new) passed, as
      expected, since it pins existing behaviour, not this day's fix. `src/Cli`
      restored (`git checkout HEAD -- src/Cli` after committing the fix), all 13
      green.

      `API.md` states every refusal and both limits (`SweepValues.MaxAxisValues`,
      `SweepDocumentReader.MaxCases`) in its errors table and the `sweep` field list;
      the guide's `--mass-tolerance` sentence names the documents and commands the
      option applies to and says a reactant propellant's document is exit 2.
      `CHANGELOG.md`'s 0.2.0 entry names every user-visible change above.

      Evidence: `dotnet test tests/Cli.Tests/APThermo.Cli.Tests.csproj` is 142 of 142
      (129 baseline, plus these 13 facts); `dotnet test
      tests/Docs.Tests/APThermo.Docs.Tests.csproj` is 29 of 29, unchanged, no approved
      output moved (the guide's `--mass-tolerance` sentence is prose, not a checked
      invocation). No `Bits*.approved.txt` or `PublicSurface.approved.txt` moved
      (`git status --short`): the `run` section is cut out before the bit hash is
      taken (`Harness.RunPropertyCut`), so removing `species`'s `threshold` and
      `massTolerance` moves no snapshot. The schemas are unchanged save
      `species.schema.json`, whose `run` no longer requires or declares `threshold`
      and `massTolerance`. `dotnet build APThermo.sln`: 0 warnings, 0 errors. The
      protocol lint: 0 errors, 0 warnings.
- [x] 2026-10-03 — Warm solvers for in-process invocations (the test pyramid, root `BOOT.md`, Test time
      budgets; `API.md`, "Entry point (tree contract)"): `Program.RunCached` keeps one solver per database
      content (the provenance hashes) and requested accelerator, `Run` is unchanged, a warm invocation gives
      the bytes of a fresh one, and no `Bits*.approved.txt` moved. `Cli.Tests.SolverCacheTests`, over `Count`:
      one solver for invocations on one database, two for a second content, the warm bytes equal the fresh
      ones with `run` cut, disposing empties the cache. Red once: the key replaced by a constant, the
      two-contents fact red (expected 2, actual 1), reverted. The accelerator half of the key has no fact: a
      second accelerator is an `auto` run that binds CUDA on a machine that has it, and the command line's
      tests are CPU only; it is the third element of the key tuple and nothing else reads it. The shipped cold
      path is held to the same bits by `Cli.Tests.BitSnapshotTests.EveryExampleGivesTheRecordedOutputThroughTheProcess`
      (`tests/Cli.Tests/BOOT.md`, the criterion of this date). `dotnet build APThermo.sln`: 0 warnings,
      0 errors; the protocol lint: 0 errors, 0 warnings.

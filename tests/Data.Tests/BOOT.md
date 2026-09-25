# BOOT.md — Data.Tests

## Purpose

The definition of what "`Data` is ready" means: the levels of verification, what
each is checked against, what is covered and what is not. This is not "tests for the
code" (AGENTS.md §1): it is the readiness criterion, moved into a node of its own.

| Level | What it checks | Against what (source of truth) | State |
|---|---|---|---|
| L0 | numeric field reading: `D`/`E`/blank exponents, a sign in place of the exponent letter, bare decimals, blank fields, non-numeric text | expected doubles in the theory data of `FortranNumberTests` (the one place a number is typed: the forms are the subject, not the data) | ✅ 2026-09-12 |
| L1 | full loads of the committed `data/thermo.inp` and `data/trans.inp`: counts, fixture records, interval ordering and contiguity, anomaly list, transport blocks, atomic weights, failure on corrupted copies | an independent line scan in the test, the approved anomaly list, the fixture records written by `transcribe.py` | ✅ 2026-09-12 |
| L1 | the same-name groups of the committed file: `Records` returns every record of a name in file order and the indexer the first of them; a negative interval count fails as a format error with its line, the seventh corruption case | the test's own scan of the file for repeated names (`Cr(cr)`, `Fe(a)`, `Cr2O3(I)` among them), the minimal in-memory file (`ThermoLoadTests`, `CorruptionTests`) | ✅ 2026-09-14 |
| L1 | the database embedded in the assembly (root `BOOT.md`, `## Delivery`, `Data`): the three resources' bytes hash to the same SHA-256 as `data/`'s files, `LoadBundled()` equals `Load()` species by species and coefficient by coefficient, `BundledNotice()` equals `data/NOTICE` | the committed `data/thermo.inp`, `data/trans.inp`, `data/NOTICE` | ✅ 2026-09-15 |
| Protocol | the tree invariant, documents against code | `AGENTS.md`, the surface snapshot | ✅ (2026-09-13, the Protocol.Tests node) |

Each next level makes sense only when the previous one is green.

## Invariants

- **The reference is files, not numbers in the test code.** Expected records live in
  JSON fixture files next to the tests; a test never types a coefficient.
- **Counts are generated, not typed**: the number of records the loader must find is
  produced by the test's own scan of the file text, so the criterion cannot fall
  behind the file.
- **Corruption tests mutate an in-memory copy** of one record (a minimal file built
  from the `H2O` record and parsed from a string); the committed data files are never
  touched.

  ⚠ 2026-09-12: stood "mutate a copy in the test's temporary directory". A minimal
  in-memory file proved enough, keeps the line numbers small and leaves nothing on
  disk; changed when the corruption tests were written.

## Dependencies

- [Data](../../src/Data/API.md) — what is being checked.
- [Fixtures](../Fixtures/API.md) — `RepositoryPaths`, the repository root and the `data/` directory.

Outside the tree: xunit; Python 3 for `transcribe.py`; the committed data files
`data/thermo.inp`, `data/trans.inp`.

## Constraints

- Part of the default test command.
- Data paths are resolved from the repository root through `RepositoryPaths` of the
  fixtures node (the root is found from a source file with `[CallerFilePath]`), never
  by `../../..` chains.
- Tests write nothing into the working directory except the
  `records/interval-anomalies.actual.txt` of a failed approval comparison, written
  next to the approved file so that the two can be compared, and ignored by git.
- Fixture records are regenerated with `python tests/Data.Tests/transcribe.py`; the
  species and transport blocks it transcribes are listed at the top of the script.

  ⚠ 2026-09-12: the third constraint stood "Tests do not write into the working
  directory". The approval comparison of the anomaly list writes its actual text next
  to the approved file, as approval tests do; reworded when that test was written.

## Shape exceptions

Added 2026-09-14 by the design session, after the protocol tests node's measurements found
these two constructors over the root's six parameters. Both records mirror, field for
field, the JSON records `transcribe.py` writes into `records/`, and only the deserializer
builds them, so no creation in code can swap an argument; grouping their fields would part
them from the files they read.

| Where | Rule | Measured | Reason |
|---|---|---|---|
| `Records.SpeciesRecord.SpeciesRecord` | parameters | 12 | the species record of `records/species/*.json`, field for field; built by the deserializer only |
| `Records.IntervalRecord.IntervalRecord` | parameters | 7 | a temperature interval of the same records, field for field; built by the deserializer only |

## Acceptance criteria

- [x] 2026-09-12 — L0 green: `FortranNumberTests.ParsesEveryFormOfTheFiles`,
      `FortranNumberTests.RejectsText`.
- [x] 2026-09-12 — L1 green: `ThermoLoadTests` (`EveryRecordOfTheFileIsParsed`,
      `HeaderCarriesTheDefaultIntervalBounds`,
      `FixtureRecordsParseToTheTranscribedValues` for every file under
      `records/species/`, `IntervalAnomaliesEqualTheApprovedList`,
      `AtomicWeightsComeFromTheMonatomicSpecies`, `UnknownNamesAreReportedByName`);
      `TransLoadTests`
      (`EveryBlockOfTheFileIsParsed`, `FixtureBlocksParseToTheTranscribedValues`
      for every file under `records/transport/`, `PairsAreFoundInEitherOrder`);
      `CorruptionTests` (`TheMinimalFileItselfLoads`,
      `ATruncatedCoefficientLineNamesItsLine`,
      `AMissingIntervalFailsBeforeTheEndMarker`, `AFileWithoutTheEndMarkerFails`,
      `ABadCoefficientCountIsRejected`, `AMissingFileIsReportedBeforeParsing`).
- [x] 2026-09-12 — Every check proven non-degenerate once (AGENTS.md §13): a
      coefficient of `records/species/H2O.json` altered by 1e-9 turned
      `FixtureRecordsParseToTheTranscribedValues` red; the parser made to drop the
      last product record turned `EveryRecordOfTheFileIsParsed` red (2030
      expected, 2029 found); `ATruncatedCoefficientLineNamesItsLine` was red
      (line 5 reported, 6 expected) until the parser stamped the failing field's line;
      `FixtureBlocksParseToTheTranscribedValues` was red while the transcription
      collapsed the double blank of the `H2` reference. Mutations reverted; nothing of
      them is committed.

  ⚠ 2026-09-14: the L1 list above held `Loading_the_full_file_takes_under_a_second`,
  a wall-clock second in the fast set with no level in the table and no criterion
  behind it, red on a cold or loaded machine for no defect (the clean-code review's
  F-TK-14). The root states no latency target for version 1, and the `Data` node
  records the load time as a measurement; the test is deleted.
- [x] 2026-09-14 — The facts of 2026-09-14 (the level table's second L1 row):
      `ThermoLoadTests.EveryRecordOfARepeatedNameIsReturnedInFileOrder`
      over the repeated names found by the test's own scan of the file (generated,
      not typed; `Cr(cr)`, `Fe(a)` and `Cr2O3(I)` are among them), with the indexer
      and `TryGet` returning the first of each and `Records` of an unknown name
      empty; `CorruptionTests.ANegativeIntervalCountNamesItsLine`; each seen
      red once (`Records` made to return the first record only; the count check
      removed from the reader) and reverted.
- [x] 2026-09-15 — The bundled-database level of the table above:
      `BundledDatabaseTests` (`EmbeddedResourceBytesEqualTheCommittedFiles`,
      `LoadBundledEqualsLoadOnEverySpeciesAndCoefficient`,
      `BundledNoticeEqualsTheCommittedFile`), each seen red once (AGENTS.md §13):
      the embedded `thermo.inp` resource pointed at a copy truncated to its first 100
      lines turned both the hash test and the equality test red (a
      `DatabaseFormatException` at line 96, and a hash mismatch); reverted, nothing of
      the mutation committed.

## Taboos

- Do not loosen a comparison: parsed doubles are compared exactly, not approximately.
- Do not hard-code expectations that exist in a fixture file.
- Do not skip a test when a data file is missing: a missing file is a failure.

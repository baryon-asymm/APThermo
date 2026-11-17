# API.md — Data.Tests

The node exposes nothing outward: nobody references a test project. Its contract
points upward: it is what the parent may consider proven about `Data`.

## What this node guarantees

| Claim | Confirmed by | State |
|---|---|---|
| every record of the committed NASA files is parsed, and the fixture records parse to the transcribed values exactly | L1: `ThermoLoadTests.EveryRecordOfTheFileIsParsed`, `ThermoLoadTests.FixtureRecordsParseToTheTranscribedValues`, `TransLoadTests.EveryBlockOfTheFileIsParsed`, `TransLoadTests.FixtureBlocksParseToTheTranscribedValues` | ✅ 2026-09-12 |
| the column parser handles every numeric form present in the files | L0: `FortranNumberTests.ParsesEveryFormOfTheFiles` | ✅ 2026-09-12 |
| the corruption controls fail the load with the line number of the bad field | L1: `CorruptionTests` | ✅ 2026-09-12 |
| missing required interval-header and species-properties tails fail at their source line; complete numeric blanks and optional text tails remain valid | `FixedWidthHeaderTests` (synthetic format controls), full database and fixture tests | ✅ 2026-11-17 |
| only blank lines and comments may follow `END REACTANTS`; other lines are rejected at their exact source location | `CorruptionTests.ABlankOrCommentTrailerIsAccepted`, `UnexpectedTrailerContentIsRejectedAtItsSourceLine` | ✅ 2026-11-16 |
| atomic weights come from the monatomic species records | L1: `ThermoLoadTests.AtomicWeightsComeFromTheMonatomicSpecies` | ✅ 2026-09-12 |
| interval bounds are stored as written, and the records whose first interval is not ascending are exactly those on the approved anomaly list | L1: `ThermoLoadTests.IntervalAnomaliesEqualTheApprovedList` | ✅ 2026-09-12 |
| every record of a repeated name is reachable in file order through `Records`, and the indexer returns the first | L1: `ThermoLoadTests.EveryRecordOfARepeatedNameIsReturnedInFileOrder` | ✅ 2026-09-14 |
| a negative interval count fails the load with the line number of the field | L1: `CorruptionTests.ANegativeIntervalCountNamesItsLine` | ✅ 2026-09-14 |
| the three resources embedded in the assembly hash to the same SHA-256 as `data/`'s files, and `LoadBundled()` equals `Load()` species by species and coefficient by coefficient | L1: `BundledDatabaseTests` | ✅ 2026-09-15 |

## What the tests rely on

- ⚠ 2026-11-17: the former general corrupted-record claim exceeded its named
  controls: header and properties truncations were not covered. The table now names
  that evidence's scope and the new independent fixed-column controls explicitly.
- The repository root is found from the test source file; data files are read from
  `data/` under it.
- Fixture records are JSON files in this node's `records/` directory, one per species
  or transport block, written by `transcribe.py` of this node, an independent
  token-based reader of the same files, with the line numbers noted.
- The independent record count is a regular-expression scan over the file text
  implemented in the test, separate from the loader.

# API.md — Data.Tests

The node exposes nothing outward: nobody references a test project. Its contract
points upward: it is what the parent may consider proven about `Data`.

## What this node guarantees

| Claim | Confirmed by | State |
|---|---|---|
| every record of the committed NASA files is parsed, and the fixture records parse to the transcribed values exactly | L1: `ThermoLoadTests.EveryRecordOfTheFileIsParsed`, `ThermoLoadTests.FixtureRecordsParseToTheTranscribedValues`, `TransLoadTests.EveryBlockOfTheFileIsParsed`, `TransLoadTests.FixtureBlocksParseToTheTranscribedValues` | ✅ 2026-09-12 |
| the column parser handles every numeric form present in the files | L0: `FortranNumberTests.ParsesEveryFormOfTheFiles` | ✅ 2026-09-12 |
| a corrupted record fails the load with the line number of the bad field | L1: `CorruptionTests` | ✅ 2026-09-12 |
| atomic weights come from the monatomic species records | L1: `ThermoLoadTests.AtomicWeightsComeFromTheMonatomicSpecies` | ✅ 2026-09-12 |
| interval bounds are stored as written, and the records whose first interval is not ascending are exactly those on the approved anomaly list | L1: `ThermoLoadTests.IntervalAnomaliesEqualTheApprovedList` | ✅ 2026-09-12 |
| every record of a repeated name is reachable in file order through `Records`, and the indexer returns the first | L1: `ThermoLoadTests.EveryRecordOfARepeatedNameIsReturnedInFileOrder` | ✅ 2026-09-14 |
| a negative interval count fails the load with the line number of the field | L1: `CorruptionTests.ANegativeIntervalCountNamesItsLine` | ✅ 2026-09-14 |
| the three resources embedded in the assembly hash to the same SHA-256 as `data/`'s files, and `LoadBundled()` equals `Load()` species by species and coefficient by coefficient | L1: `BundledDatabaseTests` | ✅ 2026-09-15 |

## What the tests rely on

- The repository root is found from the test source file; data files are read from
  `data/` under it.
- Fixture records are JSON files in this node's `records/` directory, one per species
  or transport block, written by `transcribe.py` of this node, an independent
  token-based reader of the same files, with the line numbers noted.
- The independent record count is a regular-expression scan over the file text
  implemented in the test, separate from the loader.

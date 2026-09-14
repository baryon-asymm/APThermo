# API.md — Data.Tests

The node exposes nothing outward: nobody references a test project. Its contract
points upward: it is what the parent may consider proven about `Data`.

## What this node guarantees

| Claim | Confirmed by | State |
|---|---|---|
| every record of the committed NASA files is parsed, and the fixture records parse to the transcribed values exactly | L1: `ThermoLoadTests.Every_record_of_the_file_is_parsed`, `ThermoLoadTests.Fixture_records_parse_to_the_transcribed_values`, `TransLoadTests.Every_block_of_the_file_is_parsed`, `TransLoadTests.Fixture_blocks_parse_to_the_transcribed_values` | ✅ 2026-09-12 |
| the column parser handles every numeric form present in the files | L0: `FortranNumberTests.Parses_every_form_of_the_files` | ✅ 2026-09-12 |
| a corrupted record fails the load with the line number of the bad field | L1: `CorruptionTests` | ✅ 2026-09-12 |
| atomic weights come from the monatomic species records | L1: `ThermoLoadTests.Atomic_weights_come_from_the_monatomic_species` | ✅ 2026-09-12 |
| interval bounds are stored as written, and the records whose first interval is not ascending are exactly those on the approved anomaly list | L1: `ThermoLoadTests.Interval_anomalies_equal_the_approved_list` | ✅ 2026-09-12 |
| every record of a repeated name is reachable in file order through `Records`, and the indexer returns the first | L1: `ThermoLoadTests.Every_record_of_a_repeated_name_is_returned_in_file_order` | ✅ 2026-09-14 |
| a negative interval count fails the load with the line number of the field | L1: `CorruptionTests.A_negative_interval_count_names_its_line` | ✅ 2026-09-14 |

## What the tests rely on

- The repository root is found from the test source file; data files are read from
  `data/` under it.
- Fixture records are JSON files in this node's `records/` directory, one per species
  or transport block, written by `transcribe.py` of this node, an independent
  token-based reader of the same files, with the line numbers noted.
- The independent record count is a regular-expression scan over the file text
  implemented in the test, separate from the loader.

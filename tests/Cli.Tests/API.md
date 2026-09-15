# API.md — Cli.Tests

The node exposes nothing outward. Its contract points upward: what the parent may
consider proven about `Cli`.

## What this node guarantees ✅

| Claim | Confirmed by | State |
|---|---|---|
| the documented input, states and output shapes are what the executable reads and writes: every example document runs and validates against the schema files, and the schema's field lists are the library's structs | L0, L1 against the schema files (`InputDocumentTests`, `OutputDocumentTests`) | ✅ |
| the executable's numbers are the library's numbers, field by field and exactly, for a rocket case with transport, an hp case and an elemental tp case | L2 (`LibraryEqualityTests`) | ✅ |
| exit codes and error messages follow the contract, in-process and as a process; an invalid document writes nothing and names the JSON path | L0 in-process (`ExitCodeTests`, `InputDocumentTests`, `CommandLineTests`) and as a process (`ProcessTests`) | ✅ |
| a state record or an elemental propellant whose composition does not weigh one kilogram is exit code 2 with the documented message naming the record (file and position, or the JSON path) and the mass; a record that does weigh one kilogram solves; the record examples of the `Cli` API solve | L0 (`InputDocumentTests`, `ExitCodeTests.A_record_that_weighs_one_kilogram_is_exit_0_and_one_that_does_not_is_named_by_its_line`) | ✅ |
| `--mass-tolerance` is the tolerance the run declares, echoed in `run`, and every case reports the mass of its mixture, exactly the library's | L0, L1, L2 (`CommandLineTests`, `OutputDocumentTests.The_mass_tolerance_is_echoed_and_every_case_reports_the_mass_of_its_mixture`, `ExitCodeTests.The_mass_tolerance_option_is_the_tolerance_the_run_declares`, `LibraryEqualityTests`) | ✅ |
| sweeps expand in the documented order into one document, states files of every accepted form give the same cases in input order, the threshold and the transport flag act as documented, the CSV has the documented layout | L1 (`OutputDocumentTests`, `CsvTests` against the approved file) | ✅ |
| the states example gives the library's numbers through the front door's state batches; an invalid record carries the front door's reason behind its source; an unexpected exception is exit code 3; a run that fell back to the CPU accelerator says why in its document | L0, L2 (`LibraryEqualityTests`, `ExitCodeTests`, `OutputDocumentTests`, the facts of 2026-09-14) | ✅ 2026-09-14 |
| every example's output is byte for byte what it was before the decomposition of 2026-09-14, the `run` section aside | Bits (`BitSnapshotTests`, `Bits.approved.txt`; BOOT.md, the criterion of 2026-09-15) | ✅ 2026-09-15 |

⚠ 2026-09-15: this row claimed every example's output is byte for byte what it was
before the decomposition, `run` aside. Until this date the JSON half hashed
`JsonNode.Parse(json).AsObject()` with `run` removed, then `ToJsonString()`: a compact
re-serialization with the default encoder, so the document's indentation, line breaks,
the final newline and string escaping were not guarded — only its values, keys and
their order were. Found by the repair review of 2026-09-15 (R-Cli.Tests-2); `src/Cli/BOOT.md`
and `tests/Harness/BOOT.md` carry the same correction where they relied on this claim.
The JSON half now hashes the bytes the command line delivers for the document with the
top-level `run` property cut out (`RunPropertyCut`, Cli.Tests BOOT.md, the Bits level),
so the claim holds as written; every line of `Bits.approved.txt` was re-approved once
for the change of the hash alone (Cli.Tests BOOT.md, the criterion of 2026-09-15).

## What the tests rely on

- JSON schema files in this node's `schemas/` directory for the input, states, output,
  species and devices documents, and a validator of this node for the keywords they use.
- Example documents in this node's `documents/` directory, generated from the fixtures;
  the examples of the `Cli` API, read from `API.md` at run time; the approved CSV
  `documents/rocket-lox-lh2.approved.csv`.
- The committed database and the CPU accelerator; the `dotnet` host for the process runs.
- `Bits.approved.txt` in this node: one line per example output, the example and the
  SHA-256 of the bytes the command line delivers for its JSON document with the
  top-level `run` property cut out (`RunPropertyCut`), or of its CSV text (2026-09-14;
  the JSON half redefined 2026-09-15, the warning above).

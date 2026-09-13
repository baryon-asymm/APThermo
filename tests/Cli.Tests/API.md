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
| sweeps expand in the documented order into one document, states files of every accepted form give the same cases in input order, the threshold and the transport flag act as documented, the CSV has the documented layout | L1 (`OutputDocumentTests`, `CsvTests` against the approved file) | ✅ |

## What the tests rely on

- JSON schema files in this node's `schemas/` directory for the input, states, output,
  species and devices documents, and a validator of this node for the keywords they use.
- Example documents in this node's `documents/` directory, generated from the fixtures;
  the examples of the `Cli` API, read from `API.md` at run time; the approved CSV
  `documents/rocket-lox-lh2.approved.csv`.
- The committed database and the CPU accelerator; the `dotnet` host for the process runs.

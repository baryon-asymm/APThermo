# API.md — Cli.Tests

The node exposes nothing outward. Its contract points upward: what the parent may
consider proven about `Cli`.

## What this node guarantees

| Claim | Confirmed by | State |
|---|---|---|
| the documented input and output schemas are what the executable reads and writes | L0, L1 against the schema files | ⏳ |
| the executable's numbers are the library's numbers | L2 | ⏳ |
| exit codes and error messages follow the contract | L0 in-process and as a process | ⏳ |

## What the tests rely on

- JSON schema files in this node for the input and output documents.
- Example documents copied from the `Cli` API into this node's `documents/` directory.
- The committed database and the CPU accelerator.

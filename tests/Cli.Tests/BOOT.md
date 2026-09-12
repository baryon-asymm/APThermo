# BOOT.md — Cli.Tests

## Purpose

The definition of what "`Cli` is ready" means.

| Level | What it checks | Against what (source of truth) | State |
|---|---|---|---|
| L0 | input document parsing and validation messages; option parsing; exit codes | the schema files of this node; documented exit codes | ⏳ |
| L1 | the example documents of the `Cli` API run end to end and validate against the output schema; CSV layout | schema files; a golden CSV | ⏳ |
| L2 | the LOX/LH2 example document's numbers equal the library result field by field | the `Problems` result of the same case (reflection-enumerated fields) | ⏳ |
| Protocol | the tree invariant, documents against code | `AGENTS.md`, the surface snapshot | ⏳ |

## Invariants

- Documents in tests are files in this node, not strings in code.
- The CLI is exercised in-process through its entry point and, once per level, as a
  separate process, so that exit codes and standard streams are real.

## Dependencies

- [Cli](../../src/Cli/API.md) — what is being checked.
- [Problems](../../src/Problems/API.md) — the library result the document is compared with.
- [Data](../../src/Data/API.md) — the database.
- [Fixtures](../Fixtures/API.md) — the reference case the example document encodes.

Outside the tree: xunit.

## Constraints

- Part of the default test command; the CPU accelerator only.
- Output documents are written into the test's temporary directory.

## Acceptance criteria

- [ ] L0, L1, L2 green (date, test names).
- [ ] Every check proven non-degenerate once: a field renamed in the schema, an exit
      code swapped, a unit changed in the document, each seen red.

## Taboos

- Do not compare documents by string equality of floating-point text: parse and
  compare numbers with the documented tolerance (exact for pass-through fields).
- Do not skip when the data files are missing.

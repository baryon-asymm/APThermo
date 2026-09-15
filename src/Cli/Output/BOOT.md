# BOOT.md — Cli.Output

## Purpose

Renders a run's cases into the output document, in JSON or CSV, and delivers it to a
file or to standard output. A child node of `src/Cli`
(`AerospacePropellantThermodynamics.Cli.Output`, compiled into the parent's assembly,
root `BOOT.md`, Constraints, 2026-09-15): it has its own reason to change — a field
added to the document, a new document form — and the rest of the node reaches it
through `DocumentWriter.Write` (a full run), `DocumentWriter.Deliver`/`Render` (a
listing's own document) and `RunSection.Write`/`WriteAccelerator` (the `run` object),
never through the per-station field projection it renders from.

Split out of the parent's `## Structure` review of 2026-09-15 (the child-nodes phase,
root `BOOT.md`): `JsonOutput`, `CsvOutput`, `DocumentWriter`, `RunSection`,
`ExitCodes`, `StationFields` and `Cell` moved here unchanged, `git mv` and a namespace
edit only, no logic touched.

## Invariants

- **One projection, two document forms.** `StationFields` is the only place a station's
  library structs (`MixtureState`, `PerformanceFigures`, `TransportFigures`) are
  reflected into named cells; `JsonOutput` and `CsvOutput` both read a station through
  it, so a field added to a library struct reaches both forms without a second list
  (the parent's `BOOT.md`, F-CL-06, F-CL-07).
- **The non-finite-number rule is one function.** `DocumentWriter.WriteNumber` writes a
  non-finite station value as JSON `null`; every JSON writer of this node calls it
  instead of `Utf8JsonWriter.WriteNumber` directly.
- **The exit code is a pure function of the cases.** `ExitCodes.Of` looks only at
  `CaseStatus` and the transport status of every case and station; it never inspects
  the run or an exception, which `Cli.Failures` (the parent's own) decides instead.

## Dependencies

[Cases](../Cases/API.md)
[CommandLine](../CommandLine/API.md)
[Cli](../API.md)
[Problems](../../Problems/API.md)
[Execution](../../Execution/API.md)
[Performance](../../Performance/API.md)
[Thermo](../../Thermo/API.md)
[Transport](../../Transport/API.md)

Outside the tree: none beyond what the linked nodes already declare in their own
`BOOT.md`.

## Constraints

Inherited from the parent ([BOOT.md](../BOOT.md)) and the root. In addition:

- `StationFields` reads a library struct's fields by reflection, in the field's own
  metadata-token order (source declaration order), never `Type.GetFields()`'s
  undocumented order, so the document's key and column order cannot depend on a
  runtime detail (the parent's `BOOT.md`, F-CL-06).
- No JSON parsing and no document-shape validation: this node only writes.

## Acceptance criteria

- [x] 2026-09-15 — The moved types compile unchanged under
      `AerospacePropellantThermodynamics.Cli.Output` and every test of
      `tests/Cli.Tests` that exercised them before the move (`BitSnapshotTests`,
      `CsvTests`, the rendering cases of `OutputDocumentTests` and `ExitCodeTests`)
      passes after it, same count as before the split
      (`tests/Cli.Tests/BOOT.md`, the Bits level).
- [x] 2026-09-15 — The protocol tests node attributes every type of this directory to
      `Cli.Output` by namespace, not to `Cli` (`Protocol.Tests`,
      `CoverageTests`/`DeclarationTests`, the child-node attribution of 8c3c77f).

## Taboos

- No case building: turning a document into a library problem is `Cli.Cases`'s.
- No command-line concern: `DocumentWriter.Write` takes `Cli.CommandLine`'s
  `CommandOptions` by value and reads only `Format` and `Output`; the threshold comes
  from the run's own `RunLimits` (the parent's own `RunInfo.Limits`), not from the
  options directly. This node does not parse or validate an option.
- No exception handling beyond the non-finite-number rule: a library refusal is
  translated where the library is called (`ProblemCommand`, `Cli.Cases`), not here.

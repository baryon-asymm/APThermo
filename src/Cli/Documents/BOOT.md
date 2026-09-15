# BOOT.md — Cli.Documents

## Purpose

The JSON document shapes of `apthermo` and their strict readers: the problem document
(`propellant`, `problem`, `sweep`, `engine`), the state-record files of the `states`
command, and the front door's refusals renamed to a record's own source. A child node
of `src/Cli` (`AerospacePropellantThermodynamics.Cli.Documents`, compiled into the
parent's assembly, root `BOOT.md`, Constraints, 2026-09-15): it has its own reason to
change — a field, a document shape or a message wording — and the rest of the node
reaches it through one reader per document kind, never through the strict-object
mechanism or the per-entity readers it composes.

Split out of the parent's `## Structure` review of 2026-09-15 (the child-nodes phase,
root `BOOT.md`): `JsonText`, `StrictObject`, `SweepValues`, `ProblemDocumentReader`,
`PropellantDocumentReader`, `ProblemPartReader`, `SweepDocumentReader`,
`StateRecordReader`, `RecordNaming`, `InputDocument`, `ProblemDocument`,
`RocketDocument`, `EquilibriumDocument`, `PropellantDocument`, `ReactantPropellant`,
`ElementalPropellant`, `ReactantDocument`, `SweepDocument` and `RecordSource` moved
here unchanged, `git mv` and a namespace edit only, no logic touched.
`RecordNaming` moved with this cluster rather than staying at the `states` command: its
only parameter beyond the library's own exceptions is `RecordSource`, this node's own,
and its purpose — a refusal renamed from the batch's index to the record's file and
position — is a record-document concern, read by `StatesCommand` as one generic method.

## Invariants

- **Only the document's JSON shape is read here.** The rules of a state record (exactly
  one target, exits needing an enthalpy, a flow only with exits) are the front door's
  (`Problems`' `StateRecord`, `Solver.SolveStates`, `Solver.SolveRocketStates`): this
  node hands the parsed shape over and does not re-decide those rules a second time
  (the parent's `BOOT.md`, the F-AR-02 decision of 2026-09-14).
- **Every field of an object must be read, or `Finish` refuses it.** `StrictObject` is
  the one mechanism of this rule; every reader of this node reads through it, so an
  unknown field or a wrong type is refused at the same place with the same message
  shape, naming its JSON path.
- **A reader's exception already names its source.** `ProblemDocumentReader.Read` folds
  the document's source (a file path) into every `InputException` it raises;
  `StateRecordReader.Read` and `RecordNaming.Named` do the same for a record, down to
  the file and the position within it, never the batch's own index.

## Dependencies

[Cli](../API.md)
[Execution](../../Execution/API.md)
[Problems](../../Problems/API.md)
[Performance](../../Performance/API.md)
[Equilibrium](../../Equilibrium/API.md)

Outside the tree: none beyond what the linked nodes already declare in their own
`BOOT.md`.

## Constraints

Inherited from the parent ([BOOT.md](../BOOT.md)) and the root. In addition:

- No unit conversion and no defaulting beyond what the parent's `API.md` ("Input
  document") documents: a reader either finds a field and returns it, or leaves it to
  the field's own optional/required rule.
- `StrictObject` is a class, not a struct or a static helper, because it carries the
  set of fields already read across the calls a reader makes to it before `Finish`.

## Acceptance criteria

- [x] 2026-09-15 — The moved types compile unchanged under
      `AerospacePropellantThermodynamics.Cli.Documents` and every test of
      `tests/Cli.Tests` that exercised them before the move
      (`InputDocumentTests`, the document-shape cases of `BitSnapshotTests` and
      `OutputDocumentTests`) passes after it, same count as before the split
      (`tests/Cli.Tests/BOOT.md`, the Bits level).
- [x] 2026-09-15 — The protocol tests node attributes every type of this directory to
      `Cli.Documents` by namespace, not to `Cli` (`Protocol.Tests`,
      `CoverageTests`/`DeclarationTests`, the child-node attribution of 8c3c77f).

## Taboos

- No command-line concern: an option or the argument tables are `Cli.CommandLine`'s.
- No case building or result document: turning a document into a library problem, or a
  library result into an output document, is `Cli.Cases`'s and `Cli.Output`'s.
- No reaction to a library refusal beyond naming its source: the translation of the
  library's own exceptions (`ArgumentException`, `KeyNotFoundException`,
  `MixtureMassException`) at solve time stays where the library is called
  (`ProblemCommand`, `Cli.Cases`), not here.

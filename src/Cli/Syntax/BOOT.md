# BOOT.md — Cli.Syntax

## Purpose

The command-line grammar of `apthermo`, decoupled from every command's own logic: the
token walk, the two tables of commands and options, folding the options into one
record, and the checks that a command and its options agree. A child node of `src/Cli`
(`APThermo.Cli.Syntax`, compiled into the parent's assembly,
root `BOOT.md`, Constraints, 2026-09-15) because it has its own reason to change — a
new option or command — and the rest of the node uses it through one function
(`CommandLine.Parse`) and two small records (`Invocation`, `CommandOptions`), never
through its token walk or its tables directly.

The folder is not named `CommandLine`, though its central type is: a namespace and a
type sharing a name inside it forces `CommandLine.CommandLine.Parse(...)` on every
caller inside the `Cli` namespace tree (C# resolves the bare `CommandLine` to the
namespace there, before any `using` directive is even consulted), a cost the .NET
design guidelines advise against and this node does not need to pay. `Syntax` names the
same cluster — the command line's grammar — without colliding with the type.

Split out of the parent's `## Structure` review of 2026-09-15 (the child-nodes phase,
root `BOOT.md`): `ArgumentScanner`, `ScannedArguments`, `CommandLine`, `CommandOptions`,
`CommandSpec`, `CommandTable`, `OptionSpec`, `OptionValues`, `Invocation` and
`OutputFormat` moved here unchanged, `git mv` and a namespace edit only, no logic
touched. `OutputFormat` moved with this cluster rather than staying at the parent's own
level: it is declared and read by four of this cluster's own types (`CommandOptions`,
`CommandSpec`, `CommandTable`, `OptionValues`) and reaches the rest of the node only as
one value already carried by `CommandOptions.Format`, so its own cluster is `Syntax`,
not wherever a `==` check on it happens to sit (`Output/DocumentWriter.cs`,
`SpeciesCommand.cs`).

## Invariants

- **One parser, no dependency.** The token walk (`ArgumentScanner`) knows nothing of
  commands or options beyond the two tables it is handed; `CommandLine.Parse` is the
  only entry point that turns `string[]` into an `Invocation`, so a caller cannot
  partially parse and diverge from the checks (arity, applicability, format).
- **The tables are the one statement of what the command line accepts.** `CommandTable`
  is read by the parser to validate and by itself to build the usage text
  (`CommandTable.Usage`), whose numeric defaults are read from
  `CommandOptions.DefaultThreshold` and the `Problems` node's
  `ElementalMixture.DefaultMassTolerance` rather than typed a second time: a command or
  an option added to the tables reaches parsing, validation and `--help` together,
  never two of the three.
- **A bad value is reported before the command is even looked at**: `CommandLine.Fold`
  applies every option given, regardless of the command, so `apthermo frobnicate
  --threshold -1` reports the threshold, not the unknown command.

## Dependencies

[Cli](../API.md)
[Execution](../../Execution/API.md)
[Problems](../../Problems/API.md)

Outside the tree: none beyond what `Execution` and `Problems` already declare in their
own `BOOT.md`.

## Constraints

Inherited from the parent ([BOOT.md](../BOOT.md)) and the root. In addition:

- Hand-written parsing, no argument-parsing package (the parent's `BOOT.md`, `##
  Structure`, `CommandLine`'s row: "no dependency for it").
- `CommandOptions` is declared with init properties and a default per field, never a
  positional constructor, so a construction site at the tables (`CommandTable.Options`)
  names every field it changes with `with`.
- Depends on [Problems](../../Problems/API.md) for `ElementalMixture`
  (`DefaultMassTolerance`, `IsValidMassTolerance`) and on
  [Execution](../../Execution/API.md) for `AcceleratorKind`.

## Acceptance criteria

- [x] 2026-09-15 — The moved types compile unchanged under
      `APThermo.Cli.Syntax` and every test of
      `tests/Cli.Tests` that exercised them before the move (`CommandLineTests`,
      the option and format cases of `OutputDocumentTests` and `ExitCodeTests`)
      passes after it, same count as before the split
      (`tests/Cli.Tests/BOOT.md`, the Bits and process levels).
- [x] 2026-09-15 — The protocol tests node attributes every type of this directory to
      `Cli.Syntax` by namespace, not to `Cli` (`Protocol.Tests`,
      `CoverageTests`/`DeclarationTests`, the child-node attribution of 8c3c77f).

## Taboos

- No command's own logic here: a command's read-solve-write sequence belongs to its
  command type at the parent's own level (`ProblemCommand`, `StatesCommand`,
  `SpeciesCommand`), not to this node.
- No document parsing: a JSON problem or record document is `Cli.Documents`'.
- No rendering of a result: an output document is `Cli.Output`'s.

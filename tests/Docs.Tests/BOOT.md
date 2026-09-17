# BOOT.md — Docs.Tests

## Purpose

The definition of what "the documentation is ready" means: the guide, the samples and
the schemas are one system, and this node checks them against each other by machine
(root `BOOT.md`, Delivery: Documentation). It runs the command line and the scenarios
in-process through their tree contracts, so a drift between a sentence, a snippet and
an output is a failing test, not a review opinion.

| Level | What it checks | Against what (source of truth) | State |
|---|---|---|---|
| L1 snippets | every marked C# block of `README.md`, `docs/guide/*.md` and the package READMEs under `docs/nuget/` is preceded by a line `<!-- snippet: <name> -->` and equals that sample's region byte for byte (line endings normalized to LF); every scenario name is quoted exactly once | the samples node's source files; the names from `Samples.Scenarios` | ⏳ |
| L2 sample outputs | every scenario of the samples node, run in-process, prints its approved output | `approved/samples/<name>.approved.txt`, whole file | ⏳ |
| L3 CLI examples | every command-line example of the guide (a fenced `console` block whose first line is `$ apthermo …`) names its input document under `samples/cli/` and runs in-process with exit code 0; the delivered JSON document with the top-level `run` property cut out equals the approved file | `approved/cli/<key>.approved.json`, the key the input file's name | ⏳ |
| L4 links | every relative link of `README.md`, `llms.txt`, `docs/**/*.md` and the package READMEs resolves to an existing file (external `http(s)` links and in-page anchors are out) | the repository tree | ⏳ |
| L5 schemas | every document under `samples/cli/problems/` validates against the `input` schema; every record of a file under `samples/cli/states/` (`json` array, object or `jsonl`) against `states`; the schemas are read through `apthermo schema <name>` in-process, so no copy lives here | the command line's embedded schemas (root `BOOT.md`, Delivery: Documentation) | ⏳ |
| L6 guide shape | every page of `docs/guide/*.md` carries the shared headings in order, once each: `## Purpose`, `## When to use`, `## Steps`, `## Errors`, `## See also` | the root `BOOT.md`, Delivery: Documentation | ⏳ |

## Invariants

- **In-process, never a child process**: the command line and the scenarios are run
  through their tree contracts (`Program.Run`), so the checks hold on any machine the
  solution builds on and need no installed tool.
- **No copy of what is checked elsewhere**: the schemas are read through `apthermo
  schema`, the snippet regions from the samples' source files, the guide's blocks from
  the markdown itself. This node holds only its approved files and its tests.
- **Approved files are whole-file comparisons**; on a mismatch the actual bytes are
  written as `<name>.actual.txt` beside the approved one (git-ignored) and the test
  fails naming both paths. No tolerance, no numeric parsing: the numbers are the
  library's, and this node guards that what is shown is what runs.
- **An example without a committed input is a failure**: an L3 example whose input
  document is not under `samples/cli/` cannot be run from the tree, so it fails until
  the document is committed there.

## Dependencies

- [Cli](../../src/Cli/API.md) — the command line's tree contract (`Program.Run`) and the `schema` command the schemas are read through.
- [Samples](../../samples/Samples/API.md) — the scenarios' tree contract (`Program.Run`, `Scenarios`).
- [Harness](../Harness/API.md) — `JsonSchema` for L5, `RunPropertyCut` for L3.
- [Fixtures](../Fixtures/API.md) — `RepositoryPaths`, paths from the repository root.

Outside the tree: xunit, as every tests node.

## Constraints

Inherited from the root ([BOOT.md](../../BOOT.md)). In addition:

- The project is `tests/Docs.Tests/APThermo.Docs.Tests.csproj`, an xunit test assembly
  in the solution and in the fast suite; namespace `APThermo.Docs.Tests`. It receives
  `InternalsVisibleTo` grants from `APThermo.Cli` and `APThermo.Samples` alone (root
  `BOOT.md`, Delivery: Tree contracts: a grant follows the declared dependency).
- The approved files live in this node's `approved/` directory: `samples/` for L2,
  `cli/` for L3. `*.actual.txt` beside them is git-ignored (the root `.gitignore`).
- The guide's command-line examples pin `--accelerator cpu`, so L3 approves the same
  bytes on a CUDA machine and on CI; an example that needs another accelerator is not
  a documented example until it is made reproducible.

## Shape exceptions

| Where | Rule | Measured | Reason |
|---|---|---|---|
| `SchemaValidationTests.Every_states_file_validates_against_the_states_schema` | nesting | 4 | the states walker nests a per-line parse and validate inside the file loop and its `.jsonl` branch; that is the walk, not accidental depth |

## Acceptance criteria

- [ ] L1–L6 are green over the committed guide, samples and documents
      (`dotnet test tests/Docs.Tests`).
- [ ] Every level has been seen red once against a deliberate drift (a mutated snippet,
      an edited approved output, a command-line example with a wrong byte, a broken
      link, a sample document that violates its schema, a guide page missing a heading)
      and reverted; nothing of the mutations committed.

## Taboos

- No copy of a schema, a sample source or a guide block: each is read from where it
  lives.
- No assertion on physics values: this node compares bytes to approved files; the
  tolerance work is the fixtures' and the per-node tests'.
- No write to any document it checks: `README.md`, `docs/`, `samples/` are read-only
  from here; the only files this node writes are its own `*.actual.txt`.
- No network.

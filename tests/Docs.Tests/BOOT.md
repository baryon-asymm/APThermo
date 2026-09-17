# BOOT.md — Docs.Tests

## Purpose

The definition of what "the documentation is ready" means: the guide, the samples and
the schemas are one system, and this node checks them against each other by machine
(root `BOOT.md`, Delivery: Documentation). It runs the command line and the scenarios
in-process through their tree contracts, so a drift between a sentence, a snippet and
an output is a failing test, not a review opinion.

| Level | What it checks | Against what (source of truth) | State |
|---|---|---|---|
| L1 snippets | every `<!-- snippet: name -->`-marked C# block of `README.md`, `docs/guide/*.md` and the package READMEs under `docs/nuget/` equals the samples node's region of that name byte for byte (common indentation stripped, line endings normalized to LF); fails when no marker exists anywhere in the guide | the samples node's `// snippet-start: name` / `// snippet-end` regions | ✅ |
| L2 sample outputs | every scenario of the samples node, run in-process, prints its approved output | `approved/samples/<class>.approved.txt`, whole file | ✅ |
| L3 CLI examples | every `apthermo …` invocation in a fenced block of `README.md`, `docs/guide/*.md` or `docs/nuget/*.md` is either a runnable example (a single line, `--accelerator cpu`, exactly one committed `samples/cli/` input, no `--output`/`--format`) checked against its approved document, or a declared synopsis (every other form, counted but not run); fails when no invocation exists, or when a runnable example and an approved file do not name each other | `approved/cli/<key>.approved.json`, the key the input file's name, the top-level `run` property cut | ✅ |
| L4 links | every relative link of `README.md`, `llms.txt` and the markdown under `docs/` (`docs/protocol/templates` excluded — its placeholder links are deliberate; every other document under `docs/protocol` is checked) resolves to an existing file; the package READMEs under `docs/nuget/` may carry no relative link at all, since nuget.org renders them outside the repository | the repository tree | ✅ |
| L5 schemas | every file under `samples/cli/`, walked recursively, validates against the schema its top-level directory declares (`problems` → `input`, `states` → `states`; any other top-level directory fails loudly instead of being skipped); a `.jsonl` file is checked record by record; the schemas are read through `apthermo schema <name>` in-process, so no copy lives here | the command line's embedded schemas (root `BOOT.md`, Delivery: Documentation) | ✅ |
| L6 guide shape | every page of `docs/guide/*.md` carries the shared headings in order, once each, read outside fenced code blocks: `## Purpose`, `## When to use`, `## Steps`, `## Errors`, `## See also` | the root `BOOT.md`, Delivery: Documentation | ✅ |

⚠ 2026-09-17: the table above, and every level's test, were rewritten after the audit
of that day (`SCRATCH/audit/review-docs.md`) found the previous forms could pass
vacuously (an early return on an empty set: D1), checked marked blocks only while two
guide C# blocks went unmarked (D2, together with the root's W1), ran one of the
guide's several `apthermo` invocations and searched `docs/guide` alone (D3), excluded
all of `docs/protocol` instead of `docs/protocol/templates` (D5), walked two directories
of `samples/cli` non-recursively (D7), and were never shown red (D4). Each level now
fails loudly on an empty population, and each was shown red once against a deliberate
drift and reverted — see `## Acceptance criteria` below for the dated list. L1's rule
also changed with the root's restored marker syntax (`BOOT.md`, Delivery, the ⚠ of
2026-09-17): a region may be quoted on several pages, so this table no longer requires
"exactly once"; it does not require every scenario to be quoted either, since the guide
pages were not rewritten in this task (the C# blocks were the only prose this task's
coder could touch) — `samples/Samples/API.md` records which scenarios are quoted today
and which are proven by L2 alone.

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
- **A level fails loudly on an empty population**: an early return that turns "nothing
  to check" into a pass is a standing loophole (AGENTS.md §13); every level of the
  table above asserts its own population is non-empty before checking anything in it.
- **An example without a committed input is a failure**: a runnable L3 example whose
  input document is not under `samples/cli/` cannot be found, so `RunnableKeyOf`
  returns null and the invocation is counted as a declared synopsis rather than
  silently skipped; a synopsis with a committed approved file that no runnable example
  claims is an orphan, and fails too (`CheckApprovedFilesMatch`).

## Dependencies

- [Cli](../../src/Cli/API.md) — the command line's tree contract (`Program.Run`) and the `schema` command the schemas are read through.
- [Samples](../../samples/Samples/API.md) — the scenarios' tree contract (`Program.Run`, `Scenarios`, `ClassNameOf`).
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
  `cli/` for L3. `*.actual.txt` and `*.actual.json` beside them are git-ignored (the
  root `.gitignore`).
- The guide's command-line examples pin `--accelerator cpu`, so L3 approves the same
  bytes on a CUDA machine and on CI; an example that needs another accelerator is not
  a documented example until it is made reproducible.
- **L3's choice of "how a page shows its approved output"** (the root `BOOT.md`,
  Delivery: Documentation, asks the docs tests node to record this): a page shows only
  the command, never the delivered document inline; the whole delivered document
  (`run` cut) is what `approved/cli/` holds. A fenced block with more than one
  non-empty line is therefore always a declared synopsis, never a runnable example —
  there is no guide content today that shows an example's output next to its command,
  so a "declared excerpt" comparison was not built without anything to prove it red
  against (AGENTS.md §13). A later docs pass that wants to show output on a page adds
  that comparison then, with a page that needs it.

## Shape exceptions

None. The previous row (`Every_states_file_validates_against_the_states_schema`,
nesting 4) is gone: `SchemaValidationTests` now extracts the JSON Lines loop into
`CheckJsonLines`/`CheckJsonLine`, which removes the exception (D11,
`SCRATCH/audit/review-docs.md`).

## Acceptance criteria

- [x] 2026-09-17 — L1–L6 are green over the committed guide, samples and documents:
      `dotnet test tests/Docs.Tests`, 18 of 18 passed.
- [x] 2026-09-17 — Every level has been seen red once against a deliberate drift and
      reverted, nothing of the mutations committed:
      - L1: a byte changed in `docs/guide/rocket.md`'s `RocketSolve` block —
        `SnippetTests.Every_marked_csharp_block_equals_its_sample_region` red.
      - L2: `approved/samples/QuickStart.approved.txt` edited — `SampleOutputTests`
        red for `quick-start`.
      - L3: a byte changed in `approved/cli/rocket.approved.json` —
        `CommandLineExampleTests` red; separately, `docs/guide` and `README.md`
        renamed away made the committed `rocket.approved.json` an orphan of no
        runnable example — the same test red with a different message ("approved/cli
        file(s) with no matching runnable example: rocket"), proving the orphan guard
        (D1) non-degenerate.
      - L4: a link target in `README.md` renamed to a non-existent file —
        `LinkTests.Every_relative_link_resolves_to_a_file` red; a relative link added
        to `docs/nuget/APThermo.md` — `LinkTests.No_package_readme_carries_a_relative_link`
        red.
      - L5: an unknown field added to `samples/cli/problems/rocket.json` —
        `SchemaValidationTests` red.
      - L6: `## Errors` renamed to `## Errors (MUTATED)` in `docs/guide/rocket.md` —
        `GuideShapeTests` red.
      - Vacuous-population guards: `docs/guide` and `README.md` renamed away —
        `GuideShapeTests` ("no guide page was found"), `LinkTests` (a link to the now-
        missing `docs/guide/rocket.md` failed to resolve) both red rather than
        vacuously green.

## Taboos

- No copy of a schema, a sample source or a guide block: each is read from where it
  lives.
- No assertion on physics values: this node compares bytes to approved files; the
  tolerance work is the fixtures' and the per-node tests'.
- No write to any document it checks: `README.md`, `docs/`, `samples/` are read-only
  from here; the only files this node writes are its own `*.actual.txt`/`*.actual.json`.
- No network.
- No early return that turns an empty population into a pass.

# BOOT.md — Docs.Tests

## Purpose

The definition of what "the documentation is ready" means: the guide, the samples and
the schemas are one system, and this node checks them against each other by machine
(root `BOOT.md`, Delivery: Documentation). It runs the command line and the scenarios
in-process through their tree contracts, so a drift between a sentence, a snippet and
an output is a failing test, not a review opinion.

| Level | What it checks | Against what (source of truth) | State |
|---|---|---|---|
| L1 snippets | every C# fence (info `csharp`, `cs` or `c#`, any case) of `README.md`, `docs/guide/*.md` and the package READMEs under `docs/nuget/` is preceded by a `<!-- snippet: name -->` marker (`Every_csharp_fence_is_preceded_by_a_snippet_marker`); every marked block equals the samples node's region of that name byte for byte (common indentation stripped, line endings normalized to LF, `Every_marked_csharp_block_equals_its_sample_region`); the region names the samples node's source declares are exactly, for every scenario, its class name and that name plus `Usings` (`Region_names_are_exactly_each_scenario_class_name_or_that_name_plus_Usings`) | the samples node's `// snippet-start: name` / `// snippet-end` regions, and its `Program.Scenarios`/`ClassNameOf` | ✅ |
| L2 sample outputs | every scenario of the samples node, run in-process, prints its approved output | `approved/samples/<class>.approved.txt`, whole file | ✅ |
| L3 CLI examples | every `apthermo …` invocation of `README.md`, `docs/guide/*.md` or `docs/nuget/*.md` — any line of a fence, or an inline code span — is classified: a `rocket`/`equilibrium`/`states` verb with at least one more token must be a runnable example (a single line, `--accelerator cpu`, exactly one committed `samples/cli/` input, no `--output`/`--format`) checked against its approved document, or the test fails naming the reason; that verb alone, with nothing after it, is a bare mention and is skipped; a verb from the declared synopsis list (`species`, `devices`, `schema`, `--help`, `--version`) is counted, except the two deterministic forms `apthermo species --find H2O` and `apthermo schema input`, which are run and approved like a runnable example; any other verb fails as an unknown command. Fails when no invocation exists, when a runnable example (or deterministic synopsis) and an approved file do not name each other, or when a fence carries an `apthermo …` invocation together with any other non-empty line (not a supported form: a placeholder synopsis belongs in prose, never a fence). A second fact ties a JSON document shown next to prose to its `samples/cli/` file through a `<!-- cli-document: path -->` marker: the shown block must equal that file and validates against its schema (`Every_marked_cli_document_equals_its_samples_cli_file_and_validates_against_its_schema`) | `approved/cli/<key>.approved.json` (the key the input file's name, or the deterministic synopsis's own name; the top-level `run` property cut, except `schema input`'s document, which carries no `run` property to cut), and `samples/cli/<path>` for a marked JSON document | ✅ |
| L4 links | every relative link of `README.md`, `llms.txt` and the markdown under `docs/` (`docs/protocol/templates` excluded — its placeholder links are deliberate; every other document under `docs/protocol` is checked), in every form (inline, reference-style, HTML `href`), resolves to an existing file; a `#anchor` fragment, bare or on a file link, names an existing heading of its target by GitHub's own slug; the package READMEs under `docs/nuget/` may carry no relative link at all, since nuget.org renders them outside the repository, and no document (package READMEs included) may carry the placeholder `OWNER/REPO`. `README.md` and `llms.txt` are asserted to exist | the repository tree, and each target document's own headings | ✅ |
| L5 schemas | every file under `samples/cli/`, walked recursively, validates against the schema its top-level directory declares (`problems` → `input`, `states` → `states`; any other top-level directory fails loudly instead of being skipped); a `.jsonl` file is checked record by record; the schemas are read through `apthermo schema <name>` in-process, so no copy lives here | the command line's embedded schemas (root `BOOT.md`, Delivery: Documentation) | ✅ |
| L6 guide shape | every page of `docs/guide/*.md` carries the shared headings in order, once each, read outside fenced code blocks: `## Purpose`, `## When to use`, `## Steps`, `## Errors`, `## See also` | the root `BOOT.md`, Delivery: Documentation | ✅ |
| L7 status table | the status table of `docs/guide/troubleshooting.md` (the one headed `\| Status \| Meaning \|`) lists exactly the names of `CaseStatus`, in its declared order | `Thermo`'s `CaseStatus` (`Enum.GetNames<CaseStatus>()`) | ✅ |

⚠ 2026-09-17: the table above, and every level's test, were rewritten after the audit
of that day (closed in `3c0d271`) found the previous forms could pass
vacuously (an early return on an empty set: D1), checked marked blocks only while two
guide C# blocks went unmarked (D2, together with the root's W1), ran one of the
guide's several `apthermo` invocations and searched `docs/guide` alone (D3), excluded
all of `docs/protocol` instead of `docs/protocol/templates` (D5), walked two directories
of `samples/cli` non-recursively (D7), and were never shown red (D4). Each level now
fails loudly on an empty population, and each was shown red once against a deliberate
drift and reverted — see `## Acceptance criteria` below for the dated list. L1's rule
also changed with the root's restored marker syntax (`BOOT.md`, Delivery, the ⚠ of
2026-09-17): a region may be quoted on several pages, so this table no longer requires
"exactly once".

⚠ 2026-09-17 (second audit, closed in `657410d`): the paragraph above went
on to say that L1 "does not require every scenario to be quoted either, since the guide
pages were not rewritten in this task … `samples/Samples/API.md` records which
scenarios are quoted today and which are proven by L2 alone". That was true of the
commit the first audit reviewed; the guide rewrite that followed it (merged as
`e229d2e`, before this second audit) quotes every one of the twelve scenarios at least
once, as `samples/Samples/API.md` itself now records. The sentence was found stale by
this task's coder while closing the second audit's B1 (below) and is removed rather
than silently corrected in place, per AGENTS.md §8. L1 was widened the same day: a C#
fence with no marker at all now fails
(`Every_csharp_fence_is_preceded_by_a_snippet_marker`, B1), and the region names
`GuideDocuments.SnippetRegions()` finds are now tied exactly to the samples node's
scenario list (`Region_names_are_exactly_each_scenario_class_name_or_that_name_plus_Usings`,
m10), closing a region defined in a method no scenario calls (X7) reading as quotable.
L3 was rewritten in full: it used to read only the first non-empty line of a fence and
never an inline code span (X4, X8), and it downgraded a `rocket`/`equilibrium`/`states`
invocation with a missing `--accelerator cpu` or a misspelled input path to a silently
uninspected "declared synopsis" instead of failing (X2, X3); a JSON document shown next
to prose was never checked at all (X9). L4 gained reference-style links, HTML `href`s,
anchor-fragment resolution and the `OWNER/REPO` placeholder rejection (M5, m6), and an
explicit assertion that `README.md` and `llms.txt` exist (m1, closing X6). L7 is new
(M4). `## Acceptance criteria` below carries the dated, red-then-reverted evidence for
every one of these.

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
- **A `rocket`/`equilibrium`/`states` invocation with at least one more token must
  run**: unlike the other declared verbs, these three describe commands that always
  take an input document, so a missing `--accelerator cpu`, an input not committed
  under `samples/cli/`, or delivery through `--output`/`--format` fails the test
  naming the reason (`TryRunnableKey`) instead of being silently downgraded to a
  declared synopsis (the second audit's X2, X3). The bare verb alone (`` `apthermo
  rocket` `` in prose, naming the command with nothing to run) is not an invocation
  and is skipped. A synopsis with a committed approved file that no runnable example
  or deterministic synopsis claims is an orphan, and fails too
  (`CheckApprovedFilesMatch`).

## Dependencies

- [Cli](../../src/Cli/API.md) — the command line's tree contract (`Program.Run`) and the `schema` command the schemas are read through.
- [Samples](../../samples/Samples/API.md) — the scenarios' tree contract (`Program.Run`, `Scenarios`, `ClassNameOf`).
- [Harness](../Harness/API.md) — `JsonSchema` for L5 and L3's cli-document check, `RunPropertyCut` for L3.
- [Fixtures](../Fixtures/API.md) — `RepositoryPaths`, paths from the repository root.
- [Thermo](../../src/Thermo/API.md) — `CaseStatus`, the names L7 compares the status table against.

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
  (`run` cut, except `schema input`'s, which carries no `run` property) is what
  `approved/cli/` holds. A fence carrying an `apthermo …` invocation together with any
  other non-empty line is therefore not a supported form and fails — there is no
  guide content today that shows an example's output next to its command, so a
  "declared excerpt" comparison was not built without anything to prove it red against
  (AGENTS.md §13). A later docs pass that wants to show output on a page adds that
  comparison then, with a page that needs it. A JSON document shown next to prose
  instead (a package README's example input, for instance) is a different form,
  checked by its own `<!-- cli-document: path -->` marker against the `samples/cli/`
  file it names, not against `approved/cli/`.

## Shape exceptions

None. The previous row (`Every_states_file_validates_against_the_states_schema`,
nesting 4) is gone: `SchemaValidationTests` now extracts the JSON Lines loop into
`CheckJsonLines`/`CheckJsonLine`, which removes the exception (finding D11,
closed in `3c0d271`).

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
- [x] 2026-09-17 (guide rewrite) — L3's last silent skip is closed:
      `InvocationsOf` used to `continue` past any fenced block of more than one
      non-empty line without ever looking at it, so a multi-line block that opened
      with an `apthermo …` invocation (a command synopsis with its output pasted
      below it, for instance) passed unseen. It now fails, naming the page and line,
      when such a block's first line is an invocation; a placeholder synopsis
      belongs in prose or a link to `src/Cli/API.md` instead (root `BOOT.md`,
      Delivery: Documentation, item 3 of the guide-rewrite task). Shown red by
      adding a second line to `docs/guide/cli.md`'s `apthermo devices` fence
      (`CommandLineExampleTests` failing with
      "docs/guide/cli.md:57: a fenced block with more than one line must not open
      with an 'apthermo …' invocation … apthermo devices"), then reverted; nothing
      of the mutation committed. `dotnet test tests/Docs.Tests`, 18 of 18 passed
      afterwards, over two new runnable examples (`apthermo equilibrium
      samples/cli/problems/equilibrium.json --accelerator cpu`, `apthermo states
      samples/cli/states/tp-states.json --accelerator cpu`) and four new guide
      pages (`getting-started.md`, `gpu.md`, `data.md`, `troubleshooting.md`).

      ⚠ 2026-09-17 (second audit): the equilibrium example above was recorded without
      `--accelerator cpu`, unlike the states example next to it and unlike the actual
      page (`docs/guide/cli.md:37`, which always carried it). Found stale by the second
      audit's m8 while closing this task; corrected in place, a
      typo of form rather than a claim that changed meaning (AGENTS.md §8).
- [x] 2026-09-17 (second audit, closed in `657410d`) — every escape the
      second audit found in this node's checks is closed, and every new or changed
      check was shown red once against a deliberate drift and reverted, nothing of the
      mutations committed: `dotnet test tests/Docs.Tests`, 24 of 24 passed (18 before,
      plus L1's two new facts, L3's `cli-document` fact, L4's two new facts, and L7).
      - B1 (blocker): a C# fence with its marker removed now fails L1 instead of going
        unchecked — the `QuickStart` marker deleted from `README.md`,
        `SnippetTests.Every_csharp_fence_is_preceded_by_a_snippet_marker` red
        ("README.md:34: this C# fence is not preceded by a '<!-- snippet: name -->'
        marker"), reverted. "Marked" is removed from this document (the table above)
        and from `API.md`'s first row.
      - D14: a marker followed by prose instead of a fence now fails with a message
        instead of throwing — prose inserted between `README.md`'s `QuickStart`
        marker and its fence,
        `SnippetTests.Every_marked_csharp_block_equals_its_sample_region` red
        ("README.md:34: the snippet marker is not followed by a fenced code block",
        no `NullReferenceException`), reverted. `GuideDocuments.TryFencedBlockAt`
        carries the fix and is shared by L1 and L3's new `cli-document` fact, so
        neither can repeat D14's mistake.
      - m10: the region-name set is now tied to the samples node's scenario list — a
        `QuickStartOrphan` region added to `samples/Samples/QuickStart.cs` (outside
        the scope of this task, mutated and reverted only to prove the check),
        `SnippetTests.Region_names_are_exactly_each_scenario_class_name_or_that_name_plus_Usings`
        red ("region(s) named by no scenario's class name (or that name plus Usings):
        QuickStartOrphan"), reverted.
      - M1/X2: a runnable verb with a misspelled `--accelerator` no longer reads as a
        synopsis — `--accelerator cpu` misspelled `--acelerator cpu` in `README.md`'s
        `rocket` example, `CommandLineExampleTests` red ("is not a runnable example:
        does not pin '--accelerator cpu'"), reverted.
      - M1/X3: a misspelled input path no longer reads as a synopsis —
        `rocket.json` misspelled `rocet.json` in the same line, the same test red
        ("names no document committed under samples/cli/"), reverted.
      - M1/X4: an invocation on any line but the first of a fence is no longer
        silently unread — a comment line added before `README.md`'s `apthermo rocket`
        fence, the same test red ("a fenced block carrying an 'apthermo …' invocation
        must hold no other non-empty line … 1 invocation line(s) among 2 non-empty
        line(s)"), reverted.
      - M1/X8, X9: an inline `apthermo rocket problem.json` span with no
        `--accelerator cpu` and no committed input (`docs/nuget/APThermo.Cli.md:31`),
        and its JSON example never checked against `samples/cli/`
        (`docs/nuget/APThermo.Cli.md:18-29`) — both were live escapes on the second
        audit's HEAD, not a synthetic mutation: `CommandLineExampleTests` red on the
        unmodified document ("is not a runnable example: does not pin '--accelerator
        cpu'"), then the `Every_marked_cli_document_equals_its_samples_cli_file_and_validates_against_its_schema`
        fact itself red on an empty population before any marker existed ("no
        '<!-- cli-document: path -->' marker was found"). Fixed by quoting
        `samples/cli/problems/rocket.json` verbatim under a `cli-document` marker and
        making the invocation runnable (`docs/nuget/APThermo.Cli.md`); both facts
        green afterward.
      - M4/L7: the status table forgetting a `CaseStatus` name now fails — the
        `NoTransportData` row deleted from `docs/guide/troubleshooting.md`,
        `StatusTableTests.The_status_table_lists_exactly_the_names_of_CaseStatus` red
        (listing seven names against `CaseStatus`'s eight), reverted. The three
        missing rows (`ThroatNotFound`, `AreaRatioInvalid`, `NoTransportData`) and the
        completed `InvalidInput` meaning are added to the table, sourced from the
        Errors sections of `Thermo`, `Performance`, `Equilibrium` and `Transport`'s
        `API.md`, not from memory.
      - M5: the `OWNER/REPO` placeholder now fails L4 — the line "See
        https://github.com/OWNER/REPO for the repository." appended to `README.md`,
        `LinkTests.No_document_carries_the_OWNER_REPO_placeholder` red, reverted. The
        two real placeholder links (`docs/nuget/APThermo.md:76`,
        `docs/nuget/APThermo.Cli.md:53` as the second audit found them) are removed
        until the repository exists publicly; the task's own report names where to
        restore them once it does.
      - m1: `README.md` or `llms.txt` missing now fails L4 instead of shrinking the
        checked set — `README.md` moved aside, `LinkTests.The_root_guide_entry_points_exist`
        red ("README.md does not exist at the repository root"), restored.
      - m6, three escapes in one fact: a reference-style link
        (`[text][id]` / `[id]: target`) to a non-existent file —
        `LinkTests.Every_relative_link_resolves_to_a_file` red ("the link
        'docs/roadmap-does-not-exist.md' does not resolve to an existing file");
        an HTML `<a href="…">` to the same non-existent file — the same test red
        with the same message; a `#no-such-heading` anchor on a real link
        (`docs/guide/cli.md#no-such-heading`) — the same test red ("the anchor
        '#no-such-heading' … names no heading; known anchors: command-line, purpose,
        when-to-use, steps, errors, see-also"). Each mutated alone in `README.md` and
        reverted.

## Taboos

- No copy of a schema, a sample source or a guide block: each is read from where it
  lives.
- No assertion on physics values: this node compares bytes to approved files; the
  tolerance work is the fixtures' and the per-node tests'.
- No write to any document it checks: `README.md`, `docs/`, `samples/` are read-only
  from here; the only files this node writes are its own `*.actual.txt`/`*.actual.json`.
- No network.
- No early return that turns an empty population into a pass.

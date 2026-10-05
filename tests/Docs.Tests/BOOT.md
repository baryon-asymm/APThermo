# BOOT.md — Docs.Tests

## Purpose

The definition of what "the documentation is ready" means: the guide, the samples and
the schemas are one system, and this node checks them against each other by machine
(root `BOOT.md`, Delivery: Documentation). It runs the command line and the scenarios
in-process through their tree contracts, and the command line once more as a process in
the end-to-end set, so a drift between a sentence, a snippet and an output is a failing
test, not a review opinion.

| Level | What it checks | Against what (source of truth) | State |
|---|---|---|---|
| L1 snippets | every C# fence (info `csharp`, `cs` or `c#`, any case, the first word of the info string, N1) of `README.md`, `docs/guide/*.md` and the package READMEs under `docs/nuget/` is preceded by a `<!-- snippet: name -->` marker (`EveryCsharpFenceIsPrecededByASnippetMarker`); every marked block equals the samples node's region of that name byte for byte (common indentation stripped, line endings normalized to LF, `EveryMarkedCsharpBlockEqualsItsSampleRegion`); the region names the samples node's source declares are exactly, for every scenario, its class name and that name plus `Usings` (`RegionNamesAreExactlyEachScenarioClassNameOrThatNamePlusUsings`); and each scenario's body region lies, by its own line span, inside the block body of that scenario's `Run` method, parsed with Roslyn rather than merely named by it (`EachScenarioBodyRegionLiesInsideItsClassRunMethod`, ma2) | the samples node's `// snippet-start: name` / `// snippet-end` regions, its `Program.Scenarios`/`ClassNameOf`, and the Roslyn syntax tree of each scenario's own source file | ✅ |
| L1b scenario table | `samples/Samples/API.md`'s "## Scenarios" table lists exactly `Program.Scenarios`, in order, with the class column exactly `ClassNameOf` of each (`ScenarioTableTests`, ma1): the table is what `ci.yml`'s packaged-library step reads instead of a second, typed-in list | `APThermo.Samples.Program.Scenarios`/`ClassNameOf` | ✅ |
| L2 sample outputs | every scenario of the samples node, run in-process, prints its approved output | `approved/samples/<class>.approved.txt`, whole file | ✅ |
| L3 CLI examples | every `apthermo …` invocation of `README.md`, `docs/guide/*.md` or `docs/nuget/*.md` — any line of a fence (a leading shell prompt `$ `, `> `, `PS> ` or `PS C:\…> ` stripped first, N4), or an inline code span (the same prompt stripped first, minor 2 of the fourth documentation review) — is classified, each carrying whether it came from a fence or from prose: a `rocket`/`equilibrium`/`states` verb with at least one more token must be a runnable example (a single line, `--accelerator cpu`, exactly one committed `samples/cli/` input, no `--output`/`--format`, every token checked against its own declared synopsis, read from `src/Cli/API.md`'s "## Command line" block rather than a second, typed-in list — `CommandSynopses.FromCliApi`, N8, minor 6) checked against its approved document, or the test fails naming the reason; that verb alone, with nothing after it, is a bare mention and is skipped when it is an inline span (naming the command in prose); the same bare verb alone in its own fence is a command, not a mention — a fence always demonstrates something to run — and is checked as a full invocation instead (minor 2); `devices` and `--version` are counted but never run, since their output depends on the machine or the release (root `BOOT.md`, Delivery: Documentation); every other declared verb — `species`, `schema`, `--help` — is a full invocation whenever it carries at least one more token, or always for `--help`, and is run and approved the same way (ma10); any other verb fails as an unknown command, and any token a verb's own synopsis does not declare fails too (N8). Fails when no invocation exists, when a runnable example and an approved file do not name each other, when a fence carries an `apthermo …` invocation together with any other non-empty line (not a supported form: a placeholder synopsis belongs in prose, never a fence), when a fenced block is opened but never closed before the document ends (N1b), or when a fence line or an inline span names `apthermo` but is not recognised as an invocation even after a known prompt is stripped (N4, both halves). A second fact ties a JSON document shown next to prose to its `samples/cli/` file through a `<!-- cli-document: path -->` marker: the shown block must equal that file and validates against its schema (`EveryMarkedCliDocumentEqualsItsSamplesCliFileAndValidatesAgainstItsSchema`); a third requires that marker on every JSON fence (info `json`, first word, any case), not only a marked one (`EveryJsonFenceIsPrecededByACliDocumentMarker`, MA1) — both in `CliDocumentTests.cs`, split from `CommandLineExampleTests.cs` for the code-shape limit. A runnable example's JSON document is checked twice (2026-09-29, root `BOOT.md`, Delivery: Documentation, "The command-line examples' approved documents follow the root's platform rule"): field by field (the Harness node's `JsonFieldComparison.Mismatch` — the same members in the same order, every string and boolean equal, every number within 1e-9 relative) in `EveryCommandLineInvocationIsACheckedExampleOrADeclaredSynopsis` itself, on every runner; and byte for byte in `EveryCommandLineExamplesApprovedDocumentMatchesItsPlatformExactly`, tagged `Category=BitSnapshot`, which every runner runs, the hosted ones included (2026-10-05); both run in-process on the warm solvers `CliInvocation` shares, and the same invocations run again as fresh processes, the shipped cold path, in the end-to-end set (2026-10-03, `Category=EndToEnd`): field by field on every runner (`EveryCommandLineInvocationRunAsAProcessMatchesItsApprovedDocumentFieldByField`) and exactly on every runner (`...Exactly`, also `Category=BitSnapshot`), the process's standard output read as bytes and decoded as UTF-8 | `approved/cli/<key>.approved.json` on every platform (2026-10-05, one record; `--help`'s plain-text usage is a `.txt`) — the key the input file's name plus a suffix for any option beyond the mandatory `--accelerator cpu` (D10: two invocations of one input with different options never collide on one key), or the verb plus its own tokens for `species`/`schema`/`--help` — and `samples/cli/<path>` for a marked JSON document, and `src/Cli/API.md`'s "## Command line" block for every synopsis | ✅ |
| L0 fence tags | every fence of `README.md`, `docs/guide/*.md` and the package READMEs under `docs/nuget/` carries a first-word tag from the allow-list `csharp`/`cs`/`c#`, `json` or `console` (`EveryFenceCarriesATagFromTheAllowList`, minor 3 of the fourth documentation review): before this task an untagged fence, or one with an unrecognised tag, reached no check at all, since L1 and the `cli-document` facts each look only for their own tag and skip whatever does not carry it — a broken JSON or C# example with no language word would silently escape every level below | `GuideDocuments.IsCSharpFenceInfo`/`IsJsonFenceInfo`/`IsConsoleFenceInfo` | ✅ |
| L4 links | every relative link of `README.md`, `llms.txt` and the markdown under `docs/` (`docs/protocol/templates` excluded — its placeholder links are deliberate; every other document under `docs/protocol` is checked), in every form (inline, reference-style, HTML `href`), resolves to an existing file; an image wrapped in a link (`[![alt](image-url)](target)`, the form a badge takes) resolves by its outer `target`, the link a reader actually follows, not by its inner image source; an absolute link into the repository's own public copy (`https://github.com/baryon-asymm/APThermo/blob/main/…` or `.../tree/main/…`) resolves the same way, against the repository root instead of the document's directory; a `#anchor` fragment, bare or on a file link, names an existing heading of its target by GitHub's own slug; the package READMEs under `docs/nuget/` may carry no relative link at all, since nuget.org renders them outside the repository, but their self-repository links are checked the same way as any other document's; no document (package READMEs included) may carry the placeholder `OWNER/REPO`. `README.md` and `llms.txt` are asserted to exist | the repository tree, and each target document's own headings | ✅ |
| L5 schemas | every file under `samples/cli/`, walked recursively, validates against the schema its top-level directory declares (`problems` → `input`, `states` → `states`; any other top-level directory fails loudly instead of being skipped); a `.jsonl` file is checked record by record; the schemas are read through `apthermo schema <name>` in-process, so no copy lives here | the command line's embedded schemas (root `BOOT.md`, Delivery: Documentation) | ✅ |
| L6 guide shape | every page of `docs/guide/*.md` carries the shared headings in order, once each, read outside fenced code blocks: `## Purpose`, `## When to use`, `## Steps`, `## Errors`, `## See also` | the root `BOOT.md`, Delivery: Documentation | ✅ |
| L7 status table | the status table of `docs/guide/troubleshooting.md` (the one headed `\| Status \| Meaning \|`) lists exactly the names of `CaseStatus`, in its declared order | `Thermo`'s `CaseStatus` (`Enum.GetNames<CaseStatus>()`) | ✅ |

⚠ 2026-09-17: was levels that could pass on an empty population, now each fails on one
and was shown red once → HISTORY.md#levels-rewritten

⚠ 2026-09-17 (second audit): was a stale sentence on L1's scope, now L1, L3 and L4
widened and L7 added → HISTORY.md#second-audit-levels

⚠ 2026-09-17 (third audit): was L1 blind to a region's place and L3 open to six escapes
(N1, N1b, N4, N8, MA1, D10), now closed → HISTORY.md#third-audit-escapes

⚠ 2026-09-17 (fourth review): was a bare verb alone in a fence skipped and no L0, now
both checked (minors 2, 3, 6) → HISTORY.md#fourth-review-minors

⚠ 2026-09-17 (badges): was an `InlineLink` regex that read a badge by its image, now by
its outer target → HISTORY.md#badge-links

⚠ 2026-10-05: was the exact comparison of L3 "only on the reference machine", filtered out of the
hosted runs (`Category!=LongRunning&Category!=BitSnapshot`), now the hosted runs filter
`Category!=LongRunning` only and the exact facts run on every runner against the one record
(root BOOT.md, Platform; the criterion of 2026-09-29 below states the old rule)

## Invariants

- **In-process, a child process only in the end-to-end set**: the command line and the
  scenarios are run through their tree contracts (`Program.RunCached` on the warm solvers
  this node shares, `Program.Run`), so the checks hold on any machine the solution builds on
  and need no installed tool; the two L3 process facts, `Category=EndToEnd`, start the
  command line's built assembly through `Harness.DotnetProcess`.

  ⚠ 2026-10-03: was "In-process, never a child process", `Program.Run` with a fresh engine
  per invocation (the node's run was most of the fast set); now warm solvers in process, and
  the shipped cold path proven once more through the real process, field by field and exactly.
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

- [Cli](../../src/Cli/API.md) — the command line's tree contract (`Program.Run`, `Program.RunCached` with `SolverCache`) and the `schema` command the schemas are read through.
- [Samples](../../samples/Samples/API.md) — the scenarios' tree contract (`Program.Run`, `Scenarios`, `ClassNameOf`).
- [Harness](../Harness/API.md) — `JsonSchema` for L5 and L3's cli-document check, `RunPropertyCut` and `JsonFieldComparison` for L3, and (2026-10-03) `DotnetProcess` for L3's process facts.
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
  bytes regardless of which accelerators a machine has installed; an example that needs
  another accelerator is not a documented example until it is made reproducible.

  ⚠ 2026-09-17: was "and on CI" (never measured), now the CPU, Windows run only; the
  CI matrix waits (`.github/BOOT.md`) → HISTORY.md#ci-claim-unmeasured
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
      Red-once records per level and for the vacuous-population guards (`SnippetTests`,
      `SampleOutputTests`, `CommandLineExampleTests`, `LinkTests`, `SchemaValidationTests`,
      `GuideShapeTests`) → HISTORY.md#red-once-first-pass
- [x] 2026-09-17 (guide rewrite) — L3's last silent skip is closed:
      `InvocationsOf` used to `continue` past any fenced block of more than one
      non-empty line without ever looking at it, so a multi-line block that opened
      with an `apthermo …` invocation (a command synopsis with its output pasted
      below it, for instance) passed unseen. It now fails, naming the page and line,
      when such a block's first line is an invocation; a placeholder synopsis
      belongs in prose or a link to `src/Cli/API.md` instead (root `BOOT.md`,
      Delivery: Documentation, item 3 of the guide-rewrite task). Shown red once, then
      reverted; 18 of 18 passed afterwards → HISTORY.md#guide-rewrite-red-once

      ⚠ 2026-09-17: was an equilibrium example recorded without `--accelerator cpu`, now
      with it → HISTORY.md#guide-rewrite-typo
- [x] 2026-09-17 (second audit, closed in `657410d`) — every escape the
      second audit found in this node's checks is closed, and every new or changed
      check was shown red once against a deliberate drift and reverted, nothing of the
      mutations committed: `dotnet test tests/Docs.Tests`, 24 of 24 passed (18 before,
      plus L1's two new facts, L3's `cli-document` fact, L4's two new facts, and L7).
      - B1 (blocker): a C# fence with its marker removed now fails L1 instead of going
        unchecked — the `QuickStart` marker deleted from `README.md`,
        `SnippetTests.EveryCsharpFenceIsPrecededByASnippetMarker` red
        ("README.md:34: this C# fence is not preceded by a '<!-- snippet: name -->'
        marker"), reverted. "Marked" is removed from this document (the table above)
        and from `API.md`'s first row.
      - D14: a marker followed by prose instead of a fence now fails with a message
        instead of throwing — prose inserted between `README.md`'s `QuickStart`
        marker and its fence,
        `SnippetTests.EveryMarkedCsharpBlockEqualsItsSampleRegion` red
        ("README.md:34: the snippet marker is not followed by a fenced code block",
        no `NullReferenceException`), reverted. `GuideDocuments.TryFencedBlockAt`
        carries the fix and is shared by L1 and L3's new `cli-document` fact, so
        neither can repeat D14's mistake.
      - m10: the region-name set is now tied to the samples node's scenario list — a
        `QuickStartOrphan` region added to `samples/Samples/QuickStart.cs` (outside
        the scope of this task, mutated and reverted only to prove the check),
        `SnippetTests.RegionNamesAreExactlyEachScenarioClassNameOrThatNamePlusUsings`
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
        cpu'"), then the `EveryMarkedCliDocumentEqualsItsSamplesCliFileAndValidatesAgainstItsSchema`
        fact itself red on an empty population before any marker existed ("no
        '<!-- cli-document: path -->' marker was found"). Fixed by quoting
        `samples/cli/problems/rocket.json` verbatim under a `cli-document` marker and
        making the invocation runnable (`docs/nuget/APThermo.Cli.md`); both facts
        green afterward.
      - M4/L7: the status table forgetting a `CaseStatus` name now fails — the
        `NoTransportData` row deleted from `docs/guide/troubleshooting.md`,
        `StatusTableTests.TheStatusTableListsExactlyTheNamesOfCaseStatus` red
        (listing seven names against `CaseStatus`'s eight), reverted. The three
        missing rows (`ThroatNotFound`, `AreaRatioInvalid`, `NoTransportData`) and the
        completed `InvalidInput` meaning are added to the table, sourced from the
        Errors sections of `Thermo`, `Performance`, `Equilibrium` and `Transport`'s
        `API.md`, not from memory.
      - M5: the `OWNER/REPO` placeholder now fails L4 — the line "See
        https://github.com/OWNER/REPO for the repository." appended to `README.md`,
        `LinkTests.NoDocumentCarriesTheOWNERREPOPlaceholder` red, reverted. The
        two real placeholder links (`docs/nuget/APThermo.md:76`,
        `docs/nuget/APThermo.Cli.md:53` as the second audit found them) are removed
        until the repository exists publicly; the root `ACCEPTANCE.md`'s "The packages"
        acceptance criterion now carries that condition ("until it exists they carry no
        guide link", `ce114ea`) and is where the restoration is tracked (ma8, this
        task: the report a coding task writes is not part of the tree and is not a
        place a document may point to, AGENTS.md §11).
      - m1: `README.md` or `llms.txt` missing now fails L4 instead of shrinking the
        checked set — `README.md` moved aside, `LinkTests.TheRootGuideEntryPointsExist`
        red ("README.md does not exist at the repository root"), restored.
      - m6, three escapes in one fact: a reference-style link
        (`[text][id]` / `[id]: target`) to a non-existent file —
        `LinkTests.EveryRelativeLinkResolvesToAFile` red ("the link
        'docs/roadmap-does-not-exist.md' does not resolve to an existing file");
        an HTML `<a href="…">` to the same non-existent file — the same test red
        with the same message; a `#no-such-heading` anchor on a real link
        (`docs/guide/cli.md#no-such-heading`) — the same test red ("the anchor
        '#no-such-heading' … names no heading; known anchors: command-line, purpose,
        when-to-use, steps, errors, see-also"). Each mutated alone in `README.md` and
        reverted.
- [x] 2026-09-17 (third docs audit, this task) — every escape that review found in this
      node is closed, and every new or changed check was shown red once against a
      deliberate drift and reverted, nothing of the mutations committed:
      `dotnet test tests/Docs.Tests`, 27 of 27 passed (24 before, plus L1's new fact,
      the new L1b `ScenarioTableTests`, and L3's new `EveryJsonFenceIsPrecededByACliDocumentMarker`).
      - ma2: a snippet region moved into a decoy method its own scenario's `Run` never
        calls — `QuickStart.cs`'s `QuickStart` region relocated into a new, uncalled
        `Decoy` method, `SnippetTests.EachScenarioBodyRegionLiesInsideItsClassRunMethod`
        red ("the 'QuickStart' snippet region (0-based lines 40-41) does not lie inside
        'QuickStart.Run' (0-based body lines 13-34)"), reverted.
      - N1: an info string with attributes after the language name —
        `` ```csharp title="Program.cs" ``` `` with no marker, added to a scratch guide
        page — `SnippetTests.EveryCsharpFenceIsPrecededByASnippetMarker` red
        (still recognised as a C# fence by its first word, still failing for want of a
        marker), reverted with the scratch page.
      - N1b: a fence opened but never closed before the end of the document — the same
        scratch page's fence closer deleted,
        `SnippetTests.EveryCsharpFenceIsPrecededByASnippetMarker` red ("a fenced
        block opened here has no closing fence before the end of the document"),
        reverted.
      - N4: an unrecognised shell prompt — `` `user@host:~$ apthermo devices` `` in the
        scratch page's fence, `CommandLineExampleTests` red ("this line names 'apthermo'
        but is not recognised as an invocation after stripping a known prompt"),
        reverted; a positive control, `` `PS C:\Users\ed> apthermo devices` ``, passed
        in the same run, proving the strip itself (not just the failure path) works.
      - N8: `apthermo devices --outptu x` (the review's own example) in the scratch
        page, `CommandLineExampleTests` red ("'apthermo devices …' names an option
        '--outptu' its declared synopsis does not have"), reverted.
      - MA1/X9b: a JSON fence with no `cli-document` marker — first proven on the
        scratch page, then reproduced exactly as the review found it: the marker at
        `docs/nuget/APThermo.Cli.md:19` removed,
        `CliDocumentTests.EveryJsonFenceIsPrecededByACliDocumentMarker`
        red naming that file and line, reverted.
      - D10: two invocations of `samples/cli/problems/rocket.json` with different
        options — a second `apthermo rocket … --accelerator cpu --threshold 1e-3` fence
        added to `docs/guide/cli.md` next to the existing one,
        `CommandLineExampleTests` red ("the approved file for 'rocket-threshold-1e-3' is
        missing"), proving the two no longer collide on `rocket.approved.json`,
        reverted.
      - ma1: the scenario table's class column diverging from the code — `QuickStart`
        misspelled `QuickStartWrong` in `samples/Samples/API.md`'s "## Scenarios" table,
        `ScenarioTableTests` red, reverted.
- [x] 2026-09-17 (fourth documentation review, this task) — every minor that review
      found is closed, and every changed or new check was shown red once against a
      deliberate drift and reverted, nothing of the mutations committed:
      `dotnet test tests/Docs.Tests`, 28 of 28 passed (27 before, plus the new L0
      `FenceTagTests`).
      - Minor 1: `docs/guide/getting-started.md`'s "every `apthermo` example is run"
        now names the two exceptions, `devices` and `--version`, next to it.
      - Minor 2, the inline prompt: a scratch page's inline span
        `` `user@host:~$ apthermo devices` `` (an unrecognised prompt),
        `CommandLineExampleTests` red ("this inline span names 'apthermo' but is not
        recognised as an invocation after stripping a known prompt … 'user@host:~$
        apthermo devices'"); the same page's `` `PS> apthermo rocket
        samples/cli/problems/rocket.json --accelerator cpu` `` (a recognised prompt)
        passed in the same run, proving the strip itself works, not only its failure
        path.
      - Minor 2, the fence-vs-prose bare verb: the same scratch page's
        `` ```console\n$ apthermo species\n``` `` (a bare verb alone in its own
        fence), `CommandLineExampleTests` red ("the approved file for 'species' is
        missing"), proving it is now checked as a full invocation rather than
        silently skipped as a mention. All reverted; the scratch page is not
        committed.
      - Minor 3: a scratch page's untagged fence ` ``` ` around a JSON object,
        `FenceTagTests.EveryFenceCarriesATagFromTheAllowList` red ("this
        fence carries no tag from the allow-list (csharp, json, console): the fence
        is untagged"); the same fence tagged ` ```python `, the same test red
        ("found 'python'"); reverted. A regression control on the same page — a
        ` ```console ` fence carrying `` `apthermo devices --outptu x` `` — passed
        `FenceTagTests` (a `console` tag is allow-listed) and then failed
        `CommandLineExampleTests` on the misspelled option (N8), proving a
        `console`-tagged fence still reaches L3 after L0 was added; reverted.
      - Minor 4: `tests/Docs.Tests/BOOT.md`'s own citation of
        `CommandLineExampleTests.EveryJsonFenceIsPrecededByACliDocumentMarker`
        is corrected to `CliDocumentTests.…`, the class the fact has lived in since
        the third audit's move; `API.md`'s L3 row now credits `CliDocumentTests`
        alongside `CommandLineExampleTests`.
      - Minor 5: `src/Execution/Kernels.cs:10`'s and
        `src/Execution/APThermo.Execution.csproj`'s citations of
        `SCRATCH/api-review-report.md` (a path outside the tree, meaningless to a
        reader of the shipped XML documentation or the packed source) are removed;
        `grep -rn "SCRATCH/" src` returns empty, and the build stays warning-free
        (`dotnet build APThermo.sln`, 0 warnings, CS1591 included).
      - Minor 6: `src/Cli/API.md`'s "## Command line" block is corrected — `devices`
        gains `[--format json]` (`CommandTable.cs` already accepted it;
        `CommandLineExampleTests.cs`'s superseded `Synopsis` dictionary and the block
        itself did not declare it) and `equilibrium`'s unparseable `[same options]`
        is spelled out like `rocket`'s —
        with a ⚠ on the corrected lines. `CommandLineExampleTests.cs`'s typed-in
        `Synopsis` dictionary is replaced by `CommandSynopses.FromCliApi()`, which
        parses this block directly (new file, `tests/Docs.Tests/CommandSynopses.cs`).
        Shown red once: a scratch page's `` ```console\n$ apthermo devices --format
        json\n``` `` passed against the corrected block; `--format json` then removed
        from the block's `devices` line, `CommandLineExampleTests` red ("'apthermo
        devices …' names an option '--format' its declared synopsis does not have"),
        the block restored, the same page green again, reverted.
      - Minor 7: `SchemaCommand`'s exit-2 refusal on a missing or unknown name was
        undocumented in `CommandTable.cs`'s own `--help` text ("no name lists the
        embedded names", contradicted by `SchemaCommand.cs:11-14` and
        `src/Cli/API.md`'s Errors table); corrected to "a missing or unknown name
        exits 2, naming the known schemas". `tests/Docs.Tests/approved/cli/help.approved.txt`
        re-approved to the new bytes; `dotnet test tests/Cli.Tests`, 116 of 116
        passed, confirming no other approval of the help text needed a change.
- [x] 2026-09-17 (repository-links task) — L4 gained the self-repository link check:
      the package READMEs of `docs/nuget/` now point their guide references at
      `https://github.com/baryon-asymm/APThermo/blob/main/…` (the root `ACCEPTANCE.md`'s
      "The packages" acceptance criterion, restored: "before the first release the
      package READMEs link the guide on the public repository", now that the
      repository exists), and `LinkTests.CheckDocument` was extended to resolve such
      a link against the repository root instead of skipping it as external, in a new
      fact, `EverySelfRepositoryLinkOfAPackageReadmeResolvesToAFileOrDirectory`.
      Shown red once: `docs/guide/getting-started.md` misspelled
      `docs/guide/gettign-started.md` in `docs/nuget/APThermo.md`'s link, the new fact
      red ("the link '…blob/main/docs/guide/gettign-started.md' does not resolve to an
      existing file or directory of the repository"), reverted; nothing of the
      mutation committed. `dotnet test tests/Docs.Tests`, 29 of 29 passed afterward (28
      before, plus this fact). The `OWNER/REPO` rejection (`NoDocumentCarriesTheOWNERREPOPlaceholder`)
      is unchanged and still runs over both package READMEs.
- [x] 2026-09-17 (actions-and-badges task) — L4's `InlineLink` regex is fixed to read an
      image-wrapped-in-a-link badge by its outer target instead of its inner image
      source (the ⚠ above). Red once through a deliberately broken badge target; 29 of 29
      passed afterward (`dotnet test tests/Docs.Tests`) → HISTORY.md#badge-red-once
- [x] 2026-10-05 — One approved document per example on every platform (owner decision O5, item 13
      of release 0.2.2). The tree's own correctly rounded `Exp`, `Log` and `Pow` make the Windows
      and the Linux (WSL2) documents equal byte for byte: the six `*.linux.approved.json` files
      are deleted, `rocket.approved.json` was regenerated once (the numbers moved with the own
      math), and `EveryCommandLineExamplesApprovedDocumentMatchesItsPlatformExactly` (name kept)
      and the process fact compare the one file on both platforms under `Category=BitSnapshot`.
      The declared deviation of the next criterion (the six Linux copies that were no real
      recording) is lifted by it, and so is its per-platform file picking. The field-by-field
      comparison stays for the hosted runners, whose proof the orchestrator owns.
      Evidence: `CommandLineExampleTests`, Windows and WSL2 Ubuntu 24.04, one commit.
      ⚠ 2026-10-05: was a Windows and a Linux approved file per example, now one file → tests/Harness/HISTORY.md#one-record-2026-10-05
- [x] 2026-09-29 — The command-line examples' approved documents follow the root's
      platform rule (the owner's decision, root `BOOT.md`, Delivery: Documentation).
      - `approved/cli/<key>.approved.json` holds the Windows record and
        `<key>.linux.approved.json` the Linux one, picked by the running platform as
        the harness picks bit snapshots (`SplitApprovedFileName`/`ApprovedPathOf`,
        `OperatingSystem.IsLinux()`); `--help`'s `.txt` record is not split, since its
        plain-text usage has not been shown to differ between the platforms.
      - The exact comparison (`EveryCommandLineExamplesApprovedDocumentMatchesItsPlatformExactly`)
        carries `Category=BitSnapshot`, so it runs on the reference machine (locally
        and on the self-hosted release runners) and not on the hosted CI runners.
        ⚠ 2026-10-05: was "not on the hosted CI runners" (`Category!=LongRunning&Category!=BitSnapshot`),
        now the hosted runs filter `Category!=LongRunning` only and it runs on every runner.
      - Every runner, the hosted ones included, compares the same delivered document
        with the platform's approved file field by field
        (`EveryCommandLineInvocationIsACheckedExampleOrADeclaredSynopsis`, via
        the Harness node's `JsonFieldComparison.Mismatch`): the same members in the same
        order, every string and boolean equal, every number within 1e-9 relative (1e-9
        is four orders above the measured platform difference, about 1e-13, and five
        below the fixtures' tolerance rows). The comparison is one named function, not
        a per-test tolerance.

        ⚠ 2026-09-30: was `JsonFieldComparison` a class of this node, now `tests/Harness`'s
        → HISTORY.md#field-comparison-moved
      - Red once each, on the reference machine (Windows), both reverted by
        `git checkout` afterward and confirmed clean (`git diff --stat`, no residue):
        `tp-states.approved.json`'s `inputs.temperature` moved from `3000.0` to
        `3000.00005` (1.667e-8 relative) turned
        `EveryCommandLineInvocationIsACheckedExampleOrADeclaredSynopsis` red, naming
        the path, the field (`$.cases[0].inputs.temperature`) and the relative
        difference; reverted, the same file's `composition.H` moved one ULP in its
        last decimal digit (`...528607` to `...528608`, small enough to stay inside
        the field tolerance) turned `EveryCommandLineExamplesApprovedDocumentMatchesItsPlatformExactly`
        red ("bytes differ"), the field fact staying green on that mutation. Both
        facts green again once reverted.
      - ⚠ 2026-10-05: was a declared deviation (six Linux copies that were no real recording), now lifted, one record → HISTORY.md#linux-copies-lifted

      Evidence: `dotnet test tests/Docs.Tests/APThermo.Docs.Tests.csproj --no-build`,
      30 of 30 passed (22 m 39 s) on the unmutated tree, Windows, this task's commit.
      The two red-once runs above, each a single filtered fact
      (`--filter "FullyQualifiedName~CommandLineExampleTests.<name>"`), each failing
      with the message quoted above, each reverted and rerun clean.

- [x] 2026-10-03 — L3 through the real process (the test pyramid, root `BOOT.md`, Test time
      budgets): every invocation of the guide runs as a fresh process and equals its
      approved file, field by field (`EveryCommandLineInvocationRunAsAProcessMatchesItsApprovedDocumentFieldByField`,
      every runner) and exactly (`EveryCommandLineInvocationRunAsAProcessMatchesItsApprovedDocumentExactly`,
      `Category=BitSnapshot`), both `Category=EndToEnd`, on the Windows approved files
      unmoved; the in-process facts of L3 run on `CliInvocation`'s warm solvers and their
      approved files are unmoved too. Red once, a process-only change: `Program.Main`
      writing one byte to standard output before running turned both process facts red
      (`'x' is an invalid start of a value`, and `bytes differ`) while the in-process exact
      fact stayed green; the runner's streams swapped turned both red; both reverted. The
      in-process and process runs share one comparison, passed the run as a delegate so that
      no in-process fact reaches a process start (`Protocol.Tests.EndToEndTests` walks the
      call graph). Evidence, Windows, `APTHERMO_NO_CUDA=1`: 30 facts of the fast set in 44 s and
      the two process facts in 44 s, inside whole-solution runs on a machine other agents used.

## Taboos

- No copy of a schema, a sample source or a guide block: each is read from where it
  lives.
- No assertion on physics values: this node compares bytes to approved files; the
  tolerance work is the fixtures' and the per-node tests'.
- No write to any document it checks: `README.md`, `docs/`, `samples/` are read-only
  from here; the only files this node writes are its own `*.actual.txt`/`*.actual.json`.
- No network.
- No early return that turns an empty population into a pass.

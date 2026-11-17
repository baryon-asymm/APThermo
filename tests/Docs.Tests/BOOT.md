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
- **Approved files have two comparison paths**: the bit-snapshot facts check whole
  files exactly; every runner also compares JSON fields with the harness's numeric
  tolerance. Mismatches write `<name>.actual.txt` beside the approved file
  (git-ignored) and name the paths. This node guards what is shown against what runs.

  ⚠ 2026-11-17: the old wording said "No tolerance, no numeric parsing" for approved
  files. `CommandLineExampleTests` also parses JSON and uses `JsonFieldComparison`;
  the independent exact-byte path remains in place.
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

R19: cold-call metadata reads the actual immutable DotnetProcess timeout/cleanup
policy. Keep the original five-minute/five-second values, runner arguments,
exception/result identity, source population and all comparisons. An alternate
policy in an isolated pure build/control must expose frozen duplicated literals;
the corrected consumer follows the runner policy without launching a real CLI.

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

### Cold CLI invocation diagnostics

Before every actual process invocation, record its machine-enumerated ordinal,
source document/line, comparison mode, complete argument vector, assembly/working
paths, committed input path and SHA-256 where present, and effective five-minute
process plus five-second cleanup limits. Record monotonic start/finish elapsed,
exit or exception outcome, using ITestOutputHelper so failed and successful TRX
retain the records. No environment dump or user-secret data is recorded.
Use the same supplied runner and return the same result; on error add diagnostic
context to the original exception Data and rethrow that same exception with its
original stack/raw StandardOutput/StandardError arrays and cancellation token.
A diagnostic failure must not replace the original process failure. Keep both
field and exact cold-process facts, every discovered invocation and duplicate
source location. Do not deduplicate work, increase deadlines, skip examples or
change any approved output. In-process facts must not start processes or require
a process-only clock/logger. Synthetic timeout/normal runner controls prove the
context before invocation, original exception/result identity and raw data.

The cold facts construct a dedicated diagnostic runner per comparison mode; its
ordinal increments for each actual runner call, including duplicates. The normal
in-process delegates accept source context without creating diagnostics, a clock or
a process runner; the class can be constructed without diagnostic output. Before
the cold delegate runs, JSON start output freezes source/argv and hashes committed
sample inputs with SHA-256 (uppercase hex). Finish/failure records carry monotonic
elapsed seconds, exit code or exception type. The same original args reach the
supplied runner and the same normal result is returned.
Process exceptions gain `Docs.ColdCliInvocation` and `Docs.ColdCliOutcome` Data
records; raw arrays and tokens are untouched, and bare throw preserves the stack.
Diagnostic actions execute synchronously in fault-contained Tasks on the default
scheduler, with child attachment denied and observed exceptions. They never run in
the background; a diagnostic sink/data-write fault cannot replace the runner outcome.

⚠ 2026-11-15: Release49 Windows job132779316611 timed out one numerical CLI
invocation after five minutes with no output. RunToExit0 previously called the
runner before attaching where/arguments to an assertion, so that log cannot name
the invocation. The historical command and cause remain unknown; new diagnostics
must identify future invocations rather than reconstruct an unsampled past.

## Acceptance criteria

→ [ACCEPTANCE.md](ACCEPTANCE.md)

## Taboos

- No copy of a schema, a sample source or a guide block: each is read from where it
  lives.
- No assertion on physics values: this node compares bytes to approved files; the
  tolerance work is the fixtures' and the per-node tests'.
- No write to any document it checks: `README.md`, `docs/`, `samples/` are read-only
  from here; the only files this node writes are its own `*.actual.txt`/`*.actual.json`.
- No network.
- No early return that turns an empty population into a pass.

⚠ 2026-11-14: schema-input approval was the former general Equilibrium shape; now
it is the owner-approved A26 TP/HP/SP contract ([Cli](../../src/Cli/API.md)). Delivered
and canonical resource bytes agree; `SCRATCH/audit/schema-approval-proof.json` records
the exact structural delta and hashes. Only this schema approval changed.

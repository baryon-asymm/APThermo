# API.md — Docs.Tests

The node exposes nothing outward. Its contract points upward: what the parent may
consider proven about the documentation, the samples and the schemas.

## What this node guarantees ✅

| Claim | Confirmed by | State |
|---|---|---|
| every C# fence (`csharp`/`cs`/`c#`, any case, its first word) of the guide is preceded by a `<!-- snippet: name -->` marker, every marked block equals its named region of the samples node's source byte for byte, the region names the samples node declares are exactly its scenarios' class names and those names plus `Usings`, and each scenario's body region lies inside that scenario's own `Run` method, proven with Roslyn | L1 (`SnippetTests`) | ✅ |
| `samples/Samples/API.md`'s scenario table names exactly `Program.Scenarios`/`ClassNameOf`, in order | L1b (`ScenarioTableTests`) | ✅ |
| every scenario of the samples node prints its approved output | L2 (`SampleOutputTests`, `approved/samples/`) | ✅ |
| a `rocket`/`equilibrium`/`states` invocation of the guide with at least one more token is a runnable example that delivers its approved document, `run` cut as the command line's tests cut it, or the test fails naming why; a bare verb mention is skipped in prose (an inline span), but the same bare verb alone in its own fence is a command and is checked as a full invocation; `devices` and `--version` are counted but never run, since their output depends on the machine or the release; every other declared verb (`species`, `schema`, `--help`) is a full invocation whenever it carries a further token, or always for `--help`, and is run and approved the same way; an unknown verb, or a token its own declared synopsis does not have, fails, the synopsis read from `src/Cli/API.md`'s own "## Command line" block; a runnable example and an approved file always name each other, keyed by the whole invocation, not only its input file; a fence never carries an `apthermo …` invocation together with any other non-empty line, nor opens one it never closes; a fence line or an inline span naming `apthermo` must be recognised as an invocation once a known shell prompt is stripped; a JSON document marked `<!-- cli-document: path -->` equals its `samples/cli/` file and validates against its schema; every JSON fence (`json`, first word) must carry that marker | L3 (`CommandLineExampleTests`, `CliDocumentTests`, `CommandSynopses`, `approved/cli/`) | ✅ |
| every fence of the guide carries a first-word tag from the allow-list `csharp`/`cs`/`c#`, `json`, `console`; an untagged or unrecognised-tag fence fails naming the page and line | L0 (`FenceTagTests`) | ✅ |
| every relative link of `README.md`, `llms.txt` and `docs/**/*.md` (`docs/protocol/templates` excepted), inline, reference-style or HTML, resolves to a file, and its `#anchor` fragment, when present, names an existing heading of its target; an image wrapped in a link (`[![alt](image-url)](target)`, a badge) resolves by its outer `target`, not its image source; an absolute link into the repository's own public copy (`https://github.com/baryon-asymm/APThermo/blob/main/…` or `.../tree/main/…`) resolves the same way, against the repository root; no package README under `docs/nuget/` carries a relative link, but its self-repository links resolve the same way as any other document's; no document carries the `OWNER/REPO` placeholder; `README.md` and `llms.txt` exist | L4 (`LinkTests`) | ✅ |
| every document under `samples/cli/`, walked recursively, validates against the schema its top-level directory declares, read through `apthermo schema` | L5 (`SchemaValidationTests`) | ✅ |
| every guide page has the shared shape | L6 (`GuideShapeTests`) | ✅ |
| the status table of `docs/guide/troubleshooting.md` lists exactly the names of `CaseStatus`, in order | L7 (`StatusTableTests`) | ✅ |
| every level above fails on an empty population instead of passing vacuously, and was shown red once against a deliberate drift | `tests/Docs.Tests/BOOT.md`, Acceptance criteria | ✅ |

⚠ 2026-09-17: the first row read "every marked C# block equals its sample's snippet
region byte for byte, and every scenario is quoted exactly once", then, after the
first audit, "… and every scenario is quoted exactly once" was dropped with a note
that the guide did not yet quote every scenario. The guide rewrite that followed
(merged as `e229d2e`, before the second audit) quotes every one of the twelve
scenarios at least once, so that second note is now stale; `samples/Samples/API.md`
still records which pages quote which scenario, but the claim here no longer needs to
hedge on it. The second audit (its finding B1, closed in `657410d`) also found L1
checked marked blocks only, leaving an unmarked C# fence unchecked; the claim now
states the stronger rule L1 enforces today (every C# fence must be marked), plus m10's
region-name closure.

⚠ 2026-09-17 (third docs audit, this task): the first row did not yet prove a marked
region physically sits inside the scenario's own `Run` method (ma2, closed by
`Each_scenario_body_region_lies_inside_its_class_Run_method`); a new L1b row is added
for the scenario-table fact `ci.yml`'s packaged-library step depends on (ma1). The
third row's "every other verb is a counted, declared synopsis, except the two
deterministic ones" is corrected: `devices` and `--version` are the only verbs left
uninspected (their output depends on the machine or the release, root `BOOT.md`,
Delivery: Documentation), and `species`/`schema`/`--help` are now full, always-checked
invocations, not two fixed forms (ma10). The row also now states the approval key
folds in an invocation's options, not only its input file (D10), and that every JSON
fence, not only a marked one, must carry the `cli-document` marker (MA1).

⚠ 2026-09-17 (fourth documentation review, this task): the third row let a bare verb
alone in its own fence read as a skipped mention, the same as one in prose, so a fence
that never showed a runnable form of a declared verb passed silently (minor 2); it now
states the fence/prose distinction. The row's synopsis check read from a second, typed-in
list; it now states the synopsis comes from `src/Cli/API.md` itself (minor 6, via the new
`CommandSynopses`). A new row is added for L0, `FenceTagTests` (minor 3): before this
task an untagged or misspelled-tag fence reached no check at all, since L1 and the
`cli-document` facts each look only for their own tag.

⚠ 2026-09-17 (repository-links task): the L4 row read "no package README under
`docs/nuget/` carries a relative link", with no mention of a self-repository link,
because none existed yet. The package READMEs now link their guide pages by an
absolute `https://github.com/baryon-asymm/APThermo/blob/main/…` URL (the root
`BOOT.md`'s "The packages" acceptance criterion), and `LinkTests` was extended to
resolve such a link against the repository root instead of treating it as ordinary
external; the row now states that rule.

⚠ 2026-09-17 (actions-and-badges task): the L4 row said nothing about an image inside a
link, because no document carried one yet. `README.md`'s new badge row does
(`[![CI](image-url)](target)`), and `LinkTests`'s link-target regex used to stop at the
image's own closing `]`, so a badge's outer target — the link a reader follows — was
never checked; a badge's licence link deliberately pointed at a non-existent file and
the full L4 suite stayed green (`tests/Docs.Tests/BOOT.md`, this date). The regex now
treats one level of nested `![...](...)` as part of the outer link's text, so the
badge's own target is what gets read and checked; the row states that rule.

## What the tests rely on

- The tree contracts of `Cli` and `Samples` (`Program.Run`, `Scenarios`,
  `ClassNameOf`), in-process.
- `JsonSchema` and `RunPropertyCut` of the Harness node (moved from `Cli.Tests` in the
  distribution phase, 2026-09-16).
- `RepositoryPaths` of the Fixtures node for paths from the repository root.
- `CaseStatus` of the Thermo node, the names L7 compares the status table against.
- The approved files in this node's `approved/` directory; on a mismatch the actual
  bytes are written beside them as `*.actual.txt`/`*.actual.json` (git-ignored).

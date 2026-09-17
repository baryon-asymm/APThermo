# API.md — Docs.Tests

The node exposes nothing outward. Its contract points upward: what the parent may
consider proven about the documentation, the samples and the schemas.

## What this node guarantees ✅

| Claim | Confirmed by | State |
|---|---|---|
| every C# fence (`csharp`/`cs`/`c#`, any case) of the guide is preceded by a `<!-- snippet: name -->` marker, every marked block equals its named region of the samples node's source byte for byte, and the region names the samples node declares are exactly its scenarios' class names and those names plus `Usings` | L1 (`SnippetTests`) | ✅ |
| every scenario of the samples node prints its approved output | L2 (`SampleOutputTests`, `approved/samples/`) | ✅ |
| a `rocket`/`equilibrium`/`states` invocation of the guide with at least one more token is a runnable example that delivers its approved document, `run` cut as the command line's tests cut it, or the test fails naming why (a bare verb mention is skipped); every other verb is a counted, declared synopsis, except the two deterministic ones, which run and approve like a runnable example; an unknown verb fails; a runnable example (or deterministic synopsis) and an approved file always name each other; a fence never carries an `apthermo …` invocation together with any other non-empty line; a JSON document marked `<!-- cli-document: path -->` equals its `samples/cli/` file and validates against its schema | L3 (`CommandLineExampleTests`, `approved/cli/`) | ✅ |
| every relative link of `README.md`, `llms.txt` and `docs/**/*.md` (`docs/protocol/templates` excepted), inline, reference-style or HTML, resolves to a file, and its `#anchor` fragment, when present, names an existing heading of its target; no package README under `docs/nuget/` carries a relative link; no document carries the `OWNER/REPO` placeholder; `README.md` and `llms.txt` exist | L4 (`LinkTests`) | ✅ |
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
hedge on it. The second audit (`SCRATCH/audit/review-docs-2.md`, B1) also found L1
checked marked blocks only, leaving an unmarked C# fence unchecked; the claim now
states the stronger rule L1 enforces today (every C# fence must be marked), plus m10's
region-name closure.

## What the tests rely on

- The tree contracts of `Cli` and `Samples` (`Program.Run`, `Scenarios`,
  `ClassNameOf`), in-process.
- `JsonSchema` and `RunPropertyCut` of the Harness node (moved from `Cli.Tests` in the
  distribution phase, 2026-09-16).
- `RepositoryPaths` of the Fixtures node for paths from the repository root.
- `CaseStatus` of the Thermo node, the names L7 compares the status table against.
- The approved files in this node's `approved/` directory; on a mismatch the actual
  bytes are written beside them as `*.actual.txt`/`*.actual.json` (git-ignored).

# API.md — Docs.Tests

The node exposes nothing outward. Its contract points upward: what the parent may
consider proven about the documentation, the samples and the schemas.

## What this node guarantees ✅

| Claim | Confirmed by | State |
|---|---|---|
| every `<!-- snippet: name -->`-marked C# block of the guide equals its named region of the samples node's source, byte for byte | L1 (`SnippetTests`) | ✅ |
| every scenario of the samples node prints its approved output | L2 (`SampleOutputTests`, `approved/samples/`) | ✅ |
| every runnable `apthermo` invocation of the guide delivers its approved document, `run` cut as the command line's tests cut it; every other single-line invocation is a counted, declared synopsis; a runnable example and an approved file always name each other; a fenced block of more than one line never opens with an `apthermo …` invocation | L3 (`CommandLineExampleTests`, `approved/cli/`) | ✅ |
| every relative link of `README.md`, `llms.txt` and `docs/**/*.md` (`docs/protocol/templates` excepted) resolves to a file; no package README under `docs/nuget/` carries a relative link | L4 (`LinkTests`) | ✅ |
| every document under `samples/cli/`, walked recursively, validates against the schema its top-level directory declares, read through `apthermo schema` | L5 (`SchemaValidationTests`) | ✅ |
| every guide page has the shared shape | L6 (`GuideShapeTests`) | ✅ |
| every level above fails on an empty population instead of passing vacuously, and was shown red once against a deliberate drift | `tests/Docs.Tests/BOOT.md`, Acceptance criteria | ✅ |

⚠ 2026-09-17: the first row read "every marked C# block equals its sample's snippet
region byte for byte, and every scenario is quoted exactly once". The root's Delivery
bullet was restored the same day to let a region be quoted by several pages ("at least
once, not exactly once"; `BOOT.md`'s ⚠ of 2026-09-17), and this task's coder did not
rewrite the guide pages to quote every one of the twelve scenarios the samples node
now carries (out of its scope: the C# blocks of the guide, not its prose or page
count). The claim now states only what L1 actually checks: a marked block matches its
named region. `samples/Samples/API.md` records which scenarios the guide quotes today.

## What the tests rely on

- The tree contracts of `Cli` and `Samples` (`Program.Run`, `Scenarios`,
  `ClassNameOf`), in-process.
- `JsonSchema` and `RunPropertyCut` of the Harness node (moved from `Cli.Tests` in the
  distribution phase, 2026-09-16).
- `RepositoryPaths` of the Fixtures node for paths from the repository root.
- The approved files in this node's `approved/` directory; on a mismatch the actual
  bytes are written beside them as `*.actual.txt`/`*.actual.json` (git-ignored).

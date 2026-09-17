# API.md — Docs.Tests

The node exposes nothing outward. Its contract points upward: what the parent may
consider proven about the documentation, the samples and the schemas.

## What this node guarantees ✅

| Claim | Confirmed by | State |
|---|---|---|
| every marked C# block of the guide equals its sample's snippet region byte for byte, and every scenario is quoted exactly once | L1 (`SnippetTests`) | ✅ |
| every scenario prints its approved output | L2 (`SampleOutputTests`, `approved/samples/`) | ✅ |
| every command-line example of the guide runs from `samples/cli/` and delivers its approved document, `run` cut as the command line's tests cut it | L3 (`CommandLineExampleTests`, `approved/cli/`) | ✅ |
| every relative link of the entry documents resolves to a file | L4 (`LinkTests`) | ✅ |
| every document under `samples/cli/` validates against its schema, read through `apthermo schema` | L5 (`SchemaValidationTests`) | ✅ |
| every guide page has the shared shape | L6 (`GuideShapeTests`) | ✅ |

## What the tests rely on

- The tree contracts of `Cli` and `Samples` (`Program.Run`, `Scenarios`), in-process.
- `JsonSchema` and `RunPropertyCut` of the Harness node (moved from `Cli.Tests` in the
  distribution phase, 2026-09-16).
- `RepositoryPaths` of the Fixtures node for paths from the repository root.
- The approved files in this node's `approved/` directory; on a mismatch the actual
  bytes are written beside them as `*.actual.txt` (git-ignored).

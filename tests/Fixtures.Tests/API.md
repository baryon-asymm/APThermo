# API.md — Fixtures.Tests

The node exposes nothing outward: nobody references a test project. Its contract
points upward: it is what the parent may consider proven about `Fixtures`.

## What this node guarantees

| Claim | Confirmed by | State |
|---|---|---|
| every committed fixture loads, names its kind, its script and the pinned package version, and was generated from the committed data files | L1: `FixtureLoadingTests` | ✅ 2026-09-12 |
| the tolerance table loads with a derivation per field and covers every numeric state field the rocket and equilibrium fixtures report | L1: `ToleranceTableTests` | ✅ 2026-09-12 |
| a malformed fixture or table is rejected with the file name and the field | L1: `MalformedFixtureTests` | ✅ 2026-09-12 |

## What the tests rely on

- The repository root and the fixture directories come from `RepositoryPaths` and
  `FixtureFiles` of the Fixtures node.
- The kinds are the directories under `cases/`; the compared fields are read from the
  fixture documents; the pinned package version is read from `generate/requirements.txt`.
- Malformed documents are written under the system temporary directory and removed
  when the test class is disposed.

# API.md — Fixtures.Tests

The node exposes no package API. Its assembly participates in protocol reflection;
its contract points upward: it is what the parent may consider proven about `Fixtures`.

## What this node guarantees

| Claim | Confirmed by | State |
|---|---|---|
| every committed fixture loads, names its kind, its script and the pinned package version, and was generated from the committed data files | L1: `FixtureLoadingTests` | ✅ 2026-09-12 |
| the tolerance table loads with a derivation per field and covers every numeric state field the rocket and equilibrium fixtures report | L1: `ToleranceTableTests` | ✅ 2026-09-12 |
| shared snapshot names preserve approved bytes; schema reference siblings and unused branches are checked; tolerance comparisons refuse nonfinite values | `OracleGuardTests` | ✅ 2026-11-14 |
| a malformed fixture or table is rejected with the file name and the field | L1: `MalformedFixtureTests` | ✅ 2026-09-12 |
| a `seeded` document names its tp seed and assigns exactly one of enthalpy and entropy, and the kind is covered by the tolerance table | L1: `MalformedFixtureTests`, `ToleranceTableTests`, `FixtureLoadingTests` | ✅ 2026-10-03 |
| every fixture's `scriptSha256` and `generatorSha256` match the committed generator, and all fixtures carry one `thermoLibSha256` and one `transLibSha256` | L1: `FixtureLoadingTests` | ✅ 2026-09-27 |

## What the tests rely on

- The repository root and the fixture directories come from `RepositoryPaths` and
  `FixtureFiles` of the Fixtures node.
- The kinds are the directories under `cases/`; the compared fields are read from the
  fixture documents; the pinned package version is read from `generate/requirements.txt`.
- Malformed documents are written under the system temporary directory and removed
  when the test class is disposed.

Process infrastructure is checked with the owned finite ProcessProbe executable
(`DotnetProcessGuardTests`, Category=EndToEnd): exact bytes/arguments/environment,
nonzero exits, validation, cancellation, deadline and inherited-pipe/tree cleanup.

⚠ 2026-11-14: the old introduction said nobody referenced a test project; the protocol
inventory does reference this assembly. This is verification, not a consumer API.

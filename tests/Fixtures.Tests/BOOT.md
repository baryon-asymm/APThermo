# BOOT.md — Fixtures.Tests

## Purpose

The definition of what "`Fixtures` is ready" means: the levels of verification, what
each is checked against, what is covered and what is not. The fixtures are the
reference every numerical node is compared with; this node proves their form,
provenance and coverage, never their values (AGENTS.md §1).

| Level | What it checks | Against what (source of truth) | State |
|---|---|---|---|
| L1 | every committed fixture loads through the loader, names its kind, its script and the pinned package version, and carries the hashes of the committed data files | the directory listing of `cases/`, `generate/requirements.txt`, the SHA-256 of `data/thermo.inp` and `data/trans.inp` computed in the test | ✅ 2026-09-12 |
| L1 | the tolerance table loads, every field has a derivation, an unknown field throws, the comparison is absolute plus relative, and every numeric state field the rocket and equilibrium fixtures report has an entry | `tolerances.json` and the fixture files themselves (field names read from them) | ✅ 2026-09-12 |
| L1 | a malformed document is rejected with the file name and the field | documents written to a temporary directory | ✅ 2026-09-12 |
| L2 | regeneration is byte-identical (`regenerate.py --check` exits 0) | the generator environment; a command of the Fixtures node's procedure, run by hand or in CI, not by xunit | manual |

## Invariants

- **Lists are produced by the machine**: kinds by listing `cases/`, compared fields by
  reading the fixtures, the pinned version by reading `requirements.txt`. The one typed
  list is the set of kinds of the case matrix, which is the design decision under test.
- **No fixture is interpreted here.** Whether a value is right is the business of the
  node under test; this node checks form, provenance and coverage.
- **Temporary files only.** Malformed documents are written under the system temporary
  directory and deleted; nothing is written into the working directory.

## Dependencies

- [Fixtures](../Fixtures/API.md) — the loader, the tolerance table and the fixture files under test.

Outside the tree: xunit.

## Constraints

- Part of the default test command; runs without Python and without the reference
  package.
- The regeneration check is not an xunit test: a test that must be skipped where the
  package is absent is a check nobody sees red (AGENTS.md §13). It is a command in the
  Fixtures node's procedure instead, and its last run is dated in that node's
  acceptance criteria.

## Acceptance criteria

- [x] 2026-09-12 — L1 loader: `FixtureLoadingTests` (`Every_fixture_of_a_kind_loads`
      for every kind directory, `The_kinds_present_are_those_of_the_case_matrix`,
      `Every_fixture_is_tied_to_the_committed_data_files`,
      `Every_fixture_names_the_pinned_package`, `Every_fixture_names_the_script_that_wrote_it`).
- [x] 2026-09-12 — L1 tolerance table: `ToleranceTableTests`
      (`The_table_loads_with_a_derivation_for_every_field`, `Unknown_fields_are_reported_by_name`,
      `Matches_adds_the_absolute_and_the_relative_part`,
      `Every_state_field_of_the_fixtures_has_a_tolerance` for `rocket`, `tp`, `hp`, `sp`).
- [x] 2026-09-12 — L1 malformed documents: `MalformedFixtureTests` (`A_complete_document_loads`,
      `A_missing_field_names_the_file_and_the_field`, `A_missing_provenance_field_is_named`,
      `A_kind_that_does_not_match_its_directory_is_rejected`,
      `Text_that_is_not_json_is_rejected_with_the_file_name`,
      `A_tolerance_without_a_derivation_is_rejected`, `A_negative_tolerance_is_rejected`).
- [x] 2026-09-12 — Every check proven non-degenerate once (AGENTS.md §13): the
      `dataThermoSha256` of `cases/constants/R.json` altered turned
      `Every_fixture_is_tied_to_the_committed_data_files` red with the file named; the
      `gammaS` entry removed from `tolerances.json` turned
      `Every_state_field_of_the_fixtures_has_a_tolerance` red for `rocket`, `tp`, `hp` and
      `sp` with the field named. Mutations reverted; `regenerate.py --check` confirmed the
      fixtures unchanged afterwards.

## Taboos

- Do not type an expected value that exists in a fixture or in `requirements.txt`.
- Do not skip a test when a fixture directory is empty: an empty kind is a failure.
- Do not loosen the loader to accept a document missing a field: the field list is the contract.

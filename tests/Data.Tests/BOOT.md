# BOOT.md — Data.Tests

## Purpose

The definition of what "`Data` is ready" means: the levels of verification, what
each is checked against, what is covered and what is not. This is not "tests for the
code" (AGENTS.md §1): it is the readiness criterion, moved into a node of its own.

| Level | What it checks | Against what (source of truth) | State |
|---|---|---|---|
| L0 | column parsing: `D`/`E`/blank exponents, formula pairs, phase field, `N = 0` records, transport header codes | hand-transcribed record strings with expected values in fixture files of this node | ⏳ |
| L1 | full loads of the committed `data/thermo.inp` and `data/trans.inp`: counts, fixture records, interval contiguity, anomaly list, atomic weights, failure on corrupted copies | an independent line scan in the test, the anomaly list approved file, the fixture records | ⏳ |
| Protocol | the tree invariant, documents against code | `AGENTS.md`, the surface snapshot (protocol tests node) | ⏳ |

Each next level makes sense only when the previous one is green.

## Invariants

- **The reference is files, not numbers in the test code.** Expected records live in
  JSON fixture files next to the tests; a test never types a coefficient.
- **Counts are generated, not typed**: the number of records the loader must find is
  produced by the test's own scan of the file text, so the criterion cannot fall
  behind the file.
- **Corruption tests mutate a copy** in the test's temporary directory; the committed
  data files are never touched.

## Dependencies

- [Data](../../src/Data/API.md) — what is being checked.

Outside the tree: xunit; the committed data files `data/thermo.inp`, `data/trans.inp`.

## Constraints

- Part of the default test command.
- Data paths are resolved from the repository root (found from the test source file
  with `[CallerFilePath]`), never by `../../..` chains.
- Tests do not write into the working directory.

## Acceptance criteria

- [ ] L0 green with the fixture strings listed in the node (date and test names).
- [ ] L1 green: counts, fixture records, anomaly list, transport blocks, atomic
      weights, corruption failures (date and test names).
- [ ] Every check proven non-degenerate once: a fixture value altered, a count
      altered, a record corrupted, each seen red (AGENTS.md §13).

## Taboos

- Do not loosen a comparison: parsed doubles are compared exactly, not approximately.
- Do not hard-code expectations that exist in a fixture file.
- Do not skip a test when a data file is missing: a missing file is a failure.

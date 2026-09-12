# BOOT.md — <Node>.Tests

## Purpose

The definition of what "<Node> is ready" means: the levels of verification, what each
is checked against, what is covered and what is not. This is not "tests for the code"
(AGENTS.md §1): it is the readiness criterion, moved into a node of its own.

| Level | What it checks | Against what (source of truth) | State |
|---|---|---|---|
| L0 | <small deterministic parts> | <an independent truth: analytics, reference files, another implementation> | ⏳ |
| L1 | <subsystems in isolation> | <…> | ⏳ |
| L2 | <an end-to-end scenario> | <…> | ⏳ |
| Protocol | the tree invariant, documents against code | `AGENTS.md`, the surface snapshot | ⏳ |

Each next level makes sense only when the previous one is green: a green L2 with a
red L0 means not "correct" but "matched for an unknown reason".

## Invariants

- **The reference is files, not numbers in the test code.** A test that rewrites the
  expectation by hand stops being a check and becomes a copy of the code.
- **A tolerance is derived, not tuned**: from the precision of the reference, the print
  format, the word size. Tolerances live in one place; a local tolerance in the body of
  a test makes a loosening invisible when reading the diff.
- <Long checks are marked `<marker>` and excluded from the fast set; the mark itself is
  checked by the machine, not by convention: the price of a forgotten mark falls on
  whoever runs the fast set next.>

## Dependencies

- [<Node>](../../src/<Node>/API.md) — what is being checked.

Outside the tree: <test framework and version>.

## Constraints

- The node is part of the default test command: a project that is not run is
  indistinguishable from an absent one.
- Paths to data are resolved from the repository root, not by a chain of `../../..`.
- Tests do not write into the working directory; temporary things go into the test's
  temporary directory.

## Acceptance criteria

- [ ] <Every row of the levels table is green, with a date and the names of the tests.>
- [ ] Every check is proven non-degenerate: what it guards was broken and the red was
      seen (AGENTS.md §13).

## Taboos

- Do not loosen a tolerance for the sake of green.
- Do not hard-code expectations, duplicating the reference.
- Do not mark a test as skipped when the reference is unavailable: an unavailable
  reference is a failure, not "nothing to check".

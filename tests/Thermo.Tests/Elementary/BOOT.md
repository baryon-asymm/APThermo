# BOOT.md — Thermo.Tests.Elementary

## Purpose

The definition of what "the tree's own exp, log and pow are correctly rounded" means: the oracle (mpmath at 400 bits,
the exact value rounded to nearest even), the fixtures it writes, the generators of the tables and the facts that hold
the node [Thermo.Elementary](../../../src/Thermo/Elementary/API.md) to them. A child node of `tests/Thermo.Tests`
(no project of its own: its facts compile into the thermo tests assembly, namespace `APThermo.Thermo.Tests.Elementary`).
It also holds the Python files, which are code of the node: `oracle.py`, `generate_fixtures.py`, `generate_tables.py` and
`check_worst_cases.py`.

## Invariants

- **The reference is an exact oracle, not another library.** Every expected value is the exact real value
  computed by mpmath at 400 bits and rounded to nearest, ties to even, subnormals and overflow included, written as a bit
  pattern in `fixtures/` by `generate_fixtures.py`; nothing is compared with `System.Math` except the special
  values the standard fixes by the operands (the runtime's C library is what the node exists to replace, and it is
  not correctly rounded: 657 of 38 220 exp inputs were misrounded by it in the design measurement).
- **The exact cases are computed on the integers.** The oracle's exp/log approximation cannot round an exact
  midpoint (it lies on the boundary): when the exact power is a dyadic rational (`exact_rational_power`) the value is
  that rational. Found by CORE-MATH's list (2026-10-05): the first oracle rounded `x^(17/16)` midpoints by the
  noise of its own approximation, and the tree's `ExactPower` had to learn the dyadic exponents.
- **The fixtures are constructed and published cases, deterministic.** The families (hard-to-round neighbourhoods
  of 1 for exp and log, every log table boundary in five octaves, the subnormal range, the thresholds, the transport and
  throat exponents, the midpoint cases of pow) come from `oracle.py` with fixed seeds; the worst cases are a
  selection of CORE-MATH's lists by a rule written in `fixtures/PROVENANCE.txt` (hardest by distance from a midpoint,
  exact midpoints, a uniform sample), with the project's commit and its MIT licence text. The lists are not
  committed whole.
- **Every fact is shown red once** (AGENTS.md §13) by a mutation named in the criteria below.
- **No tolerance.** The facts compare bits. The margin fact compares an error against four times
  below a bound, a number the node did not choose: the bound is the source's.

## Dependencies

- [Thermo.Elementary](../../../src/Thermo/Elementary/API.md) — the functions, the fast and accurate paths, the tables.
- [Thermo](../../../src/Thermo/API.md) — `KernelMath`, the entry the numerical nodes use.
- [Fixtures](../../Fixtures/API.md) — `RepositoryPaths`, to find the fixture files from the repository root.
- [Thermo.Tests](../API.md) — the parent's assembly, as parent internals: the compiler's own helper types for
  collection and array expressions (`<>z__ReadOnlyArray`, `<PrivateImplementationDetails>`), which sit in the assembly's
  global namespace and so read as the project node's; no member the parent wrote is used.

Outside the tree: xunit; Python 3.8+ with mpmath 1.3.0 for the generators and the optional full check.

## Constraints

- Part of the default test command of the thermo tests; no CUDA involved (the execution tests node compares the two
  accelerators exactly, and the CPU accelerator with `KernelMath`).
- Paths from the repository root. No fact writes into the tree; the optional full check writes into the system
  temporary directory and deletes it.
- `generate_tables.py` writes the three generated files of the source node; a regeneration is a committed change
  of both, never of one. `generate_fixtures.py --wc-dir <directory>` needs the CORE-MATH lists
  (https://gitlab.inria.fr/core-math/core-math, commit 284b3b0e198042c38f5c30316f696786b10816b0) and rewrites `fixtures/`.

## Acceptance criteria

- [x] 2026-10-05 — Correct rounding: `CorrectRoundingTests.EveryFixtureInputIsCorrectlyRounded` and
      `TheAccuratePathAgreesWithTheOracleOnEveryFixture`, six fixtures each (the families 4 420, 3 948 and
      2 140 inputs; the worst-case selections 6 991, 7 014 and 8 070), 0 not correctly rounded; the evidence
      of [the source node](../../../src/Thermo/Elementary/BOOT.md).
- [x] 2026-10-05 — Every published worst case: `FullWorstCaseListsTests.TheFullWorstCaseListsAreCorrectlyRounded`
      with `APTHERMO_COREMATH_WC` set to the directory of CORE-MATH's `exp.wc`, `log.wc`, `pow.wc`: 2 266 122
      inputs, 0 not correctly rounded, 1 m 54 s (LongRunning; without the variable it is skipped with that reason,
      the one skip of the node).
- [x] 2026-10-05 — The fast paths' bounds: `FastPathMarginTests` (exp 6.96×, log 10.87×, pow 8.94×).
- [x] 2026-10-05 — Specials, table identities, structure: `SpecialValueTests`, `TableIdentityTests`, `StructureTests`.
- [x] 2026-10-05 — Each fact red once, mutation reverted: exp's ε 2^-72 → 2^-80 (`exp.wc.txt` and the exp and pow
      margins red); `rl·r³` dropped from log1p (the log and pow margins red); the accurate path's Taylor degree 27 → 14
      (the accurate-path facts of `exp.wc.txt` and `pow.wc.txt` red); `ExactPower` off (531 rows of `pow.wc.txt` red);
      `!xInfinite` removed from pow's NaN rule (`PowOfTheSpecialOperandsEqualsTheRuntimes` red); a digit of
      `T(1)` truncated (`TheExpTableSquaresToItself` red; a change below 2^-104 of the entry is below what an identity
      can see, and the fixtures and the margin see it instead); `NoInlining` removed from `LogFunction.AccurateTd`
      and a mutable static field added (`StructureTests`, one each).

## Taboos

- Do not compute an expected value with the code under test, or with the runtime's C library.
- Do not loosen a bound or a required margin for green.
- Do not skip a fact because a fixture is missing: that is a failure. The one skip is the optional full check without
  its environment variable.
- Do not commit the full CORE-MATH lists.

# BOOT.md — Thermo.Elementary

## Purpose

The tree's own elementary functions, correctly rounded: exp, log and pow, the double-double
and triple-double arithmetic they are built from, the exact midpoint cases of pow, and their
constant tables. A child node of `src/Thermo` (no project of its own, namespace
`APThermo.Thermo.Elementary`, compiled into the thermo assembly): every numerical node reaches it through
`KernelMath` only. It is kernel-compatible C#, so the CPU accelerator and CUDA run the same operations and
return the same bits, and those bits are the exact value rounded to nearest, whatever the runtime's C
library or libdevice would have said (design of 2026-10-05, `ACCEPTANCE.md` of the root, "own math").

## Invariants

- **Correctly rounded.** For every double input (two for pow) the result is the exact real value rounded to
  nearest, ties to even, subnormals and overflow included; the special values follow IEEE 754 and C99
  Annex F (pow's table equals the runtime's). A NaN operand gives the first NaN operand; a NaN made from
  non-NaN operands is the constant `double.NaN`.
- **Two phases, one decision.** A fast path computes a double-double whose relative error is below the bound
  named in its source (exp 2^-72, log 2^-74, pow 2^-72 + |y log x|·2^-74). The one rounding test
  (`RoundingTest`) returns `h + l` only when `h + (l − ε|h|)` and `h + (l + ε|h|)` are the same double;
  otherwise the accurate path rounds: table-free, triple-double (error below 2^-125), taking the argument
  the test could not certify and every subnormal result. pow's accurate path first resolves the exact
  midpoints on the integers (`ExactPower`), which no approximation can round: `x^y` is exactly a midpoint
  of two doubles for an integer y in [2, 53] and an odd significand of x with `a^y` of 54 bits, for the
  dyadic exponents `p/2^k` of a perfect power (`3^(17/16)`-like cases of CORE-MATH's list, 2026-10-05), and
  for a power of two reaching 2^-1075.
- **Only IEEE operations.** `+ - * /`, `Math.FusedMultiplyAdd`, `Math.Floor`, `Math.Abs`, `Math.Sqrt` (the
  perfect-square test of `ExactPower`), the bit conversions of `BitConverter`, integer arithmetic. No
  `System.Math` function that a C library or libdevice evaluates; a product is fused with a sum only where the
  source writes `ErrorFree.Fma` (root, "Math in numerical nodes").
- **No state.** The tables are `switch` expressions over constants, written by a generator; no static field
  but constants.
- **One copy each.** `ExpFunction.Exp`, `LogFunction.Log`, `PowFunction.Pow` and each accurate path carry
  `[MethodImpl(MethodImplOptions.NoInlining)]` (root, Compile size); a fast path inlined at the tree's
  ninety call sites did not compile within twelve minutes.
- **The rounding test is the only copy**: a bound changes in the source of its function, and the margin fact
  of the tests node holds the measured worst error at least four times below it.

## Dependencies

None.

Outside the tree: nothing at run time. The generator `tests/Thermo.Tests/Elementary/generate_tables.py`
(Python 3.8+, mpmath 1.3.0) writes `ExpTable.cs`, `LogTable.cs` and `ElementaryConstants.cs`.

## Constraints

Inherited from [Thermo](../BOOT.md) and the root. In addition:

- Reduction and tables. exp: `k = round(64x/ln 2)`, `T(j) = 2^(j/64)` as a double-double (64 entries). log:
  `i = round(32(m − 1))`, `c_i = RN(1/(1 + i/32))` with `c_0 = 1` and `c_32 = 1/2`, −log `c_i` as a
  double-double; the entry of 32 is the ln 2 double-double itself, so that `e ln 2 − log c` cancels to exactly 0
  for x in [1 − 2^-7, 1 + 2^-6).
- Tables and constants are generated, never typed: `ExpTable.cs`, `LogTable.cs` and `ElementaryConstants.cs`
  carry the generator's header. A fact checks their identities (`T(j)² = T(2j)` within 2^-104, `c_i(1 + i/32)`
  within 2^-53, the two tables against each other through the fast path, the ln 2 split).
- The comparisons keep the constant on the right (root, the third ILGPU defect); every method is at most 60
  lines, a table splits into two methods of 32 arms; no `partial`, no region.
- The accurate paths are rare and slow by design: over 4·10^6 random calls the rounding test sent 1.5·10^-6
  of the calls of exp, under 2.5·10^-7 of log and 1.1·10^-5 of pow to them (design of 2026-10-05); on
  CUDA a warp pays for one when any of its 32 lanes needs it. Nothing here is optimised for them.
- Cost, measured 2026-10-05 on the 100 000-case rocket sweep against the C library and libdevice: the
  CUDA kernel +74 %, the CPU accelerator +25 to 34 % (owner decision O9: accepted with correct rounding).

## Acceptance criteria

The facts live in `tests/Thermo.Tests/Elementary`; each was shown red once by the mutation named after it.

- [x] 2026-10-05 — Every input of the oracle fixtures is correctly rounded, through the entries and through
      the accurate paths alone: `CorrectRoundingTests.EveryFixtureInputIsCorrectlyRounded` and
      `TheAccuratePathAgreesWithTheOracleOnEveryFixture` over `exp`, `log` and `pow`, each the constructed
      families (4 420, 3 948 and 2 140 inputs) and a selection of CORE-MATH's worst-case lists (6 991, 7 014 and
      8 070 inputs, among them every exact midpoint family of pow), 0 not correctly rounded; each fails on
      a truncated fixture. Red once: exp's ε set to 2^-80 (`exp.wc.txt` red), the Taylor degree of the accurate
      path 27 → 14 (four rows red), `ExactPower` switched off (531 of 8 070 rows of `pow.wc.txt` red).
- [x] 2026-10-05 — The full published lists, 2 266 122 inputs (exp 1 129 176, log 134 942, pow 1 002 004),
      are correctly rounded against the oracle: `FullWorstCaseListsTests.TheFullWorstCaseListsAreCorrectlyRounded`,
      LongRunning, read from `APTHERMO_COREMATH_WC` (skipped with that reason when the variable is not
      set, the one optional fact of the node), 0 not correctly rounded (the run before the oracle's
      fix of a negative underflow found the oracle, not the tree, wrong on one input).
- [x] 2026-10-05 — The fast paths' errors stay at least four times below their bounds: `FastPathMarginTests`
      (exp 6.96×, log 10.87×, pow 8.94× over 2 200, 1 955 and 1 500 inputs at the reduction's worst places
      against the oracle's exact double-double). Red once: the term `rl·r³` of log1p dropped (log and pow red).
- [x] 2026-10-05 — The special values equal the runtime's: `SpecialValueTests`; the tables and constants hold
      their identities: `TableIdentityTests`; no static state and `NoInlining` on every entry:
      `StructureTests`. Red once: `x < 0` with an infinite base left to the NaN rule (pow red), a digit of
      a table entry changed (the square identity red), one `NoInlining` removed, one static field added.

## Taboos

- No table in a static array or a span: ILGPU puts a static array into local memory on every call (40 times
  slower) and the no-hidden-state check refuses it.
- No widening of an error bound to turn a fact green: a bound is an analytic budget, and the measured
  worst stays at least four times below it.
- No claim of correct rounding without the oracle fixture; a function enters the root's math list with its fixture.
- No call of `System.Math.Exp`, `Log`, `Log10`, `Pow` or a hyperbolic or trigonometric function in this node.
- No `float`, no `LibDevice`, no `XMath`.

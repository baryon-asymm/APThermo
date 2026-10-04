# BOOT.md — Equilibrium.GasPhase

## Purpose

A child node of `src/Equilibrium` (its `BOOT.md`, `## Structure`), born 2026-10-03 for 0.2.2. It
decides, at one assigned temperature and pressure, whether the equilibrium holds a gas phase at all:

- `CondensedSimplex` finds the minimum of the Gibbs energy of the condensed species alone under
  element conservation: a linear program, since a pure condensed phase has a chemical potential
  independent of its amount; a two-phase revised simplex with Bland's rule.
- `TangentPlane` tests the gas phase at that minimum's multipliers: an ideal gas of any composition
  lowers the Gibbs energy if and only if `S = Σ_j exp(Σ_i a_ij π_i − g_j/RT − ln(p/p°))` over the
  gases exceeds 1; where the optimal multipliers are not unique it searches their face for `S < 1`.
- `GasPhaseVerdict` composes the two into `Gasless`, `GasRequired` or `Undecided`, and gives the
  condensed minimum's enthalpy, entropy and heat capacity (`CondensedFigures`).

The `Recovery` node asks the verdict after a failed tp attempt; nothing else calls it. The trace-gas node
asks one more entry of the same program, `PhaseOnePoint`: the point phase one stops at, which starts its
pass (2026-10-04). The cluster
has a reason of its own to change: RP-1311's unknowns `ln n_j` and `ln n` cannot represent a gas
phase of zero moles, so the equilibria where the gas vanishes (KO2 beside K2O2, water below its
boiling point, Al2O3, KCl, a thermite, CaCO3 below its decomposition) need a method of their own
and a proof in place of a convergence.

## Invariants

The parent's invariants hold here; the method is still the minimum of the Gibbs energy under
element conservation, restricted to the condensed species, with no equilibrium constant.

- **A verdict is a proof or nothing.** `Gasless` only when the condensed minimum holds every element
  (phase one leaves each artificial column within `1e-12 · max(1, b_i)`, and the moles written are
  every basic record at its positive amount), no eligible record left out gains more than 1e-9 per
  mole at the reported multipliers, and `ln S < −1e-9` there. What the search cannot prove is
  `Undecided`, and the caller keeps its failure.
  - The 1e-12 is in kmol/kg: `b_i` is below 1 for every real mixture, so it is an absolute bound.
    An element the condensed species cannot hold within it is left unheld by a `Gasless` state, whose
    residual is then the input's own perturbation (a mixture within ±1e-12 of exact stoichiometry
    ends `NoGasPhase` with an excess of up to 2e-12 of an element unaccounted for). The owner chose
    this bound over a relative 1e-13 · b_i (decision G1, 2026-10-04): the relative form turns 273 of
    the ±1e-12 states into `Ok` with their excess as a trace gas and costs six `Ok` states.

  ⚠ 2026-10-04: was "the moles written omit an amount within that", now every basic record with a
  positive amount is written: KO2 − 1e-10 O is KO2 and K2O, the K2O at 9.4e-13 kmol/kg, and the
  omission dropped it with its potassium and oxygen (ρ_K = −1.3e-10). `ZeroLevel` stays for the face search.
- **A verdict writes only a proof.** For every verdict but `Gasless`, `Decide` returns with
  `result.Moles` and `result.Multipliers` as it found them, bit for bit; for `Gasless` it writes the
  condensed minimum (every gas zero) and the certificate's multipliers, and nothing else of the result.
- **Eligibility is the condensed-species rule's.** A record is a column when its elements are
  present (any mark but `Absent`) and it lies in its effective range at the temperature
  (`PhaseGeometry.InEffectiveRange`); an attempt's anti-cycling memories do not bind the program,
  which is global.

## Dependencies

- [Equilibrium](../API.md) — the descriptors of its inputs, scratch and outputs, `ScratchLayout`,
  `DenseSolver`, `SpeciesMarks`, `Composition.EvaluateFunctions`, `CaseSetup.LogPressure`.
- [Condensed](../Condensed/API.md) — `PhaseGeometry.InEffectiveRange`.
- [Thermo](../../Thermo/API.md) — the species table view, `KernelMath`, `PhysicalConstants`.

Outside the tree: ILGPU 1.5.3 (`ArrayView<T>`). The parent's internal types this node uses are not
in the parent's tree contract; the child belongs to the parent's assembly and reads them as its own.

## Constraints

Inherited from the parent ([BOOT.md](../BOOT.md)) and, through it, from the root. In addition:

- **No whole-struct copies the CUDA post-link rejects** (2026-10-04): no struct of this node holds two `bool`
  fields next to each other, and a struct with a `bool` field is not returned by value (the rule and its reason
  are in [Recovery/BOOT.md](../Recovery/BOOT.md), "No whole-struct copies"; `ByteVectorTests` checks every entry point).
- Every type is `internal`; no project of its own: the files compile into `src/Equilibrium`'s
  assembly under `APThermo.Equilibrium.GasPhase`. The root's code-shape constraint applies; no
  row is declared.
- Kernel-compatible C#: no allocation, recursion or exception; of the math list `Exp`, `Log`,
  `Abs` and `KernelMath.Min`/`Max`; an ordered comparison with a constant has it on the right.
- **Reached once.** `GasPhaseVerdict.Decide` carries `[MethodImpl(MethodImplOptions.NoInlining)]`
  and has one call site, `Recovery.AttemptPlan.Next` (the root's compile-size constraint). `PhaseOnePoint`
  likewise, its one call site the `TraceGas` node's (2026-10-04).
- **Scratch.** No slice of its own; it borrows what an ended attempt leaves free: the matrix, the
  right-hand side and the row scales; `Corrections` (the basic values); `CondensedInSolution` (the
  basis: a column, or `−1 − row` for an artificial one); `Tie.CondensedSet` (row → element);
  `Tie.LogMoles` (the multipliers by row); `Tie.CondensedMoles` (the face coordinates); `LogMoles`
  and `Tie.Elements.Multipliers` (the failed attempt's moles and multipliers, restored unless
  `Gasless`). The face directions lie in the matrix past its first `ElementCount` rows of stride.
  `BracketEnds` is never touched.
- **The program.** Rows: the active elements. Columns: the eligible records at cost `g_j/RT`, and
  one artificial column per row, cost 1 in phase one and 0 in phase two. Bland's rule: the entering
  column is the lowest index with a reduced cost below −1e-9, the leaving one the smallest ratio,
  ties to the lowest column; a pivot below 1e-11 is refused; at most 256 pivots per phase. After
  phase one, an element the condensed species cannot hold beyond `1e-12 · max(1, b_i)` makes the
  verdict `GasRequired`. Artificial columns at zero level are driven out by an eligible record with
  a nonzero entry in their row; a row none can enter is redundant and keeps its artificial. Phase
  two must leave every basic value at least −1e-12, else `Undecided`.
- **The certificate.** `ln S` over the gases whose elements are present, at `π = π0 + V t`: `π0` the
  phase-two multipliers; `V` one direction `B⁻ᵀ e_q` per basic slot at zero level (a record at zero
  moles: its coordinate bounded above by 0; a redundant row's artificial: by 200); `t` from 0. While
  `ln S ≥ −1e-9`, at most 32 sweeps of a coordinate search: each coordinate moves to the minimum of
  the convex `ln S` along its direction (100 bisections on its slope) between the bounds that keep
  every eligible record out at a gain of at most 0. `ln S > 1e-9` at the end is `GasRequired`;
  between the margins, `Undecided`.
- **The figures.** `h = R T Σ n_j (H°/RT)_j`, `s = R Σ n_j (S°/R)_j`, `cp = R Σ n_j (Cp°/R)_j` over
  the condensed minimum, J/kg and J/(kg·K), with no mixing or pressure term (pure condensed phases
  of no volume, as RP-1311). The bracket reads them as a probe's value and slope.
- **No temperature search here.** The verdict holds at the temperature it is given; an hp or sp case
  finds its temperature in the `Recovery` node's bracket over tp probes. The prototype's grid of 96
  temperatures and its bisection on the condensed minimum's `h` or `s` are not copied (2026-10-03).

## Structure

Internal, kernel-compatible, one type per file: `GasPhaseVerdict` (static facade), `CondensedSimplex`
(static), `TangentPlane` (static), `GasVerdict` (enum: `Undecided = 0`, `GasRequired`, `Gasless`),
`CondensedFigures` (struct: `Enthalpy`, `Entropy`, `HeatCapacity`), `Face` (readonly struct: the
row count, the direction count, the directions' offset, `ln(p/p°)`, the temperature) and
`CoordinateRange` (readonly struct: the interval of one face coordinate), which keep every method of
`TangentPlane` within six parameters and its nesting within three. Each constant of `## Constraints`
is named in the class that uses it.

## Acceptance criteria

- [x] tp `NoGasPhase`, each red on the old code (`NotConverged` or `TemperatureOutOfRange`; red with the
      verdict disabled, 109 of 113 facts of `GasPhaseTests`, 2026-10-04): KO2 (K:O = 1:2(1+ε), ε from −1e-2 to 1e-12),
      H2O at 300 and 350 K and 1 bar, Al2O3, KCl, KCl+KO2, the thermite Al:Fe:O = 2:2:3, CaCO3 below its
      decomposition, MgO, Li2O; every gas zero, the element invariant, every eligible left-out gain ≤ 1e-9,
      `ln S` < −1e-9 at the reported multipliers, a state of T and p only (2026-10-04,
      `GasPhaseTests.AGaslessSystemEndsNoGasPhaseAndIsClearOfTheGaslessConditions`, whose conditions are
      `EquilibriumConditions.GaslessViolations`, computed by the test; `EquilibriumTests.ACompositionWithNoGasPhaseIsAStatusWithATemperatureAndAPressureOnly`
      in Problems.Tests).
- [x] The minimum is the minimum: on systems of at most three elements, the condensed minimum's Gibbs energy
      equals the least over every feasible basis the test enumerates (2026-10-04,
      `GasPhaseTests.TheCondensedMinimumIsTheLeastOverEveryBasis`, over `BasisEnumeration`).
- [x] A gasless minimum writes every record it holds (2026-10-04, `GasPhaseTests.AGaslessMinimumWritesEveryRecordItHolds`:
      KO2 − 1e-10 O, 300 to 1 500 K, 1 kPa to 10 MPa, 22 `NoGasPhase` states, each with its second record and every
      element within the relative invariant; red with the omission at 1e-12 restored; `ResidueVerdictTests`
      holds the ±1e-12 states to the verdict's absolute bound).
- [x] The degenerate face: exact stoichiometry whose vertex multipliers do not prove it and whose face search does
      (2026-10-04, `GasPhaseTests.TheDegenerateFaceNeedsTheSearchForItsCertificate`, 11 of 119 facts red with the
      sweeps set to 0); a redundant row keeps its artificial column (`ARedundantRowKeepsItsArtificialColumnAndTheVerdictStillHolds`);
      a composition that needs the gas is `GasRequired` and leaves the failed attempt's moles and multipliers bit for
      bit (`AVerdictThatProvesNothingLeavesTheFailedAttemptAsItFoundIt`, an oxygen excess, phase one infeasible;
      `WaterUnderItsVapourPressureNeedsTheGasAndLeavesTheAttemptAsItFoundIt`, the tangent plane). The prototype's
      residual of an infeasible phase one is not part of the contract: the trace-gas design adds its own entry.
- [x] Bit-neutral: a failed tp case the verdict does not prove keeps its moles, multipliers, status and iterations
      bit for bit; no line of an `Ok` case moved in any `Bits*.approved.txt` (2026-10-04, the two `GasPhaseTests`
      facts of the previous criterion for the moles and multipliers; every `BitSnapshotTests` fact of the
      Equilibrium, Problems, Performance and Cli tests nodes green against the unchanged Windows records).
- [ ] CUDA: a tp family of gasless states, GPU equal to CPU (status, moles and multipliers within the tier); Linux
      bits re-approved; the shape facts green with no new row.

## Taboos

- No `Gasless` without the certificate: a failed attempt is no evidence that the gas vanished.
- No temperature search: it is the bracket's ([Recovery](../Recovery/BOOT.md)).
- No write to `BracketEnds`, and no write to the result for a verdict but `Gasless`; `PhaseOnePoint` writes
  `result.Moles` only, for its one caller.
- No public type; no `float`, exception, allocation or virtual call.

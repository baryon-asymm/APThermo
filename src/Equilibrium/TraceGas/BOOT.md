# BOOT.md — Equilibrium.TraceGas

## Purpose

A child node of `src/Equilibrium` (its `BOOT.md`, `## Structure`), born 2026-10-04 for 0.2.2. It
converges a case whose gas phase, or one direction of whose multipliers, is carried by amounts too
small for the reduced iteration of RP-1311, and it states what every `Ok` must satisfy at the gas
level:

- `TraceGasPass`: `Run`, one trace-gas pass (its starts, its convergences and condensed-set changes,
  the entry restored on failure), and `Stationary`, the close guard.
- `TraceGasStart` and `PhaseOneSeed`: where a pass starts.
- `TraceGasIteration`: one convergence of one condensed set.
- `TraceGasSystem` and `TraceGasStep`: its matrix and its step.

The cluster has a reason of its own to change. RP-1311's tests weigh a correction by the share of
the whole mixture (3.5), and its iteration carries ln n_j. Where the gas is 1e-6 of the mixture, or a
direction of π is carried only by species below the retention threshold, those tests pass while the
gas has not converged:
- MgCO3 under CO2 at 10 MPa walked one e-fold per step along π_O − π_Mg − 2π_C and failed the
  element invariant at the close;
- MgCO3 with 1e-6 excess CO2 closed hp and sp `Ok` with CO at 2e-5 of the gas where the equilibrium
  holds none.

## Invariants

The parent's invariants hold here.

- **The same equations.** A trace-gas convergence solves the conditions of the reduced iteration:
  - the element balances and the condensed stationarities;
  - the gases on their stationarity through `x_j(π) = exp(Σ_i a_ij π_i − g_j/RT − ln(p/p°))`;
  - `Σ x_j = 1`, and for hp and sp the energy equation.

  The gas composition is substituted exactly instead of linearised. No equation is added and there
  is no equilibrium constant.
- **Run only after a reason.** A pass runs only where the `Recovery` node schedules it:
  - after a tp failure whose gasless verdict is `GasRequired` (seam (a));
  - as a bracket's final (seams (b) and (b′)).

  An `Ok` of the reduced iteration never reaches it.
- **An `Ok` leaves what a converged `ConvergenceSequence` leaves**: the temperature, `LogN`, the
  condensed set and its count, the moles refreshed at the second retention stage, no tie, every
  step counted.
- **A failure leaves what it found.** On any status but `Ok`, `result.Moles` and `result.Multipliers`
  are those of the entry.
- **Every `Ok` is stationary at the gas level** (the close guard, both paths): every gas reported with
  moles above zero has `|g_j/RT + ln(n_j/Σ_gas n) + ln(p/p°) − Σ_i a_ij π_i| ≤ 1e-9`.

## Dependencies

- [Equilibrium](../API.md) — the descriptors of its inputs, scratch and outputs, `ScratchLayout`,
  `DenseSolver`, `IterationState`, `SystemLayout`, `Composition`, `SpeciesMarks`, `ElementBalance`,
  `CaseSetup.LogPressure` and the constants of `EquilibriumSolver`.
- [Condensed](../Condensed/API.md) — `CondensedSet.Update`.
- [GasPhase](../GasPhase/API.md) — `GasPhaseVerdict.PhaseOnePoint`.
- [Thermo](../../Thermo/API.md) — the species table view, `CaseStatus`, `ProblemKind`, `KernelMath`,
  `PhysicalConstants`.

Outside the tree: ILGPU 1.5.3 (`ArrayView<T>`). The parent's internal types this node uses (parent
internals) are not in the parent's tree contract; the child belongs to the parent's assembly.

## Constraints

Inherited from the parent ([BOOT.md](../BOOT.md)) and, through it, from the root. In addition:

- Every type is `internal`, under `APThermo.Equilibrium.TraceGas`, with no project of its own.
- The root's code-shape constraint applies; no row is declared. Kernel-compatible C#, the math list
  (`Exp`, `Log`, `Sqrt`, `Abs`, `KernelMath.Min` and `Max`), and an ordered comparison with a
  constant keeps it on the right.
- **Reached once.** `TraceGasPass.Run` and `TraceGasIteration.Converge` carry
  `[MethodImpl(MethodImplOptions.NoInlining)]`, each with one call site (`EquilibriumSolver.Solve`,
  and the sequence of `Run`), as the root's compile-size constraint asks; so does
  `GasPhaseVerdict.PhaseOnePoint`, called from `PhaseOneSeed.Fetch` alone.
- **No whole `IterationState` through `ref`.** The entry is kept field by field. A build that
  assigned the whole struct through `ref` failed ptxas on CUDA on 2026-10-04: "vector with elements
  of different types" in `st`. That the copy caused it is inferred; the GPU run did not separate
  the causes. The rule of [Recovery/BOOT.md](../Recovery/BOOT.md), "No whole-struct copies", holds
  here as there: no struct of this node holds two `bool` fields next to each other.
- **The unknowns and rows.**
  - Unknowns: `dπ_i`, `u_c = dn_c/n`, `δ = dn/n`, and `τ = d ln T` for hp and sp.
  - Element row i, divided by n: `Σ_j a_ij x_j (Σ_k a_kj dπ_k + δ) + Σ_c a_ic u_c` (plus the
    temperature terms) `= (b_i − Σ_j a_ij n_j)/n`. A ridge of 1e-12 times the row's largest entry
    sits on the π diagonal, toward the present π. An absent element is a unit row.
  - Condensed row c: `Σ_i a_ic dπ_i = g_c/RT − Σ_i a_ic π_i` (plus `h_c τ`).
  - Phase-sum row: `Σ_j (x_j/S)(Σ_i a_ij dπ_i) = −ln S`, with `S = Σ_j x_j` over every gas in play.
  - hp and sp add the temperature column and the enthalpy or entropy row.

  The matrix does not depend on n, so it stays regular as the gas vanishes.
- **The step.**
  - `λ = min(1, 2 / max |Δ ln x_j|)` over the gases above the first retention stage, with `5|τ|`
    among them. The others obey the bound of equation (3.2) at `−ln 1e-4`.
  - `π += λ dπ`, `n_c += λ n u_c`, `n ← max(n (1 + λ δ), 1e-3 n)`, `ln T += λ τ`.
  - A temperature outside `[100 K, 20 000 K]` is `TemperatureOutOfRange`.
  - A bound of ±2 on `Δ ln n` was measured and rejected (79 `Ok` lost in the scans, 2026-10-04).
- **Converged** when a step with `λ = 1` leaves weighted corrections and `|τ|` at or below 1e-11,
  and the next evaluation finds every active element within `1e-13 · max(1, b_i)` and
  `|ln S| ≤ 1e-10`. Then `LogN = ln n + ln S` and the second retention stage holds.
- **Caps.** At most 150 steps per convergence; 50 left 18 of 19 CaCO3 states near their plateau
  unconverged (2026-10-04). A fraction above `e^300` or an `S` that is not finite is `NotConverged`;
  a singular matrix is `SingularMatrix`. Either ends the start.
- **The sequence.**
  - Converge, `Composition.Refresh`, `CondensedSet.Update`.
  - A change counts in `SetChanges` against `MaxCondensedSetChanges`. It is followed by the
    projection from the present iterate and another convergence.
  - No change ends the start `Ok`.
- **The starts**, in this order, each from the entry:
  1. **The projection.** π is the weighted least squares of the stationarities of the gases the
     entry reports: moles above zero and above the second retention stage, weight `x_j`. The
     entry's condensed species are rows of weight 1. A ridge pulls toward the anchor, and n is the
     reported gas. `scratch.LogMoles` does not select the gases, because `CaseSetup.Begin` writes an
     estimate there for every gas it was not given.
  2. **The phase-one point, own amounts.** The point is that of `GasPhaseVerdict.PhaseOnePoint`
     when its residual is positive, otherwise the entry's condensed species. From it:
     - π moves the least distance from the anchor onto the condensed stationarities;
     - π then moves along the unit excess `r = b − A n_c` until `|ln S| < 1e-12` (at most 60 Newton
       steps on `ln S`);
     - `n = (g · r)/(g · g)` with `g_i = Σ_j a_ij x_j`.

     The start is skipped when `r` vanishes.
  3. **The same species, least-squares amounts** (`Aᵀ A n_c = Aᵀ b`), then as in 2.
  4. **The phase-one point with its records at or below 1e-12 kmol/kg dropped**, least-squares
     amounts, then as in 2. It runs only when `PhaseOnePoint` completed.

  `PhaseOnePoint` is asked once per pass, before start 2. Measured on 2026-10-04 by an emulation of
  seam (a) over 4 357 tp states of five scans:
  - 438 failures `Ok`, 1 704 certified gasless first, 3 left `NotConverged`;
  - no `Ok` lost, no record line moved;
  - without starts 2 to 4, every state whose failed iterate held no condensed species failed.
- **The anchor and the entry.**
  - At entry, `Run` saves `result.Moles` in `scratch.Tie.LogMoles`, and takes the anchor multipliers
    from `scratch.Tie.Elements.Multipliers` into `result.Multipliers` (a multiplier that is not
    finite anchors at zero). The verdict leaves the multipliers it restored there (seam (a));
    `BracketSeeds` leaves the seeding probe's (seams (b) and (b′)).
  - The phase-one point is kept in `Tie.CondensedMoles` and `Tie.CondensedSet`. Its simplex
    overwrites `Tie.LogMoles`, so the entry's moles are put aside in `scratch.LogMoles` for it and
    back, and `RestoreEntry` follows it.
  - A failed pass restores moles and multipliers.
  - The node has no slice of its own and never touches `BracketEnds`.
- **No tie, no remedy.** A pass keeps no rule-A tie and runs no singular remedy. A direction of π
  that no reported gas fixes is held by the ridge, and then fixed by the exact fractions of every
  gas in play. The unit row the derivatives need there is found at the close
  ([StateRecord](../StateRecord/BOOT.md)).
- **The close guard** (`TraceGasPass.Stationary`, called by the parent's close after the element
  invariant and the exit guard, `NotConverged` when false). The bound is 1e-9:
  - over the 17 356 closes of the Equilibrium tests the largest residual was 1.95e-11;
  - a trace-gas convergence leaves at most 1e-10;
  - the false `Ok`s found carry 1.7e-8 to 2.2e-5.

  The residual is then the share of gas the iterate's n leaves out, so the guard bounds a stray
  carrier at 1e-9 of the gas. In force, it refused:
  - no state of any test node's record;
  - 8 false `Ok`s, and 5 states whose gas (1e-6 of the mixture or less) had converged only to 3e-8
    to 5e-8, which the pass and the bracket now converge.

## Structure

One type per file:

| Type | Kind | Holds |
|---|---|---|
| `TraceGasPass` | static | `Run`, `Stationary`, the sequence |
| `TraceGasStart` | static | the entry's save and restore, the projection |
| `PhaseOneSeed` | static | the point's fetch and load, starts 2 to 4 |
| `TraceGasIteration` | static | `Converge` |
| `TraceGasSystem` | static | `Assemble`, `Solve`, the energy row |
| `TraceGasStep` | static | the fractions, the control factor, the weighted corrections, the step, the balance |
| `TraceGasFrame` | readonly struct | the layout, `ln(p/p°)`, n, S and T, which keeps every method within six parameters |

Each constant of `## Constraints` is named in the class that uses it.

## Acceptance criteria

- [ ] A trace carrier's walk (MgCO3 under CO2, Mg:C:O = 1:2:5, 10 MPa, cold tp every 5 K from 700 to
      900 K):
      - every state `Ok`, clear of `EquilibriumConditions` at 1e-9;
      - CO twice O2 to the balance of the combination O − Mg − 2C, 3 · 1e-12 kmol/kg;
      - red on the old code: 25 of 41 `NotConverged`.

      ⚠ 2026-10-04: was "x_CO = 2 x_O2 within 1e-6", now the balance of the combination: the carriers
      are 1e-11 to 1e-9 of the gas, the balance holds to the element invariant, and the ratio measured
      1.9e-2 off at 765 K and 3e-5 at 800 K.
- [ ] Trace excesses: CaCO3 + 1e-8 to 1e-5 CO2 (1 kPa to 10 MPa, 0.01 to 300 K below the plateau),
      Al2O3 + 1e-10 and 1e-8 O (1 000 to 3 000 K), KCl + 1e-10 Cl (1 000, 1 200 K), CaCO3 + 1e-6 O
      (300 K) and MgCO3 + 1e-6 CO2 below its plateau, cold tp:
      - every state `Ok` and clear at 1e-9;
      - red on the old code;
      - starts 2 to 4 each shown necessary by removing them.
- [ ] The residue: the exact and ±1e-12 states of the scans end `NoGasPhase`, or `Ok` with a gas of
      1e-12 kmol/kg or more, apart from the declared leftovers.
- [ ] The close guard:
      - a unit fact at 5e-10 and 2e-9;
      - the 13 states it refused in the scans end `Ok` at the tp temperature within 1e-9 and clear
        at 1e-9 (hp and sp through the bracket);
      - red with the guard removed.
- [ ] Host units:
      - the matrix equal at n and at 1e-12 n;
      - the control factor on hand-built corrections;
      - a failed pass leaving `Moles` and `Multipliers` bit-equal to its entry;
      - the iterations of a case the sum of its attempt's and its pass's.
- [ ] A `LongRunning` scan fact over the four scan families:
      - every `Ok` clear at 1e-9;
      - no `Ok` of the code before the pass lost;
      - the `NotConverged` tp states printed, and the declared leftovers only.
- [ ] No line of an `Ok` case moved in any `Bits*.approved.txt`, Windows and Linux; every changed line
      was a failure before and is listed.
- [ ] CUDA on the reference machine:
      - the families `trace-gas-magnesite-1e7`, `trace-gas-excess` and `trace-gas-hp` equal to the
        CPU within the tier;
      - `LaunchBudget` with `trace-gas-hp`;
      - the rocket kernel's compile within its bound, its figure recorded (765 652 424 bytes for the
        emulation);
      - the fast set within 5 minutes; WSL green.

## Declared leftovers

The tp states of the scans that the pass does not settle are declared in `TraceGasLeftovers.txt` of
[the tests node](../../../tests/Equilibrium.Tests/BOOT.md), one name per line, kind `notconverged`
(as before the pass) or `residue` (`Ok` with less than 1e-12 kmol/kg of gas, through the pass: the
verdict's face search found no certificate). 2026-10-04, the design's three and two more of the
second kind, to be confirmed by the measurement of the whole scan.

## Taboos

- No equilibrium constant and no equation beyond the reduced iteration's.
- No pass without the verdict's `GasRequired` or a bracket's final.
- No second call site of `Run`, `Converge` or `PhaseOnePoint`.
- No write to `BracketEnds`, and no whole `IterationState` assigned through `ref`.
- No public type; no `float`, exception, allocation or virtual call.

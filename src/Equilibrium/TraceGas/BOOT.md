# BOOT.md — Equilibrium.TraceGas

## Purpose

A child node of `src/Equilibrium` (its `BOOT.md`, `## Structure`), born 2026-10-04 for 0.2.2. It
converges a case whose gas phase, or one direction of whose multipliers, is carried by amounts too
small for the reduced iteration of RP-1311, and it states what every `Ok` must satisfy at the gas
level:

- `TraceGasPass`: `Run`, one trace-gas pass (its starts, its convergences and condensed-set changes,
  the entry restored on failure).
- `TraceGasStart` and `PhaseOneSeed`: where a pass starts.
- `TraceGasIteration`: one convergence of one condensed set.
- `TraceGasSystem` and `TraceGasStep`: its matrix and its step; `TraceGasStep.Stationary` is the
  close guard (it sits there because `TraceGasPass` stands at the limit of 14 names).
- `TraceGasReport`: which gases below the second retention stage an `Ok` still reports.
- `DataJunction` and `JunctionPin`: the pin of an hp or sp convergence at an interval bound of the data.

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
- **An `Ok` reports its balance carriers** (2026-10-04, `TraceGasReport`). Every gas below the
  second retention stage whose atoms exceed `1e-16 · b_i` of some active element stays at its
  converged amount (`IterationState.TraceCarriers` counts them; `RetainedSetHeld` keeps them through
  the refresh); the others are zeroed. The dropped ones sum to under `1e-16 · b_i` times their
  number, a tenth of the invariant at the most. The converged iteration held every gas, so a gas the
  report drops takes its atoms out of the balance it closed: Al(OH)3 + 1e-6 O at 10 MPa and 800 K
  missed H by 3.8e-13 kmol/kg (9.8e-12 of b_H) with H2 and Al(OH)3 at 5e-12 and 2e-12 of the gas
  dropped. The derivatives read no carrier ([StateRecord](../StateRecord/BOOT.md)); the sums of h, s
  and M include them.

  ⚠ 2026-10-04: was "the moles refreshed at the second retention stage" alone, now the carriers held
  too: the report dropped every gas below the stage.
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
    temperature terms) `= (b_i − Σ_j a_ij n_j)/n`. A ridge of 1e-13 times the row's largest entry
    sits on the π diagonal, toward the present π (1e-12 stalled the H2O ± 1e-12 states at a balance of
    6.7e-13: a carrier at 1e-12 of the gas has a curvature at the ridge's own scale; 1e-15 lost H2O at
    step 3, `SingularMatrix`). An absent element is a unit row.
  - Condensed row c: `Σ_i a_ic dπ_i = g_c/RT − Σ_i a_ic π_i` (plus `h_c τ`).
  - Phase-sum row: `Σ_j (x_j/S)(Σ_i a_ij dπ_i) = −ln S`, with `S = Σ_j x_j` over every gas in play.
  - hp and sp add the temperature column and the enthalpy or entropy row. The column is divided by its
    largest entry when that exceeds one (the unknown is then `scale · τ`, and `Solve` scales it back):
    it holds the h/RT of the species, tens to hundreds, and a direction of π that only trace gases
    carry sits at 1e-10 beside it, whose pivot would fall under the dense solver's 1e-13 of the
    row's largest entry although the system is regular (2026-10-04: without the scaling every hp and
    sp trace-gas final ends `SingularMatrix`, 24 facts of this node red).

  The matrix does not depend on n, so it stays regular as the gas vanishes.
- **The step.**
  - `λ = min(1, 2 / max |Δ ln x_j|)` over the gases above the first retention stage, with `5|τ|`
    among them. The others obey the bound of equation (3.2) at `−ln 1e-4`.
  - `π += λ dπ`, `n_c += λ n u_c`, `n ← max(n (1 + λ δ), 1e-3 n)`, `ln T += λ τ`.
  - A temperature outside `[100 K, 20 000 K]` is `TemperatureOutOfRange`.
  - A bound of ±2 on `Δ ln n` was measured and rejected (79 `Ok` lost in the scans, 2026-10-04).
- **Converged** when a step with `λ = 1` leaves weighted corrections and `|τ|` at or below 1e-11,
  and the next evaluation finds every active element within `3e-14 · b_i` (0.3 of the node's
  invariant, so that the report's rounding stays inside it) and `|ln S| ≤ 1e-10`. Then
  `LogN = ln n + ln S`, the second retention stage holds and the balance carriers are reported.

  ⚠ 2026-10-04: was `1e-13 · max(1, b_i)`, now `3e-14 · b_i`: the old test was an absolute 1e-13
  kmol/kg for every real mixture, 6.4e-12 of the carbon of MgCO3, and `magnesite-band|700` closed
  carbon to 3.5e-12 with CO five times its equilibrium.
- **Caps.** At most 150 steps per convergence; 50 left 18 of 19 CaCO3 states near their plateau
  unconverged (2026-10-04). A fraction above `e^300` or an `S` that is not finite is `NotConverged`;
  a singular matrix is `SingularMatrix`. Either ends the start.
- **The sequence.**
  - Converge, `Composition.Refresh`, `CondensedSet.Update`.
  - A change counts in `SetChanges` against `MaxCondensedSetChanges`. It is followed by room for
    the gas (below), the projection from the present iterate and another convergence.
  - No change ends the start `Ok`.
- **The starts**, in this order, each from the entry:
  1. **The projection.** π is the weighted least squares of the stationarities of the gases the
     entry reports: moles above zero and above the second retention stage, weight `x_j`. The
     entry's condensed species are rows of weight 1. A ridge pulls toward the anchor, and n is the
     reported gas. `scratch.LogMoles` does not select the gases, because `CaseSetup.Begin` writes an
     estimate there for every gas it was not given.
  2. **The phase-one point, own amounts.** The point is that of `GasPhaseVerdict.PhaseOnePoint`
     whenever it completed, a residual of zero included (F1, 2026-10-04: it was the entry's
     condensed species when the residual was zero, so a failed iterate with none started the pass
     all-gas and every inclusion had to bring the whole condensed mass in from zero). From it:
     - π moves the least distance from the anchor onto the condensed stationarities;
     - π then moves along the unit excess `r = b − A n_c` until `|ln S| < 1e-12` (at most 60 Newton
       steps on `ln S`);
     - `n = (g · r)/(g · g)` with `g_i = Σ_j a_ij x_j`.

     The start is skipped when `r` vanishes.
  3. **The same species, least-squares amounts** (`Aᵀ A n_c = Aᵀ b`), then as in 2.
  4. **The phase-one point with its records at or below 1e-12 kmol/kg dropped**, least-squares
     amounts, then as in 2. It runs only when `PhaseOnePoint` completed.

  **Room for the gas** (F2, 2026-10-04): beside a gas at an assigned temperature and pressure the phase
  rule allows at most (active elements − 1) condensed phases. When a loaded point, or the set after
  `CondensedSet.Update`, holds as many records as active elements, the record with the smallest positive
  amount leaves and its moles go to zero (`PhaseOneSeed.KeepRoomForTheGas`); the newcomer of an inclusion
  enters at zero and stays. The verdict has proved a gas required, and the record the gas replaces is the
  one that carries the excess, the smallest. It counts in `SetChanges`. The phase-one point of a
  `GasRequired` verdict is a vertex with as many records as elements, so with the gas added the system
  was over-determined: the condensed rows fixed every π and the phase-sum row was dependent
  (`SingularMatrix`; for KCl, whose gas is congruent with the liquid, the determinant was O(1e-20)).
  A projection of the excess off the condensed formulas was tried and rejected (22 states lost, CaCO3 + O
  among them). Removing F2 reds the KCl states; removing F1 reds `TraceExcessTests` and the scan facts.

  `PhaseOnePoint` is asked once per pass, before start 2. Measured on 2026-10-04 by an emulation of
  seam (a) over 4 357 tp states of five scans:
  - 438 failures `Ok`, 1 704 certified gasless first, 3 left `NotConverged`;
  - no `Ok` lost, no record line moved;
  - without starts 2 to 4, every state whose failed iterate held no condensed species failed.

  Measured on the code, the same day, over the 4 158 tp states of `TraceGasCases.ScanFamilies`
  (`TraceGasScanTests`, against `TraceGasScanBaseline.txt`, the code before the pass):
  - 315 failures `Ok`, 1 702 `NoGasPhase` from the verdict (all of them so before), 3 left
    `NotConverged`, 2 138 `Ok` unchanged, no `Ok` lost, no line of a bit record moved;
  - each start removed alone: start 2 reds 3 facts of `TraceExcessTests`, start 4 reds
    `ResidueVerdictTests`, start 3 reds the scan fact only; all three removed: 63 facts red.
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
- **The close guard** (`TraceGasStep.Stationary`, called by the parent's close after the element
  invariant and the exit guard, `NotConverged` when false). The bound is 1e-9:
  - over the 17 356 closes of the Equilibrium tests the largest residual was 1.95e-11;
  - a trace-gas convergence leaves at most 1e-10;
  - the false `Ok`s found carry 1.7e-8 to 2.2e-5.

  The residual is then the share of gas the iterate's n leaves out, so the guard bounds a stray
  carrier at 1e-9 of the gas. In force, it refused:
  - no state of any test node's record (no line of a `Bits*.approved.txt` moved);
  - the false `Ok`s of the design, and states whose gas (1e-6 of the mixture or less) had converged
    only to 3e-8 to 5e-8, which the pass and the bracket now converge to the tp state's temperature
    within 1e-9 (`GasStationarityTests`); removing the guard reds 81 facts of this node.
  - A state at the junction of the data's two temperature ranges, 1 000 K, has no hp state within the
    data's own jump of 9e-9 in ln x: the NASA fits are not continuous there. The data junction, below,
    pins it.

  ⚠ 2026-10-04: was "KCl + 1e-6 Cl at 100 kPa and 1 000 K, hp cold, warm and warm from 5 K above, ends
  `NotConverged` after the trace-gas final: declared", now `Ok` at the junction itself.
- **The data junction** (2026-10-04, `DataJunction`, `JunctionPin`). An internal interval bound T_J of
  a species' fits, where the fits of two ranges disagree by the data's own jump (ΔG/RT 1e-8 to 1e-9
  at 1 000 K, 1 531 of the gas junctions of `thermo.inp`), is a defect of the fits, not physics; any
  state between T_J and the next double is the equilibrium to the data's precision.
  - **Detection.** In an hp or sp convergence, a temperature step that crosses an interval bound of
    a species in play (a gas in play, or a condensed species of the solution) again, after the
    previous crossing crossed the same bound, with `|τ| ≤ 1e-7`, is dithering across it. The bound is
    found by a bisection on `SpeciesFunctions.IntervalOf` between the two temperatures (at most 64
    halvings, ending on adjacent doubles): T_J is the largest double on the lower interval, T_J⁺ the
    next, the smallest on the upper. No contract of `Thermo` is read beyond `IntervalOf`.
  - **Pinned.** The convergence then drops the temperature unknown and the energy row and continues as
    a tp convergence: at T_J (the lower interval's functions), then at T_J⁺ from that iterate.
  - **The nearer of the two** (decision of the owner, 2026-10-04, option (B); proposed was T_J alone,
    refused was `NotConverged`). The answer is the tp state whose h (hp) or s (sp) is nearer the
    target, T_J on a tie: when the upper state is nearer it is kept, otherwise a third tp convergence
    returns to T_J. The reported h or s differs from the target by at most the jump of the mixture's
    h or s at T_J: measured at most 8.3e-9 of c_p T for gas mixtures at 1 000 K, nil for the KCl state.
  - **The bound 1e-7** sits 12 times above the largest measured mixture jump and above the steps of
    the dither (1e-15 to 1e-8). A mixture dominated by a species whose junction jumps h by 1e-4 of c_p T
    (ALOCL, SnCL2 at 1 000 K) is not pinned and stays `NotConverged`: declared.
  - **Only the trace-gas iteration pins.** The reduced iteration of the Newton loop keeps its bits;
    its junction failures are refused by the close guard and reach this iteration through the
    bracket's finals. Measured over 822 hp and sp solves at the junction (nine gas systems at three
    pressures, five temperatures at and about 1 000 K, six modes, with KCl + 1e-6 Cl): none ends
    otherwise than `Ok`; 77 are pinned, 22 end at T_J and 55 at T_J⁺, the other 745 converge freely.
## Structure

One type per file:

| Type | Kind | Holds |
|---|---|---|
| `TraceGasPass` | static | `Run`, the sequence |
| `TraceGasStart` | static | the entry's save and restore, the projection |
| `PhaseOneSeed` | static | the point's fetch and load, starts 2 to 4 |
| `TraceGasIteration` | static | `Converge` |
| `TraceGasSystem` | static | `Assemble`, `Solve`, the energy row |
| `TraceGasStep` | static | the fractions, the control factor, the weighted corrections, the step, the balance, `Stationary` |
| `TraceGasReport` | static | `KeepBalanceCarriers`: what an `Ok` reports below the second retention stage |
| `DataJunction` | static | `Pins`, `Decides`, `Bound`: the data junction |
| `JunctionPin` | struct | what a convergence remembers of a junction: the last bound, T_J, T_J⁺, the first miss, the phase |
| `TraceGasFrame` | readonly struct | the layout, `ln(p/p°)`, n, S and T, which keeps every method within six parameters |

Each constant of `## Constraints` is named in the class that uses it.

## Acceptance criteria

- [x] A trace carrier's walk (MgCO3 under CO2, Mg:C:O = 1:2:5, 10 MPa, cold tp every 5 K from 700 to
      900 K):
      - every state `Ok`, clear of `EquilibriumConditions` at 1e-9;
      - CO twice O2 to the balance of the combination O − Mg − 2C, 1e-13 · (b_O + b_Mg + 2 b_C);
      - red on the old code: 25 of 41 `NotConverged`.

      2026-10-04, `TraceCarrierWalkTests`; red with seam (a) removed: 26 of its 42 facts.

      ⚠ 2026-10-04: was "x_CO = 2 x_O2 within 1e-6", now the balance of the combination: the carriers
      are 1e-11 to 1e-9 of the gas, the balance holds to the element invariant, and the ratio measured
      1.9e-2 off at 765 K and 3e-5 at 800 K.
- [x] Every `Ok` closes every element to the relative invariant (`1e-13 · b_i`):
      - the 72 plateau states of calcite and magnesite (1e4, 1e5, 1e6 Pa, hp and sp, three fractions,
        cold and seeded), the magnesite hp states among them, reached by the bracket's trace-gas final;
      - Al(OH)3 + 1e-6 O at 10 MPa and 800 K, whose dropped gases carried hydrogen.

      2026-10-04, `TraceGasClosureTests`, `ElementConservationTests`, `TraceCarrierWalkTests`; 60
      facts of the Equilibrium tests red on the absolute form, none on the relative; red with the
      carriers kept out of the derivatives removed: 58 facts, with the report of the carriers removed: 55.
- [x] Trace excesses: CaCO3 + 1e-8 to 1e-5 CO2 (1 kPa to 10 MPa, 0.01 to 300 K below the plateau),
      Al2O3 + 1e-10 and 1e-8 O (1 000 to 3 000 K), KCl + 1e-10 Cl (1 000, 1 200 K), CaCO3 + 1e-6 O
      (300 K) and MgCO3 + 1e-6 CO2 below its plateau, cold tp:
      - every state `Ok` and clear at 1e-9;
      - red on the old code;
      - starts 2 to 4 each shown necessary by removing them.

      2026-10-04, `TraceExcessTests` (79 states); red with seam (a) removed: 67 of 79; each start
      removed alone: start 2 reds 3 of these facts, start 4 reds `ResidueVerdictTests`, start 3 the
      scan fact (`TraceGasScanTests`) alone.
- [ ] The residue: the exact and ±1e-12 states of the scans end `NoGasPhase`, or `Ok` with a gas of
      1e-12 kmol/kg or more, apart from the declared leftovers.
- [x] The close guard:
      - a unit fact at 5e-10 and 2e-9;
      - the states it refused in the scans (MgCO3 + 1e-6 CO2 below its plateau, and the loose `Ok`s of
        KCl, NaCl and KO2) end `Ok` at the tp temperature within 1e-9, clear at 1e-9, with the gas of
        their tp state within 1e-9 of its fractions (hp and sp, cold and warm, through the bracket and
        the trace-gas finals);
      - red with the guard removed.

      2026-10-04: `GasStationarityTests`, 124 facts (the unit fact at ±5e-10 and ±2e-9, and 120 states
      in six modes) and `TraceGasFinalTests`, all green; red with the guard removed: 81 facts; with
      (b′) removed: 37; with the temperature column unscaled: 24.

      ⚠ 2026-10-04: was ticked for all but the three hp modes of KCl + 1e-6 Cl at 100 kPa and 1 000 K,
      declared because no state of the data reaches them within its own jump of 9e-9 in ln x; now the
      data junction settles them and the box is ticked.
- [x] The data junction:
      - every hp and sp state, cold and warm, at the enthalpy and entropy of the tp states of nine gas
        systems at 1 kPa, 100 kPa and 10 MPa at 1 000 K, the double below and above it and 1 000 K times
        1 ± 1e-9 (135 states) ends `Ok`, clear at 1e-9, at the tp temperature within 1e-8 and within
        1e-7 of c_p T (hp) or c_p (sp) of the target;
      - a pinned state ends at the nearer of T_J and T_J⁺ (both outcomes occur), KCl + 1e-6 Cl at T_J
        exactly with h within one ulp of the target;
      - the search for the bound finds 1 000 K between its neighbouring doubles and the lowest of two bounds;
      - red with the pin removed: 27 of the 135 states and the KCl and the nearer-of-two facts.

      2026-10-04, `JunctionTests` (140 facts), `GasStationarityTests`; 77 of 822 solves pinned, 22 at
      T_J and 55 at T_J⁺.
- [x] Host units:
      - the matrix equal at n and at 1e-12 n;
      - the control factor on hand-built corrections;
      - a failed pass leaving `Moles` and `Multipliers` bit-equal to its entry;
      - the iterations of a case the sum of its attempt's and its pass's.

      2026-10-04, `TraceGasUnitTests` and `TraceCarrierWalkTests.TheIterationsOfACaseAreTheStepsOfItsAttemptAndOfItsTracePass`.
- [ ] A `LongRunning` scan fact over the four scan families:
      - every `Ok` clear at 1e-9;
      - no `Ok` of the code before the pass lost;
      - the `NotConverged` tp states printed, and the declared leftovers only.

      Written and green 2026-10-04 (`TraceGasScanTests`, 4 158 states, the three declared
      leftovers); left unticked for the decision on those three (`## Declared leftovers`).
- [ ] No line of an `Ok` case moved in any `Bits*.approved.txt`, Windows and Linux; every changed line
      was a failure before and is listed.

      Windows, 2026-10-04: the fast set of every test node green with no record changed. Linux: not
      run here (no WSL for this agent); no `Bits.linux.approved.txt` was touched, and none needs
      re-approval unless the WSL run of the orchestrator moves a line.
- [ ] CUDA on the reference machine:
      - the families `trace-gas-magnesite-1e7`, `trace-gas-excess` and `trace-gas-hp` equal to the
        CPU within the tier;
      - `LaunchBudget` with `trace-gas-hp`;
      - the rocket kernel's compile within its bound, its figure recorded (765 652 424 bytes for the
        emulation);
      - the fast set within 5 minutes; WSL green.

## Declared leftovers

The states of the scans that the pass does not settle are declared in `TraceGasLeftovers.txt` of
[the tests node](../../../tests/Equilibrium.Tests/BOOT.md), one name per line, kind `notconverged`
(as before the pass) or `residue` (`Ok` with less than 1e-12 kmol/kg of gas, through the pass: the
verdict's face search found no certificate). Measured on the code, 2026-10-04:
- `NotConverged` tp states: KCl − 1e-10 Cl at 1 200 K and 1 kPa and at 1 500 K and 100 kPa left the list
  on 2026-10-04 (room for the gas). Al(OH)3 − 1e-12 O at 300 K and 1 kPa: the verdict answers `GasRequired`
  with residual 0, and every start ends `SingularMatrix` after one change of the condensed set.
- Under investigation (2026-10-04), the owner has not decided: eight tp states of the trace-excess scan
  (`TraceGasCases.TraceScan`, 1 632 states at ± 1e-8 and ± 1e-10) that no mechanism settles, and
  `Al(OH)3|-1E-12|100000|500`, a false `Ok` before the relative invariant (H open by 3.2e-12 of its
  abundance) and `NotConverged` since. The eight: Al(OH)3 − 1e-8 O at 500 K and 1 kPa, Al(OH)3 − 1e-10 O
  at 500 K and 100 kPa, Li2O + 1e-10 O at 800 K and 1 kPa and 100 kPa, CaCO3 + 1e-8 O at 500 K and 1 kPa
  and at 800 K and 1 kPa, 100 kPa and 10 MPa. Each is an excess of the anion-forming element beside its
  oxide, or an Al(OH)3 deficit. In the trace of the related KCl + 1e-12 Cl at 800 K every step moves π
  toward a K-rich gas while n collapses by the positivity floor, and starts 2 to 4 end at step 0 (S not
  finite after the placement along the excess). A follow-up investigation with a different start.
- `Ok` with a residue of gas, the verdict not certifying Al(OH)3 at 300 K and 1 kPa gasless: the
  exact state (1.3e-18 kmol/kg of gas) and the + 1e-12 state (3.1e-14), both through start 4.
- hp, three modes of KCl + 1e-6 Cl at 100 kPa and 1 000 K left the list on 2026-10-04: the data junction
  settles them (above).
- The 49 further tp states at excesses of 1e-8 and 1e-10 that ended `NotConverged` (K2O, Li2O, MgO,
  Al(OH)3, thermite, CaCO3) are walked now (`TraceGasScanTests`, the trace-excess scan): all end `Ok` or
  `NoGasPhase` but the eight above, which stay.

## Taboos

- No equilibrium constant and no equation beyond the reduced iteration's.
- No pass without the verdict's `GasRequired` or a bracket's final.
- No second call site of `Run`, `Converge` or `PhaseOnePoint`.
- No write to `BracketEnds`, and no whole `IterationState` assigned through `ref`.
- No public type; no `float`, exception, allocation or virtual call.

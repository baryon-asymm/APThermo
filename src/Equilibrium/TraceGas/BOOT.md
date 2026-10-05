# BOOT.md — Equilibrium.TraceGas

## Purpose

A child node of `src/Equilibrium` (its `BOOT.md`, `## Structure`), born 2026-10-04 for 0.2.2. It
converges a case whose gas phase, or one direction of whose multipliers, is carried by amounts too
small for the reduced iteration of RP-1311, and it states what every `Ok` must satisfy at the gas
level:

- `TraceGasPass`: `Run`, one trace-gas pass (its starts, its convergences and condensed-set changes,
  the entry restored on failure).
- `TraceGasStart`, `PhaseOneSeed` and `GasBasisSeed`: where a pass starts.
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
  `GasPhaseVerdict.PhaseOnePoint`, called from `PhaseOneSeed.Point` alone, which `PhaseOneSeed.Fetch` (the
  records) and `GasBasisSeed.Place` (the records and the gases) call.

  ⚠ 2026-10-05: was "called from `PhaseOneSeed.Fetch` alone", now from `PhaseOneSeed.Point`, whose two callers
  are the phase-one point and the gas basis; still one call site and one compiled copy (the rocket kernel's
  compile on the CPU accelerator allocated 1.008 to 1.010 GB before, 1.012 to 1.015 GB after).
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
  5. **The gas basis** (2026-10-05, `GasBasisSeed`), after Reynolds's STANJAN initializer (1986, Sec. 7):
     `PhaseOnePoint` over the records and every gas, a gas a column at unit fraction with the cost
     `g_j/RT + ln(p/p°)`. Its optimal basis is the start:
     - the condensed set is the condensed basics, one at zero level included at zero moles (the balancing
       phase: CaO beside CaCO3 under an excess of oxygen);
     - π solves the stationarities of the basis: `g_c/RT` for a record, `g_j/RT + ln(p/p°) + ln(v_j/n)` for
       a basic gas of amount `v_j`, at unit fraction for one at zero level;
     - n is the sum of the basic gases; the start is skipped when an artificial column stays basic or the basis
       is singular;
     - **the mixture column** (2026-10-05): when no gas is basic and `ln S0 > 0` at the multipliers π0 of the
       basis, every gas lies below unit fraction while their mixture lowers the Gibbs energy. The mixture
       `y_j = x_j(π0)/S0` is the column of least reduced cost, `−ln S0`, and enters by one pivot: `B w = A y`;
       the record it replaces is the least `v_k/w_k` over `w_k > 1e-11` (the lowest row on a tie); n is that
       ratio, the other basics move to `v − n w`; π solves the remaining records' stationarities and the
       mixture's, `y·Aπ = y·Aπ0 − ln S0`, where `S ≥ 1` by Jensen's inequality and is 1 to second order. The
       start is skipped when a basic is a gas or an artificial, `ln S0 ≤ 0`, no record leaves at a positive
       ratio, or a system is singular. The case is the band above a transition where a gasless assemblage
       gives way to a gas of several species: CaCO3 − 1e-7 O is CaCO3(cr) + CaO(cr) + C(gr) until `ln S0` of
       that vertex reaches zero, at T_t = 848.153 K at 1 kPa, and CaCO3(cr) + CaO(cr) under CO and CO2 above it;
       until CO alone reaches unit fraction at the vertex (854.3 K) the program holds no gas.

     ⚠ 2026-10-05: was "the start is skipped when no gas is basic with a positive amount", now the gas mixture
     enters as one column then. The states of the band ended `NotConverged`: starts 1 to 4 dropped CaO for the
     gas (room for the gas, below: CaO and C(gr) tie at 1.5e-7 b_Ca), the projection after its re-inclusion
     overflowed `S`, and the gas basis was skipped.

     Every gas then follows from π, and the excess sits in its carrier on its own branch. With the records
     fixed the gas must satisfy `n F(π) = b*` and `Σ_j x_j = 1`; along a free direction of π the sum is
     convex, so it has two roots, and only the one where `F` has the sign of `b*` gives a positive n. The
     start runs last. As the first start it settles the same states and moves the bits of 728 `Ok` states of
     the scans; in place of starts 2 to 4 it loses 7 `Ok` states (CaCO3 + 1e-6 and 1e-10 O and Al(OH)3 at
     300 K), alone 9. Measured 2026-10-05 (the scans on the prototype of this code, the rest on it):
     - the 5 795 tp states of the two scan facts and `LooseOks`: the nine declared `NotConverged` end `Ok`
       and clear, no other state changes its status or a bit;
     - 7 968 further tp states, the 17 systems and the binary scan at ±1e-14 to ±1e-1: 13 of 14
       `NotConverged` end `Ok`, nothing else moves;
     - removing the gas columns reds the nine and both scan facts. Neither the zero-level record in the set
       nor the shares of the basic gases is needed for the status: without the record the CaCO3 pass takes
       4 to 20 steps instead of 1 to 3, without the shares the Al(OH)3 pass 22 to 29 instead of 1.

     The mixture column, measured 2026-10-05 on its prototype, each state solved with the column and without:
     - 393 807 tp states of the 17 systems at ±1e-6 to ±1e-10, 100 Pa to 10 MPa, 300 to 3 000 K, refined to
       0.01 K wherever the status or the condensed set changes: 1 122 `NotConverged` end `Ok` and clear (CaCO3 −
       1e-7 O at 100 Pa and 1 kPa, NaCl − 1e-6 Cl at 100 kPa and 1 160 K), nothing else changes a status or a bit;
     - 127 143 states of CaCO3, MgCO3 and CaCO3 with its carbon shifted, at 1e-6 to 1e-10 either way, 100 Pa to
       10 MPa, 300 to 1 600 K: 22 787 `NotConverged` end `Ok` and clear (CaCO3 − 1e-7 and − 3e-8 O at every
       pressure from 100 Pa, the band 5 to 16 K wide), five MgCO3 states stay `NotConverged` as before (declared
       leftovers, below), nothing else moves;
     - the record left by the smallest amount instead of the ratio test: 1 120 of the 1 122 stay `NotConverged`;
       π0 kept instead of the mixture's row: no status changes, 0.7 more steps on average.

  **Room for the gas** (F2, 2026-10-04): beside a gas at an assigned temperature and pressure the phase
  rule allows at most (active elements − 1) condensed phases. When a loaded point, or the set after
  `CondensedSet.Update`, holds as many records as active elements, the record with the smallest positive
  amount leaves and its moles go to zero (`PhaseOneSeed.KeepRoomForTheGas`); the newcomer of an inclusion
  enters at zero and stays. The verdict has proved a gas required, and the record the gas replaces is
  usually the one that carries the excess, the smallest. It counts in `SetChanges`.

  ⚠ 2026-10-05: was "the record the gas replaces is the one that carries the excess, the smallest", now
  usually: for CaCO3 − ε O, CaO(cr) and C(gr) tie at 1.5 ε b_Ca and the gas replaces C(gr); the rule dropped
  CaO and the start failed. The ratio test of the mixture column (start 5) chooses by the gas's composition. The phase-one point of a
  `GasRequired` verdict is a vertex with as many records as elements, so with the gas added the system
  was over-determined: the condensed rows fixed every π and the phase-sum row was dependent
  (`SingularMatrix`; for KCl, whose gas is congruent with the liquid, the determinant was O(1e-20)).
  A projection of the excess off the condensed formulas was tried and rejected (22 states lost, CaCO3 + O
  among them). Removing F2 reds the KCl states; removing F1 reds `TraceExcessTests` and the scan facts.

  `PhaseOnePoint` is asked at most twice per pass: over the records before start 2, over the records and the
  gases at start 5.

  ⚠ 2026-10-05: was "asked once per pass, before start 2", now twice: the gas basis asks the program with
  the gases.

  Measured on 2026-10-04 by an emulation of
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
| `PhaseOneSeed` | static | the point's fetch and load, starts 2 to 4, `Point`: the one call site of `PhaseOnePoint` |
| `GasBasisSeed` | static | `Place`: start 5, the gas basis, and its mixture column |
| `TraceGasIteration` | static | `Converge` |
| `TraceGasSystem` | static | `Assemble`, `Solve`, the energy row |
| `TraceGasStep` | static | the fractions, the control factor, the weighted corrections, the step, the balance, `Stationary` |
| `TraceGasReport` | static | `KeepBalanceCarriers`: what an `Ok` reports below the second retention stage |
| `DataJunction` | static | `Pins`, `Decides`, `Bound`: the data junction |
| `JunctionPin` | struct | what a convergence remembers of a junction: the last bound, T_J, T_J⁺, the first miss, the phase |
| `TraceGasFrame` | readonly struct | the layout, `ln(p/p°)`, n, S and T, which keeps every method within six parameters; `AtStart`, the frame a start is placed in |

Each constant of `## Constraints` is named in the class that uses it.

## Acceptance criteria

→ [ACCEPTANCE.md](ACCEPTANCE.md)

## Declared leftovers

The states of the scans that the pass does not settle are declared in `TraceGasLeftovers.txt` of
[the tests node](../../../tests/Equilibrium.Tests/BOOT.md), one name per line, kind `notconverged`
(as before the pass) or `residue` (`Ok` with less than 1e-12 kmol/kg of gas, through the pass: the
verdict's face search found no certificate). Measured on the code, 2026-10-04:
- `NotConverged` tp states: KCl − 1e-10 Cl at 1 200 K and 1 kPa and at 1 500 K and 100 kPa, and
  Al(OH)3 − 1e-12 O at 300 K and 1 kPa, left the list on 2026-10-04 (room for the gas, and for the
  Al(OH)3 state the gasless write: its phase-one point needs the records at zero level). Every start
  of the three had ended `SingularMatrix` after one change of the condensed set.
- Settled on 2026-10-05 by the gas basis (start 5): the eight tp states of the trace-excess scan and
  `Al(OH)3|-1E-12|100000|500` declared under investigation. Three mechanisms, traced on 7c921f02:
  - Li2O + 1e-10 O at 800 K: every start reached the metal-rich root of `Σ_j x_j = 1` (`F` < 0); the step
    then asked `δ` of −1, −88, −9e4 and on, and n fell by the positivity floor, 3.3e-6 to 5e-322;
  - CaCO3 + 1e-8 O at 500 and 800 K: the starts put O2 on its branch, but beside CaCO3 alone no gas above
    e^−72 carried π_Ca − π_C and the first step was `SingularMatrix`; the balancing CaO (1.3e-26 to 5.7e-12
    kmol/kg) was in no start's set;
  - Al(OH)3 − 1e-8 to − 1e-12 O at 500 K: the gas is the water, 1.9e-2 kmol/kg, not a trace; the phase-one
    point held it as H2O(L), the projection as AL(OH)3 gas, and the iteration, which drops no record within
    a convergence, diverged (condensed amounts of −2e64).

  ⚠ 2026-10-05: was "under investigation (2026-10-04), the owner has not decided ... in the trace of the
  related KCl + 1e-12 Cl at 800 K every step moves π toward a K-rich gas while n collapses", now settled;
  that mechanism held for Li2O alone, and KCl + 1e-12 Cl at 800 K has ended `NoGasPhase` since the gasless
  write (K over by 1.3e-14 kmol/kg, inside the verdict's 1e-12).
- Closed 2026-10-05 by the balancing record ([Condensed/BOOT.md](../Condensed/BOOT.md), `## Constraints`): CaCO3 + 1e-7 O at
  10 MPa and 300 K, CaCO3 + 1e-9 O at 1 MPa and 300 K and CaCO3 + 1e-6 O at 10 MPa and 350 K ended `NotConverged` after
  every start. The gas basis converged with the balancing CaO at −5e-48 to −6e-43 kmol/kg (its amount, n x_CO2 ≈ 3e-34,
  lies below the rounding of the calcium balance, 1e-18), `CondensedSet.Update` removed it, and CaCO3 alone is singular as
  above. They end `Ok` and clear, CaO kept at zero in the set (`BalancingRecordTests`). `NoHiddenStateTests` no longer
  takes the first as its failure through the whole pass: it builds one, a table of CaCO3(cr) and O2 alone, `SingularMatrix`.

  ⚠ 2026-10-05: was "Known outside the scans: CaCO3 + 1e-7 O at 10 MPa and 300 K ends `NotConverged` after every start ...
  `NoHiddenStateTests` takes it as its failure", now closed by the rule above.
- Closed 2026-10-05 by the mixture column (start 5, above): CaCO3 − 1e-7 O at 1 kPa from T_t = 848.153 K to 854.3 K, the band
  where the gasless CaCO3(cr) + CaO(cr) + C(gr) has given way to CO and CO2 and no gas reaches unit fraction at its vertex; the same
  band lies above T_t at every pressure from 100 Pa (775.1 K) to 10 MPa (1 380.4 K), and with − 3e-8 O. Each state ends `Ok` and
  clear, CaCO3(cr) + CaO(cr) under 83 % CO at 850 K and 1 kPa, which cea 3.3.4 reproduces (CO2/CO 0.2032, CaO 3.61e-7 of Ca); cea
  finds no state below T_t, where this tree's is `NoGasPhase` (`GasMixtureStartTests`).

  ⚠ 2026-10-05: was "Known outside the scans: CaCO3 − 1e-7 O at 1 kPa at 849, 850 and 851 K ends `NotConverged` ... at 2 kPa the
  same temperatures end `NoGasPhase`", now closed; the band was 848.16 to 854.27 K, and at 2 kPa it lies at 873.0 to 879.5 K.
- Closed 2026-10-05 by the balancing record's last change ([Condensed/BOOT.md](../Condensed/BOOT.md)): MgCO3 + 1e-8 O at 10 MPa from
  334.8 to 336.8 K and at 1 MPa at 307.5 K, and + 1e-6 O at 10 MPa from 300.6 to 301.4 K, whose set alternated on MgO(cr) (n x_CO2 ≈
  5e-19 kmol/kg, below the rounding of the magnesium balance) until `MaxCondensedSetChanges`. They end `Ok` and clear, MgO(cr) at
  zero in the set, x_CO2 = 2.80e-9 at 335.09 K as cea 3.3.4 gives at 1e-6 (cea finds no state at 1e-8).

  ⚠ 2026-10-05: was "Known outside the scans: MgCO3 + 1e-8 O at 10 MPa from 335.09 to 336.66 K ends `NotConverged` … for the
  owner", now closed; the finer scan found the same alternation at 1 MPa and at 1e-6.
- Known outside the scans (2026-10-05): the supersaturated gas below a dead-end floor, NaCl at ±1e-6 to ±1e-10 Cl from 250 to 300 K
  (every pressure from 100 Pa to 10 MPa) and C:O = 2:1 at ±1e-6 to ±1e-10 from 254 to 273 K, 738 tp states of 401 000 walked: no
  condensed record is a candidate there (the range rule), the reduced iteration converges, and the close refuses the element
  invariant; the trace-gas pass reaches |ln S| of 5e-14 but not its balance test of 3e-14 · b_i in 150 steps, at multipliers of
  ±176 on a gas of Na3CL3 or C3O2 and C5, where ln x carries some 1e-13 of rounding. Neither alternation nor a condensed set is
  involved; unchanged by the mixture column and the last change. For the owner.
- `Ok` with a residue of gas, the verdict not certifying Al(OH)3 at 300 K and 1 kPa gasless: the
  exact state (1.3e-18 kmol/kg of gas), the + 1e-12 state (3.1e-14), both through start 4, and since
  2026-10-04 the − 1e-12 state (6.2e-14, the carrier of the deficit of oxygen).
- hp, three modes of KCl + 1e-6 Cl at 100 kPa and 1 000 K left the list on 2026-10-04: the data junction
  settles them (above).
- The 49 further tp states at excesses of 1e-8 and 1e-10 that ended `NotConverged` (K2O, Li2O, MgO,
  Al(OH)3, thermite, CaCO3) are walked now (`TraceGasScanTests`, the trace-excess scan): all end `Ok` or
  `NoGasPhase`, the eight that stayed `NotConverged` until 2026-10-05 included.

## Taboos

- No equilibrium constant and no equation beyond the reduced iteration's.
- No pass without the verdict's `GasRequired` or a bracket's final.
- No second call site of `Run`, `Converge` or `PhaseOnePoint`.
- No write to `BracketEnds`, and no whole `IterationState` assigned through `ref`.
- No public type; no `float`, exception, allocation or virtual call.

# BOOT.md — Equilibrium.StateRecord

## Purpose

A child node of `src/Equilibrium` (its `BOOT.md`, `## Structure`), split out of that node on
2026-10-02 because the property definitions and the plateau convention bind only these files and
the parent was over the §15 limit. It turns a converged or frozen composition into the `MixtureState`:

- `DerivativeSystem` solves the derivative system of RP-1311 section 2.5 at the converged
  composition, with one species of a pinned set (the condensed species whose element vectors
  are linearly dependent) left out as the representative.
- `TiedDerivatives` solves the derivative system at the close and, when it stays singular with no tie
  in force, again with the tie the element rows show over the species of the sums (2026-10-04).
- `PlateauIsentrope` assembles the isentropic system, the sp-shaped system at the converged composition
  that carries `γ_s` at a gas-participating plateau and beside a near-univariant composition.
- `MixtureProperties` writes the state: the assignments both paths share, then the equilibrium (or
  pinned-set) closure of section 2.6 or the frozen one, behind the state guard.

`EquilibriumSolver` calls `TiedDerivatives.Solve` (which calls `DerivativeSystem.Solve`) and
`MixtureProperties.WriteEquilibrium` at the close of `Solve`, and `MixtureProperties.WriteFrozen` in `SolveFrozen` (`API.md`). The cluster has a
reason of its own to change: the property definitions of the report and the reference's convention
at a plateau.

⚠ 2026-10-02: was the state record in `src/Equilibrium` itself, now this node
→ HISTORY.md#structure-split-2026-10-02

## Invariants

The invariants of the parent ([BOOT.md](../BOOT.md)) hold here unchanged.

- **A refused state is never written.** `WriteEquilibrium` and `WriteFrozen` decide the state guard
  before `result.State[0]` is touched, return false when it fails with nothing written, and write
  nothing but `result.State[0]` otherwise.

## Dependencies

- [Equilibrium](../API.md) — the descriptors of its inputs, scratch and outputs, `IterationState`,
  `SystemLayout`, `MixtureSums`, `Derivatives`, `DerivativeKind` and `DenseSolver`.
- [Newton](../Newton/API.md) — `ElementCoupling.Find`, for the tie found at the close.
- [Thermo](../../Thermo/API.md) — the species table view, `MixtureState` and
  `PhysicalConstants`.

Outside the tree: ILGPU 1.5.3 (`ArrayView<T>`); NASA RP-1311 Part I sections 2.5 and 2.6 and
Gordon (1970) for the pinned representative. The parent's internal types this node uses
(`IterationState`, `SystemLayout`, `MixtureSums`, `Derivatives`, `SpeciesMarks`) are not in the
parent's tree contract; the child belongs to the parent's assembly and reads them as its own.

## Constraints

Inherited from the parent ([BOOT.md](../BOOT.md)) and, through it, from the root. In addition:

- Every type here is `internal`; none becomes public (root `BOOT.md`, `## Delivery`,
  "Tree contracts").
- No project of its own: the `.cs` files compile into `src/Equilibrium`'s assembly under the
  namespace `APThermo.Equilibrium.StateRecord`, mirroring this directory from the tree root
  (`AGENTS.md` §1).
- The root's code-shape constraint applies unchanged.
- The mixture's temperature window and the frozen mode's validity rules are the parent's
  ([BOOT.md](../BOOT.md)); the tie and its release are the `Newton` node's
  ([BOOT.md](../Newton/BOOT.md)).

  A state guard (2026-09-28), this node's own and not the reference's: an `Ok` whose
  frozen or equilibrium heat capacity (Cp or Cv), `γ_s` or sound speed is not finite
  and positive is `TemperatureOutOfRange`. The window above keeps every case the
  committed data can serve, and the guard catches what a polynomial evaluated outside
  its fit may still produce inside the window. The pinned pair's `Cp_eq = Cv_eq = 0`
  convention (Property definitions below) is exempt for the pinned set only.

    - **Derivatives.** A tie that survives to the close gives the derivative system a
      unit row that fixes the tied element's multiplier derivative at 0.

  An `Ok` frozen state carries `CpEquilibrium = CpFrozen`, `CvEquilibrium = CvFrozen`,
  the derivatives 1 and −1 and `γ_s = Cp/Cv`.

- **Two retries of a singular derivative system** (2026-10-04, for 0.2.2). Both act only where the
  derivatives were unsolved, the state's status `SingularMatrix`, so no solved state moves.
  - **Scaled.** A pass whose constant-temperature system is singular, at a composition the
    gas-participating plateau does not explain or with a pinned set, is assembled again with the
    condensed columns of the element rows multiplied by n (the gaseous moles of the sums), solved,
    and its condensed unknowns multiplied back by n. It is the same system with `dn_c/n` for
    unknowns. A trace gas (n of 1e-10 beside condensed species of 1e-2) leaves the element rows'
    multiplier pivots below the row scales the condensed columns set, and the scaled solve keeps
    them at their own scale.
  - **The tie found at the close.** When the system is still singular and no tie is in force, the
    first active element whose row `ElementCoupling.Find` expresses as a combination of the other
    rows over the species of the sums is tied, as a surviving tie is (recorded in the case's
    `IterationState`, which the close therefore takes by `ref`), and the system is solved again.
    - This happens after a convergence that held no tie: the trace-gas pass's, or the reduced
      iteration's when the carriers of a direction sit below the retention threshold.
    - The tied direction is invisible to every species of the sums, so the unit row's zero changes
      no derivative the state reports.
    - Measured with the prototype and an emulation (2026-10-04): MgCO3 under CO2 hp and sp at 1 kPa
      to 1 MPa, 29 to 32 states `TemperatureOutOfRange` before, now `Ok`; no bit moved.
- Property definitions of RP-1311: `Cp_eq` includes the reaction contribution of the
  composition derivatives ((2.49), (2.59), section 2.5); `γ_s = −(∂ln p/∂ln V)_s` and
  `a² = n R T γ_s` per unit mass (2.71, 2.74, section 2.6); `M = 1/n` (2.3a) with `n`
  the total gaseous moles per kilogram; `MW = 1/Σ n_j` (2.4a) over all species with
  the condensed ones counted as moles (the reference's MW, see the Thermo `API.md`);
  the condensed species are included in h, s and Cp of the mixture as in CEA.
  At a pinned set the constant-pressure derivatives do not exist. At the close the
  condensed species of the solution are tested in solution order for linear dependence of
  their element vectors (modified Gram–Schmidt, relative residual 1e-9); the first one that
  is a combination of those before it is the representative left out. A pinned pair (two
  records of one formula, a melting plateau) is the one-formula case; a reaction plateau
  among different condensed species (2 Al(OH)3 = Al2O3 + 3 H2O(L)) is the general one, and
  both fix the temperature independently of the pressure, so the isentrope on the plateau
  is an isotherm. The derivative system is assembled once, at constant temperature,
  without the representative (RP-1311 section 3.5; Gordon 1970), and the state carries the
  reference's convention `Cp_eq = Cv_eq = (∂ln V/∂ln T)_p = 0` with
  `(∂ln V/∂ln p)_T` real, `γ_s = −1/(∂ln V/∂ln p)_T` and `a² = n R T γ_s` — the
  plateau values the throat search needs (the AP/Al verification record: `γ_s` 0.816
  on the plateau against 1.09 beside it; the zeros against NaN-plus-flag decided in
  the design session of 2026-09-13, so that the state struct, the surface snapshot
  and the reference comparisons stay unchanged).

  ⚠ 2026-10-03: was a pinned **pair** found by `PhaseGeometry.SameFormula`, now a pinned
  **set** found by linear dependence, so this node stops depending on `Condensed` (the
  orchestrator's investigation B3 for 0.2.1). Inside the Al(OH)3/Al2O3/H2O(L) band the
  three condensed vectors are dependent, the constant-temperature derivative system was
  singular, and every hp and sp state there failed (114 of 114 seeded, 474 in a scan; the
  warm-start fallback reported `TemperatureOutOfRange`). Measured with the prototype: all
  `Ok`, clear of the equilibrium conditions, the first-order fields within the tolerance
  table against cea seeded from 430 K (150 of 150), `γ_s` against `d ln p/d ln ρ` along
  the isentrope and `(∂ln V/∂ln p)_T` against the isothermal difference within 1e-7, no
  bit moved. At a reaction plateau cea 3.3.4 recognises no plateau and reports its
  singular fallback (frozen values, `γ_s` up to 75 % off the finite difference); the tree
  does not follow it there. Not covered: a univariant equilibrium where the gas takes
  part (CaCO3/CaO under pure CO2), where the plateau temperature depends on the pressure.

  ⚠ 2026-10-03: was "not covered", now the gas-participating plateau below (the
  orchestrator's investigation for 0.2.2). There the condensed vectors are independent, so
  the pinned set does not fire, the constant-temperature system is singular, the converged
  state was discarded and the fallback reported `TemperatureOutOfRange` or `NotConverged`
  (0 `Ok` of 96 seeded and cold hp/sp states; every station of an isentrope through the
  two-phase region of nearly pure H2O, NH4Cl, CaCO3 failed). Beside it, at near-univariant
  compositions, an `Ok` carried `γ_s` up to 1.7e-3 wrong (20 of 272 states over the 1e-4
  tier), from cancellation in `Cv = Cp + n (∂ln V/∂ln T)² / (∂ln V/∂ln p)` at `Cp/Cv` of
  1e10 to 1e13.
- **The gas-participating plateau** (2026-10-03, for 0.2.2). When the constant-temperature
  system is singular and the pinned set did not fire, the same Gram–Schmidt runs with the
  gas composition `g_i = Σ_j a_ij n_j` (gaseous j) as the first column, as Newton's rule B
  does; a condensed vector in that span makes the state a gas-participating plateau. Along
  it `T*(p)` moves with `p`, so `(∂ln V/∂ln T)_p` and `(∂ln V/∂ln p)_T` do not exist and the
  isentrope is not an isotherm. The state then carries `Cp_eq = Cv_eq = (∂ln V/∂ln T)_p = 0`
  (the pinned convention, `Pinned` set, exempt from the state guard), and `γ_s`, `a²` and
  `DlnVdlnP` from the **isentropic system**: the sp-shaped linear system at the converged
  composition in `dπ_i`, `dn_c`, `d ln n`, `d ln T` with `d ln p = 1` (element rows,
  condensed rows, the `n` row and the entropy row of RP-1311's sp iteration, a surviving
  tie's unit row as today), non-singular on the plateau because the `T` column and the `s`
  row break the null direction; `(∂ln V/∂ln p)_s = d ln n + d ln T − 1`,
  `γ_s = −1/(∂ln V/∂ln p)_s`, `a² = n R T γ_s`, and `DlnVdlnP` carries `(∂ln V/∂ln p)_s`,
  which keeps the pinned identity `γ_s = −1/DlnVdlnP` and equals the isothermal value on a
  condensed-only plateau, whose isentrope is an isotherm. `γ_s` may fall below 1 (0.886
  measured for Ca(OH)2 at 1 MPa), as for wet steam.
- **The near-univariant sliver** (2026-10-03, for 0.2.2). After a non-singular solve with
  `|Cp/Cv| > 1e6` (both per R), the isentropic system is solved as well: `γ_s` comes from
  it, `Cv = −Cp/(γ_s DlnVdlnP)`, and `Cp`, `(∂ln V/∂ln T)_p`, `(∂ln V/∂ln p)_T` stay from
  the constant-temperature route. The threshold is a design margin, not a fit: the
  constant-temperature `γ_s` is within 4e-7 below `Cp/Cv` = 1e8 and the isentropic route is
  within the finite-difference noise at every ratio measured, so the switch costs nothing
  where it fires early.

  Measured with the prototype (the orchestrator's scratchpad, `probe/prototype-final.diff`,
  2026-10-03): 254 of 294 exact CaCO3 plateau states `Ok` (the other 40 are the hp
  convergence failure below), `γ_s` within 4.6e-8 and `a` within 2e-8 of the isentropic
  finite difference; water, NH4CL(III), Ca(OH)2 and MgCO3 within 5.3e-6; the sliver 272 of
  272 within 4.1e-7 (1.7e-3 before); `γ_s` continuous across the singular boundary; no bit
  moved. Cross-check, pure participating gas, condensed volume neglected: with
  `L = Δh_r/(ν_g R T)` and `c = Cp_frozen/(nR)`, `γ_s = 1/(1 − 2/L + c/L²)`, within 7e-7
  at 1e4 Pa. cea 3.3.4 is no reference here: seeded on the plateau it reports `cp_eq` of
  1e15 to 1e28 and `γ_s` from 1e-10 to 1. Left, and declared: an hp or sp state seeded on
  the one-condensed side drives `T` below the 100 K window before the second condensed
  species can enter (`TemperatureOutOfRange`; cea fails the same way).
  ⚠ 2026-10-03: was a seed-side hp or sp state left `TemperatureOutOfRange`, now bracketed ([Recovery](../Recovery/BOOT.md))

## Structure

The classes of this node, all internal, static and kernel-compatible: `DerivativeSystem`,
`PlateauIsentrope` and `MixtureProperties`, each described by the summary of its declaration. Every
floating-point expression keeps the form and the order of evaluation it had when the node was part of
its parent.

- **The close's entry.** `TiedDerivatives.Solve` is what `EquilibriumSolver`'s close calls. It calls
  `DerivativeSystem.Solve` once, and once more with the tie it found. `DerivativeSystem` stays at
  the limit of the dependency check, and the search is a type of its own for that reason.
  `ScaledRetry` is private to `DerivativeSystem`.
- **The dependence test.** `DerivativeSystem.DependentSlot` runs the modified Gram–Schmidt of
  `## Constraints` over the element vectors of the condensed species of the solution, in solution
  order, keeping its orthonormal basis in the first rows of the matrix scratch, which `Assemble`
  clears before it assembles anything: it needs no scratch of its own, and it reads no node but the
  table view (2026-10-03). With its `gasColumn` argument the first row of the basis is the unit
  gas composition `g_i` (`LoadGasVector`), the condensed vectors follow from the second row, and
  the slot returned is still the condensed species' index (2026-10-03, for 0.2.2).
- **The isentropic system.** `PlateauIsentrope.Assemble` clears and fills the matrix scratch with the
  system of `## Constraints`, "The gas-participating plateau", for the layout of the
  constant-temperature system (its element and condensed counts, its stride and its tie); the
  caller, `DerivativeSystem.Isentropic`, pins a surviving tie's row with the same `PinTiedRow` the
  constant-temperature system uses, solves, and reads `(∂ln V/∂ln p)_s` from `PlateauIsentrope.DlnVdlnP`.
  The assembly is its own class because the system has a reason of its own to change (RP-1311's sp
  rows) and `DerivativeSystem` stands at the limit of the dependency check (`## Shape exceptions`).
- **The carrier.** The result travels in `Derivatives.Isentropic` and `Derivatives.DlnVdlnPIsentropic`
  (parent's `Carriers.cs`, 2026-10-03): at a gas-participating plateau `Pinned`, `Solved` and
  `Isentropic` are all true, `DlnNdlnT` and `Reaction` zero, and `WriteEquilibrium` writes `DlnVdlnP`
  from the isentropic value; at the sliver `Isentropic` is true and `Pinned` false, and
  `WriteEquilibrium` overrides `γ_s` and `Cv` after the constant-temperature closure.
  `MixtureProperties.IsNearUnivariant` decides the sliver from the same `CpEquilibrium` and
  `CvEquilibrium` helpers the closure uses, so the ratio it tests is the one the state would carry.

- **The pinned representative.** `DerivativeSystem` swaps the representative into the
  last slot exactly as today, so that the assembled rows and the pivoting keep their
  order, and restores the caller's order before returning: the scratch is not
  permuted behind the caller's back.

- **One behaviour changed, deliberately and invisibly.** `DerivativeSystem` restores
the caller's condensed order before returning on every path, the singular one included;
`Solve` rebuilds the set from `result.Moles` at every entry, so no result moves.
→ HISTORY.md#s-settled

## Shape exceptions

No type of this node is a declared exception to the root's code-shape constraint:
`DerivativeSystem` measures 14 by a hand count of the types its signatures and bodies name (the
walk's own figure is not printed while the check is green), at the root's limit of 14 and no
higher; the isentropic assembly is `PlateauIsentrope` (Ce 6) for that reason.

⚠ 2026-10-03: was `DerivativeSystem` at 11, now 14 (`MixtureSums`, `PlateauIsentrope` and
`MixtureProperties` added for the gas-participating plateau). A further type named by it is a
decomposition to make, not an exception to declare.

⚠ 2026-09-28: was `DerivativeSystem` at 12, now 11 → HISTORY.md#ce-third-pass

## Acceptance criteria

The criteria of the parent ([ACCEPTANCE.md](../ACCEPTANCE.md)) that name these files hold unchanged:
the derivatives and the plateau facts, `MixturePropertiesTests` and the bit snapshot.

- [x] 2026-10-02 — On the CPU path the split moved this node's files with no change but the
  namespace, `using` lines and doc references, and moved no bit: the criterion of the same date in
  [ACCEPTANCE.md](../ACCEPTANCE.md), `git diff -M` and the bit snapshot.
- [x] 2026-10-02 — On CUDA, on the reference machine: the second part of the same criterion
      (`../ACCEPTANCE.md`), green on `6dc2370`.
- [x] 2026-10-03 — The pinned set (`## Constraints`): unit facts, each red on the pair rule —
      seeded hp and sp inside the Al(OH)3/Al2O3/H2O(L) band at 1, 7 and 20 MPa end `Ok`, clear
      of the equilibrium conditions, at `T* = 415.948162 K` with five condensed species; on the
      plateau `Cp_eq = Cv_eq = (∂ln V/∂ln T)_p = 0` and `γ_s = −1/(∂ln V/∂ln p)_T`; `γ_s`
      within 1e-6 of `d ln p/d ln ρ` along sp and `(∂ln V/∂ln p)_T` within 1e-6 of the
      isothermal difference; a `DerivativeSystem` fact on three dependent species `Solved`
      and pinned; the seeded fixtures of the fixtures node green; no existing bit moved;
      CUDA equal on the reference machine.
      Evidence: `ReactionPlateauTests` and `PinnedSetTests` of `tests/Equilibrium.Tests`
      (`678fe4e`, `53ab7c7`, merged `229fb5d`), 25 of 39 red on the pair rule and green on this
      one; the 30 fixtures of the `seeded` kind; no line of `Bits.approved.txt` moved; the WSL
      fast suite green on `377998d`. CUDA: every CUDA proof green with this derivative system
      (merge-guard on `229fb5d` and `377998d`).

      ⚠ 2026-10-03: was "CUDA equal on the reference machine" for the seeded cases, now the
      CUDA proofs with this code. A seeded case starts from the tp seed's moles, and a batch
      carries a temperature estimate only (`src/Execution/API.md`), so no batch expresses it.

- [x] 2026-10-03 — The gas-participating plateau and the near-univariant sliver
      (`## Constraints`): facts, each red on the current rule, over CaCO3/CaO under CO2
      (Ca:C:O = 1:1:3 and 1:2:5, 1e4 to 1e7 Pa, seeded hp and sp inside the plateau) `Ok`,
      clear of the equilibrium conditions, with `γ_s` and `a` against the isentropic finite
      difference and the closed form; an sp march through the two-phase region of nearly
      pure H2O with every station `Ok`; NH4CL(III), Ca(OH)2 and MgCO3 one state each; the
      sliver's `γ_s` against its finite difference at `Cp/Cv` above 1e10; continuity of `γ_s`
      across the singular boundary; the shape limits held (the isentropic assembly its own
      class); no existing bit moved; CUDA equal on the reference machine for a family of
      such states that a batch can express; Linux green.
      Evidence: `7c741f2`, `ca04760`, merged `2ebebad` with `--cuda` (fast suite 5836, CUDA
      proofs 398, the four `GasPlateauFamilies` among them); 91 of the 99 new facts red with
      the fallback disabled and 12 with the sliver switch disabled; the CaCO3 grid within
      2.9e-10 of the isentropic difference, the closed form within 4.8e-6 at 1e4 and 1e5 Pa
      (it neglects the condensed volume above), the other systems within 1.1e-10, the water
      march 40 of 40 `Ok`, the sliver within 3.1e-9; the facts seed hp and sp at fractions
      0.7 and 0.9, the lower fractions being the seed-side failure of the parent's open
      item; `DerivativeSystem` Ce 14; WSL fast suite green on `57ce721`.

- [x] The tie found at the close: MgCO3 1:2:5 hp and sp, cold, below the plateau at 1 kPa to 1 MPa,
      end `Ok`, `Cp_eq` within 1e-6 of a central difference of the solver's own enthalpy at T ± 0.01 K;
      red without the tie. 2026-10-04, `TiedDerivativesTests.AnHpOrSpStateOfMagnesiteUnderCarbonDioxideBelowItsPlateauEndsOkWithTheHeatCapacityOfItsEnthalpy`
      (12 states, 100 to 1 K below the plateau); red without the tie: 33 facts of the node's tests, 2 of them in this
      file. The unit fact that needs no pass stands:
      `TiedDerivativesTests.ADirectionNoSpeciesOfTheSumsSeesIsFixedByTheTieAndMovesNoDerivative` (MgO beside
      CO2 alone: the plain system unsolved, the tied one solved with the derivatives of a gas of fixed moles).
- [x] The scaled retry: a trace-gas state whose plain derivative system is singular ends `Ok`, `Cp_eq`
      against the same difference; red without the retry. No line of a record moved. 2026-10-04,
      `ScaledDerivativeTests` (5 states of the trace-gas families that end `SingularMatrix` without the retry);
      red without it: 137 facts of the node's tests, 5 of them these; Windows records unchanged, the Linux
      ones not run here.

## Taboos

- No public type here: undocumented surface is a contract nobody agreed to.
- No state written for a refused case: the state guard decides first.
- No iteration and no condensed-set change in this node: they are the `Newton` and `Condensed` nodes'.

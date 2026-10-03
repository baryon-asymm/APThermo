# BOOT.md — Equilibrium.StateRecord

## Purpose

A child node of `src/Equilibrium` (its `BOOT.md`, `## Structure`), split out of that node on
2026-10-02 because the property definitions and the plateau convention bind only these files and
the parent was over the §15 limit. It turns a converged or frozen composition into the `MixtureState`:

- `DerivativeSystem` solves the derivative system of RP-1311 section 2.5 at the converged
  composition, with one species of a pinned set (the condensed species whose element vectors
  are linearly dependent) left out as the representative.
- `MixtureProperties` writes the state: the assignments both paths share, then the equilibrium (or
  pinned-pair) closure of section 2.6 or the frozen one, behind the state guard.

`EquilibriumSolver` calls `DerivativeSystem.Solve` and `MixtureProperties.WriteEquilibrium` at the
close of `Solve`, and `MixtureProperties.WriteFrozen` in `SolveFrozen` (`API.md`). The cluster has a
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

## Structure

The classes of this node, all internal, static and kernel-compatible: `DerivativeSystem` and
`MixtureProperties`, each described by the summary of its declaration. Every floating-point
expression keeps the form and the order of evaluation it had when the node was part of its parent.

- **The dependence test.** `DerivativeSystem.DependentSlot` runs the modified Gram–Schmidt of
  `## Constraints` over the element vectors of the condensed species of the solution, in solution
  order, keeping its orthonormal basis in the first rows of the matrix scratch, which `Assemble`
  clears before it assembles anything: it needs no scratch of its own, and it reads no node but the
  table view (2026-10-03).

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
`DerivativeSystem` measures 11 by the dependency check's walk, below the root's limit of 14.

⚠ 2026-09-28: was `DerivativeSystem` at 12, now 11 → HISTORY.md#ce-third-pass

## Acceptance criteria

The criteria of the parent ([ACCEPTANCE.md](../ACCEPTANCE.md)) that name these files hold unchanged:
the derivatives and the plateau facts, `MixturePropertiesTests` and the bit snapshot.

- [x] 2026-10-02 — On the CPU path the split moved this node's files with no change but the
  namespace, `using` lines and doc references, and moved no bit: the criterion of the same date in
  [ACCEPTANCE.md](../ACCEPTANCE.md), `git diff -M` and the bit snapshot.
- [x] 2026-10-02 — On CUDA, on the reference machine: the second part of the same criterion
      (`../ACCEPTANCE.md`), green on `6dc2370`.
- [ ] The pinned set (2026-10-03, `## Constraints`): unit facts, each red on the pair rule —
      seeded hp and sp inside the Al(OH)3/Al2O3/H2O(L) band at 1, 7 and 20 MPa end `Ok`, clear
      of the equilibrium conditions, at `T* = 415.948162 K` with five condensed species; on the
      plateau `Cp_eq = Cv_eq = (∂ln V/∂ln T)_p = 0` and `γ_s = −1/(∂ln V/∂ln p)_T`; `γ_s`
      within 1e-6 of `d ln p/d ln ρ` along sp and `(∂ln V/∂ln p)_T` within 1e-6 of the
      isothermal difference; a `DerivativeSystem` fact on three dependent species `Solved`
      and pinned; the seeded fixtures of the fixtures node green; no existing bit moved;
      CUDA equal on the reference machine.
      Evidence so far, CPU accelerator, 2026-10-03, not yet a tick (CUDA and Linux are open):
      `ReactionPlateauTests` and `PinnedSetTests` of `tests/Equilibrium.Tests`, red on the pair
      rule and green on this one, the fixtures of the `seeded` kind, and no line of
      `Bits.approved.txt` moved.

## Taboos

- No public type here: undocumented surface is a contract nobody agreed to.
- No state written for a refused case: the state guard decides first.
- No iteration and no condensed-set change in this node: they are the `Newton` and `Condensed` nodes'.

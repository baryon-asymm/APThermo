# BOOT.md — Transport

## Purpose

Viscosity, thermal conductivity and Prandtl number of the mixture at one station of
one case, in the frozen and in the reacting (equilibrium) sense, from the NASA
transport fits and the equilibrium solution. It is a separate node because it uses a
different data file, is an optional stage of every problem, and, by the root's
decision, is brought to the GPU after the thermodynamic path.

⚠ Declared deviation (`AGENTS.md` §6, §12): the method is that of NASA RP-1311 Part I,
chapter 5 (transport properties: the pure-species fits, the mixture rules for
viscosity and frozen conductivity, the reaction contribution to the conductivity of a
reacting mixture). This document fixes the choices and the units; it does not restate
the formulas. Whoever codes this node reads chapter 5.

## Invariants

- **Units are converted once, here**: fits give micropoise and μW/(cm·K); the outputs
  are Pa·s (`1 μP = 1e-7 Pa·s`) and W/(m·K) (`1 μW/(cm·K) = 1e-4 W/(m·K)`).
- **Only species with data take part.** A species without a transport entry is
  excluded from the mixture rules and the mole fractions of the remaining species
  are renormalized, as CEA does; the fraction of the mixture that was excluded is
  reported, and a station where it exceeds the threshold below is `NoTransportData`.
- **Pair data first.** For a pair with an interaction entry the entry is used; without
  one, the pure-species estimate of the report is used. No third source.
- **Frozen and reacting values are consistent**: the reacting conductivity is never
  below the frozen one, and at a station where no composition change is possible
  (frozen flow) the two are equal within 1e-12 relative.
- **Stateless, deterministic, no allocation**, as every numerical node.

## Dependencies

- [Data](../Data/API.md) — the transport fits and pair entries (`TransportDatabase`).
- [Thermo](../Thermo/API.md) — the species table view (molar masses, order),
  `MixtureState`, `CaseStatus`, the gas constant.
- [Equilibrium](../Equilibrium/API.md) — the mole numbers and Lagrange multipliers
  of the station, and the equilibrium derivatives used by the reaction contribution.

Outside the tree: ILGPU 1.5.3 (`ArrayView<T>`); NASA RP-1311 Part I, chapter 5.

## Constraints

Inherited from the root ([BOOT.md](../../BOOT.md)). In addition:

- Kernel-compatible C#; the compact transport table (a builder from `Data` for the
  species of a `SpeciesTable`, host side) and the per-station evaluation (kernel side).
- The transport table stores, per species of the species table, the fit intervals
  for viscosity and conductivity and, per pair with data, the interaction fits; a
  species without data has zero intervals.
- Threshold for `NoTransportData`: when more than 1 % of the gaseous moles at the
  station belong to species without data (CEA reports the properties anyway; the
  threshold is this tree's choice and is reported, not silently applied).
- Fit evaluation outside a fit's temperature range uses the nearest interval, as for
  the thermodynamic polynomials.
- Condensed species are excluded from the transport mixture; the reacting
  conductivity uses the gaseous species only, as in the report.
- Outputs: viscosity, frozen conductivity, reacting conductivity, frozen Prandtl
  number (`Cp_fr η / λ_fr`), reacting Prandtl number (`Cp_eq η / λ_eq`), the excluded
  mole fraction, and a status.

## Acceptance criteria

- [ ] For the reference rocket cases run with transport output (LOX/LH2, MMH/NTO,
      and the RP-1311 example 11 style case with transport), viscosity, frozen and
      reacting conductivity and both Prandtl numbers at chamber, throat and exits agree
      with the fixtures within the tolerance table (four significant digits, the
      reference print precision, unless the calibration in the fixtures node says otherwise).
- [ ] A pure-species check: for `H2`, `N2`, `H2O` at fixture temperatures the fit
      evaluation matches an independent Python evaluation of the same entries.
- [ ] The exclusion policy is exercised by a fixture case containing a species without
      data, and the reported excluded fraction equals the independently computed one.
- [ ] Runs unchanged inside an ILGPU kernel on the CPU accelerator with the same
      results as the host call.

## Taboos

- No default transport properties for species without data: absence is reported,
  never guessed.
- No unit other than SI leaves this node; no unit conversion elsewhere.
- No re-solving of the equilibrium here: the reaction contribution takes the
  derivatives from `Equilibrium`'s outputs.
- No `float`, no exceptions, no allocations: kernel code.

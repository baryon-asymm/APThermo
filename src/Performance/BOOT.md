# BOOT.md — Performance

## Purpose

The rocket problem for one case with an infinite-area chamber: the chamber state at
assigned enthalpy and pressure, the throat by the sonic condition, the exit stations
at assigned area ratios or pressure ratios, in shifting equilibrium or in frozen flow,
and the performance figures: characteristic velocity, thrust coefficient, specific
impulse and vacuum specific impulse. It sits on `Equilibrium`, which it calls at every
station, and it is verified against the reference implementation's rocket tables.

⚠ Declared deviation (`AGENTS.md` §6, §12): the method is that of NASA RP-1311 Part I,
chapter 6 (rocket performance, infinite-area combustor): the pressure-ratio iteration
for the throat, the area-ratio iteration for the exit stations, the definitions of the
performance parameters. This document fixes the choices and limits; it does not
restate the equations. Whoever codes this node reads chapter 6.

## Invariants

- **Isentropic expansion.** Every station downstream of the chamber has the chamber
  entropy: `|s_station − s_chamber| ≤ 1e-9 · |s_chamber|` at convergence; a station
  violating it is reported as `NotConverged`.
- **Sonic throat.** At the throat `|u²/a² − 1| ≤ 4e-5` (the report's tolerance, with
  the equilibrium sound speed in equilibrium flow and the frozen one in frozen flow).
- **Area ratios are met by construction.** An exit station requested by area ratio
  satisfies `|(ρ_t u_t)/(ρ_e u_e) − ε| ≤ 1e-6 · ε` at convergence.
- **Frozen means frozen.** In frozen flow the composition downstream of the freezing
  station is bit-identical to the freezing station's composition; only temperature and
  pressure change.
- **Performance figures are defined once**, as in the report: `c* = p_c / (ρ_t u_t)`;
  `Isp = u_e` (effective exhaust velocity, m/s, ambient pressure equal to exit
  pressure); `Ivac = u_e + p_e / (ρ_e u_e)`; `C_F = Isp / c*`; `Mach = u / a` with the
  sound speed of the flow model.
- **Stateless, deterministic, bounded**, as `Equilibrium`: explicit inputs and
  scratch; iteration caps; a status per case.

## Dependencies

- [Equilibrium](../Equilibrium/API.md) — the hp and sp solves (shifting flow) and the
  frozen solve (frozen flow), the mixture state and its derivatives.
- [Thermo](../Thermo/API.md) — the table view, `MixtureState`, `CaseStatus`, the gas constant.

Outside the tree: ILGPU 1.5.3 (`ArrayView<T>`); NASA RP-1311 Part I, chapter 6.

## Constraints

Inherited from the root ([BOOT.md](../../BOOT.md)). In addition:

- Kernel-compatible C#; one case is one sequential program: chamber, throat, then
  the exit stations in the order given.
- Inputs per case: the element moles per kilogram, the reactant enthalpy per kilogram,
  the chamber pressure, the flow model (`ShiftingEquilibrium`, `FrozenAtChamber`,
  `FrozenAtThroat`), and a fixed number of exit stations per batch, each either an
  area ratio (`≥ 1`, supersonic branch) or a pressure ratio `p_c/p_e (> 1)`.
- Throat: initial pressure ratio from the chamber `γ_s` as in the report; Newton on
  `ln(p_c/p_t)` with the report's update; at most 20 iterations, else `ThroatNotFound`.
- Exit by area ratio: initial pressure ratio from the isentropic relation with the
  throat `γ_s`; Newton on `ln(p_c/p_e)` as in the report; at most 20 iterations; the
  supersonic branch only, so an area ratio below 1 is `AreaRatioInvalid`.
- Exit by pressure ratio: one sp solve at `p_e`; the area ratio is an output.
- Each station's sp solve starts from the previous station's composition and
  temperature as the estimate.
- Frozen flow: the freezing station's composition is copied once; downstream stations
  use `SolveFrozen`.
- Outputs per case: the chamber, throat and exit `MixtureState`s, the mole numbers at
  every station (for composition output and for `Transport`), the performance figures
  per exit station, and a status.
- Not in version 1: finite-area chamber, subsonic exit stations, freezing at an
  arbitrary station.

## Acceptance criteria

- [ ] For the reference rocket cases of the four propellants and RP-1311 example 8
      (LOX/LH2) and example 12 (MMH/NTO, equilibrium and frozen with freezing at the
      throat), the chamber, throat and exit temperatures, pressures, `M`, `γ_s`, sound
      speed, Mach, `c*`, `C_F`, `Isp` and `Ivac` agree with the fixtures within the
      tolerance table; the list of compared fields is generated from the fixture.
- [ ] Exit stations requested by pressure ratio reproduce the reference area ratios,
      and stations requested by area ratio reproduce the reference pressure ratios.
- [ ] Frozen flow at the throat and at the chamber both reproduce the reference
      (example 12 covers `nfz = 2`; a generated fixture covers `nfz = 1`).
- [ ] The invariants' tolerances (entropy, sonic condition, area ratio) hold for every
      converged fixture case (machine-generated list).
- [ ] An area ratio below 1 returns `AreaRatioInvalid` for that station and leaves the
      other stations unaffected.
- [ ] Runs unchanged inside an ILGPU kernel on the CPU accelerator with the same
      results as the host call.

## Taboos

- No second equilibrium solver or mixture-property formula here: call `Equilibrium`.
- No performance figure computed in more than one place; no conversion to seconds
  (that is the command line's convenience).
- No `float`, no exceptions, no allocations: kernel code.
- No finite-area chamber approximations smuggled in under a flag: version 1 is
  infinite-area only, and a later version gets its own design session.

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
  `FrozenAtThroat`), and a fixed number of exit stations per batch (possibly none),
  each either an area ratio (`≥ 1`, supersonic branch) or a pressure ratio `p_c/p_e (> 1)`.
- Throat: initial pressure ratio from the chamber `γ_s` as in the report (6.15);
  the momentum update of (6.17) on the throat pressure; at most 20 iterations, else
  `ThroatNotFound`. The report stops at `|u² − a²|/u² ≤ 4e-5` (6.16); this node goes
  on to `1e-10` when it can, so that the reported throat is at rounding level, and
  accepts the report's tolerance as `Ok` otherwise.
- Exit by area ratio: initial pressure ratio from the report's estimates, the
  correction of (6.23)–(6.24) on `ln(p_c/p_e)`; at most 20 iterations; the
  supersonic branch only, so an area ratio below 1 is `AreaRatioInvalid`. The report
  stops at `4e-5` on the correction (6.25); this node goes on to `1e-10` when it can.

  ⚠ 2026-09-12: stood "initial pressure ratio from the isentropic relation with the
  throat γ_s". The report's own estimates are used instead: the extrapolation with the
  derivative (6.23) from the previous station when both area ratios exceed 2, else its
  empirical formulas (6.21) for ratios up to 2 and (6.22) above. The formulas were
  recovered from the report's text, whose typography lost the range boundaries; the
  ranges are this node's reading, and they only change how many iterations a station
  takes, never where it converges.
- Exit by pressure ratio: one sp solve at `p_e`; the area ratio is an output.
- Each station's sp solve starts from the last converged station's composition and
  temperature as the estimate (a failed station is skipped over), in frozen flow from
  the freezing station's composition.
- Frozen flow: the freezing station's composition is copied once; downstream stations
  use `SolveFrozen`. In `FrozenAtChamber` flow the chamber's isentropic exponent,
  sound speed and derivatives are the frozen ones (section 6.5.3), as the reference
  reports them, while its heat capacities stay the equilibrium ones.
- Outputs per case: the chamber, throat and exit `MixtureState`s with velocity and
  Mach number, the mole numbers and multipliers at every station (for composition
  output and for `Transport`), the performance figures per station (the reference
  reports them at every station: `c*` everywhere, the throat's `C_F`, `Isp`, `Ivac`),
  a status per station, the equilibrium iteration counts, and the case status.
- Not in version 1: finite-area chamber, subsonic exit stations, freezing at an
  arbitrary station, the report's stop of a frozen expansion 50 K below the range of
  a condensed species present at the chamber (section 6.5.1).

## Acceptance criteria

- [x] 2026-09-12 — For the reference rocket cases of the four propellants and
      RP-1311 example 8 (LOX/LH2) and example 12 (MMH/NTO, equilibrium and frozen with
      freezing at the throat), the chamber, throat and exit temperatures, pressures,
      `M`, `γ_s`, sound speed, Mach, `c*`, `C_F`, `Isp` and `Ivac` agree with the
      fixtures within the tolerance table; the list of compared fields is generated
      from the fixture. `Performance.Tests`,
      `RocketFixtureTests.The_rocket_case_reproduces_the_reference` over the enumerated
      `cases/rocket` directory (89 files): every numeric station output mapped by name to
      a field of `MixtureState` or `PerformanceFigures`, plus every listed mole
      fraction; left out by the fixtures node's caveats: the reference's `cv` at frozen
      stations and its gas-phase frozen heat capacities at stations with condensed
      species and transport on; the one subsonic station of example 8 is outside
      version 1. The `pressure` tolerance was calibrated to the reference's own
      iteration residual (the fixtures node's criteria).
- [x] 2026-09-12 — Exit stations requested by pressure ratio reproduce the reference
      area ratios, and stations requested by area ratio reproduce the reference
      pressure ratios: part of the same comparison (`areaRatio` at the pressure-ratio
      stations of examples 8 and 12, `pressureRatio` and `pressure` at every area-ratio
      station).
- [x] 2026-09-12 — Frozen flow at the throat and at the chamber both reproduce the
      reference: the same test over the 15 `frozenAtChamber` and 40 `frozenAtThroat`
      propellant cases and example 12 (`nfz = 2`).
- [x] 2026-09-12 — The invariants' tolerances (entropy, sonic condition, area ratio,
      frozen composition bit for bit, velocity from the energy equation) hold for every
      converged fixture case:
      `InvariantTests.Entropy_sonic_throat_area_ratio_and_frozen_composition_hold` over
      the 89 files.
- [x] 2026-09-12 — An area ratio below 1 returns `AreaRatioInvalid` for that station
      and leaves the other stations unaffected:
      `InvariantTests.An_area_ratio_below_one_fails_its_station_only` (and
      `A_pressure_ratio_not_above_one_fails_its_station_only`,
      `A_case_without_exits_gives_the_chamber_and_the_throat`,
      `A_non_positive_chamber_pressure_is_invalid_input`).
- [x] 2026-09-12 — Runs unchanged inside an ILGPU kernel on the CPU accelerator with
      the same results as the host call: `KernelEqualityTests.Kernel_and_host_give_the_same_bits`
      over the 6 batches of fixtures sharing a table and an exit layout (89 cases; states,
      figures, moles and statuses bit for bit).

## Taboos

- No second equilibrium solver or mixture-property formula here: call `Equilibrium`.
- No performance figure computed in more than one place; no conversion to seconds
  (that is the command line's convenience).
- No `float`, no exceptions, no allocations: kernel code.
- No finite-area chamber approximations smuggled in under a flag: version 1 is
  infinite-area only, and a later version gets its own design session.

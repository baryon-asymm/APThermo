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
  violating it is reported as `NotConverged`. This node checks it at every station it
  accepts (2026-09-26).

  ⚠ 2026-09-26: was unchecked, now checked at each station → HISTORY.md#entropy-check
- **The throat is the first maximum of the mass flux met from the chamber**
  (2026-09-28). Expanding along the chamber isentrope from the chamber's pressure, the
  throat is the highest pressure at which `ρu` has a local maximum, that is where the
  Mach number reaches 1 from below, continuously or by a jump. Where `ρu` has one
  maximum, this is the point of largest mass flux. Where `u²/a²` crosses 1 continuously,
  it is the sonic point, `|u²/a² − 1| ≤ 4e-5` (the report's tolerance, with the
  equilibrium sound speed in equilibrium flow and the frozen one in frozen flow).
  Where `u²/a²` jumps across 1, the throat is the edge where it jumps. That happens
  at the high-pressure edge of a melting plateau, where the equilibrium sound speed
  is discontinuous between the single-phase state and the pinned pair (2026-09-26).
  `ρu` is continuous there and has its maximum at the edge. The throat's state is
  then the single-phase state on the chamber side of the edge, and its Mach number is
  below 1.

  ⚠ 2026-09-28: was "the throat carries the largest mass flux", now the first maximum
  met from the chamber → HISTORY.md#throat-first-maximum

  ⚠ 2026-09-26: was "Sonic throat: |u²/a² − 1| ≤ 4e-5 at the throat", now the mass-flux
  maximum above (no sonic root at a plateau edge) → HISTORY.md#sonic-throat
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
  each either an area ratio (`> 1`, supersonic branch; 2026-09-26) or a pressure ratio
  `p_c/p_e (> 1)`. A flow model other than the three named values is `InvalidInput` for
  the case (2026-09-26).

  ⚠ 2026-09-26: was solved as FrozenAtThroat, now InvalidInput → HISTORY.md#flow-model
- Throat: initial pressure ratio from the chamber `γ_s` as in the report (6.15);
  the momentum update of (6.17) on the throat pressure; at most 20 iterations. The
  report stops at `|u² − a²|/u² ≤ 4e-5` (6.16); this node goes on, so that the
  reported throat is at rounding level, and accepts the report's tolerance as `Ok`
  otherwise. Going on is a decision and a fixed tail (the owner's decision of
  2026-10-02): the first trial with `|u²/a² − 1| ≤ 1e-8` decides, then exactly two more
  momentum steps are taken whatever their ratio, and the second is the throat. The two
  steps may run past the 20 iterations. A decision far from the noise flips between
  accelerators rarely, and a flipped decision costs under 1e-12 in `ln p` after the
  tail. The area-ratio iteration keeps `RocketSolver.TightTolerance` (1e-10).

  ⚠ 2026-10-02, the owner's decisions: was `1e-10`, then `1e-11`, now `1e-8` and two steps → HISTORY.md#throat-stop-rule;
  ⚠ 2026-10-03: was "the area-ratio exits'", now mostly the polish's → HISTORY.md#stations-35

  The bracket (2026-09-26). The search keeps the smallest pressure solved with
  `u²/a² < 1` and the largest solved with `u²/a² > 1`. When the 20 momentum
  iterations end without either tolerance and such a bracket exists, the search
  halves the bracket in `ln p`:
  - When both ends hold one condensed set, it stops at the first solve within `1e-8`
    and takes the same two momentum steps; a step that changes the condensed set or
    fails falls back to the midpoint. That is the sonic throat.
  - Otherwise it stops when the bracket is narrower than `1e-10` in `ln p`. If the
    condensed sets at its two ends differ, the throat is the plateau edge: the state at
    the high-pressure end.
  - At most `MaxThroatBisections` (60) solves.
  - Anything else ends `ThroatNotFound`: no bracket, or a jump without a change of
    the condensed set, or the report's tolerance still unmet at the end of the
    bisection. The end of the bisection tests that tolerance on its last trial
    (2026-09-28): a trial within `4e-5` is the throat, `Ok`.
  - The plateau edge's state (2026-09-28) is accepted only when its condensed set is
    the bracket's subsonic end's and its `u²/a² < 1`. Otherwise it is solved again
    from the chamber's composition further toward the chamber, at `ln p` offsets
    growing geometrically from the bracket width (`1e-10·4^k`, the third pass of
    2026-09-28), until a solve lands on the subsonic side or the offset would exceed
    `1e-4`. The first such solve is the throat; `ρu` there departs from the edge's by
    less than the offset, far inside the fixtures' tolerance. No landing within that
    reach is `ThroatNotFound`.

    ⚠ 2026-09-28, the third pass: was "a few bracket widths further toward the
    chamber" (eight steps of 1e-10), now the geometric offsets above
    → HISTORY.md#plateau-edge-reach

  The first trial (6.15) at `γ_s` = 1 exactly, the equilibrium node's plateau
  convention for an undissociated gas, is `Math.Pow(1, ∞)` = 1, the chamber itself.
  Within 1e-6 of 1 the trial is the limit of (6.15), `p_c·e^(−1/2)` (2026-09-28). A
  trial whose `u²` is not positive lies at or above the chamber's enthalpy: it counts
  as the subsonic side, and the pressure steps down, instead of ending the search
  without a bracket. The momentum loop computes `u²/a²` before it tests the trial, so
  the tolerance is never judged on a previous trial's ratio.

  The first maximum (2026-09-28). The local search above returns a candidate throat and
  its condensed set. Two stages then prove that no choke lies between the chamber and
  the candidate:
  - `PhaseBoundaryLocator`: where the chamber's condensed set and the candidate's
    differ, it locates each boundary between them along the isentrope by a bounded
    bisection in `ln p` on the condensed set, to the bracket width above.
  - `UpstreamChokeCheck`: walking the boundaries from the chamber side, the throat is
    the first boundary with `u²/a² < 1` above it and `≥ 1` below it (the plateau-edge
    rule), or the first sonic crossing inside an interval between boundaries, found by
    the bisection above. With equal sets at the chamber and the candidate, the
    candidate is the throat, as before.
  - The walk visits at most `MaxPhaseBoundaries` boundaries (8, the third pass of
    2026-09-28). A cold scan of Li/O/H at 3 MPa already crosses four
    ({Li2O(L)} → … → {Li2O(cr)}); a walk that reaches the cap without reaching the
    candidate's set has proved nothing, and ends `ThroatNotFound`, never `Ok`.

    ⚠ 2026-09-28, the third pass: was a cap of 4 that accepted the unverified
    candidate as Ok, now 8 and ThroatNotFound → HISTORY.md#boundary-cap

  `ThroatSearch` composes the local search and the check, and stays the only writer of
  the throat's figures. `FrozenAtChamber` flow is unaffected: its frozen sound speed is
  continuous. The throat's figures are those of the state actually solved: its pressure
  ratio is `p_c` over that state's pressure (2026-09-26). → HISTORY.md#throat-search

  ⚠ 2026-09-28: was "converging cases never reach the bisection", now every candidate
  meets the first-maximum stages → HISTORY.md#converging-cases

  ⚠ 2026-09-26: was the pressure the last update produced, now the pressure the
  reported state was solved at → HISTORY.md#exhausted-search-pressure
- Exit by area ratio: initial pressure ratio from the report's estimates, the
  correction of (6.23)–(6.24) on `ln(p_c/p_e)`; at most 20 iterations; the supersonic
  branch only. An area ratio not above 1 is `AreaRatioInvalid`, as the reference
  refuses it; the throat itself is station 1. The report stops at `4e-5` on the
  correction (6.25); this node goes on to `1e-10` when it can. A pass that lands on the
  subsonic side is stepped outward and never accepted: a station is accepted only on a
  supersonic pass (2026-09-26). → HISTORY.md#area-ratio-wording

  ⚠ 2026-09-26: was "an area ratio below 1 is `AreaRatioInvalid`", now "not above 1"
  → HISTORY.md#area-ratio-one

  ⚠ 2026-09-12: was the isentropic relation with the throat γ_s, now the report's own
  estimates (6.21)–(6.23) → HISTORY.md#initial-pressure-ratio
- Exit by pressure ratio: one sp solve at `p_e`; the area ratio is an output. In
  `FrozenAtThroat` flow a pressure ratio not above the throat's is `InvalidInput` for
  that station (2026-09-28): upstream of the freezing point the flow is in equilibrium,
  and the reference omits such a point. → HISTORY.md#pressure-ratio-wording

  ⚠ 2026-09-28: was a station at or above the throat's pressure `Ok` (Mach 0), now
  `InvalidInput` → HISTORY.md#frozen-throat-exits
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
- Compile size (2026-09-30, the root's Compile size constraint): `StationSolve.At` is
  marked `[MethodImpl(MethodImplOptions.NoInlining)]`. It is the one method through
  which the rocket program reaches `Equilibrium`'s solves, from eight call sites
  (`AreaRatioIteration`, `PhaseBoundaryLocator`, `PressureRatioStation`,
  `ThroatBracketSearch` four times, `UpstreamChokeCheck`; the fourth is the throat
  tail's and the bisection fallback's `Trial`, 2026-10-02), and ILGPU inlines a full copy
  of the solve at each. A new stage that calls it adds a call, never a copy; a stage that
  would reach `Equilibrium` another way is a root decision. It passes the callee copies of the
  context's `Table` and `Scratch`, never a reference into the context (2026-10-04, the root's
  Compile size constraint: a `NoInlining` call takes copies of loop-live locals); no bit moves.

## Structure

Decided 2026-09-14 (the clean-code pass; the root's code-shape constraint). The rocket
solve of one case is one public entry over internal static stage classes, all
kernel-compatible, one class per file in this directory and namespace, sharing the
existing view, scratch and result structs. Every floating-point expression keeps its
present form and its present order of evaluation; the bit snapshot of the tests node
(the acceptance criteria below) is the proof that the decomposition moved code and
rewrote no formula.

⚠ 2026-09-15: was "one public entry", now `RocketSolver` and the descriptors internal
(`API.md`, tree contract) → HISTORY.md#internal-entry

| Class | Responsibility | Visibility |
|---|---|---|
| `RocketSolver` | the contract: the constants and `Solve`, reduced to the station order (clear the views, the chamber, the throat, the exits, the case status); holds no formula | internal (2026-09-15, distribution phase), contract unchanged |
| `ChamberSolve` | the chamber state at assigned enthalpy and pressure, made frozen where the flow model says so (sections 6.3.1 and 6.5.3); returns `ChamberReference` | internal |
| `ThroatSearch` | asks `ThroatBracketSearch` for the throat pressure and defines what the accepted station gives the case: the mass flux and `c*`; returns `ThroatReference` | internal |
| `ThroatBracketSearch` | the sonic throat pressure: the momentum iterations of (6.15)–(6.17), the bracket they track along the way, its bisection where the iterations end short, and the plateau-edge acceptance (2026-09-26, finding F1); since 2026-09-28 a candidate with its condensed set, the first trial's limit at `γ_s` = 1, and the edge state checked for its side | internal |
| `PhaseBoundaryLocator` | the pressures along the chamber isentrope where the condensed set changes between the chamber and a candidate throat, each by a bounded bisection in `ln p` (2026-09-28) | internal |
| `UpstreamChokeCheck` | the first maximum of the mass flux from the chamber: over the located boundaries, the first plateau edge or sonic crossing, else the candidate (2026-09-28) | internal |
| `ExitStations` | the loop over the exits, the dispatch on `ExitSpecification` to `AreaRatioIteration` or `PressureRatioStation`, the estimate chain from station to station, the case status; holds no formula (Size, below) | internal |
| `AreaRatioIteration` | one exit assigned by area ratio: the initial `ln(p_c/p_e)` of (6.21)–(6.23), the correction of (6.24)–(6.25), an explicit outcome | internal |
| `PressureRatioStation` | one exit assigned by pressure ratio (6.3.6): the station pressure from the ratio, the solve at that pressure, the velocity, the area ratio and the figures as outputs | internal |
| `StationSolve` | one station's sp or frozen solve: the sub-views, the estimate composition copied in, the call into `Equilibrium` | internal |
| `StationFigures` | the energy equation, the area ratio and the figures of section 6.2, each written once | internal |

Carriers (`Carriers.cs`, each documented there): `RocketContext`, `ChamberReference`,
`ThroatReference`, `StationRequest`, `StationFigureInputs`, `ThroatQuery` (its
constructor picks the flow from the problem, keeping that choice out of
`ThroatSearch`'s coupling count, 2026-09-26), `ThroatBracket`, `ExitEstimate`,
`PhaseBoundaryEnd`, `PhaseBoundaryQuery`, `PhaseBoundary`, and the enums
`StationFlow { Shifting, Frozen }` and `ExitOutcome { Converged, WithinReportTolerance,
NeverSupersonic, SolveFailed }`. → HISTORY.md#carriers-wording

⚠ 2026-09-14: was `ExitOutcome` with four values, `ExitEstimate` without a temperature,
now a fifth value `NotMet` (twenty corrections, the last above the report's tolerance,
`NotConverged`) and `ExitEstimate.Temperature` → HISTORY.md#carriers-found-in-coding

Decisions taken with the review of 2026-09-14:

- **A station that never went supersonic is `NotConverged`.** `NeverSupersonic` ends the
  station as `NotConverged`, as `API.md` says; the subsonic step of `0.1` in
  `ln(p_c/p_e)` is the named constant `SubsonicStep`; no fixture reaches the path, the
  tests node drives it through `AreaRatioIteration` from an estimate deep on the
  subsonic side. → HISTORY.md#decision-never-supersonic
- **Two velocity formulas, both named.** `u = sqrt(2(h_c − h))` was written five times,
  once clamped at zero for a pressure-ratio station. `StationFigures.Velocity` and
  `StationFigures.VelocityClamped` are the two, extracted verbatim; whether a negative
  radicand should be a clamp or a `NotConverged` station is one rule with the outcome
  above and is decided in a later session with its own test, not inside the
  decomposition.
- **The frozen chamber keeps its four lines: a declared deviation (AGENTS.md §12) from
  the one-source rule of the clean-code criteria (A5) and from this node's own
  taboo.** In `FrozenAtChamber` flow the chamber's `γ_s = Cp/Cv` and
  `a = sqrt(γ_s R T/M)` are computed here, a second time in the tree, because the
  reference reports the frozen exponent and sound speed on an otherwise equilibrium
  chamber state, which `Equilibrium`'s frozen solve cannot produce without also
  freezing the heat capacities. What lifts it: a public frozen-state helper in
  `Equilibrium`'s `MixtureProperties`, a root decision once that node's decomposition
  has landed. Until then `ChamberSolve` carries the four lines verbatim.
- **`InternalsVisibleTo` for the tests node** is added to the project, as `Transport`
  and `Equilibrium` have it, so that the stages are testable directly.
- **Size.** No method over 60 lines and no control flow nested deeper than 3 in every
  stage. `ExitStations` hands the station assigned by pressure ratio to
  `PressureRatioStation`, as it hands the one assigned by area ratio to
  `AreaRatioIteration`, and keeps the loop, the dispatch, the estimate chain and the
  case status, none of them a formula. Its efferent coupling by the dependency check's
  walk is expected at the root's limit of 14; above it, `ExitStations` is this node's
  composition root of the exits, its measured figure written into its row.

  ⚠ 2026-09-14: was "no composition-root exception, `ExitStations` at Ce 10", now 14,
  the ceiling → HISTORY.md#size-coupling-estimate
- **The descriptors keep their constructors** (added 2026-09-14). `RocketProblem` (7
  parameters) and `RocketResult` (7) mirror, one argument per field, the case the
  kernel reads and the views the solver writes into, as `API.md` publishes them;
  grouping them would move the contract and re-emit the kernels. They are this node's
  declared exception to the parameter rule, on the root's condition that every
  creation names its arguments, wherever in the tree it stands; a scan of the
  construction sites found every one positional.

## Shape exceptions

The rows below are this node's declared exceptions to the root's code-shape constraint,
in the form the protocol tests node reads; their reasons are decisions of `## Structure`.

| Where | Rule | Measured | Reason |
|---|---|---|---|
| `RocketProblem.RocketProblem` | parameters | 7 | mirrors, one argument per field, the case the kernel reads, as `API.md` publishes it; grouping it would move the contract and re-emit the kernels (the decision "The descriptors keep their constructors"); every creation names its arguments |
| `RocketResult.RocketResult` | parameters | 7 | mirrors, one argument per field, the views the solver writes into, as `RocketProblem` above |
| `UpstreamChokeCheck` | efferent coupling | 15 | the first-maximum orchestration stage (2026-09-28, finding F1): it walks `PhaseBoundaryLocator`'s boundaries and hands each to `ThroatBracketSearch`'s own `Bisect` or `AcceptPlateauEdge`, so it names every carrier the two together use (`RocketContext`, `ThroatQuery`, `ChamberReference`, `CaseStatus`, `PhaseBoundaryEnd`, `PhaseBoundaryQuery`, `PhaseBoundary`, `ThroatBracket`, `StationRequest`, `MixtureState`) plus the four stages themselves (`PhaseBoundaryLocator`, `ThroatBracketSearch`, `StationSolve`, `RocketSolver`) — the same kind of figure the root's own history records for the decomposed kernel stages (12 to 16, `BOOT.md`'s Code shape note of 2026-09-14); it holds no formula of its own, only the walk and the acceptance dispatch |

Outside the row above no type of this node names more than 14 distinct types of the
tree by the dependency check's walk (`ExitStations` and `ChamberSolve` tie at 14, the
ceiling): the Size bullet's composition-root exception is not claimed, and this node
needs no other efferent-coupling row. → HISTORY.md#shape-closing-wording

### Audit closure (2026-11-14)

- Condensed-set identity is exact, order-independent and ignores vacant slots.
  A hash alone never certifies identity; scratch holds at most twenty distinct
  species, each below the species-table limit.
- Final rocket results clear failed stations' State and figures. NoGasPhase
  retains only temperature and pressure. Earlier successful stations and
  diagnostic composition remain available.
- A bounded phase-boundary walk accepts no candidate after unresolved exhaustion.
  The boundary classification and the cap are exercised through the real walk.


## Acceptance criteria

→ [ACCEPTANCE.md](ACCEPTANCE.md)

## Taboos

- No second equilibrium solver or mixture-property formula here: call `Equilibrium`.
- No performance figure computed in more than one place; no conversion to seconds
  (that is the command line's convenience).
- No `float`, no exceptions, no allocations: kernel code.
- No finite-area chamber approximations smuggled in under a flag: version 1 is
  infinite-area only, and a later version gets its own design session.

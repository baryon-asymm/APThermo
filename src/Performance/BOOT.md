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

  ⚠ 2026-09-26: no code of this node compared the entropies. The sentence relied on the
  equilibrium solver's convergence and was never checked; no violation had been
  observed. Found by the hidden-defect audit of 2026-09-26 (its notes).
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

  ⚠ 2026-09-28: stood "**The throat carries the largest mass flux.** The throat is the
  point of largest mass flux `ρu` along the chamber isentrope". Near a melting plateau
  `ρu` can have two local maxima: one at the plateau (on the pinned pair, or at its
  high-pressure edge) and a second, continuous sonic crossing after the plateau's end.
  - In 652 cases of the second audit's sweeps the search converged to the downstream
    crossing although the upstream maximum carries more mass flux: c* too high by up to
    1.47 % (B2O3), 1.31 % (LiF), 0.88 % (lean Al/O/H) and 0.33 % (the AP/HTPB/Al band),
    `C_F` and every area ratio wrong with it. AP/HTPB/Al at 7 MPa and
    h = −4.3383 MJ/kg: the tree 1 358.2288 m/s at p/p_c 0.5546, the true first maximum
    1 356.2237 at 0.6067 by cea 3.3.4's own sp solves. cea's rocket loop makes the same
    choice (1 358.2296), so the fixtures could not see it.
  - In 17 cases the first maximum is lower than a later one, and the tree took the
    first, contradicting only the wording "largest".

  The owner decided on 2026-09-28 for the first maximum. A convergent-divergent nozzle
  narrows monotonically to its throat, so the subsonic flow's `ρu` must grow all the
  way there, and the flow chokes at the first maximum; the second can be reached only
  through a nozzle that narrows again, a second throat (Schnerr and Leidner, "Internal
  flows with multiple sonic points", 1994). RP-1311 section 6.3.3 defines the throat as
  the minimum area ratio, "or, equivalently", the sonic point: the two coincide only
  where `ρu` has one maximum, and (6.18) places the throat at the melting point where
  the solid just appears, the plateau's first maximum. Found by the second hidden-defect
  audit of 2026-09-28 (Performance and Transport, finding F1).

  ⚠ 2026-09-26: stood "**Sonic throat.** At the throat `|u²/a² − 1| ≤ 4e-5` …". At a
  plateau edge `u²/a²` has no root, and the momentum update oscillated across the edge
  until `ThroatNotFound`. Cases where this happened:
  - AP/HTPB/Al at h − 2.25 MJ/kg, at 1, 3, 7 and 15 MPa: `u²/a²` jumps from 0.946 to
    1.121 at `AL2O3`'s 2327 K;
  - RP-1311 example 13 at 5 MPa, h + 250 kJ/kg: from 0.880 to 1.017 at `BeO`'s
    2851 K;
  - 85 of 648 variants of example 13.

  Found by the hidden-defect audit of 2026-09-26 (finding F1). The report defines this
  throat, in two places:
  - Section 6.3.3 defines the throat as the pressure "for which the area ratio is a
    minimum or, equivalently, for which the velocity of flow is equal to the velocity
    of sound". The report uses the second form. A minimum area ratio is the largest
    mass flux, the definition this invariant takes.
  - Section 6.3.4, "Discontinuities at Throat", covers the case where "the velocity of
    sound is discontinuous at the throat", as at "a melting point … being calculated
    at the throat". Its equation (6.18) estimates "the throat pressure at the melting
    point, where the solid phase just begins to appear" (Gordon 1970).

  Both passages were read in the report itself on 2026-09-26 (NASA NTRS 19950013764),
  at the owner's request that the session of 2026-09-13 not be trusted from memory. The bisection
  below finds the same point, to `1e-10` in `ln p`, without (6.18)'s linearization.
  The melting-plateau session of 2026-09-13 left (6.18) out: every fixture's throat
  then lay on a plateau or off it, never at its onset, and example 13 converged
  without it.

  The reference's code does not follow the report. cea 3.3.4 applies (6.18) only in
  the first three trials (`rocket.f90:538-554`), does not re-solve the state at the
  pressure it moves to, and never reports failure. Run the same day for the
  AP/HTPB/Al case at 7 MPa, it reports a throat at Mach 0.9197 with
  c* = 1411.7 m/s. The largest `ρu` over the package's own sp solves along the same
  isentrope gives 1330.444 m/s, 6.1 % lower. It lies at p/p_c = 0.584632 on the
  plateau's edge (2327.0 K): there `u²/a²` jumps from 0.947 to 1.119 and γ_s from
  1.180 to 0.999, while `ρu` is continuous. The state the package prints was solved at
  a pressure other than the one it reports, so no fixture is taken from its rocket
  solver there (the acceptance criterion below).
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

  ⚠ 2026-09-26: undefined flow model values (a cast, or a number in deserialized input)
  were solved as `FrozenAtThroat`, because every stage compared with two named values
  only (the hidden-defect audit, finding F5).
- Throat: initial pressure ratio from the chamber `γ_s` as in the report (6.15);
  the momentum update of (6.17) on the throat pressure; at most 20 iterations. The
  report stops at `|u² − a²|/u² ≤ 4e-5` (6.16); this node goes on to `1e-10` when it
  can, so that the reported throat is at rounding level, and accepts the report's
  tolerance as `Ok` otherwise.

  The bracket (2026-09-26). The search keeps the smallest pressure solved with
  `u²/a² < 1` and the largest solved with `u²/a² > 1`. When the 20 momentum
  iterations end without either tolerance and such a bracket exists, the search
  halves the bracket in `ln p`:
  - It stops when a solve meets the tight tolerance: that is the sonic throat.
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

    ⚠ 2026-09-28, the third pass: stood "a few bracket widths further toward the
    chamber, up to a bounded number of times, where `ρu` moves by about 1e-10
    relative" (eight steps of `1e-10`). Solved from the chamber's composition, the
    equilibrium solve stays on the pinned pair until about `1e-9` to `1e-8` in `ln p`
    above the edge: the bisection's own trials start from the previous trial's
    composition and so see the edge elsewhere. Eight steps of `1e-10` never reached
    the single-phase side, and whole Li/O/H bands that were `Ok` at `5a732f0` became
    `ThroatNotFound`: at 0.3 MPa, 21 of 26 cases (h 3.05625 to 3.36875 MJ/kg); at
    3 MPa, 10 of 12 (h 2.15 to 2.2109), among them the F3 bands the criterion below
    had ticked. The tests stayed green because the first-maximum fact returned without
    a check on a non-`Ok` throat, and the no-`ThroatNotFound` sweeps covered only
    AP/HTPB/Al and Li/O/H at 7 MPa. Found by the third audit pass (part 2, finding 1),
    which measured the geometric step closing all 32 cases with c* unchanged.

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

    ⚠ 2026-09-28, the third pass: the cap was 4, unrecorded here, and a walk that
    reached it accepted the unverified candidate as `Ok` (part 2, observation).

  `ThroatSearch` composes the local search and the check, and stays the only writer of
  the throat's figures. `FrozenAtChamber` flow is unaffected: its frozen sound speed is
  continuous, and the audit's oracle agreed with it in all 5 588 cases. The
  throat's figures are those of the state actually solved: its pressure ratio is `p_c`
  over that state's pressure (2026-09-26).

  ⚠ 2026-09-28: stood "A case whose momentum iterations converge never reaches the
  bisection, so no converging case changes". That was the defect of the ⚠ under the
  first-maximum invariant: nothing compared the accepted point with the pressures
  between the chamber and it. Also found by the same audit:
  - the plateau-edge state landed on the far side of the edge in 17 of 726 edge
    throats: Mach 1.011 to 1.124 with the pinned pair's `γ_s` (Li2O at 0.3 and 3 MPa;
    BeO/H2O at 15 MPa), c* right (finding F3);
  - with the chamber on the LiOH plateau at 7 MPa, 24 of 50 cases ended
    `ThroatNotFound` and 26 `Ok`, alternating with the enthalpy by the sign of a
    rounding-level `u²` at the first trial (finding F4);
  - the bisection's end never tested the report's tolerance, and a `break` kept the
    previous trial's `u²/a²` (observations O1 and O2, by reading).

  ⚠ 2026-09-26: after an exhausted search the throat's pressure ratio and the
  reference the exits start from used the pressure the last update produced, not the
  one the reported state was solved at. The mismatch is at most 2.3e-5, and it was
  never reached by a fixture (the audit's finding F6, by reading).
- Exit by area ratio: initial pressure ratio from the report's estimates, the
  correction of (6.23)–(6.24) on `ln(p_c/p_e)`; at most 20 iterations; the
  supersonic branch only. An area ratio not above 1 is `AreaRatioInvalid`, as the
  reference refuses it ("Supersonic area ratio must be greater than 1.0", cea 3.3.4
  `rocket.f90:888-890`); the throat itself is station 1. The report stops at `4e-5` on
  the correction (6.25); this node goes on to `1e-10` when it can. A pass that lands on
  the subsonic side is stepped outward and never accepted: a station is accepted only
  on a supersonic pass (2026-09-26).

  ⚠ 2026-09-26: stood "an area ratio below 1 is `AreaRatioInvalid`", which admitted
  exactly 1. There the first estimate is the throat, where the derivative (6.23)
  vanishes. The correction was rounding noise over rounding noise, and the station's
  fate hung on the sign of `u² − a²` at the throat's last iterate, about 1e-11. It
  failed in one of the 98 fixtures when their exits were set to 1, and in 43 of 1 420
  sweep cases. That sign is a last-bit quantity that libdevice and .NET can decide
  differently. Also, a subsonic final pass kept the verdict of the supersonic pass
  before it and was accepted: 164 `Ok` stations had Mach below 1, all at ε = 1. Found
  by the hidden-defect audit of 2026-09-26 (findings F2 and F3).

  ⚠ 2026-09-12: stood "initial pressure ratio from the isentropic relation with the
  throat γ_s". The report's own estimates are used instead: the extrapolation with the
  derivative (6.23) from the previous station when both area ratios exceed 2, else its
  empirical formulas (6.21) for ratios up to 2 and (6.22) above. The formulas were
  recovered from the report's text, whose typography lost the range boundaries; the
  ranges are this node's reading, and they only change how many iterations a station
  takes, never where it converges.
- Exit by pressure ratio: one sp solve at `p_e`; the area ratio is an output. In
  `FrozenAtThroat` flow a pressure ratio not above the throat's is `InvalidInput` for
  that station (2026-09-28): upstream of the freezing point the flow is in equilibrium,
  and the reference omits such a point ("FOR FROZEN PERFORMANCE, POINT OMITTED BECAUSE
  ASSIGNED pi/p IS LESS THAN VALUE AT nfz", cea 3.3.4 `rocket.f90:725-732`).

  ⚠ 2026-09-28: such a station was solved with the throat's frozen composition at
  (s_c, p), where near `p_c` its enthalpy exceeds the chamber's; the negative `u²` was
  clamped to 0, and the station was `Ok` with Mach 0 and infinite `A/A_t` and `Ivac`
  (339 stations of the audit's exits sweep), or finite with `Isp` 2 % to 36 % low.
  Found by the second hidden-defect audit of 2026-09-28 (finding F5). The clamp stays
  for the other flows, where the audit found every station finite.
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

## Structure

Decided 2026-09-14 (the clean-code pass; the root's code-shape constraint). The rocket
solve of one case is one public entry over internal static stage classes, all
kernel-compatible, one class per file in this directory and namespace, sharing the
existing view, scratch and result structs. Every floating-point expression keeps its
present form and its present order of evaluation; the bit snapshot of the tests node
(the acceptance criteria below) is the proof that the decomposition moved code and
rewrote no formula.

⚠ 2026-09-15 (distribution phase): "one public entry" and `RocketSolver`'s row below
stood before the API review of that day (fixed in `24156be`) found no
consumer scenario for it, `ExitSpecification`, `RocketProblem`, `RocketLayout` or
`RocketResult`: every use is `Execution` composing the kernel, `Problems` building a
batch, or this node's own tests. All five moved into `API.md`'s tree-contract
section; `APThermo.Performance.csproj` grants `InternalsVisibleTo` to `Execution`,
`Problems`, `Execution.Tests` and `Benchmarks`. `FlowModel` and `PerformanceFigures`
stay public. The entry point is internal now, reached only through the grant; the
decomposition itself (one class per stage) is unaffected.

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

Carriers (`Carriers.cs`): `RocketContext` (the table view, the problem, the scratch
and the result, built once in `Solve`), `ChamberReference` (pressure, enthalpy,
entropy, `γ_s`), `ThroatReference` (pressure, mass flux, `c*`, the logarithm of the
pressure ratio, `γ_s`), `StationRequest` (the station index, pressure, temperature
estimate, entropy and flow), `StationFigureInputs` (a station's velocity, area ratio,
pressure ratio and `c*`, the four `StationFigures.Write` needs beside the chamber's
entropy), `ThroatQuery` (the case, the chamber and the flow model, what every stage of
`ThroatBracketSearch` shares; its constructor picks the flow from the problem, keeping
that choice out of `ThroatSearch`'s own coupling count, 2026-09-26), `ThroatBracket`
(the smallest-subsonic / largest-supersonic pressure bracket the momentum trials and
the bisection track and narrow, 2026-09-26), `ExitEstimate` (the extrapolation state
carried between exits), `PhaseBoundaryEnd` (a point's pressure, temperature, sonic
ratio and condensed fingerprint, 2026-09-28), `PhaseBoundaryQuery` (the outer end,
closer to the chamber, whose fingerprint `PhaseBoundaryLocator.Locate` holds fixed
while it narrows, and the inner end, closer to the candidate throat, 2026-09-28),
`PhaseBoundary` (one located boundary's `Hi` and `Lo` ends, 2026-09-28), and the enums
`StationFlow { Shifting, Frozen }` (in place of
the boolean that picked the solver) and `ExitOutcome { Converged,
WithinReportTolerance, NeverSupersonic, SolveFailed }`.

⚠ 2026-09-14, found in the coding: two of those carriers are not what the design
wrote. `ExitOutcome` has a fifth value, `NotMet`: the four above name every ending of
the iteration but one — twenty corrections whose last is above the report's tolerance
— which the code of `8e36a27` ended as `NotConverged` and which must keep ending so;
with four values that ending had no name and would have had to borrow one. And
`ExitEstimate` carries `Temperature` beside the extrapolation state: it is the
temperature estimate of the last station that converged, which the exit loop used to
read from that station and pass down, and carrying it here keeps
`AreaRatioIteration.At` at the six parameters the root's code shape allows. The
station's verdict is written by the iteration itself (`NotConverged` for
`NeverSupersonic` and for `NotMet`), so the exit loop reads one thing — the station
status — as it did before.

⚠ 2026-09-15: the reason is the estimate, not the count: the temperature and the
extrapolation state are both what the next exit starts from (Constraints); that
`AreaRatioIteration.At` stays at six parameters follows from it.

Decisions taken with the review of 2026-09-14:

- **A station that never went supersonic is `NotConverged`.** The area-ratio iteration
  accepted a station whose correction was never computed, because its acceptance test
  read an initial value of zero (the review's F-PF-01). The outcome is now a value:
  `NeverSupersonic` ends the station as `NotConverged`, as `API.md` always said, and
  the subsonic step of `0.1` in `ln(p_c/p_e)` is the named constant `SubsonicStep`. No
  fixture reaches the path; the tests node drives it through `AreaRatioIteration` from
  an estimate deep on the subsonic side.
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

  ⚠ 2026-09-14: this bullet stood "no composition-root exception is expected,
  `ExitStations` staying at or under Ce 10 with `AreaRatioIteration` split from it".
  The estimate counted the names in the source. Measured by the dependency check's
  walk, which also counts the types of the fields a body reads and of the members it
  calls, the decomposition gave `ExitStations` 16, `ChamberSolve` 14,
  `AreaRatioIteration`, `RocketSolver` and `ThroatSearch` 13 and `StationSolve` 12.
  The root recalibrated its limit to 14 on that walk; `ExitStations` is the one stage
  above it.
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

No type of this node names more than 14 distinct types of the tree by the dependency
check's walk, outside the row above (`ExitStations` and `ChamberSolve` tie at 14, the
ceiling): the Size bullet's composition-root exception, reserved above for
`ExitStations` before `PressureRatioStation` was split from it, is not claimed, and
this node needs no other efferent-coupling row.

## Acceptance criteria

- [x] 2026-09-12 — For the reference rocket cases of the four propellants and
      RP-1311 example 8 (LOX/LH2) and example 12 (MMH/NTO, equilibrium and frozen with
      freezing at the throat), the chamber, throat and exit temperatures, pressures,
      `M`, `γ_s`, sound speed, Mach, `c*`, `C_F`, `Isp` and `Ivac` agree with the
      fixtures within the tolerance table; the list of compared fields is generated
      from the fixture. `Performance.Tests`,
      `RocketFixtureTests.TheRocketCaseReproducesTheReference` over the enumerated
      `cases/rocket` directory (89 that day): every numeric station output mapped by name to
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
      reference: the same test over the `frozenAtChamber` and `frozenAtThroat`
      propellant cases and example 12 (`nfz = 2`), enumerated from the fixture directory.

      ⚠ 2026-09-15: this bullet stood "the 15 `frozenAtChamber` and 40 `frozenAtThroat`
      propellant cases". `frozenAtThroat` was 35 propellant cases (plus example 12)
      already on 2026-09-12, the date of this tick, at every commit the repair review
      checked (BASE and 7661ea9 alike): the count was not a figure that went stale with
      time, it was wrong when written. Found by the repair review (R-Performance-7); the
      bullet now points at the enumerated fixture directory instead of a typed count
      that can drift again.
- [x] 2026-09-14 — The invariants' tolerances (entropy, sonic condition, area ratio,
      frozen composition bit for bit, velocity from the energy equation) hold for every
      converged case of the enumerated rocket fixtures, one test per invariant:
      `InvariantTests.TheThroatIsSonic`,
      `InvariantTests.EntropyIsConstantAlongTheNozzle`,
      `InvariantTests.VelocityFollowsTheEnergyEquation`,
      `InvariantTests.AssignedAreaAndPressureRatiosAreMet`,
      `InvariantTests.TheCompositionIsFrozenAfterTheFreezingStation`; green on
      the decomposed code at `5cb2664`. Re-dated from 2026-09-12, when one theory,
      `InvariantTests.Entropy_sonic_throat_area_ratio_and_frozen_composition_hold`,
      held them over the 89 files of that day: the tests node split it (its F-TK-06
      and F-TK-07) and left the typed count out (F-TK-03), and this node's code was
      decomposed on this date, which a tick of an earlier date cannot prove
      (AGENTS.md §6).
- [x] 2026-09-12 — An area ratio below 1 returns `AreaRatioInvalid` for that station
      and leaves the other stations unaffected:
      `InvariantTests.AnAreaRatioBelowOneFailsItsStationOnly` (and
      `APressureRatioNotAboveOneFailsItsStationOnly`,
      `ACaseWithoutExitsGivesTheChamberAndTheThroat`,
      `ANonPositiveChamberPressureIsInvalidInput`).
- [x] 2026-09-12 — Runs unchanged inside an ILGPU kernel on the CPU accelerator with
      the same results as the host call: `KernelEqualityTests.KernelAndHostGiveTheSameBits`
      over the batches of fixtures sharing a table and an exit layout, enumerated from the
      fixture directory (states, figures, moles and statuses bit for bit).
- [x] 2026-09-14 — The decomposition of 2026-09-14 (`## Structure`): every type of the
      node within the root's code-shape constraint. The largest methods,
      `AreaRatioIteration.At` and `ThroatSearch.At`, are within the root's limit of 60
      lines of code (`ShapeTests.NoMethodSpansMoreThan60Lines`); before the
      decomposition `RocketSolver.Solve` alone was 238 lines in a 326-line file. The
      largest type is under 120 lines (`Carriers.cs`, seven small carriers, none of
      them individually near the limit); before, the single `RocketSolver` type was
      326 lines. Confirmed by the tree-wide inventory (nothing of this node in its
      list of types ≥ 250 or methods ≥ 60 lines) and by `Protocol.Tests` (9 of 9
      green, run before and after this node's work; its `ShapeTests` does not exist
      yet and re-measures this over the tree when that node has it). The
      public surface is unchanged: `Protocol.Tests.SurfaceTests` green against the
      unchanged `PublicSurface.approved.txt`; every new type of the decomposition
      (`RocketContext`, `ChamberReference`, `ThroatReference`, `ExitEstimate`,
      `ExitOutcome`, `StationFlow`, `StationRequest`, `ChamberSolve`, `ThroatSearch`,
      `ExitStations`, `AreaRatioIteration`, `StationSolve`, `StationFigures`) is
      `internal`, reached by the tests node only through the new
      `InternalsVisibleTo`. Every rocket fixture is bit for bit as at `8e36a27` on the
      CPU accelerator: `BitSnapshotTests` green against `Bits.approved.txt` recorded
      at step 0 (`c038877`) and unmoved since, through every later step including the
      tests-node refactor of the criterion below.
      `KernelEqualityTests` green (post its own F-TK-07 split). Every criterion above
      still green, and every criterion of `tests/Performance.Tests/BOOT.md`: the full
      fast suite, `dotnet test APThermo.sln --filter
      "Category!=LongRunning"` with `APTHERMO_NO_CUDA=1`, 2632 tests, 0 failed, 0
      skipped. The execution tests node's CUDA sweep and throughput benchmark are
      `Category=LongRunning`; the 2026-09-14 coding session's instructions directed
      leaving them to the orchestrator after the merge, so they were not run then —
      the one part of this criterion not verified that session.

      ⚠ 2026-09-14: the parenthetical on `Protocol.Tests` first read "9 of 9 green,
      `ShapeTests` included". The protocol tests node had no `ShapeTests` then (its
      Shape level is still planned): the nine were the existing reflection checks,
      and the shape figures above come from the tree-wide inventory.

      ⚠ 2026-09-15: this bullet named `AreaRatioIteration.At` at 53 physical lines
      (`ThroatSearch.At` next, at 52 physical lines): the 53 came from the tree-wide
      inventory (`inventory.py` at `5281b7e`), where `ShapeMeasures` did not yet
      exist, and it still counted blank and comment lines toward the span. `At` was
      53 lines only because
      `a2a891b`, later the same day, split its verdict into a separate `Close` to fit
      the then-current physical-line rule; the repair review found the split
      count-driven, writing a station's extrapolation state before its verdict for no
      reason the code itself states (R-Performance-1). Commit `9a8ee68` (2026-09-15)
      merges `Close` back into `At`/`Accept`, restoring the shape this bullet's own
      tick predates: `ShapeTests.NoMethodSpansMoreThan60Lines` now holds both
      methods within the root's 60-line-of-code limit.
- [x] 2026-09-14 — An exit station that never leaves the subsonic side of the sonic
      point is `NotConverged` and its neighbours are `Ok`:
      `Performance.Tests.SubsonicStationTests.AStationThatNeverLeavesTheSubsonicSideIsNotConverged`
      drives `AreaRatioIteration` (through `InternalsVisibleTo`) from an estimate two
      units of `ln(p_c/p_e)` below the throat's, where the twenty subsonic steps of
      `SubsonicStep` cannot reach the sonic point, and reads the station's status and
      its neighbours'. Seen red once against the acceptance test of `8e36a27`, which
      read the never-written correction as zero and returned the station `Ok`; green
      with `NeverSupersonic` ending the station as `NotConverged`. No fixture reaches
      the path: the bit snapshot of all 98 rocket fixtures did not move.
- [x] 2026-09-14 — The pressure-ratio station is a stage of its own (`## Structure`,
      Size): `PressureRatioStation` holds what `ExitStations.AtPressureRatio` held,
      moved with every expression in its form and its order of evaluation (its own
      file, `PressureRatioStation.cs`); `ExitStations` keeps the loop, the dispatch,
      the estimate chain and the case status, holding no formula. Efferent coupling
      by the dependency check's walk, measured by the scratch tool that reproduces
      it (`AGENTS.md` §13; the tool used for the root's recalibration to 14) run over
      this step's build: `ExitStations` 14, `ChamberSolve` 14, `AreaRatioIteration`
      13, `RocketSolver` 13, `ThroatSearch` 13, `StationSolve` 12,
      `PressureRatioStation` 11 — every stage at or under the root's limit of 14, so
      `ExitStations` does not need the composition-root exception the Size bullet
      allowed for, and this node needs no efferent-coupling row.

      ⚠ 2026-09-14: the last clause stood "this node needs no `## Shape exceptions`
      table". It was true when written, of the coupling rule this criterion measures;
      later the same day the descriptors' constructors became a declared exception to
      the parameter rule (the decision "The descriptors keep their constructors"), and
      the table now carries their two rows, while no coupling row is needed.
      `BitSnapshotTests.EveryRocketFixtureGivesTheRecordedBits` green with
      `Bits.approved.txt` unmoved (byte for byte before and after this step) and
      `KernelEqualityTests` green: `dotnet test tests/Performance.Tests`, 699 tests,
      0 failed, 0 skipped. The public surface is unchanged
      (`Protocol.Tests.SurfaceTests` green; the new type is internal). The execution
      tests node's fast set green on CUDA on the reference machine: `dotnet test
      tests/Execution.Tests --filter "Category!=LongRunning"`, no `APTHERMO_NO_CUDA`,
      41 tests, 0 failed, 0 skipped.
- [x] 2026-09-14 — Every creation of `RocketProblem` and `RocketResult` in the tree
      names its arguments (the decision "The descriptors keep their constructors"), the
      protocol tests node's named-construction fact green once it exists; the tests
      node's bit snapshot unchanged. A scan of every `new T(…)` and `T x = new(…)` in
      `src/` and `tests/` (a script outside the tree) finds four sites of each of the two
      types, in `Execution`'s `Kernels`, `Execution.Tests`' `HostSolves` and
      `Performance.Tests`' `KernelEqualityTests` and `RocketCase`, every argument named;
      the builds of `Execution`, `Execution.Tests` and `Performance.Tests` after the
      change carry the IL of the builds before it, method by method, string literals
      compared by value (one differs: the source path a test embeds at compile time),
      so no argument binds to another parameter; `Performance.Tests` (699) and the fast
      set of `Execution.Tests` (41) green; `tests/Performance.Tests/Bits.approved.txt`
      unchanged (blob `5aa32f2b` before and after). The fact,
      `ShapeTests.EveryWideConstructorIsCalledWithNamedArguments`, is designed
      and not yet written; it takes over as the evidence when it is.
- [x] 2026-09-15 — Every ticked criterion above re-verified on the decomposed and
      repaired code at `62cd99e`: its tests green in the full suite
      (`APTHERMO_NO_CUDA=1`, every category, 3037 tests, none skipped), and
      CUDA-category evidence on the reference machine (`tests/Execution.Tests`, 41,
      and the long-running sweep and throughput tests).
- [x] 2026-09-27 — The audit's findings F1, F2, F3, F5 and F6 and the entropy check
      (the ⚠ notes of 2026-09-26), coded at `e1318c2` on the fixtures of `efe7d7e`/`8c1c1cb`:
      - **The plateau-edge throat.** The six audit cases end `Ok`
        (`ThroatFixtureTests.TheThroatCaseReproducesTheReference`, 10/10, over the
        fixtures node's `throat` family):
        - AP/HTPB/Al at h − 2.25 MJ/kg, at 1, 3, 7 and 15 MPa;
        - AP/HTPB/Al at 7 MPa, h − 2.275 MJ/kg;
        - RP-1311 example 13 at 5 MPa, h + 250 kJ/kg.

        The three properties, each checked independently of the fixture's own
        reference (`tests/Performance.Tests/ThroatPlateauEdgeTests.cs`):
        - `ThePlateauEdgeHasTheGreatestMassFluxNearby`: the throat's `ρu` is not below
          that of the package's own sp solves, driven directly through `StationSolve`,
          at `p(1 ± 1e-4)`. Shown red once by loosening `ThroatBracketWidth` from
          `1e-10` to `1e-2`: all six red (a throat flux measurably below a neighbour's,
          e.g. `11274.175373307384` against `11274.22650204147`), reverted.
        - `ThePlateauEdgeIsSinglePhaseAndSubsonic`: `Mach < 1`, `GammaS > 1.05` and
          `CpEquilibrium > 0`, the way the plateau-edge reseed fix was itself
          diagnosed. Shown red once together with the fixture theory below by removing
          the chamber-composition reseed in `ThroatBracketSearch.Resolve`: 3 of 6
          fail (`GammaS` collapsing to about `0.999`, `Mach` above 1, e.g.
          `1.0578068050772251` at 7 MPa), and 4 of 6 `ThroatFixtureTests` fail the
          same way (`cpEquilibrium` reference `1931.57`, tree `0`); reverted.
        - c* and the throat pressure ratio match the fixture within tolerance:
          `ThroatFixtureTests`, above.

        `TheExample13SweepNeverEndsThroatNotFound` (both flow models, 9 chamber
        pressures 2 to 40 MPa times 5 enthalpy offsets ±400 kJ/kg around the family's
        own example-13 mixture, 45 cases per model): no `ThroatNotFound`, and every
        `Ok` case also passes the supersonic-acceptance and throat-figures facts below.
        Not `Category=LongRunning`: both theories together run under a second.

        Shown red once as a whole, matching the pre-F1 code's behaviour: removing the
        momentum loop's `bracket.Track` call (so the bracket never completes) reproduces
        exactly the six plateau-edge fixtures as `ThroatNotFound` (iterations `[k, 7]`,
        the momentum-only four unaffected), confirming the bracket-and-bisection
        mechanism as a whole against the fixtures it was built for; reverted.
      - **ε = 1.** `AnAreaRatioOfExactlyOneIsInvalid`: an area ratio of exactly 1 is
        `AreaRatioInvalid` for that station only, on the audit's failing fixture
        `lox-rp1_of3.2_pc7MPa_shiftingEquilibrium`. Shown red once by restoring the
        pre-fix `!(value >= 1.0)` gate: the station converges to `NotConverged`
        instead (never `AreaRatioInvalid`); reverted.
        `AnAreaRatioJustAboveOneIsOkAndSupersonic`: 1 + 1e-9 on the same fixture is
        `Ok` with `Mach ≥ 1`.
      - **Supersonic acceptance.** `EveryAcceptedAreaRatioExitIsSupersonic`: over every
        rocket fixture, every `Ok` area-ratio station has `Mach ≥ 1`; also checked over
        the example-13 sweep above. Vacuously true on every one of the 98 rocket
        fixtures and the sweep for the general stale-verdict mutation (removing
        `!lastPassSupersonic ||`), because F2's ε = 1 gate now rejects the one input
        the audit's own scan tied the defect to (the ⚠ of 2026-09-26 under
        Constraints: "all at ε = 1") before `AreaRatioIteration.At` ever runs on it.
        `AnExitAtTheThroatsOwnAreaRatioIsNeverAcceptedOnAStaleSupersonicVerdict` drives
        the station directly, under the gate, at that exact input: a scan of all 98
        rocket fixtures with the guard removed found the defect reproduces on
        `lox-lh2_of8_pc10MPa_shiftingEquilibrium` (`Ok` at `Mach 0.9999998296014142`)
        and three others; the fixture test uses the first and is shown red once by the
        same mutation, reverted.
      - **Flow models.** `AnUndefinedFlowModelIsInvalidInput`: `(FlowModel)7` and
        `(FlowModel)(-1)` are `InvalidInput` for the whole case. Shown red once by
        dropping `IsDefinedFlow` from `RocketSolver.Solve`'s gate (kept referenced to
        build): both `Ok` instead of `InvalidInput`; reverted.
      - **Throat figures.** `PressureRatioMatchesTheSolvedPressure` and its throat-family
        and sweep counterparts: every station's `PressureRatio` equals `p_c` over its
        state's pressure bit for bit, over every rocket fixture, the throat family and
        the sweep. The exhausted-search path — where the pressure actually solved must
        be reported, not one step past it — is driven by the same six plateau-edge
        fixtures the bracket mechanism above is shown red on: mutating the plateau-edge
        return to `bracket.SupersonicPressure` (the wrong end) is caught by
        `ThroatFixtureTests`' bit-for-bit comparison to the reference, not by this fact
        alone (see the note below). The momentum loop's own natural-exhaustion case
        (within `SonicTolerance` but not `TightTolerance` at iteration 20) is not
        reached by any of the 108 rocket-and-throat fixtures: Newton's step converges
        to `TightTolerance` well inside 20 iterations wherever it converges at all, and
        the six genuinely discontinuous cases exhaust far short of `SonicTolerance`
        instead, going to the bracket. A blanket mutation reproducing the historical
        "one momentum step past" bug (unconditionally overwriting the reported pressure
        with the next candidate) left every one of the 108 fixtures unaffected, because
        it is a no-op whenever the loop breaks on convergence (candidate and solved
        pressure are equal there) — confirming, rather than contradicting, that no
        fixture exercises that exact branch.
      - **Entropy.** `EntropyCheckTests`: a station whose entropy is perturbed by ten
        times the tolerance after a real solve is written `NotConverged`; one within a
        tenth of the tolerance is left `Ok`. Shown red once by loosening the check's
        tolerance factor by `1e300`: the perturbed station reads `Ok` instead of
        `NotConverged`; reverted. No fixture's own converged entropy ever drifts enough
        to trigger the check (every rocket, throat and sweep case passes it
        unperturbed).
      - **Bits and records.**
        - No bit snapshot moves (`git diff --stat` over every `Bits*.approved.txt` and
          `Throughput*.approved.txt`, empty). No existing fixture reaches the
          bisection or the plateau edge: a temporary instrumented count (three
          counters in `ThroatBracketSearch`, a throwaway theory over every `rocket` and
          `throat` fixture, both removed before the commit) gave momentum-only 102,
          bisected 0, plateau-edge 6 — the 6 are exactly the new throat family's own
          plateau cases (98 rocket + 4 throat momentum-only, 6 throat plateau-edge,
          0 bisected anywhere), so no *existing* fixture's path or bits move.
        - `API.md` states the statuses and the pressure-ratio and supersonic rules
          above.
        - The guide's troubleshooting row for `AreaRatioInvalid` reads "not above 1".
        - `CHANGELOG.md` names the fixes and the `AreaRatioInvalid` change for ε = 1.

      Evidence: `dotnet build APThermo.sln`, 0 warnings 0 errors; `APTHERMO_NO_CUDA=1
      dotnet test APThermo.sln --filter "Category!=LongRunning"`, every project green
      (`Performance.Tests` 937/937, `Protocol.Tests` 28/28, the shape and coupling
      facts included after `ThroatSearch` was split into `ThroatSearch` and
      `ThroatBracketSearch`, `## Structure` below); the protocol lint, 0 errors,
      0 warnings.

      ⚠ 2026-09-28: `ThePlateauEdgeHasTheGreatestMassFluxNearby` compared the throat
      with `p(1 ± 1e-4)` only, and `TheThroatIsSonic` accepted any sonic point, so a
      throat at the second maximum passed both (the ⚠ of that date under Invariants).
- [x] 2026-09-28 — The second hidden-defect audit of 2026-09-28 (Performance, findings F1, F3, F4,
      F5 and observations O1, O2; the guards part's O3 and O4) is closed by the rules of
      that date.
      - **The first maximum (F1).** A mass-flux oracle in the tests node: `ρu` from sp
        solves driven through `StationSolve` at pressures between the chamber and the
        throat, dense enough to see a plateau (the audit used 121 points over
        p/p_c 0.30 to 0.90, refined by golden section). The fact: the throat's `ρu` is not
        below the oracle's at any pressure between the chamber and the throat, and no
        local maximum of the oracle lies above the throat's pressure. Over:
        - the AP/HTPB/Al band h −4.42 to −4.32 MJ/kg at 1, 3, 7 and 15 MPa;
        - the lean Al/O/H band (Al 0.08, O2 0.62, H2 0.30 by mass, 7 MPa, around
          h = 1.9125 MJ/kg);
        - a B2O3 and a LiF case from the audit's sweeps (its harness,
          `scratchpad/audit2/harness/pt/performance/`, and outputs, `out/pt/`);
        - every rocket and throat fixture.

        Red at `5a732f0` on the bands, green after. The two named cases carry their
        numbers: AP/HTPB/Al at 7 MPa, h = −4.3383 MJ/kg, c* within the fixtures'
        tolerance of 1 356.2237 m/s at p/p_c 0.6067; lean Al/O/H c* of 2 729.983 at
        0.585897. The references are the audit's cea sp solves; they become fixtures
        of the `throat` family through its generator (`throat_scan.py`), not typed
        numbers. The reverse cases keep their first maximum: AP/HTPB/Al at 7 MPa,
        h = −4.3733 MJ/kg, c* 1 356.2311 at 0.60666.
      - **The far side (F3).** The Li2O cases at 0.3 MPa (h 3.294 to 3.375 MJ/kg) and
        3 MPa (h 2.206 to 2.213) and BeO/H2O at 15 MPa (h −11.069): the edge throat is
        single-phase and subsonic. Red at `5a732f0`.
      - **γ_s = 1 (F4).** Li/O/H at 7 MPa with the chamber on the LiOH plateau, the
        audit's 50 cases (h −8.1125 to −8.1 MJ/kg and beyond, 6.25 kJ/kg steps): all
        `Ok`, none `ThroatNotFound`. Red at `5a732f0`.
      - **Frozen at the throat (F5).** Pressure ratios 1 + 1e-12, 1 + 1e-9, 1.0001 and
        1.01 in `FrozenAtThroat` flow on every rocket fixture: each station at or above
        the throat's pressure is `InvalidInput`, and no `Ok` station of any flow carries
        a non-finite figure. Red at `5a732f0`.
      - **The loop's reading (O1, O2).** A unit fact each on `ThroatBracketSearch`: a
        bisection ending within the report's tolerance is `Ok`; a `break` never leaves a
        stale ratio.
      - **Hand-typed lists (guards O3, O4).** The example-13 sweep asserts its count of
        `Ok` cases, not only the absence of `ThroatNotFound`. `PlateauEdgeCases` is
        generated from the `throat` family (the fixtures whose path reaches the
        plateau edge), not typed.
      - **Bits.** A throat that moves to the first maximum moves no committed fixture's
        bits if the audit is right that no fixture has two maxima. The coder counts the
        fixtures that reach the new stages; a moved bit snapshot is re-approved only with
        the case named, its oracle shown, and every CEA tolerance test green.

      ⚠ 2026-09-28, corrected at the coordinator's review: the paragraph below (same
      date) stood in place of this one, substituting `ThePlateauEdgeIsSinglePhaseAndSubsonic`
      for F3 and the AP/HTPB/Al band for F4 because the audit's own Li2O, BeO/H2O and
      Li/O/H scratch cases (its harness under `scratchpad/`) were thought unavailable in
      this worktree. The review found both substitutions insufficient:
      `ThePlateauEdgeIsSinglePhaseAndSubsonic` ran on the six fixtures already committed,
      every one of which happens to land on the chamber side even with the old,
      unconditional accept (so the fact was green before the fix too, proving nothing),
      and the AP/HTPB/Al band's chamber `GammaS` (0.9994) is not the audit's `γ_s = 1`
      degeneracy, so that sweep exercises the ordinary branch of equation (6.15), not
      the limit. The audit's harness and reports are at absolute paths outside the
      tree (kept there per `AGENTS.md` §2) and were read directly, at the coordinator's
      direction, to build the evidence below.
      - **F3** is proven on representative points of the audit's own Li2O (`li-o-h`)
        and BeO/H2O (`be-o-h`) systems, generated into the `throat` family
        (`PLATEAU_EDGE_CASES`, Fixtures BOOT.md): `SecondAuditFixTests.ThePlateauEdgeAcceptsTheChamberSideOnTheAuditsLi2OAndBeOCases`.
        Shown red once by reverting `AcceptPlateauEdge` to its old, unconditional
        accept: the 0.3 MPa Li2O case's Mach then measures 1.0674725047617122 (inside
        the audit's own reported range for that band, "Mach 1.067 to 1.124"), against
        0.9803150262208528 with the fix; the 3 MPa Li2O and 15 MPa BeO/H2O points do
        not move under that revert (this system's exact numbers already land on the
        chamber side on the first, unchecked trial), so they stand as the audit's own
        further citations, not as independent red-once proofs.
      - **F4** is proven directly against the degenerate formula, not against a real
        mixture: `SecondAuditFixTests.TheGammaOneLimitProducesABracketInsteadOfThroatNotFound`
        builds a synthetic `ChamberReference` with `GammaS` set to the literal `1.0`
        (every other field borrowed from a real, ordinary chamber solve) and calls
        `ThroatBracketSearch.Locate` directly. Shown red once by reverting the limit
        and the u² ≤ 0 step together: the search then ends `ThroatNotFound`, its one
        trial solved at exactly the chamber's own pressure (equation (6.15) computing
        `Math.Pow(1, ±∞) = 1`), giving u² = 0 exactly and no bracket ever tracked. The
        audit's own "no `ThroatNotFound`" claim, over its own `li-o-h` system, 7 MPa
        and 50-case sweep width, is proven separately
        (`TheThroatSearchNeverEndsThroatNotFoundAcrossTheLiOHPlateauAt7MPa`): all 50
        `Ok`. Its own bit-exact `γ_s = 1` no longer reproduces on this branch after the
        Equilibrium rules A and B (merged into this branch after the audit's own
        snapshot at `5a732f0`): the mixture's chamber `GammaS` measures
        0.999999999446852 here, inside `RocketSolver.GammaOneTolerance` (1e-6) but not
        bit-exact — a separate fact,
        `TheChamberSGammaSIsWithinTheLimitsToleranceOnTheAuditsNamedLiOHPoints`, checks
        the tolerance on the audit's own nine named points, which is why the formula-level
        fact above, not a real-mixture sweep, is what actually proves the fix. That fact
        also asserts the sweep's full width: all 50 cases `Ok`, none `ThroatNotFound`, not
        only the nine named points (`TheThroatSearchNeverEndsThroatNotFoundAcrossTheLiOHPlateauAt7MPa`);
        the nine-point tolerance fact is a second, narrower one over the same sweep.

        ⚠ 2026-09-28, `RocketSolver.GammaOneTolerance = 1.0e-6` justified (asked at the
        coordinator's second review): a throwaway probe (`ZzDiag2.cs`, deleted before this
        commit) ran equation (6.15)'s own ordinary branch, `1.0 / Math.Pow(0.5 * (gamma +
        1.0), gamma / (gamma - 1.0))`, in this tree's own arithmetic, for `gamma = 1.0 +
        delta` at `delta` from 1e-3 down to 0, against the analytic limit `Math.Exp(-0.5)`.
        The ordinary branch's own relative error scales cleanly as ≈0.375·`delta` from
        `delta = 1e-3` (relative 3.748e-4) down to `delta = 1e-13` (relative 3.752e-14),
        then breaks down: at `delta = 1e-14` the relative error jumps to 1.117e-2, at
        `1e-15` to 0.1052, and at `delta = 0` (or any `delta` below one ULP of 1.0) the
        ordinary branch collapses to exactly 1 (`Math.Pow(1, ±∞) = 1` firing, F4's own
        degeneracy), a relative error of 0.6487. `GammaOneTolerance = 1e-6` sits about
        eight orders of magnitude above where the ordinary branch's own error becomes
        significant (≈1e-13): choosing the limit branch there costs no accuracy the
        ordinary branch would otherwise have kept, since its own error at `delta = 1e-6`
        (3.751e-7) is already inside the fixture tolerance table's 1e-4 rows. The figure is
        set instead by the pinned-pair convention's own numbers, not by the formula's
        accuracy floor: the audit's harness measured the `li-o-h` mixture's chamber
        `GammaS − 1 = 0` exactly at `5a732f0`; after the Equilibrium rules A and B it
        measures 5.53e-10 here (above, F4). `1e-6` is about 1 800× above that measured
        deviation, leaving headroom against further last-bit drift in `Equilibrium`, while
        staying about 600× below the AP/HTPB/Al system's own, genuinely distinct chamber
        exponent (0.9994, `|Δ| = 6.00e-4`, not a pinned-pair state) found during the F4
        review. The tolerance therefore separates the pinned-pair convention's own
        last-bit noise from a real, non-degenerate exponent with three to four orders of
        margin on each side, far inside the range where the ordinary branch would still
        compute the correct limit on its own.
      - **The bit move.** `rp1311-example13` (rocket family) moved: its own record is
        `tests/Performance.Tests/BOOT.md`'s 2026-09-28 entry (the oracle's one maximum —
        the audit's "no fixture has two maxima" holds for this case — and the actual
        mechanism, `UpstreamChokeCheck`'s unconditional re-solve across a melting-plateau
        boundary the search crosses on its way to the sonic point, not a second maximum).
        `tests/Problems.Tests/BOOT.md` records the same case's `Bits.approved.txt` move.

        ⚠ 2026-09-28, the move measured field by field (asked at the coordinator's second
        review): a throwaway diagnostic (`ZzDiag.cs`, deleted before this commit) printed
        the throat station's pressure, temperature, c*, mass flux, `GammaS`, Mach and every
        non-zero mole fraction, once with `ThroatSearch.At`'s `UpstreamChokeCheck.Verify`
        call temporarily skipped (the pre-`627f815` path, `ThroatBracketSearch.Locate`'s
        own candidate stands unchanged) and once with the tree's current code. Pressure
        (12694259.494254986), temperature (2851.0000144702376) and `GammaS`
        (0.9978925188362665) are bit-identical between the two runs — the momentum search
        finds the same candidate either way, as the doc comment above already says. Every
        other field differs only at the rounding floor: the largest relative change of any
        field is 4.378e-13, on the condensed `BeO(b)` mole fraction (old
        0.020208206802093842, new 0.02020820680210269); c*, mass flux and Mach each move by
        about 3.4e-14 relative. Every one of these is many orders below the ~1e-9 relative
        floor a genuine divergence would have to clear, and below the fixture tolerance
        table's own rows by nine to eleven orders: the claim "a re-solve's rounding", not a
        physical change, holds by this measurement, not only by the mechanism's own
        description.
      - **A structural finding beyond the audit's own scope**: the `throat` fixture
        family's own reference generation is not immune to F3 either — its ternary
        search can refine onto the pinned-pair side of a plateau edge, past Mach 1
        (`beo-h2o-throat_pc15MPa_h-11.06875MJkg`'s reference Mach is 1.011285688885813).
        `tests/Performance.Tests/StationComparison.IsPlateauEdgeDivergence` skips that
        one station's comparison, guarded on the reference's own recorded Mach; the
        Performance.Tests `BOOT.md` entry above has the red-once evidence.

      The evidence for F1, F5, O1 and O2 below is this coder's own, verified against
      the running code, not retyped from the audit report:
      - **F1**: `SecondAuditFixTests.TheThroatIsTheOraclesFirstMaximum` (every rocket
        and throat fixture), `…OverTheApHtpbAlBand` (h −4.42 to −4.32 MJ/kg at 1, 3, 7
        and 15 MPa) and `…OverTheElementMixtureCases` (the three new element-mixture
        throat fixtures). Red at `5a732f0` (`ThroatBracketSearch.Momentum` returned the
        grid's overall maximum, not the first met from the chamber); green after. A
        first probe of the band fact used the oracle's entire grid instead of only the
        region between the chamber and the throat (BOOT.md's own wording, above) and
        failed on a real second maximum at 3 MPa, h = −4.36 MJ/kg (throat 2211.42 m/s
        at p/p_c 0.6067, a downstream maximum of 2221.32 m/s at 0.5565): the fact was
        narrowed to that region, not the tolerance, and the case is not a regression —
        the accepted throat is the correct, first (upstream) one.
      - **F5**: `AFrozenAtThroatExitAtOrAboveTheThroatIsInvalidInput` and
        `EveryOkStationCarriesOnlyFiniteFigures`, both over every rocket and throat
        fixture. Red at `5a732f0` (the rule did not exist; a pressure ratio at or above
        the throat gave an `Ok` station with infinite area ratio and vacuum Isp).
      - **O1, O2**: `ABisectionAcceptedWithinTheSonicToleranceIsOk` exercises
        `ThroatBracketSearch.Bisect`'s own acceptance line directly, over brackets of
        very different widths and asymmetries around a real sonic throat
        (`rp1311-example13-throat_pc5MPa_dh0`); every one converges within the tight
        polish tolerance on this system (a fact the test also records), so the wider,
        report-tolerance branch stayed unreached by any swept bracket — a probe (not
        committed) found the same over eight bracket widths spanning six orders of
        magnitude. The branch is read directly rather than forced: the fact confirms
        every accepted state meets `RocketSolver.SonicTolerance`, the bound the
        acceptance line itself reads, not a narrower one a regression could still pass.
        `TryRatioNeverLeavesAStaleRatioOnFailure` is the direct fact for O2: a
        non-positive sound speed makes `TryRatio` report failure with `ratio` at 0,
        never left at the caller's previous value.

      Evidence, merged into `main` from `88c6a03`: `dotnet build APThermo.sln` 0 warnings,
      0 errors; `APTHERMO_NO_CUDA=1 dotnet test APThermo.sln --filter
      "Category!=LongRunning"` 5396 of 5396, none skipped; the protocol lint 0 errors,
      0 warnings; `dotnet test tests/Execution.Tests -c Release` on CUDA 144 of 144 at
      `417bff0`, the last commit of the branch that changed code under `src`; every
      fixture outside the seven new `throat` cases moved in its provenance keys only
      (a field-by-field comparison with `main`); the bits moved for
      `rp1311-example13` only, in this node's and the front door's snapshots, as
      measured above. Ticked by the orchestrator at the merge.

      ⚠ 2026-09-28, the same evening: the F3 bullet's claim "the edge throat is
      single-phase and subsonic" held only for the committed fixtures. Over the audit's
      own bands most cases ended `ThroatNotFound` (the third pass, the ⚠ under
      Constraints, the plateau edge's state); the 3 MPa fixture lies at h 2.2375, outside
      the band h 2.206 to 2.213 the bullet names. The criterion below reopens it.
- [x] 2026-09-28 — The third audit pass of 2026-09-28 (part 2: finding 1 and the
      observation on the boundary cap) is closed by the rules of that date under
      Constraints.
      - **The plateau edge's reach.** The geometric offsets of the plateau-edge rule
        (`ThroatBracketSearch.AcceptPlateauEdge`, `1e-10·4^k` up to an offset of `1e-4`)
        and the cap of 8 boundaries (`UpstreamChokeCheck.MaxPhaseBoundaries`) with
        `ThroatNotFound` on exhaustion, never an unverified `Ok`.
      - **The bands as facts.** `Performance.Tests/ThirdPassFixTests.cs`:
        `TheLi2OBandAt0Point3MPaNeverEndsThroatNotFound` over the element moles of
        `li2o-throat_pc0.3MPa_h3.29375MJkg`, h 3.05625 to 3.36875 MJ/kg in 12.5 kJ/kg
        steps (26 cases), and `TheLi2OBandAt3MPaNeverEndsThroatNotFound` over
        `li2o-throat_pc3MPa_h2.2375MJkg`, h 2.15 to 2.2015625 MJ/kg at the probe's own
        step (4.6875 kJ/kg, 12 cases): every case `Ok`, no `ThroatNotFound`, each
        plateau-edge throat single-phase and subsonic (`Mach < 1`, `GammaS > 1.05`), and
        each passing `SecondAuditFixTests.AssertFirstMaximum`. Red at `c02e14d` (21 of
        26, and 10 of 12, `ThroatNotFound`), green after. The audit's probe is
        `scratchpad/audit3/b/repo/tests/Performance.Tests/ZzProbeBand.cs` with its
        outputs `band-old.csv`, `band-new.csv`, `band-geo.csv` (kept out of the tree).

        ⚠ 2026-09-28: this bullet named the 3 MPa band's upper bound 2.2109 MJ/kg with
        12 cases. Read against the audit's own probe output (`band-new.csv`), 2.2109 MJ/kg
        (2210937.5 J/kg) is the *highest* pressure at which the pre-fix code still failed,
        not the edge of a contiguous 12-case sweep: two of the twelve points between 2.15
        and 2.2109 MJ/kg (2182812.5 and 2196875 J/kg) were already `Ok` even before the
        fix, so a contiguous sweep to 2.2109 MJ/kg holds 14 cases, 12 of them failing. The
        audit's own report (`scratchpad/audit3/2-throat-execution-guards.md`, "3 MPa
        h 2.15-2.2109: 12/12 Ok before, 10 ThroatNotFound after") states 12 total and 10
        failing, which the contiguous span 2.15 to 2.2015625 MJ/kg (12 points at the
        probe's own 4.6875 kJ/kg step) reproduces exactly; that narrower, contiguous span
        is what the fact sweeps.
      - **A fixture inside the 3 MPa band.** `li2o-throat_pc3MPa_h2.20625MJkg`, added to
        `tests/Fixtures/generate/throat_scan.py`'s new `THIRD_PASS_REACH_CASES` (h
        2.20625 MJ/kg, inside 2.206 to 2.213 MJ/kg) and generated through
        `regenerate.py`, never typed: `ThroatFixtureTests.TheThroatCaseReproducesTheReference`
        covers it by the directory listing, so the F3 fact covers the band it names.
        `regenerate.py --check` and `--check --sample` both exit 0; every other
        committed fixture moved in its provenance (`generatorSha256`, `generatedOn`,
        `scriptSha256`) only, verified field by field.
      - **No vacuous pass.** `SecondAuditFixTests.AssertFirstMaximum` fails on a non-`Ok`
        chamber or throat unless the caller's own `declaredStatus` names it (default
        `Ok`; no committed case is declared otherwise); `ThirdPassFixTests` asserts each
        band it sweeps and `RocketAndThroatFixtures` are non-empty. Shown red once: with
        the old, linear eight-step reach restored in `ThroatBracketSearch.AcceptPlateauEdge`,
        both band facts fail with "throat ended ThroatNotFound, not the declared Ok" on
        their first case, instead of silently returning; reverted.
      - **The cap.** `ThirdPassFixTests.AWalkThatMeetsMoreBoundariesThanTheCapEndsThroatNotFound`:
        a hand-built chain of narrow (inside `ThroatBracketWidth`) boundaries, each echoed
        back verbatim by `PhaseBoundaryLocator.Locate`'s own first check (no solve, the
        idiom observations O1/O2 use), every end subsonic and every fingerprint distinct
        from a sentinel candidate fingerprint, run one more time than the cap: none of
        `UpstreamChokeCheck.Verify`'s three resolving conditions ever fires, reproducing
        the walk `Verify` performs for a case with more real boundaries than its cap. No
        real system on hand needs more than a handful of boundaries to resolve (the
        richest measured, BeO/H2O at 15 MPa, six; Li2O at 3 MPa, seven, over its whole
        chamber-to-vacuum isentrope) — below both the old cap of 4 and the new cap of 8 —
        so the exhaustion branch itself is exercised directly against the walk's own
        mechanism, as O1/O2 already do for `ThroatBracketSearch`'s bisection, rather than
        against physics that does not reach it. With the cap temporarily lowered to 4 and
        the exhaustion return reverted to the unverified `RestoreCandidate`, the full fast
        suite of this node stayed green (1429/1429): no committed fixture's own boundary
        count reaches even the *old* cap, matching the same "no real system on hand"
        finding, and confirming the direct unit fact is the only guard against a
        regression here, not a fixture-level one.
      - **Bits.** No `Bits*.approved.txt` of this node moves: `BitSnapshotTests` covers
        only the `rocket` fixture kind, and the new fixture is of the `throat` kind
        (`git status --short` after regeneration touches only `tests/Fixtures/cases/`).

      Evidence: `dotnet build APThermo.sln`, 0 warnings, 0 errors; `APTHERMO_NO_CUDA=1
      dotnet test APThermo.sln --filter "Category!=LongRunning"`, every project green;
      `dotnet test tests/Performance.Tests`, 1429 of 1429, none skipped (up from 1418 at
      `0928618`, the new fixture's own facts and the four new `ThirdPassFixTests`); the
      protocol lint, 0 errors, 0 warnings; no `Bits*.approved.txt` or
      `PublicSurface.approved.txt` moved.

## Taboos

- No second equilibrium solver or mixture-property formula here: call `Equilibrium`.
- No performance figure computed in more than one place; no conversion to seconds
  (that is the command line's convenience).
- No `float`, no exceptions, no allocations: kernel code.
- No finite-area chamber approximations smuggled in under a flag: version 1 is
  infinite-area only, and a later version gets its own design session.

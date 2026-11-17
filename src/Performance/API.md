# API.md — Performance

Namespace `APThermo.Performance`. The node exposes one
kernel-compatible rocket solver for one case and the descriptors of its inputs and
outputs. Everything not listed here, in a package-surface section (one whose heading
carries no `(tree contract)` mark), is internal and may change without notice (root
`BOOT.md`, Delivery: Public surface). The tree-contract section below lists the
internal types `Execution` and `Problems` use (root `BOOT.md`, Delivery: Tree
contracts); the assembly grants `InternalsVisibleTo` to those nodes, to
`Execution.Tests` and `Benchmarks` (`APThermo.Performance.csproj`).

⚠ 2026-09-15 (distribution phase): the review of that day
(fixed in `24156be`) found no consumer scenario for `ExitSpecification`,
`RocketProblem`, `RocketLayout`, `RocketResult` or `RocketSolver`: every use is
`Execution` composing the kernel, or `Problems` building a batch, or this node's own
tests. They moved from the package surface into the tree contract below; only
`FlowModel` and `PerformanceFigures` stay public, because a consumer reads or sets
them on `RocketProblem`/`StateRecord` and `Station` of `Problems` (distinct,
same-named types there: `Problems.RocketResult`, not this node's).

## Flow and figures ✅

```csharp
namespace APThermo.Performance;

public enum FlowModel { ShiftingEquilibrium, FrozenAtChamber, FrozenAtThroat }

public struct PerformanceFigures : IEquatable<PerformanceFigures>   // one station; SI
{
    public double AreaRatio { get; set; }                // A/A_t; 1 at the throat, 0 at the chamber (undefined there)
    public double PressureRatio { get; set; }            // p_c/p; 1 at the chamber
    public double CharacteristicVelocity { get; set; }   // c* = p_c/(ρ_t u_t), m/s, the same at every station
    public double ThrustCoefficient { get; set; }        // C_F = u/c*; 0 at the chamber
    public double SpecificImpulse { get; set; }          // Isp = u, m/s (p_ambient = p); 0 at the chamber
    public double VacuumSpecificImpulse { get; set; }    // Ivac = u + p/(ρ u), m/s; 0 at the chamber

    public readonly bool Equals(PerformanceFigures other);     // every property equal by double.Equals (NaN equals NaN)
    public override readonly bool Equals(object? obj);
    public override readonly int GetHashCode();
    public static bool operator ==(PerformanceFigures left, PerformanceFigures right);
    public static bool operator !=(PerformanceFigures left, PerformanceFigures right);
}
```

⚠ 2026-09-24: until 0.1.0 the members were public fields without equality; the
root's Diagnostics constraint (CA1051, CA1815) made them auto-properties with value
equality. The consequences are the same as for `MixtureState` (`src/Thermo/API.md`):
source-compatible for readers, a recompile for binaries built against 0.1.0, and
unchanged layout and bits.

## Rocket solver (tree contract) ✅

```csharp
internal enum ExitSpecification { AreaRatio, PressureRatio }

internal readonly struct RocketProblem                     // one case
{
    public readonly double ChamberPressure;              // Pa
    public readonly double ReactantEnthalpy;             // J/kg of propellant
    public readonly double TemperatureEstimate;          // K for the chamber solve, 0 = the equilibrium node's default
    public readonly FlowModel Flow;
    public readonly ArrayView<double> ElementMoles;      // [element], kmol per kg
    public readonly ArrayView<double> ExitValues;        // [exit], area ratio or p_c/p_e, in station order; may be empty
    public readonly ArrayView<int> ExitKinds;            // [exit], ExitSpecification
    public RocketProblem(double chamberPressure, double reactantEnthalpy, double temperatureEstimate, FlowModel flow,
                         ArrayView<double> elementMoles, ArrayView<double> exitValues, ArrayView<int> exitKinds);
}

internal static class RocketLayout
{
    public const int FixedStations = 2;                  // chamber and throat
    public static int StationCount(int exitCount);       // FixedStations + exitCount
}

internal readonly struct RocketResult                      // views the solver writes into
{
    public readonly ArrayView<MixtureState> Stations;    // [stations]: chamber, throat, exits in order
    public readonly ArrayView<double> Moles;             // [stations * species], kmol per kg
    public readonly ArrayView<double> Multipliers;       // [stations * elements], equilibrium diagnostics
    public readonly ArrayView<PerformanceFigures> Figures; // [stations]
    public readonly ArrayView<int> StationStatus;        // [stations], CaseStatus per station
    public readonly ArrayView<int> Iterations;           // [stations], equilibrium iterations of the last solve at the station
    public readonly ArrayView<int> Status;               // [1], CaseStatus of the case
    public RocketResult(ArrayView<MixtureState> stations, ArrayView<double> moles, ArrayView<double> multipliers,
                        ArrayView<PerformanceFigures> figures, ArrayView<int> stationStatus, ArrayView<int> iterations,
                        ArrayView<int> status);
}

internal static class RocketSolver                         // kernel-compatible
{
    public const double SonicTolerance = 4.0e-5;         // equation (6.16), on |u² − a²|/u²
    public const double AreaRatioTolerance = 4.0e-5;     // equation (6.25), on the last correction of ln(p_c/p_e)
    internal const double AreaRatioResidualTolerance = 1.0e-6; // final relative area residual before Ok
    internal const double ThroatDecisionTolerance = 1.0e-8; // the throat decides on |u²/a² − 1| within this (2026-10-02, owner)
    internal const int ThroatTailSteps = 2;              // momentum steps after the decision, the last is the throat (2026-10-02, owner)
    public const int MaxThroatIterations = 20;
    public const int MaxAreaRatioIterations = 20;
    public const int MaxThroatBisections = 60;           // halvings of the throat bracket in ln p (2026-09-26, BOOT.md)
    public static void Solve(in SpeciesTableView table, in RocketProblem problem,
                             in EquilibriumScratch scratch, in RocketResult result);
    internal static void FinalizeFailures(in RocketResult result); // failed state/figures zero, NoGasPhase T,p retained
}
```

`Stations[0]` is the chamber (velocity 0, Mach 0), `Stations[1]` the throat: the first
maximum of the mass flux met from the chamber (`BOOT.md`'s invariant, 2026-09-28), Mach
1 within the sonic tolerance away from a melting plateau's edge, and below 1 at such an
edge (`BOOT.md`, finding F1). `Stations[2 + k]` is the k-th exit. Every station's
`MixtureState` is the equilibrium node's, with `Velocity` and `Mach` filled in; the
figures follow RP-1311 section 6.2. The throat and the area-ratio iterations continue
past the report's tolerances so that a reported station is at rounding level: the
throat by a decision at `1e-8` and two more momentum steps (`BOOT.md`, 2026-10-02), the
area-ratio iterations to `1e-10`, when they can; a case whose last correction lies above
its tight tolerance but within the report's tolerance is `Ok` only when the final
area residual also meets `1e-6` relative. In frozen flow the stations downstream of
the freezing
station carry its composition bit for bit and the frozen state of the equilibrium
node. In `FrozenAtChamber` flow the chamber's `GammaS`, `SoundSpeed`, `DlnVdlnT` and
`DlnVdlnP` are the frozen ones (`Cp/Cv`, 1, −1), as the reference reports them; its
heat capacities stay the equilibrium ones. The estimate for an exit station is the
last station that converged. A case may have no exit at all: chamber and throat alone.

⚠ 2026-09-12: the sketch had `PerformanceFigures` per exit station only and no
`Iterations`; the reference reports the figures at every station (`c*` at the chamber,
the throat's `C_F`, `Isp` and `Ivac`), so the figures are per station, and the
equilibrium iteration counts are kept for diagnostics. The sketch's `InvalidInput` for
"no exits" is dropped: a case with chamber and throat only is valid.

⚠ 2026-09-26: the design commit `9a6888f` first placed `MaxThroatBisections` inside
this ✅ block, before any code had it, in a separate `## Throat bracket (tree
contract) ⏳` section meant to hold it until the code existed.
`DeclarationTests.EveryDeclarationUnderATickExists` went red on `main`. The Execution
coder of the same day found it and reported it (AGENTS.md §7: a ✅ may be placed only
after what is declared exists); the constant was moved to a ⏳ section until the code
was written. The throat's largest-mass-flux rule (`BOOT.md`, 2026-09-26) now carries
the constant, and it moved back into the block above, with this note kept as history.

⚠ 2026-11-14: the final-area residual constant and failure-finalization entry were
implemented during audit closure but omitted from this tree-contract declaration.
Their real signatures are now recorded. Internal payload writes are owned by
StationFigures; the RocketSolver finalization entry retains its existing signature.

## Errors

The solver never throws. The case status is `Ok` only when every station is `Ok`;
otherwise it is the first failure found, and the stations after a failed exit are
still computed from the last converged station. Statuses: `InvalidInput` (non-positive
chamber pressure, empty table, a flow model other than the three named values; a
pressure ratio not above 1 for that station; in `FrozenAtThroat` flow, a pressure-ratio
exit whose pressure is at or above the throat's, 2026-09-28, finding F5), `AreaRatioInvalid`
(an area ratio not above 1, that station; the reference's rule, 2026-09-26), `ThroatNotFound`
(neither the sonic condition within `MaxThroatIterations` nor the bracket's bisection found
the throat; `BOOT.md`, the first maximum of the mass flux met from the chamber), `NotConverged`
(an area ratio not met within `MaxAreaRatioIterations`, nonfinite station figures, or an
accepted station's entropy off the chamber's by more than the relative tolerance `1e-9`,
`BOOT.md`'s entropy check), and the
statuses of `Equilibrium` propagated from a station's solve. A chamber or throat failure ends
the case; the exits keep `InvalidInput`.

At the high-pressure edge of a melting plateau the throat is the edge, and its `Mach`
figure is below 1 (`BOOT.md`'s invariant, 2026-09-28, "the throat is the first maximum
of the mass flux met from the chamber"; found first as "the throat carries the largest
mass flux", 2026-09-26). An exit at an area ratio of exactly 1 used to be accepted; it
is now `AreaRatioInvalid`. The throat is always station 1. An area-ratio pass that lands
on the subsonic side of the sonic point is never accepted for that station: only a last
pass that is supersonic is (2026-09-26, `BOOT.md`, finding F3).

In `FrozenAtThroat` flow, a pressure-ratio exit whose implied pressure (`chamber.Pressure
/ value`) is not below the throat's pressure is `InvalidInput` (2026-09-28, finding F5):
the flow upstream of the freezing point is still in equilibrium, and the reference itself
omits such a point ("FOR FROZEN PERFORMANCE, POINT OMITTED BECAUSE ASSIGNED pi/p IS LESS
THAN VALUE AT nfz"). Freezing the throat's composition there put the station at an
enthalpy above the chamber's; this status replaces the `Ok` station with an infinite
area ratio and vacuum specific impulse that resulted before the fix.

A failed station has zero State and performance figures, except NoGasPhase retains
its temperature and pressure only. Earlier successful stations and the permitted
diagnostic composition remain available.

⚠ 2026-11-17: a pressure-ratio exit with a rounded zero velocity could report `Ok`
with infinite area and vacuum impulse. Nonfinite figures now refuse that station as
`NotConverged`; this does not reject other finite pressure-ratio exits.

⚠ 2026-11-14: failed frozen/area iterations could expose a partial State even with
a correct failure status (A36). Finalization now enforces the outward payload rule.
The correction preserves NoGasPhase's T,p exception and equilibrium's internal
State-not-written contract.

## Condensed-model applicability

Ok certifies the numerical conditions of the selected model. It does not establish
that every physically possible condensed phase is admitted at that temperature.
The candidate-domain and effective/open-below rules belong to
[Equilibrium](../Equilibrium/API.md); they also apply to the default product list.

Frozen flow holds the freezing station's condensed composition. This version does
not implement CEA's stop 50 K below a condensed fit's range: the fixed condensed
fit can be extrapolated below its tabulated interval while the gas-based temperature
guard remains satisfied. An Ok station can therefore contain a condensate outside
its raw fit domain. Check the records' ranges when interpreting such results.

Both equilibrium and frozen transport properties may be reported for a frozen-flow
station; the appropriate transport model depends on the intended perturbation.

⚠ 2026-11-14: the earlier out-of-scope sentence named the missing CEA stop without
explaining its effect on an Ok output (A37). The policy is unchanged; its numerical
and physical applicability limits are now explicit.

## Side effects

None.

## Out of scope

- Transport properties at the stations: `Transport`, from temperature and `Moles`.
- Reactant enthalpy and element moles from a propellant definition: `Problems`.
- Finite-area chamber, area-assigned subsonic exits, the report's stop 50 K below a condensed
  species' range in frozen flow: not in version 1. Pressure-assigned finite exits may be subsonic.

⚠ 2026-11-17: the previous wording excluded all “subsonic exits”; the implementation and
`BOOT.md` distinguish area-assigned exits from pressure-assigned finite exits.

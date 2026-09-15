# API.md — Performance

Namespace `APThermo.Performance`. The node exposes one
kernel-compatible rocket solver for one case and the descriptors of its inputs and
outputs. Everything not listed here is internal and may change.

## Rocket solver ✅

```csharp
namespace APThermo.Performance;

public enum FlowModel { ShiftingEquilibrium, FrozenAtChamber, FrozenAtThroat }
public enum ExitSpecification { AreaRatio, PressureRatio }

public readonly struct RocketProblem                     // one case
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

public struct PerformanceFigures                         // one station; SI
{
    public double AreaRatio;                             // A/A_t; 1 at the throat, 0 at the chamber (undefined there)
    public double PressureRatio;                         // p_c/p; 1 at the chamber
    public double CharacteristicVelocity;                // c* = p_c/(ρ_t u_t), m/s, the same at every station
    public double ThrustCoefficient;                     // C_F = u/c*; 0 at the chamber
    public double SpecificImpulse;                       // Isp = u, m/s (p_ambient = p); 0 at the chamber
    public double VacuumSpecificImpulse;                 // Ivac = u + p/(ρ u), m/s; 0 at the chamber
}

public static class RocketLayout
{
    public const int FixedStations = 2;                  // chamber and throat
    public static int StationCount(int exitCount);       // FixedStations + exitCount
}

public readonly struct RocketResult                      // views the solver writes into
{
    public readonly ArrayView<MixtureState> Stations;    // [stations]: chamber, throat, exits in order
    public readonly ArrayView<double> Moles;             // [stations * species], kmol per kg
    public readonly ArrayView<double> Multipliers;       // [stations * elements], for Transport
    public readonly ArrayView<PerformanceFigures> Figures; // [stations]
    public readonly ArrayView<int> StationStatus;        // [stations], CaseStatus per station
    public readonly ArrayView<int> Iterations;           // [stations], equilibrium iterations of the last solve at the station
    public readonly ArrayView<int> Status;               // [1], CaseStatus of the case
    public RocketResult(ArrayView<MixtureState> stations, ArrayView<double> moles, ArrayView<double> multipliers,
                        ArrayView<PerformanceFigures> figures, ArrayView<int> stationStatus, ArrayView<int> iterations,
                        ArrayView<int> status);
}

public static class RocketSolver                         // kernel-compatible
{
    public const double SonicTolerance = 4.0e-5;         // equation (6.16), on |u² − a²|/u²
    public const double AreaRatioTolerance = 4.0e-5;     // equation (6.25), on the last correction of ln(p_c/p_e)
    public const int MaxThroatIterations = 20;
    public const int MaxAreaRatioIterations = 20;
    public static void Solve(in SpeciesTableView table, in RocketProblem problem,
                             in EquilibriumScratch scratch, in RocketResult result);
}
```

`Stations[0]` is the chamber (velocity 0, Mach 0), `Stations[1]` the throat (Mach 1
within the sonic tolerance), `Stations[2 + k]` the k-th exit. Every station's
`MixtureState` is the equilibrium node's, with `Velocity` and `Mach` filled in; the
figures follow RP-1311 section 6.2. The throat and the area-ratio iterations continue
past the report's tolerances to `1e-10` when they can, so that a reported station is at
rounding level; a case whose last correction lies between `1e-10` and the report's
tolerance is still `Ok`. In frozen flow the stations downstream of the freezing
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

## Errors

The solver never throws. The case status is `Ok` only when every station is `Ok`;
otherwise it is the first failure found, and the stations after a failed exit are
still computed from the last converged station. Statuses: `InvalidInput` (non-positive
chamber pressure, empty table; a pressure ratio not above 1 for that station),
`AreaRatioInvalid` (an area ratio below 1, that station), `ThroatNotFound` (the sonic
condition not met within `MaxThroatIterations`), `NotConverged` (an area ratio not met
within `MaxAreaRatioIterations`), and the statuses of `Equilibrium` propagated from a
station's solve. A chamber or throat failure ends the case; the exits keep
`InvalidInput`.

## Side effects

None.

## Out of scope

- Transport properties at the stations: `Transport`, from `Moles` and `Multipliers`.
- Reactant enthalpy and element moles from a propellant definition: `Problems`.
- Finite-area chamber, subsonic exits, the report's stop 50 K below a condensed
  species' range in frozen flow: not in version 1.

# API.md — Performance

Namespace `AerospacePropellantThermodynamics.Performance`. The node exposes one
kernel-compatible rocket solver for one case and the descriptors of its inputs and
outputs. Everything not listed here is internal and may change.

## Rocket solver ⏳

```csharp
namespace AerospacePropellantThermodynamics.Performance;

public enum FlowModel { ShiftingEquilibrium, FrozenAtChamber, FrozenAtThroat }
public enum ExitSpecification { AreaRatio, PressureRatio }

public readonly struct RocketProblem                     // one case
{
    public readonly double ChamberPressure;              // Pa
    public readonly double ReactantEnthalpy;             // J/kg of propellant
    public readonly double TemperatureEstimate;          // K, 0 = default
    public readonly FlowModel Flow;
    public readonly ArrayView<double> ElementMoles;      // [element], kmol per kg
    public readonly ArrayView<double> ExitValues;        // [exit], area ratio or p_c/p_e
    public readonly ArrayView<int> ExitKinds;            // [exit], ExitSpecification
}

public struct PerformanceFigures                         // one exit station; SI
{
    public double AreaRatio;                             // A_e/A_t
    public double PressureRatio;                         // p_c/p_e
    public double CharacteristicVelocity;                // c*, m/s
    public double ThrustCoefficient;                     // C_F
    public double SpecificImpulse;                       // Isp, m/s (p_ambient = p_e)
    public double VacuumSpecificImpulse;                 // Ivac, m/s
}

public readonly struct RocketResult                      // views the solver writes into
{
    public readonly ArrayView<MixtureState> Stations;    // [2 + exits]: chamber, throat, exits in order
    public readonly ArrayView<double> Moles;             // [(2 + exits) * species], kmol per kg
    public readonly ArrayView<double> Multipliers;       // [(2 + exits) * elements], for Transport
    public readonly ArrayView<PerformanceFigures> Figures; // [exits]
    public readonly ArrayView<int> StationStatus;        // [2 + exits], CaseStatus per station
    public readonly ArrayView<int> Status;               // [1], CaseStatus of the case
}

public static class RocketSolver                         // kernel-compatible
{
    public static void Solve(in SpeciesTableView table, in RocketProblem problem,
                             in EquilibriumScratch scratch, in RocketResult result);
}
```

`Stations[0]` is the chamber (velocity 0, Mach 0), `Stations[1]` the throat (Mach 1
within the sonic tolerance), `Stations[2 + k]` the k-th exit. The case status is `Ok`
only when every station is `Ok`; a failed exit station leaves the others valid and
sets the case status to the first failure found.

## Errors

The solver never throws. Statuses: `InvalidInput` (non-positive chamber pressure,
area ratio below 1, pressure ratio not above 1, no exits), `NotConverged`
(propagated from an equilibrium solve), `ThroatNotFound`, `AreaRatioInvalid`, and
the statuses of `Equilibrium`.

## Side effects

None.

## Out of scope

- Transport properties at the stations: `Transport`, from `Moles` and `Multipliers`.
- Reactant enthalpy and element moles from a propellant definition: `Problems`.
- Finite-area chamber, subsonic exits: not in version 1.

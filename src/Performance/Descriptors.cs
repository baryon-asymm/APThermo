using AerospacePropellantThermodynamics.Thermo;
using ILGPU;

namespace AerospacePropellantThermodynamics.Performance;

/// <summary>Where the composition stops following the equilibrium.</summary>
public enum FlowModel
{
    ShiftingEquilibrium,
    FrozenAtChamber,
    FrozenAtThroat,
}

/// <summary>How an exit station is assigned.</summary>
public enum ExitSpecification
{
    AreaRatio,       // A_e/A_t ≥ 1, supersonic branch
    PressureRatio,   // p_c/p_e > 1
}

/// <summary>One rocket case: the propellant as element moles and enthalpy per kilogram, the chamber pressure, the flow model and the exit stations.</summary>
public readonly struct RocketProblem
{
    /// <summary>Pa.</summary>
    public readonly double ChamberPressure;

    /// <summary>J per kg of propellant.</summary>
    public readonly double ReactantEnthalpy;

    /// <summary>K for the chamber solve; 0 = the equilibrium node's default.</summary>
    public readonly double TemperatureEstimate;

    public readonly FlowModel Flow;

    /// <summary>[element], kmol per kg.</summary>
    public readonly ArrayView<double> ElementMoles;

    /// <summary>[exit], the area ratio or the pressure ratio p_c/p_e of each exit station, in the order of the stations.</summary>
    public readonly ArrayView<double> ExitValues;

    /// <summary>[exit], <see cref="ExitSpecification"/> of each exit station.</summary>
    public readonly ArrayView<int> ExitKinds;

    public RocketProblem(double chamberPressure, double reactantEnthalpy, double temperatureEstimate, FlowModel flow,
                         ArrayView<double> elementMoles, ArrayView<double> exitValues, ArrayView<int> exitKinds)
    {
        ChamberPressure = chamberPressure;
        ReactantEnthalpy = reactantEnthalpy;
        TemperatureEstimate = temperatureEstimate;
        Flow = flow;
        ElementMoles = elementMoles;
        ExitValues = exitValues;
        ExitKinds = exitKinds;
    }
}

/// <summary>The performance figures of one station; SI. At the chamber only the characteristic velocity and the pressure ratio (1) are defined.</summary>
public struct PerformanceFigures
{
    public double AreaRatio;              // A/A_t; 1 at the throat, 0 at the chamber (undefined)
    public double PressureRatio;          // p_c/p
    public double CharacteristicVelocity; // c* = p_c/(ρ_t u_t), m/s; the same at every station
    public double ThrustCoefficient;      // C_F = u/c*
    public double SpecificImpulse;        // Isp = u, m/s (ambient pressure equal to the station pressure)
    public double VacuumSpecificImpulse;  // Ivac = u + p/(ρ u), m/s
}

/// <summary>Sizes of a case's station arrays.</summary>
public static class RocketLayout
{
    /// <summary>Chamber and throat.</summary>
    public const int FixedStations = 2;

    public static int StationCount(int exitCount) => FixedStations + exitCount;
}

/// <summary>The views the rocket solver writes into; station 0 is the chamber, 1 the throat, 2 + k the k-th exit.</summary>
public readonly struct RocketResult
{
    public readonly ArrayView<MixtureState> Stations;      // [stations]
    public readonly ArrayView<double> Moles;               // [stations * species], kmol per kg
    public readonly ArrayView<double> Multipliers;         // [stations * elements]
    public readonly ArrayView<PerformanceFigures> Figures; // [stations]
    public readonly ArrayView<int> StationStatus;          // [stations], CaseStatus per station
    public readonly ArrayView<int> Iterations;             // [stations], equilibrium iterations of the last solve at the station
    public readonly ArrayView<int> Status;                 // [1], CaseStatus of the case

    public RocketResult(ArrayView<MixtureState> stations, ArrayView<double> moles, ArrayView<double> multipliers,
                        ArrayView<PerformanceFigures> figures, ArrayView<int> stationStatus, ArrayView<int> iterations, ArrayView<int> status)
    {
        Stations = stations;
        Moles = moles;
        Multipliers = multipliers;
        Figures = figures;
        StationStatus = stationStatus;
        Iterations = iterations;
        Status = status;
    }
}

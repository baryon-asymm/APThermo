using APThermo.Thermo;
using ILGPU;

namespace APThermo.Performance;

/// <summary>Where the composition stops following the equilibrium.</summary>
public enum FlowModel
{
    /// <summary>The composition stays in equilibrium all the way through the nozzle.</summary>
    ShiftingEquilibrium,

    /// <summary>The composition is frozen at the chamber composition from the throat onward.</summary>
    FrozenAtChamber,

    /// <summary>The composition follows equilibrium up to the throat, then freezes there.</summary>
    FrozenAtThroat,
}

/// <summary>How an exit station is assigned.</summary>
internal enum ExitSpecification
{
    AreaRatio,       // A_e/A_t ≥ 1, supersonic branch
    PressureRatio,   // p_c/p_e > 1
}

/// <summary>One rocket case: the propellant as element moles and enthalpy per kilogram, the chamber pressure, the flow model and the exit stations.</summary>
internal readonly struct RocketProblem(double chamberPressure, double reactantEnthalpy, double temperatureEstimate, FlowModel flow,
                                       ArrayView<double> elementMoles, ArrayView<double> exitValues, ArrayView<int> exitKinds)
{
    /// <summary>Pa.</summary>
    public readonly double ChamberPressure = chamberPressure;

    /// <summary>J per kg of propellant.</summary>
    public readonly double ReactantEnthalpy = reactantEnthalpy;

    /// <summary>K for the chamber solve; 0 = the equilibrium node's default.</summary>
    public readonly double TemperatureEstimate = temperatureEstimate;

    public readonly FlowModel Flow = flow;

    /// <summary>[element], kmol per kg.</summary>
    public readonly ArrayView<double> ElementMoles = elementMoles;

    /// <summary>[exit], the area ratio or the pressure ratio p_c/p_e of each exit station, in the order of the stations.</summary>
    public readonly ArrayView<double> ExitValues = exitValues;

    /// <summary>[exit], <see cref="ExitSpecification"/> of each exit station.</summary>
    public readonly ArrayView<int> ExitKinds = exitKinds;
}

/// <summary>The performance figures of one station; SI. At the chamber only the characteristic velocity and the pressure ratio (1) are defined.</summary>
public struct PerformanceFigures : IEquatable<PerformanceFigures>
{
    /// <summary>The area ratio A/A_t, dimensionless; 1 at the throat, 0 (undefined) at the chamber.</summary>
    public double AreaRatio { get; set; }

    /// <summary>The pressure ratio p_c/p, dimensionless.</summary>
    public double PressureRatio { get; set; }

    /// <summary>The characteristic velocity c* = p_c/(ρ_t u_t), in m/s; the same value at every station of a case.</summary>
    public double CharacteristicVelocity { get; set; }

    /// <summary>The thrust coefficient C_F = u/c*, dimensionless.</summary>
    public double ThrustCoefficient { get; set; }

    /// <summary>The specific impulse Isp = u, in m/s (the effective exhaust velocity, ambient pressure equal to
    /// the station pressure).</summary>
    public double SpecificImpulse { get; set; }

    /// <summary>The vacuum specific impulse Ivac = u + p/(ρ u), in m/s.</summary>
    public double VacuumSpecificImpulse { get; set; }

    /// <summary>Every property equal to <paramref name="other"/>'s by <see cref="double.Equals(double)"/>, so NaN equals NaN.</summary>
    public readonly bool Equals(PerformanceFigures other) =>
        AreaRatio.Equals(other.AreaRatio) &&
        PressureRatio.Equals(other.PressureRatio) &&
        CharacteristicVelocity.Equals(other.CharacteristicVelocity) &&
        ThrustCoefficient.Equals(other.ThrustCoefficient) &&
        SpecificImpulse.Equals(other.SpecificImpulse) &&
        VacuumSpecificImpulse.Equals(other.VacuumSpecificImpulse);

    /// <inheritdoc/>
    public override readonly bool Equals(object? obj) => obj is PerformanceFigures other && Equals(other);

    /// <inheritdoc/>
    public override readonly int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(AreaRatio);
        hash.Add(PressureRatio);
        hash.Add(CharacteristicVelocity);
        hash.Add(ThrustCoefficient);
        hash.Add(SpecificImpulse);
        hash.Add(VacuumSpecificImpulse);
        return hash.ToHashCode();
    }

    /// <summary>Value equality, field by field.</summary>
    public static bool operator ==(PerformanceFigures left, PerformanceFigures right) => left.Equals(right);

    /// <summary>Value inequality, field by field.</summary>
    public static bool operator !=(PerformanceFigures left, PerformanceFigures right) => !left.Equals(right);
}

/// <summary>Sizes of a case's station arrays.</summary>
internal static class RocketLayout
{
    /// <summary>Chamber and throat.</summary>
    public const int FixedStations = 2;

    public static int StationCount(int exitCount) => FixedStations + exitCount;
}

/// <summary>The views the rocket solver writes into; station 0 is the chamber, 1 the throat, 2 + k the k-th exit.</summary>
internal readonly struct RocketResult(ArrayView<MixtureState> stations, ArrayView<double> moles, ArrayView<double> multipliers,
                                      ArrayView<PerformanceFigures> figures, ArrayView<int> stationStatus, ArrayView<int> iterations, ArrayView<int> status)
{
    public readonly ArrayView<MixtureState> Stations = stations;           // [stations]
    public readonly ArrayView<double> Moles = moles;                      // [stations * species], kmol per kg
    public readonly ArrayView<double> Multipliers = multipliers;          // [stations * elements]
    public readonly ArrayView<PerformanceFigures> Figures = figures;      // [stations]
    public readonly ArrayView<int> StationStatus = stationStatus;         // [stations], CaseStatus per station
    public readonly ArrayView<int> Iterations = iterations;               // [stations], equilibrium iterations of the last solve at the station
    public readonly ArrayView<int> Status = status;                       // [1], CaseStatus of the case
}

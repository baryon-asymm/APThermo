using APThermo.Equilibrium;
using APThermo.Performance;

namespace APThermo.Problems;

/// <summary>A rocket problem: chamber pressure, flow model and the exit stations, pressure-ratio exits before area-ratio exits.</summary>
public sealed record RocketProblem
{
    /// <summary>Pa.</summary>
    public double ChamberPressure { get; init; }

    public FlowModel Flow { get; init; } = FlowModel.ShiftingEquilibrium;

    /// <summary>p_c / p_e of the exits reported first.</summary>
    public IReadOnlyList<double> PressureRatios { get; init; } = [];

    /// <summary>A / A_t of the supersonic exits reported after the pressure-ratio ones.</summary>
    public IReadOnlyList<double> AreaRatios { get; init; } = [];

    /// <summary>K for the chamber solve; 0 = the equilibrium node's default.</summary>
    public double TemperatureEstimate { get; init; }

    /// <summary>Evaluate viscosity, conductivity and Prandtl numbers at every station.</summary>
    public bool Transport { get; init; }

    public int ExitCount => PressureRatios.Count + AreaRatios.Count;
}

/// <summary>An equilibrium problem at an assigned pressure with the temperature, the enthalpy or the entropy assigned.</summary>
public sealed record EquilibriumProblem
{
    public ProblemKind Kind { get; init; } = ProblemKind.AssignedEnthalpyPressure;

    /// <summary>Pa.</summary>
    public double Pressure { get; init; }

    /// <summary>K: the assigned temperature, or the estimate for the other kinds (0 = the default).</summary>
    public double Temperature { get; init; }

    /// <summary>J/kg for the assigned-enthalpy kind; null = the mixture's enthalpy.</summary>
    public double? Enthalpy { get; init; }

    /// <summary>J/(kg·K) for the assigned-entropy kind.</summary>
    public double Entropy { get; init; }

    public bool Transport { get; init; }
}

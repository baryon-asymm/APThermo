using AerospacePropellantThermodynamics.Equilibrium;
using AerospacePropellantThermodynamics.Thermo;

namespace AerospacePropellantThermodynamics.Performance;

/// <summary>
/// The four views one rocket case is solved over, built once in <see cref="RocketSolver.Solve"/> and passed to every stage:
/// the species table, the case, its scratch and the result it writes into. Blittable, as the kernel requires.
/// </summary>
internal readonly struct RocketContext
{
    public readonly SpeciesTableView Table;
    public readonly RocketProblem Problem;
    public readonly EquilibriumScratch Scratch;
    public readonly RocketResult Result;

    public RocketContext(in SpeciesTableView table, in RocketProblem problem, in EquilibriumScratch scratch, in RocketResult result)
    {
        Table = table;
        Problem = problem;
        Scratch = scratch;
        Result = result;
    }
}

/// <summary>What the chamber fixes for every station downstream of it: its pressure, enthalpy, entropy and isentropic exponent.</summary>
internal readonly struct ChamberReference
{
    public readonly double Pressure;
    public readonly double Enthalpy;
    public readonly double Entropy;
    public readonly double GammaS;

    public ChamberReference(double pressure, double enthalpy, double entropy, double gammaS)
    {
        Pressure = pressure;
        Enthalpy = enthalpy;
        Entropy = entropy;
        GammaS = gammaS;
    }
}

/// <summary>What the throat fixes for the exit stations: its pressure and isentropic exponent, the mass flux, the characteristic velocity and ln(p_c/p_t).</summary>
internal readonly struct ThroatReference
{
    public readonly double Pressure;
    public readonly double MassFlux;
    public readonly double CharacteristicVelocity;
    public readonly double LogPressureRatio;
    public readonly double GammaS;

    public ThroatReference(double pressure, double massFlux, double characteristicVelocity, double logPressureRatio, double gammaS)
    {
        Pressure = pressure;
        MassFlux = massFlux;
        CharacteristicVelocity = characteristicVelocity;
        LogPressureRatio = logPressureRatio;
        GammaS = gammaS;
    }
}

/// <summary>What the estimate of the next exit station is made of: the temperature of the last station that converged and the extrapolation state of (6.23).</summary>
internal struct ExitEstimate
{
    /// <summary>K, the temperature the next station's solve starts from.</summary>
    public double Temperature;

    /// <summary>Whether the previous station may be extrapolated from: it converged and its area ratio exceeds <see cref="RocketSolver.ExtrapolationAreaRatio"/>.</summary>
    public bool Extrapolable;

    /// <summary>ln(p_c/p_e) of the last exit accepted by area ratio; read only while <see cref="Extrapolable"/>.</summary>
    public double LogPressureRatio;

    /// <summary>ln(A/A_t) of that exit, meaningful only while <see cref="Extrapolable"/>.</summary>
    public double LogAreaRatio;

    /// <summary>∂ln(A/A_t)/∂ln(p_c/p_e) at constant entropy at that exit, equation (6.23), meaningful only while <see cref="Extrapolable"/>.</summary>
    public double Derivative;
}

/// <summary>How the area-ratio iteration of one exit station ended.</summary>
internal enum ExitOutcome
{
    /// <summary>The correction of (6.25) fell below the tight tolerance.</summary>
    Converged,

    /// <summary>The iterations ran out; the last correction is within the report's tolerance, and the station is accepted.</summary>
    WithinReportTolerance,

    /// <summary>The iterations ran out and the last correction is above the report's tolerance.</summary>
    NotMet,

    /// <summary>Every pass fell on the subsonic side of the sonic point, so no correction was ever computed.</summary>
    NeverSupersonic,

    /// <summary>A station solve did not return Ok; the station carries the equilibrium node's status.</summary>
    SolveFailed,
}

/// <summary>Which solver a station is solved with: the composition follows the equilibrium, or it stays the one already in the station's row.</summary>
internal enum StationFlow
{
    Shifting,
    Frozen,
}

/// <summary>One station's solve as it is asked for: where it is written, at which pressure, from which temperature estimate, at which entropy, in which flow.</summary>
internal readonly struct StationRequest
{
    public readonly int Station;
    public readonly double Pressure;
    public readonly double TemperatureEstimate;
    public readonly double Entropy;
    public readonly StationFlow Flow;

    public StationRequest(int station, double pressure, double temperatureEstimate, double entropy, StationFlow flow)
    {
        Station = station;
        Pressure = pressure;
        TemperatureEstimate = temperatureEstimate;
        Entropy = entropy;
        Flow = flow;
    }
}

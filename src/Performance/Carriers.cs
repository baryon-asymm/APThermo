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

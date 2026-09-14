using AerospacePropellantThermodynamics.Performance;
using AerospacePropellantThermodynamics.Thermo;
using AerospacePropellantThermodynamics.Transport;

namespace AerospacePropellantThermodynamics.Problems;

/// <summary>
/// One station's slice of the engine's flat batch result: the table it was solved over, the flat offset into the moles array
/// computed once, and the fields <see cref="StationFactory"/> assembles into a <see cref="Station"/>. Built with init
/// properties, not a positional constructor, so that assembling a station takes a name and one slice instead of nine parameters.
/// </summary>
internal readonly record struct StationSlice
{
    public required SpeciesTable Table { get; init; }

    public required MixtureState State { get; init; }

    /// <summary>Rocket stations only; null for an equilibrium state.</summary>
    public PerformanceFigures? Performance { get; init; }

    /// <summary>The batch's flat moles array, [case * stationCount + station] * SpeciesCount + species, shared across stations.</summary>
    public required double[] Moles { get; init; }

    /// <summary>The flat offset of this station's first species within <see cref="Moles"/>.</summary>
    public required long Offset { get; init; }

    /// <summary>Null when transport was not requested or its status was not Ok.</summary>
    public TransportFigures? Transport { get; init; }

    /// <summary>Null when transport was not requested.</summary>
    public CaseStatus? TransportStatus { get; init; }

    public required CaseStatus Status { get; init; }
}

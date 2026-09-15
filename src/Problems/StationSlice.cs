using APThermo.Performance;
using APThermo.Thermo;
using APThermo.Transport;

namespace APThermo.Problems;

/// <summary>
/// One station as the engine's flat batch result reports it: the table it was solved over, the batch's moles with this
/// station's offset computed once, and the state, figures and statuses the result carries. Both runners build one and
/// <see cref="StationFactory.Create"/> turns it into the published <see cref="Station"/>, so that record is constructed in
/// one place with named arguments (BOOT.md, the decision "Size"); the init properties name every field at both building sites.
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

using System.Text.Json.Nodes;
using AerospacePropellantThermodynamics.Problems;
using AerospacePropellantThermodynamics.Thermo;

namespace AerospacePropellantThermodynamics.Cli;

/// <summary>
/// One case of an output document: what it was given, and what the library returned (the mixture with its mass in
/// kg). Declared with init properties, all required, so that every construction site names its fields (F-CL-09).
/// </summary>
internal sealed record CaseOutput
{
    public required int Index { get; init; }

    public required JsonNode Inputs { get; init; }

    public required CaseStatus Status { get; init; }

    public required ElementalMixture Mixture { get; init; }

    public required double MixtureMass { get; init; }

    public required IReadOnlyList<string> Species { get; init; }

    public required IReadOnlyList<Station> Stations { get; init; }
}

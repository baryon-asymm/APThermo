using System.Text.Json;
using AerospacePropellantThermodynamics.Performance;

namespace AerospacePropellantThermodynamics.Cli;

/// <summary>
/// One record of the states command. <see cref="Record"/> is the record as given, echoed into the output;
/// <see cref="Source"/> names it in a message (a file and a position). Step 2 of the clean-code decomposition keeps
/// this node's own reading of the record's shape; the front door's <c>StateRecord</c> and its rules follow in step 3.
/// </summary>
internal sealed record StateDocument(int Index, string Source, JsonElement Record, double Pressure, IReadOnlyDictionary<string, double> Composition)
{
    public double? Enthalpy { get; init; }

    public double? Temperature { get; init; }

    public double? Entropy { get; init; }

    public IReadOnlyList<double> AreaRatios { get; init; } = [];

    public IReadOnlyList<double> PressureRatios { get; init; } = [];

    public FlowModel Flow { get; init; } = FlowModel.ShiftingEquilibrium;

    public bool IsRocket => AreaRatios.Count + PressureRatios.Count > 0;
}

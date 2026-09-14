using AerospacePropellantThermodynamics.Execution;
using AerospacePropellantThermodynamics.Thermo;

namespace AerospacePropellantThermodynamics.Problems;

/// <summary>One element set with its species table and its copy uploaded to the engine's accelerator; disposable.</summary>
internal sealed class ChemicalSystem(IReadOnlyList<string> elements, SpeciesTable table, UploadedTables tables) : IDisposable
{
    public IReadOnlyList<string> Elements { get; } = elements;

    public SpeciesTable Table { get; } = table;

    public UploadedTables Tables { get; } = tables;

    public void Dispose() => Tables.Dispose();
}

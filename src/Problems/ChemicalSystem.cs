using APThermo.Execution;
using APThermo.Thermo;

namespace APThermo.Problems;

/// <summary>One element set with its species table and its copy uploaded to the engine's accelerator; disposable.</summary>
internal sealed class ChemicalSystem(IReadOnlyList<string> elements, SpeciesTable table, UploadedTables tables) : IDisposable
{
    public IReadOnlyList<string> Elements { get; } = elements;

    public SpeciesTable Table { get; } = table;

    public UploadedTables Tables { get; } = tables;

    // Cleanup stores diagnostics in the existing owner, so a child OOM does not allocate during the attempt loop.
    internal Exception? DisposalFailure { get; set; }

    internal ChemicalSystem? NextFailedDisposal { get; set; }

    public void Dispose() => Tables.Dispose();
}

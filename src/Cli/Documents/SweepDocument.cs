namespace AerospacePropellantThermodynamics.Cli.Documents;

/// <summary>The lists of a sweep, expanded from lists or ranges; null where the document sweeps nothing.</summary>
internal sealed record SweepDocument(IReadOnlyList<double>? OxidizerToFuel, IReadOnlyList<double>? ChamberPressure, IReadOnlyList<double>? Pressure, IReadOnlyList<double>? Temperature);

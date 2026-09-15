namespace APThermo.Cli.Cases;

/// <summary>One combination of the sweep; null where the document's own value stands.</summary>
internal sealed record Combination(double? OxidizerToFuel, double? ChamberPressure, double? Pressure, double? Temperature);

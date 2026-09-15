namespace APThermo.Data;

/// <summary>Phase of a species record: gaseous, or condensed (solid or liquid).</summary>
public enum SpeciesPhase
{
    Gas,
    Condensed,
}

/// <summary>Section of <c>thermo.inp</c> a record comes from.</summary>
public enum SpeciesSection
{
    Products,
    Reactants,
}

/// <summary>One element of a chemical formula: the symbol as written in the file and the number of atoms.</summary>
public readonly record struct ElementCount(string Symbol, double Count);

/// <summary>
/// One temperature interval of a species record: the NASA nine-constant polynomial
/// (seven coefficients and two integration constants) with the exponents of T it applies to.
/// </summary>
public sealed record TemperatureInterval(
    double TLow,
    double THigh,
    IReadOnlyList<double> Exponents,
    IReadOnlyList<double> Coefficients,
    double B1,
    double B2,
    double EnthalpyOffset);

/// <summary>A species record of <c>thermo.inp</c>, exactly as in the file; units are the file's (kg/kmol, J/mol, K).</summary>
public sealed record Species(
    string Name,
    string Comment,
    string DateCode,
    IReadOnlyList<ElementCount> Formula,
    SpeciesPhase Phase,
    double MolarMass,
    double FormationEnthalpy,
    double AssignedTemperature,
    IReadOnlyList<TemperatureInterval> Intervals,
    SpeciesSection Section,
    bool IsInert);

/// <summary>Where the loaded data came from.</summary>
public sealed record DatabaseProvenance(
    string HeaderDate,
    IReadOnlyList<double> DefaultIntervalBounds,
    string ThermoSha256,
    string? TransSha256);

/// <summary>A transport fit: ln(property) = A ln T + B/T + C/T² + D, in the file's units.</summary>
public sealed record TransportFit(double TLow, double THigh, double A, double B, double C, double D);

/// <summary>A block of <c>trans.inp</c>: one species, or one binary interaction when <see cref="Partner"/> is set.</summary>
public sealed record TransportEntry(
    string Species,
    string? Partner,
    string Reference,
    IReadOnlyList<TransportFit> Viscosity,
    IReadOnlyList<TransportFit> Conductivity);

/// <summary>Raised when a file does not follow the NASA format; names the file and the line.</summary>
public sealed class DatabaseFormatException : Exception
{
    public DatabaseFormatException(string? fileName, int lineNumber, string message, Exception? inner = null)
        : base($"{fileName ?? "<text>"}:{lineNumber}: {message}", inner)
    {
        FileName = fileName;
        LineNumber = lineNumber;
    }

    public string? FileName { get; }

    public int LineNumber { get; }
}

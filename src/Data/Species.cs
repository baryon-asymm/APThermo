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
/// Nominal, with an internal constructor (root <c>BOOT.md</c>, Delivery: Tree contracts, "records the
/// library creates for consumers ... have internal constructors"): only this node parses one.
/// </summary>
public sealed record TemperatureInterval
{
    internal TemperatureInterval(
        double tLow, double tHigh, IReadOnlyList<double> exponents, IReadOnlyList<double> coefficients,
        double b1, double b2, double enthalpyOffset)
    {
        TLow = tLow;
        THigh = tHigh;
        Exponents = exponents;
        Coefficients = coefficients;
        B1 = b1;
        B2 = b2;
        EnthalpyOffset = enthalpyOffset;
    }

    public double TLow { get; }

    public double THigh { get; }

    public IReadOnlyList<double> Exponents { get; }

    public IReadOnlyList<double> Coefficients { get; }

    public double B1 { get; }

    public double B2 { get; }

    public double EnthalpyOffset { get; }
}

/// <summary>
/// A species record of <c>thermo.inp</c>, exactly as in the file; units are the file's (kg/kmol, J/mol, K).
/// Nominal, with an internal constructor (root <c>BOOT.md</c>, Delivery: Tree contracts): only this node
/// parses one.
/// </summary>
public sealed record Species
{
    internal Species(
        string name, string comment, string dateCode, IReadOnlyList<ElementCount> formula, SpeciesPhase phase,
        double molarMass, double formationEnthalpy, double assignedTemperature, IReadOnlyList<TemperatureInterval> intervals,
        SpeciesSection section, bool isInert)
    {
        Name = name;
        Comment = comment;
        DateCode = dateCode;
        Formula = formula;
        Phase = phase;
        MolarMass = molarMass;
        FormationEnthalpy = formationEnthalpy;
        AssignedTemperature = assignedTemperature;
        Intervals = intervals;
        Section = section;
        IsInert = isInert;
    }

    public string Name { get; }

    public string Comment { get; }

    public string DateCode { get; }

    public IReadOnlyList<ElementCount> Formula { get; }

    public SpeciesPhase Phase { get; }

    public double MolarMass { get; }

    public double FormationEnthalpy { get; }

    public double AssignedTemperature { get; }

    public IReadOnlyList<TemperatureInterval> Intervals { get; }

    public SpeciesSection Section { get; }

    public bool IsInert { get; }
}

/// <summary>
/// Where the loaded data came from. Nominal, with an internal constructor (root <c>BOOT.md</c>, Delivery:
/// Tree contracts): only <see cref="SpeciesDatabase.Load"/> builds one.
/// </summary>
public sealed record DatabaseProvenance
{
    internal DatabaseProvenance(string headerDate, IReadOnlyList<double> defaultIntervalBounds, string thermoSha256, string? transSha256)
    {
        HeaderDate = headerDate;
        DefaultIntervalBounds = defaultIntervalBounds;
        ThermoSha256 = thermoSha256;
        TransSha256 = transSha256;
    }

    public string HeaderDate { get; }

    public IReadOnlyList<double> DefaultIntervalBounds { get; }

    public string ThermoSha256 { get; }

    public string? TransSha256 { get; }
}

/// <summary>
/// A transport fit: ln(property) = A ln T + B/T + C/T² + D, in the file's units. Nominal, with an internal
/// constructor (root <c>BOOT.md</c>, Delivery: Tree contracts): only this node parses one.
/// </summary>
public sealed record TransportFit
{
    internal TransportFit(double tLow, double tHigh, double a, double b, double c, double d)
    {
        TLow = tLow;
        THigh = tHigh;
        A = a;
        B = b;
        C = c;
        D = d;
    }

    public double TLow { get; }

    public double THigh { get; }

    public double A { get; }

    public double B { get; }

    public double C { get; }

    public double D { get; }
}

/// <summary>
/// A block of <c>trans.inp</c>: one species, or one binary interaction when <see cref="Partner"/> is set.
/// Nominal, with an internal constructor (root <c>BOOT.md</c>, Delivery: Tree contracts): only this node
/// parses one.
/// </summary>
public sealed record TransportEntry
{
    internal TransportEntry(string species, string? partner, string reference, IReadOnlyList<TransportFit> viscosity, IReadOnlyList<TransportFit> conductivity)
    {
        Species = species;
        Partner = partner;
        Reference = reference;
        Viscosity = viscosity;
        Conductivity = conductivity;
    }

    public string Species { get; }

    public string? Partner { get; }

    public string Reference { get; }

    public IReadOnlyList<TransportFit> Viscosity { get; }

    public IReadOnlyList<TransportFit> Conductivity { get; }
}

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

namespace APThermo.Data;

/// <summary>Phase of a species record: gaseous, or condensed (solid or liquid).</summary>
public enum SpeciesPhase
{
    /// <summary>A gas-phase species.</summary>
    Gas,

    /// <summary>A condensed (solid or liquid) species.</summary>
    Condensed,
}

/// <summary>Section of <c>thermo.inp</c> a record comes from.</summary>
public enum SpeciesSection
{
    /// <summary>The PRODUCTS section: a species the equilibrium solver may form.</summary>
    Products,

    /// <summary>The REACTANTS section: a species a propellant is assembled from.</summary>
    Reactants,
}

/// <summary>One element of a chemical formula: the symbol as written in the file and the number of atoms.</summary>
/// <param name="Symbol">The element symbol as written in the file, for example <c>"AL"</c> or <c>"Al"</c>.</param>
/// <param name="Count">The number of atoms of <paramref name="Symbol"/> in the formula.</param>
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

    /// <value>The lower bound of the interval, in kelvin, as written in the file.</value>
    public double TLow { get; }

    /// <value>The upper bound of the interval, in kelvin, as written in the file (see the note on
    /// non-ascending bounds in <c>API.md</c>).</value>
    public double THigh { get; }

    /// <value>The eight exponents of T the polynomial applies to, in file order.</value>
    public IReadOnlyList<double> Exponents { get; }

    /// <value>The seven polynomial coefficients a1…a7, in file order.</value>
    public IReadOnlyList<double> Coefficients { get; }

    /// <value>The first integration constant of the NASA polynomial.</value>
    public double B1 { get; }

    /// <value>The second integration constant of the NASA polynomial.</value>
    public double B2 { get; }

    /// <value>H(298.15) − H(0), in J/mol.</value>
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

    /// <value>The species name as in the file, trailing blanks trimmed.</value>
    public string Name { get; }

    /// <value>The free-text comment field of the record, as in the file.</value>
    public string Comment { get; }

    /// <value>The date code field of the record, as in the file.</value>
    public string DateCode { get; }

    /// <value>The chemical formula, zero-count pairs dropped, in file order.</value>
    public IReadOnlyList<ElementCount> Formula { get; }

    /// <value>Whether the species is gaseous or condensed.</value>
    public SpeciesPhase Phase { get; }

    /// <value>The molar mass, in kg/kmol.</value>
    public double MolarMass { get; }

    /// <value>The formation enthalpy at 298.15 K, in J/mol; the assigned enthalpy when
    /// <see cref="Intervals"/> is empty.</value>
    public double FormationEnthalpy { get; }

    /// <value>The assigned temperature, in kelvin; meaningful only when <see cref="Intervals"/> is empty.</value>
    public double AssignedTemperature { get; }

    /// <value>The temperature intervals of the NASA polynomial, in file order; empty for an assigned-enthalpy
    /// record.</value>
    public IReadOnlyList<TemperatureInterval> Intervals { get; }

    /// <value>Whether the record comes from the PRODUCTS or the REACTANTS section.</value>
    public SpeciesSection Section { get; }

    /// <value><see langword="true"/> when the name starts with <c>"Inert"</c> (a CEA pseudo-element record).</value>
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

    /// <value>The date field of the file's header line, as written.</value>
    public string HeaderDate { get; }

    /// <value>The default temperature interval bounds declared by the file's header line, in kelvin.</value>
    public IReadOnlyList<double> DefaultIntervalBounds { get; }

    /// <value>The lowercase hexadecimal SHA-256 of the <c>thermo.inp</c> bytes that were loaded or parsed.</value>
    public string ThermoSha256 { get; }

    /// <value>The lowercase hexadecimal SHA-256 of the <c>trans.inp</c> bytes that were loaded or parsed, or
    /// <see langword="null"/> when no transport file was given.</value>
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

    /// <value>The lower bound of the fit's temperature range, in kelvin.</value>
    public double TLow { get; }

    /// <value>The upper bound of the fit's temperature range, in kelvin.</value>
    public double THigh { get; }

    /// <value>The coefficient A of ln(property) = A ln T + B/T + C/T² + D.</value>
    public double A { get; }

    /// <value>The coefficient B of ln(property) = A ln T + B/T + C/T² + D.</value>
    public double B { get; }

    /// <value>The coefficient C of ln(property) = A ln T + B/T + C/T² + D.</value>
    public double C { get; }

    /// <value>The coefficient D of ln(property) = A ln T + B/T + C/T² + D; the fitted property comes out in
    /// the file's units (micropoise for viscosity, μW/(cm·K) for conductivity).</value>
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

    /// <value>The species name of the block, as in the file.</value>
    public string Species { get; }

    /// <value>The interaction partner's name when this is a binary-interaction block, or <see langword="null"/>
    /// for a single-species block.</value>
    public string? Partner { get; }

    /// <value>The reference field of the block, as in the file.</value>
    public string Reference { get; }

    /// <value>The viscosity fits of the block, in file order.</value>
    public IReadOnlyList<TransportFit> Viscosity { get; }

    /// <value>The thermal conductivity fits of the block, in file order.</value>
    public IReadOnlyList<TransportFit> Conductivity { get; }
}

/// <summary>Raised when a file does not follow the NASA format; names the file and the line.</summary>
public sealed class DatabaseFormatException : Exception
{
    /// <summary>Creates the exception for a malformed line at <paramref name="lineNumber"/>.</summary>
    /// <param name="fileName">The name of the offending file, or <see langword="null"/> when the data was
    /// parsed from a <see cref="TextReader"/> rather than loaded from a path.</param>
    /// <param name="lineNumber">The 1-based line number of the offending field.</param>
    /// <param name="message">A description of what is wrong with the line.</param>
    /// <param name="inner">The exception that caused this one, or <see langword="null"/> when there is none.</param>
    public DatabaseFormatException(string? fileName, int lineNumber, string message, Exception? inner = null)
        : base($"{fileName ?? "<text>"}:{lineNumber}: {message}", inner)
    {
        FileName = fileName;
        LineNumber = lineNumber;
    }

    /// <value>The name of the offending file, or <see langword="null"/> when parsed from a
    /// <see cref="TextReader"/>.</value>
    public string? FileName { get; }

    /// <value>The 1-based line number of the offending field.</value>
    public int LineNumber { get; }

    /// <summary>Initializes a new instance with no file name and a neutral line number of 0. The tree itself
    /// always throws through the constructor above; this exists for the .NET exception conventions (CA1032).</summary>
    public DatabaseFormatException() : base()
    {
        FileName = null;
        LineNumber = 0;
    }

    /// <summary>Initializes a new instance with the given message, no file name and a neutral line number of 0.</summary>
    public DatabaseFormatException(string message) : base(message)
    {
        FileName = null;
        LineNumber = 0;
    }

    /// <summary>Initializes a new instance with the given message and inner exception, no file name and a
    /// neutral line number of 0.</summary>
    public DatabaseFormatException(string message, Exception innerException) : base(message, innerException)
    {
        FileName = null;
        LineNumber = 0;
    }
}

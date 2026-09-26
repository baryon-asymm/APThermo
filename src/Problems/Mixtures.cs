using System.Globalization;
using APThermo.Performance;

namespace APThermo.Problems;

/// <summary>A mixture given by its element abundances and enthalpy per kilogram: what every solve starts from, whatever the input was.</summary>
public sealed record ElementalMixture
{
    /// <summary>
    /// The mass tolerance a mixture declares when it names none: how far the mass of its element moles, Σ n_i A_i with the database's
    /// atomic weights, may lie from one kilogram (relative). The solver refuses a mixture beyond its tolerance with a
    /// <see cref="MixtureMassException"/>, whichever front door it came through; BOOT.md derives the value.
    /// </summary>
    public const double DefaultMassTolerance = 1.0e-2;

    private ElementalMixture(IReadOnlyDictionary<string, double> elementMoles, double? enthalpy, IReadOnlyList<string> elements,
                             IReadOnlyList<string> omit, IReadOnlyList<string>? only, double massTolerance)
    {
        ElementMoles = elementMoles;
        Enthalpy = enthalpy;
        Elements = elements;
        Omit = omit;
        Only = only;
        MassTolerance = massTolerance;
    }

    /// <summary>
    /// Element moles per kilogram (mol/kg) by symbol, the enthalpy in J/kg (null when only assigned-temperature problems will be solved),
    /// and the mass tolerance the mixture declares, relative to one kilogram (BOOT.md).
    /// </summary>
    /// <param name="elementMoles">Element moles, in mol/kg, by symbol; not empty, every symbol non-blank and
    /// given once, every abundance finite and non-negative.</param>
    /// <param name="enthalpy">The mixture's enthalpy, in J/kg, or <see langword="null"/> when only
    /// assigned-temperature problems will be solved with this mixture; finite when given.</param>
    /// <param name="omit">Product species never to consider, or <see langword="null"/> for none.</param>
    /// <param name="only">When given, exactly the product species to consider.</param>
    /// <param name="massTolerance">The mass tolerance this mixture declares, relative to one kilogram; finite
    /// and non-negative. Defaults to <see cref="DefaultMassTolerance"/>.</param>
    /// <returns>The mixture.</returns>
    /// <exception cref="ArgumentException"><paramref name="elementMoles"/> is empty, names a blank or repeated
    /// symbol, or carries a negative, infinite or non-finite abundance; <paramref name="enthalpy"/> is given and
    /// is not finite; or <paramref name="massTolerance"/> is not finite and non-negative.</exception>
    public static ElementalMixture Create(IReadOnlyDictionary<string, double> elementMoles, double? enthalpy = null,
                                          IReadOnlyList<string>? omit = null, IReadOnlyList<string>? only = null,
                                          double massTolerance = DefaultMassTolerance)
    {
        ArgumentNullException.ThrowIfNull(elementMoles);
        if (elementMoles.Count == 0)
        {
            throw new ArgumentException("the mixture has no elements", nameof(elementMoles));
        }

        if (enthalpy is { } h && !double.IsFinite(h))
        {
            throw new ArgumentException("the enthalpy must be finite", nameof(enthalpy));
        }

        if (!IsValidMassTolerance(massTolerance))
        {
            throw new ArgumentException($"the mass tolerance must be a finite non-negative number, not {massTolerance}", nameof(massTolerance));
        }

        var moles = new Dictionary<string, double>(StringComparer.Ordinal);
        var elements = new List<string>();
        foreach (var (symbol, value) in elementMoles)
        {
            if (string.IsNullOrWhiteSpace(symbol))
            {
                throw new ArgumentException("an element symbol is empty", nameof(elementMoles));
            }

            var spelling = SpeciesSelection.Spelling(symbol);
            if (!moles.TryAdd(spelling, value))
            {
                throw new ArgumentException($"element '{spelling}' is given twice", nameof(elementMoles));
            }

            if (!(value >= 0.0) || double.IsInfinity(value))
            {
                throw new ArgumentException($"element '{spelling}': the abundance must be a finite non-negative number, not {value}", nameof(elementMoles));
            }

            elements.Add(spelling);
        }

        return new ElementalMixture(moles, enthalpy, elements, omit?.Distinct(StringComparer.Ordinal).ToList() ?? [], only?.Distinct(StringComparer.Ordinal).ToList(), massTolerance);
    }

    /// <summary>mol per kg, by symbol in the database spelling; insertion order is the order of <see cref="Elements"/>.</summary>
    public IReadOnlyDictionary<string, double> ElementMoles { get; }

    /// <summary>J/kg, or null.</summary>
    public double? Enthalpy { get; }

    /// <summary>The order used in results and in the tables.</summary>
    public IReadOnlyList<string> Elements { get; }

    /// <value>Product species never to consider.</value>
    public IReadOnlyList<string> Omit { get; }

    /// <value>When not <see langword="null"/>, exactly the product species to consider.</value>
    public IReadOnlyList<string>? Only { get; }

    /// <summary>The mass tolerance this mixture declares: how far Σ n_i A_i may lie from one kilogram, relative; the solver checks it.</summary>
    public double MassTolerance { get; }

    /// <summary>Whether a mass tolerance is one <see cref="Create"/> accepts (finite and non-negative); the one statement of the rule (the clean-code review's open question 2).</summary>
    /// <param name="massTolerance">The mass tolerance to check, relative to one kilogram.</param>
    /// <returns><see langword="true"/> when <paramref name="massTolerance"/> is finite and non-negative.</returns>
    public static bool IsValidMassTolerance(double massTolerance) => double.IsFinite(massTolerance) && massTolerance >= 0.0;

    /// <summary>The abundances in the numerical nodes' unit, kmol per kg, in the order of the given element list; absent elements are zero.</summary>
    internal double[] KilomolesPerKilogram(IReadOnlyList<string> elements)
    {
        var result = new double[elements.Count];
        for (var i = 0; i < elements.Count; i++)
        {
            // Multiplying by the reciprocal reproduces the pre-decomposition v * 1.0e-3 bit for bit; see MixtureMass.Of.
            result[i] = ElementMoles.TryGetValue(elements[i], out var value) ? value * (1.0 / UnitFactors.MolesPerKilomole) : 0.0;
        }

        return result;
    }
}

/// <summary>
/// The exchange record of another simulation: one state of a mixture, with exactly one of enthalpy, temperature and entropy
/// given. A record with exits (<see cref="AreaRatios"/> or <see cref="PressureRatios"/>) is a rocket case whose
/// <see cref="Pressure"/> is the chamber pressure and whose enthalpy is required (F-AR-02, decided at the root); it solves
/// through <see cref="Solver.SolveRocketStates"/>, and one without exits through <see cref="Solver.SolveStates"/>.
/// </summary>
/// <param name="Pressure">The pressure, in Pa; the chamber pressure when <see cref="HasExits"/> is
/// <see langword="true"/>.</param>
/// <param name="Composition">Element moles, in mol/kg, by symbol.</param>
/// <param name="Enthalpy">The enthalpy, in J/kg, for an assigned-enthalpy problem or for every record with
/// exits; <see langword="null"/> otherwise.</param>
/// <param name="Temperature">The temperature, in kelvin, for an assigned-temperature problem;
/// <see langword="null"/> otherwise.</param>
/// <param name="Entropy">The entropy, in J/(kg·K), for an assigned-entropy problem; <see langword="null"/>
/// otherwise.</param>
public sealed record StateRecord(
    double Pressure,                                  // Pa
    IReadOnlyDictionary<string, double> Composition,  // element moles, mol per kg
    double? Enthalpy = null,                          // J/kg: an assigned-enthalpy problem, or every rocket case
    double? Temperature = null,                       // K: an assigned-temperature problem
    double? Entropy = null)                           // J/(kg·K): an assigned-entropy problem
{
    /// <summary>Supersonic A / A_t, reported after the pressure-ratio exits; empty by default.</summary>
    public IReadOnlyList<double> AreaRatios { get; init; } = [];

    /// <summary>p_c / p_e, reported first; empty by default.</summary>
    public IReadOnlyList<double> PressureRatios { get; init; } = [];

    /// <summary>Records with exits only; null means shifting equilibrium. Meaningless, and refused, on a record without exits.</summary>
    public FlowModel? Flow { get; init; }

    /// <summary>Whether this record names an exit: the fact <see cref="StateRecords"/> reads to route it to the rocket or to the equilibrium path.</summary>
    public bool HasExits => AreaRatios.Count > 0 || PressureRatios.Count > 0;
}

/// <summary>Options of a state batch: transport at every state, the species lists applied to the batch's table, and the mass tolerance every record declares.</summary>
/// <param name="Transport">Whether to compute transport properties at every state.</param>
/// <param name="Omit">Product species never to consider, or <see langword="null"/> for none.</param>
/// <param name="Only">When given, exactly the product species to consider.</param>
/// <param name="MassTolerance">The mass tolerance every record of the batch declares, relative to one kilogram.
/// Defaults to <see cref="ElementalMixture.DefaultMassTolerance"/>.</param>
public sealed record StateBatchOptions(bool Transport = false, IReadOnlyList<string>? Omit = null, IReadOnlyList<string>? Only = null,
                                       double MassTolerance = ElementalMixture.DefaultMassTolerance);

/// <summary>
/// A state record refused by a rule of its shape (`StateRecords`, BOOT.md): none or several of enthalpy, temperature and
/// entropy given; exits without an enthalpy; a flow named without exits; a record with exits given to
/// <see cref="Solver.SolveStates"/> or one without given to <see cref="Solver.SolveRocketStates"/>; or a composition rule
/// (a negative abundance, an empty or duplicated symbol). Not the mass rule, which keeps <see cref="MixtureMassException"/>,
/// carrying the index already.
/// </summary>
public sealed class StateRecordException : ArgumentException
{
    /// <summary>Creates the exception for the record at <paramref name="index"/>.</summary>
    /// <param name="index">The record's position in the list given.</param>
    /// <param name="reason">A description of the shape rule that was broken.</param>
    public StateRecordException(int index, string reason)
        : base($"state record {index}: {reason}")
    {
        Index = index;
        Reason = reason;
    }

    /// <summary>The record's position in the list given.</summary>
    public int Index { get; }

    /// <summary>The message without the subject, for a caller that names the record its own way.</summary>
    public string Reason { get; }

    /// <summary>Initializes a new instance with a neutral index of −1 and an empty reason. The tree itself
    /// always throws through the constructor above; this exists for the .NET exception conventions (CA1032).</summary>
    public StateRecordException() : base()
    {
        Index = -1;
        Reason = string.Empty;
    }

    /// <summary>Initializes a new instance with the given message as the reason and a neutral index of −1.</summary>
    public StateRecordException(string message) : base(message)
    {
        Index = -1;
        Reason = message;
    }

    /// <summary>Initializes a new instance with the given message as the reason and inner exception, and a
    /// neutral index of −1.</summary>
    public StateRecordException(string message, Exception innerException) : base(message, innerException)
    {
        Index = -1;
        Reason = message;
    }
}

/// <summary>
/// A mixture whose element moles do not describe one kilogram: their mass with the database's atomic weights differs from 1 kg by more
/// than the mixture's <see cref="ElementalMixture.MassTolerance"/>. An <see cref="ArgumentException"/>, so that a caller mapping those maps this one too.
/// </summary>
public sealed class MixtureMassException : ArgumentException
{
    /// <summary>Creates the exception naming <paramref name="subject"/> and reporting the mass and the
    /// tolerance in force.</summary>
    /// <param name="subject">How the caller names the mixture (for example <c>"mixture 3"</c> or
    /// <c>"state record 3"</c>).</param>
    /// <param name="index">The position of the mixture in its batch: the case index, or the state record's
    /// index.</param>
    /// <param name="mass">The mass, in kg, the element moles describe with the database's atomic weights.</param>
    /// <param name="tolerance">The tolerance in force, relative to one kilogram.</param>
    public MixtureMassException(string subject, int index, double mass, double tolerance)
        : base(string.Create(CultureInfo.InvariantCulture, $"{subject}: {ReasonFor(mass, tolerance)}"))
    {
        Index = index;
        Mass = mass;
        Tolerance = tolerance;
        Reason = ReasonFor(mass, tolerance);
    }

    /// <summary>The position of the mixture in its batch: the case index, or the state record's index.</summary>
    public int Index { get; }

    /// <summary>Σ n_i A_i in kg: the mass the element moles describe with the database's atomic weights.</summary>
    public double Mass { get; }

    /// <summary>The tolerance in force, relative to one kilogram: the mixture's <see cref="ElementalMixture.MassTolerance"/>.</summary>
    public double Tolerance { get; }

    /// <summary>The message without the subject, for a caller that names the mixture its own way (the command line names the record's file and position).</summary>
    public string Reason { get; }

    private static string ReasonFor(double mass, double tolerance) => string.Create(
        CultureInfo.InvariantCulture,
        $"the composition weighs {mass * UnitFactors.GramsPerKilogram:G7} g with the database's atomic weights; element moles are per kilogram of mixture, so it must weigh 1000 g within {tolerance * 100.0:G3} %");

    /// <summary>Initializes a new instance with a neutral index of −1, NaN mass and tolerance, and an empty
    /// reason. The tree itself always throws through the constructor above; this exists for the .NET
    /// exception conventions (CA1032).</summary>
    public MixtureMassException() : base()
    {
        Index = -1;
        Mass = double.NaN;
        Tolerance = double.NaN;
        Reason = string.Empty;
    }

    /// <summary>Initializes a new instance with the given message as the reason, a neutral index of −1 and
    /// NaN mass and tolerance.</summary>
    public MixtureMassException(string message) : base(message)
    {
        Index = -1;
        Mass = double.NaN;
        Tolerance = double.NaN;
        Reason = message;
    }

    /// <summary>Initializes a new instance with the given message as the reason and inner exception, a
    /// neutral index of −1 and NaN mass and tolerance.</summary>
    public MixtureMassException(string message, Exception innerException) : base(message, innerException)
    {
        Index = -1;
        Mass = double.NaN;
        Tolerance = double.NaN;
        Reason = message;
    }
}

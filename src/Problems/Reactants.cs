using APThermo.Data;

namespace APThermo.Problems;

/// <summary>The group a reactant belongs to: oxidizers and fuels are split by the oxidizer-to-fuel ratio; a named reactant carries a total mass fraction.</summary>
public enum ReactantRole
{
    Oxidizer,
    Fuel,
    Named,
}

/// <summary>How a reactant's amount is given within its group.</summary>
public enum AmountKind
{
    MassFraction,
    Moles,
}

/// <summary>
/// The facts that exist only together on a custom reactant (F-PR-05): its formula, its enthalpy and the temperature it was
/// fitted at, and optionally its molar mass; travel as one record instead of four adjacent parameters of <see cref="Reactant.Custom"/>.
/// </summary>
public sealed record CustomReactantDefinition(
    IReadOnlyList<ElementCount> Formula,   // atoms per formula unit; not empty
    double Enthalpy,                       // J/mol at Temperature; finite
    double Temperature,                    // K; positive
    double? MolarMass = null);             // kg/kmol; null = from the formula and the atomic weights

/// <summary>One reactant of a propellant: a database record by name, or a custom definition by formula and enthalpy (a binder such as HTPB).</summary>
public sealed record Reactant
{
    /// <summary>The temperature assumed for a database record with polynomial intervals when none is given, as the reference assumes it.</summary>
    public const double DefaultTemperature = 298.15;

    private Reactant(string name, ReactantRole role, double amount, AmountKind amountKind, double? temperature, CustomReactantDefinition? definition)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        if (!(amount >= 0.0) || double.IsInfinity(amount))
        {
            throw new ArgumentException($"reactant '{name}': the amount must be a finite non-negative number", nameof(amount));
        }

        if (temperature is { } t && !(t > 0.0 && double.IsFinite(t)))
        {
            throw new ArgumentException($"reactant '{name}': the temperature must be positive and finite", nameof(temperature));
        }

        if (definition is not null)
        {
            if (definition.Formula.Count == 0)
            {
                throw new ArgumentException($"reactant '{name}': the formula is empty", nameof(definition));
            }

            foreach (var pair in definition.Formula)
            {
                if (string.IsNullOrWhiteSpace(pair.Symbol) || !(pair.Count > 0.0) || double.IsInfinity(pair.Count))
                {
                    throw new ArgumentException($"reactant '{name}': the formula entry '{pair.Symbol}' must have a symbol and a positive count", nameof(definition));
                }
            }

            if (!double.IsFinite(definition.Enthalpy))
            {
                throw new ArgumentException($"reactant '{name}': the enthalpy must be finite", nameof(definition));
            }

            if (definition.MolarMass is { } m && !(m > 0.0 && double.IsFinite(m)))
            {
                throw new ArgumentException($"reactant '{name}': the molar mass must be positive", nameof(definition));
            }
        }

        Name = name.Trim();
        Role = role;
        Amount = amount;
        AmountKind = amountKind;
        Temperature = temperature;
        Definition = definition;
    }

    /// <summary>A reactant of the database by exact name; the temperature defaults to the record's assigned temperature, or to 298.15 K for a record with polynomial intervals.</summary>
    public static Reactant FromDatabase(string name, ReactantRole role, double amount, double? temperature = null, AmountKind amountKind = AmountKind.MassFraction) =>
        new(name, role, amount, amountKind, temperature, null);

    /// <summary>A reactant defined by its <paramref name="definition"/> (formula, enthalpy and temperature), as the reference defines exploded-formula reactants.</summary>
    public static Reactant Custom(string name, CustomReactantDefinition definition, ReactantRole role, double amount, AmountKind amountKind = AmountKind.MassFraction)
    {
        ArgumentNullException.ThrowIfNull(definition);
        return new Reactant(name, role, amount, amountKind, definition.Temperature, definition);
    }

    public string Name { get; }

    public ReactantRole Role { get; }

    /// <summary>Within its role group: a mass fraction or a mole count, normalized by the builder.</summary>
    public double Amount { get; }

    public AmountKind AmountKind { get; }

    /// <summary>K; null means the record's default.</summary>
    public double? Temperature { get; }

    public bool IsCustom => Definition is not null;

    /// <summary>Custom reactants only: the formula, enthalpy, temperature and molar mass that exist only together (F-PR-05).</summary>
    public CustomReactantDefinition? Definition { get; }
}

/// <summary>How the reactants' amounts make up one kilogram of propellant.</summary>
public abstract record MixtureSpecification
{
    private MixtureSpecification()
    {
    }

    /// <summary>The oxidizer group takes Ratio / (1 + Ratio) of the kilogram, the fuel group the rest; amounts are normalized within each group.</summary>
    public sealed record OxidizerToFuel(double Ratio) : MixtureSpecification;

    /// <summary>The amounts are total mass fractions over all reactants, normalized to one.</summary>
    public sealed record MassFractions : MixtureSpecification;
}

/// <summary>A propellant: reactants with their amounts and temperatures, the mixture rule, and the species lists to omit or to use only.</summary>
public sealed record Propellant
{
    internal Propellant(IReadOnlyList<Reactant> reactants, MixtureSpecification mixture, IReadOnlyList<string> elements,
                        IReadOnlyList<string> omit, IReadOnlyList<string>? only, IReadOnlyList<ResolvedReactant> resolved)
    {
        Reactants = reactants;
        Mixture = mixture;
        Elements = elements;
        Omit = omit;
        Only = only;
        Resolved = resolved;
    }

    public static PropellantBuilder From(SpeciesDatabase database) => new(database);

    public IReadOnlyList<Reactant> Reactants { get; }

    public MixtureSpecification Mixture { get; }

    /// <summary>Database spelling, in order of first appearance: oxidizers, then fuels, then named reactants, each in the order given.</summary>
    public IReadOnlyList<string> Elements { get; }

    /// <summary>Product species never to consider.</summary>
    public IReadOnlyList<string> Omit { get; }

    /// <summary>When given, exactly the product species to consider.</summary>
    public IReadOnlyList<string>? Only { get; }

    /// <summary>The ratio of the mixture rule, or null for a propellant given by total mass fractions.</summary>
    public double? OxidizerToFuelRatio => Mixture is MixtureSpecification.OxidizerToFuel ratio ? ratio.Ratio : null;

    internal IReadOnlyList<ResolvedReactant> Resolved { get; }
}

/// <summary>
/// A reactant with its database record resolved: the formula in database spelling, the molar mass, the temperature and
/// the amount as a mass. Its enthalpy source follows from what it resolved to, so it is derived, not stored: a record with
/// intervals is evaluated at <see cref="Temperature"/>; a record without intervals carries its assigned enthalpy in the
/// formation-enthalpy field, and a custom reactant in its definition.
/// </summary>
internal sealed record ResolvedReactant(
    Reactant Reactant,
    Species? Record,                                   // null for a custom reactant
    IReadOnlyList<(string Symbol, double Count)> Formula,
    double MolarMass,                                  // kg/kmol
    double Temperature,                                // K, the default applied
    double Mass)                                        // the amount as a mass, before normalization
{
    /// <summary>True when the enthalpy comes from the record's polynomial at <see cref="Temperature"/>, false when it is <see cref="AssignedEnthalpy"/>.</summary>
    public bool HasFits => Record is { Intervals.Count: > 0 };

    /// <summary>J/mol, used when <see cref="HasFits"/> is false: the record's assigned enthalpy, or a custom reactant's definition.</summary>
    public double AssignedEnthalpy => Record?.FormationEnthalpy ?? Reactant.Definition!.Enthalpy;
}

/// <summary>Builds a propellant against a database: names are resolved, temperatures checked and amounts converted to masses when Build runs.</summary>
public sealed class PropellantBuilder
{
    /// <summary>A reactant temperature may lie this far outside its record's range; the reference's own notion of a record's valid range is this wide around an assigned temperature.</summary>
    public const double TemperatureMargin = 10.0;

    private readonly SpeciesDatabase _database;
    private readonly List<Reactant> _reactants = [];
    private readonly List<string> _omit = [];
    private List<string>? _only;
    private double? _ratio;

    internal PropellantBuilder(SpeciesDatabase database)
    {
        ArgumentNullException.ThrowIfNull(database);
        _database = database;
    }

    public PropellantBuilder Oxidizer(string name, double? temperature = null, double amount = 1.0, AmountKind amountKind = AmountKind.MassFraction) =>
        Add(Reactant.FromDatabase(name, ReactantRole.Oxidizer, amount, temperature, amountKind));

    public PropellantBuilder Fuel(string name, double? temperature = null, double amount = 1.0, AmountKind amountKind = AmountKind.MassFraction) =>
        Add(Reactant.FromDatabase(name, ReactantRole.Fuel, amount, temperature, amountKind));

    /// <summary>A reactant with a total mass fraction, outside any oxidizer-to-fuel split.</summary>
    public PropellantBuilder Named(string name, double massFraction, double? temperature = null) =>
        Add(Reactant.FromDatabase(name, ReactantRole.Named, massFraction, temperature));

    public PropellantBuilder Custom(Reactant reactant) => Add(reactant);

    public PropellantBuilder Add(Reactant reactant)
    {
        ArgumentNullException.ThrowIfNull(reactant);
        _reactants.Add(reactant);
        return this;
    }

    public PropellantBuilder OxidizerToFuelRatio(double ratio)
    {
        _ratio = ratio;
        return this;
    }

    public PropellantBuilder Omit(params string[] species)
    {
        _omit.AddRange(species);
        return this;
    }

    public PropellantBuilder Only(params string[] species)
    {
        _only = [.. species];
        return this;
    }

    public Propellant Build()
    {
        if (_reactants.Count == 0)
        {
            throw new ArgumentException("a propellant needs at least one reactant");
        }

        var resolved = _reactants.Select(r => ReactantResolver.Resolve(_database, r)).ToList();
        var oxidizers = resolved.Where(r => r.Reactant.Role == ReactantRole.Oxidizer).ToList();
        var fuels = resolved.Where(r => r.Reactant.Role == ReactantRole.Fuel).ToList();
        var named = resolved.Where(r => r.Reactant.Role == ReactantRole.Named).ToList();
        var mixture = MixtureRule.Validate(oxidizers, fuels, named, _ratio);

        var elements = ElementOrder.OfFirstAppearance(oxidizers.Concat(fuels).Concat(named).Select(r => r.Formula.Select(pair => pair.Symbol)));

        var omit = _omit.Distinct(StringComparer.Ordinal).ToList();
        var only = _only?.Distinct(StringComparer.Ordinal).ToList();
        if (only is not null)
        {
            SpeciesSelection.ValidateOnly(_database, elements, only);
        }

        return new Propellant(_reactants.ToList(), mixture, elements, omit, only, resolved);
    }
}

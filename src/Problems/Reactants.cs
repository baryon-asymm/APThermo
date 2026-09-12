using AerospacePropellantThermodynamics.Data;

namespace AerospacePropellantThermodynamics.Problems;

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

/// <summary>One reactant of a propellant: a database record by name, or a custom definition by formula and enthalpy (a binder such as HTPB).</summary>
public sealed record Reactant
{
    /// <summary>The temperature assumed for a database record with polynomial intervals when none is given, as the reference assumes it.</summary>
    public const double DefaultTemperature = 298.15;

    private Reactant(string name, ReactantRole role, double amount, AmountKind amountKind, double? temperature,
                     IReadOnlyList<ElementCount>? formula, double? enthalpy, double? molarMass)
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

        if (formula is not null)
        {
            if (formula.Count == 0)
            {
                throw new ArgumentException($"reactant '{name}': the formula is empty", nameof(formula));
            }

            foreach (var pair in formula)
            {
                if (string.IsNullOrWhiteSpace(pair.Symbol) || !(pair.Count > 0.0) || double.IsInfinity(pair.Count))
                {
                    throw new ArgumentException($"reactant '{name}': the formula entry '{pair.Symbol}' must have a symbol and a positive count", nameof(formula));
                }
            }

            if (enthalpy is null || !double.IsFinite(enthalpy.Value))
            {
                throw new ArgumentException($"reactant '{name}': the enthalpy must be finite", nameof(enthalpy));
            }

            if (molarMass is { } m && !(m > 0.0 && double.IsFinite(m)))
            {
                throw new ArgumentException($"reactant '{name}': the molar mass must be positive", nameof(molarMass));
            }
        }

        Name = name.Trim();
        Role = role;
        Amount = amount;
        AmountKind = amountKind;
        Temperature = temperature;
        Formula = formula;
        Enthalpy = enthalpy;
        MolarMass = molarMass;
    }

    /// <summary>A reactant of the database by exact name; the temperature defaults to the record's assigned temperature, or to 298.15 K for a record with polynomial intervals.</summary>
    public static Reactant FromDatabase(string name, ReactantRole role, double amount, double? temperature = null, AmountKind amountKind = AmountKind.MassFraction) =>
        new(name, role, amount, amountKind, temperature, null, null, null);

    /// <summary>A reactant defined by its formula and its enthalpy (J/mol) at a temperature, as the reference defines exploded-formula reactants.</summary>
    public static Reactant Custom(string name, IReadOnlyList<ElementCount> formula, double enthalpy, double temperature, ReactantRole role, double amount,
                                  double? molarMass = null, AmountKind amountKind = AmountKind.MassFraction)
    {
        ArgumentNullException.ThrowIfNull(formula);
        return new Reactant(name, role, amount, amountKind, temperature, formula, enthalpy, molarMass);
    }

    public string Name { get; }

    public ReactantRole Role { get; }

    /// <summary>Within its role group: a mass fraction or a mole count, normalized by the builder.</summary>
    public double Amount { get; }

    public AmountKind AmountKind { get; }

    /// <summary>K; null means the record's default.</summary>
    public double? Temperature { get; }

    public bool IsCustom => Formula is not null;

    /// <summary>Custom reactants only: atoms per formula unit.</summary>
    public IReadOnlyList<ElementCount>? Formula { get; }

    /// <summary>Custom reactants only: J/mol at <see cref="Temperature"/>.</summary>
    public double? Enthalpy { get; }

    /// <summary>Custom reactants only: kg/kmol; null means the sum over the formula with the database's atomic weights.</summary>
    public double? MolarMass { get; }
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

    /// <summary>The mass fraction of every reactant in one kilogram, for the propellant's ratio or the one given.</summary>
    internal double[] MassFractionsFor(double? oxidizerToFuelRatio)
    {
        var count = Resolved.Count;
        var fractions = new double[count];
        double? ratio = oxidizerToFuelRatio ?? OxidizerToFuelRatio;
        if (ratio is null)
        {
            var total = Resolved.Sum(r => r.Mass);
            for (var k = 0; k < count; k++)
            {
                fractions[k] = Resolved[k].Mass / total;
            }

            return fractions;
        }

        if (Mixture is MixtureSpecification.MassFractions)
        {
            throw new ArgumentException("the propellant is given by total mass fractions; it has no oxidizer-to-fuel ratio to set", nameof(oxidizerToFuelRatio));
        }

        var of = ratio.Value;
        if (!(of > 0.0) || double.IsInfinity(of))
        {
            throw new ArgumentException($"the oxidizer-to-fuel ratio must be positive and finite, not {of}", nameof(oxidizerToFuelRatio));
        }

        var oxidizerShare = of / (1.0 + of);
        var fuelShare = 1.0 / (1.0 + of);
        var oxidizerMass = Resolved.Where(r => r.Reactant.Role == ReactantRole.Oxidizer).Sum(r => r.Mass);
        var fuelMass = Resolved.Where(r => r.Reactant.Role == ReactantRole.Fuel).Sum(r => r.Mass);
        for (var k = 0; k < count; k++)
        {
            var r = Resolved[k];
            fractions[k] = r.Reactant.Role == ReactantRole.Oxidizer ? oxidizerShare * r.Mass / oxidizerMass : fuelShare * r.Mass / fuelMass;
        }

        return fractions;
    }
}

/// <summary>A reactant with its database record resolved: the formula in database spelling, the molar mass, the temperature and the enthalpy source.</summary>
internal sealed record ResolvedReactant(
    Reactant Reactant,
    Species? Record,                                   // null for a custom reactant
    IReadOnlyList<(string Symbol, double Count)> Formula,
    double MolarMass,                                  // kg/kmol
    double Temperature,                                // K, the default applied
    bool HasFits,                                      // the enthalpy comes from the record's polynomial at Temperature
    double AssignedEnthalpy,                           // J/mol, used when HasFits is false
    double Mass);                                      // the amount as a mass, before normalization

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

        var resolved = _reactants.Select(Resolve).ToList();
        var oxidizers = resolved.Where(r => r.Reactant.Role == ReactantRole.Oxidizer).ToList();
        var fuels = resolved.Where(r => r.Reactant.Role == ReactantRole.Fuel).ToList();
        var named = resolved.Where(r => r.Reactant.Role == ReactantRole.Named).ToList();
        MixtureSpecification mixture;
        if (_ratio is { } ratio)
        {
            if (!(ratio > 0.0) || double.IsInfinity(ratio))
            {
                throw new ArgumentException($"the oxidizer-to-fuel ratio must be positive and finite, not {ratio}");
            }

            if (oxidizers.Count == 0 || fuels.Count == 0)
            {
                throw new ArgumentException("an oxidizer-to-fuel ratio needs at least one oxidizer and one fuel");
            }

            if (named.Count > 0)
            {
                throw new ArgumentException($"reactant '{named[0].Reactant.Name}' is named with a total mass fraction, which cannot be combined with an oxidizer-to-fuel ratio");
            }

            if (!(oxidizers.Sum(r => r.Mass) > 0.0))
            {
                throw new ArgumentException("the oxidizer group has zero mass");
            }

            if (!(fuels.Sum(r => r.Mass) > 0.0))
            {
                throw new ArgumentException("the fuel group has zero mass");
            }

            mixture = new MixtureSpecification.OxidizerToFuel(ratio);
        }
        else
        {
            if (oxidizers.Count > 0 && fuels.Count > 0)
            {
                throw new ArgumentException("oxidizers and fuels were given without an oxidizer-to-fuel ratio; set the ratio, or name every reactant with a total mass fraction");
            }

            if (!(resolved.Sum(r => r.Mass) > 0.0))
            {
                throw new ArgumentException("the reactants have zero total mass");
            }

            mixture = new MixtureSpecification.MassFractions();
        }

        var elements = new List<string>();
        foreach (var r in oxidizers.Concat(fuels).Concat(named))
        {
            foreach (var (symbol, _) in r.Formula)
            {
                if (!elements.Contains(symbol, StringComparer.Ordinal))
                {
                    elements.Add(symbol);
                }
            }
        }

        var omit = _omit.Distinct(StringComparer.Ordinal).ToList();
        var only = _only?.Distinct(StringComparer.Ordinal).ToList();
        if (only is not null)
        {
            SpeciesSelection.ValidateOnly(_database, elements, only);
        }

        return new Propellant(_reactants.ToList(), mixture, elements, omit, only, resolved);
    }

    private ResolvedReactant Resolve(Reactant reactant)
    {
        if (reactant.IsCustom)
        {
            var formula = reactant.Formula!.Select(pair => (SpeciesSelection.Spelling(pair.Symbol), pair.Count)).ToList();
            var molarMass = 0.0;
            foreach (var (symbol, count) in formula)
            {
                double weight;
                try
                {
                    weight = _database.AtomicWeight(symbol);
                }
                catch (KeyNotFoundException inner)
                {
                    throw new ArgumentException($"reactant '{reactant.Name}': element '{symbol}' has no atomic weight in the database", inner);
                }

                molarMass += count * weight;
            }

            molarMass = reactant.MolarMass ?? molarMass;
            var temperature = reactant.Temperature!.Value;
            return new ResolvedReactant(reactant, null, formula, molarMass, temperature, false, reactant.Enthalpy!.Value, MassOf(reactant, molarMass));
        }

        if (!_database.TryGet(reactant.Name, out var record))
        {
            throw new KeyNotFoundException($"reactant '{reactant.Name}' is not in the database");
        }

        var hasFits = record.Intervals.Count > 0;
        var t = reactant.Temperature ?? (hasFits ? Reactant.DefaultTemperature : record.AssignedTemperature);
        double low, high;
        if (hasFits)
        {
            low = record.Intervals.Min(i => i.TLow);
            high = record.Intervals.Max(i => i.THigh);
        }
        else
        {
            low = high = record.AssignedTemperature;
        }

        if (t < low - TemperatureMargin || t > high + TemperatureMargin)
        {
            throw new ArgumentException(
                $"reactant '{reactant.Name}': temperature {t} K is outside the record's range {low}–{high} K (up to {TemperatureMargin} K beyond it is accepted)");
        }

        var pairs = record.Formula.Select(pair => (SpeciesSelection.Spelling(pair.Symbol), pair.Count)).ToList();
        return new ResolvedReactant(reactant, record, pairs, record.MolarMass, t, hasFits, record.FormationEnthalpy, MassOf(reactant, record.MolarMass));
    }

    private static double MassOf(Reactant reactant, double molarMass) =>
        reactant.AmountKind == AmountKind.Moles ? reactant.Amount * molarMass : reactant.Amount;
}

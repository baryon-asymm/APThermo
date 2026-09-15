using AerospacePropellantThermodynamics.Data;

namespace AerospacePropellantThermodynamics.Problems;

/// <summary>
/// One <see cref="Reactant"/> resolved against the database (BOOT.md, invariants): a database record by exact name, its
/// temperature defaulted (298.15 K for a record with intervals, its assigned temperature for one without) and accepted
/// within the record's range widened by <see cref="PropellantBuilder.TemperatureMargin"/>; or a custom definition, its
/// molar mass from the formula and the database's atomic weights unless the definition gives one. Either way the formula
/// in database spelling and the amount as a mass.
/// </summary>
internal static class ReactantResolver
{
    public static ResolvedReactant Resolve(SpeciesDatabase database, Reactant reactant) =>
        reactant.IsCustom ? Custom(database, reactant) : FromDatabase(database, reactant);

    private static ResolvedReactant Custom(SpeciesDatabase database, Reactant reactant)
    {
        var definition = reactant.Definition!;
        var formula = definition.Formula.Select(pair => (SpeciesSelection.Spelling(pair.Symbol), pair.Count)).ToList();
        var molarMass = 0.0;
        foreach (var (symbol, count) in formula)
        {
            double weight;
            try
            {
                weight = database.AtomicWeight(symbol);
            }
            catch (KeyNotFoundException inner)
            {
                throw new ArgumentException($"reactant '{reactant.Name}': element '{symbol}' has no atomic weight in the database", inner);
            }

            molarMass += count * weight;
        }

        molarMass = definition.MolarMass ?? molarMass;
        var temperature = reactant.Temperature!.Value;
        return new ResolvedReactant(reactant, null, formula, molarMass, temperature, MassOf(reactant, molarMass));
    }

    private static ResolvedReactant FromDatabase(SpeciesDatabase database, Reactant reactant)
    {
        if (!database.TryGet(reactant.Name, out var record))
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

        if (t < low - PropellantBuilder.TemperatureMargin || t > high + PropellantBuilder.TemperatureMargin)
        {
            throw new ArgumentException(
                $"reactant '{reactant.Name}': temperature {t} K is outside the record's range {low}–{high} K (up to {PropellantBuilder.TemperatureMargin} K beyond it is accepted)");
        }

        var pairs = record.Formula.Select(pair => (SpeciesSelection.Spelling(pair.Symbol), pair.Count)).ToList();
        return new ResolvedReactant(reactant, record, pairs, record.MolarMass, t, MassOf(reactant, record.MolarMass));
    }

    private static double MassOf(Reactant reactant, double molarMass) =>
        reactant.AmountKind == AmountKind.Moles ? reactant.Amount * molarMass : reactant.Amount;
}

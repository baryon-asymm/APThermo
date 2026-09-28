using APThermo.Data;

namespace APThermo.Problems;

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
        var records = database.Records(reactant.Name);
        if (records.Count == 0)
        {
            throw new KeyNotFoundException($"reactant '{reactant.Name}' is not in the database");
        }

        // Several records of one name resolve to the last, as cea 3.3.4 does (BOOT.md, the audit fixes of
        // 2026-09-26: reactant-only names such as n-Butanol carry a gas and a liquid record; the reference takes the
        // liquid, the one written last). A name whose every record carries polynomial intervals (a condensed species
        // Thermo joins into one continuous curve, e.g. Fe2O3(cr)) shares one formula and molar mass across its
        // records, so the choice of record does not change them; only the accepted temperature range does, below.
        //
        // A name whose records mix fitted and unfitted intervals is refused (the second audit's observation 3):
        // "records[^1] applies to every multi-record name" would take the range and default temperature from
        // whether any record has fits, but the enthalpy source from the last record alone, silently ignoring a
        // given temperature when the two disagree. No committed name does this, and the reference's behaviour for
        // it is not established.
        var fittedCount = records.Count(r => r.Intervals.Count > 0);
        if (fittedCount > 0 && fittedCount < records.Count)
        {
            throw new ArgumentException($"reactant '{reactant.Name}': its records mix fitted and unfitted intervals, which this node does not resolve");
        }

        var record = records[^1];
        var hasFits = fittedCount > 0;
        var t = reactant.Temperature ?? (hasFits ? Reactant.DefaultTemperature : record.AssignedTemperature);
        double low, high;
        if (hasFits)
        {
            // The range Thermo's join-and-cut covers: the union of every record's intervals, not one record's own
            // (the audit's finding 6: a first-record-only range rejected Fe2O3(cr) at 1000 K although the joined
            // table species, which PropellantMixtures actually evaluates, covers 298.15-6000 K). Each interval's
            // own two bounds are taken as an unordered pair before the union (the second audit's observation 6):
            // Br2(cr)'s one interval is written 300 -> 265.9 K, and reading its columns separately (TLow's minimum,
            // THigh's maximum) reproduces the same inverted, empty range: cea 3.3.4 evaluates the record at
            // 298.15 K, so its own lower and upper bound are 265.9 and 300, not 300 and 265.9.
            var intervals = records.SelectMany(r => r.Intervals).ToList();
            low = intervals.Min(i => Math.Min(i.TLow, i.THigh));
            high = intervals.Max(i => Math.Max(i.TLow, i.THigh));
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

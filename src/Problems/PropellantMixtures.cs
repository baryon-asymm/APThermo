using AerospacePropellantThermodynamics.Data;
using AerospacePropellantThermodynamics.Execution;
using AerospacePropellantThermodynamics.Thermo;

namespace AerospacePropellantThermodynamics.Problems;

/// <summary>
/// The propellant front door only: turns a <see cref="Propellant"/> into the <see cref="ElementalMixture"/> it implies, with the
/// reactant-enthalpy cache and the species-function batch that evaluates a fitted record's enthalpy at its temperature.
/// </summary>
internal sealed class PropellantMixtures(SpeciesDatabase database, Engine engine)
{
    private readonly Dictionary<Propellant, double[]> _reactantEnthalpies = new(ReferenceEqualityComparer.Instance);

    /// <summary>The element moles and the enthalpy per kilogram a propellant implies, for its own ratio or the one given.</summary>
    public ElementalMixture Of(Propellant propellant, double? oxidizerToFuelRatio)
    {
        var fractions = propellant.MassFractionsFor(oxidizerToFuelRatio);
        var perKilogram = ReactantEnthalpies(propellant);
        var elements = propellant.Elements;
        var moles = new double[elements.Count];
        var enthalpy = 0.0;
        for (var k = 0; k < propellant.Resolved.Count; k++)
        {
            var r = propellant.Resolved[k];
            foreach (var (symbol, count) in r.Formula)
            {
                moles[IndexOf(elements, symbol)] += fractions[k] * count / r.MolarMass;
            }

            enthalpy += fractions[k] * perKilogram[k];
        }

        var byName = new Dictionary<string, double>(elements.Count, StringComparer.Ordinal);
        for (var i = 0; i < elements.Count; i++)
        {
            byName[elements[i]] = moles[i] * UnitFactors.MolesPerKilomole;
        }

        return ElementalMixture.Create(byName, enthalpy, propellant.Omit, propellant.Only);
    }

    /// <summary>J per kilogram of every reactant at its temperature: the record's polynomial through the engine, or the assigned enthalpy.</summary>
    private double[] ReactantEnthalpies(Propellant propellant)
    {
        if (_reactantEnthalpies.TryGetValue(propellant, out var cached))
        {
            return cached;
        }

        var resolved = propellant.Resolved;
        var perKilogram = new double[resolved.Count];
        var fitted = new List<int>();
        for (var k = 0; k < resolved.Count; k++)
        {
            var r = resolved[k];
            if (r.HasFits)
            {
                fitted.Add(k);
            }
            else
            {
                perKilogram[k] = r.AssignedEnthalpy * UnitFactors.MolesPerKilomole / r.MolarMass;
            }
        }

        if (fitted.Count > 0)
        {
            var names = fitted.Select(k => resolved[k].Record!.Name).Distinct(StringComparer.Ordinal).ToList();
            var elements = fitted.SelectMany(k => resolved[k].Formula.Select(pair => pair.Symbol)).Distinct(StringComparer.Ordinal).ToList();
            var table = SpeciesTable.Build(database, elements, names);
            using var tables = engine.Upload(table);
            var batch = new SpeciesFunctionBatch(fitted.Count);
            for (var i = 0; i < fitted.Count; i++)
            {
                var r = resolved[fitted[i]];
                // The piece of a cut record covering the reactant's temperature: Thermo's own answer (SpeciesTable.PieceOf,
                // API.md, Range questions), not this node's re-derivation of the interval layout (the architecture review's F-AR-01).
                batch.Species[i] = table.PieceOf(r.Record!.Name, r.Temperature);
                batch.Temperature[i] = r.Temperature;
            }

            var functions = engine.Run(tables, batch);
            for (var i = 0; i < fitted.Count; i++)
            {
                var r = resolved[fitted[i]];
                perKilogram[fitted[i]] = functions.HOverRT[i] * PhysicalConstants.R * r.Temperature / r.MolarMass;
            }
        }

        _reactantEnthalpies[propellant] = perKilogram;
        return perKilogram;
    }

    private static int IndexOf(IReadOnlyList<string> elements, string symbol)
    {
        for (var i = 0; i < elements.Count; i++)
        {
            if (string.Equals(elements[i], symbol, StringComparison.Ordinal))
            {
                return i;
            }
        }

        throw new InvalidOperationException($"element {symbol} is not in the propellant's element list");
    }
}

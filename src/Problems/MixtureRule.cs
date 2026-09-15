namespace AerospacePropellantThermodynamics.Problems;

/// <summary>
/// The role composition and the ratio guard (its one owner, F-PR-07): validates the resolved reactants' roles against an
/// oxidizer-to-fuel ratio, or against each other when there is none, and returns the <see cref="MixtureSpecification"/> they
/// imply; and the kilogram split of a mixture's reactants, moved off <see cref="Propellant"/>, which stays a definition record.
/// </summary>
internal static class MixtureRule
{
    public static MixtureSpecification Validate(IReadOnlyList<ResolvedReactant> oxidizers, IReadOnlyList<ResolvedReactant> fuels, IReadOnlyList<ResolvedReactant> named, double? ratio)
    {
        if (ratio is { } value)
        {
            if (!(value > 0.0) || double.IsInfinity(value))
            {
                throw new ArgumentException($"the oxidizer-to-fuel ratio must be positive and finite, not {value}");
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

            return new MixtureSpecification.OxidizerToFuel(value);
        }

        if (oxidizers.Count > 0 && fuels.Count > 0)
        {
            throw new ArgumentException("oxidizers and fuels were given without an oxidizer-to-fuel ratio; set the ratio, or name every reactant with a total mass fraction");
        }

        if (!(oxidizers.Concat(fuels).Concat(named).Sum(r => r.Mass) > 0.0))
        {
            throw new ArgumentException("the reactants have zero total mass");
        }

        return new MixtureSpecification.MassFractions();
    }

    /// <summary>The mass fraction of every reactant in one kilogram, for the mixture's ratio or the one given (BOOT.md, amounts).</summary>
    public static double[] MassFractionsOf(IReadOnlyList<ResolvedReactant> resolved, MixtureSpecification mixture, double? oxidizerToFuelRatio)
    {
        var count = resolved.Count;
        var fractions = new double[count];
        var ratio = oxidizerToFuelRatio ?? (mixture is MixtureSpecification.OxidizerToFuel own ? own.Ratio : null);
        if (ratio is null)
        {
            var total = resolved.Sum(r => r.Mass);
            for (var k = 0; k < count; k++)
            {
                fractions[k] = resolved[k].Mass / total;
            }

            return fractions;
        }

        if (mixture is MixtureSpecification.MassFractions)
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
        var oxidizerMass = resolved.Where(r => r.Reactant.Role == ReactantRole.Oxidizer).Sum(r => r.Mass);
        var fuelMass = resolved.Where(r => r.Reactant.Role == ReactantRole.Fuel).Sum(r => r.Mass);
        for (var k = 0; k < count; k++)
        {
            var r = resolved[k];
            fractions[k] = r.Reactant.Role == ReactantRole.Oxidizer ? oxidizerShare * r.Mass / oxidizerMass : fuelShare * r.Mass / fuelMass;
        }

        return fractions;
    }
}

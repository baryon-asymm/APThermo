namespace APThermo.Problems;

/// <summary>
/// The role composition and the ratio guard (its one owner, F-PR-07): validates the resolved reactants' roles against an
/// oxidizer-to-fuel ratio, or against each other when there is none, and returns the <see cref="MixtureSpecification"/> they
/// imply; and the kilogram split of a mixture's reactants, moved off <see cref="Propellant"/>, which stays a definition record.
/// </summary>
internal static class MixtureRule
{
    public static MixtureSpecification Validate(IReadOnlyList<ResolvedReactant> oxidizers, IReadOnlyList<ResolvedReactant> fuels, IReadOnlyList<ResolvedReactant> named, double? ratio)
    {
        ValidateFiniteMass(oxidizers, "oxidizer");
        ValidateFiniteMass(fuels, "fuel");
        ValidateFiniteMass(named, "named");
        ValidateOneAmountKindPerGroup(oxidizers, "oxidizer");
        ValidateOneAmountKindPerGroup(fuels, "fuel");
        ValidateOneAmountKindPerGroup(named, "named");
        if (ratio is null)
        {
            ValidateOneAmountKindOverall(oxidizers, fuels, named);
        }

        return ValidateRatio(oxidizers, fuels, named, ratio);
    }

    /// <summary>
    /// One role group must use one amount kind (BOOT.md, the audit fixes of 2026-09-26): a mass fraction has no unit
    /// and a mole amount becomes amount x M in g/mol, so summing the two within a group would weigh a fraction as if
    /// it were grams.
    /// </summary>
    private static void ValidateOneAmountKindPerGroup(IReadOnlyList<ResolvedReactant> group, string groupName) =>
        ValidateOneAmountKind(group, $"the {groupName} group");

    /// <summary>
    /// Without a ratio the whole propellant is the unit of normalization, one kilogram over every group (BOOT.md,
    /// the second audit's fix F2): a fuel group in mass fractions beside a named group in moles both pass the
    /// per-group check above and are then pooled by <see cref="MassFractionsOf"/>, weighing a fraction as if it
    /// were grams the same way the per-group rule of 2026-09-26 was written to stop.
    /// </summary>
    private static void ValidateOneAmountKindOverall(IReadOnlyList<ResolvedReactant> oxidizers, IReadOnlyList<ResolvedReactant> fuels, IReadOnlyList<ResolvedReactant> named) =>
        ValidateOneAmountKind([.. oxidizers, .. fuels, .. named], "the propellant, with no ratio to split it by group,");

    private static void ValidateOneAmountKind(IReadOnlyList<ResolvedReactant> reactants, string subject)
    {
        if (reactants.Select(r => r.Reactant.AmountKind).Distinct().Count() > 1)
        {
            var names = string.Join(", ", reactants.Select(r => $"'{r.Reactant.Name}'"));
            throw new ArgumentException($"{subject} mixes mass-fraction and mole amounts ({names}); one unit of normalization must use one amount kind");
        }
    }

    /// <summary>
    /// A group's amounts sum to a finite mass (BOOT.md, the second audit's observation 7): an overflowing sum (two
    /// reactants of 1e308) would otherwise reach the ratio split as if the group weighed zero or infinity, refused
    /// later with a reason that names the wrong subject.
    /// </summary>
    private static void ValidateFiniteMass(IReadOnlyList<ResolvedReactant> group, string groupName)
    {
        var mass = group.Sum(r => r.Mass);
        if (!double.IsFinite(mass))
        {
            throw new ArgumentException($"the {groupName} group's amounts sum to a non-finite mass ({mass})");
        }
    }

    private static MixtureSpecification ValidateRatio(IReadOnlyList<ResolvedReactant> oxidizers, IReadOnlyList<ResolvedReactant> fuels, IReadOnlyList<ResolvedReactant> named, double? ratio) =>
        ratio is { } value
            ? !(value > 0.0) || double.IsInfinity(value)
                ? throw new ArgumentException($"the oxidizer-to-fuel ratio must be positive and finite, not {value}")
                : oxidizers.Count == 0 || fuels.Count == 0
                    ? throw new ArgumentException("an oxidizer-to-fuel ratio needs at least one oxidizer and one fuel")
                    : named.Count > 0
                        ? throw new ArgumentException($"reactant '{named[0].Reactant.Name}' is named with a total mass fraction, which cannot be combined with an oxidizer-to-fuel ratio")
                        : !(oxidizers.Sum(r => r.Mass) > 0.0)
                            ? throw new ArgumentException("the oxidizer group has zero mass")
                            : !(fuels.Sum(r => r.Mass) > 0.0)
                                ? throw new ArgumentException("the fuel group has zero mass")
                                : new MixtureSpecification.OxidizerToFuel(value)
            : oxidizers.Count > 0 && fuels.Count > 0
                ? throw new ArgumentException("oxidizers and fuels were given without an oxidizer-to-fuel ratio; set the ratio, or name every reactant with a total mass fraction")
                : !(oxidizers.Concat(fuels).Concat(named).Sum(r => r.Mass) > 0.0)
                    ? throw new ArgumentException("the reactants have zero total mass")
                    : new MixtureSpecification.MassFractions();

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

namespace AerospacePropellantThermodynamics.Cli.Documents;

/// <summary>A propellant given by its reactants; <see cref="OxidizerToFuel"/> is the mixture rule's ratio, null for the total-mass-fractions rule.</summary>
internal sealed record ReactantPropellant(IReadOnlyList<ReactantDocument> Reactants, double? OxidizerToFuel, IReadOnlyList<string> Omit, IReadOnlyList<string>? Only)
    : PropellantDocument(Omit, Only);

namespace AerospacePropellantThermodynamics.Cli;

/// <summary>A propellant given directly by its element moles per kilogram and, optionally, its enthalpy per kilogram.</summary>
internal sealed record ElementalPropellant(IReadOnlyDictionary<string, double> ElementMoles, double? Enthalpy, IReadOnlyList<string> Omit, IReadOnlyList<string>? Only)
    : PropellantDocument(Omit, Only);

namespace AerospacePropellantThermodynamics.Cli;

/// <summary>A propellant document: by reactants (<see cref="ReactantPropellant"/>) or by element moles (<see cref="ElementalPropellant"/>).</summary>
internal abstract record PropellantDocument(IReadOnlyList<string> Omit, IReadOnlyList<string>? Only);

using APThermo.Problems;

namespace APThermo.Cli.Documents;

/// <summary>
/// One reactant of a propellant document. <see cref="Custom"/> carries the front door's own shape for a custom
/// reactant's formula, enthalpy and molar mass, so this type need not repeat those fields on its own.
/// </summary>
internal sealed record ReactantDocument(string Name, ReactantRole Role, double Amount, AmountKind AmountKind)
{
    public double? Temperature { get; init; }

    public CustomReactantDefinition? Custom { get; init; }
}

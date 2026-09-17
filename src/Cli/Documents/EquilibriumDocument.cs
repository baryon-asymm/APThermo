using ProblemKind = APThermo.Equilibrium.ProblemKind;

namespace APThermo.Cli.Documents;

/// <summary>An equilibrium problem document: assigned pressure with the temperature, the enthalpy or the entropy given.</summary>
internal sealed record EquilibriumDocument(ProblemKind Kind, double Pressure, double? Temperature, double? Enthalpy, double? Entropy, bool Transport) : ProblemDocument;

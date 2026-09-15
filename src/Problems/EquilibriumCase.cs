namespace AerospacePropellantThermodynamics.Problems;

/// <summary>One equilibrium case as the runner sees it: its mixture, problem and the propellant it came from, if any.</summary>
internal sealed record EquilibriumCase(ElementalMixture Mixture, EquilibriumProblem Problem, Propellant? Propellant);

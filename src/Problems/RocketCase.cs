namespace APThermo.Problems;

/// <summary>One rocket case as the runner sees it: its mixture, problem, and the propellant and ratio it came from, if any.</summary>
internal sealed record RocketCase(ElementalMixture Mixture, RocketProblem Problem, Propellant? Propellant, double? Ratio);

using APThermo.Data;
using APThermo.Equilibrium;

namespace APThermo.Problems;

/// <summary>Every "before any kernel runs" rule of a rocket and of an equilibrium problem; the subject of a refusal is a field of the case.</summary>
internal static class ProblemValidation
{
    public static void Rocket(SpeciesDatabase database, ElementalMixture mixture, RocketProblem problem, int index)
    {
        if (mixture.Enthalpy is null)
        {
            throw new ArgumentException($"rocket problem {index}: the mixture has no enthalpy; a rocket problem needs the reactant enthalpy");
        }

        if (!(problem.ChamberPressure > 0.0) || double.IsInfinity(problem.ChamberPressure))
        {
            throw new ArgumentException($"rocket problem {index}: the chamber pressure must be positive and finite, not {problem.ChamberPressure}");
        }

        if (problem.TemperatureEstimate < 0.0 || double.IsNaN(problem.TemperatureEstimate))
        {
            throw new ArgumentException($"rocket problem {index}: the temperature estimate must not be negative");
        }

        foreach (var value in problem.PressureRatios.Concat(problem.AreaRatios))
        {
            if (!(value > 0.0) || double.IsInfinity(value))
            {
                throw new ArgumentException($"rocket problem {index}: an exit value must be positive and finite, not {value}");
            }
        }

        if (problem.Transport && database.Transport is null)
        {
            throw new ArgumentException($"rocket problem {index}: transport properties were requested, but the database was loaded without a trans.inp file");
        }
    }

    /// <summary>Validates the problem and returns its target (h, s, or 0.0 for tp, which reads the assigned temperature instead).</summary>
    public static double Equilibrium(SpeciesDatabase database, ElementalMixture mixture, EquilibriumProblem problem, int index)
    {
        if (!(problem.Pressure > 0.0) || double.IsInfinity(problem.Pressure))
        {
            throw new ArgumentException($"equilibrium problem {index}: the pressure must be positive and finite, not {problem.Pressure}");
        }

        if (problem.Temperature < 0.0 || double.IsNaN(problem.Temperature))
        {
            throw new ArgumentException($"equilibrium problem {index}: the temperature must not be negative");
        }

        if (problem.Transport && database.Transport is null)
        {
            throw new ArgumentException($"equilibrium problem {index}: transport properties were requested, but the database was loaded without a trans.inp file");
        }

        switch (problem.Kind)
        {
            case ProblemKind.AssignedTemperaturePressure:
                if (!(problem.Temperature > 0.0))
                {
                    throw new ArgumentException($"equilibrium problem {index}: an assigned-temperature problem needs a positive temperature");
                }

                return 0.0;
            case ProblemKind.AssignedEnthalpyPressure:
                var enthalpy = problem.Enthalpy ?? mixture.Enthalpy
                               ?? throw new ArgumentException($"equilibrium problem {index}: neither the problem nor the mixture gives an enthalpy");
                if (!double.IsFinite(enthalpy))
                {
                    throw new ArgumentException($"equilibrium problem {index}: the enthalpy must be finite");
                }

                return enthalpy;
            case ProblemKind.AssignedEntropyPressure:
                if (!double.IsFinite(problem.Entropy))
                {
                    throw new ArgumentException($"equilibrium problem {index}: the entropy must be finite");
                }

                return problem.Entropy;
            default:
                throw new ArgumentException($"equilibrium problem {index}: unknown problem kind {problem.Kind}");
        }
    }
}

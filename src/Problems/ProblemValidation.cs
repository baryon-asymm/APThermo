using APThermo.Data;
using APThermo.Equilibrium;

namespace APThermo.Problems;

/// <summary>
/// Every "before any kernel runs" rule of a rocket and of an equilibrium problem. The subject of a refusal follows
/// the caller's own noun, exactly as <see cref="MixtureMass.Subject"/> already distinguishes a batch over mixtures
/// from a batch of state records (BOOT.md, the audit fixes of 2026-09-26): a state record's rule breaks as a
/// <see cref="StateRecordException"/> carrying the record's own index, so that a caller renaming it by file and
/// position (the command line's <c>RecordNaming</c>) needs no second translation; a batch over mixtures keeps the
/// batch-local "problem k" wording it always had.
/// </summary>
internal static class ProblemValidation
{
    private const string StateRecordNoun = "state record";

    public static void Rocket(SpeciesDatabase database, ElementalMixture mixture, RocketProblem problem, string noun, int index)
    {
        if (mixture.Enthalpy is null)
        {
            throw Refuse(noun, "rocket", index, "the mixture has no enthalpy; a rocket problem needs the reactant enthalpy");
        }

        if (!(problem.ChamberPressure > 0.0) || double.IsInfinity(problem.ChamberPressure))
        {
            throw Refuse(noun, "rocket", index, $"the chamber pressure must be positive and finite, not {problem.ChamberPressure}");
        }

        if (problem.TemperatureEstimate < 0.0 || !double.IsFinite(problem.TemperatureEstimate))
        {
            throw Refuse(noun, "rocket", index, "the temperature estimate must be finite and not negative");
        }

        foreach (var value in problem.PressureRatios.Concat(problem.AreaRatios))
        {
            if (!(value > 0.0) || double.IsInfinity(value))
            {
                throw Refuse(noun, "rocket", index, $"an exit value must be positive and finite, not {value}");
            }
        }

        if (problem.Transport && database.Transport is null)
        {
            throw Refuse(noun, "rocket", index, "transport properties were requested, but the database was loaded without a trans.inp file");
        }
    }

    /// <summary>Validates the problem and returns its target (h, s, or 0.0 for tp, which reads the assigned temperature instead).</summary>
    public static double Equilibrium(SpeciesDatabase database, ElementalMixture mixture, EquilibriumProblem problem, string noun, int index)
    {
        if (!(problem.Pressure > 0.0) || double.IsInfinity(problem.Pressure))
        {
            throw Refuse(noun, "equilibrium", index, $"the pressure must be positive and finite, not {problem.Pressure}");
        }

        if (problem.Temperature < 0.0 || !double.IsFinite(problem.Temperature))
        {
            throw Refuse(noun, "equilibrium", index, "the temperature must be finite and not negative");
        }

        if (problem.Transport && database.Transport is null)
        {
            throw Refuse(noun, "equilibrium", index, "transport properties were requested, but the database was loaded without a trans.inp file");
        }

        switch (problem.Kind)
        {
            case ProblemKind.AssignedTemperaturePressure:
                if (!(problem.Temperature > 0.0))
                {
                    throw Refuse(noun, "equilibrium", index, "an assigned-temperature problem needs a positive temperature");
                }

                return 0.0;
            case ProblemKind.AssignedEnthalpyPressure:
                var enthalpy = problem.Enthalpy ?? mixture.Enthalpy
                               ?? throw Refuse(noun, "equilibrium", index, "neither the problem nor the mixture gives an enthalpy");
                if (!double.IsFinite(enthalpy))
                {
                    throw Refuse(noun, "equilibrium", index, "the enthalpy must be finite");
                }

                return enthalpy;
            case ProblemKind.AssignedEntropyPressure:
                if (!double.IsFinite(problem.Entropy))
                {
                    throw Refuse(noun, "equilibrium", index, "the entropy must be finite");
                }

                return problem.Entropy;
            default:
                throw Refuse(noun, "equilibrium", index, $"unknown problem kind {problem.Kind}");
        }
    }

    /// <summary>
    /// A state record's rule breaks as a <see cref="StateRecordException"/> naming its own index, the subject a
    /// caller such as the command line's record naming reconstructs from <see cref="StateRecordException.Reason"/>;
    /// a batch over mixtures keeps the plain, batch-local wording (BOOT.md, the audit fixes of 2026-09-26).
    /// </summary>
    private static Exception Refuse(string noun, string problemWord, int index, string reason) =>
        noun == StateRecordNoun ? new StateRecordException(index, reason) : new ArgumentException($"{problemWord} problem {index}: {reason}");
}

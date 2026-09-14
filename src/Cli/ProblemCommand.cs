using AerospacePropellantThermodynamics.Execution;
using AerospacePropellantThermodynamics.Problems;

namespace AerospacePropellantThermodynamics.Cli;

/// <summary>The rocket and equilibrium commands: read the document, check its problem type, build the mixtures, expand the sweep, solve, write.</summary>
internal static class ProblemCommand
{
    public static ExitCode Execute(Invocation invocation, TextWriter output)
    {
        var path = invocation.Arguments[0];
        var document = InputDocuments.ReadProblem(InputFile.ReadAllText(path), path);
        CheckProblemType(document.Problem, invocation.Command, path);
        var options = invocation.Options;
        var accelerator = options.Accelerator ?? document.Accelerator ?? AcceleratorKind.Auto;
        using var session = SolverSession.Open(options.Database, accelerator);
        var combinations = Sweeps.Expand(document.Sweep);
        var (mixtures, ownRatio) = BuildMixtures(session.Solver, document.Propellant, combinations, options.MassTolerance);
        var cases = SolveCases(session.Solver, document, mixtures, combinations, ownRatio, path);
        var run = session.Stop(invocation.Command, [path], new RunLimits(options.Threshold, options.MassTolerance));
        return DocumentWriter.Write(run, cases, options, output);
    }

    private static IReadOnlyList<CaseOutput> SolveCases(Solver solver, InputDocument document, IReadOnlyList<ElementalMixture> mixtures,
        IReadOnlyList<Combination> combinations, double? ownRatio, string path)
    {
        try
        {
            return document.Problem switch
            {
                RocketDocument rocket => RocketCases.Build(solver, mixtures, combinations, rocket, ownRatio),
                EquilibriumDocument equilibrium => EquilibriumCases.Build(solver, mixtures, combinations, equilibrium, ownRatio),
                var other => throw new InvalidOperationException($"unknown problem document {other.GetType().Name}"),
            };
        }
        catch (MixtureMassException e) when (document.Propellant is ElementalPropellant)
        {
            // The library names the mixture by its index in the batch; the document has one, at a JSON path.
            throw new InputException($"{path}: $.propellant.elementMoles: {e.Reason}");
        }
        catch (Exception e) when (e is ArgumentException or KeyNotFoundException)
        {
            // Every other library refusal at solve time (F-CL-13): an unknown element, a missing enthalpy, and the like.
            throw new InputException(e.Message);
        }
    }

    private static (IReadOnlyList<ElementalMixture> Mixtures, double? OwnRatio) BuildMixtures(
        Solver solver, PropellantDocument propellant, IReadOnlyList<Combination> combinations, double massTolerance)
    {
        try
        {
            if (propellant is ReactantPropellant reactants)
            {
                var built = Propellants.Build(solver.Database, reactants);
                var mixtures = combinations.Select(c => solver.MixtureOf(built, c.OxidizerToFuel)).ToList();
                return (mixtures, built.OxidizerToFuelRatio);
            }

            var mixture = Propellants.Build((ElementalPropellant)propellant, massTolerance);
            return (combinations.Select(_ => mixture).ToList(), null);
        }
        catch (Exception e) when (e is ArgumentException or KeyNotFoundException)
        {
            // The library's refusals (an unknown reactant, a temperature out of range, and the like), translated where it is called (F-CL-13).
            throw new InputException(e.Message);
        }
    }

    private static void CheckProblemType(ProblemDocument problem, string command, string path)
    {
        var isRocket = problem is RocketDocument;
        if (isRocket != (command == "rocket"))
        {
            var type = isRocket ? "rocket" : "equilibrium";
            throw new InputException($"{path}: the problem type is '{type}'; run apthermo {type}");
        }
    }
}

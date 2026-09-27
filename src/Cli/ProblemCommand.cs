using APThermo.Cli.Cases;
using APThermo.Cli.Documents;
using APThermo.Cli.Output;
using APThermo.Cli.Syntax;
using APThermo.Execution;
using APThermo.Problems;

namespace APThermo.Cli;

/// <summary>The rocket and equilibrium commands: read the document, check its problem type, build the mixtures, expand the sweep, solve, write.</summary>
internal static class ProblemCommand
{
    public static ExitCode Execute(Invocation invocation, TextWriter output)
    {
        var path = invocation.Arguments[0];
        var document = ProblemDocumentReader.Read(InputFile.ReadAllText(path), path);
        CheckProblemType(document.Problem, invocation.Command, path);
        var options = invocation.Options;
        CheckMassToleranceApplies(document.Propellant, options);
        var accelerator = options.Accelerator ?? document.Accelerator ?? AcceleratorKind.Auto;
        using var session = SolverSession.Open(options.Database, accelerator);
        var combinations = Sweeps.Expand(document.Sweep);
        var (mixtures, ownRatio) = BuildMixtures(session.Solver, document.Propellant, combinations, options.MassTolerance);
        var cases = SolveCases(session.Solver, document, mixtures, combinations, ownRatio, path);
        var massTolerance = document.Propellant is ReactantPropellant ? ElementalMixture.DefaultMassTolerance : options.MassTolerance;
        var run = session.Stop(invocation.Command, [path], new RunLimits(options.Threshold, massTolerance));
        return DocumentWriter.Write(run, cases, options, output);
    }

    /// <summary>
    /// <c>--mass-tolerance</c> applies to a document that carries element moles; a reactant propellant's mixture is
    /// always held to the library's own default, so the option does not apply to it (2026-09-26, Problems BOOT.md,
    /// the audit fixes: "Mass tolerance").
    /// </summary>
    private static void CheckMassToleranceApplies(PropellantDocument propellant, CommandOptions options)
    {
        if (options.Given.Contains("mass-tolerance") && propellant is ReactantPropellant)
        {
            throw new InputException("option --mass-tolerance does not apply to a propellant given by reactants");
        }
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
        catch (MixtureMassException e) when (document.Propellant is ReactantPropellant)
        {
            // A reactant propellant's mixture is built from a single library Propellant, held at the default
            // tolerance; naming it "the propellant's mixture (case i)" matches the front door's own propellant path
            // (MixtureMass.Subject) instead of the batch-over-mixtures wording this command's own Solve call would
            // otherwise carry, since building the mixtures per combination already drops the Propellant reference
            // (2026-09-26, Problems BOOT.md, the audit fixes: "Mass tolerance").
            throw new InputException($"the propellant's mixture (case {e.Index}): {e.Reason}");
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
            return ([.. combinations.Select(_ => mixture)], null);
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

using System.Text.Json.Nodes;
using AerospacePropellantThermodynamics.Execution;
using AerospacePropellantThermodynamics.Problems;
using AerospacePropellantThermodynamics.Thermo;
using ProblemKind = AerospacePropellantThermodynamics.Equilibrium.ProblemKind;

namespace AerospacePropellantThermodynamics.Cli;

/// <summary>
/// The states command: the records split by whether they name an exit, one call over the records without exits and
/// one over the records with exits, cases placed back in input order. Step 2 of the clean-code decomposition keeps
/// this node's own record rules and the general list-of-mixtures solve; the front door's state batches follow in
/// step 3.
/// </summary>
internal static class StatesCommand
{
    public static ExitCode Execute(Invocation invocation, TextWriter output)
    {
        var files = invocation.Arguments.Select(path => (path, InputFile.ReadAllText(path))).ToList();
        var records = InputDocuments.ReadStates(files);
        var options = invocation.Options;
        using var session = SolverSession.Open(options.Database, options.Accelerator ?? AcceleratorKind.Auto);
        var cases = new CaseOutput[records.Count];
        SolveEquilibrium(session.Solver, records.Where(r => !r.IsRocket).ToList(), options, cases);
        SolveRockets(session.Solver, records.Where(r => r.IsRocket).ToList(), options, cases);
        var run = session.Stop("states", invocation.Arguments, new RunLimits(options.Threshold, options.MassTolerance));
        return DocumentWriter.Write(run, cases, options, output);
    }

    private static void SolveEquilibrium(Solver solver, IReadOnlyList<StateDocument> records, CommandOptions options, CaseOutput[] cases)
    {
        if (records.Count == 0)
        {
            return;
        }

        var mixtures = records.Select(r => RecordNaming.MixtureOf(r, options.MassTolerance)).ToList();
        var problems = records.Select(r => new EquilibriumProblem
        {
            Kind = r.Temperature is not null ? ProblemKind.AssignedTemperaturePressure
                 : r.Entropy is not null ? ProblemKind.AssignedEntropyPressure
                 : ProblemKind.AssignedEnthalpyPressure,
            Pressure = r.Pressure,
            Temperature = r.Temperature ?? 0.0,
            Enthalpy = r.Enthalpy,
            Entropy = r.Entropy ?? 0.0,
            Transport = options.Transport,
        }).ToList();
        var results = RecordNaming.Named(records, () => solver.Solve(mixtures, problems));
        for (var k = 0; k < records.Count; k++)
        {
            Place(cases, records[k], results[k].Status, results[k].Mixture, results[k].MixtureMass, results[k].Species, [results[k].State]);
        }
    }

    private static void SolveRockets(Solver solver, IReadOnlyList<StateDocument> records, CommandOptions options, CaseOutput[] cases)
    {
        if (records.Count == 0)
        {
            return;
        }

        var mixtures = records.Select(r => RecordNaming.MixtureOf(r, options.MassTolerance)).ToList();
        var problems = records.Select(r => new RocketProblem
        {
            ChamberPressure = r.Pressure,
            Flow = r.Flow,
            AreaRatios = r.AreaRatios,
            PressureRatios = r.PressureRatios,
            Transport = options.Transport,
        }).ToList();
        var results = RecordNaming.Named(records, () => solver.Solve(mixtures, problems));
        for (var k = 0; k < records.Count; k++)
        {
            Place(cases, records[k], results[k].Status, results[k].Mixture, results[k].MixtureMass, results[k].Species, results[k].Stations);
        }
    }

    private static void Place(CaseOutput[] cases, StateDocument record, CaseStatus status, ElementalMixture mixture, double mass,
        IReadOnlyList<string> species, IReadOnlyList<Station> stations)
    {
        cases[record.Index] = new CaseOutput
        {
            Index = record.Index,
            Inputs = JsonNode.Parse(record.Record.GetRawText())!,
            Status = status,
            Mixture = mixture,
            MixtureMass = mass,
            Species = species,
            Stations = stations,
        };
    }
}

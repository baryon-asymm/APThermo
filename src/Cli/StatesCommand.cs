using System.Text.Json.Nodes;
using AerospacePropellantThermodynamics.Execution;
using AerospacePropellantThermodynamics.Problems;

namespace AerospacePropellantThermodynamics.Cli;

/// <summary>
/// The states command: the records split by <see cref="StateRecord.HasExits"/>, one call to
/// <see cref="Solver.SolveStates"/> over the records without exits and one to
/// <see cref="Solver.SolveRocketStates"/> over the records with exits, cases placed back in input order. The front
/// door owns the record's own rules and its mapping to a problem (F-AR-02); this command only reads the file shape
/// (<see cref="StateRecordReader"/>) and renames a refusal (<see cref="RecordNaming"/>).
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
        var batch = new StateBatchOptions(options.Transport, MassTolerance: options.MassTolerance);
        SolveEquilibrium(session.Solver, records.Where(r => !r.Record.HasExits).ToList(), batch, cases);
        SolveRockets(session.Solver, records.Where(r => r.Record.HasExits).ToList(), batch, cases);
        var run = session.Stop("states", invocation.Arguments, new RunLimits(options.Threshold, options.MassTolerance));
        return DocumentWriter.Write(run, cases, options, output);
    }

    private static void SolveEquilibrium(
        Solver solver, IReadOnlyList<(StateRecord Record, RecordSource Source)> group, StateBatchOptions options, CaseOutput[] cases)
    {
        if (group.Count == 0)
        {
            return;
        }

        var sources = group.Select(g => g.Source).ToList();
        var results = RecordNaming.Named(sources, () => solver.SolveStates(group.Select(g => g.Record).ToList(), options));
        for (var k = 0; k < group.Count; k++)
        {
            var source = group[k].Source;
            var result = results[k];
            cases[source.Index] = new CaseOutput
            {
                Index = source.Index,
                Inputs = JsonNode.Parse(source.Raw.GetRawText())!,
                Status = result.Status,
                Mixture = result.Mixture,
                MixtureMass = result.MixtureMass,
                Species = result.Species,
                Stations = [result.State],
            };
        }
    }

    private static void SolveRockets(
        Solver solver, IReadOnlyList<(StateRecord Record, RecordSource Source)> group, StateBatchOptions options, CaseOutput[] cases)
    {
        if (group.Count == 0)
        {
            return;
        }

        var sources = group.Select(g => g.Source).ToList();
        var results = RecordNaming.Named(sources, () => solver.SolveRocketStates(group.Select(g => g.Record).ToList(), options));
        for (var k = 0; k < group.Count; k++)
        {
            var source = group[k].Source;
            var result = results[k];
            cases[source.Index] = new CaseOutput
            {
                Index = source.Index,
                Inputs = JsonNode.Parse(source.Raw.GetRawText())!,
                Status = result.Status,
                Mixture = result.Mixture,
                MixtureMass = result.MixtureMass,
                Species = result.Species,
                Stations = result.Stations,
            };
        }
    }
}

using APThermo.Data;
using APThermo.Execution;

namespace APThermo.Problems;

/// <summary>Equilibrium cases grouped by the transport flag, one batch per group, so that the transport pass runs only over the cases that asked (BOOT.md, F-PR-08).</summary>
internal sealed class EquilibriumRunner(SpeciesDatabase database, Engine engine)
{
    /// <summary>A case past the before-any-kernel rules, with what admitting it measured: the mixture's mass, which its result reports, and the target its batch row assigns (h, s, or 0 for tp).</summary>
    private readonly record struct AdmittedCase(EquilibriumCase Case, double Mass, double Target);

    public IReadOnlyList<EquilibriumResult> Solve(ChemicalSystem system, IReadOnlyList<EquilibriumCase> cases, string noun = "mixture")
    {
        if (cases.Count == 0)
        {
            throw new ArgumentException("no equilibrium problems were given");
        }

        var admitted = new AdmittedCase[cases.Count];
        for (var k = 0; k < cases.Count; k++)
        {
            var (mixture, problem, propellant) = cases[k];
            ArgumentNullException.ThrowIfNull(problem);
            var target = ProblemValidation.Equilibrium(database, mixture, problem, k);
            var mass = MixtureMass.Check(database, mixture, MixtureMass.Subject(propellant, noun, k), k);
            admitted[k] = new AdmittedCase(cases[k], mass, target);
        }

        var speciesNames = StationFactory.SpeciesNames(system.Table);
        var results = new EquilibriumResult[cases.Count];
        foreach (var transportGroup in Enumerable.Range(0, cases.Count).GroupBy(k => cases[k].Problem.Transport))
        {
            var memberIndices = transportGroup.ToList();
            var group = memberIndices.Select(k => admitted[k]).ToList();
            var groupResults = SolveGroup(system, group, transportGroup.Key, speciesNames);
            for (var m = 0; m < memberIndices.Count; m++)
            {
                results[memberIndices[m]] = groupResults[m];
            }
        }

        return results;
    }

    private EquilibriumResult[] SolveGroup(ChemicalSystem system, IReadOnlyList<AdmittedCase> group, bool wantsTransport, IReadOnlyList<string> speciesNames)
    {
        var table = system.Table;
        var batch = new EquilibriumBatch(group.Count, table.ElementCount);
        for (var m = 0; m < group.Count; m++)
        {
            var (mixture, problem, _) = group[m].Case;
            batch.Kind[m] = problem.Kind;
            batch.Pressure[m] = problem.Pressure;
            batch.Temperature[m] = problem.Temperature;
            batch.Target[m] = group[m].Target;
            Array.Copy(mixture.KilomolesPerKilogram(system.Elements), 0, batch.ElementMoles, m * table.ElementCount, table.ElementCount);
        }

        var run = engine.Run(system.Tables, batch);
        var transport = wantsTransport ? engine.Run(system.Tables, TransportBatch.FromEquilibrium(run)) : null;
        var results = new EquilibriumResult[group.Count];
        for (var m = 0; m < group.Count; m++)
        {
            var (mixture, problem, propellant) = group[m].Case;
            var (transportStatus, figures) = StationFactory.TransportOf(wantsTransport, run.Status[m], transport, m);
            var slice = new StationSlice
            {
                Table = table,
                State = run.State[m],
                Moles = run.Moles,
                Offset = (long)m * table.SpeciesCount,
                Transport = figures,
                TransportStatus = transportStatus,
                Status = run.Status[m],
            };
            var state = StationFactory.Create("state", slice);
            results[m] = new EquilibriumResult(
                propellant: propellant,
                mixture: mixture,
                mixtureMass: group[m].Mass,
                problem: problem,
                species: speciesNames,
                state: state,
                status: run.Status[m],
                accelerator: run.Accelerator);
        }

        return results;
    }
}

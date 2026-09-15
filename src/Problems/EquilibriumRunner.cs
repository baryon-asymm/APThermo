using AerospacePropellantThermodynamics.Data;
using AerospacePropellantThermodynamics.Execution;
using AerospacePropellantThermodynamics.Thermo;

namespace AerospacePropellantThermodynamics.Problems;

/// <summary>Equilibrium cases as one batch, further grouped by the transport flag so that the transport pass runs only over the cases that asked (BOOT.md, F-PR-08).</summary>
internal sealed class EquilibriumRunner(SpeciesDatabase database, Engine engine)
{
    /// <summary>What is fixed for the whole of one <see cref="Solve"/> call, built once and passed to every group instead of six parameters each.</summary>
    private readonly record struct SolveContext(
        ChemicalSystem System, SpeciesTable Table, IReadOnlyList<EquilibriumCase> Cases,
        double[] Masses, IReadOnlyList<string> SpeciesNames, EquilibriumResult[] Results);

    public IReadOnlyList<EquilibriumResult> Solve(ChemicalSystem system, IReadOnlyList<EquilibriumCase> cases, string noun = "mixture")
    {
        if (cases.Count == 0)
        {
            throw new ArgumentException("no equilibrium problems were given");
        }

        var table = system.Table;
        var masses = new double[cases.Count];
        var targets = new double[cases.Count];
        for (var k = 0; k < cases.Count; k++)
        {
            var (mixture, problem, propellant) = cases[k];
            ArgumentNullException.ThrowIfNull(problem);
            targets[k] = ProblemValidation.Equilibrium(database, mixture, problem, k);
            masses[k] = MixtureMass.Check(database, mixture, MixtureMass.Subject(propellant, noun, k), k);
        }

        var speciesNames = StationFactory.SpeciesNames(table);
        var results = new EquilibriumResult[cases.Count];
        var context = new SolveContext(system, table, cases, masses, speciesNames, results);
        foreach (var transportGroup in Enumerable.Range(0, cases.Count).GroupBy(k => cases[k].Problem.Transport))
        {
            SolveGroup(context, transportGroup.ToList(), transportGroup.Key, targets);
        }

        return results;
    }

    private void SolveGroup(SolveContext context, IReadOnlyList<int> members, bool wantsTransport, double[] targets)
    {
        var table = context.Table;
        var batch = new EquilibriumBatch(members.Count, table.ElementCount);
        for (var m = 0; m < members.Count; m++)
        {
            var (mixture, problem, _) = context.Cases[members[m]];
            batch.Kind[m] = problem.Kind;
            batch.Pressure[m] = problem.Pressure;
            batch.Temperature[m] = problem.Temperature;
            batch.Target[m] = targets[members[m]];
            Array.Copy(mixture.KilomolesPerKilogram(context.System.Elements), 0, batch.ElementMoles, m * table.ElementCount, table.ElementCount);
        }

        var run = engine.Run(context.System.Tables, batch);
        var transport = wantsTransport ? engine.Run(context.System.Tables, TransportBatch.FromEquilibrium(run)) : null;
        for (var m = 0; m < members.Count; m++)
        {
            var (mixture, problem, propellant) = context.Cases[members[m]];
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
            context.Results[members[m]] = new EquilibriumResult(
                Propellant: propellant,
                Mixture: mixture,
                MixtureMass: context.Masses[members[m]],
                Problem: problem,
                Species: context.SpeciesNames,
                State: state,
                Status: run.Status[m],
                Accelerator: run.Accelerator);
        }
    }
}

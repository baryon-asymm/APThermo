using AerospacePropellantThermodynamics.Data;
using AerospacePropellantThermodynamics.Execution;
using AerospacePropellantThermodynamics.Thermo;
using AerospacePropellantThermodynamics.Transport;

namespace AerospacePropellantThermodynamics.Problems;

/// <summary>One equilibrium case as the runner sees it: its mixture, problem and the propellant it came from, if any.</summary>
internal sealed record EquilibriumCase(ElementalMixture Mixture, EquilibriumProblem Problem, Propellant? Propellant);

/// <summary>Equilibrium cases as one batch, further grouped by the transport flag so that the transport pass runs only over the cases that asked (BOOT.md, F-PR-08).</summary>
internal sealed class EquilibriumRunner(SpeciesDatabase database, Engine engine)
{
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
            masses[k] = MixtureMass.Check(database, mixture, propellant is null ? $"{noun} {k}" : $"the propellant's mixture (case {k})", k);
        }

        var speciesNames = StationFactory.SpeciesNames(table);
        var results = new EquilibriumResult[cases.Count];
        foreach (var transportGroup in Enumerable.Range(0, cases.Count).GroupBy(k => cases[k].Problem.Transport))
        {
            SolveGroup(system, table, cases, transportGroup.ToList(), transportGroup.Key, targets, masses, speciesNames, results);
        }

        return results;
    }

    private void SolveGroup(ChemicalSystem system, SpeciesTable table, IReadOnlyList<EquilibriumCase> cases, IReadOnlyList<int> members, bool wantsTransport,
                            double[] targets, double[] masses, IReadOnlyList<string> speciesNames, EquilibriumResult[] results)
    {
        var batch = new EquilibriumBatch(members.Count, table.ElementCount);
        for (var m = 0; m < members.Count; m++)
        {
            var (mixture, problem, _) = cases[members[m]];
            batch.Kind[m] = problem.Kind;
            batch.Pressure[m] = problem.Pressure;
            batch.Temperature[m] = problem.Temperature;
            batch.Target[m] = targets[members[m]];
            Array.Copy(mixture.KilomolesPerKilogram(system.Elements), 0, batch.ElementMoles, m * table.ElementCount, table.ElementCount);
        }

        var run = engine.Run(system.Tables, batch);
        var transport = wantsTransport ? engine.Run(system.Tables, TransportBatch.FromEquilibrium(run)) : null;
        for (var m = 0; m < members.Count; m++)
        {
            var (mixture, problem, propellant) = cases[members[m]];
            var wantStationTransport = wantsTransport && run.Status[m] == CaseStatus.Ok;
            var transportStatus = wantStationTransport ? transport!.Status[m] : (CaseStatus?)null;
            var figures = transportStatus == CaseStatus.Ok ? transport!.Figures[m] : (TransportFigures?)null;
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
            results[members[m]] = new EquilibriumResult(propellant, mixture, masses[members[m]], problem, speciesNames, state, run.Status[m], run.Accelerator);
        }
    }
}

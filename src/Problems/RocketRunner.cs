using AerospacePropellantThermodynamics.Data;
using AerospacePropellantThermodynamics.Execution;
using AerospacePropellantThermodynamics.Performance;
using AerospacePropellantThermodynamics.Thermo;

namespace AerospacePropellantThermodynamics.Problems;

/// <summary>One rocket case as the runner sees it: its mixture, problem, and the propellant and ratio it came from, if any.</summary>
internal sealed record RocketCase(ElementalMixture Mixture, RocketProblem Problem, Propellant? Propellant, double? Ratio);

/// <summary>
/// Rocket cases grouped by exit layout (pressure- and area-ratio counts) and, within a layout, by the transport flag, so
/// that the transport pass runs only over the cases that asked for it (BOOT.md, F-PR-08).
/// </summary>
internal sealed class RocketRunner(SpeciesDatabase database, Engine engine)
{
    /// <summary>What is fixed for the whole of one <see cref="Solve"/> call, built once and passed to every group instead of six parameters each.</summary>
    private readonly record struct SolveContext(
        ChemicalSystem System, SpeciesTable Table, IReadOnlyList<RocketCase> Cases,
        double[] Masses, IReadOnlyList<string> SpeciesNames, RocketResult[] Results);


    public IReadOnlyList<RocketResult> Solve(ChemicalSystem system, IReadOnlyList<RocketCase> cases, string noun = "mixture")
    {
        if (cases.Count == 0)
        {
            throw new ArgumentException("no rocket problems were given");
        }

        var groups = new Dictionary<(int Pressures, int Areas), List<int>>();
        var order = new List<(int Pressures, int Areas)>();
        var masses = new double[cases.Count];
        for (var k = 0; k < cases.Count; k++)
        {
            var (mixture, problem, propellant, _) = cases[k];
            ArgumentNullException.ThrowIfNull(problem);
            ProblemValidation.Rocket(database, mixture, problem, k);
            masses[k] = MixtureMass.Check(database, mixture, MixtureMass.Subject(propellant, noun, k), k);
            var key = (problem.PressureRatios.Count, problem.AreaRatios.Count);
            if (!groups.TryGetValue(key, out var members))
            {
                groups[key] = members = [];
                order.Add(key);
            }

            members.Add(k);
        }

        var table = system.Table;
        var speciesNames = StationFactory.SpeciesNames(table);
        var results = new RocketResult[cases.Count];
        var context = new SolveContext(system, table, cases, masses, speciesNames, results);
        foreach (var key in order)
        {
            var kinds = Enumerable.Repeat(ExitSpecification.PressureRatio, key.Pressures).Concat(Enumerable.Repeat(ExitSpecification.AreaRatio, key.Areas)).ToArray();
            foreach (var transportGroup in groups[key].GroupBy(k => cases[k].Problem.Transport))
            {
                SolveGroup(context, transportGroup.ToList(), transportGroup.Key, kinds);
            }
        }

        return results;
    }

    private void SolveGroup(SolveContext context, IReadOnlyList<int> members, bool wantsTransport, ExitSpecification[] kinds)
    {
        var table = context.Table;
        var batch = new RocketBatch(members.Count, table.ElementCount, kinds);
        for (var m = 0; m < members.Count; m++)
        {
            var (mixture, problem, _, _) = context.Cases[members[m]];
            batch.ChamberPressure[m] = problem.ChamberPressure;
            batch.ReactantEnthalpy[m] = mixture.Enthalpy!.Value;
            batch.TemperatureEstimate[m] = problem.TemperatureEstimate;
            batch.Flow[m] = problem.Flow;
            Array.Copy(mixture.KilomolesPerKilogram(context.System.Elements), 0, batch.ElementMoles, m * table.ElementCount, table.ElementCount);
            var exits = problem.PressureRatios.Concat(problem.AreaRatios).ToArray();
            Array.Copy(exits, 0, batch.ExitValues, m * batch.Exits, batch.Exits);
        }

        var run = engine.Run(context.System.Tables, batch);
        var transport = wantsTransport ? engine.Run(context.System.Tables, TransportBatch.FromRocket(run)) : null;
        var stationCount = run.StationCount;
        for (var m = 0; m < members.Count; m++)
        {
            var (mixture, problem, propellant, ratio) = context.Cases[members[m]];
            var stations = new Station[stationCount];
            for (var s = 0; s < stationCount; s++)
            {
                var index = m * stationCount + s;
                var (transportStatus, figures) = StationFactory.TransportOf(wantsTransport, run.StationStatus[index], transport, index);
                var slice = new StationSlice
                {
                    Table = table,
                    State = run.Stations[index],
                    Performance = run.Figures[index],
                    Moles = run.Moles,
                    Offset = (long)index * table.SpeciesCount,
                    Transport = figures,
                    TransportStatus = transportStatus,
                    Status = run.StationStatus[index],
                };
                stations[s] = StationFactory.Create(StationFactory.NameOf(s), slice);
            }

            context.Results[members[m]] = new RocketResult(
                Propellant: propellant,
                Mixture: mixture,
                MixtureMass: context.Masses[members[m]],
                Problem: problem,
                OxidizerToFuelRatio: ratio,
                Species: context.SpeciesNames,
                Stations: stations,
                Status: run.Status[m],
                Accelerator: run.Accelerator);
        }
    }
}

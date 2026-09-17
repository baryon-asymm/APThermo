using APThermo.Data;
using APThermo.Execution;
using APThermo.Performance;
using APThermo.Thermo;

namespace APThermo.Problems;

/// <summary>
/// Rocket cases grouped by exit layout (pressure- and area-ratio counts) and, within a layout, by the transport flag, so
/// that the transport pass runs only over the cases that asked for it (BOOT.md, F-PR-08).
/// </summary>
internal sealed class RocketRunner(SpeciesDatabase database, Engine engine)
{
    /// <summary>A case past the before-any-kernel rules, with what admitting it measured: the mixture's mass, which its result reports.</summary>
    private readonly record struct AdmittedCase(RocketCase Case, double Mass);

    public IReadOnlyList<RocketResult> Solve(ChemicalSystem system, IReadOnlyList<RocketCase> cases, string noun = "mixture")
    {
        if (cases.Count == 0)
        {
            throw new ArgumentException("no rocket problems were given");
        }

        var admitted = new AdmittedCase[cases.Count];
        var groups = new Dictionary<(int Pressures, int Areas), List<int>>();
        var order = new List<(int Pressures, int Areas)>();
        for (var k = 0; k < cases.Count; k++)
        {
            var (mixture, problem, propellant, _) = cases[k];
            ArgumentNullException.ThrowIfNull(problem);
            ProblemValidation.Rocket(database, mixture, problem, k);
            var mass = MixtureMass.Check(database, mixture, MixtureMass.Subject(propellant, noun, k), k);
            admitted[k] = new AdmittedCase(cases[k], mass);
            var key = (problem.PressureRatios.Count, problem.AreaRatios.Count);
            if (!groups.TryGetValue(key, out var members))
            {
                groups[key] = members = [];
                order.Add(key);
            }

            members.Add(k);
        }

        var speciesNames = StationFactory.SpeciesNames(system.Table);
        var results = new RocketResult[cases.Count];
        foreach (var key in order)
        {
            var kinds = Enumerable.Repeat(ExitSpecification.PressureRatio, key.Pressures).Concat(Enumerable.Repeat(ExitSpecification.AreaRatio, key.Areas)).ToArray();
            foreach (var transportGroup in groups[key].GroupBy(k => cases[k].Problem.Transport))
            {
                var memberIndices = transportGroup.ToList();
                var group = memberIndices.Select(k => admitted[k]).ToList();
                var groupResults = SolveGroup(system, group, transportGroup.Key, kinds, speciesNames);
                for (var m = 0; m < memberIndices.Count; m++)
                {
                    results[memberIndices[m]] = groupResults[m];
                }
            }
        }

        return results;
    }

    private RocketResult[] SolveGroup(ChemicalSystem system, IReadOnlyList<AdmittedCase> group, bool wantsTransport, ExitSpecification[] kinds, IReadOnlyList<string> speciesNames)
    {
        var table = system.Table;
        var batch = new RocketBatch(group.Count, table.ElementCount, kinds);
        for (var m = 0; m < group.Count; m++)
        {
            var (mixture, problem, _, _) = group[m].Case;
            batch.ChamberPressure[m] = problem.ChamberPressure;
            batch.ReactantEnthalpy[m] = mixture.Enthalpy!.Value;
            batch.TemperatureEstimate[m] = problem.TemperatureEstimate;
            batch.Flow[m] = problem.Flow;
            Array.Copy(mixture.KilomolesPerKilogram(system.Elements), 0, batch.ElementMoles, m * table.ElementCount, table.ElementCount);
            var exits = problem.PressureRatios.Concat(problem.AreaRatios).ToArray();
            Array.Copy(exits, 0, batch.ExitValues, m * batch.Exits, batch.Exits);
        }

        var run = engine.Run(system.Tables, batch);
        var transport = wantsTransport ? engine.Run(system.Tables, TransportBatch.FromRocket(run)) : null;
        var stationCount = run.StationCount;
        var results = new RocketResult[group.Count];
        for (var m = 0; m < group.Count; m++)
        {
            var (mixture, problem, propellant, ratio) = group[m].Case;
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

            results[m] = new RocketResult(
                propellant: propellant,
                mixture: mixture,
                mixtureMass: group[m].Mass,
                problem: problem,
                oxidizerToFuelRatio: ratio,
                species: speciesNames,
                stations: stations,
                status: run.Status[m],
                accelerator: run.Accelerator);
        }

        return results;
    }
}

using AerospacePropellantThermodynamics.Data;
using AerospacePropellantThermodynamics.Execution;
using AerospacePropellantThermodynamics.Performance;
using AerospacePropellantThermodynamics.Thermo;
using AerospacePropellantThermodynamics.Transport;

namespace AerospacePropellantThermodynamics.Problems;

/// <summary>One rocket case as the runner sees it: its mixture, problem, and the propellant and ratio it came from, if any.</summary>
internal sealed record RocketCase(ElementalMixture Mixture, RocketProblem Problem, Propellant? Propellant, double? Ratio);

/// <summary>
/// Rocket cases grouped by exit layout (pressure- and area-ratio counts) and, within a layout, by the transport flag, so
/// that the transport pass runs only over the cases that asked for it (BOOT.md, F-PR-08).
/// </summary>
internal sealed class RocketRunner(SpeciesDatabase database, Engine engine)
{
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
            masses[k] = MixtureMass.Check(database, mixture, Subject(propellant, noun, k), k);
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
        foreach (var key in order)
        {
            var kinds = Enumerable.Repeat(ExitSpecification.PressureRatio, key.Pressures).Concat(Enumerable.Repeat(ExitSpecification.AreaRatio, key.Areas)).ToArray();
            foreach (var transportGroup in groups[key].GroupBy(k => cases[k].Problem.Transport))
            {
                SolveGroup(system, table, cases, transportGroup.ToList(), transportGroup.Key, kinds, masses, speciesNames, results);
            }
        }

        return results;
    }

    private void SolveGroup(ChemicalSystem system, SpeciesTable table, IReadOnlyList<RocketCase> cases, IReadOnlyList<int> members, bool wantsTransport,
                            ExitSpecification[] kinds, double[] masses, IReadOnlyList<string> speciesNames, RocketResult[] results)
    {
        var batch = new RocketBatch(members.Count, table.ElementCount, kinds);
        for (var m = 0; m < members.Count; m++)
        {
            var (mixture, problem, _, _) = cases[members[m]];
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
        for (var m = 0; m < members.Count; m++)
        {
            var (mixture, problem, propellant, ratio) = cases[members[m]];
            var stations = new Station[stationCount];
            for (var s = 0; s < stationCount; s++)
            {
                var index = m * stationCount + s;
                var wantStationTransport = wantsTransport && run.StationStatus[index] == CaseStatus.Ok;
                var transportStatus = wantStationTransport ? transport!.Status[index] : (CaseStatus?)null;
                var figures = transportStatus == CaseStatus.Ok ? transport!.Figures[index] : (TransportFigures?)null;
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

            results[members[m]] = new RocketResult(propellant, mixture, masses[members[m]], problem, ratio, speciesNames, stations, run.Status[m], run.Accelerator);
        }
    }

    private static string Subject(Propellant? propellant, string noun, int index) =>
        propellant is null ? $"{noun} {index}" : $"the propellant's mixture (case {index})";
}

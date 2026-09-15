using AerospacePropellantThermodynamics.Cli.Documents;
using AerospacePropellantThermodynamics.Problems;

namespace AerospacePropellantThermodynamics.Cli.Cases;

/// <summary>Combinations and a rocket problem document into problems, and results into case outputs.</summary>
internal static class RocketCases
{
    public static IReadOnlyList<CaseOutput> Build(Solver solver, IReadOnlyList<ElementalMixture> mixtures, IReadOnlyList<Combination> combinations,
                                                   RocketDocument document, double? ownRatio)
    {
        var problems = combinations.Select(c => new RocketProblem
        {
            ChamberPressure = c.ChamberPressure ?? document.ChamberPressure,
            Flow = document.Flow,
            AreaRatios = document.AreaRatios,
            PressureRatios = document.PressureRatios,
            Transport = document.Transport,
            TemperatureEstimate = document.TemperatureEstimate,
        }).ToList();
        var results = solver.Solve(mixtures, problems);
        var cases = new List<CaseOutput>(results.Count);
        for (var i = 0; i < results.Count; i++)
        {
            var inputs = CaseInputs.Rocket(combinations[i].OxidizerToFuel ?? ownRatio, problems[i]);
            cases.Add(new CaseOutput
            {
                Index = i,
                Inputs = inputs,
                Status = results[i].Status,
                Mixture = results[i].Mixture,
                MixtureMass = results[i].MixtureMass,
                Species = results[i].Species,
                Stations = results[i].Stations,
            });
        }

        return cases;
    }
}

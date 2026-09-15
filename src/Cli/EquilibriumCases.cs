using AerospacePropellantThermodynamics.Problems;

namespace AerospacePropellantThermodynamics.Cli;

/// <summary>Combinations and an equilibrium problem document into problems, and results into case outputs.</summary>
internal static class EquilibriumCases
{
    public static IReadOnlyList<CaseOutput> Build(Solver solver, IReadOnlyList<ElementalMixture> mixtures, IReadOnlyList<Combination> combinations,
                                                    EquilibriumDocument document, double? ownRatio)
    {
        var problems = combinations.Select(c => new EquilibriumProblem
        {
            Kind = document.Kind,
            Pressure = c.Pressure ?? document.Pressure,
            Temperature = c.Temperature ?? document.Temperature ?? 0.0,
            Enthalpy = document.Enthalpy,
            Entropy = document.Entropy ?? 0.0,
            Transport = document.Transport,
        }).ToList();
        var results = solver.Solve(mixtures, problems);
        var cases = new List<CaseOutput>(results.Count);
        for (var i = 0; i < results.Count; i++)
        {
            var inputs = CaseInputs.Equilibrium(combinations[i].OxidizerToFuel ?? ownRatio, problems[i], mixtures[i]);
            cases.Add(new CaseOutput
            {
                Index = i,
                Inputs = inputs,
                Status = results[i].Status,
                Mixture = results[i].Mixture,
                MixtureMass = results[i].MixtureMass,
                Species = results[i].Species,
                Stations = [results[i].State],
            });
        }

        return cases;
    }
}

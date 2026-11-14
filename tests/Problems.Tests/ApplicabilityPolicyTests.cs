using System.Text.Json.Nodes;
using APThermo.Equilibrium;
using APThermo.Harness;
using APThermo.Thermo;
using Xunit.Abstractions;

namespace APThermo.Problems.Tests;

/// <summary>Public consumers demonstrate the disclosed phase-admission and retained-frozen policies, not material validity.</summary>
[Collection(SolverFixture.CollectionName)]
public sealed class ApplicabilityPolicyTests(ITestOutputHelper output)
{
    /// <summary>The default species set admits graphite at its bound and excludes it immediately below.</summary>
    [Fact]
    public void TheGraphiteBoundaryFollowsTheRecordedAdmissionPolicy() => CheckEquilibrium("graphite");

    /// <summary>The declared open-below ice exception remains admitted below its raw NASA fit.</summary>
    [Fact]
    public void IceRemainsAdmittedBelowItsRawFit() => CheckEquilibrium("ice");

    private void CheckEquilibrium(string name)
    {
        var item = ApplicabilityMetadata.Read()[name]!;
        var composition = item["elementMoles"]!.AsObject().ToDictionary(pair => pair.Key, pair => pair.Value!.GetValue<double>(), StringComparer.Ordinal);
        var only = item["products"] is JsonArray products ? ApplicabilityMetadata.Strings(products) : null;
        var mixture = ElementalMixture.Create(composition, only: only);
        var species = item["species"]!.GetValue<string>();
        var accepted = new List<double>();
        foreach (var sample in item["cases"]!.AsArray())
        {
            var temperature = sample!["temperature"]!.GetValue<double>();
            var result = SolverFixture.Shared.Solver.Solve(mixture, new EquilibriumProblem
            {
                Kind = ProblemKind.AssignedTemperaturePressure,
                Pressure = item["pressure"]!.GetValue<double>(),
                Temperature = temperature,
            });
            Assert.Equal(sample["expectedStatus"]!.GetValue<string>(), result.Status.ToString());
            var present = result.State.CondensedMassFractions.TryGetValue(species, out var mass) && mass > 0.0;
            Assert.Equal(sample["condensatePresent"]!.GetValue<bool>(), present);
            if (present) { accepted.Add(temperature); }
            output.WriteLine($"A37 {name}: T={temperature:R}; status={result.Status}; {species} mass={mass:R}; h={result.State.State.Enthalpy:R}");
        }

        Assert.NotEmpty(accepted);
        Assert.Equal(item["admissionLower"]!.GetValue<double>(), accepted.Min());
    }

    /// <summary>The exact archived rocket inputs retain their condensate while an Ok exit extrapolates below its raw/table fit.</summary>
    [Fact]
    public void FrozenFlowRetainsAluminaBelowItsFit()
    {
        var item = ApplicabilityMetadata.Read()["frozenAlumina"]!;
        var fixture = FixtureCases.Load("rocket", Path.GetFileNameWithoutExtension(item["inputFixture"]!.GetValue<string>()));
        var propellant = FixtureCases.PropellantOf(SolverFixture.Shared.Database, fixture);
        var problem = new RocketProblem
        {
            ChamberPressure = item["chamberPressure"]!.GetValue<double>(),
            Flow = FixtureCases.FlowOf(item["flow"]!.GetValue<string>()),
            AreaRatios = [.. item["areaRatios"]!.AsArray().Select(value => value!.GetValue<double>())],
            Transport = item["transport"]!.GetValue<bool>(),
        };
        var result = SolverFixture.Shared.Solver.Solve(propellant, problem);
        Assert.Equal(CaseStatus.Ok, result.Status);
        var species = item["species"]!.GetValue<string>();
        var initial = result.Stations[0].CondensedMassFractions[species];
        Assert.True(initial > 0.0);
        Assert.All(result.Stations, station =>
        {
            Assert.Equal(CaseStatus.Ok, station.Status);
            Assert.True(Bits.Same(initial, station.CondensedMassFractions[species]));
        });
        var exit = result.Stations[^1];
        Assert.True(exit.State.Temperature < item["rawFit"]!["low"]!.GetValue<double>());
        Assert.True(exit.State.Temperature < item["tableFit"]!["low"]!.GetValue<double>());
        output.WriteLine($"A37 frozen: {exit.Name} T={exit.State.Temperature:R}; status={exit.Status}; {species} mass={initial:R}");
    }
}

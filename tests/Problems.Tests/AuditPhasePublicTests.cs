using System.Globalization;
using System.Text.Json;
using APThermo.Data;
using APThermo.Equilibrium;
using APThermo.Execution;
using APThermo.Fixtures;
using APThermo.Thermo;
using Xunit.Sdk;
using Problem = APThermo.Problems.EquilibriumProblem;

namespace APThermo.Problems.Tests;

/// <summary>Public scientific obligations from the independent phase and feasible-objective audit witnesses.</summary>
public sealed class AuditPhasePublicTests
{
    // The existing execution acceptance bound for element closure, including trace elements.
    private const double ElementClosureBound = 1e-13;
    private static readonly SpeciesDatabase Database = SpeciesDatabase.LoadBundled();
    private static readonly JsonElement Cases = LoadCases();
    private static readonly ToleranceTable Tolerances = ToleranceTable.Load();

    /// <summary>Each unequal-potential point must choose the independently preferred condensed partner.</summary>
    [Fact]
    public void SrCl2TpStatesRespectBothSignsOfTheIndependentPhaseOracle()
    {
        var input = Cases.GetProperty("srcl2");
        using var solver = CpuSolver();
        var mixture = Mixture(input);
        foreach (var point in input.GetProperty("phaseChecks").EnumerateArray())
        {
            var temperature = point.GetProperty("temperature").GetDouble();
            var result = solver.Solve(mixture, new Problem { Kind = ProblemKind.AssignedTemperaturePressure, Pressure = Number(input, "pressure"), Temperature = temperature });
            Assert.Equal(CaseStatus.Ok, result.Status);
            Assert.Equal(temperature, result.State.State.Temperature);
            Assert.Equal(Number(input, "pressure"), result.State.State.Pressure);
            AssertElements(mixture.ElementMoles, result.State);
            Assert.True(Number(point, "alternativeMinusPresentGibbs") < 0, "the saved independent exchange must lower Gibbs");
            var stable = point.GetProperty("alternative").GetString()!;
            var unstable = point.GetProperty("presentInOldRepro").GetString()!;
            Assert.True(result.State.CondensedMassFractions[stable] > 0, $"no stable {stable} at {temperature:R} K");
            Assert.True(result.State.MoleFractions[unstable] <= Tolerances.For("moleFractionFloor").Absolute, $"improving allowed exchange from {unstable} at {temperature:R} K");
        }
    }

    /// <summary>At the fitted crossing the test constrains conservation, not an arbitrary unique phase fraction.</summary>
    [Fact]
    public void TheEqualPotentialTpPointDoesNotRequireAUniquePhaseFraction()
    {
        var input = Cases.GetProperty("srcl2");
        var temperature = Number(input, "crossingTemperature");
        using var solver = CpuSolver();
        IReadOnlyList<string>[] orders = [Products(input), [.. Products(input).Reverse()]];
        foreach (var products in orders)
        {
            var result = solver.Solve(Mixture(input, products), new Problem { Kind = ProblemKind.AssignedTemperaturePressure, Pressure = Number(input, "pressure"), Temperature = temperature });
            if (result.Status == CaseStatus.Ok)
            {
                Assert.Equal(temperature, result.State.State.Temperature);
                AssertElements(Mixture(input).ElementMoles, result.State);
                Assert.True(result.State.CondensedMassFractions.Values.Sum() > 0);
            }
            else
            {
                Assert.Contains(result.Status, new[] { CaseStatus.NotConverged, CaseStatus.SingularMatrix });
                Assert.Equal(default, result.State.State);
            }
        }
    }

    /// <summary>HP and SP must meet the assigned property and cannot lose to an independently verified feasible state.</summary>
    /// <param name="kind">The assigned property of the frozen witness.</param>
    [Theory]
    [InlineData("hp")]
    [InlineData("sp")]
    public void MultiFloorColdStatesDominateTheIndependentSameConstraintWitness(string kind)
    {
        ArgumentNullException.ThrowIfNull(kind);
        var input = Cases.GetProperty("multiFloor");
        var constraint = input.GetProperty(kind);
        var witness = constraint.GetProperty("witness");
        using var solver = CpuSolver();
        var mixture = Mixture(input);
        var problem = new Problem
        {
            Kind = kind == "hp" ? ProblemKind.AssignedEnthalpyPressure : ProblemKind.AssignedEntropyPressure,
            Pressure = Number(input, "pressure"),
            Enthalpy = kind == "hp" ? Number(constraint, "target") : null,
            Entropy = kind == "sp" ? Number(constraint, "target") : 0,
        };
        var result = solver.Solve(mixture, problem);
        Assert.Equal(CaseStatus.Ok, result.Status);
        AssertElements(mixture.ElementMoles, result.State);
        AssertWitnessElements(mixture.ElementMoles, witness);
        AssertDominatesWitness(kind, constraint, result.State.State);
        var old = constraint.GetProperty("oldReturned");
        var oldState = result.State.State with { Enthalpy = Number(old, "enthalpy"), Entropy = Number(old, "entropy") };
        var rejected = Record.Exception(() => AssertDominatesWitness(kind, constraint, oldState));
        _ = Assert.IsType<XunitException>(rejected, exactMatch: false);
        Assert.Contains("loses to independent feasible", rejected.Message, StringComparison.Ordinal);
    }

    /// <summary>An HP equilibrium cannot have lower entropy than an allowed single-partner feasible branch at the same H,p.</summary>
    [Fact]
    public void SrCl2HpAndRocketChamberHaveNoImprovingSameConstraintPhaseBranch()
    {
        var input = Cases.GetProperty("srcl2");
        using var solver = CpuSolver();
        var mixture = Mixture(input);
        var problem = new Problem { Kind = ProblemKind.AssignedEnthalpyPressure, Pressure = Number(input, "pressure"), Enthalpy = Number(input, "enthalpy") };
        var result = solver.Solve(mixture, problem);
        Assert.Equal(CaseStatus.Ok, result.Status);
        AssertHpBranchOptimality(solver, input, result.State, Number(input, "enthalpy"));
        var old = input.GetProperty("oldReturnedHp");
        var oldState = result.State with { State = result.State.State with { Enthalpy = Number(old, "enthalpy"), Entropy = Number(old, "entropy") } };
        var rejected = Record.Exception(() => AssertHpBranchOptimality(solver, input, oldState, Number(input, "enthalpy")));
        _ = Assert.IsType<XunitException>(rejected, exactMatch: false);
        Assert.Contains("loses to feasible same-H branch", rejected.Message, StringComparison.Ordinal);
        var rocket = solver.Solve(mixture, new RocketProblem
        {
            ChamberPressure = Number(input, "pressure"),
            PressureRatios = [.. input.GetProperty("pressureRatios").EnumerateArray().Select(value => value.GetDouble())],
        });
        Assert.Equal(CaseStatus.Ok, rocket.Stations[0].Status);
        AssertHpBranchOptimality(solver, input, rocket.Stations[0], Number(input, "enthalpy"));
    }

    /// <summary>An overall failed rocket must not hide a falsely successful, dominated chamber.</summary>
    [Fact]
    public void MultiFloorRocketCannotReportADominatedChamberAsOk()
    {
        var input = Cases.GetProperty("multiFloor");
        var constraint = input.GetProperty("hp");
        using var solver = CpuSolver();
        var rocket = solver.Solve(Mixture(input), new RocketProblem { ChamberPressure = Number(input, "pressure"), AreaRatios = [.. input.GetProperty("areaRatios").EnumerateArray().Select(value => value.GetDouble())] });
        // The chamber is an HP problem with the mixture's assigned enthalpy, independent of exit success.
        Assert.Equal(CaseStatus.Ok, rocket.Stations[0].Status);
        AssertDominatesWitness("hp", constraint, rocket.Stations[0].State);
        AssertElements(rocket.Mixture.ElementMoles, rocket.Stations[0]);
        if (rocket.Status == CaseStatus.Ok)
        {
            Assert.All(rocket.Stations, station => Assert.Equal(CaseStatus.Ok, station.Status));
        }
    }

    private static void AssertHpBranchOptimality(Solver solver, JsonElement input, Station full, double enthalpy)
    {
        AssertClose(enthalpy, full.State.Enthalpy, "assigned HP enthalpy");
        AssertElements(Mixture(input).ElementMoles, full);
        foreach (var excluded in new[] { "SrCL2(a)", "SrCL2(b)" })
        {
            // A constrained candidate set supplies a feasible branch, not an independent numerical oracle.
            // The independent fixed-TP phase points above verify which exchanges are thermodynamically improving.
            var restricted = Mixture(input, [.. Products(input).Where(name => name != excluded)]);
            var feasible = solver.Solve(restricted, new Problem { Kind = ProblemKind.AssignedEnthalpyPressure, Pressure = Number(input, "pressure"), Enthalpy = enthalpy });
            Assert.Equal(CaseStatus.Ok, feasible.Status);
            AssertClose(enthalpy, feasible.State.State.Enthalpy, "restricted branch assigned H");
            AssertElements(restricted.ElementMoles, feasible.State);
            Assert.True(full.State.Entropy + Budget(feasible.State.State.Entropy) >= feasible.State.State.Entropy,
                $"Ok HP state S={full.State.Entropy:R} loses to feasible same-H branch S={feasible.State.State.Entropy:R}");
        }
    }

    private static void AssertDominatesWitness(string kind, JsonElement constraint, MixtureState state)
    {
        var witness = constraint.GetProperty("witness");
        var target = Number(constraint, "target");
        var enthalpy = Number(witness, "independentEnthalpy");
        var entropy = Number(witness, "independentEntropy");
        if (kind == "hp")
        {
            AssertClose(target, enthalpy, "independent witness same H");
            AssertClose(target, state.Enthalpy, "returned assigned H");
            Assert.True(state.Entropy + Budget(entropy) >= entropy, $"Ok HP state S={state.Entropy:R} loses to independent feasible S={entropy:R}");
        }
        else
        {
            AssertClose(target, entropy, "independent witness same S");
            AssertClose(target, state.Entropy, "returned assigned S");
            Assert.True(state.Enthalpy <= enthalpy + Budget(enthalpy), $"Ok SP state H={state.Enthalpy:R} loses to independent feasible H={enthalpy:R}");
        }
    }

    private static void AssertWitnessElements(IReadOnlyDictionary<string, double> expected, JsonElement witness)
    {
        var fractions = witness.GetProperty("moleFractions").EnumerateObject().ToDictionary(property => property.Name, property => property.Value.GetDouble(), StringComparer.Ordinal);
        var molarMass = fractions.Sum(pair => pair.Value * Database[pair.Key].MolarMass);
        AssertElements(expected, fractions, molarMass);
    }

    private static void AssertElements(IReadOnlyDictionary<string, double> expected, Station station) =>
        AssertElements(expected, station.MoleFractions, station.State.MixtureMolarMass);

    private static void AssertElements(IReadOnlyDictionary<string, double> expected, IReadOnlyDictionary<string, double> fractions, double molarMass)
    {
        Assert.True(double.IsFinite(molarMass) && molarMass > 0);
        foreach (var element in expected)
        {
            var actual = fractions.Sum(species => species.Value * Database[species.Key].Formula.Where(atom => atom.Symbol == element.Key).Sum(atom => atom.Count)) * 1000 / molarMass;
            Assert.True(double.IsFinite(actual) && Math.Abs(actual - element.Value) <= ElementClosureBound * Math.Abs(element.Value),
                $"{element.Key}: expected {element.Value:R} mol/kg, actual {actual:R}");
        }
    }

    private static double Budget(double value) => Tolerances.For("polishThresholdRelative").Relative * Math.Max(1, Math.Abs(value));

    private static void AssertClose(double expected, double actual, string label) =>
        Assert.True(double.IsFinite(actual) && Math.Abs(actual - expected) <= Budget(expected), $"{label}: expected {expected:R}, actual {actual:R}");

    private static ElementalMixture Mixture(JsonElement input, IReadOnlyList<string>? products = null) =>
        ElementalMixture.Create(input.GetProperty("elementMoles").EnumerateObject().ToDictionary(property => property.Name, property => property.Value.GetDouble(), StringComparer.Ordinal),
            enthalpy: input.TryGetProperty("enthalpy", out var enthalpy) ? enthalpy.GetDouble() : input.GetProperty("hp").GetProperty("target").GetDouble(), only: products ?? Products(input));

    private static string[] Products(JsonElement input) => [.. input.GetProperty("products").EnumerateArray().Select(value => value.GetString()!)];

    private static double Number(JsonElement input, string name)
    {
        var value = input.GetProperty(name);
        return value.ValueKind == JsonValueKind.String ? double.Parse(value.GetString()!, CultureInfo.InvariantCulture) : value.GetDouble();
    }

    private static Solver CpuSolver() => Solver.Create(Database, new EngineOptions { Accelerator = AcceleratorKind.Cpu });

    private static JsonElement LoadCases()
    {
        using var document = JsonDocument.Parse(File.ReadAllText(RepositoryPaths.Resolve("tests", "Fixtures", "AuditPhaseCases.json")));
        Assert.Equal(Database.Provenance.ThermoSha256, document.RootElement.GetProperty("provenance").GetProperty("rawDataSha256").GetString());
        return document.RootElement.Clone();
    }
}
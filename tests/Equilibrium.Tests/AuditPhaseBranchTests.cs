using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text.Json;
using APThermo.Thermo;

namespace APThermo.Equilibrium.Tests;

/// <summary>Frozen independently checked witnesses for audit A01 and A35, not output snapshots.</summary>
[Collection(CpuFixture.CollectionName)]
public sealed class AuditPhaseBranchTests
{
    /// <summary>Matches the assigned-property polish; the witness decimals were independently evaluated at 60 digits.</summary>
    private const double PropertyResidual = 1.0e-9;

    /// <summary>The converged multiplier rounding limit of the condensed exit guard, dimensionless.</summary>
    private const double PhasePotentialResidual = 1.0e-9;

    /// <summary>The audit's raw-NASA9, 80-decimal-digit states on both sides of the SrCl2 crossing.</summary>
    public static TheoryData<double, string> OverlapWitnesses()
    {
        var data = new TheoryData<double, string>();
        foreach (var check in Data().GetProperty("phaseChecks").EnumerateArray())
        {
            var alpha = Number(check.GetProperty("alpha"), "g_J_per_kmol");
            var beta = Number(check.GetProperty("beta"), "g_J_per_kmol");
            data.Add(Number(check, "T_K"), alpha < beta ? "SrCL2(a)" : "SrCL2(b)");
        }

        return data;
    }

    /// <summary>An in-range phase cannot hide a lower-potential same-formula partner at an HP solution.</summary>
    [Fact]
    public void TheSrCl2HpStateHoldsTheStableEligiblePolymorph()
    {
        var problem = Strontium(ProblemKind.AssignedEnthalpyPressure, 0.0, Data().GetProperty("phaseHpTarget").GetDouble());
        var solution = HostSolver.Solve(CpuFixture.Shared.Accelerator, problem);
        Assert.Equal(CaseStatus.Ok, solution.Status);
        Assert.True(Math.Abs(solution.State.Enthalpy - problem.Target) <= PropertyResidual * Math.Abs(problem.Target));
        Assert.Empty(EquilibriumConditions.ElementConservationViolations(solution));
        AssertStablePartner(solution);
    }

    /// <summary>Cold and warm TP decisions agree with independently evaluated raw-data phase potentials.</summary>
    [Theory]
    [MemberData(nameof(OverlapWitnesses))]
    public void BothStartsChooseTheLowerPotentialEligiblePhase(double temperature, string expected)
    {
        var problem = Strontium(ProblemKind.AssignedTemperaturePressure, temperature, 0.0);
        var seed = HostSolver.Solve(CpuFixture.Shared.Accelerator, problem with { Temperature = temperature < 950.0 ? 980.0 : 920.0 });
        Assert.Equal(CaseStatus.Ok, seed.Status);
        var cold = HostSolver.Solve(CpuFixture.Shared.Accelerator, problem);
        var warm = HostSolver.Solve(CpuFixture.Shared.Accelerator, problem, seed.Moles);
        foreach (var solution in new[] { cold, warm })
        {
            Assert.Equal(CaseStatus.Ok, solution.Status);
            Assert.True(solution.Moles[problem.Table.IndexOf(expected)] > 0.0);
            Assert.Empty(EquilibriumConditions.ElementConservationViolations(solution));
            AssertStablePartner(solution);
        }
    }

    /// <summary>Same-H/S feasible witnesses rule out a thermodynamically inferior cold root below the condensed floors.</summary>
    [Theory]
    [InlineData(ProblemKind.AssignedEnthalpyPressure, "HP_feasible_higher_S")]
    [InlineData(ProblemKind.AssignedEntropyPressure, "SP_feasible_lower_H")]
    public void AColdHpOrSpResultIsNoWorseThanTheIndependentFeasibleWitness(ProblemKind kind, string witnessName)
    {
        var witness = Data().GetProperty("witnesses").GetProperty(witnessName);
        var problem = Magnesium(kind, witness);
        var tp = HostSolver.Solve(CpuFixture.Shared.Accelerator,
            problem with { Kind = ProblemKind.AssignedTemperaturePressure, Temperature = Number(witness, "T") });
        Assert.Equal(CaseStatus.Ok, tp.Status);
        AssertProperty(Number(witness, "independent_H"), tp.State.Enthalpy);
        AssertProperty(Number(witness, "independent_S"), tp.State.Entropy);
        var solution = HostSolver.Solve(CpuFixture.Shared.Accelerator, problem);
        Assert.Equal(CaseStatus.Ok, solution.Status);
        Assert.Empty(EquilibriumConditions.ElementConservationViolations(solution));
        var hp = kind == ProblemKind.AssignedEnthalpyPressure;
        AssertProperty(problem.Target, hp ? solution.State.Enthalpy : solution.State.Entropy);
        var expected = Number(witness, hp ? "independent_S" : "independent_H");
        var actual = hp ? solution.State.Entropy : solution.State.Enthalpy;
        var bound = PropertyResidual * Math.Abs(expected);
        Assert.True(hp ? actual >= expected - bound : actual <= expected + bound,
            $"{kind}: objective {actual:R}; feasible witness {expected:R} at {Number(witness, "T"):R} K; returned {solution.State.Temperature:R} K");
    }

    private static EquilibriumCase Strontium(ProblemKind kind, double temperature, double target)
    {
        var database = CpuFixture.Shared.Database;
        var products = Data().GetProperty("phaseProducts").EnumerateArray().Select(x => x.GetString()!).ToArray();
        var table = SpeciesTable.Build(database, ["AR", "SR", "CL"], products);
        var salt = 0.5 / database["SrCL2"].MolarMass;
        return new EquilibriumCase(table, kind, 1.0e5, temperature, target, [0.5 / database.AtomicWeight("AR"), salt, 2.0 * salt]);
    }

    private static EquilibriumCase Magnesium(ProblemKind kind, JsonElement witness)
    {
        var mixture = Data().GetProperty("mixture");
        var abundances = mixture.GetProperty("b").EnumerateObject().ToArray();
        var elements = abundances.Select(x => x.Name).ToArray();
        var products = CpuFixture.Shared.Database.Products
            .Where(x => x.Formula.Count > 0 && x.Formula.All(e => elements.Contains(e.Symbol, StringComparer.Ordinal)))
            .Select(x => x.Name).Distinct(StringComparer.Ordinal).ToArray();
        var table = SpeciesTable.Build(CpuFixture.Shared.Database, elements, products);
        var target = Number(witness, kind == ProblemKind.AssignedEnthalpyPressure ? "reported_H" : "reported_S");
        return new EquilibriumCase(table, kind, mixture.GetProperty("p").GetDouble(), 0.0, target,
            [.. abundances.Select(x => x.Value.GetDouble() / 1000.0)]);
    }

    private static void AssertStablePartner(HostSolution solution)
    {
        using var buffers = SpeciesTableBuffers.Upload(CpuFixture.Shared.Accelerator, solution.Case.Table);
        var alpha = solution.Case.Table.IndexOf("SrCL2(a)");
        var beta = solution.Case.Table.IndexOf("SrCL2(b)");
        var temperature = solution.State.Temperature;
        foreach (var (held, alternative) in new[] { (alpha, beta), (beta, alpha) })
        {
            if (solution.Moles[held] > 0.0 && temperature >= SpeciesFunctions.RecordLow(buffers.View, alternative)
                && temperature <= SpeciesFunctions.RecordHigh(buffers.View, alternative))
            {
                var gain = SpeciesFunctions.GOverRT(buffers.View, held, temperature) - SpeciesFunctions.GOverRT(buffers.View, alternative, temperature);
                Assert.True(gain <= PhasePotentialResidual, $"phase-exchange gain {gain:R} at {temperature:R} K");
            }
        }
    }

    private static void AssertProperty(double expected, double actual) =>
        Assert.True(double.IsFinite(actual) && Math.Abs(actual - expected) <= PropertyResidual * Math.Abs(expected),
            $"assigned/witness property: expected {expected:R}, actual {actual:R}");

    private static double Number(JsonElement item, string name) =>
        double.Parse(item.GetProperty(name).GetString()!, CultureInfo.InvariantCulture);

    private static JsonElement Data([CallerFilePath] string source = "")
    {
        using var document = JsonDocument.Parse(File.ReadAllText(Path.Combine(Path.GetDirectoryName(source)!, "AuditCases.json")));
        return document.RootElement.Clone();
    }
}
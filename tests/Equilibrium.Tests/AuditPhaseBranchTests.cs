using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text.Json;
using APThermo.Equilibrium.Recovery;
using APThermo.Thermo;
using ILGPU.Runtime;

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

    /// <summary>A rejected floor advances to every next distinct floor before the deterministic original replay.</summary>
    [Theory]
    [InlineData("RejectedProbe")]
    [InlineData("FailedFinal")]
    [InlineData("InferiorFinal")]
    public void RejectingOneFloorDoesNotExcludeEveryLaterDistinctFloor(string outcome)
    {
        var witness = Data().GetProperty("witnesses").GetProperty("SP_feasible_lower_H");
        var original = Data().GetProperty("witnesses").GetProperty("SP_returned_cold");
        var problem = Magnesium(ProblemKind.AssignedEntropyPressure, witness) with { Temperature = Number(original, "T") };
        CheckRejectedFloors(problem, outcome);
    }

    private static void CheckRejectedFloors(EquilibriumCase problem, string outcome)
    {
        var accelerator = CpuFixture.Shared.Accelerator;
        var table = problem.Table;
        using var buffers = SpeciesTableBuffers.Upload(accelerator, table);
        using var elements = accelerator.Allocate1D(problem.ElementMoles);
        using var doubles = accelerator.Allocate1D<double>(ScratchLayout.DoublesPerCase(table.SpeciesCount, table.ElementCount));
        using var ints = accelerator.Allocate1D<int>(ScratchLayout.IntsPerCase(table.SpeciesCount, table.ElementCount));
        using var moles = accelerator.Allocate1D<double>(table.SpeciesCount);
        using var multipliers = accelerator.Allocate1D<double>(table.ElementCount);
        using var states = accelerator.Allocate1D<MixtureState>(1);
        using var status = accelerator.Allocate1D<int>(1);
        using var iterations = accelerator.Allocate1D<int>(1);
        var input = new EquilibriumProblem(problem.Kind, problem.Pressure, problem.Temperature, problem.Target, elements.View);
        var scratch = EquilibriumScratch.Slice(doubles.View, ints.View, table.SpeciesCount, table.ElementCount);
        var result = new EquilibriumResult(moles.View, multipliers.View, states.View, status.View, iterations.View);
        var state = new IterationState();
        Assert.Equal(CaseStatus.Ok, CaseSetup.Begin(buffers.View, input, scratch, result, EstimateSource.Defaults, ref state));
        var floors = Enumerable.Range(table.GasCount, table.CondensedCount)
            .Where(j => DeadEnds.IsDeadEnd(buffers.View, j))
            .Select(j => SpeciesFunctions.RecordLow(buffers.View, j))
            .Where(t => t > problem.Temperature * (1.0 + 1.0e-9)).Distinct().Order().ToArray();
        Assert.True(floors.Length > 1, "this control requires several distinct floors");
        var plan = new AttemptPlan();
        plan.Begin(input, useMolesAsEstimate: false);
        plan.Recheck.Original = input;
        plan.Recheck.Floor = floors[0];
        plan.Recheck.Stage = Recheck.Pending;
        Assert.Equal(floors[0], DeadEnds.NextFloorAbove(buffers.View, scratch, problem.Temperature));
        for (var index = 1; index <= floors.Length; index++)
        {
            plan.Phase = AttemptPhase.TraceGasFinal;
            plan.Status = outcome == "FailedFinal" ? CaseStatus.NotConverged : CaseStatus.Ok;
            plan.Recheck.Stage = outcome == "RejectedProbe" ? Recheck.Pending : Recheck.Held;
            plan.Recheck.OriginalObjective = 1.0;
            result.State[0] = new MixtureState { Enthalpy = 2.0 };
            Assert.True(outcome == "RejectedProbe"
                ? DeadEndRecheck.Rejects(buffers.View, input, scratch, result, problem.Target + 1.0, ref plan)
                : DeadEndRecheck.EndFinal(buffers.View, input, scratch, result, ref plan));
            if (index < floors.Length)
            {
                Assert.Equal(Recheck.Pending, plan.Recheck.Stage);
                Assert.Equal(floors[index], plan.Recheck.Floor);
                AssertProperty(floors[index], plan.Current.Temperature);
            }
            else
            {
                Assert.Equal(Recheck.Replay, plan.Recheck.Stage);
                Assert.Equal(problem.Temperature, plan.Current.Temperature);
            }
        }
    }
    /// <summary>An HP target inside the overlap latent heat admits both phases at their independently evaluated crossing.</summary>
    [Fact]
    public void AnOverlapLatentHeatStatePinsAtTheCrossing()
    {
        var crossing = Number(Data().GetProperty("phaseCrossing"), "temperature_K");
        var problem = Strontium(ProblemKind.AssignedTemperaturePressure, crossing - 0.1, 0.0);
        var below = HostSolver.Solve(CpuFixture.Shared.Accelerator, problem);
        var above = HostSolver.Solve(CpuFixture.Shared.Accelerator, problem with { Temperature = crossing + 0.1 });
        Assert.Equal(CaseStatus.Ok, below.Status);
        Assert.Equal(CaseStatus.Ok, above.Status);
        var target = (below.State.Enthalpy + above.State.Enthalpy) * 0.5;
        var solution = HostSolver.Solve(CpuFixture.Shared.Accelerator,
            problem with { Kind = ProblemKind.AssignedEnthalpyPressure, Temperature = 0.0, Target = target });
        Assert.Equal(CaseStatus.Ok, solution.Status);
        AssertProperty(crossing, solution.State.Temperature);
        AssertProperty(target, solution.State.Enthalpy);
        Assert.True(solution.Moles[problem.Table.IndexOf("SrCL2(a)")] > 0.0);
        Assert.True(solution.Moles[problem.Table.IndexOf("SrCL2(b)")] > 0.0);
        Assert.Empty(EquilibriumConditions.ElementConservationViolations(solution));
        AssertStablePartner(solution);
    }
    /// <summary>The exit guard catches a lower-potential excluded partner even when it has stood down.</summary>
    [Theory]
    [InlineData("SrCL2(a)", false)]
    [InlineData("SrCL2(b)", true)]
    public void TheExitGuardDoesNotExemptAnExcludedSameFormulaPartner(string held, bool expected)
    {
        var temperature = Data().GetProperty("phaseChecks").EnumerateArray().Max(x => Number(x, "T_K"));
        var problem = Strontium(ProblemKind.AssignedTemperaturePressure, temperature, 0.0);
        var accelerator = CpuFixture.Shared.Accelerator;
        var table = problem.Table;
        using var buffers = SpeciesTableBuffers.Upload(accelerator, table);
        using var elements = accelerator.Allocate1D(problem.ElementMoles);
        using var doubles = accelerator.Allocate1D<double>(ScratchLayout.DoublesPerCase(table.SpeciesCount, table.ElementCount));
        using var ints = accelerator.Allocate1D<int>(ScratchLayout.IntsPerCase(table.SpeciesCount, table.ElementCount));
        using var moles = accelerator.Allocate1D<double>(table.SpeciesCount);
        using var multipliers = accelerator.Allocate1D<double>(table.ElementCount);
        using var states = accelerator.Allocate1D<MixtureState>(1);
        using var status = accelerator.Allocate1D<int>(1);
        using var iterations = accelerator.Allocate1D<int>(1);
        var input = new EquilibriumProblem(problem.Kind, problem.Pressure, problem.Temperature, problem.Target, elements.View);
        var scratch = EquilibriumScratch.Slice(doubles.View, ints.View, table.SpeciesCount, table.ElementCount);
        var result = new EquilibriumResult(moles.View, multipliers.View, states.View, status.View, iterations.View);
        var state = new IterationState();
        Assert.Equal(CaseStatus.Ok, CaseSetup.Begin(buffers.View, input, scratch, result, EstimateSource.Defaults, ref state));
        var heldIndex = table.IndexOf(held);
        var absentIndex = table.IndexOf(held == "SrCL2(a)" ? "SrCL2(b)" : "SrCL2(a)");
        state.CondensedCount = 1;
        scratch.CondensedInSolution[0] = heldIndex;
        SpeciesMarks.Set(scratch, absentIndex, SpeciesMark.StoodDown);
        for (var j = 0; j < table.SpeciesCount; j++)
        {
            scratch.GOverRT[j] = SpeciesFunctions.GOverRT(buffers.View, j, temperature);
        }

        for (var i = 0; i < table.ElementCount; i++)
        {
            result.Multipliers[i] = table.Elements[i] == "SR" ? scratch.GOverRT[heldIndex] : 0.0;
        }

        Assert.Equal(expected, Condensed.CondensedSet.ExitGuardFindsAPositiveCandidate(buffers.View, scratch, result, state));
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

using APThermo.Equilibrium.Recovery;
using APThermo.Harness;
using APThermo.Thermo;
using ILGPU.Runtime;

namespace APThermo.Equilibrium.Tests;

/// <summary>Synthetic completed finals check selection and exact transfer, separately from the physical audit witnesses.</summary>
[Collection(CpuFixture.CollectionName)]
public sealed class RecoveryCandidateTests
{
    /// <summary>A second improving candidate beats the first; remaining floors cannot overwrite its banked state.</summary>
    [Theory]
    [InlineData(ProblemKind.AssignedEnthalpyPressure)]
    [InlineData(ProblemKind.AssignedEntropyPressure)]
    public void ALaterImprovingFinalWinsAfterTheFirstImprovement(ProblemKind kind)
    {
        WithViews(kind, (table, input, scratch, result) =>
        {
            var plan = new AttemptPlan();
            plan.Begin(input, useMolesAsEstimate: false);
            plan.Status = CaseStatus.Ok;
            WriteCandidate(table, result, State(kind, 0), 1.0);
            DeadEndCandidates.Begin(table, input, scratch, result, ref plan);
            plan.Recheck.Floor = DeadEnds.NextFloorAbove(table, scratch, 180.0);
            plan.Recheck.Stage = Recheck.Held;
            plan.Phase = AttemptPhase.TraceGasFinal;
            plan.Iterations = 7;
            WriteCandidate(table, result, State(kind, 1), 2.0);
            Assert.True(RecheckFinal.End(table, input, scratch, result, ref plan));
            Assert.Equal(Recheck.Pending, plan.Recheck.Stage);
            var winner = State(kind, 2);
            plan.Recheck.Stage = Recheck.Held;
            plan.Phase = AttemptPhase.TraceGasFinal;
            plan.Status = CaseStatus.Ok;
            plan.Iterations += 11;
            WriteCandidate(table, result, winner, 3.0);
            var next = RecheckFinal.End(table, input, scratch, result, ref plan);
            var remaining = next ? Exhaust(table, input, scratch, result, ref plan) : 0;
            Assert.Equal(Recheck.Complete, plan.Recheck.Stage);
            Assert.Equal(CaseStatus.Ok, plan.Status);
            AssertState(winner, result.State[0]);
            AssertCandidate(table, result, 3.0);
            Assert.Equal(18 + remaining, plan.Iterations);
        });
    }

    /// <summary>The original wins exact ties and inferior candidates; probe work is still reported.</summary>
    [Theory]
    [InlineData(ProblemKind.AssignedEnthalpyPressure)]
    [InlineData(ProblemKind.AssignedEntropyPressure)]
    public void NoImprovementAndTiesRestoreTheOriginalWithoutDiscardingWork(ProblemKind kind)
    {
        WithViews(kind, (table, input, scratch, result) =>
        {
            var plan = new AttemptPlan();
            plan.Begin(input, useMolesAsEstimate: false);
            plan.Status = CaseStatus.Ok;
            var original = State(kind, 0);
            WriteCandidate(table, result, original, 1.0);
            DeadEndCandidates.Begin(table, input, scratch, result, ref plan);
            plan.Iterations = 13;
            WriteCandidate(table, result, original with { Temperature = original.Temperature + 1.0 }, 2.0);
            DeadEndCandidates.Consider(table, input, scratch, result, ref plan);
            WriteCandidate(table, result, State(kind, -1), 3.0);
            DeadEndCandidates.Consider(table, input, scratch, result, ref plan);
            DeadEndCandidates.Complete(table, input, scratch, result, ref plan);
            AssertState(original, result.State[0]);
            AssertCandidate(table, result, 1.0);
            Assert.Equal(13, plan.Iterations);
            Assert.Equal(CaseStatus.Ok, plan.Status);
        });
    }

    /// <summary>An unresolved branch cannot turn the best known composition into a falsely certified Ok state.</summary>
    [Fact]
    public void AnUnresolvedBranchReturnsAnHonestRefusalWithDiagnosticComposition()
    {
        WithViews(ProblemKind.AssignedEnthalpyPressure, (table, input, scratch, result) =>
        {
            var plan = new AttemptPlan();
            plan.Begin(input, useMolesAsEstimate: false);
            plan.Status = CaseStatus.Ok;
            WriteCandidate(table, result, State(input.Kind, 0), 1.0);
            DeadEndCandidates.Begin(table, input, scratch, result, ref plan);
            WriteCandidate(table, result, State(input.Kind, 1), 2.0);
            DeadEndCandidates.Consider(table, input, scratch, result, ref plan);
            plan.Recheck.Unresolved = true;
            DeadEndCandidates.Complete(table, input, scratch, result, ref plan);
            Assert.Equal(CaseStatus.NotConverged, plan.Status);
            AssertState(default, result.State[0]);
            AssertCandidate(table, result, 2.0);
        });
    }

    /// <summary>A gasless candidate uses its thermodynamic figures and restores its allowed T,p-only state.</summary>
    [Fact]
    public void AGaslessWinnerRetainsItsStatusAndTpOnlyState()
    {
        WithViews(ProblemKind.AssignedEnthalpyPressure, (table, input, scratch, result) =>
        {
            var plan = new AttemptPlan();
            plan.Begin(input, useMolesAsEstimate: false);
            plan.Status = CaseStatus.Ok;
            WriteCandidate(table, result, State(input.Kind, 0), 1.0);
            DeadEndCandidates.Begin(table, input, scratch, result, ref plan);
            var state = new MixtureState { Temperature = 400.0, Pressure = input.Pressure };
            WriteCandidate(table, result, state, 2.0);
            for (var j = 0; j < table.GasCount; j++)
            {
                result.Moles[j] = 0.0;
            }
            plan.Status = CaseStatus.NoGasPhase;
            plan.Found = EndKind.Gasless;
            plan.Figures.Entropy = 10.0;
            DeadEndCandidates.Consider(table, input, scratch, result, ref plan);
            DeadEndCandidates.Complete(table, input, scratch, result, ref plan);
            Assert.Equal(CaseStatus.NoGasPhase, plan.Status);
            AssertState(state, result.State[0]);
            for (var j = 0; j < table.SpeciesCount; j++)
            {
                Assert.Equal(j < table.GasCount ? 0.0 : 2.0 * (j + 1), result.Moles[j]);
            }
        });
    }

    /// <summary>A completed warm result, including signed-zero state bits, survives rejected alternatives exactly.</summary>
    [Theory]
    [InlineData(ProblemKind.AssignedEnthalpyPressure)]
    [InlineData(ProblemKind.AssignedEntropyPressure)]
    public void ASuccessfulWarmHpOrSpBanksAndRestoresTheExactOriginal(ProblemKind kind)
    {
        WithViews(kind, (table, input, scratch, result) =>
        {
            var plan = new AttemptPlan();
            plan.Begin(input, useMolesAsEstimate: true);
            plan.Iterations = 17;
            var original = State(kind, 0);
            original.InternalEnergy = -0.0;
            WriteCandidate(table, result, original, 1.0);
            SetPhaseGains(table, scratch, positive: true);
            Assert.True(AttemptPlan.Next(table, input, scratch, result, CaseStatus.Ok, ref plan));
            Assert.Equal(Recheck.Pending, plan.Recheck.Stage);
            Assert.Equal(AttemptPhase.Cold, plan.Phase);
            Assert.Equal(EstimateSource.Defaults, plan.Source);
            Assert.Equal(ProblemKind.AssignedTemperaturePressure, plan.Current.Kind);
            AssertState(original, plan.Recheck.BestState);
            var spent = Exhaust(table, input, scratch, result, ref plan);
            Assert.Equal(Recheck.Complete, plan.Recheck.Stage);
            Assert.Equal(CaseStatus.Ok, plan.Status);
            AssertState(original, result.State[0]);
            AssertCandidate(table, result, 1.0);
            Assert.Equal(17 + spent, plan.Iterations);
        });
    }

    /// <summary>A warm success without a positive-gain floor still completes directly, without changing its result.</summary>
    [Theory]
    [InlineData(ProblemKind.AssignedEnthalpyPressure)]
    [InlineData(ProblemKind.AssignedEntropyPressure)]
    public void ANoFloorWarmSuccessEndsUntouched(ProblemKind kind)
    {
        WithViews(kind, (table, input, scratch, result) =>
        {
            var plan = new AttemptPlan();
            plan.Begin(input, useMolesAsEstimate: true);
            plan.Iterations = 17;
            var original = State(kind, 0);
            WriteCandidate(table, result, original, 1.0);
            SetPhaseGains(table, scratch, positive: false);
            Assert.False(AttemptPlan.Next(table, input, scratch, result, CaseStatus.Ok, ref plan));
            Assert.Equal(Recheck.None, plan.Recheck.Stage);
            Assert.Equal(AttemptPhase.Warm, plan.Phase);
            Assert.Equal(EstimateSource.PreviousSolution, plan.Source);
            Assert.Equal(17, plan.Iterations);
            AssertState(original, result.State[0]);
            AssertCandidate(table, result, 1.0);
        });
    }

    /// <summary>A failed warm attempt keeps the existing one-time cold fallback and discards its temperature estimate.</summary>
    [Theory]
    [InlineData(ProblemKind.AssignedEnthalpyPressure)]
    [InlineData(ProblemKind.AssignedEntropyPressure)]
    public void AFailedWarmAttemptStillRequestsItsColdFallback(ProblemKind kind)
    {
        WithViews(kind, (table, input, scratch, result) =>
        {
            var seeded = new EquilibriumProblem(kind, input.Pressure, 725.0, input.Target, input.ElementMoles);
            var plan = new AttemptPlan();
            plan.Begin(seeded, useMolesAsEstimate: true);
            plan.Iterations = 17;
            Assert.True(AttemptPlan.Next(table, seeded, scratch, result, CaseStatus.SingularMatrix, ref plan));
            Assert.Equal(AttemptPhase.Cold, plan.Phase);
            Assert.Equal(EstimateSource.Defaults, plan.Source);
            Assert.Equal(0.0, plan.Current.Temperature);
            Assert.Equal(input.Target, plan.Current.Target);
            Assert.Equal(input.Pressure, plan.Current.Pressure);
            Assert.Equal(Recheck.None, plan.Recheck.Stage);
            Assert.Equal(17, plan.Iterations);
        });
    }

    /// <summary>Recovery finals contribute to the existing bank and advance its cursor instead of beginning a new recheck.</summary>
    [Theory]
    [InlineData(ProblemKind.AssignedEnthalpyPressure, false)]
    [InlineData(ProblemKind.AssignedEntropyPressure, false)]
    [InlineData(ProblemKind.AssignedEnthalpyPressure, true)]
    [InlineData(ProblemKind.AssignedEntropyPressure, true)]
    public void AWarmRecheckFinalAdvancesWithoutResettingItsBank(ProblemKind kind, bool traceGas)
    {
        WithViews(kind, (table, input, scratch, result) =>
        {
            var plan = new AttemptPlan();
            plan.Begin(input, useMolesAsEstimate: true);
            WriteCandidate(table, result, State(kind, 0), 1.0);
            SetPhaseGains(table, scratch, positive: true);
            Assert.True(AttemptPlan.Next(table, input, scratch, result, CaseStatus.Ok, ref plan));
            var firstFloor = plan.Recheck.Floor;
            plan.Recheck.Stage = Recheck.Held;
            plan.Phase = traceGas ? AttemptPhase.TraceGasFinal : AttemptPhase.Final;
            var better = State(kind, 1);
            WriteCandidate(table, result, better, 2.0);
            Assert.True(AttemptPlan.Next(table, input, scratch, result, CaseStatus.Ok, ref plan));
            Assert.True(plan.Recheck.Floor > firstFloor);
            AssertState(better, plan.Recheck.BestState);
            _ = Exhaust(table, input, scratch, result, ref plan);
            AssertState(better, result.State[0]);
            AssertCandidate(table, result, 2.0);
        });
    }

    private static void SetPhaseGains(SpeciesTableView table, EquilibriumScratch scratch, bool positive)
    {
        for (var index = table.GasCount; index < table.SpeciesCount; index++)
        {
            scratch.GOverRT[index] = positive ? -1e6 : 1e6;
        }
    }
    private static int Exhaust(SpeciesTableView table, EquilibriumProblem problem, EquilibriumScratch scratch,
                               EquilibriumResult result, ref AttemptPlan plan)
    {
        var count = 0;
        while (true)
        {
            count++;
            Assert.True(count <= table.SpeciesCount, "the distinct-floor walk must terminate");
            plan.Iterations++;
            plan.Status = CaseStatus.Ok;
            WriteCandidate(table, result, new MixtureState { Temperature = 999.0 }, 99.0);
            if (!DeadEndRecheck.Rejects(table, problem, scratch, result, problem.Target + 1.0, ref plan))
            {
                return count;
            }
        }
    }

    private static MixtureState State(ProblemKind kind, int rank)
    {
        object boxed = default(MixtureState);
        var marker = 20.0 + rank;
        foreach (var property in typeof(MixtureState).GetProperties().Where(p => p.PropertyType == typeof(double) && p.CanWrite))
        {
            property.SetValue(boxed, marker++);
        }

        var state = (MixtureState)boxed;
        state.Temperature = 180.0 + rank;
        state.Pressure = 1.0e5;
        state.Enthalpy = kind == ProblemKind.AssignedEnthalpyPressure ? 1.0 : 3.0 - rank;
        state.Entropy = kind == ProblemKind.AssignedEnthalpyPressure ? 1.0 + rank : 1.0;
        return state;
    }

    private static void AssertState(MixtureState expected, MixtureState actual)
    {
        var properties = typeof(MixtureState).GetProperties().Where(p => p.PropertyType == typeof(double)).ToArray();
        Assert.NotEmpty(properties);
        Assert.Equal(typeof(MixtureState).GetProperties().Length, properties.Length);
        foreach (var property in properties)
        {
            Assert.True(Bits.Same((double)property.GetValue(expected)!, (double)property.GetValue(actual)!), property.Name);
        }
    }
    private static void WriteCandidate(SpeciesTableView table, EquilibriumResult result, MixtureState state, double marker)
    {
        result.State[0] = state;
        for (var j = 0; j < table.SpeciesCount; j++)
        {
            result.Moles[j] = marker * (j + 1);
        }

        for (var i = 0; i < table.ElementCount; i++)
        {
            result.Multipliers[i] = -marker * (i + 1);
        }
    }

    private static void AssertCandidate(SpeciesTableView table, EquilibriumResult result, double marker)
    {
        for (var j = 0; j < table.SpeciesCount; j++)
        {
            Assert.Equal(marker * (j + 1), result.Moles[j]);
        }

        for (var i = 0; i < table.ElementCount; i++)
        {
            Assert.Equal(-marker * (i + 1), result.Multipliers[i]);
        }
    }

    private static void WithViews(ProblemKind kind, Action<SpeciesTableView, EquilibriumProblem, EquilibriumScratch, EquilibriumResult> inspect)
    {
        var name = kind == ProblemKind.AssignedEnthalpyPressure ? "HP_feasible_higher_S" : "SP_feasible_lower_H";
        var witness = AuditPhaseBranchTests.Data().GetProperty("witnesses").GetProperty(name);
        var c = AuditPhaseBranchTests.Magnesium(kind, witness);
        var accelerator = CpuFixture.Shared.Accelerator;
        var table = c.Table;
        using var buffers = SpeciesTableBuffers.Upload(accelerator, table);
        using var elements = accelerator.Allocate1D(c.ElementMoles);
        using var doubles = accelerator.Allocate1D<double>(ScratchLayout.DoublesPerCase(table.SpeciesCount, table.ElementCount));
        using var ints = accelerator.Allocate1D<int>(ScratchLayout.IntsPerCase(table.SpeciesCount, table.ElementCount));
        using var moles = accelerator.Allocate1D<double>(table.SpeciesCount);
        using var multipliers = accelerator.Allocate1D<double>(table.ElementCount);
        using var states = accelerator.Allocate1D<MixtureState>(1);
        using var status = accelerator.Allocate1D<int>(1);
        using var iterations = accelerator.Allocate1D<int>(1);
        var input = new EquilibriumProblem(kind, c.Pressure, c.Temperature, c.Target, elements.View);
        var scratch = EquilibriumScratch.Slice(doubles.View, ints.View, table.SpeciesCount, table.ElementCount);
        var result = new EquilibriumResult(moles.View, multipliers.View, states.View, status.View, iterations.View);
        var state = new IterationState();
        Assert.Equal(CaseStatus.Ok, CaseSetup.Begin(buffers.View, input, scratch, result, EstimateSource.Defaults, ref state));
        inspect(buffers.View, input, scratch, result);
    }
}

using APThermo.Thermo;
using ILGPU.Runtime;

namespace APThermo.Equilibrium.Tests;

/// <summary>
/// L0: the Newton loop's bookkeeping (BOOT.md, the loop's bookkeeping, 2026-09-26, the audit's finding 3), unit-tested
/// directly on the host without a table: a crossing of the trace threshold during a step fails its verdict, a failed
/// verdict clears the converged mark and the polish count, and a singular removal of a condensed species restarts
/// the step count and counts toward <see cref="EquilibriumSolver.MaxCondensedSetChanges"/>.
/// </summary>
[Collection(CpuFixture.CollectionName)]
public sealed class NewtonLoopStateTests
{
    private static readonly string[] Elements = ["H", "O"];
    private static readonly string[] Species = ["H2", "O2"];
    private static readonly double[] TwoElementMoles = [0.2, 0.1];

    /// <summary>A failed verdict clears the converged mark and the polish count.</summary>
    [Fact]
    public void ANotConvergedVerdictClearsTheConvergedMarkAndThePolishCount()
    {
        var loop = new NewtonLoopState();
        loop.RecordVerdict(ConvergenceVerdict.ReportTestsMet);
        loop.RecordVerdict(ConvergenceVerdict.ReportTestsMet);
        Assert.True(loop.Converged);
        Assert.Equal(2, loop.PolishSteps);

        loop.RecordVerdict(ConvergenceVerdict.NotConverged);

        Assert.False(loop.Converged);
        Assert.Equal(0, loop.PolishSteps);
    }

    /// <summary>ReportTestsMet marks convergence and counts a polish step; Polished marks it without counting one more.</summary>
    [Fact]
    public void ReportTestsMetCountsAPolishStepAndPolishedDoesNotCountAnother()
    {
        var loop = new NewtonLoopState();

        loop.RecordVerdict(ConvergenceVerdict.ReportTestsMet);
        Assert.True(loop.Converged);
        Assert.Equal(1, loop.PolishSteps);

        loop.RecordVerdict(ConvergenceVerdict.Polished);
        Assert.True(loop.Converged);
        Assert.Equal(1, loop.PolishSteps);
    }

    /// <summary>A change of the condensed set restarts the step count.</summary>
    [Fact]
    public void RecordSetChangeResetsTheStepCount()
    {
        var loop = new NewtonLoopState { Steps = 7 };
        loop.RecordSetChange();
        Assert.Equal(0, loop.Steps);
    }

    /// <summary>A gaseous species moving across the trace threshold during the step just applied crosses.</summary>
    [Fact]
    public void ASpeciesCrossingTheTraceThresholdDuringTheStepIsReportedAsACrossing()
    {
        var accelerator = CpuFixture.Shared.Accelerator;
        var table = SpeciesTable.Build(CpuFixture.Shared.Database, Elements, Species);
        using var buffers = SpeciesTableBuffers.Upload(accelerator, table);
        var view = buffers.View;
        using var doubles = accelerator.Allocate1D<double>(ScratchLayout.DoublesPerCase(view.SpeciesCount, view.ElementCount));
        using var ints = accelerator.Allocate1D<int>(ScratchLayout.IntsPerCase(view.SpeciesCount, view.ElementCount));
        var scratch = EquilibriumScratch.Slice(doubles.View, ints.View, view.SpeciesCount, view.ElementCount);
        using var molesBuffer = accelerator.Allocate1D<double>(view.SpeciesCount);
        using var multipliers = accelerator.Allocate1D<double>(view.ElementCount);
        using var state = accelerator.Allocate1D<MixtureState>(1);
        using var status = accelerator.Allocate1D<int>(1);
        using var iterations = accelerator.Allocate1D<int>(1);
        var result = new EquilibriumResult(molesBuffer.View, multipliers.View, state.View, status.View, iterations.View);

        SpeciesMarks.Set(scratch, 0, SpeciesMark.Active);
        SpeciesMarks.Set(scratch, 1, SpeciesMark.Active);
        scratch.LogMoles[0] = 0.0;                                     // n = 1: far above the trace threshold
        scratch.LogMoles[1] = -20.0;                                   // n ≈ 2e-9: below it (TraceThreshold = 18.42)
        result.Moles[0] = 1.0;                                         // was retained
        result.Moles[1] = 1.0;                                         // was retained too, at the linearization point

        // Species 1 was retained (its moles from the linearization point are positive) but the step just taken puts
        // it below the trace threshold: a crossing, whatever the step's own corrections say.
        Assert.True(ConvergenceTests.RetentionCrossed(view, scratch, result, logN: 0.0, traceThreshold: EquilibriumSolver.TraceThreshold));
    }

    /// <summary>No crossing when every gaseous species keeps the same side of the trace threshold.</summary>
    [Fact]
    public void NoCrossingWhenEveryGasSpeciesKeepsItsSideOfTheTraceThreshold()
    {
        var accelerator = CpuFixture.Shared.Accelerator;
        var table = SpeciesTable.Build(CpuFixture.Shared.Database, Elements, Species);
        using var buffers = SpeciesTableBuffers.Upload(accelerator, table);
        var view = buffers.View;
        using var doubles = accelerator.Allocate1D<double>(ScratchLayout.DoublesPerCase(view.SpeciesCount, view.ElementCount));
        using var ints = accelerator.Allocate1D<int>(ScratchLayout.IntsPerCase(view.SpeciesCount, view.ElementCount));
        var scratch = EquilibriumScratch.Slice(doubles.View, ints.View, view.SpeciesCount, view.ElementCount);
        using var molesBuffer = accelerator.Allocate1D<double>(view.SpeciesCount);
        using var multipliers = accelerator.Allocate1D<double>(view.ElementCount);
        using var state = accelerator.Allocate1D<MixtureState>(1);
        using var status = accelerator.Allocate1D<int>(1);
        using var iterations = accelerator.Allocate1D<int>(1);
        var result = new EquilibriumResult(molesBuffer.View, multipliers.View, state.View, status.View, iterations.View);

        SpeciesMarks.Set(scratch, 0, SpeciesMark.Active);
        SpeciesMarks.Set(scratch, 1, SpeciesMark.Active);
        scratch.LogMoles[0] = 0.0;
        scratch.LogMoles[1] = -20.0;
        result.Moles[0] = 1.0;
        result.Moles[1] = 0.0;                                         // was not retained either

        Assert.False(ConvergenceTests.RetentionCrossed(view, scratch, result, logN: 0.0, traceThreshold: EquilibriumSolver.TraceThreshold));
    }

    /// <summary>
    /// Once the gaseous resets are spent, the targeted remedy removes the one condensed species of the solution: a
    /// change of the set that restarts the loop's step count, counts toward the cap on condensed-set changes, and
    /// marks the removed record for the anti-cycling skip (BOOT.md, the targeted singular remedy, 2026-09-28).
    /// </summary>
    [Fact]
    public void ASingularRemovalOfACondensedSpeciesRestartsTheStepCountAndCountsTowardTheChangeCap()
    {
        var accelerator = CpuFixture.Shared.Accelerator;
        var table = SpeciesTable.Build(CpuFixture.Shared.Database, ["AL"], ["AL", "AL(cr)"]);
        using var buffers = SpeciesTableBuffers.Upload(accelerator, table);
        var view = buffers.View;
        using var doubles = accelerator.Allocate1D<double>(ScratchLayout.DoublesPerCase(view.SpeciesCount, view.ElementCount));
        using var ints = accelerator.Allocate1D<int>(ScratchLayout.IntsPerCase(view.SpeciesCount, view.ElementCount));
        var scratch = EquilibriumScratch.Slice(doubles.View, ints.View, view.SpeciesCount, view.ElementCount);
        using var molesBuffer = accelerator.Allocate1D<double>(view.SpeciesCount);
        using var multipliers = accelerator.Allocate1D<double>(view.ElementCount);
        using var state = accelerator.Allocate1D<MixtureState>(1);
        using var status = accelerator.Allocate1D<int>(1);
        using var iterations = accelerator.Allocate1D<int>(1);
        var result = new EquilibriumResult(molesBuffer.View, multipliers.View, state.View, status.View, iterations.View);
        var condensed = table.IndexOf("AL(cr)");
        scratch.CondensedInSolution[0] = condensed;
        result.Moles[condensed] = 0.5;

        var loop = new NewtonLoopState { Steps = 5, SingularResets = 2 };    // the gaseous resets are already spent
        var caseState = new IterationState { CondensedCount = 1, SetChanges = 0 };

        // No row targeting applies (failedRow outside every element and condensed row): the last-species fallback
        // removes the solution's one condensed species, as the pre-targeted remedy always did.
        var recovered = SingularRemedies.Recover(view, scratch, result, failedRow: -1, ref loop, ref caseState);

        Assert.True(recovered);
        Assert.Equal(0, caseState.CondensedCount);
        Assert.Equal(1, caseState.SetChanges);
        Assert.Equal(0, loop.SingularResets);
        Assert.Equal(0, loop.Steps);
        Assert.Equal(SpeciesMark.ForgivenOnce, SpeciesMarks.Of(scratch, condensed));
        Assert.Equal(condensed, caseState.LastRemovedForRange);
    }

    /// <summary>
    /// The case's first convergence switches the retention threshold to its second stage as a change of the retained
    /// set (it goes through <see cref="NewtonLoopState.RecordSetChange"/>, the same method a condensed-set change
    /// uses), and <see cref="NewtonIteration.Converge"/> exits Ok only once it has converged again under that second
    /// stage (BOOT.md, the two-stage retention threshold, 2026-09-28): every Ok this fact reaches already has
    /// <see cref="IterationState.RetentionSecondStage"/> set.
    /// </summary>
    [Fact]
    public void TheFirstConvergenceSwitchesToTheSecondRetentionStageBeforeConvergeExitsOk()
    {
        var accelerator = CpuFixture.Shared.Accelerator;
        var table = SpeciesTable.Build(CpuFixture.Shared.Database, Elements, Species);
        using var buffers = SpeciesTableBuffers.Upload(accelerator, table);
        var view = buffers.View;
        using var doubles = accelerator.Allocate1D<double>(ScratchLayout.DoublesPerCase(view.SpeciesCount, view.ElementCount));
        using var ints = accelerator.Allocate1D<int>(ScratchLayout.IntsPerCase(view.SpeciesCount, view.ElementCount));
        var scratch = EquilibriumScratch.Slice(doubles.View, ints.View, view.SpeciesCount, view.ElementCount);
        using var elements = accelerator.Allocate1D(TwoElementMoles);
        using var molesBuffer = accelerator.Allocate1D<double>(view.SpeciesCount);
        using var multipliers = accelerator.Allocate1D<double>(view.ElementCount);
        using var stateBuffer = accelerator.Allocate1D<MixtureState>(1);
        using var status = accelerator.Allocate1D<int>(1);
        using var iterations = accelerator.Allocate1D<int>(1);
        var result = new EquilibriumResult(molesBuffer.View, multipliers.View, stateBuffer.View, status.View, iterations.View);
        var problem = new EquilibriumProblem(ProblemKind.AssignedTemperaturePressure, 1e5, 3000.0, 0.0, elements.View);

        var state = new IterationState();
        Assert.Equal(CaseStatus.Ok, CaseSetup.Begin(view, problem, scratch, result, EstimateSource.Defaults, ref state));
        var caseStatus = NewtonIteration.Converge(view, problem, scratch, result, CaseSetup.LogPressure(problem), ref state);

        Assert.Equal(CaseStatus.Ok, caseStatus);
        Assert.True(state.RetentionSecondStage);
    }
}

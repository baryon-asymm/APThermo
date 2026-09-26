using APThermo.Thermo;

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
        Assert.True(ConvergenceTests.RetentionCrossed(view, scratch, result, logN: 0.0));
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

        Assert.False(ConvergenceTests.RetentionCrossed(view, scratch, result, logN: 0.0));
    }

    /// <summary>
    /// Once the gaseous resets are spent, the last condensed species is dropped: a change of the set that restarts
    /// the loop's step count and counts toward the cap on condensed-set changes.
    /// </summary>
    [Fact]
    public void ASingularRemovalOfACondensedSpeciesRestartsTheStepCountAndCountsTowardTheChangeCap()
    {
        const int gasCount = 1;
        const int speciesCount = 2;                                          // species 0 gas, species 1 condensed
        const int elementCount = 1;
        var accelerator = CpuFixture.Shared.Accelerator;
        using var doubles = accelerator.Allocate1D<double>(ScratchLayout.DoublesPerCase(speciesCount, elementCount));
        using var ints = accelerator.Allocate1D<int>(ScratchLayout.IntsPerCase(speciesCount, elementCount));
        var scratch = EquilibriumScratch.Slice(doubles.View, ints.View, speciesCount, elementCount);
        using var molesBuffer = accelerator.Allocate1D<double>(speciesCount);
        using var multipliers = accelerator.Allocate1D<double>(elementCount);
        using var state = accelerator.Allocate1D<MixtureState>(1);
        using var status = accelerator.Allocate1D<int>(1);
        using var iterations = accelerator.Allocate1D<int>(1);
        var result = new EquilibriumResult(molesBuffer.View, multipliers.View, state.View, status.View, iterations.View);
        scratch.CondensedInSolution[0] = 1;
        result.Moles[1] = 0.5;

        var singularResets = 2;                                              // the gaseous resets are already spent
        var loop = new NewtonLoopState { Steps = 5 };
        var caseState = new IterationState { CondensedCount = 1, SetChanges = 0 };

        var recovered = SingularRemedies.Recover(scratch, result, gasCount, ref singularResets, ref loop, ref caseState);

        Assert.True(recovered);
        Assert.Equal(0, caseState.CondensedCount);
        Assert.Equal(1, caseState.SetChanges);
        Assert.Equal(0, singularResets);
        Assert.Equal(0, loop.Steps);
    }
}

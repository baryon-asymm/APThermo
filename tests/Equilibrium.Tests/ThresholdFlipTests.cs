using APThermo.Equilibrium.Newton;
using APThermo.Fixtures;
using APThermo.Thermo;
using ILGPU.Runtime;

namespace APThermo.Equilibrium.Tests;

/// <summary>
/// L0 and L2: the threshold flip (Newton BOOT.md, "The bookkeeping of the loop", the verdict, the switch and the hold,
/// 2026-10-03; the orchestrator's investigation B2 for 0.2.1). The unit facts drive <see cref="Composition.IsRetained"/>,
/// <see cref="Composition.Retain"/> and <see cref="ConvergenceTests.RetentionVerdict"/> on a three-species table of H and O,
/// where the flip's two sides, one gas entering the retained set while another leaves, are two species placed by hand
/// either side of the threshold; the solve fact runs the one state of the investigation's normalized scan that only the
/// hold resolves.
/// </summary>
[Collection(CpuFixture.CollectionName)]
public sealed class ThresholdFlipTests
{
    private static readonly string[] Elements = ["H", "O"];
    private static readonly string[] Species = ["H2", "O2", "H2O"];

    /// <summary>ln of a mole number far above the first stage's threshold, with ln n = 0.</summary>
    private const double Above = -10.0;

    /// <summary>ln of a mole number below both stages' thresholds (18.42 and 25.33), with ln n = 0.</summary>
    private const double Below = -30.0;

    /// <summary>
    /// A held gas stays in the sums below the threshold, and one that was never retained does not enter on holding alone:
    /// holding only adds species to the minimized set that the case already carries.
    /// </summary>
    [Fact]
    public void AHeldGasStaysRetainedBelowTheThresholdAndANeverRetainedGasDoesNotEnter()
    {
        using var rig = Rig.Of();
        rig.Place(above: [0], moles: [1.0, 0.5, 0.0]);   // H2 above; O2 below but retained; H2O below and never retained

        Assert.True(Composition.IsRetained(rig.Scratch, rig.Result, 1, 0.0, EquilibriumSolver.TraceThreshold, held: true));
        Assert.False(Composition.IsRetained(rig.Scratch, rig.Result, 1, 0.0, EquilibriumSolver.TraceThreshold, held: false));
        Assert.False(Composition.IsRetained(rig.Scratch, rig.Result, 2, 0.0, EquilibriumSolver.TraceThreshold, held: true));

        var sum = Composition.Retain(rig.View, rig.Scratch, rig.Result, 0.0, EquilibriumSolver.TraceThreshold, held: true);

        Assert.Equal(Math.Exp(Above) + Math.Exp(Below), sum, 15);
        Assert.Equal(Math.Exp(Below), rig.Result.Moles[1]);
        Assert.Equal(0.0, rig.Result.Moles[2]);

        rig.Place(above: [0], moles: [1.0, 0.5, 0.0]);
        Assert.Equal(Math.Exp(Above), Composition.Retain(rig.View, rig.Scratch, rig.Result, 0.0, EquilibriumSolver.TraceThreshold, held: false), 15);
        Assert.Equal(0.0, rig.Result.Moles[1]);
    }

    /// <summary>One flip does not hold the set, a step that is not a flip clears the run, and the second flip in a row reports the hold.</summary>
    [Fact]
    public void ANonFlipClearsTheRunOfFlipsAndTwoFlipsInARowReportTheHold()
    {
        var loop = new NewtonLoopState();

        Assert.False(loop.RecordFlip(true));
        Assert.Equal(1, loop.Flips);
        Assert.False(loop.RecordFlip(false));
        Assert.Equal(0, loop.Flips);
        Assert.False(loop.RecordFlip(true));
        Assert.True(loop.RecordFlip(true));
        Assert.Equal(NewtonLoopState.FlipsBeforeHold, loop.Flips);
    }

    /// <summary>The switch to the second stage clears the polish steps of the first stage: they do not count toward the second convergence.</summary>
    [Fact]
    public void RestartingThePolishClearsThePolishSteps()
    {
        var loop = new NewtonLoopState();
        loop.RecordVerdict(ConvergenceVerdict.ReportTestsMet);
        loop.RecordVerdict(ConvergenceVerdict.ReportTestsMet);
        Assert.Equal(2, loop.PolishSteps);

        loop.RestartPolish();

        Assert.Equal(0, loop.PolishSteps);
        Assert.True(loop.Converged);
    }

    /// <summary>
    /// Under the first stage a crossing refuses nothing: the step that passes the report's tests converges and only
    /// triggers the switch, as the reference's <c>tsize</c> does, and it is no flip even when a gas entered and another left.
    /// </summary>
    [Fact]
    public void UnderTheFirstStageACrossingDoesNotRefuseThePassedVerdict()
    {
        using var rig = Rig.Of();
        rig.PlaceAFlip();
        var loop = new NewtonLoopState();
        var state = new IterationState { LogN = 0.0, RetentionSecondStage = false };

        var verdict = ConvergenceTests.RetentionVerdict(rig.View, rig.Scratch, rig.Result, ConvergenceVerdict.ReportTestsMet, ref loop, ref state);

        Assert.Equal(ConvergenceVerdict.ReportTestsMet, verdict);
        Assert.Equal(0, loop.Flips);
        Assert.False(state.RetainedSetHeld);
    }

    /// <summary>Under the second stage a crossing refuses the passed verdict, and a step the tests did not pass is refused already and counts as no flip.</summary>
    [Fact]
    public void UnderTheSecondStageACrossingRefusesThePassedVerdictAndAFailedStepIsNoFlip()
    {
        using var rig = Rig.Of();
        rig.Place(above: [0], moles: [1.0, 1.0, 0.0]);   // O2 leaves, nothing enters: not a flip
        var loop = new NewtonLoopState();
        var state = new IterationState { LogN = 0.0, RetentionSecondStage = true };

        var verdict = ConvergenceTests.RetentionVerdict(rig.View, rig.Scratch, rig.Result, ConvergenceVerdict.Polished, ref loop, ref state);

        Assert.Equal(ConvergenceVerdict.NotConverged, verdict);
        Assert.Equal(0, loop.Flips);

        rig.PlaceAFlip();
        var failed = ConvergenceTests.RetentionVerdict(rig.View, rig.Scratch, rig.Result, ConvergenceVerdict.NotConverged, ref loop, ref state);

        Assert.Equal(ConvergenceVerdict.NotConverged, failed);
        Assert.Equal(0, loop.Flips);
        Assert.False(state.RetainedSetHeld);
    }

    /// <summary>
    /// Two consecutive second-stage flips set <see cref="IterationState.RetainedSetHeld"/> and the step after it no longer
    /// sees the gas that left as leaving, so the flip cannot repeat.
    /// </summary>
    [Fact]
    public void TwoConsecutiveSecondStageFlipsHoldTheRetainedSetAndTheGasThatLeftNoLongerLeaves()
    {
        using var rig = Rig.Of();
        var loop = new NewtonLoopState();
        var state = new IterationState { LogN = 0.0, RetentionSecondStage = true };

        rig.PlaceAFlip();
        Assert.Equal(ConvergenceVerdict.NotConverged, ConvergenceTests.RetentionVerdict(rig.View, rig.Scratch, rig.Result, ConvergenceVerdict.ReportTestsMet, ref loop, ref state));
        Assert.False(state.RetainedSetHeld);

        rig.PlaceAFlip();
        Assert.Equal(ConvergenceVerdict.NotConverged, ConvergenceTests.RetentionVerdict(rig.View, rig.Scratch, rig.Result, ConvergenceVerdict.ReportTestsMet, ref loop, ref state));
        Assert.True(state.RetainedSetHeld);

        rig.PlaceAFlip();
        var crossing = ConvergenceTests.Crossing(rig.View, rig.Scratch, rig.Result, 0.0, EquilibriumSolver.TraceThreshold, held: true);

        Assert.Equal(RetentionCrossing.Entered, crossing);
    }

    /// <summary>
    /// Na : Cl : O = 1 : 1 : 3 per kilogram at 825 K and 1 kPa on the table of <c>naclo4_T500</c>: the state of the
    /// investigation's normalized scan that the verdict and the switch alone leave <c>NotConverged</c> and that the hold
    /// resolves. It ends <c>Ok</c> and clears every independent equilibrium condition. Red with the hold disabled.
    /// </summary>
    [Fact]
    public void SodiumPerchlorateTableAtNaClO3Ratio825KAnd1KPaConvergesAndHoldsTheEquilibriumConditions()
    {
        var c = HostSolver.Load("tp", "naclo4_T500");
        var table = HostSolver.BuildTable(CpuFixture.Shared.Database, c);
        var elementMoles = ElementMolesAtOxygenRatio(c, 3.0);

        var solution = HostSolver.Solve(CpuFixture.Shared.Accelerator,
                                        new EquilibriumCase(table, ProblemKind.AssignedTemperaturePressure, 1.0e3, 825.0, 0.0, elementMoles));

        Assert.Equal(CaseStatus.Ok, solution.Status);
        Assert.Empty(EquilibriumConditions.Violations(solution, 1.0e-6));
    }

    /// <summary>
    /// The fixture's element moles with oxygen at <paramref name="oxygen"/> per sodium and chlorine, renormalized to one
    /// kilogram through the molar masses of the fixture's own pure-element reactants.
    /// </summary>
    private static double[] ElementMolesAtOxygenRatio(CeaCase c, double oxygen)
    {
        var elements = HostSolver.ElementsOf(c);
        var moles = HostSolver.ElementMolesOf(c);
        var oxygenIndex = Array.IndexOf(elements, "O");
        moles[oxygenIndex] *= oxygen / 4.0;
        var mass = 0.0;
        foreach (var reactant in c.Inputs.GetProperty("reactants").EnumerateArray())
        {
            var element = reactant.GetProperty("formula").EnumerateObject().First().Name;
            mass += moles[Array.IndexOf(elements, element)] * reactant.GetProperty("molarMass").GetDouble();
        }

        return [.. moles.Select(m => m / mass)];
    }

    /// <summary>The H, O table of three gases with the views of one case over a CPU-accelerator buffer set.</summary>
    private sealed class Rig : IDisposable
    {
        private readonly SpeciesTableBuffers _tableBuffers;
        private readonly List<IDisposable> _buffers = [];

        private Rig(SpeciesTableBuffers tableBuffers, EquilibriumScratch scratch, EquilibriumResult result)
        {
            _tableBuffers = tableBuffers;
            Scratch = scratch;
            Result = result;
        }

        public SpeciesTableView View => _tableBuffers.View;

        public EquilibriumScratch Scratch { get; }

        public EquilibriumResult Result { get; }

        public static Rig Of()
        {
            var accelerator = CpuFixture.Shared.Accelerator;
            var table = SpeciesTable.Build(CpuFixture.Shared.Database, Elements, Species);
            var tableBuffers = SpeciesTableBuffers.Upload(accelerator, table);
            var view = tableBuffers.View;
            var doubles = accelerator.Allocate1D<double>(ScratchLayout.DoublesPerCase(view.SpeciesCount, view.ElementCount));
            var ints = accelerator.Allocate1D<int>(ScratchLayout.IntsPerCase(view.SpeciesCount, view.ElementCount));
            var moles = accelerator.Allocate1D<double>(view.SpeciesCount);
            var multipliers = accelerator.Allocate1D<double>(view.ElementCount);
            var state = accelerator.Allocate1D<MixtureState>(1);
            var status = accelerator.Allocate1D<int>(1);
            var iterations = accelerator.Allocate1D<int>(1);
            doubles.MemSetToZero();
            ints.MemSetToZero();
            moles.MemSetToZero();
            var scratch = EquilibriumScratch.Slice(doubles.View, ints.View, view.SpeciesCount, view.ElementCount);
            var rig = new Rig(tableBuffers, scratch, new EquilibriumResult(moles.View, multipliers.View, state.View, status.View, iterations.View));
            rig._buffers.AddRange([doubles, ints, moles, multipliers, state, status, iterations]);
            return rig;
        }

        /// <summary>
        /// Puts the gases in play and sets the amounts the last sums retained (<paramref name="moles"/>); the species in
        /// <paramref name="above"/> sit above the first stage's threshold, the others below it.
        /// </summary>
        public void Place(int[] above, double[] moles)
        {
            for (var j = 0; j < moles.Length; j++)
            {
                SpeciesMarks.Set(Scratch, j, SpeciesMark.Active);
                Scratch.LogMoles[j] = above.Contains(j) ? Above : Below;
                Result.Moles[j] = moles[j];
            }
        }

        /// <summary>The flip: H2 was not retained and now sits above the threshold, O2 was retained and now sits below it.</summary>
        public void PlaceAFlip() => Place(above: [0], moles: [0.0, 1.0, 0.0]);

        public void Dispose()
        {
            foreach (var buffer in _buffers)
            {
                buffer.Dispose();
            }

            _tableBuffers.Dispose();
        }
    }
}

using APThermo.Equilibrium;
using APThermo.Execution.Chunks;
using APThermo.Thermo;
using ILGPU;
using ILGPU.Runtime;

namespace APThermo.Execution;

/// <summary>The equilibrium program: its host arrays, its device buffers and views struct, and the result it assembles.</summary>
internal static class EquilibriumPipeline
{
    public static EquilibriumBatchResult Run(AcceleratorSession session, KernelCache kernels, EngineOptions options,
                                             UploadedTables tables, EquilibriumBatch batch)
    {
        var table = tables.Species;
        var speciesCount = table.SpeciesCount;
        batch.Validate(table.ElementCount, speciesCount);
        var timer = new RunTimer();
        var launcher = kernels.Get<Action<AcceleratorStream, Index1D, SpeciesTableView, EquilibriumBatchViews>>(nameof(Kernels.Equilibrium), out var warmUp);
        timer.AddWarmUp(warmUp);

        // BatchRun.Execute disposes buffers itself on the way out (2026-09-29, the third audit pass's finding 2): the
        // one place of this node that drops a lost session's own sticky exception. This using declaration's own
        // dispose, at the end of a successful run, finds every buffer already disposed and does nothing (ILGPU's own
        // dispose is idempotent) — kept only because a diagnostic (CA2000) needs a literal dispose beside the
        // allocation below, in the same method, to accept that this object does not escape undisposed.
        using var buffers = new ChunkBuffers(session.Accelerator);
        var (kindBuffer, pressureBuffer, temperatureBuffer, targetBuffer, elementBuffer, scratchDoubles, scratchInts,
             molesBuffer, multiplierBuffer, stateBuffer, statusBuffer, iterationBuffer,
             moles, states, status, iterations) = Declare(buffers, batch, speciesCount);

        var plan = ChunkPlan.For(batch.Count, buffers.BytesPerCase, buffers.MaxElementsPerCase, options, session.Budget);
        buffers.Allocate(plan.Size);
        var views = new EquilibriumBatchViews(
            seeded: batch.IsSeeded ? 1 : 0, kinds: kindBuffer.View, pressures: pressureBuffer.View, temperatures: temperatureBuffer.View, targets: targetBuffer.View,
            elementMoles: elementBuffer.View, scratchDoubles: scratchDoubles.View, scratchInts: scratchInts.View,
            moles: molesBuffer.View, multipliers: multiplierBuffer.View, states: stateBuffer.View,
            status: statusBuffer.View, iterations: iterationBuffer.View);
        BatchRun.Execute(session, plan, buffers, timer,
                         cases => launcher(session.Accelerator.DefaultStream, cases, tables.SpeciesBuffers.View, views));
        return new EquilibriumBatchResult(
            speciesCount: speciesCount, state: states, moles: moles,
            status: [.. status.Select(code => (CaseStatus)code)], iterations: iterations,
            timings: timer.Timings(), accelerator: session.Info);
    }

    /// <summary>
    /// The one declaration of this program's chunk buffers (2026-09-29, the third audit pass's observation on
    /// <see cref="DeclareBuffers"/>): <see cref="Run"/> and <see cref="DeclareBuffers"/> both call this and nothing
    /// else, so the shape a real run declares cannot drift from the shape <see cref="DeclareBuffers"/> hands the tests
    /// node. Also builds the host output arrays <see cref="Run"/> assembles its result from, sized from
    /// <paramref name="batch"/>'s own count — a single-case placeholder batch when called from
    /// <see cref="DeclareBuffers"/>, so nothing beyond the shape below is realistic there.
    /// </summary>
    private static (
        ChunkBuffer<int> Kind, ChunkBuffer<double> Pressure, ChunkBuffer<double> Temperature, ChunkBuffer<double> Target,
        ChunkBuffer<double> ElementMoles, ChunkBuffer<double> ScratchDoubles, ChunkBuffer<int> ScratchInts,
        ChunkBuffer<double> Moles, ChunkBuffer<double> Multipliers, ChunkBuffer<MixtureState> States,
        ChunkBuffer<int> Status, ChunkBuffer<int> Iterations,
        double[] MolesHost, MixtureState[] StatesHost, int[] StatusHost, int[] IterationsHost)
        Declare(ChunkBuffers buffers, EquilibriumBatch batch, int speciesCount)
    {
        var elementCount = batch.ElementCount;
        var count = batch.Count;
        var kinds = batch.Kind.Select(kind => (int)kind).ToArray();
        var kind = buffers.Input(kinds, 1);
        var pressure = buffers.Input(batch.Pressure, 1);
        var temperature = buffers.Input(batch.Temperature, 1);
        var target = buffers.Input(batch.Target, 1);
        var elementMoles = buffers.Input(batch.ElementMoles, elementCount);
        var scratchDoubles = buffers.Scratch<double>(ScratchLayout.DoublesPerCase(speciesCount, elementCount));
        var scratchInts = buffers.Scratch<int>(ScratchLayout.IntsPerCase(speciesCount, elementCount));

        // A seeded batch's moles buffer carries the seed in and the solution out; the copy leaves the caller's seed untouched.
        var molesHost = batch.IsSeeded ? (double[])batch.SeedMoles.Clone() : new double[(long)count * speciesCount];
        var moles = batch.IsSeeded ? buffers.InputOutput(molesHost, speciesCount) : buffers.ClearedOutput(molesHost, speciesCount);
        var multipliers = buffers.Scratch<double>(elementCount);
        var statesHost = new MixtureState[count];
        var states = buffers.ClearedOutput(statesHost, 1);
        var statusHost = new int[count];
        var status = buffers.Output(statusHost, 1);
        var iterationsHost = new int[count];
        var iterations = buffers.Output(iterationsHost, 1);
        return (kind, pressure, temperature, target, elementMoles, scratchDoubles, scratchInts,
                moles, multipliers, states, status, iterations, molesHost, statesHost, statusHost, iterationsHost);
    }

    /// <summary>
    /// The same buffer declarations <see cref="Run"/> makes for a table of the given shape, with a single-case
    /// placeholder batch and no device allocation (2026-09-28, the guards audit's F8; 2026-09-29, the third audit
    /// pass): lets a test read the real <see cref="ChunkBuffers.MaxElementsPerCase"/> this program declares — and so
    /// prove the 32-bit offset cap is actually wired from it into <see cref="ChunkPlan.For(int, long, long, EngineOptions, LaunchBudget)"/> —
    /// without running a batch large enough to make that cap bind for real.
    /// </summary>
    /// <remarks>With <paramref name="seeded"/> (2026-10-04) it declares a seeded batch's buffers, so a test can read that a seeded batch and a cold one cost the same device bytes per case.</remarks>
    internal static ChunkBuffers DeclareBuffers(Accelerator accelerator, int speciesCount, int elementCount, bool seeded = false)
    {
        var buffers = new ChunkBuffers(accelerator);
        var placeholder = seeded ? new EquilibriumBatch(1, elementCount, speciesCount) : new EquilibriumBatch(1, elementCount);
        _ = Declare(buffers, placeholder, speciesCount);
        return buffers;
    }
}

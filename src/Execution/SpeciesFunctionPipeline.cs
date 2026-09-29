using APThermo.Execution.Chunks;
using APThermo.Thermo;
using ILGPU;
using ILGPU.Runtime;

namespace APThermo.Execution;

/// <summary>The species-function program: its host arrays, its device buffers and views struct, and the result it assembles.</summary>
internal static class SpeciesFunctionPipeline
{
    public static SpeciesFunctionBatchResult Run(AcceleratorSession session, KernelCache kernels, EngineOptions options,
                                                 UploadedTables tables, SpeciesFunctionBatch batch)
    {
        batch.Validate(tables.Species.SpeciesCount);
        var count = batch.Count;
        var timer = new RunTimer();
        var launcher = kernels.Get<Action<AcceleratorStream, Index1D, SpeciesTableView, SpeciesFunctionBatchViews>>(nameof(Kernels.Functions), out var warmUp);
        timer.AddWarmUp(warmUp);

        var cpOverR = new double[count];
        var hOverRT = new double[count];
        var sOverR = new double[count];
        var inRange = new int[count];

        // BatchRun.Execute disposes buffers itself on the way out (2026-09-29, the third audit pass's finding 2): the
        // one place of this node that drops a lost session's own sticky exception. This using declaration's own
        // dispose, at the end of a successful run, finds every buffer already disposed and does nothing (ILGPU's own
        // dispose is idempotent) — kept only because a diagnostic (CA2000) needs a literal dispose beside the
        // allocation below, in the same method, to accept that this object does not escape undisposed.
        using var buffers = new ChunkBuffers(session.Accelerator);
        var speciesBuffer = buffers.Input(batch.Species, 1);
        var temperatureBuffer = buffers.Input(batch.Temperature, 1);
        var cpBuffer = buffers.Output(cpOverR, 1);
        var hBuffer = buffers.Output(hOverRT, 1);
        var sBuffer = buffers.Output(sOverR, 1);
        var rangeBuffer = buffers.Output(inRange, 1);

        var plan = ChunkPlan.For(count, buffers.BytesPerCase, buffers.MaxElementsPerCase, options, session.Budget);
        buffers.Allocate(plan.Size);
        var views = new SpeciesFunctionBatchViews(speciesBuffer.View, temperatureBuffer.View, cpBuffer.View, hBuffer.View, sBuffer.View, rangeBuffer.View);
        BatchRun.Execute(session, plan, buffers, timer,
                         cases => launcher(session.Accelerator.DefaultStream, cases, tables.SpeciesBuffers.View, views));
        return new SpeciesFunctionBatchResult(cpOverR, hOverRT, sOverR, [.. inRange.Select(flag => flag != 0)], timer.Timings(), session.Info);
    }
}

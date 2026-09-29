using APThermo.Execution.Chunks;
using APThermo.Thermo;
using APThermo.Transport;
using ILGPU;
using ILGPU.Runtime;

namespace APThermo.Execution;

/// <summary>The transport program: its host arrays, its device buffers and views struct, and the result it assembles.</summary>
internal static class TransportPipeline
{
    public static TransportBatchResult Run(AcceleratorSession session, KernelCache kernels, EngineOptions options,
                                           UploadedTables tables, TransportBatch batch)
    {
        var transportBuffers = tables.TransportBuffers ?? throw new ArgumentException("the tables were uploaded without a transport table", nameof(tables));
        var table = tables.Species;
        batch.Validate(table.SpeciesCount);
        var timer = new RunTimer();
        var launcher = kernels.Get<Action<AcceleratorStream, Index1D, SpeciesTableView, TransportTableView, TransportBatchViews>>(
            nameof(Kernels.Transport), out var warmUp);
        timer.AddWarmUp(warmUp);

        // BatchRun.Execute disposes buffers itself on the way out (2026-09-29, the third audit pass's finding 2): the
        // one place of this node that drops a lost session's own sticky exception. This using declaration's own
        // dispose, at the end of a successful run, finds every buffer already disposed and does nothing (ILGPU's own
        // dispose is idempotent) — kept only because a diagnostic (CA2000) needs a literal dispose beside the
        // allocation below, in the same method, to accept that this object does not escape undisposed.
        using var buffers = new ChunkBuffers(session.Accelerator);
        var (temperatureBuffer, molesBuffer, scratchDoubles, scratchInts, figureBuffer, statusBuffer, figures, status) =
            Declare(buffers, batch, table.ElementCount);

        var plan = ChunkPlan.For(batch.Count, buffers.BytesPerCase, buffers.MaxElementsPerCase, options, session.Budget);
        buffers.Allocate(plan.Size);
        var views = new TransportBatchViews(temperatureBuffer.View, molesBuffer.View, scratchDoubles.View, scratchInts.View,
                                            figureBuffer.View, statusBuffer.View);
        BatchRun.Execute(session, plan, buffers, timer,
                         cases => launcher(session.Accelerator.DefaultStream, cases, tables.SpeciesBuffers.View, transportBuffers.View, views));
        return new TransportBatchResult(figures, [.. status.Select(code => (CaseStatus)code)], timer.Timings(), session.Info);
    }

    /// <summary>
    /// The one declaration of this program's chunk buffers (2026-09-29, the third audit pass's observation on
    /// <see cref="DeclareBuffers"/>): see the note on <see cref="EquilibriumPipeline"/>'s own private <c>Declare</c>.
    /// </summary>
    private static (
        ChunkBuffer<double> Temperature, ChunkBuffer<double> Moles, ChunkBuffer<double> ScratchDoubles, ChunkBuffer<int> ScratchInts,
        ChunkBuffer<TransportFigures> Figures, ChunkBuffer<int> Status, TransportFigures[] FiguresHost, int[] StatusHost)
        Declare(ChunkBuffers buffers, TransportBatch batch, int elementCount)
    {
        var speciesCount = batch.SpeciesCount;
        var count = batch.Count;
        var temperature = buffers.Input(batch.Temperature, 1);
        var moles = buffers.Input(batch.Moles, speciesCount);
        var scratchDoubles = buffers.Scratch<double>(TransportLayout.DoublesPerCase(elementCount));
        var scratchInts = buffers.Scratch<int>(TransportLayout.IntsPerCase(speciesCount, elementCount));
        var figuresHost = new TransportFigures[count];
        var figures = buffers.Output(figuresHost, 1);
        var statusHost = new int[count];
        var status = buffers.Output(statusHost, 1);
        return (temperature, moles, scratchDoubles, scratchInts, figures, status, figuresHost, statusHost);
    }

    /// <summary>The same buffer declarations <see cref="Run"/> makes for a table of the given shape, with a
    /// single-station placeholder batch and no device allocation (2026-09-28, the guards audit's F8; 2026-09-29,
    /// the third audit pass): see the note on <see cref="EquilibriumPipeline.DeclareBuffers"/>.</summary>
    internal static ChunkBuffers DeclareBuffers(Accelerator accelerator, int speciesCount, int elementCount)
    {
        var buffers = new ChunkBuffers(accelerator);
        _ = Declare(buffers, new TransportBatch(1, speciesCount), elementCount);
        return buffers;
    }
}

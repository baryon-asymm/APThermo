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
        var speciesCount = table.SpeciesCount;
        var elementCount = table.ElementCount;
        var count = batch.Count;
        var timer = new RunTimer();
        var launcher = kernels.Get<Action<AcceleratorStream, Index1D, SpeciesTableView, TransportTableView, TransportBatchViews>>(
            nameof(Kernels.Transport), out var warmUp);
        timer.AddWarmUp(warmUp);

        var figures = new TransportFigures[count];
        var status = new int[count];

        using var buffers = new ChunkBuffers(session.Accelerator);
        var temperatureBuffer = buffers.Input(batch.Temperature, 1);
        var molesBuffer = buffers.Input(batch.Moles, speciesCount);
        var scratchDoubles = buffers.Scratch<double>(TransportLayout.DoublesPerCase(elementCount));
        var scratchInts = buffers.Scratch<int>(TransportLayout.IntsPerCase(speciesCount, elementCount));
        var figureBuffer = buffers.Output(figures, 1);
        var statusBuffer = buffers.Output(status, 1);

        var plan = ChunkPlan.For(count, buffers.BytesPerCase, buffers.MaxElementsPerCase, options, session.Budget);
        buffers.Allocate(plan.Size);
        var views = new TransportBatchViews(temperatureBuffer.View, molesBuffer.View, scratchDoubles.View, scratchInts.View,
                                            figureBuffer.View, statusBuffer.View);
        BatchRun.Execute(session, plan, buffers, timer,
                         cases => launcher(session.Accelerator.DefaultStream, cases, tables.SpeciesBuffers.View, transportBuffers.View, views));
        return new TransportBatchResult(figures, [.. status.Select(code => (CaseStatus)code)], timer.Timings(), session.Info);
    }

    /// <summary>The same buffer declarations <see cref="Run"/> makes for a table of the given shape, with empty host
    /// arrays and no device allocation (2026-09-28, the guards audit's F8): see the note on
    /// <see cref="EquilibriumPipeline.DeclareBuffers"/>.</summary>
    internal static ChunkBuffers DeclareBuffers(Accelerator accelerator, int speciesCount, int elementCount)
    {
        var buffers = new ChunkBuffers(accelerator);
        _ = buffers.Input(Array.Empty<double>(), 1);
        _ = buffers.Input(Array.Empty<double>(), speciesCount);
        _ = buffers.Scratch<double>(TransportLayout.DoublesPerCase(elementCount));
        _ = buffers.Scratch<int>(TransportLayout.IntsPerCase(speciesCount, elementCount));
        _ = buffers.Output(Array.Empty<TransportFigures>(), 1);
        _ = buffers.Output(Array.Empty<int>(), 1);
        return buffers;
    }
}

using AerospacePropellantThermodynamics.Thermo;
using AerospacePropellantThermodynamics.Transport;
using ILGPU;
using ILGPU.Runtime;

namespace AerospacePropellantThermodynamics.Execution;

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
        var scratchDoubles = buffers.Scratch<double>(TransportLayout.DoublesPerCase(speciesCount, elementCount));
        var scratchInts = buffers.Scratch<int>(TransportLayout.IntsPerCase(speciesCount, elementCount));
        var figureBuffer = buffers.Output(figures, 1);
        var statusBuffer = buffers.Output(status, 1);

        var plan = ChunkPlan.For(count, buffers.BytesPerCase, options);
        buffers.Allocate(plan.Size);
        var views = new TransportBatchViews(temperatureBuffer.View, molesBuffer.View, scratchDoubles.View, scratchInts.View,
                                            figureBuffer.View, statusBuffer.View);
        BatchRun.Execute(session, plan, buffers, timer,
                         cases => launcher(session.Accelerator.DefaultStream, cases, tables.SpeciesBuffers.View, transportBuffers.View, views));
        return new TransportBatchResult(figures, status.Select(code => (CaseStatus)code).ToArray(), timer.Timings(), session.Info);
    }
}

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
        batch.Validate(table.ElementCount);
        var speciesCount = table.SpeciesCount;
        var elementCount = table.ElementCount;
        var count = batch.Count;
        var timer = new RunTimer();
        var launcher = kernels.Get<Action<AcceleratorStream, Index1D, SpeciesTableView, EquilibriumBatchViews>>(nameof(Kernels.Equilibrium), out var warmUp);
        timer.AddWarmUp(warmUp);

        var states = new MixtureState[count];
        var moles = new double[(long)count * speciesCount];
        var status = new int[count];
        var iterations = new int[count];
        var kinds = batch.Kind.Select(kind => (int)kind).ToArray();

        using var buffers = new ChunkBuffers(session.Accelerator);
        var kindBuffer = buffers.Input(kinds, 1);
        var pressureBuffer = buffers.Input(batch.Pressure, 1);
        var temperatureBuffer = buffers.Input(batch.Temperature, 1);
        var targetBuffer = buffers.Input(batch.Target, 1);
        var elementBuffer = buffers.Input(batch.ElementMoles, elementCount);
        var scratchDoubles = buffers.Scratch<double>(ScratchLayout.DoublesPerCase(speciesCount, elementCount));
        var scratchInts = buffers.Scratch<int>(ScratchLayout.IntsPerCase(speciesCount, elementCount));
        var molesBuffer = buffers.ClearedOutput(moles, speciesCount);
        var multiplierBuffer = buffers.Scratch<double>(elementCount);
        var stateBuffer = buffers.ClearedOutput(states, 1);
        var statusBuffer = buffers.Output(status, 1);
        var iterationBuffer = buffers.Output(iterations, 1);

        var plan = ChunkPlan.For(count, buffers.BytesPerCase, options);
        buffers.Allocate(plan.Size);
        var views = new EquilibriumBatchViews(
            kinds: kindBuffer.View, pressures: pressureBuffer.View, temperatures: temperatureBuffer.View, targets: targetBuffer.View,
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
}

using APThermo.Equilibrium;
using APThermo.Execution.Chunks;
using APThermo.Performance;
using APThermo.Thermo;
using ILGPU;
using ILGPU.Runtime;

namespace APThermo.Execution;

/// <summary>The rocket program: its host arrays, its device buffers and views struct, and the result it assembles.</summary>
internal static class RocketPipeline
{
    public static RocketBatchResult Run(AcceleratorSession session, KernelCache kernels, EngineOptions options,
                                        UploadedTables tables, RocketBatch batch)
    {
        var table = tables.Species;
        batch.Validate(table.ElementCount);
        var speciesCount = table.SpeciesCount;
        var elementCount = table.ElementCount;
        var count = batch.Count;
        var exits = batch.Exits;
        var stations = batch.StationCount;
        var timer = new RunTimer();
        var launcher = kernels.Get<Action<AcceleratorStream, Index1D, SpeciesTableView, RocketBatchViews>>(nameof(Kernels.Rocket), out var warmUp);
        timer.AddWarmUp(warmUp);

        var stationStates = new MixtureState[(long)count * stations];
        var moles = new double[(long)count * stations * speciesCount];
        var figures = new PerformanceFigures[(long)count * stations];
        var stationStatus = new int[(long)count * stations];
        var iterations = new int[(long)count * stations];
        var status = new int[count];

        using var buffers = new ChunkBuffers(session.Accelerator);
        var pressureBuffer = buffers.Input(batch.ChamberPressure, 1);
        var enthalpyBuffer = buffers.Input(batch.ReactantEnthalpy, 1);
        var estimateBuffer = buffers.Input(batch.TemperatureEstimate, 1);
        var flowBuffer = buffers.Input(batch.Flow.Select(flow => (int)flow).ToArray(), 1);
        var elementBuffer = buffers.Input(batch.ElementMoles, elementCount);
        var exitValueBuffer = buffers.Input(batch.ExitValues, exits);
        var exitKindBuffer = buffers.Constant(batch.ExitKinds.Select(kind => (int)kind).ToArray());
        var scratchDoubles = buffers.Scratch<double>(ScratchLayout.DoublesPerCase(speciesCount, elementCount));
        var scratchInts = buffers.Scratch<int>(ScratchLayout.IntsPerCase(speciesCount, elementCount));
        var stationBuffer = buffers.ClearedOutput(stationStates, stations);
        var molesBuffer = buffers.ClearedOutput(moles, (long)stations * speciesCount);
        var multiplierBuffer = buffers.Scratch<double>((long)stations * elementCount);
        var figureBuffer = buffers.ClearedOutput(figures, stations);
        var stationStatusBuffer = buffers.Output(stationStatus, stations);
        var iterationBuffer = buffers.Output(iterations, stations);
        var statusBuffer = buffers.Output(status, 1);

        var plan = ChunkPlan.For(count, buffers.BytesPerCase, buffers.MaxElementsPerCase, options, session.Budget);
        buffers.Allocate(plan.Size);
        var views = new RocketBatchViews(
            exitCount: exits, chamberPressures: pressureBuffer.View, reactantEnthalpies: enthalpyBuffer.View,
            temperatureEstimates: estimateBuffer.View, flows: flowBuffer.View, elementMoles: elementBuffer.View,
            exitValues: exitValueBuffer.View, exitKinds: exitKindBuffer.View, scratchDoubles: scratchDoubles.View,
            scratchInts: scratchInts.View, stations: stationBuffer.View,
            moles: molesBuffer.View, multipliers: multiplierBuffer.View, figures: figureBuffer.View,
            stationStatus: stationStatusBuffer.View, iterations: iterationBuffer.View,
            status: statusBuffer.View);
        BatchRun.Execute(session, plan, buffers, timer,
                         cases => launcher(session.Accelerator.DefaultStream, cases, tables.SpeciesBuffers.View, views));
        return new RocketBatchResult(
            speciesCount: speciesCount, stationCount: stations, stations: stationStates, moles: moles, figures: figures,
            stationStatus: [.. stationStatus.Select(code => (CaseStatus)code)], iterations: iterations,
            status: [.. status.Select(code => (CaseStatus)code)], timings: timer.Timings(), accelerator: session.Info);
    }

    /// <summary>The same buffer declarations <see cref="Run"/> makes for a table of the given shape, with empty host
    /// arrays and no device allocation (2026-09-28, the guards audit's F8): see the note on
    /// <see cref="EquilibriumPipeline.DeclareBuffers"/>.</summary>
    internal static ChunkBuffers DeclareBuffers(Accelerator accelerator, int speciesCount, int elementCount, int exits)
    {
        var stations = RocketLayout.StationCount(exits);
        var buffers = new ChunkBuffers(accelerator);
        _ = buffers.Input(Array.Empty<double>(), 1);
        _ = buffers.Input(Array.Empty<double>(), 1);
        _ = buffers.Input(Array.Empty<double>(), 1);
        _ = buffers.Input(Array.Empty<int>(), 1);
        _ = buffers.Input(Array.Empty<double>(), elementCount);
        _ = buffers.Input(Array.Empty<double>(), exits);
        _ = buffers.Constant(Array.Empty<int>());
        _ = buffers.Scratch<double>(ScratchLayout.DoublesPerCase(speciesCount, elementCount));
        _ = buffers.Scratch<int>(ScratchLayout.IntsPerCase(speciesCount, elementCount));
        _ = buffers.ClearedOutput(Array.Empty<MixtureState>(), stations);
        _ = buffers.ClearedOutput(Array.Empty<double>(), (long)stations * speciesCount);
        _ = buffers.Scratch<double>((long)stations * elementCount);
        _ = buffers.ClearedOutput(Array.Empty<PerformanceFigures>(), stations);
        _ = buffers.Output(Array.Empty<int>(), stations);
        _ = buffers.Output(Array.Empty<int>(), stations);
        _ = buffers.Output(Array.Empty<int>(), 1);
        return buffers;
    }
}

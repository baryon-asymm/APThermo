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
        var stations = batch.StationCount;
        var timer = new RunTimer();
        var launcher = kernels.Get<Action<AcceleratorStream, Index1D, SpeciesTableView, RocketBatchViews>>(nameof(Kernels.Rocket), out var warmUp);
        timer.AddWarmUp(warmUp);

        // BatchRun.Execute disposes buffers itself on the way out (2026-09-29, the third audit pass's finding 2): the
        // one place of this node that drops a lost session's own sticky exception. This using declaration's own
        // dispose, at the end of a successful run, finds every buffer already disposed and does nothing (ILGPU's own
        // dispose is idempotent) — kept only because a diagnostic (CA2000) needs a literal dispose beside the
        // allocation below, in the same method, to accept that this object does not escape undisposed.
        using var buffers = new ChunkBuffers(session.Accelerator);
        var (pressureBuffer, enthalpyBuffer, estimateBuffer, flowBuffer, elementBuffer, exitValueBuffer, exitKindBuffer,
             scratchDoubles, scratchInts, stationBuffer, molesBuffer, multiplierBuffer, figureBuffer, stationStatusBuffer,
             iterationBuffer, statusBuffer, stationStates, moles, figures, stationStatus, iterations, status) =
            Declare(buffers, batch, speciesCount);

        var plan = ChunkPlan.For(batch.Count, buffers.BytesPerCase, buffers.MaxElementsPerCase, options, session.Budget);
        buffers.Allocate(plan.Size);
        var views = new RocketBatchViews(
            exitCount: batch.Exits, chamberPressures: pressureBuffer.View, reactantEnthalpies: enthalpyBuffer.View,
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

    /// <summary>
    /// The one declaration of this program's chunk buffers (2026-09-29, the third audit pass's observation on
    /// <see cref="DeclareBuffers"/>): see the note on <see cref="EquilibriumPipeline"/>'s own private <c>Declare</c>.
    /// </summary>
    private static (
        ChunkBuffer<double> Pressure, ChunkBuffer<double> Enthalpy, ChunkBuffer<double> Estimate, ChunkBuffer<int> Flow,
        ChunkBuffer<double> ElementMoles, ChunkBuffer<double> ExitValues, ChunkBuffer<int> ExitKinds,
        ChunkBuffer<double> ScratchDoubles, ChunkBuffer<int> ScratchInts, ChunkBuffer<MixtureState> Stations,
        ChunkBuffer<double> Moles, ChunkBuffer<double> Multipliers, ChunkBuffer<PerformanceFigures> Figures,
        ChunkBuffer<int> StationStatus, ChunkBuffer<int> Iterations, ChunkBuffer<int> Status,
        MixtureState[] StationsHost, double[] MolesHost, PerformanceFigures[] FiguresHost, int[] StationStatusHost,
        int[] IterationsHost, int[] StatusHost)
        Declare(ChunkBuffers buffers, RocketBatch batch, int speciesCount)
    {
        var elementCount = batch.ElementCount;
        var exits = batch.Exits;
        var stations = batch.StationCount;
        var count = batch.Count;
        var pressure = buffers.Input(batch.ChamberPressure, 1);
        var enthalpy = buffers.Input(batch.ReactantEnthalpy, 1);
        var estimate = buffers.Input(batch.TemperatureEstimate, 1);
        var flow = buffers.Input(batch.Flow.Select(kind => (int)kind).ToArray(), 1);
        var elementMoles = buffers.Input(batch.ElementMoles, elementCount);
        var exitValues = buffers.Input(batch.ExitValues, exits);
        var exitKinds = buffers.Constant(batch.ExitKinds.Select(kind => (int)kind).ToArray());
        var scratchDoubles = buffers.Scratch<double>(ScratchLayout.DoublesPerCase(speciesCount, elementCount));
        var scratchInts = buffers.Scratch<int>(ScratchLayout.IntsPerCase(speciesCount, elementCount));
        var stationsHost = new MixtureState[(long)count * stations];
        var stationBuffer = buffers.ClearedOutput(stationsHost, stations);
        var molesHost = new double[(long)count * stations * speciesCount];
        var moles = buffers.ClearedOutput(molesHost, (long)stations * speciesCount);
        var multipliers = buffers.Scratch<double>((long)stations * elementCount);
        var figuresHost = new PerformanceFigures[(long)count * stations];
        var figures = buffers.ClearedOutput(figuresHost, stations);
        var stationStatusHost = new int[(long)count * stations];
        var stationStatus = buffers.Output(stationStatusHost, stations);
        var iterationsHost = new int[(long)count * stations];
        var iterations = buffers.Output(iterationsHost, stations);
        var statusHost = new int[count];
        var status = buffers.Output(statusHost, 1);
        return (pressure, enthalpy, estimate, flow, elementMoles, exitValues, exitKinds, scratchDoubles, scratchInts,
                stationBuffer, moles, multipliers, figures, stationStatus, iterations, status,
                stationsHost, molesHost, figuresHost, stationStatusHost, iterationsHost, statusHost);
    }

    /// <summary>The same buffer declarations <see cref="Run"/> makes for a table of the given shape, with a
    /// single-case placeholder batch and no device allocation (2026-09-28, the guards audit's F8; 2026-09-29, the
    /// third audit pass): see the note on <see cref="EquilibriumPipeline.DeclareBuffers"/>.</summary>
    internal static ChunkBuffers DeclareBuffers(Accelerator accelerator, int speciesCount, int elementCount, int exits)
    {
        var buffers = new ChunkBuffers(accelerator);
        var exitKinds = new ExitSpecification[exits];
        _ = Declare(buffers, new RocketBatch(1, elementCount, exitKinds), speciesCount);
        return buffers;
    }
}

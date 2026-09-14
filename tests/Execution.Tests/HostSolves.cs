using AerospacePropellantThermodynamics.Equilibrium;
using AerospacePropellantThermodynamics.Performance;
using AerospacePropellantThermodynamics.Thermo;
using AerospacePropellantThermodynamics.Transport;
using ILGPU.Runtime;

namespace AerospacePropellantThermodynamics.Execution.Tests;

/// <summary>One rocket case solved on the host over the accelerator's buffers, as the numerical node is called directly.</summary>
internal readonly record struct HostRocketCase(MixtureState[] Stations, double[] Moles, PerformanceFigures[] Figures, CaseStatus[] StationStatus, int[] Iterations, CaseStatus Status);

/// <summary>One equilibrium case solved on the host.</summary>
internal readonly record struct HostEquilibriumCase(MixtureState State, double[] Moles, CaseStatus Status, int Iterations);

/// <summary>One station's transport evaluated on the host.</summary>
internal readonly record struct HostTransportStation(CaseStatus Status, TransportFigures Figures);

/// <summary>One case run through the numerical nodes directly over the accelerator's own buffers: the L2 bit-equality standard.</summary>
internal static class HostSolves
{
    public static HostRocketCase Rocket(Accelerator accelerator, SpeciesTableBuffers buffers, RocketBatch batch, int k)
    {
        var table = buffers.Table;
        var speciesCount = table.SpeciesCount;
        var elementCount = table.ElementCount;
        var stationCount = batch.StationCount;
        using var elements = accelerator.Allocate1D(batch.ElementMoles.AsSpan(k * elementCount, elementCount).ToArray());
        using var exitValues = accelerator.Allocate1D(batch.Exits == 0 ? new[] { 0.0 } : batch.ExitValues.AsSpan(k * batch.Exits, batch.Exits).ToArray());
        using var exitKinds = accelerator.Allocate1D(batch.Exits == 0 ? new[] { 0 } : batch.ExitKinds.Select(x => (int)x).ToArray());
        using var doubles = accelerator.Allocate1D<double>(ScratchLayout.DoublesPerCase(speciesCount, elementCount));
        using var ints = accelerator.Allocate1D<int>(ScratchLayout.IntsPerCase(speciesCount, elementCount));
        using var stations = accelerator.Allocate1D<MixtureState>(stationCount);
        using var moles = accelerator.Allocate1D<double>((long)stationCount * speciesCount);
        using var multipliers = accelerator.Allocate1D<double>((long)stationCount * elementCount);
        using var figures = accelerator.Allocate1D<PerformanceFigures>(stationCount);
        using var stationStatus = accelerator.Allocate1D<int>(stationCount);
        using var iterations = accelerator.Allocate1D<int>(stationCount);
        using var status = accelerator.Allocate1D<int>(1);
        moles.MemSetToZero();
        stations.MemSetToZero();
        figures.MemSetToZero();
        var problem = new RocketProblem(
            chamberPressure: batch.ChamberPressure[k], reactantEnthalpy: batch.ReactantEnthalpy[k],
            temperatureEstimate: batch.TemperatureEstimate[k], flow: batch.Flow[k], elementMoles: elements.View,
            exitValues: exitValues.View.SubView(0, batch.Exits), exitKinds: exitKinds.View.SubView(0, batch.Exits));
        var scratch = EquilibriumScratch.Slice(doubles.View, ints.View, speciesCount, elementCount);
        var result = new RocketResult(
            stations: stations.View, moles: moles.View, multipliers: multipliers.View, figures: figures.View,
            stationStatus: stationStatus.View, iterations: iterations.View, status: status.View);
        var view = buffers.View;
        RocketSolver.Solve(in view, in problem, in scratch, in result);
        return new HostRocketCase(stations.GetAsArray1D(), moles.GetAsArray1D(), figures.GetAsArray1D(),
                                  stationStatus.GetAsArray1D().Select(s => (CaseStatus)s).ToArray(), iterations.GetAsArray1D(), (CaseStatus)status.GetAsArray1D()[0]);
    }

    public static HostEquilibriumCase Equilibrium(Accelerator accelerator, SpeciesTableBuffers buffers, EquilibriumBatch batch, int k)
    {
        var table = buffers.Table;
        var speciesCount = table.SpeciesCount;
        var elementCount = table.ElementCount;
        using var elements = accelerator.Allocate1D(batch.ElementMoles.AsSpan(k * elementCount, elementCount).ToArray());
        using var doubles = accelerator.Allocate1D<double>(ScratchLayout.DoublesPerCase(speciesCount, elementCount));
        using var ints = accelerator.Allocate1D<int>(ScratchLayout.IntsPerCase(speciesCount, elementCount));
        using var moles = accelerator.Allocate1D<double>(speciesCount);
        using var multipliers = accelerator.Allocate1D<double>(elementCount);
        using var state = accelerator.Allocate1D<MixtureState>(1);
        using var status = accelerator.Allocate1D<int>(1);
        using var iterations = accelerator.Allocate1D<int>(1);
        moles.MemSetToZero();
        state.MemSetToZero();
        var problem = new EquilibriumProblem(batch.Kind[k], batch.Pressure[k], batch.Temperature[k], batch.Target[k], elements.View);
        var scratch = EquilibriumScratch.Slice(doubles.View, ints.View, speciesCount, elementCount);
        var result = new EquilibriumResult(moles.View, multipliers.View, state.View, status.View, iterations.View);
        var view = buffers.View;
        EquilibriumSolver.Solve(in view, in problem, in scratch, in result, false);
        return new HostEquilibriumCase(state.GetAsArray1D()[0], moles.GetAsArray1D(), (CaseStatus)status.GetAsArray1D()[0], iterations.GetAsArray1D()[0]);
    }

    public static HostTransportStation Transport(Accelerator accelerator, SpeciesTableBuffers species, TransportTableBuffers transport,
                                                  double temperature, double[] moles, int offset)
    {
        var speciesCount = species.Table.SpeciesCount;
        var elementCount = species.Table.ElementCount;
        using var molesBuffer = accelerator.Allocate1D(moles.AsSpan(offset, speciesCount).ToArray());
        using var doubles = accelerator.Allocate1D<double>(TransportLayout.DoublesPerCase(speciesCount, elementCount));
        using var ints = accelerator.Allocate1D<int>(TransportLayout.IntsPerCase(speciesCount, elementCount));
        using var figures = accelerator.Allocate1D<TransportFigures>(1);
        var scratch = TransportScratch.Slice(doubles.View, ints.View, speciesCount, elementCount);
        var speciesView = species.View;
        var transportView = transport.View;
        var status = TransportSolver.Evaluate(in speciesView, in transportView, temperature, molesBuffer.View, in scratch, figures.View);
        return new HostTransportStation(status, figures.GetAsArray1D()[0]);
    }
}

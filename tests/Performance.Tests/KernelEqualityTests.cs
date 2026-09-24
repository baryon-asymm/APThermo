using APThermo.Equilibrium;
using APThermo.Fixtures;
using APThermo.Harness;
using APThermo.Thermo;
using ILGPU;
using ILGPU.Runtime;

namespace APThermo.Performance.Tests;

/// <summary>The batch views of one kernel launch: structure of arrays, one case per thread, a fixed exit layout per batch.</summary>
public readonly struct RocketBatchViews(
    int exitCount, ArrayView<double> chamberPressures, ArrayView<double> reactantEnthalpies, ArrayView<int> flows,
    ArrayView<double> elementMoles, ArrayView<double> exitValues, ArrayView<int> exitKinds,
    ArrayView<double> scratchDoubles, ArrayView<int> scratchInts,
    ArrayView<MixtureState> stations, ArrayView<double> moles, ArrayView<double> multipliers, ArrayView<PerformanceFigures> figures,
    ArrayView<int> stationStatus, ArrayView<int> iterations, ArrayView<int> status)
{
    public readonly int ExitCount = exitCount;
    public readonly ArrayView<double> ChamberPressures = chamberPressures;
    public readonly ArrayView<double> ReactantEnthalpies = reactantEnthalpies;
    public readonly ArrayView<int> Flows = flows;
    public readonly ArrayView<double> ElementMoles = elementMoles;
    public readonly ArrayView<double> ExitValues = exitValues;
    public readonly ArrayView<int> ExitKinds = exitKinds;
    public readonly ArrayView<double> ScratchDoubles = scratchDoubles;
    public readonly ArrayView<int> ScratchInts = scratchInts;
    public readonly ArrayView<MixtureState> Stations = stations;
    public readonly ArrayView<double> Moles = moles;
    public readonly ArrayView<double> Multipliers = multipliers;
    public readonly ArrayView<PerformanceFigures> Figures = figures;
    public readonly ArrayView<int> StationStatus = stationStatus;
    public readonly ArrayView<int> Iterations = iterations;
    public readonly ArrayView<int> Status = status;
}

/// <summary>
/// One batch's buffers on the accelerator, released together: allocates and uploads the cases' inputs, their scratch and their
/// station rows, exactly as <see cref="RocketCase"/> does for one case. The batch counterpart of its owned buffers.
/// </summary>
internal sealed class RocketBatchBuffers : IDisposable
{
    private readonly List<IDisposable> _owned = [];

    public RocketBatchBuffers(Accelerator accelerator, SpeciesTable table, IReadOnlyList<RocketInputs> inputs)
    {
        var speciesCount = table.SpeciesCount;
        var elementCount = table.ElementCount;
        var exitCount = inputs[0].ExitCount;
        var count = inputs.Count;
        var stationCount = RocketLayout.StationCount(exitCount);
        var tableBuffers = Own(SpeciesTableBuffers.Upload(accelerator, table));
        var chamberPressures = Own(accelerator.Allocate1D(inputs.Select(i => i.ChamberPressure).ToArray()));
        var reactantEnthalpies = Own(accelerator.Allocate1D(inputs.Select(i => i.Mixture.ReactantEnthalpy).ToArray()));
        var flows = Own(accelerator.Allocate1D(inputs.Select(i => (int)i.Flow).ToArray()));
        var elementMoles = Own(accelerator.Allocate1D(inputs.SelectMany(i => i.Mixture.ElementMoles).ToArray()));
        var exitValues = Own(accelerator.Allocate1D(inputs.SelectMany(i => i.Exits.Values).ToArray()));
        var exitKinds = Own(accelerator.Allocate1D(inputs.SelectMany(i => i.Exits.Kinds.Select(k => (int)k)).ToArray()));
        var scratchDoubles = Own(accelerator.Allocate1D<double>((long)count * ScratchLayout.DoublesPerCase(speciesCount, elementCount)));
        var scratchInts = Own(accelerator.Allocate1D<int>((long)count * ScratchLayout.IntsPerCase(speciesCount, elementCount)));
        var multipliers = Own(accelerator.Allocate1D<double>((long)count * stationCount * elementCount));
        var iterations = Own(accelerator.Allocate1D<int>((long)count * stationCount));
        Stations = Own(accelerator.Allocate1D<MixtureState>((long)count * stationCount));
        Moles = Own(accelerator.Allocate1D<double>((long)count * stationCount * speciesCount));
        Figures = Own(accelerator.Allocate1D<PerformanceFigures>((long)count * stationCount));
        StationStatus = Own(accelerator.Allocate1D<int>((long)count * stationCount));
        Status = Own(accelerator.Allocate1D<int>(count));
        Moles.MemSetToZero();
        Stations.MemSetToZero();

        Table = tableBuffers.View;
        Views = new RocketBatchViews(
            exitCount: exitCount, chamberPressures: chamberPressures.View, reactantEnthalpies: reactantEnthalpies.View, flows: flows.View,
            elementMoles: elementMoles.View, exitValues: exitValues.View, exitKinds: exitKinds.View, scratchDoubles: scratchDoubles.View,
            scratchInts: scratchInts.View, stations: Stations.View, moles: Moles.View, multipliers: multipliers.View, figures: Figures.View,
            stationStatus: StationStatus.View, iterations: iterations.View, status: Status.View);
    }

    public SpeciesTableView Table { get; }

    public RocketBatchViews Views { get; }

    public MemoryBuffer1D<MixtureState, Stride1D.Dense> Stations { get; }

    public MemoryBuffer1D<double, Stride1D.Dense> Moles { get; }

    public MemoryBuffer1D<PerformanceFigures, Stride1D.Dense> Figures { get; }

    public MemoryBuffer1D<int, Stride1D.Dense> StationStatus { get; }

    public MemoryBuffer1D<int, Stride1D.Dense> Status { get; }

    public void Dispose()
    {
        for (var k = _owned.Count - 1; k >= 0; k--)
        {
            _owned[k].Dispose();
        }
    }

    /// <summary>Adds a buffer to the set this batch releases on Dispose, and returns it.</summary>
    private T Own<T>(T buffer)
        where T : IDisposable
    {
        _owned.Add(buffer);
        return buffer;
    }
}

/// <summary>L1: the rocket solver inside a CPU-accelerator kernel gives the same bits as the host call.</summary>
[Collection(CpuCollection.Name)]
public sealed class KernelEqualityTests(CpuFixture fixture)
{
    /// <summary>The families of rocket fixtures sharing one table and one exit layout, largest first; each is one batch.</summary>
    public static IEnumerable<object[]> Batches() => FixtureFamilies.Of(["rocket"], c => RocketInputs.Of(c).BatchKey);

    [Theory]
    [MemberData(nameof(Batches))]
    public void Kernel_and_host_give_the_same_bits(string key, int count, IReadOnlyList<CeaCase> cases)
    {
        Assert.Equal(count, cases.Count);
        var inputs = cases.Select(RocketInputs.Of).ToList();
        var table = SpeciesTable.Build(fixture.Database, inputs[0].System.Elements, inputs[0].System.Products);
        var host = inputs.Select(i => RocketHost.Solve(fixture.Accelerator, table, i)).ToList();

        using var buffers = new RocketBatchBuffers(fixture.Accelerator, table, inputs);
        var kernel = fixture.Accelerator.LoadAutoGroupedStreamKernel<Index1D, SpeciesTableView, RocketBatchViews>(SolveKernel);
        kernel(count, buffers.Table, buffers.Views);
        fixture.Accelerator.Synchronize();

        AssertSameBits(host, table, buffers, cases, key);
    }

    /// <summary>Every station's state, figures and moles, bit for bit, host against kernel; the fields come from reflection.</summary>
    private static void AssertSameBits(IReadOnlyList<RocketSolution> host, SpeciesTable table, RocketBatchBuffers buffers, IReadOnlyList<CeaCase> cases, string key)
    {
        var stationCount = RocketLayout.StationCount(buffers.Views.ExitCount);
        var speciesCount = table.SpeciesCount;
        var kernelStations = buffers.Stations.GetAsArray1D();
        var kernelMoles = buffers.Moles.GetAsArray1D();
        var kernelFigures = buffers.Figures.GetAsArray1D();
        var kernelStationStatus = buffers.StationStatus.GetAsArray1D();
        var kernelStatus = buffers.Status.GetAsArray1D();
        var stateFields = typeof(MixtureState).GetProperties();
        var figureFields = typeof(PerformanceFigures).GetProperties();
        for (var k = 0; k < host.Count; k++)
        {
            var label = $"{key} {cases[k].Kind}:{cases[k].Name}";
            Assert.True(host[k].Status == CaseStatus.Ok, $"{label}: host status {host[k].Status}");
            Assert.True((int)host[k].Status == kernelStatus[k], $"{label}: kernel status {(CaseStatus)kernelStatus[k]}");
            for (var s = 0; s < stationCount; s++)
            {
                var offset = k * stationCount + s;
                Assert.True((int)host[k].Outcome.StationStatus[s] == kernelStationStatus[offset], $"{label} station {s}: status differs");
                foreach (var field in stateFields)
                {
                    var a = (double)field.GetValue(host[k].Outcome.Stations[s])!;
                    var b = (double)field.GetValue(kernelStations[offset])!;
                    Assert.True(Bits.Same(a, b), $"{label} station {s}: {field.Name} host {a:R}, kernel {b:R}");
                }

                foreach (var field in figureFields)
                {
                    var a = (double)field.GetValue(host[k].Outcome.Figures[s])!;
                    var b = (double)field.GetValue(kernelFigures[offset])!;
                    Assert.True(Bits.Same(a, b), $"{label} station {s}: {field.Name} host {a:R}, kernel {b:R}");
                }

                for (var j = 0; j < speciesCount; j++)
                {
                    var a = host[k].Outcome.Moles[s * speciesCount + j];
                    var b = kernelMoles[(long)offset * speciesCount + j];
                    Assert.True(Bits.Same(a, b), $"{label} station {s}: moles of {table.Species[j]} host {a:R}, kernel {b:R}");
                }
            }
        }
    }

    private static void SolveKernel(Index1D index, SpeciesTableView table, RocketBatchViews batch)
    {
        var speciesCount = table.SpeciesCount;
        var elementCount = table.ElementCount;
        var exitCount = batch.ExitCount;
        var stationCount = RocketLayout.StationCount(exitCount);
        var doublesPerCase = ScratchLayout.DoublesPerCase(speciesCount, elementCount);
        var intsPerCase = ScratchLayout.IntsPerCase(speciesCount, elementCount);
        var problem = new RocketProblem(
            chamberPressure: batch.ChamberPressures[index], reactantEnthalpy: batch.ReactantEnthalpies[index],
            temperatureEstimate: 0.0, flow: (FlowModel)batch.Flows[index],
            elementMoles: batch.ElementMoles.SubView(index * elementCount, elementCount),
            exitValues: batch.ExitValues.SubView(index * exitCount, exitCount),
            exitKinds: batch.ExitKinds.SubView(index * exitCount, exitCount));
        var scratch = EquilibriumScratch.Slice(batch.ScratchDoubles.SubView(index * doublesPerCase, doublesPerCase),
                                               batch.ScratchInts.SubView(index * intsPerCase, intsPerCase), speciesCount, elementCount);
        var result = new RocketResult(
            stations: batch.Stations.SubView(index * stationCount, stationCount),
            moles: batch.Moles.SubView(index * stationCount * speciesCount, stationCount * speciesCount),
            multipliers: batch.Multipliers.SubView(index * stationCount * elementCount, stationCount * elementCount),
            figures: batch.Figures.SubView(index * stationCount, stationCount),
            stationStatus: batch.StationStatus.SubView(index * stationCount, stationCount),
            iterations: batch.Iterations.SubView(index * stationCount, stationCount),
            status: batch.Status.SubView(index, 1));
        RocketSolver.Solve(in table, in problem, in scratch, in result);
    }
}

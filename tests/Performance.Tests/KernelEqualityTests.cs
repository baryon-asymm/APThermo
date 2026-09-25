using APThermo.Equilibrium;
using APThermo.Fixtures;
using APThermo.Harness;
using APThermo.Thermo;
using ILGPU;
using ILGPU.Runtime;

namespace APThermo.Performance.Tests;

/// <summary>
/// The batch views of one kernel launch: structure of arrays, one case per thread, a fixed exit layout per batch. Internal:
/// ILGPU 1.5.3 needs only <c>[assembly: InternalsVisibleTo("ILGPURuntime")]</c> on this assembly to reach it as a kernel
/// parameter type (root BOOT.md, Delivery: Tree contracts), and no consumer outside this node has a reason to name it
/// (CA1515). A record struct gives it value equality without a hand-written <c>Equals</c>/<c>==</c> (CA1815).
/// </summary>
internal readonly record struct RocketBatchViews(
    int ExitCount, ArrayView<double> ChamberPressures, ArrayView<double> ReactantEnthalpies, ArrayView<int> Flows,
    ArrayView<double> ElementMoles, ArrayView<double> ExitValues, ArrayView<int> ExitKinds,
    ArrayView<double> ScratchDoubles, ArrayView<int> ScratchInts,
    ArrayView<MixtureState> Stations, ArrayView<double> Moles, ArrayView<double> Multipliers, ArrayView<PerformanceFigures> Figures,
    ArrayView<int> StationStatus, ArrayView<int> Iterations, ArrayView<int> Status);

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
            ExitCount: exitCount, ChamberPressures: chamberPressures.View, ReactantEnthalpies: reactantEnthalpies.View, Flows: flows.View,
            ElementMoles: elementMoles.View, ExitValues: exitValues.View, ExitKinds: exitKinds.View, ScratchDoubles: scratchDoubles.View,
            ScratchInts: scratchInts.View, Stations: Stations.View, Moles: Moles.View, Multipliers: multipliers.View, Figures: Figures.View,
            StationStatus: StationStatus.View, Iterations: iterations.View, Status: Status.View);
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
[Collection(CpuFixture.CollectionName)]
public sealed class KernelEqualityTests
{
    /// <summary>The kinds whose fixture cases are grouped into batches: one table and exit layout per batch.</summary>
    private static readonly string[] Kinds = ["rocket"];

    /// <summary>The batch key function: rocket fixtures sharing one table and one exit layout share a batch.</summary>
    private static string BatchKeyOf(CeaCase c) => RocketInputs.Of(c).BatchKey;

    /// <summary>The key and case count of every batch, largest first; <see cref="KernelAndHostGiveTheSameBits"/> reads the cases back.</summary>
    public static TheoryData<string, int> Batches()
    {
        var data = new TheoryData<string, int>();
        foreach (var family in FixtureFamilies.Keys(Kinds, BatchKeyOf))
        {
            data.Add(family.Key, family.Count);
        }

        return data;
    }

    /// <summary>The rocket solver inside a CPU-accelerator kernel gives the same bits as the host call, for one batch.</summary>
    [Theory]
    [MemberData(nameof(Batches))]
    public void KernelAndHostGiveTheSameBits(string key, int count)
    {
        var cases = FixtureFamilies.CasesOf(Kinds, BatchKeyOf, key);
        Assert.Equal(count, cases.Count);
        var inputs = cases.Select(RocketInputs.Of).ToList();
        var table = SpeciesTable.Build(CpuFixture.Shared.Database, inputs[0].System.Elements, inputs[0].System.Products);
        var host = inputs.Select(i => RocketHost.Solve(CpuFixture.Shared.Accelerator, table, i)).ToList();

        using var buffers = new RocketBatchBuffers(CpuFixture.Shared.Accelerator, table, inputs);
        var kernel = CpuFixture.Shared.Accelerator.LoadAutoGroupedStreamKernel<Index1D, SpeciesTableView, RocketBatchViews>(SolveKernel);
        kernel(count, buffers.Table, buffers.Views);
        CpuFixture.Shared.Accelerator.Synchronize();

        AssertSameBits(host, table, buffers, cases, key);
    }

    /// <summary>Every station's state, figures and moles, bit for bit, host against kernel; the fields come from reflection.</summary>
    private static void AssertSameBits(List<RocketSolution> host, SpeciesTable table, RocketBatchBuffers buffers, IReadOnlyList<CeaCase> cases, string key)
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

using AerospacePropellantThermodynamics.Equilibrium;
using AerospacePropellantThermodynamics.Thermo;
using ILGPU;
using ILGPU.Runtime;

namespace AerospacePropellantThermodynamics.Performance.Tests;

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
/// One batch's buffers on the accelerator, released together: what <see cref="KernelEqualityTests.Fill"/> allocates and uploads,
/// and the five of them the test later downloads. The batch counterpart of <see cref="RocketCase"/>'s owned buffers.
/// </summary>
internal sealed class RocketBatchBuffers : IDisposable
{
    private readonly List<IDisposable> _owned = [];

    public SpeciesTableView Table { get; set; }

    public RocketBatchViews Views { get; set; }

    public MemoryBuffer1D<MixtureState, Stride1D.Dense> Stations { get; set; } = null!;

    public MemoryBuffer1D<double, Stride1D.Dense> Moles { get; set; } = null!;

    public MemoryBuffer1D<PerformanceFigures, Stride1D.Dense> Figures { get; set; } = null!;

    public MemoryBuffer1D<int, Stride1D.Dense> StationStatus { get; set; } = null!;

    public MemoryBuffer1D<int, Stride1D.Dense> Status { get; set; } = null!;

    /// <summary>Adds a buffer to the set this batch releases on Dispose, and returns it.</summary>
    public T Own<T>(T buffer)
        where T : IDisposable
    {
        _owned.Add(buffer);
        return buffer;
    }

    public void Dispose()
    {
        for (var k = _owned.Count - 1; k >= 0; k--)
        {
            _owned[k].Dispose();
        }
    }
}

/// <summary>L1: the rocket solver inside a CPU-accelerator kernel gives the same bits as the host call.</summary>
[Collection(CpuCollection.Name)]
public sealed class KernelEqualityTests(CpuFixture fixture)
{
    /// <summary>The families of rocket fixtures sharing one table and one exit layout, largest first; each is one batch.</summary>
    public static IEnumerable<object[]> Batches()
    {
        var families = new Dictionary<string, List<string>>();
        foreach (var row in RocketHost.Cases())
        {
            var name = (string)row[0];
            var key = RocketInputs.Of(RocketHost.Load(name)).BatchKey;
            if (!families.TryGetValue(key, out var list))
            {
                families[key] = list = [];
            }

            list.Add(name);
        }

        foreach (var family in families.Values.OrderByDescending(f => f.Count).ThenBy(f => f[0], StringComparer.Ordinal))
        {
            yield return [family[0], family.Count, family.ToArray()];
        }
    }

    [Theory]
    [MemberData(nameof(Batches))]
    public void Kernel_and_host_give_the_same_bits(string family, int count, string[] members)
    {
        Assert.Equal(count, members.Length);
        Assert.Equal(family, members[0]);
        var inputs = members.Select(m => RocketInputs.Of(RocketHost.Load(m))).ToList();
        var table = SpeciesTable.Build(fixture.Database, inputs[0].Elements, inputs[0].Products);
        var host = inputs.Select(i => RocketHost.Solve(fixture.Accelerator, table, i)).ToList();

        using var buffers = Fill(fixture.Accelerator, table, inputs);
        var kernel = fixture.Accelerator.LoadAutoGroupedStreamKernel<Index1D, SpeciesTableView, RocketBatchViews>(SolveKernel);
        kernel(count, buffers.Table, buffers.Views);
        fixture.Accelerator.Synchronize();

        AssertSameBits(host, table, buffers, members);
    }

    /// <summary>Allocates and uploads one batch: the cases' inputs, their scratch and their station rows.</summary>
    private static RocketBatchBuffers Fill(Accelerator accelerator, SpeciesTable table, IReadOnlyList<RocketInputs> inputs)
    {
        var buffers = new RocketBatchBuffers();
        var speciesCount = table.SpeciesCount;
        var elementCount = table.ElementCount;
        var exitCount = inputs[0].ExitCount;
        var count = inputs.Count;
        var stationCount = RocketLayout.StationCount(exitCount);
        var tableBuffers = buffers.Own(SpeciesTableBuffers.Upload(accelerator, table));
        var chamberPressures = buffers.Own(accelerator.Allocate1D(inputs.Select(i => i.ChamberPressure).ToArray()));
        var reactantEnthalpies = buffers.Own(accelerator.Allocate1D(inputs.Select(i => i.ReactantEnthalpy).ToArray()));
        var flows = buffers.Own(accelerator.Allocate1D(inputs.Select(i => (int)i.Flow).ToArray()));
        var elementMoles = buffers.Own(accelerator.Allocate1D(inputs.SelectMany(i => i.ElementMoles).ToArray()));
        var exitValues = buffers.Own(accelerator.Allocate1D(inputs.SelectMany(i => i.ExitValues).ToArray()));
        var exitKinds = buffers.Own(accelerator.Allocate1D(inputs.SelectMany(i => i.ExitKinds.Select(k => (int)k)).ToArray()));
        var scratchDoubles = buffers.Own(accelerator.Allocate1D<double>((long)count * ScratchLayout.DoublesPerCase(speciesCount, elementCount)));
        var scratchInts = buffers.Own(accelerator.Allocate1D<int>((long)count * ScratchLayout.IntsPerCase(speciesCount, elementCount)));
        var stations = buffers.Own(accelerator.Allocate1D<MixtureState>((long)count * stationCount));
        var moles = buffers.Own(accelerator.Allocate1D<double>((long)count * stationCount * speciesCount));
        var multipliers = buffers.Own(accelerator.Allocate1D<double>((long)count * stationCount * elementCount));
        var figures = buffers.Own(accelerator.Allocate1D<PerformanceFigures>((long)count * stationCount));
        var stationStatus = buffers.Own(accelerator.Allocate1D<int>((long)count * stationCount));
        var iterations = buffers.Own(accelerator.Allocate1D<int>((long)count * stationCount));
        var status = buffers.Own(accelerator.Allocate1D<int>(count));
        moles.MemSetToZero();
        stations.MemSetToZero();

        buffers.Table = tableBuffers.View;
        buffers.Stations = stations;
        buffers.Moles = moles;
        buffers.Figures = figures;
        buffers.StationStatus = stationStatus;
        buffers.Status = status;
        buffers.Views = new RocketBatchViews(exitCount, chamberPressures.View, reactantEnthalpies.View, flows.View, elementMoles.View,
                                             exitValues.View, exitKinds.View, scratchDoubles.View, scratchInts.View, stations.View,
                                             moles.View, multipliers.View, figures.View, stationStatus.View, iterations.View, status.View);
        return buffers;
    }

    /// <summary>Every station's state, figures and moles, bit for bit, host against kernel; the fields come from reflection.</summary>
    private static void AssertSameBits(IReadOnlyList<RocketSolution> host, SpeciesTable table, RocketBatchBuffers buffers, string[] members)
    {
        var stationCount = RocketLayout.StationCount(buffers.Views.ExitCount);
        var speciesCount = table.SpeciesCount;
        var kernelStations = buffers.Stations.GetAsArray1D();
        var kernelMoles = buffers.Moles.GetAsArray1D();
        var kernelFigures = buffers.Figures.GetAsArray1D();
        var kernelStationStatus = buffers.StationStatus.GetAsArray1D();
        var kernelStatus = buffers.Status.GetAsArray1D();
        var stateFields = typeof(MixtureState).GetFields();
        var figureFields = typeof(PerformanceFigures).GetFields();
        for (var k = 0; k < host.Count; k++)
        {
            Assert.True(host[k].Status == CaseStatus.Ok, $"{members[k]}: host status {host[k].Status}");
            Assert.True((int)host[k].Status == kernelStatus[k], $"{members[k]}: kernel status {(CaseStatus)kernelStatus[k]}");
            for (var s = 0; s < stationCount; s++)
            {
                var offset = k * stationCount + s;
                Assert.True((int)host[k].StationStatus[s] == kernelStationStatus[offset], $"{members[k]} station {s}: status differs");
                foreach (var field in stateFields)
                {
                    var a = (double)field.GetValue(host[k].Stations[s])!;
                    var b = (double)field.GetValue(kernelStations[offset])!;
                    Assert.True(SameBits(a, b), $"{members[k]} station {s}: {field.Name} host {a:R}, kernel {b:R}");
                }

                foreach (var field in figureFields)
                {
                    var a = (double)field.GetValue(host[k].Figures[s])!;
                    var b = (double)field.GetValue(kernelFigures[offset])!;
                    Assert.True(SameBits(a, b), $"{members[k]} station {s}: {field.Name} host {a:R}, kernel {b:R}");
                }

                for (var j = 0; j < speciesCount; j++)
                {
                    var a = host[k].Moles[s * speciesCount + j];
                    var b = kernelMoles[(long)offset * speciesCount + j];
                    Assert.True(SameBits(a, b), $"{members[k]} station {s}: moles of {table.Species[j]} host {a:R}, kernel {b:R}");
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
        var problem = new RocketProblem(batch.ChamberPressures[index], batch.ReactantEnthalpies[index], 0.0, (FlowModel)batch.Flows[index],
                                        batch.ElementMoles.SubView(index * elementCount, elementCount),
                                        batch.ExitValues.SubView(index * exitCount, exitCount),
                                        batch.ExitKinds.SubView(index * exitCount, exitCount));
        var scratch = EquilibriumScratch.Slice(batch.ScratchDoubles.SubView(index * doublesPerCase, doublesPerCase),
                                               batch.ScratchInts.SubView(index * intsPerCase, intsPerCase), speciesCount, elementCount);
        var result = new RocketResult(batch.Stations.SubView(index * stationCount, stationCount),
                                      batch.Moles.SubView(index * stationCount * speciesCount, stationCount * speciesCount),
                                      batch.Multipliers.SubView(index * stationCount * elementCount, stationCount * elementCount),
                                      batch.Figures.SubView(index * stationCount, stationCount),
                                      batch.StationStatus.SubView(index * stationCount, stationCount),
                                      batch.Iterations.SubView(index * stationCount, stationCount),
                                      batch.Status.SubView(index, 1));
        RocketSolver.Solve(in table, in problem, in scratch, in result);
    }

    private static bool SameBits(double a, double b) => BitConverter.DoubleToInt64Bits(a) == BitConverter.DoubleToInt64Bits(b);
}

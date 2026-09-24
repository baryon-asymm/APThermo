using APThermo.Fixtures;
using APThermo.Harness;
using APThermo.Thermo;
using ILGPU;
using ILGPU.Runtime;

namespace APThermo.Transport.Tests;

/// <summary>The batch views of one kernel launch: one station per thread.</summary>
public readonly struct TransportBatchViews(
    ArrayView<double> temperatures, ArrayView<double> moles, ArrayView<double> scratchDoubles, ArrayView<int> scratchInts,
    ArrayView<TransportFigures> figures, ArrayView<int> status)
{
    public readonly ArrayView<double> Temperatures = temperatures;
    public readonly ArrayView<double> Moles = moles;
    public readonly ArrayView<double> ScratchDoubles = scratchDoubles;
    public readonly ArrayView<int> ScratchInts = scratchInts;
    public readonly ArrayView<TransportFigures> Figures = figures;
    public readonly ArrayView<int> Status = status;
}

/// <summary>L1: the evaluation inside a CPU-accelerator kernel gives the same bits as the host call.</summary>
[Collection(CpuCollection.Name)]
public sealed class KernelEqualityTests(CpuFixture fixture)
{
    /// <summary>The families of rocket fixtures sharing one table, largest first, kept to their members with transport; each is one batch of all their stations.</summary>
    public static IEnumerable<object[]> Batches()
    {
        foreach (var row in FixtureFamilies.Of(["rocket"], TransportHost.TableKey))
        {
            var cases = ((IReadOnlyList<CeaCase>)row[2]).Where(TransportHost.HasTransport).ToList();
            if (cases.Count > 0)
            {
                yield return [row[0], cases.Count, cases];
            }
        }
    }

    [Theory]
    [MemberData(nameof(Batches))]
    public void Kernel_and_host_give_the_same_bits(string key, int count, IReadOnlyList<CeaCase> cases)
    {
        Assert.Equal(count, cases.Count);
        var (table, transport) = TransportHost.TablesOf(fixture, cases[0]);
        var stations = cases.SelectMany(c => TransportHost.StationsWithTransport(c).Select(s => (Case: c.Name, Station: s))).ToList();
        var temperatures = stations.Select(s => s.Station.GetProperty("temperature").GetDouble()).ToArray();
        var moles = stations.Select(s => TransportHost.MolesOf(table, s.Station)).ToList();

        var accelerator = fixture.Accelerator;
        using var speciesBuffers = SpeciesTableBuffers.Upload(accelerator, table);
        using var transportBuffers = TransportTableBuffers.Upload(accelerator, transport);
        var host = moles.Select((m, i) => TransportHost.Evaluate(accelerator, speciesBuffers, transportBuffers, temperatures[i], m)).ToList();

        var speciesCount = table.SpeciesCount;
        var elementCount = table.ElementCount;
        var batchSize = stations.Count;
        using var temperatureBuffer = accelerator.Allocate1D(temperatures);
        using var molesBuffer = accelerator.Allocate1D(moles.SelectMany(m => m).ToArray());
        using var scratchDoubles = accelerator.Allocate1D<double>((long)batchSize * TransportLayout.DoublesPerCase(speciesCount, elementCount));
        using var scratchInts = accelerator.Allocate1D<int>((long)batchSize * TransportLayout.IntsPerCase(speciesCount, elementCount));
        using var figures = accelerator.Allocate1D<TransportFigures>(batchSize);
        using var status = accelerator.Allocate1D<int>(batchSize);
        var batch = new TransportBatchViews(temperatureBuffer.View, molesBuffer.View, scratchDoubles.View, scratchInts.View, figures.View, status.View);
        var kernel = accelerator.LoadAutoGroupedStreamKernel<Index1D, SpeciesTableView, TransportTableView, TransportBatchViews>(EvaluateKernel);
        kernel(batchSize, speciesBuffers.View, transportBuffers.View, batch);
        accelerator.Synchronize();

        var kernelFigures = figures.GetAsArray1D();
        var kernelStatus = status.GetAsArray1D();
        var fields = typeof(TransportFigures).GetProperties();
        for (var i = 0; i < batchSize; i++)
        {
            var label = $"{key} {stations[i].Case} {stations[i].Station.GetProperty("station").GetString()}";
            Assert.True((int)host[i].Status == kernelStatus[i], $"{label}: host status {host[i].Status}, kernel {(CaseStatus)kernelStatus[i]}");
            foreach (var field in fields)
            {
                var a = field.GetValue(host[i].Figures)!;
                var b = field.GetValue(kernelFigures[i])!;
                var same = a is double x && b is double y ? Bits.Same(x, y) : a.Equals(b);
                Assert.True(same, $"{label}: {field.Name} host {a}, kernel {b}");
            }
        }
    }

    private static void EvaluateKernel(Index1D index, SpeciesTableView species, TransportTableView transport, TransportBatchViews batch)
    {
        var speciesCount = species.SpeciesCount;
        var elementCount = species.ElementCount;
        var doublesPerCase = TransportLayout.DoublesPerCase(speciesCount, elementCount);
        var intsPerCase = TransportLayout.IntsPerCase(speciesCount, elementCount);
        var scratch = TransportScratch.Slice(batch.ScratchDoubles.SubView(index * doublesPerCase, doublesPerCase),
                                             batch.ScratchInts.SubView(index * intsPerCase, intsPerCase), speciesCount, elementCount);
        batch.Status[index] = (int)TransportSolver.Evaluate(in species, in transport, batch.Temperatures[index],
                                                             batch.Moles.SubView(index * speciesCount, speciesCount), in scratch,
                                                             batch.Figures.SubView(index, 1));
    }
}

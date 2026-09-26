using APThermo.Fixtures;
using APThermo.Harness;
using APThermo.Thermo;
using ILGPU;
using ILGPU.Runtime;

namespace APThermo.Transport.Tests;

/// <summary>
/// The batch views of one kernel launch: one station per thread. Internal: ILGPU 1.5.3 needs only
/// <c>[assembly: InternalsVisibleTo("ILGPURuntime")]</c> on this assembly to reach it as a kernel parameter type
/// (root BOOT.md, Delivery: Tree contracts), and no consumer outside this node has a reason to name it (CA1515). A
/// record struct gives it value equality without a hand-written <c>Equals</c>/<c>==</c> (CA1815).
/// </summary>
internal readonly record struct TransportBatchViews(
    ArrayView<double> Temperatures, ArrayView<double> Moles, ArrayView<double> ScratchDoubles, ArrayView<int> ScratchInts,
    ArrayView<TransportFigures> Figures, ArrayView<int> Status);

/// <summary>L1: the evaluation inside a CPU-accelerator kernel gives the same bits as the host call.</summary>
[Collection(CpuFixture.CollectionName)]
public sealed class KernelEqualityTests
{
    /// <summary>The kinds whose fixture cases are grouped into batches: one table per batch.</summary>
    private static readonly string[] Kinds = ["rocket"];

    /// <summary>The cases of the family named <paramref name="key"/> that carry transport data.</summary>
    private static List<CeaCase> TransportCasesOf(string key) =>
        [.. FixtureFamilies.CasesOf(Kinds, TransportHost.TableKey, key).Where(TransportHost.HasTransport)];

    /// <summary>
    /// The key and case count of every family of rocket fixtures sharing one table, largest first, kept to their members
    /// with transport; each is one batch of all their stations. <see cref="KernelAndHostGiveTheSameBits"/> reads the cases
    /// back through <see cref="TransportCasesOf"/>.
    /// </summary>
    public static TheoryData<string, int> Batches()
    {
        var data = new TheoryData<string, int>();
        foreach (var family in FixtureFamilies.Keys(Kinds, TransportHost.TableKey))
        {
            var count = TransportCasesOf(family.Key).Count;
            if (count > 0)
            {
                data.Add(family.Key, count);
            }
        }

        return data;
    }

    /// <summary>The evaluation inside a CPU-accelerator kernel gives the same bits as the host call, for one batch.</summary>
    [Theory]
    [MemberData(nameof(Batches))]
    public void KernelAndHostGiveTheSameBits(string key, int count)
    {
        var cases = TransportCasesOf(key);
        Assert.Equal(count, cases.Count);
        var (table, transport) = TransportHost.TablesOf(CpuFixture.Shared, cases[0]);
        var stations = cases.SelectMany(c => TransportHost.StationsWithTransport(c).Select(s => (Case: c.Name, Station: s))).ToList();
        var temperatures = stations.Select(s => s.Station.GetProperty("temperature").GetDouble()).ToArray();
        var moles = stations.Select(s => TransportHost.MolesOf(table, s.Station)).ToList();

        var accelerator = CpuFixture.Shared.Accelerator;
        using var speciesBuffers = SpeciesTableBuffers.Upload(accelerator, table);
        using var transportBuffers = TransportTableBuffers.Upload(accelerator, transport);
        var host = moles.Select((m, i) => TransportHost.Evaluate(accelerator, speciesBuffers, transportBuffers, temperatures[i], m)).ToList();

        var speciesCount = table.SpeciesCount;
        var elementCount = table.ElementCount;
        var batchSize = stations.Count;
        using var temperatureBuffer = accelerator.Allocate1D(temperatures);
        using var molesBuffer = accelerator.Allocate1D(moles.SelectMany(m => m).ToArray());
        using var scratchDoubles = accelerator.Allocate1D<double>((long)batchSize * TransportLayout.DoublesPerCase(elementCount));
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
        var doublesPerCase = TransportLayout.DoublesPerCase(elementCount);
        var intsPerCase = TransportLayout.IntsPerCase(speciesCount, elementCount);
        var scratch = TransportScratch.Slice(batch.ScratchDoubles.SubView(index * doublesPerCase, doublesPerCase),
                                             batch.ScratchInts.SubView(index * intsPerCase, intsPerCase), speciesCount, elementCount);
        batch.Status[index] = (int)TransportSolver.Evaluate(in species, in transport, batch.Temperatures[index],
                                                             batch.Moles.SubView(index * speciesCount, speciesCount), in scratch,
                                                             batch.Figures.SubView(index, 1));
    }
}

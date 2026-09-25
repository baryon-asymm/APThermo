using APThermo.Fixtures;
using APThermo.Harness;
using APThermo.Thermo;
using ILGPU;
using ILGPU.Runtime;

namespace APThermo.Equilibrium.Tests;

/// <summary>
/// The batch views of one kernel launch: structure of arrays, one case per thread. Internal: ILGPU 1.5.3 needs only
/// <c>[assembly: InternalsVisibleTo("ILGPURuntime")]</c> on this assembly to reach it as a kernel parameter type
/// (root BOOT.md, Delivery: Tree contracts), and no consumer outside this node has a reason to name it (CA1515). A
/// record struct gives it value equality without a hand-written <c>Equals</c>/<c>==</c> (CA1815).
/// </summary>
internal readonly record struct BatchViews(
    ArrayView<int> Kinds, ArrayView<double> Pressures, ArrayView<double> Temperatures, ArrayView<double> Targets,
    ArrayView<double> ElementMoles, ArrayView<double> ScratchDoubles, ArrayView<int> ScratchInts,
    ArrayView<double> Moles, ArrayView<double> Multipliers, ArrayView<MixtureState> States,
    ArrayView<int> Statuses, ArrayView<int> Iterations);

/// <summary>What the kernel wrote for one batch, downloaded once, so that the host call and the kernel call may be compared.</summary>
internal readonly record struct KernelBatchResult(double[] Moles, double[] Multipliers, MixtureState[] States, int[] Statuses, int[] Iterations);

/// <summary>L1: the solver inside a CPU-accelerator kernel gives the same bits as the host call.</summary>
[Collection(CpuFixture.CollectionName)]
public sealed class KernelEqualityTests
{
    /// <summary>The kinds whose fixture cases are grouped into batches: one table (elements and products) per batch.</summary>
    private static readonly string[] Kinds = ["tp", "hp", "sp"];

    /// <summary>The key and case count of every batch, largest first; <see cref="KernelAndHostGiveTheSameBits"/> reads the cases back.</summary>
    public static TheoryData<string, int> Batches()
    {
        var data = new TheoryData<string, int>();
        foreach (var family in FixtureFamilies.Keys(Kinds, HostSolver.TableKey))
        {
            data.Add(family.Key, family.Count);
        }

        return data;
    }

    /// <summary>The solver inside a CPU-accelerator kernel gives the same bits as the host call, for one batch.</summary>
    [Theory]
    [MemberData(nameof(Batches))]
    public void KernelAndHostGiveTheSameBits(string key, int count)
    {
        var cases = FixtureFamilies.CasesOf(Kinds, HostSolver.TableKey, key);
        Assert.Equal(count, cases.Count);
        var table = HostSolver.BuildTable(CpuFixture.Shared.Database, cases[0]);
        var host = cases.Select(c => HostSolver.Solve(CpuFixture.Shared.Accelerator, HostSolver.Of(table, c))).ToList();

        var kernel = FillAndLaunch(CpuFixture.Shared.Accelerator, table, cases, count);
        AssertSameBits(host, kernel, table, cases, key);
    }

    /// <summary>Uploads the table and every case of the batch, launches the solve kernel, and downloads what it wrote.</summary>
    private static KernelBatchResult FillAndLaunch(Accelerator accelerator, SpeciesTable table, IReadOnlyList<CeaCase> cases, int count)
    {
        var speciesCount = table.SpeciesCount;
        var elementCount = table.ElementCount;
        using var buffers = SpeciesTableBuffers.Upload(accelerator, table);
        using var kinds = accelerator.Allocate1D(cases.Select(c => (int)HostSolver.KindOf(c)).ToArray());
        using var pressures = accelerator.Allocate1D(cases.Select(HostSolver.PressureOf).ToArray());
        using var temperatures = accelerator.Allocate1D(cases.Select(HostSolver.TemperatureOf).ToArray());
        using var targets = accelerator.Allocate1D(cases.Select(HostSolver.TargetOf).ToArray());
        using var elementMoles = accelerator.Allocate1D(cases.SelectMany(HostSolver.ElementMolesOf).ToArray());
        using var scratchDoubles = accelerator.Allocate1D<double>((long)count * ScratchLayout.DoublesPerCase(speciesCount, elementCount));
        using var scratchInts = accelerator.Allocate1D<int>((long)count * ScratchLayout.IntsPerCase(speciesCount, elementCount));
        using var moles = accelerator.Allocate1D<double>((long)count * speciesCount);
        using var multipliers = accelerator.Allocate1D<double>((long)count * elementCount);
        using var states = accelerator.Allocate1D<MixtureState>(count);
        using var statuses = accelerator.Allocate1D<int>(count);
        using var iterations = accelerator.Allocate1D<int>(count);
        moles.MemSetToZero();

        var batch = new BatchViews(
            Kinds: kinds.View, Pressures: pressures.View, Temperatures: temperatures.View, Targets: targets.View,
            ElementMoles: elementMoles.View, ScratchDoubles: scratchDoubles.View, ScratchInts: scratchInts.View,
            Moles: moles.View, Multipliers: multipliers.View, States: states.View, Statuses: statuses.View, Iterations: iterations.View);
        var kernel = accelerator.LoadAutoGroupedStreamKernel<Index1D, SpeciesTableView, BatchViews>(SolveKernel);
        kernel(count, buffers.View, batch);
        accelerator.Synchronize();

        return new KernelBatchResult(moles.GetAsArray1D(), multipliers.GetAsArray1D(), states.GetAsArray1D(), statuses.GetAsArray1D(), iterations.GetAsArray1D());
    }

    /// <summary>Every status, iteration count, mole, multiplier and state field of the batch, host against kernel, bit for bit.</summary>
    private static void AssertSameBits(List<HostSolution> host, KernelBatchResult kernel, SpeciesTable table, IReadOnlyList<CeaCase> cases, string key)
    {
        var speciesCount = table.SpeciesCount;
        var elementCount = table.ElementCount;
        var fields = typeof(MixtureState).GetProperties();
        for (var k = 0; k < host.Count; k++)
        {
            var label = $"{key} {cases[k].Kind}:{cases[k].Name}";
            Assert.True(host[k].Status == CaseStatus.Ok, $"{label}: host status {host[k].Status}");
            Assert.True((int)host[k].Status == kernel.Statuses[k], $"{label}: kernel status {(CaseStatus)kernel.Statuses[k]}");
            Assert.True(host[k].Iterations == kernel.Iterations[k], $"{label}: iterations host {host[k].Iterations}, kernel {kernel.Iterations[k]}");
            for (var j = 0; j < speciesCount; j++)
            {
                Assert.True(Bits.Same(host[k].Moles[j], kernel.Moles[k * speciesCount + j]),
                            $"{label}: moles of {table.Species[j]} host {host[k].Moles[j]:R}, kernel {kernel.Moles[k * speciesCount + j]:R}");
            }

            for (var i = 0; i < elementCount; i++)
            {
                Assert.True(Bits.Same(host[k].Multipliers[i], kernel.Multipliers[k * elementCount + i]), $"{label}: multiplier of {table.Elements[i]} differs");
            }

            foreach (var field in fields)
            {
                var a = (double)field.GetValue(host[k].State)!;
                var b = (double)field.GetValue(kernel.States[k])!;
                Assert.True(Bits.Same(a, b), $"{label}: {field.Name} host {a:R}, kernel {b:R}");
            }
        }
    }

    private static void SolveKernel(Index1D index, SpeciesTableView table, BatchViews batch)
    {
        var speciesCount = table.SpeciesCount;
        var elementCount = table.ElementCount;
        var doublesPerCase = ScratchLayout.DoublesPerCase(speciesCount, elementCount);
        var intsPerCase = ScratchLayout.IntsPerCase(speciesCount, elementCount);
        var problem = new EquilibriumProblem((ProblemKind)batch.Kinds[index], batch.Pressures[index], batch.Temperatures[index],
                                             batch.Targets[index], batch.ElementMoles.SubView(index * elementCount, elementCount));
        var scratch = EquilibriumScratch.Slice(batch.ScratchDoubles.SubView(index * doublesPerCase, doublesPerCase),
                                               batch.ScratchInts.SubView(index * intsPerCase, intsPerCase), speciesCount, elementCount);
        var result = new EquilibriumResult(batch.Moles.SubView(index * speciesCount, speciesCount),
                                           batch.Multipliers.SubView(index * elementCount, elementCount),
                                           batch.States.SubView(index, 1), batch.Statuses.SubView(index, 1), batch.Iterations.SubView(index, 1));
        EquilibriumSolver.Solve(in table, in problem, in scratch, in result, false);
    }
}

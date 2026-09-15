using APThermo.Fixtures;
using APThermo.Harness;
using APThermo.Thermo;
using ILGPU;
using ILGPU.Runtime;

namespace APThermo.Equilibrium.Tests;

/// <summary>The batch views of one kernel launch: structure of arrays, one case per thread.</summary>
public readonly struct BatchViews(
    ArrayView<int> kinds, ArrayView<double> pressures, ArrayView<double> temperatures, ArrayView<double> targets,
    ArrayView<double> elementMoles, ArrayView<double> scratchDoubles, ArrayView<int> scratchInts,
    ArrayView<double> moles, ArrayView<double> multipliers, ArrayView<MixtureState> states,
    ArrayView<int> statuses, ArrayView<int> iterations)
{
    public readonly ArrayView<int> Kinds = kinds;
    public readonly ArrayView<double> Pressures = pressures;
    public readonly ArrayView<double> Temperatures = temperatures;
    public readonly ArrayView<double> Targets = targets;
    public readonly ArrayView<double> ElementMoles = elementMoles;
    public readonly ArrayView<double> ScratchDoubles = scratchDoubles;
    public readonly ArrayView<int> ScratchInts = scratchInts;
    public readonly ArrayView<double> Moles = moles;
    public readonly ArrayView<double> Multipliers = multipliers;
    public readonly ArrayView<MixtureState> States = states;
    public readonly ArrayView<int> Statuses = statuses;
    public readonly ArrayView<int> Iterations = iterations;
}

/// <summary>What the kernel wrote for one batch, downloaded once, so that the host call and the kernel call may be compared.</summary>
internal readonly record struct KernelBatchResult(double[] Moles, double[] Multipliers, MixtureState[] States, int[] Statuses, int[] Iterations);

/// <summary>L1: the solver inside a CPU-accelerator kernel gives the same bits as the host call.</summary>
[Collection(CpuCollection.Name)]
public sealed class KernelEqualityTests(CpuFixture fixture)
{
    /// <summary>The families of fixture cases sharing one table (elements and products), largest first; each is one batch.</summary>
    public static IEnumerable<object[]> Batches() => FixtureFamilies.Of(["tp", "hp", "sp"], HostSolver.TableKey);

    [Theory]
    [MemberData(nameof(Batches))]
    public void Kernel_and_host_give_the_same_bits(string key, int count, IReadOnlyList<CeaCase> cases)
    {
        Assert.Equal(count, cases.Count);
        var table = HostSolver.BuildTable(fixture.Database, cases[0]);
        var host = cases.Select(c => HostSolver.Solve(fixture.Accelerator, HostSolver.Of(table, c))).ToList();

        var kernel = FillAndLaunch(fixture.Accelerator, table, cases, count);
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
            kinds: kinds.View, pressures: pressures.View, temperatures: temperatures.View, targets: targets.View,
            elementMoles: elementMoles.View, scratchDoubles: scratchDoubles.View, scratchInts: scratchInts.View,
            moles: moles.View, multipliers: multipliers.View, states: states.View, statuses: statuses.View, iterations: iterations.View);
        var kernel = accelerator.LoadAutoGroupedStreamKernel<Index1D, SpeciesTableView, BatchViews>(SolveKernel);
        kernel(count, buffers.View, batch);
        accelerator.Synchronize();

        return new KernelBatchResult(moles.GetAsArray1D(), multipliers.GetAsArray1D(), states.GetAsArray1D(), statuses.GetAsArray1D(), iterations.GetAsArray1D());
    }

    /// <summary>Every status, iteration count, mole, multiplier and state field of the batch, host against kernel, bit for bit.</summary>
    private static void AssertSameBits(IReadOnlyList<HostSolution> host, KernelBatchResult kernel, SpeciesTable table, IReadOnlyList<CeaCase> cases, string key)
    {
        var speciesCount = table.SpeciesCount;
        var elementCount = table.ElementCount;
        var fields = typeof(MixtureState).GetFields();
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

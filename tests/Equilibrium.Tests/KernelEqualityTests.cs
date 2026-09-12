using AerospacePropellantThermodynamics.Fixtures;
using AerospacePropellantThermodynamics.Thermo;
using ILGPU;
using ILGPU.Runtime;

namespace AerospacePropellantThermodynamics.Equilibrium.Tests;

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

/// <summary>L1: the solver inside a CPU-accelerator kernel gives the same bits as the host call.</summary>
[Collection(CpuCollection.Name)]
public sealed class KernelEqualityTests(CpuFixture fixture)
{
    /// <summary>The families of fixture cases sharing one table (elements and products), largest first; each is one batch.</summary>
    public static IEnumerable<object[]> Batches()
    {
        var families = new Dictionary<string, List<(string Kind, string Name)>>();
        foreach (var kind in new[] { "tp", "hp", "sp" })
        {
            foreach (var row in HostSolver.Cases(kind))
            {
                var c = HostSolver.Load(kind, (string)row[0]);
                var key = HostSolver.TableKey(c);
                if (!families.TryGetValue(key, out var list))
                {
                    families[key] = list = [];
                }

                list.Add((kind, (string)row[0]));
            }
        }

        foreach (var family in families.Values.OrderByDescending(f => f.Count).ThenBy(f => f[0].Name, StringComparer.Ordinal))
        {
            yield return [family[0].Name, family.Count, family.Select(m => m.Kind + ":" + m.Name).ToArray()];
        }
    }

    [Theory]
    [MemberData(nameof(Batches))]
    public void Kernel_and_host_give_the_same_bits(string family, int count, string[] members)
    {
        Assert.Equal(count, members.Length);
        Assert.Equal(family, members[0][3..]);
        var cases = members.Select(m => HostSolver.Load(m[..2], m[3..])).ToList();
        var table = HostSolver.BuildTable(fixture.Database, cases[0]);
        var host = cases.Select(c => HostSolver.Solve(fixture.Accelerator, table, HostSolver.KindOf(c), HostSolver.PressureOf(c),
                                                      HostSolver.TemperatureOf(c), HostSolver.TargetOf(c), HostSolver.ElementMolesOf(c))).ToList();

        var accelerator = fixture.Accelerator;
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

        var batch = new BatchViews(kinds.View, pressures.View, temperatures.View, targets.View, elementMoles.View,
                                   scratchDoubles.View, scratchInts.View, moles.View, multipliers.View, states.View, statuses.View, iterations.View);
        var kernel = accelerator.LoadAutoGroupedStreamKernel<Index1D, SpeciesTableView, BatchViews>(SolveKernel);
        kernel(count, buffers.View, batch);
        accelerator.Synchronize();

        var kernelMoles = moles.GetAsArray1D();
        var kernelMultipliers = multipliers.GetAsArray1D();
        var kernelStates = states.GetAsArray1D();
        var kernelStatuses = statuses.GetAsArray1D();
        var kernelIterations = iterations.GetAsArray1D();
        var fields = typeof(MixtureState).GetFields();
        for (var k = 0; k < count; k++)
        {
            Assert.True(host[k].Status == CaseStatus.Ok, $"{members[k]}: host status {host[k].Status}");
            Assert.True((int)host[k].Status == kernelStatuses[k], $"{members[k]}: kernel status {(CaseStatus)kernelStatuses[k]}");
            Assert.True(host[k].Iterations == kernelIterations[k], $"{members[k]}: iterations host {host[k].Iterations}, kernel {kernelIterations[k]}");
            for (var j = 0; j < speciesCount; j++)
            {
                Assert.True(SameBits(host[k].Moles[j], kernelMoles[k * speciesCount + j]),
                            $"{members[k]}: moles of {table.Species[j]} host {host[k].Moles[j]:R}, kernel {kernelMoles[k * speciesCount + j]:R}");
            }

            for (var i = 0; i < elementCount; i++)
            {
                Assert.True(SameBits(host[k].Multipliers[i], kernelMultipliers[k * elementCount + i]), $"{members[k]}: multiplier of {table.Elements[i]} differs");
            }

            foreach (var field in fields)
            {
                var a = (double)field.GetValue(host[k].State)!;
                var b = (double)field.GetValue(kernelStates[k])!;
                Assert.True(SameBits(a, b), $"{members[k]}: {field.Name} host {a:R}, kernel {b:R}");
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

    private static bool SameBits(double a, double b) => BitConverter.DoubleToInt64Bits(a) == BitConverter.DoubleToInt64Bits(b);
}

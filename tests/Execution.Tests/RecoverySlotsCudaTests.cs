using System.Globalization;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.Json;
using APThermo.Equilibrium;
using APThermo.Fixtures;
using APThermo.Execution.Ptx;
using APThermo.Harness;
using APThermo.Thermo;
using ILGPU;
using ILGPU.Backends.EntryPoints;
using ILGPU.Backends.PTX;
using ILGPU.Runtime;
using ILGPU.Runtime.Cuda;
using Xunit.Abstractions;

[assembly: InternalsVisibleTo("ILGPURuntime")]

namespace APThermo.Execution.Tests;

internal readonly record struct RecoveryProblemViews(double Pressure, double Target, double SeedTemperature, int Seeded, ArrayView<double> Elements);
internal readonly record struct RecoveryScratchViews(ArrayView<double> Doubles, ArrayView<int> Ints, ArrayView<MixtureState> Recovery);
internal readonly record struct RecoveryOutputViews(ArrayView<double> Moles, ArrayView<double> Multipliers,
    ArrayView<MixtureState> States, ArrayView<int> Statuses, ArrayView<int> Iterations);
internal readonly record struct RecoveryKernelViews(RecoveryProblemViews Problem, RecoveryScratchViews Scratch, RecoveryOutputViews Output);

/// <summary>Real cold and seeded solves retain their selected recovery candidate's condensed slots on each accelerator.</summary>
[Collection(EngineFixture.CollectionName)]
public sealed class RecoverySlotsCudaTests(ITestOutputHelper output)
{
    private sealed record Input(SpeciesTable Table, double Pressure, double Target, double SeedTemperature, double[] Elements);
    private sealed record Solved(double[] Moles, double[] Multipliers, MixtureState State, int Status, int Iterations, int[] Slots);

    /// <summary>CPU accelerator companion exercises the same kernel and fixture as the CUDA test.</summary>
    [Fact]
    public void PhysicalRecoverySlotsOnCpuKernelAgreeWithTheWinningComposition()
    {
        var input = Load();
        var seed = Seed(input);
        foreach (var seeded in new[] { false, true })
        {
            var result = Solve(EngineFixture.Shared.Cpu.IlgpuAccelerator, input, seeded ? seed : null);
            AssertConsistent(input, result);
        }
    }

    /// <summary>Both real solves, all result fields and their condensed slots match the CPU on actual CUDA hardware.</summary>
    [Fact]
    [Trait("Category", "Cuda")]
    [Trait("CudaEvidence", "hardware")]
    public void PhysicalRecoverySlotsOnActualCudaMatchCpuForColdAndSeededSolves()
    {
        var cuda = EngineFixture.Shared.RequireCuda(output);
        if (cuda is null)
        {
            output.WriteLine("actualCuda=false; explicit forbidden-CUDA refusal only; recovery hardware evidence absent");
            return;
        }

        Assert.Equal(AcceleratorKind.Cuda, cuda.Accelerator.Kind);
        Assert.Null(cuda.Accelerator.CudaSkippedBecause);
        var device = Assert.IsType<CudaAccelerator>(cuda.IlgpuAccelerator);
        Assert.False(string.IsNullOrWhiteSpace(device.Name));
        output.WriteLine($"actualCuda=true; deviceId={device.DeviceId}; name={device.Name}; ILGPU={cuda.Accelerator.IlgpuVersion}");
        var input = Load();
        var seed = Seed(input);
        foreach (var seeded in new[] { false, true })
        {
            var initial = seeded ? seed : null;
            var cpu = Solve(EngineFixture.Shared.Cpu.IlgpuAccelerator, input, initial);
            var actual = Solve(device, input, initial);
            AssertConsistent(input, cpu);
            AssertConsistent(input, actual);
            Assert.Equal(cpu.Status, actual.Status);
            Assert.Equal(cpu.Iterations, actual.Iterations);
            Assert.Equal(cpu.Slots, actual.Slots);
            Assert.Equal(cpu.Moles.Length, actual.Moles.Length);
            for (var index = 0; index < cpu.Moles.Length; index++)
            {
                Assert.True(Bits.Same(cpu.Moles[index], actual.Moles[index]), $"moles {index}, seeded={seeded}");
            }

            Assert.Equal(cpu.Multipliers.Length, actual.Multipliers.Length);
            for (var index = 0; index < cpu.Multipliers.Length; index++)
            {
                Assert.True(Bits.Same(cpu.Multipliers[index], actual.Multipliers[index]), $"multipliers {index}, seeded={seeded}");
            }

            Assert.Empty(Bits.Differences(cpu.State, actual.State, $"state seeded={seeded}"));
        }

        output.WriteLine("actualCuda=true; recovery slots, complete state, moles, multipliers, status and iterations matched CPU for cold and seeded solves");
        CudaEvidence.CompleteHardware(output, cuda.Accelerator.DeviceName);
    }

    private static Input Load()
    {
        using var document = JsonDocument.Parse(File.ReadAllText(RepositoryPaths.Resolve("tests", "Equilibrium.Tests", "AuditCases.json")));
        var root = document.RootElement;
        var mixture = root.GetProperty("mixture");
        var witnesses = root.GetProperty("witnesses");
        var abundances = mixture.GetProperty("b").EnumerateObject().ToArray();
        var elements = abundances.Select(item => item.Name).ToArray();
        var database = EngineFixture.Shared.Database;
        var products = database.Products.Where(product => product.Formula.Count > 0 && product.Formula.All(element => elements.Contains(element.Symbol, StringComparer.Ordinal)))
            .Select(product => product.Name).Distinct(StringComparer.Ordinal).ToArray();
        var table = SpeciesTable.Build(database, elements, products);
        return new Input(table, mixture.GetProperty("p").GetDouble(), Number(witnesses.GetProperty("SP_feasible_lower_H"), "reported_S"),
            Number(witnesses.GetProperty("SP_returned_cold"), "T"), [.. abundances.Select(item => item.Value.GetDouble() / 1000.0)]);
    }

    private static double Number(JsonElement item, string name) => double.Parse(item.GetProperty(name).GetString()!, CultureInfo.InvariantCulture);

    private static double[] Seed(Input input)
    {
        var cpu = EngineFixture.Shared.Cpu;
        using var tables = cpu.Upload(input.Table);
        var batch = new EquilibriumBatch(1, input.Table.ElementCount);
        batch.Kind[0] = ProblemKind.AssignedTemperaturePressure;
        batch.Pressure[0] = input.Pressure;
        batch.Temperature[0] = input.SeedTemperature;
        Array.Copy(input.Elements, batch.ElementMoles, input.Elements.Length);
        var result = cpu.Run(tables, batch);
        Assert.Equal(CaseStatus.Ok, result.Status[0]);
        return result.Moles;
    }

    private static Solved Solve(Accelerator accelerator, Input input, double[]? seed)
    {
        var table = input.Table;
        using var tables = SpeciesTableBuffers.Upload(accelerator, table);
        using var elements = accelerator.Allocate1D(input.Elements);
        using var doubles = accelerator.Allocate1D<double>(ScratchLayout.DoublesPerCase(table.SpeciesCount, table.ElementCount));
        using var ints = accelerator.Allocate1D<int>(ScratchLayout.IntsPerCase(table.SpeciesCount, table.ElementCount));
        using var moles = seed is null ? accelerator.Allocate1D<double>(table.SpeciesCount) : accelerator.Allocate1D(seed);
        using var multipliers = accelerator.Allocate1D<double>(table.ElementCount);
        using var states = accelerator.Allocate1D<MixtureState>(1);
        using var statuses = accelerator.Allocate1D<int>(1);
        using var iterations = accelerator.Allocate1D<int>(1);
        using var recovery = accelerator.Allocate1D<MixtureState>(1);
        if (seed is null) { moles.MemSetToZero(); }
        var batch = new RecoveryKernelViews(
            Problem: new RecoveryProblemViews(input.Pressure, input.Target, input.SeedTemperature, seed is null ? 0 : 1, elements.View),
            Scratch: new RecoveryScratchViews(doubles.View, ints.View, recovery.View),
            Output: new RecoveryOutputViews(moles.View, multipliers.View, states.View, statuses.View, iterations.View));
        Launch(accelerator, tables.View, batch);
        accelerator.Synchronize();
        var slots = ints.GetAsArray1D().Skip(table.SpeciesCount + table.ElementCount).Take(ScratchLayout.MaxCondensedInSolution).ToArray();
        return new Solved(moles.GetAsArray1D(), multipliers.GetAsArray1D(), states.GetAsArray1D()[0], statuses.GetAsArray1D()[0], iterations.GetAsArray1D()[0], slots);
    }

    private static void Launch(Accelerator accelerator, SpeciesTableView table, RecoveryKernelViews batch)
    {
        if (accelerator is not CudaAccelerator cuda)
        {
            var cpuKernel = accelerator.LoadAutoGroupedStreamKernel<Index1D, SpeciesTableView, RecoveryKernelViews>(SolveKernel);
            cpuKernel(1, table, batch);
            return;
        }

        var method = typeof(RecoverySlotsCudaTests).GetMethod(nameof(SolveKernel), BindingFlags.Static | BindingFlags.NonPublic)!;
        using var backend = new PTXBackend(cuda.Context, cuda.Architecture, cuda.InstructionSet, null!);
        var compiled = (PTXCompiledKernel)backend.Compile(EntryPointDescription.FromImplicitlyGroupedKernel(method), KernelSpecialization.Empty);
        var linked = PtxPostLink.Link(cuda, compiled);
        using var kernel = cuda.LoadAutoGroupedKernel(linked.Kernel);
        var launch = kernel.CreateLauncherDelegate<Action<AcceleratorStream, Index1D, SpeciesTableView, RecoveryKernelViews>>();
        launch(cuda.DefaultStream, 1, table, batch);
        cuda.Synchronize();
    }

    private static void SolveKernel(Index1D index, SpeciesTableView table, RecoveryKernelViews batch)
    {
        var problem = new EquilibriumProblem(ProblemKind.AssignedEntropyPressure, batch.Problem.Pressure,
            batch.Problem.Seeded == 0 ? 0 : batch.Problem.SeedTemperature, batch.Problem.Target, batch.Problem.Elements);
        var scratch = EquilibriumScratch.Slice(batch.Scratch.Doubles, batch.Scratch.Ints, table.SpeciesCount, table.ElementCount, batch.Scratch.Recovery);
        var result = new EquilibriumResult(batch.Output.Moles, batch.Output.Multipliers, batch.Output.States, batch.Output.Statuses, batch.Output.Iterations);
        EquilibriumSolver.Solve(in table, in problem, in scratch, in result, batch.Problem.Seeded != 0);
    }

    private static void AssertConsistent(Input input, Solved result)
    {
        Assert.Equal((int)CaseStatus.Ok, result.Status);
        var active = result.Slots.Where(index => index >= 0).Order().ToArray();
        var positive = Enumerable.Range(input.Table.GasCount, input.Table.CondensedCount).Where(index => result.Moles[index] > 0.0).Order().ToArray();
        Assert.NotEmpty(positive);
        Assert.Equal(positive, active);
    }
}

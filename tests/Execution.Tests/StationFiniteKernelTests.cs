using System.Reflection;
using APThermo.Execution.Ptx;
using APThermo.Performance;
using APThermo.Thermo;
using ILGPU;
using ILGPU.Backends.EntryPoints;
using ILGPU.Backends.PTX;
using ILGPU.Runtime;
using ILGPU.Runtime.Cuda;
using Xunit.Abstractions;

namespace APThermo.Execution.Tests;

/// <summary>Classifies the actual station figure guard on both accelerator backends.</summary>
[Collection(EngineFixture.CollectionName)]
public sealed class StationFiniteKernelTests(ITestOutputHelper output)
{
    private const ulong ExponentMask = 0x7FF0_0000_0000_0000UL;
    private const ulong SignMask = 0x8000_0000_0000_0000UL;
    private static readonly ulong[] Mantissas = [0UL, 1UL, 0x0008_0000_0000_0000UL, 0x000F_FFFF_FFFF_FFFFUL];

    /// <summary>The CPU accelerator's actual station write follows the binary64 classification oracle.</summary>
    [Fact]
    public void CpuStationGuardClassifiesBinary64()
    {
        var inputs = Inputs();
        Verify(inputs, Run(EngineFixture.Shared.Cpu.IlgpuAccelerator, inputs));
    }

    /// <summary>Actual CUDA through post-link classifies the same input classes independently of the CPU.</summary>
    [Fact]
    [Trait("Category", "Cuda")]
    [Trait("CudaEvidence", "hardware")]
    public void CudaStationGuardClassifiesBinary64()
    {
        var cuda = EngineFixture.Shared.RequireCuda(output);
        if (cuda is null) { return; }
        var inputs = Inputs();
        Verify(inputs, Run(cuda.IlgpuAccelerator, inputs));
        CudaEvidence.CompleteHardware(output, cuda.Accelerator.DeviceName);
    }

    private static double[] Inputs()
    {
        var values = new List<double>();
        foreach (var sign in new[] { 0UL, SignMask })
        {
            for (ulong exponent = 0; exponent < 2048; exponent++)
            {
                foreach (var mantissa in Mantissas)
                {
                    var bits = sign | (exponent << 52) | mantissa;
                    var value = BitConverter.UInt64BitsToDouble(bits);
                    Assert.Equal(bits, BitConverter.DoubleToUInt64Bits(value));
                    values.Add(value);
                }
            }
        }

        var inputs = values.ToArray();
        var signal = 2047 * Mantissas.Length + 1;
        Assert.Equal(0x7FF0_0000_0000_0001UL, BitConverter.DoubleToUInt64Bits(inputs[signal]));
        Assert.Equal(0xFFF0_0000_0000_0001UL, BitConverter.DoubleToUInt64Bits(inputs[signal + 2048 * Mantissas.Length]));
        return inputs;
    }

    private static void Verify(double[] inputs, int[] statuses)
    {
        Assert.Equal(inputs.Length, statuses.Length);
        for (var i = 0; i < inputs.Length; i++)
        {
            var bits = BitConverter.DoubleToUInt64Bits(inputs[i]);
            var expected = (bits & ExponentMask) == ExponentMask ? CaseStatus.NotConverged : CaseStatus.Ok;
            Assert.True(statuses[i] == (int)expected, $"bits={bits:X16}: expected {expected}, actual {(CaseStatus)statuses[i]}");
        }
    }

    private static int[] Run(Accelerator accelerator, double[] inputs)
    {
        using var values = accelerator.Allocate1D(inputs);
        using var states = accelerator.Allocate1D<MixtureState>(inputs.Length);
        using var figures = accelerator.Allocate1D<PerformanceFigures>(inputs.Length);
        using var statuses = accelerator.Allocate1D<int>(inputs.Length);
        Launch(accelerator, values.View, states.View, figures.View, statuses.View);
        accelerator.Synchronize();
        return statuses.GetAsArray1D();
    }

    private static void Launch(Accelerator accelerator, ArrayView<double> values, ArrayView<MixtureState> states,
        ArrayView<PerformanceFigures> figures, ArrayView<int> statuses)
    {
        if (accelerator is not CudaAccelerator cuda)
        {
            var cpuKernel = accelerator.LoadAutoGroupedStreamKernel<Index1D, ArrayView<double>, ArrayView<MixtureState>, ArrayView<PerformanceFigures>, ArrayView<int>>(WriteKernel);
            cpuKernel((int)values.Length, values, states, figures, statuses);
            return;
        }

        var method = typeof(StationFiniteKernelTests).GetMethod(nameof(WriteKernel), BindingFlags.Static | BindingFlags.NonPublic)!;
        using var backend = new PTXBackend(cuda.Context, cuda.Architecture, cuda.InstructionSet, null!);
        var compiled = (PTXCompiledKernel)backend.Compile(EntryPointDescription.FromImplicitlyGroupedKernel(method), KernelSpecialization.Empty);
        var linked = PtxPostLink.Link(cuda, compiled);
        using var kernel = cuda.LoadAutoGroupedKernel(linked.Kernel);
        var launch = kernel.CreateLauncherDelegate<Action<AcceleratorStream, Index1D, ArrayView<double>, ArrayView<MixtureState>, ArrayView<PerformanceFigures>, ArrayView<int>>>();
        launch(cuda.DefaultStream, (int)values.Length, values, states, figures, statuses);
        cuda.Synchronize();
    }

    private static void WriteKernel(Index1D index, ArrayView<double> values, ArrayView<MixtureState> states,
        ArrayView<PerformanceFigures> figures, ArrayView<int> statuses)
    {
        states[index] = new MixtureState { Pressure = 0.0, Density = 1.0, SoundSpeed = 1.0, Entropy = 1.0 };
        statuses[index] = (int)CaseStatus.Ok;
        var result = new RocketResult(stations: states, moles: default, multipliers: default, figures: figures,
            stationStatus: statuses, iterations: default, status: default);
        var context = new RocketContext(default, default, default, result);
        var inputs = new StationFigureInputs(1.0, values[index], 1.0, 1.0);
        StationFigures.Write(in context, index, in inputs, 1.0);
    }
}

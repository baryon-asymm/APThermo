using APThermo.Equilibrium.StateRecord;
using APThermo.Thermo;
using ILGPU.Runtime;

namespace APThermo.Equilibrium.Tests;

/// <summary>
/// L0: a state guard refusal clears poisoned output; only a complete valid state is published (A13, 2026-11-14).
/// </summary>
[Collection(CpuFixture.CollectionName)]
public sealed class MixturePropertiesTests
{
    /// <summary>A sentinel state distinguishable from anything a real solve would write, so an unwanted write is caught by value.</summary>
    private static readonly MixtureState Sentinel = new() { Temperature = -1.0, Pressure = -1.0 };

    /// <summary>The one-element buffer content every fact below allocates its state view from.</summary>
    private static readonly MixtureState[] SentinelArray = [Sentinel];

    /// <summary>A sums whose Cp°/R is NaN, so every heat capacity the closure derives from it is NaN and the state guard fails.</summary>
    private static MixtureSums NonFiniteSums() =>
        new() { Temperature = 300.0, N = 1.0, SumGas = 1.0, CpOverR = double.NaN };

    /// <summary>A sums giving a physical, finite-positive state, so the corresponding fact below writes it.</summary>
    private static MixtureSums PhysicalSums() =>
        new() { Temperature = 300.0, N = 1.0, SumGas = 1.0, CpOverR = 2.5 };

    /// <summary>False, with the state cleared, when the equilibrium closure's Cp/Cv is not finite and positive.</summary>
    [Fact]
    public void WriteEquilibriumClearsTheStateWhenTheGuardFails()
    {
        var accelerator = CpuFixture.Shared.Accelerator;
        using var stateBuffer = accelerator.Allocate1D(SentinelArray);
        var result = new EquilibriumResult(default, default, stateBuffer.View, default, default);
        var problem = new EquilibriumProblem(ProblemKind.AssignedTemperaturePressure, 1.0e5, 300.0, 0.0, default);
        var derivatives = new Derivatives { DlnNdlnT = 0.0, DlnNdlnP = 0.0, Reaction = 0.0, Pinned = false, Solved = true };

        var written = MixtureProperties.WriteEquilibrium(problem, result, NonFiniteSums(), derivatives);

        Assert.False(written);
        Assert.Equal(default, stateBuffer.GetAsArray1D()[0]);
    }

    /// <summary>True, with the state written, when the equilibrium closure is physical.</summary>
    [Fact]
    public void WriteEquilibriumWritesTheStateWhenTheGuardPasses()
    {
        var accelerator = CpuFixture.Shared.Accelerator;
        using var stateBuffer = accelerator.Allocate1D(SentinelArray);
        var result = new EquilibriumResult(default, default, stateBuffer.View, default, default);
        var problem = new EquilibriumProblem(ProblemKind.AssignedTemperaturePressure, 1.0e5, 300.0, 0.0, default);
        var derivatives = new Derivatives { DlnNdlnT = 0.0, DlnNdlnP = 0.0, Reaction = 0.0, Pinned = false, Solved = true };

        var written = MixtureProperties.WriteEquilibrium(problem, result, PhysicalSums(), derivatives);

        Assert.True(written);
        Assert.NotEqual(Sentinel, stateBuffer.GetAsArray1D()[0]);
    }

    /// <summary>False, with the state cleared, when the frozen closure's Cp/Cv is not finite and positive.</summary>
    [Fact]
    public void WriteFrozenClearsTheStateWhenTheGuardFails()
    {
        var accelerator = CpuFixture.Shared.Accelerator;
        using var stateBuffer = accelerator.Allocate1D(SentinelArray);
        var result = new EquilibriumResult(default, default, stateBuffer.View, default, default);
        var problem = new EquilibriumProblem(ProblemKind.AssignedTemperaturePressure, 1.0e5, 300.0, 0.0, default);

        var written = MixtureProperties.WriteFrozen(problem, result, NonFiniteSums());

        Assert.False(written);
        Assert.Equal(default, stateBuffer.GetAsArray1D()[0]);
    }
    /// <summary>The full state guard must catch invalid primary fields even when the heat capacities stay physical.</summary>
    [Theory]
    [InlineData("entropy")]
    [InlineData("enthalpy")]
    [InlineData("gas molar mass")]
    [InlineData("mixture molar mass")]
    [InlineData("density overflow")]
    [InlineData("density underflow")]
    public void NonResponseFieldsCannotEscapeTheGuard(string invalidField)
    {
        using var stateBuffer = CpuFixture.Shared.Accelerator.Allocate1D(SentinelArray);
        var result = new EquilibriumResult(default, default, stateBuffer.View, default, default);
        var sums = PhysicalSums();
        var pressure = 1e5;
        switch (invalidField)
        {
            case "entropy": sums.SOverR = double.PositiveInfinity; break;
            case "enthalpy": sums.HOverRT = double.MaxValue; break;
            case "gas molar mass": sums.SumGas = double.Epsilon; break;
            case "mixture molar mass": sums.CondensedMoles = -sums.SumGas; break;
            case "density overflow": sums.SumGas = 1e-300; pressure = double.MaxValue; break;
            case "density underflow": pressure = double.Epsilon; break;
            default: throw new ArgumentOutOfRangeException(nameof(invalidField));
        }

        var problem = new EquilibriumProblem(ProblemKind.AssignedTemperaturePressure, pressure, sums.Temperature, 0.0, default);
        Assert.False(MixtureProperties.WriteFrozen(problem, result, sums));
        Assert.Equal(default, stateBuffer.GetAsArray1D()[0]);
    }
}

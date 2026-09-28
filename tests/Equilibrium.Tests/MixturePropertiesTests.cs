using APThermo.Thermo;
using ILGPU.Runtime;

namespace APThermo.Equilibrium.Tests;

/// <summary>
/// L0: the third audit pass's finding F3 (BOOT.md, 2026-09-28) — the state guard decides before
/// <c>result.State[0]</c> is written, so a caller reading a failing status never finds a stale or partial state
/// (API.md, "On any status but Ok ... State is not written").
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

    /// <summary>False, with the state left at its previous value, when the equilibrium closure's Cp/Cv is not finite and positive.</summary>
    [Fact]
    public void WriteEquilibriumLeavesTheStateUntouchedWhenTheGuardFails()
    {
        var accelerator = CpuFixture.Shared.Accelerator;
        using var stateBuffer = accelerator.Allocate1D(SentinelArray);
        var result = new EquilibriumResult(default, default, stateBuffer.View, default, default);
        var problem = new EquilibriumProblem(ProblemKind.AssignedTemperaturePressure, 1.0e5, 300.0, 0.0, default);
        var derivatives = new Derivatives { DlnNdlnT = 0.0, DlnNdlnP = 0.0, Reaction = 0.0, Pinned = false, Solved = true };

        var written = MixtureProperties.WriteEquilibrium(problem, result, NonFiniteSums(), derivatives);

        Assert.False(written);
        Assert.Equal(Sentinel, stateBuffer.GetAsArray1D()[0]);
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

    /// <summary>False, with the state left at its previous value, when the frozen closure's Cp/Cv is not finite and positive.</summary>
    [Fact]
    public void WriteFrozenLeavesTheStateUntouchedWhenTheGuardFails()
    {
        var accelerator = CpuFixture.Shared.Accelerator;
        using var stateBuffer = accelerator.Allocate1D(SentinelArray);
        var result = new EquilibriumResult(default, default, stateBuffer.View, default, default);
        var problem = new EquilibriumProblem(ProblemKind.AssignedTemperaturePressure, 1.0e5, 300.0, 0.0, default);

        var written = MixtureProperties.WriteFrozen(problem, result, NonFiniteSums());

        Assert.False(written);
        Assert.Equal(Sentinel, stateBuffer.GetAsArray1D()[0]);
    }
}

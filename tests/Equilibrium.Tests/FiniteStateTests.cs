using APThermo.Harness;
using APThermo.Thermo;

namespace APThermo.Equilibrium.Tests;

/// <summary>Lower-level frozen TP pressure controls; no public validation can conceal an invalid calculated state.</summary>
[Collection(CpuFixture.CollectionName)]
public sealed class FiniteStateTests
{
    /// <summary>The smallest pressure loses density to rounding; nonfinite pressure is invalid input.</summary>
    [Theory]
    [InlineData(double.Epsilon, CaseStatus.TemperatureOutOfRange)]
    [InlineData(double.PositiveInfinity, CaseStatus.InvalidInput)]
    public void UndefinedFrozenStatesAreRefused(double pressure, CaseStatus expected)
    {
        var solution = FrozenAt(pressure);
        Assert.Equal(expected, solution.Status);
        Assert.Empty(Bits.Differences<MixtureState>(default, solution.State, "refused state"));
    }

    /// <summary>Positive controls: no arbitrary small or large finite pressure exclusion is allowed.</summary>
    [Theory]
    [InlineData(1e5)]
    [InlineData(1e-310)]
    [InlineData(double.MaxValue)]
    public void RepresentableFrozenStatesRemainAdmitted(double pressure)
    {
        var solution = FrozenAt(pressure);
        Assert.Equal(CaseStatus.Ok, solution.Status);
        foreach (var property in typeof(MixtureState).GetProperties())
        {
            var value = (double)property.GetValue(solution.State)!;
            Assert.True(double.IsFinite(value), $"{property.Name} = {value:R} at pressure {pressure:R}");
        }

        Assert.True(solution.State.Density > 0.0);
        Assert.Equal(pressure, solution.State.Pressure);
    }

    private static HostSolution FrozenAt(double pressure)
    {
        var table = SpeciesTable.Build(CpuFixture.Shared.Database, ["H", "O"], ["H2", "O2", "H2O"]);
        var moles = new double[table.SpeciesCount];
        moles[table.IndexOf("H2")] = 0.1;
        moles[table.IndexOf("O2")] = 0.05;
        var problem = new EquilibriumCase(table, ProblemKind.AssignedTemperaturePressure, pressure, 3000.0, 0.0, [0.2, 0.1]);
        return HostSolver.SolveFrozen(CpuFixture.Shared.Accelerator, problem, moles);
    }
}
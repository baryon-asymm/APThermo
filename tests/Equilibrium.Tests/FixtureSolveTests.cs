using APThermo.Thermo;

namespace APThermo.Equilibrium.Tests;

/// <summary>L1: every tp, hp and sp fixture case is solved and compared with the reference, field by field and species by species.</summary>
public sealed class FixtureSolveTests
{
    /// <summary>Theory data: the fixture files of a kind, delegating to <see cref="HostSolver.Cases"/>.</summary>
    public static TheoryData<string> Cases(string kind) => HostSolver.Cases(kind);

    /// <summary>Assigned temperature and pressure reproduces the reference.</summary>
    [Theory]
    [MemberData(nameof(Cases), "tp")]
    public void AssignedTemperatureAndPressureReproducesTheReference(string name) => Check("tp", name);

    /// <summary>Assigned enthalpy and pressure reproduces the reference.</summary>
    [Theory]
    [MemberData(nameof(Cases), "hp")]
    public void AssignedEnthalpyAndPressureReproducesTheReference(string name) => Check("hp", name);

    /// <summary>Assigned entropy and pressure reproduces the reference.</summary>
    [Theory]
    [MemberData(nameof(Cases), "sp")]
    public void AssignedEntropyAndPressureReproducesTheReference(string name) => Check("sp", name);

    private static void Check(string kind, string name)
    {
        var c = HostSolver.Load(kind, name);
        Assert.True(c.Outputs.GetProperty("converged").GetBoolean(), "the reference case did not converge; it cannot serve as a reference");
        var solution = HostSolver.Solve(CpuFixture.Shared, c);
        Assert.True(solution.Status == CaseStatus.Ok, $"status {solution.Status} after {solution.Iterations} iterations");
        var mismatches = StateComparison.Compare(c, solution, CpuFixture.Shared.Tolerances);
        Assert.True(mismatches.Count == 0, $"{mismatches.Count} mismatches after {solution.Iterations} iterations: " + string.Join("; ", mismatches));
    }
}

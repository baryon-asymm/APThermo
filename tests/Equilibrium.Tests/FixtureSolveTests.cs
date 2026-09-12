using AerospacePropellantThermodynamics.Thermo;

namespace AerospacePropellantThermodynamics.Equilibrium.Tests;

/// <summary>L1: every tp, hp and sp fixture case is solved and compared with the reference, field by field and species by species.</summary>
[Collection(CpuCollection.Name)]
public sealed class FixtureSolveTests(CpuFixture fixture)
{
    public static IEnumerable<object[]> Cases(string kind) => HostSolver.Cases(kind);

    [Theory]
    [MemberData(nameof(Cases), "tp")]
    public void Assigned_temperature_and_pressure_reproduces_the_reference(string name) => Check("tp", name);

    [Theory]
    [MemberData(nameof(Cases), "hp")]
    public void Assigned_enthalpy_and_pressure_reproduces_the_reference(string name) => Check("hp", name);

    [Theory]
    [MemberData(nameof(Cases), "sp")]
    public void Assigned_entropy_and_pressure_reproduces_the_reference(string name) => Check("sp", name);

    private void Check(string kind, string name)
    {
        var c = HostSolver.Load(kind, name);
        Assert.True(c.Outputs.GetProperty("converged").GetBoolean(), "the reference case did not converge; it cannot serve as a reference");
        var solution = HostSolver.Solve(fixture, c);
        Assert.True(solution.Status == CaseStatus.Ok, $"status {solution.Status} after {solution.Iterations} iterations");
        var mismatches = StateComparison.Compare(c, solution, fixture.Tolerances);
        Assert.True(mismatches.Count == 0, $"{mismatches.Count} mismatches after {solution.Iterations} iterations: " + string.Join("; ", mismatches));
    }
}

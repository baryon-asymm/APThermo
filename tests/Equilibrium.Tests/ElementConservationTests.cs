using APThermo.Thermo;

namespace APThermo.Equilibrium.Tests;

/// <summary>L0/L1: the element-conservation invariant of the node holds for every converged fixture case.</summary>
[Collection(CpuFixture.CollectionName)]
public sealed class ElementConservationTests
{
    /// <summary>The invariant's tolerance (Equilibrium BOOT.md): |Σ a_ij n_j − b_i| ≤ 1e-12 · max(1, b_i).</summary>
    private const double Invariant = 1e-12;

    private static readonly string[] Kinds = ["tp", "hp", "sp"];

    /// <summary>Theory data: every fixture case of the three equilibrium kinds.</summary>
    public static TheoryData<string, string> Cases()
    {
        var data = new TheoryData<string, string>();
        foreach (var kind in Kinds)
        {
            foreach (var name in HostSolver.CaseNames(kind))
            {
                data.Add(kind, name);
            }
        }

        return data;
    }

    /// <summary>Elements are conserved at the invariant tolerance.</summary>
    [Theory]
    [MemberData(nameof(Cases))]
    public void ElementsAreConservedAtTheInvariantTolerance(string kind, string name)
    {
        var c = HostSolver.Load(kind, name);
        var solution = HostSolver.Solve(CpuFixture.Shared, c);
        Assert.Equal(CaseStatus.Ok, solution.Status);

        var violations = Violations(solution);
        Assert.True(violations.Count == 0, string.Join("; ", violations));
    }

    /// <summary>
    /// A NaN mole number is a violation, not a silent pass (2026-09-27, the guards audit's F5/F6 pattern): the bound
    /// comparison below is written <c>!(residual &lt;= bound)</c>, which a NaN residual fails, rather than
    /// <c>residual &gt; bound</c>, which a NaN residual also fails to satisfy and so was never recorded. Shown red
    /// with the old form (<c>residual > bound</c> in place of the current line): the doctored solution below then
    /// reports no violation at all, and this fact's own assertion fails.
    /// </summary>
    [Fact]
    public void ANaNMoleNumberIsAConservationViolation()
    {
        var c = HostSolver.Load("tp", "rp1311-example1_r1.0_p1.0atm_T3000");
        var solution = HostSolver.Solve(CpuFixture.Shared, c);
        Assert.Equal(CaseStatus.Ok, solution.Status);
        solution.Moles[0] = double.NaN;

        Assert.NotEmpty(Violations(solution));
    }

    /// <summary>The residual of every element's conservation check against the invariant's bound; empty when it holds.</summary>
    private static List<string> Violations(HostSolution solution)
    {
        var arrays = solution.Case.Table.Arrays;
        var speciesCount = solution.Case.Table.SpeciesCount;
        var violations = new List<string>();
        for (var i = 0; i < solution.Case.Table.ElementCount; i++)
        {
            var b = 0.0;
            for (var j = 0; j < speciesCount; j++)
            {
                b += arrays.Stoichiometry[i * speciesCount + j] * solution.Moles[j];
            }

            var residual = Math.Abs(b - solution.Case.ElementMoles[i]);
            var bound = Invariant * Math.Max(1.0, solution.Case.ElementMoles[i]);
            if (!(residual <= bound))
            {
                violations.Add($"{solution.Case.Table.Elements[i]}: residual {residual:E3} above {bound:E3}");
            }
        }

        return violations;
    }
}

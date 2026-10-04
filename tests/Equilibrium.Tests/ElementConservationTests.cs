using APThermo.Thermo;
using ILGPU.Runtime;

namespace APThermo.Equilibrium.Tests;

/// <summary>L0/L1: the element-conservation invariant of the node holds for every converged fixture case.</summary>
[Collection(CpuFixture.CollectionName)]
public sealed class ElementConservationTests
{
    /// <summary>The invariant's tolerance (Equilibrium BOOT.md): |Σ a_ij n_j − b_i| ≤ 1e-13 · b_i.</summary>
    private const double Invariant = EquilibriumConditions.ElementInvariant;

    private static readonly string[] Kinds = ["tp", "hp", "sp"];
    private static readonly double[] TwoElementMoles = [0.2, 0.1];
    private static readonly double[] OneNaNMole = [double.NaN, 0.05];

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

    /// <summary>
    /// <see cref="ElementBalance.WithinInvariant"/> itself reads a NaN abundance as outside the invariant (2026-09-28,
    /// the guards audit's O8): the fact above drives the test node's own <see cref="Violations"/>, a second
    /// implementation of the same comparison, not this production method. Shown red with the method's guard written
    /// <c>residual > bound</c> in place of <c>!(residual &lt;= bound)</c>: a NaN residual satisfies neither, so the
    /// old form returned true (within the invariant) for the doctored composition below.
    /// </summary>
    [Fact]
    public void WithinInvariantItselfReadsANaNAbundanceAsOutsideTheInvariant()
    {
        var accelerator = CpuFixture.Shared.Accelerator;
        var table = SpeciesTable.Build(CpuFixture.Shared.Database, ["H", "O"], ["H2", "O2"]);
        using var buffers = SpeciesTableBuffers.Upload(accelerator, table);
        var view = buffers.View;
        using var doubles = accelerator.Allocate1D<double>(ScratchLayout.DoublesPerCase(view.SpeciesCount, view.ElementCount));
        using var ints = accelerator.Allocate1D<int>(ScratchLayout.IntsPerCase(view.SpeciesCount, view.ElementCount));
        var scratch = EquilibriumScratch.Slice(doubles.View, ints.View, view.SpeciesCount, view.ElementCount);
        using var elements = accelerator.Allocate1D(TwoElementMoles);
        using var molesBuffer = accelerator.Allocate1D(OneNaNMole);
        using var multipliers = accelerator.Allocate1D<double>(view.ElementCount);
        using var state = accelerator.Allocate1D<MixtureState>(1);
        using var status = accelerator.Allocate1D<int>(1);
        using var iterations = accelerator.Allocate1D<int>(1);
        var result = new EquilibriumResult(molesBuffer.View, multipliers.View, state.View, status.View, iterations.View);
        var problem = new EquilibriumProblem(ProblemKind.AssignedTemperaturePressure, 1e5, 3000.0, 0.0, elements.View);
        scratch.ElementActive[0] = 1;
        scratch.ElementActive[1] = 1;

        Assert.False(ElementBalance.WithinInvariant(view, problem, scratch, result));
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
            var bound = Invariant * solution.Case.ElementMoles[i];
            if (!(residual <= bound))
            {
                violations.Add($"{solution.Case.Table.Elements[i]}: residual {residual:E3} above {bound:E3}");
            }
        }

        return violations;
    }
}

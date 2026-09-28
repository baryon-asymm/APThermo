using APThermo.Equilibrium;
using APThermo.Execution;
using APThermo.Thermo;

namespace APThermo.Problems.Tests;

/// <summary>
/// The third audit pass of 2026-09-28 (part 2, finding 3; `BOOT.md`'s own unticked criterion). The refusal of an
/// element with no candidate species must not depend on what the solver solved before it.
/// </summary>
[Collection(SolverFixture.CollectionName)]
public sealed class ThirdPassFixTests
{
    /// <summary>
    /// <see cref="ChemicalSystemCache.Get(IReadOnlyList{string}, IReadOnlyList{string}, IReadOnlyList{string}?, IReadOnlyCollection{string})"/>
    /// looked its key (elements, <c>Omit</c>, <c>Only</c>) up before validating, while the validation reads which
    /// elements are abundant — a call whose own mixture has none is not part of the key. A fresh solver refuses a
    /// mixture with <c>"E": 1.0e-6</c> (`E` has no candidate species); the same solver, having first solved the
    /// same elements with <c>"E": 0.0</c> (masked, not refused, `SecondAuditFixTests`'s own finding F1), still
    /// refuses the positive-`E` mixture with the same message, not the `SingularMatrix` a stale cache hit gave.
    /// Red at `c02e14d`: the second call was not refused at all, ending `SingularMatrix`
    /// (`scratchpad/audit3/b/cache.txt`, kept out of the tree); green after validating every call before the
    /// lookup.
    /// </summary>
    [Fact]
    public void TheNoCandidateRefusalDoesNotDependOnTheCachesHistory()
    {
        var zeroElectron = new Dictionary<string, double>(RejectionTests.OneKilogram, StringComparer.Ordinal) { ["E"] = 0.0 };
        var positiveElectron = new Dictionary<string, double>(RejectionTests.OneKilogram, StringComparer.Ordinal) { ["E"] = 1.0e-6 };
        var problem = new EquilibriumProblem { Kind = ProblemKind.AssignedTemperaturePressure, Pressure = RejectionTests.RecordPressure, Temperature = 3000.0 };

        using var fresh = Solver.Create(SolverFixture.SharedDatabase, new EngineOptions { Accelerator = AcceleratorKind.Cpu });
        var control = Assert.Throws<ArgumentException>(() => fresh.Solve(ElementalMixture.Create(positiveElectron), problem));
        Assert.Contains("'E'", control.Message, StringComparison.Ordinal);
        Assert.Contains("no candidate species", control.Message, StringComparison.Ordinal);

        var zeroResult = fresh.Solve(ElementalMixture.Create(zeroElectron), problem);
        Assert.Equal(CaseStatus.Ok, zeroResult.Status);

        var afterCacheHit = Assert.Throws<ArgumentException>(() => fresh.Solve(ElementalMixture.Create(positiveElectron), problem));
        Assert.Contains("'E'", afterCacheHit.Message, StringComparison.Ordinal);
        Assert.Contains("no candidate species", afterCacheHit.Message, StringComparison.Ordinal);
    }
}

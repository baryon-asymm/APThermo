using APThermo.Equilibrium.TraceGas;

namespace APThermo.Equilibrium.Tests;

/// <summary>
/// Room for the gas (TraceGas BOOT.md, "The starts"; the leftovers design of 2026-10-04, section 2): a state whose verdict is
/// <c>GasRequired</c> and whose phase-one point holds as many records as there are elements has no room for the gas beside them, and the
/// trace-gas system is then over-determined; the starts load the point whenever it completed, and the smallest record leaves.
/// </summary>
[Collection(CpuFixture.CollectionName)]
public sealed class TraceGasRoomTests
{
    /// <summary>The tp states the three starts ended <c>SingularMatrix</c> on after one change of the condensed set, KCl − 1e-10 Cl.</summary>
    public static TheoryData<string> States() =>
        TraceGasCases.Names(TraceGasCases.ScanFamilies().Where(c => c.Name is "binary-kcl|-1E-10|1000|1200" or "binary-kcl|-1E-10|100000|1500"));

    /// <summary>
    /// KCl − 1e-10 Cl at 1 200 K and 1 kPa and at 1 500 K and 100 kPa: <c>Ok</c>, clear of the conditions at 1e-9 with every gas, every element
    /// within the relative invariant. Red without the loaded point (F1) or without the room (F2): <c>NotConverged</c>, every start ending
    /// <c>SingularMatrix</c>.
    /// </summary>
    [Theory]
    [MemberData(nameof(States))]
    public void AStateWhosePhaseOnePointHoldsAsManyRecordsAsElementsEndsOk(string name)
    {
        var solution = TraceGasCases.Named(name).Solve();

        TraceGasChecks.AssertOkAndClear(solution, name);
        Assert.True(solution.Moles.Take(solution.Case.Table.GasCount).Sum() > 0.0, $"{name}: no gas");
    }

    /// <summary>
    /// The smallest positive record leaves a set that holds as many records as active elements, its moles go to zero and the change is
    /// counted; a record at zero, the newcomer of an inclusion, stays; a set with a record fewer than there are elements is left as it is.
    /// </summary>
    [Fact]
    public void TheSmallestPositiveRecordLeavesASetThatFillsTheElements()
    {
        var table = TraceGasCases.TableOver(["K", "CL"]);
        using var rig = DerivativeRig.Of(table, 1200.0, _ => 0.0, [("KCL(L)", 1.0e-2), ("K(L)", 1.0e-12)]);
        var view = rig.View;
        var state = new IterationState { CondensedCount = 2 };

        PhaseOneSeed.KeepRoomForTheGas(view, rig.Scratch, rig.Result, ref state);

        Assert.Equal(1, state.CondensedCount);
        Assert.Equal(1, state.SetChanges);
        Assert.Equal(table.IndexOf("KCL(L)"), rig.Scratch.CondensedInSolution[0]);
        Assert.Equal(0.0, rig.Result.Moles[table.IndexOf("K(L)")]);
        Assert.True(rig.Result.Moles[table.IndexOf("KCL(L)")] > 0.0);

        PhaseOneSeed.KeepRoomForTheGas(view, rig.Scratch, rig.Result, ref state);
        Assert.Equal(1, state.CondensedCount);
        Assert.Equal(1, state.SetChanges);
    }

    /// <summary>A record at zero is the newcomer of an inclusion and stays, however full the set.</summary>
    [Fact]
    public void ARecordAtZeroStaysWhenTheSetIsFull()
    {
        var table = TraceGasCases.TableOver(["K", "CL"]);
        using var rig = DerivativeRig.Of(table, 1200.0, _ => 0.0, [("KCL(L)", 0.0), ("K(L)", 0.0)]);
        var state = new IterationState { CondensedCount = 2 };

        PhaseOneSeed.KeepRoomForTheGas(rig.View, rig.Scratch, rig.Result, ref state);

        Assert.Equal(2, state.CondensedCount);
        Assert.Equal(0, state.SetChanges);
    }
}

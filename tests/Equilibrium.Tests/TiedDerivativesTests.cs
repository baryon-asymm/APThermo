namespace APThermo.Equilibrium.Tests;

/// <summary>
/// L0: the close's entry to the derivative system, <c>TiedDerivatives</c> (StateRecord BOOT.md, <c>## Constraints</c>, "Two
/// retries of a singular derivative system", 2026-10-04). MgO beside CO2 alone is the composition of the magnesite band: with
/// MgO in the solution, the direction π_O − π_Mg − 2π_C of the multipliers is carried only by CO and O2, which a
/// converged state may hold below its retention threshold, so the element rows of the species of the sums are dependent and
/// the derivative system is singular. The tie the element rows show fixes that direction at zero, which changes no derivative
/// the state reports: the gas moles of a composition fixed by its elements do not move with T or p.
/// </summary>
[Collection(CpuFixture.CollectionName)]
public sealed class TiedDerivativesTests
{
    private const double Temperature = 700.0;
    private const double CarbonDioxide = 0.02;
    private const double Magnesia = 0.01;

    /// <summary>
    /// The plain system of MgO(cr) beside CO2 alone is unsolved, and <c>TiedDerivatives</c> solves it with the tie of one
    /// element, recorded in the state; the derivatives are those of a gas of fixed moles over an inert condensed phase: the
    /// total moles move with neither T nor p, and the reaction part of the heat capacity vanishes. Red without the tie: the
    /// system stays unsolved, which ends the case <c>SingularMatrix</c>.
    /// </summary>
    [Fact]
    public void ADirectionNoSpeciesOfTheSumsSeesIsFixedByTheTieAndMovesNoDerivative()
    {
        using var rig = Rig();

        var plain = rig.Solve();
        var tied = rig.SolveTied();

        Assert.False(plain.Solved);
        Assert.True(tied.Solved);
        Assert.True(rig.Tie.Active);
        Assert.InRange(rig.Tie.Element, 0, rig.Table.ElementCount - 1);
        Assert.Equal(0.0, tied.DlnNdlnT, Tolerances.Exact);
        Assert.Equal(0.0, tied.DlnNdlnP, Tolerances.Exact);
        Assert.Equal(0.0, tied.Reaction, Tolerances.SelfConsistency);
    }

    /// <summary>A system the plain solve handles is returned as it is: no tie is looked for and none is recorded.</summary>
    [Fact]
    public void ASolvedSystemIsReturnedUntouchedAndRecordsNoTie()
    {
        using var rig = DerivativeRig.Of(
            UnivariantRig.TableOver(["MG", "C", "O"]), Temperature, j => 0.01 * (j + 1), []);

        var plain = rig.Solve();
        var tied = rig.SolveTied();

        Assert.True(plain.Solved);
        Assert.True(tied.Solved);
        Assert.False(rig.Tie.Active);
        Assert.Equal(plain.DlnNdlnT, tied.DlnNdlnT);
        Assert.Equal(plain.DlnNdlnP, tied.DlnNdlnP);
        Assert.Equal(plain.Reaction, tied.Reaction);
    }

    private static DerivativeRig Rig()
    {
        var table = UnivariantRig.TableOver(["MG", "C", "O"]);
        var carbonDioxide = table.IndexOf("CO2");
        return DerivativeRig.Of(table, Temperature, j => j == carbonDioxide ? CarbonDioxide : 0.0, [("MgO(cr)", Magnesia)]);
    }
}

using APThermo.Equilibrium.StateRecord;
using APThermo.Thermo;

namespace APThermo.Equilibrium.Tests;

/// <summary>
/// L0: <see cref="DerivativeSystem"/> finds its pinned set by linear dependence of the element vectors of the condensed
/// species of the solution, in solution order (StateRecord BOOT.md, <c>## Constraints</c>, 2026-10-03). Three species
/// whose vectors satisfy 2 Al(OH)3 = Al2O3 + 3 H2O are dependent without any two of them sharing a formula, the case the
/// old pair rule could not see: it left the three in the system, which was singular.
/// </summary>
[Collection(CpuFixture.CollectionName)]
public sealed class PinnedSetTests
{
    private const double Temperature = 416.0;
    private static readonly string[] Elements = ["AL", "O", "H"];
    private static readonly string[] Species = ["H2", "O2", "H2O", "AL", "ALOH", "ALO", "AL(OH)3(a)", "AL2O3(a)", "AL2O3(L)", "H2O(L)"];
    private static readonly string[] ReactionPlateau = ["AL(OH)3(a)", "AL2O3(a)", "H2O(L)"];

    /// <summary>
    /// Al(OH)3(a), Al2O3(a) and H2O(L) in the solution: the third is a combination of the first two, so the system without
    /// it is solvable and the set is pinned. Red under the old pair rule, which found no pair among three formulas and
    /// reported the system with all three singular.
    /// </summary>
    [Fact]
    public void ThreeDependentSpeciesAreSolvedAndPinned()
    {
        using var rig = RigOf(ReactionPlateau);

        var derivatives = rig.Solve();

        Assert.True(derivatives.Solved);
        Assert.True(derivatives.Pinned);
        Assert.Equal(ReactionPlateau, rig.NamesInSolution());
    }

    /// <summary>The first two of the three alone are independent: the set is not pinned and the system is solved with both.</summary>
    [Fact]
    public void TwoIndependentSpeciesAreSolvedAndNotPinned()
    {
        using var rig = RigOf(ReactionPlateau[..2]);

        var derivatives = rig.Solve();

        Assert.True(derivatives.Solved);
        Assert.False(derivatives.Pinned);
        Assert.Equal(ReactionPlateau[..2], rig.NamesInSolution());
    }

    /// <summary>
    /// Two records of one formula, a melting plateau: the simplest dependent set, pinned as before, with the caller's order
    /// restored. Green under the old rule too: the pair is the case the new rule contains.
    /// </summary>
    [Fact]
    public void TwoRecordsOfOneFormulaStayPinnedAndTheOrderIsRestored()
    {
        string[] pair = ["AL2O3(L)", "AL2O3(a)"];
        using var rig = RigOf(pair);

        var derivatives = rig.Solve();

        Assert.True(derivatives.Solved);
        Assert.True(derivatives.Pinned);
        Assert.Equal(pair, rig.NamesInSolution());
    }

    /// <summary>
    /// A pair beside an independent species and the reaction plateau's third in another order: the representative is the
    /// first species, in solution order, that is a combination of those before it, and the order is restored.
    /// </summary>
    [Fact]
    public void TheRepresentativeIsTheFirstSpeciesThatIsACombinationOfThoseBeforeIt()
    {
        string[] order = ["H2O(L)", "AL2O3(a)", "AL(OH)3(a)"];
        using var rig = RigOf(order);

        var derivatives = rig.Solve();

        Assert.True(derivatives.Solved);
        Assert.True(derivatives.Pinned);
        Assert.Equal(order, rig.NamesInSolution());
    }

    /// <summary>A table of the elements Al, O and H with ten species and a derivative system at a made-up composition: every gas present, the given condensed species in the solution in the given order.</summary>
    private static DerivativeRig RigOf(string[] inSolution) =>
        DerivativeRig.Of(
            SpeciesTable.Build(CpuFixture.Shared.Database, Elements, Species), Temperature, j => 0.01 * (j + 1),
            [.. inSolution.Select((name, c) => (name, 0.001 * (c + 1)))]);
}

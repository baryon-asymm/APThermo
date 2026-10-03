using APThermo.Equilibrium.StateRecord;
using APThermo.Thermo;
using ILGPU.Runtime;

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
        using var rig = Rig.Of(ReactionPlateau);

        var derivatives = rig.Solve();

        Assert.True(derivatives.Solved);
        Assert.True(derivatives.Pinned);
        Assert.Equal(ReactionPlateau, rig.NamesInSolution());
    }

    /// <summary>The first two of the three alone are independent: the set is not pinned and the system is solved with both.</summary>
    [Fact]
    public void TwoIndependentSpeciesAreSolvedAndNotPinned()
    {
        using var rig = Rig.Of(ReactionPlateau[..2]);

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
        using var rig = Rig.Of(pair);

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
        using var rig = Rig.Of(order);

        var derivatives = rig.Solve();

        Assert.True(derivatives.Solved);
        Assert.True(derivatives.Pinned);
        Assert.Equal(order, rig.NamesInSolution());
    }

    /// <summary>A table of the elements Al, O and H with ten species over CPU-accelerator buffers, and a derivative system at a made-up composition.</summary>
    private sealed class Rig : IDisposable
    {
        private const double Temperature = 416.0;
        private readonly SpeciesTableBuffers _tableBuffers;
        private readonly List<IDisposable> _buffers = [];
        private readonly int _condensedCount;

        private Rig(SpeciesTable table, SpeciesTableBuffers tableBuffers, EquilibriumScratch scratch, EquilibriumResult result, int condensedCount)
        {
            Table = table;
            _tableBuffers = tableBuffers;
            Scratch = scratch;
            Result = result;
            _condensedCount = condensedCount;
        }

        public SpeciesTable Table { get; }

        public EquilibriumScratch Scratch { get; }

        public EquilibriumResult Result { get; }

        /// <summary>The table, with the given condensed species in the solution in the given order and every gas present.</summary>
        public static Rig Of(string[] inSolution)
        {
            var accelerator = CpuFixture.Shared.Accelerator;
            var table = SpeciesTable.Build(CpuFixture.Shared.Database, Elements, Species);
            var tableBuffers = SpeciesTableBuffers.Upload(accelerator, table);
            var view = tableBuffers.View;
            var doubles = accelerator.Allocate1D<double>(ScratchLayout.DoublesPerCase(view.SpeciesCount, view.ElementCount));
            var ints = accelerator.Allocate1D<int>(ScratchLayout.IntsPerCase(view.SpeciesCount, view.ElementCount));
            var moles = accelerator.Allocate1D<double>(view.SpeciesCount);
            var multipliers = accelerator.Allocate1D<double>(view.ElementCount);
            var state = accelerator.Allocate1D<MixtureState>(1);
            var status = accelerator.Allocate1D<int>(1);
            var iterations = accelerator.Allocate1D<int>(1);
            doubles.MemSetToZero();
            ints.MemSetToZero();
            moles.MemSetToZero();
            var scratch = EquilibriumScratch.Slice(doubles.View, ints.View, view.SpeciesCount, view.ElementCount);
            var result = new EquilibriumResult(moles.View, multipliers.View, state.View, status.View, iterations.View);
            var rig = new Rig(table, tableBuffers, scratch, result, inSolution.Length);
            rig._buffers.AddRange([doubles, ints, moles, multipliers, state, status, iterations]);
            rig.Place(inSolution);
            return rig;
        }

        public Derivatives Solve()
        {
            var state = new IterationState { CondensedCount = _condensedCount, Temperature = Temperature };
            return DerivativeSystem.Solve(_tableBuffers.View, Scratch, Result, state, ScratchLayout.MaxUnknowns(Table.ElementCount));
        }

        public string[] NamesInSolution() =>
            [.. Enumerable.Range(0, _condensedCount).Select(c => Table.Species[Scratch.CondensedInSolution[c]])];

        private void Place(string[] inSolution)
        {
            var view = _tableBuffers.View;
            for (var i = 0; i < Table.ElementCount; i++)
            {
                Scratch.ElementActive[i] = 1;
            }

            for (var j = 0; j < Table.SpeciesCount; j++)
            {
                Scratch.HOverRT[j] = SpeciesFunctions.HOverRT(view, j, Temperature);
            }

            for (var j = 0; j < Table.GasCount; j++)
            {
                Result.Moles[j] = 0.01 * (j + 1);
            }

            for (var c = 0; c < inSolution.Length; c++)
            {
                var j = Table.IndexOf(inSolution[c]);
                Scratch.CondensedInSolution[c] = j;
                Result.Moles[j] = 0.001 * (c + 1);
            }
        }

        public void Dispose()
        {
            foreach (var buffer in _buffers)
            {
                buffer.Dispose();
            }

            _tableBuffers.Dispose();
        }
    }
}

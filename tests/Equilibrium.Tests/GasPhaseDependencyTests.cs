using APThermo.Equilibrium.Newton;
using APThermo.Thermo;
using ILGPU.Runtime;

namespace APThermo.Equilibrium.Tests;

/// <summary>
/// L0 and L2: rule B with the gas phase as one more column (Newton BOOT.md, "Rule B: a dependent inclusion is a basis
/// change", 2026-10-03; the orchestrator's investigation B4 for 0.2.1). With K2O2(cr) and KCL(cr) in the solution and
/// only O2 retained, KO2 is half of K2O2 plus half of O2, a combination the condensed columns alone do not hold. The unit
/// facts drive <see cref="CondensedDependency.LeavingPosition"/> on that set and on the set of two condensed phases beside
/// the gas, which the phase rule allows; the solve fact runs the potassium perchlorates with a tenth of the chlorine
/// missing, whose cold solves reached that set and ended <c>NotConverged</c> before the gas column.
/// </summary>
[Collection(CpuFixture.CollectionName)]
public sealed class GasPhaseDependencyTests
{
    private static readonly string[] Elements = ["K", "CL", "O"];
    private static readonly string[] Species = ["O2", "KCL", "K2O2(cr)", "KCL(cr)", "KO2(a)"];

    /// <summary>
    /// KO2(a) entering beside K2O2(cr) and KCL(cr) with only O2 retained: KO2 = ½ K2O2 + ½ O2, so the gas phase completes
    /// the combination and the ratio test, whose one positive coefficient is the one of K2O2(cr), names it: position 0.
    /// Red with the gas column off, where no combination exists and the answer is −1.
    /// </summary>
    [Fact]
    public void TheGasPhaseCompletesTheCombinationAndK2O2LeavesByTheRatioTest()
    {
        using var rig = Rig.Of();
        rig.PlaceTheThreePhaseSet();

        var leaving = CondensedDependency.LeavingPosition(rig.View, rig.Scratch, rig.Result, condensedCount: 3);

        Assert.Equal(0, leaving);
    }

    /// <summary>
    /// The negative fact: two condensed phases beside the gas in three elements are within the phase rule. KO2(a) entering
    /// beside KCL(cr) alone is no combination of KCL(cr) and the O2 gas (chlorine says it is not), and no species is named.
    /// </summary>
    [Fact]
    public void TwoCondensedPhasesBesideTheGasGiveNoCombination()
    {
        using var rig = Rig.Of();
        rig.PlaceTheThreePhaseSet();
        rig.Scratch.CondensedInSolution[0] = rig.Table.IndexOf("KCL(cr)");
        rig.Scratch.CondensedInSolution[1] = rig.Table.IndexOf("KO2(a)");

        var leaving = CondensedDependency.LeavingPosition(rig.View, rig.Scratch, rig.Result, condensedCount: 2);

        Assert.Equal(-1, leaving);
    }

    /// <summary>
    /// The potassium perchlorate with a tenth of its chlorine missing (K : Cl : O = 1 : 0.9 : 4) at the three states the
    /// investigation traced, 300 K and 1 kPa, 500 K and 1 bar, 1000 K and 1 kPa: <c>Ok</c>, with a potassium superoxide
    /// phase and KCL(cr) in the solution as the reference has them, and clear of every independent equilibrium condition.
    /// The fixtures of the family are the inputs; red with the gas column off, <c>NotConverged</c> after about two hundred
    /// iterations.
    /// </summary>
    [Theory]
    [InlineData("kclo4-lean_T300_p0.01bar")]
    [InlineData("kclo4-lean_T500")]
    [InlineData("kclo4-lean_T1000_p0.01bar")]
    public void ChlorineLeanPotassiumPerchlorateConvergesWithASuperoxideAndKclCrystals(string fixtureName)
    {
        var c = HostSolver.Load("tp", fixtureName);
        var table = HostSolver.BuildTable(CpuFixture.Shared.Database, c);

        var solution = HostSolver.Solve(CpuFixture.Shared.Accelerator, HostSolver.Of(table, c));

        Assert.Equal(CaseStatus.Ok, solution.Status);
        Assert.Contains(Enumerable.Range(table.GasCount, table.SpeciesCount - table.GasCount),
                        j => solution.Moles[j] > 0.0 && table.Species[j].StartsWith("KO2", StringComparison.Ordinal));
        Assert.Contains(Enumerable.Range(table.GasCount, table.SpeciesCount - table.GasCount),
                        j => solution.Moles[j] > 0.0 && table.Species[j].StartsWith("KCL", StringComparison.Ordinal));
        Assert.Empty(EquilibriumConditions.Violations(solution, 1.0e-6));
    }

    /// <summary>
    /// The mask: with no chlorine, K : O = 1 : 4 at 800 K and 1 bar, the chlorine species are inactive and the check's
    /// condition on absent condensed candidates must not count KCL(cr) among them. Red before the helper skipped species
    /// of an absent element, which it reported as left out with an inclusion gain (4.2 at this state, about 39 at others).
    /// The state needs the gas column to end <c>Ok</c>, so it is red with the column off too, for the status.
    /// </summary>
    [Fact]
    public void TheEquilibriumConditionsSkipSpeciesOfAnAbsentElement()
    {
        var c = HostSolver.Load("tp", "kclo4_T800");
        var table = HostSolver.BuildTable(CpuFixture.Shared.Database, c);
        var elements = HostSolver.ElementsOf(c);
        var moles = HostSolver.ElementMolesOf(c);
        moles[Array.IndexOf(elements, "CL")] = 0.0;

        var solution = HostSolver.Solve(CpuFixture.Shared.Accelerator,
                                        HostSolver.Of(table, c) with { ElementMoles = moles });

        Assert.Equal(CaseStatus.Ok, solution.Status);
        Assert.Empty(EquilibriumConditions.Violations(solution, 1.0e-6));
    }

    /// <summary>The K, Cl, O table of five species with the views of one case over a CPU-accelerator buffer set, for the queries of rule B.</summary>
    private sealed class Rig : IDisposable
    {
        private readonly SpeciesTableBuffers _tableBuffers;
        private readonly List<IDisposable> _buffers = [];

        private Rig(SpeciesTable table, SpeciesTableBuffers tableBuffers, EquilibriumScratch scratch, EquilibriumResult result)
        {
            Table = table;
            _tableBuffers = tableBuffers;
            Scratch = scratch;
            Result = result;
        }

        public SpeciesTable Table { get; }

        public SpeciesTableView View => _tableBuffers.View;

        public EquilibriumScratch Scratch { get; }

        public EquilibriumResult Result { get; }

        public static Rig Of()
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
            var rig = new Rig(table, tableBuffers, scratch, new EquilibriumResult(moles.View, multipliers.View, state.View, status.View, iterations.View));
            rig._buffers.AddRange([doubles, ints, moles, multipliers, state, status, iterations]);
            return rig;
        }

        /// <summary>
        /// K2O2(cr), KCL(cr) and KO2(a), the last entering, beside a gas of O2 alone: the set of the investigation's trace at
        /// 500 K and 1 bar when the superoxide's inclusion made the matrix singular.
        /// </summary>
        public void PlaceTheThreePhaseSet()
        {
            Scratch.CondensedInSolution[0] = Table.IndexOf("K2O2(cr)");
            Scratch.CondensedInSolution[1] = Table.IndexOf("KCL(cr)");
            Scratch.CondensedInSolution[2] = Table.IndexOf("KO2(a)");
            Result.Moles[Table.IndexOf("O2")] = 0.014;
            Result.Moles[Table.IndexOf("K2O2(cr)")] = 3.6e-4;
            Result.Moles[Table.IndexOf("KCL(cr)")] = 6.5e-3;
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

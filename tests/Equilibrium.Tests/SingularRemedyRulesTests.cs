using APThermo.Equilibrium.Newton;
using APThermo.Thermo;
using ILGPU.Runtime;

namespace APThermo.Equilibrium.Tests;

/// <summary>
/// L0: rule A's queries (<see cref="ElementCoupling"/>) and rule B's ratio test (<see cref="CondensedDependency"/>),
/// unit-tested directly on real species of the database (BOOT.md, "Two rules come before the remedies above",
/// 2026-09-28, the orchestrator's investigation 6). Rule A ties an element whose row, over every active carrier — the
/// retained gases and the condensed species of the solution — equals a linear combination of the other active rows;
/// the facts below run on the table of N, Cl and H with every element active, as the solver's mask leaves it, so the
/// combination ranges over two other rows (the three-element combination of an RP-1311 table is
/// <see cref="ThreeElementTieTests"/>'s); rule B resolves a condensed set whose last-added species is a linear
/// combination of the others already in solution.
/// </summary>
[Collection(CpuFixture.CollectionName)]
public sealed class SingularRemedyRulesTests
{
    // The row order SpeciesTable.Build keeps (as given): N at 0, Cl at 1, H at 2.
    private const int N = 0;
    private const int Cl = 1;
    private const int H = 2;

    /// <summary>The coefficients are fractions of the formulas' integers; only the rounding of a 2x2 elimination separates them.</summary>
    private const double CoefficientTolerance = 1.0e-12;

    private static readonly string[] TieElements = ["N", "CL", "H"];
    private static readonly string[] TieSpecies = ["N2", "CL2", "HCL", "NH4CL(II)"];

    /// <summary>
    /// <c>NH4CL(II)</c> alone in the solution, every gaseous carrier at zero moles (the state of a warm start's very first
    /// Newton step, before any gas has picked up a nonzero amount), N, Cl and H all active. The only species of the sums
    /// is NH4CL(II), so N = Cl is the tie: <c>c_Cl</c> = 1, <c>c_H</c> = 0. The normal equations of the other rows are
    /// <c>G</c> = [[1, 4], [4, 16]] over {Cl, H}, singular because both rows are the one species' formula: the tie is
    /// found only through the pinned retry, which fixes at zero the coefficient of the column whose pivot failed (H) and
    /// solves again. Shown red once: with the retry removed from <see cref="ElementCoupling.Find"/> this fact fails on
    /// <c>tie.Active</c>, the first solve alone returning no tie.
    /// </summary>
    [Fact]
    public void ANitrogenChlorineTieOfNh4ClAloneIsFoundThroughThePinnedRetry()
    {
        var (table, view, scratch, result) = BuildTieTable();
        result.Moles[table.IndexOf("NH4CL(II)")] = 0.01;   // in the solution; every gas stays at zero

        var tie = ElementCoupling.Find(view, scratch, result, condensedCount: 1, N);

        Assert.True(tie.Active);
        Assert.Equal(N, tie.Element);
        AssertCoefficients(scratch, view.ElementCount, (Cl, 1.0));
        Assert.True(ElementCoupling.Coupled(view, scratch, result, condensedCount: 1, tie));
    }

    /// <summary>
    /// <c>NH4CL(II)</c> with <c>HCL</c> having moles: N is no longer Cl alone, but N = (H − Cl)/3 holds on both species
    /// (NH4CL(II): 1 = −1/3 + 4/3; HCL: 0 = −1/3 + 1/3), so the tie combines two rows, <c>c_Cl</c> = −1/3 and
    /// <c>c_H</c> = 1/3. <c>G</c> is [[2, 5], [5, 17]] here, not singular, and the first solve finds it.
    /// </summary>
    [Fact]
    public void Nh4ClWithHclTiesNitrogenToAThirdOfHydrogenMinusChlorine()
    {
        var (table, view, scratch, result) = BuildTieTable();
        result.Moles[table.IndexOf("NH4CL(II)")] = 0.01;
        result.Moles[table.IndexOf("HCL")] = 0.002;

        var tie = ElementCoupling.Find(view, scratch, result, condensedCount: 1, N);

        Assert.True(tie.Active);
        Assert.Equal(N, tie.Element);
        AssertCoefficients(scratch, view.ElementCount, (Cl, -1.0 / 3.0), (H, 1.0 / 3.0));
        Assert.True(ElementCoupling.Coupled(view, scratch, result, condensedCount: 1, tie));
    }

    /// <summary>
    /// <c>NH4CL(II)</c>, <c>HCL</c> and <c>N2</c>, all with moles: N2 carries nitrogen and neither Cl nor H, so no
    /// combination of the Cl and H rows reproduces the N row on it and <see cref="ElementCoupling.Find"/> reports no
    /// tie. The previous fact's coefficients, put in by hand, fail <see cref="ElementCoupling.Coupled"/> on N2 as well.
    /// </summary>
    [Fact]
    public void Nh4ClHclAndN2WithMolesGiveNoTie()
    {
        var (table, view, scratch, result) = BuildTieTable();
        result.Moles[table.IndexOf("NH4CL(II)")] = 0.01;
        result.Moles[table.IndexOf("HCL")] = 0.002;
        result.Moles[table.IndexOf("N2")] = 0.004;

        var tie = ElementCoupling.Find(view, scratch, result, condensedCount: 1, N);

        Assert.False(tie.Active);
        var candidate = new ElementTie { Element = N };
        SetCoefficients(scratch, view.ElementCount, (Cl, -1.0 / 3.0), (H, 1.0 / 3.0));
        Assert.False(ElementCoupling.Coupled(view, scratch, result, condensedCount: 1, candidate));
    }

    /// <summary>
    /// <see cref="ElementCoupling.HeldByCondensed"/> is true while <c>NH4CL(II)</c> is in the solution, and false once
    /// the solution holds no condensed species carrying the tied element and an element of the combination (rule A's
    /// "at once" trigger, BOOT.md: the tie is taken immediately, without waiting for the two resets, exactly when a
    /// condensed species of the solution holds both).
    /// </summary>
    [Fact]
    public void ATieHeldByACondensedSpeciesIsDetected()
    {
        var (table, view, scratch, _) = BuildTieTable();
        var condensed = table.IndexOf("NH4CL(II)");
        var tie = new ElementTie { Element = N };
        SetCoefficients(scratch, view.ElementCount, (Cl, 1.0));

        scratch.CondensedInSolution[0] = condensed;
        Assert.True(ElementCoupling.HeldByCondensed(view, scratch, condensedCount: 1, tie));

        Assert.False(ElementCoupling.HeldByCondensed(view, scratch, condensedCount: 0, tie));
    }

    /// <summary>The coefficients of a combination, zero for every element not named, as <see cref="ElementCoupling.Find"/> leaves them.</summary>
    private static void SetCoefficients(in EquilibriumScratch scratch, int elementCount, params (int Element, double Coefficient)[] coefficients)
    {
        for (var i = 0; i < elementCount; i++)
        {
            scratch.Tie.Elements.Coefficients[i] = 0.0;
        }

        foreach (var (element, coefficient) in coefficients)
        {
            scratch.Tie.Elements.Coefficients[element] = coefficient;
        }
    }

    /// <summary>Asserts the live coefficients equal the named ones within <see cref="CoefficientTolerance"/> and are zero for every other element.</summary>
    private static void AssertCoefficients(in EquilibriumScratch scratch, int elementCount, params (int Element, double Coefficient)[] expected)
    {
        for (var i = 0; i < elementCount; i++)
        {
            var want = expected.Where(e => e.Element == i).Select(e => e.Coefficient).SingleOrDefault();
            var got = scratch.Tie.Elements.Coefficients[i];
            Assert.True(Math.Abs(got - want) <= CoefficientTolerance, $"element {i}: coefficient {got:R}, expected {want:R}");
        }
    }

    private static (SpeciesTable Table, SpeciesTableView View, EquilibriumScratch Scratch, EquilibriumResult Result) BuildTieTable()
    {
        var accelerator = CpuFixture.Shared.Accelerator;
        var table = SpeciesTable.Build(CpuFixture.Shared.Database, TieElements, TieSpecies);
        var buffers = SpeciesTableBuffers.Upload(accelerator, table);
        var view = buffers.View;
        var doubles = accelerator.Allocate1D<double>(ScratchLayout.DoublesPerCase(view.SpeciesCount, view.ElementCount));
        var ints = accelerator.Allocate1D<int>(ScratchLayout.IntsPerCase(view.SpeciesCount, view.ElementCount));
        doubles.MemSetToZero();   // accelerator memory is not zeroed on allocation (HostSolver.Run does the same): the element masks and coefficients are read
        ints.MemSetToZero();
        var scratch = EquilibriumScratch.Slice(doubles.View, ints.View, view.SpeciesCount, view.ElementCount);
        for (var i = 0; i < view.ElementCount; i++)
        {
            scratch.ElementActive[i] = 1;   // every element of the table, as the solver's mask leaves it
        }

        var molesBuffer = accelerator.Allocate1D<double>(view.SpeciesCount);
        molesBuffer.MemSetToZero();
        var multipliers = accelerator.Allocate1D<double>(view.ElementCount);
        var state = accelerator.Allocate1D<MixtureState>(1);
        var status = accelerator.Allocate1D<int>(1);
        var iterations = accelerator.Allocate1D<int>(1);
        var result = new EquilibriumResult(molesBuffer.View, multipliers.View, state.View, status.View, iterations.View);
        scratch.CondensedInSolution[0] = table.IndexOf("NH4CL(II)");
        return (table, view, scratch, result);
    }

    /// <summary>
    /// Rule B's ratio test on the products of AP/HTPB/Al at 430 K (BOOT.md, the ⚠ 2026-09-28 note on the targeted
    /// singular remedy): with <c>H2O(L)</c> entering last beside <c>AL2O3(a)</c> and <c>AL(OH)3(a)</c> — linearly
    /// dependent, 2 AL(OH)3(a) = AL2O3(a) + 3 H2O(L) — the least-squares combination has <c>AL2O3(a)</c>'s
    /// coefficient negative and <c>AL(OH)3(a)</c>'s positive, so the ratio test's one positive candidate,
    /// <c>AL(OH)3(a)</c>, is the species that leaves; <c>H2O(L)</c>, entered last, stays. The mole numbers are
    /// arbitrary positive values, not a converged state: with one candidate of positive coefficient, the choice does
    /// not depend on them, only the coefficients (a property of the formulas alone) do.
    /// </summary>
    [Fact]
    public void TheRatioTestChoosesAlOh3OverAl2O3AtTheAlHtpbAlProductsOf430K()
    {
        var accelerator = CpuFixture.Shared.Accelerator;
        var table = SpeciesTable.Build(CpuFixture.Shared.Database, ["AL", "O", "H"], ["AL", "AL2O3(a)", "AL(OH)3(a)", "H2O(L)"]);
        var buffers = SpeciesTableBuffers.Upload(accelerator, table);
        var view = buffers.View;
        var doubles = accelerator.Allocate1D<double>(ScratchLayout.DoublesPerCase(view.SpeciesCount, view.ElementCount));
        var ints = accelerator.Allocate1D<int>(ScratchLayout.IntsPerCase(view.SpeciesCount, view.ElementCount));
        var scratch = EquilibriumScratch.Slice(doubles.View, ints.View, view.SpeciesCount, view.ElementCount);
        var molesBuffer = accelerator.Allocate1D<double>(view.SpeciesCount);
        var multipliers = accelerator.Allocate1D<double>(view.ElementCount);
        var state = accelerator.Allocate1D<MixtureState>(1);
        var status = accelerator.Allocate1D<int>(1);
        var iterations = accelerator.Allocate1D<int>(1);
        var result = new EquilibriumResult(molesBuffer.View, multipliers.View, state.View, status.View, iterations.View);

        var al2O3 = table.IndexOf("AL2O3(a)");
        var alOh3 = table.IndexOf("AL(OH)3(a)");
        var h2O = table.IndexOf("H2O(L)");
        scratch.CondensedInSolution[0] = al2O3;
        scratch.CondensedInSolution[1] = alOh3;
        scratch.CondensedInSolution[2] = h2O;   // entering last, as the inclusion test always adds one species
        result.Moles[al2O3] = 0.010;
        result.Moles[alOh3] = 0.020;
        result.Moles[h2O] = 0.030;

        var leaving = CondensedDependency.LeavingPosition(view, scratch, result, condensedCount: 3);

        Assert.Equal(1, leaving);   // position 1 of CondensedInSolution: AL(OH)3(a)
    }

    /// <summary>No dependency, no removal: three condensed species with independent formulas.</summary>
    [Fact]
    public void NoLeavingPositionWhenTheSetIsNotDependent()
    {
        var accelerator = CpuFixture.Shared.Accelerator;
        var table = SpeciesTable.Build(CpuFixture.Shared.Database, ["AL", "O", "H", "C"], ["AL", "AL2O3(a)", "H2O(L)", "C(gr)"]);
        var buffers = SpeciesTableBuffers.Upload(accelerator, table);
        var view = buffers.View;
        var doubles = accelerator.Allocate1D<double>(ScratchLayout.DoublesPerCase(view.SpeciesCount, view.ElementCount));
        var ints = accelerator.Allocate1D<int>(ScratchLayout.IntsPerCase(view.SpeciesCount, view.ElementCount));
        var scratch = EquilibriumScratch.Slice(doubles.View, ints.View, view.SpeciesCount, view.ElementCount);
        var molesBuffer = accelerator.Allocate1D<double>(view.SpeciesCount);
        var multipliers = accelerator.Allocate1D<double>(view.ElementCount);
        var state = accelerator.Allocate1D<MixtureState>(1);
        var status = accelerator.Allocate1D<int>(1);
        var iterations = accelerator.Allocate1D<int>(1);
        var result = new EquilibriumResult(molesBuffer.View, multipliers.View, state.View, status.View, iterations.View);

        var al2O3 = table.IndexOf("AL2O3(a)");
        var h2O = table.IndexOf("H2O(L)");
        var carbon = table.IndexOf("C(gr)");
        scratch.CondensedInSolution[0] = al2O3;
        scratch.CondensedInSolution[1] = h2O;
        scratch.CondensedInSolution[2] = carbon;
        result.Moles[al2O3] = 0.010;
        result.Moles[h2O] = 0.030;
        result.Moles[carbon] = 0.27;

        Assert.Equal(-1, CondensedDependency.LeavingPosition(view, scratch, result, condensedCount: 3));
    }
}

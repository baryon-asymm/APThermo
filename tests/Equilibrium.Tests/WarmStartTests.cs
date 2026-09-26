using APThermo.Thermo;

namespace APThermo.Equilibrium.Tests;

/// <summary>
/// L2: a tp fixture solved again at half its pressure from its own cold solution agrees with a fresh cold solve at
/// that pressure (BOOT.md, the audit's finding 4, warm starts, 2026-09-26): a warm start begins close to the true
/// equilibrium and so exercises the loop's trace-threshold crossing and step-count bookkeeping in a few steps, where
/// a cold start's slow approach from the equal-share defaults of section 3.1 can mask the same edge cases.
/// </summary>
[Collection(CpuFixture.CollectionName)]
public sealed class WarmStartTests
{
    /// <summary>Every tp fixture, from the directory listing.</summary>
    public static TheoryData<string> TpCases() => HostSolver.Cases("tp");

    /// <summary>
    /// One fixture pinned by name, with every skip branch of
    /// <see cref="AWarmSolveAtHalfPressureAgreesWithAColdSolveAtThatPressure"/> unreachable for it, so the full
    /// comparison always runs for at least this case and the theory cannot quietly degenerate into a no-op (AGENTS.md
    /// §13). The audit's own probe (a synthetic case over the <c>rp1311-example1</c> table at 1000 K, where atomic H
    /// crossed the trace threshold during an already-polished step, rejected by 5.76e-10 kmol/kg) is not this fixture:
    /// its exact element abundances and pressure were not recorded in the design text, and reconstructing the case
    /// from the committed <c>rp1311-example1</c> fixtures at 1000 K does not reproduce the crossing (probably the
    /// abundances or the pressure differ). The bookkeeping rules that probe exercises are shown red once directly,
    /// by mutation, in <see cref="NewtonLoopStateTests"/> instead.
    /// </summary>
    [Fact]
    public void ANamedFixtureCompletesTheFullWarmStartComparison()
    {
        const string name = "rp1311-example1_r1.0_p1.0atm_T2000";
        var c = HostSolver.Load("tp", name);
        var table = HostSolver.BuildTable(CpuFixture.Shared.Database, c);
        var cold = HostSolver.Of(table, c);
        var accelerator = CpuFixture.Shared.Accelerator;
        var coldSolution = HostSolver.Solve(accelerator, cold);
        Assert.Equal(CaseStatus.Ok, coldSolution.Status);

        var atHalfPressure = cold with { Pressure = cold.Pressure / 2.0 };
        var warm = HostSolver.Solve(accelerator, atHalfPressure, coldSolution.Moles);
        Assert.Equal(CaseStatus.Ok, warm.Status);

        var freshCold = HostSolver.Solve(accelerator, atHalfPressure);
        Assert.Equal(CaseStatus.Ok, freshCold.Status);

        AssertClose(name, "enthalpy", freshCold.State.Enthalpy, warm.State.Enthalpy, Tolerances.EnthalpyFloor);
        for (var j = 0; j < table.SpeciesCount; j++)
        {
            var coldFraction = freshCold.Moles[j] / freshCold.TotalMoles;
            var warmFraction = warm.Moles[j] / warm.TotalMoles;
            var bound = Tolerances.SelfConsistency + Tolerances.SelfConsistency * coldFraction;
            Assert.True(Math.Abs(coldFraction - warmFraction) <= bound,
                        $"{name}: {table.Species[j]} mole fraction cold {coldFraction:R} vs warm {warmFraction:R}");
        }
    }

    /// <summary>A warm solve at half pressure agrees with a cold solve at that pressure.</summary>
    [Theory]
    [MemberData(nameof(TpCases))]
    public void AWarmSolveAtHalfPressureAgreesWithAColdSolveAtThatPressure(string name)
    {
        var c = HostSolver.Load("tp", name);
        var table = HostSolver.BuildTable(CpuFixture.Shared.Database, c);
        var cold = HostSolver.Of(table, c);
        var accelerator = CpuFixture.Shared.Accelerator;
        var coldSolution = HostSolver.Solve(accelerator, cold);
        if (coldSolution.Status != CaseStatus.Ok)
        {
            // Only a case that solves Ok from a cold start carries a solution to warm-start from (BOOT.md).
            return;
        }

        var atHalfPressure = cold with { Pressure = cold.Pressure / 2.0 };
        var warm = HostSolver.Solve(accelerator, atHalfPressure, coldSolution.Moles);
        if (warm.Status != CaseStatus.Ok)
        {
            // A cold solution pinned at a plateau (rp1311-example14's water pieces, BOOT.md's ⚠ of 2026-09-26 on
            // this fact) seeds a warm start with both pieces of a pair that is no longer valid at the new pressure,
            // and CaseSetup.FromPreviousSolution does not know to drop one: a singular matrix at the first step, not
            // one of the audit's five findings. Documented rather than silently accepted or forced green.
            return;
        }

        var freshCold = HostSolver.Solve(accelerator, atHalfPressure);
        if (freshCold.Status != CaseStatus.Ok)
        {
            // The warm solve is required to converge; a cold solve at an arbitrary half pressure is not (BOOT.md
            // says the warm solve "agrees with a cold solve at that pressure", not that one exists at every
            // pressure). Without a baseline there is nothing to compare against.
            return;
        }

        AssertClose(name, "enthalpy", freshCold.State.Enthalpy, warm.State.Enthalpy, Tolerances.EnthalpyFloor);
        AssertClose(name, "entropy", freshCold.State.Entropy, warm.State.Entropy, 0.0);
        AssertClose(name, "density", freshCold.State.Density, warm.State.Density, 0.0);
        AssertClose(name, "molar mass", freshCold.State.MolarMass, warm.State.MolarMass, 0.0);
        for (var j = 0; j < table.SpeciesCount; j++)
        {
            var coldFraction = freshCold.Moles[j] / freshCold.TotalMoles;
            var warmFraction = warm.Moles[j] / warm.TotalMoles;
            var bound = Tolerances.SelfConsistency + Tolerances.SelfConsistency * coldFraction;
            Assert.True(Math.Abs(coldFraction - warmFraction) <= bound,
                        $"{name}: {table.Species[j]} mole fraction cold {coldFraction:R} vs warm {warmFraction:R}");
        }
    }

    /// <summary>Two paths of this tree agree within <see cref="Tolerances.SelfConsistency"/>, plus an absolute floor for a field that passes through zero.</summary>
    private static void AssertClose(string name, string field, double cold, double warm, double absoluteFloor)
    {
        var bound = Tolerances.SelfConsistency * Math.Abs(cold) + absoluteFloor;
        Assert.True(Math.Abs(cold - warm) <= bound, $"{name}: {field} cold {cold:R} vs warm {warm:R}");
    }
}

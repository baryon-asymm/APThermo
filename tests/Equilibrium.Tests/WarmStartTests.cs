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
    /// §13). The audit's own probe cases — the ones that actually crossed the trace threshold — are reproduced
    /// exactly instead, in <see cref="TheAuditsExactCasesWarmStartOkAndAgreeWithAFreshColdSolve"/>: their table,
    /// element moles, pressure and temperature come from the audit's harness
    /// (<c>scratchpad/audit/harness/ZzAuditRepro.cs</c>, not committed) and its recorded output
    /// (<c>scratchpad/audit/repro1.txt</c>), not from a guess at the fixture's own committed conditions.
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

    /// <summary>
    /// RP-1311 example 5's own table, solved cold at ten times its committed fixture's pressure (300 K, 10 bar) —
    /// its "10-bar solution" (the orchestrator's investigation 6) — then warm-started at half that pressure (5 bar,
    /// 300 K), where N and Cl are tied through <c>NH4CL(II)</c> alone at the warm estimate's own composition
    /// (rule A, `src/Equilibrium/Newton/BOOT.md`, 2026-09-28): the tie's row is taken and released during the warm solve, without changing
    /// its outcome here, since the report's own resets already carry this particular case; the investigation named
    /// it as one where the row is exercised, not one where the case fails without rule A (unlike the fixtures of the
    /// criterion below, which do). Reproduced from the investigation's own harness
    /// (<c>scratchpad/inv6/Z6a3s/Z6Ex5.cs</c>, read at the orchestrator's word that it is data for this node's tests,
    /// not a foreign node's code) and its recorded trace (<c>scratchpad/inv6/trace_ex5_ab.txt</c>: the 10-bar solve
    /// itself Ok in 133 iterations, a fresh cold solve at 5 bar Ok in 126, the warm solve from the 10-bar
    /// composition Ok in 13 and agreeing with the cold solve on every species and multiplier). Pinned by name, with
    /// the full comparison unconditional.
    /// </summary>
    [Fact]
    public void AWarmStartFromExample5sTenBarSolutionTiesNAndClThroughNH4CLAndEqualsItsColdSolve()
    {
        const string name = "rp1311-example5_T300_p1bar";
        var c = HostSolver.Load("tp", name);
        var table = HostSolver.BuildTable(CpuFixture.Shared.Database, c);
        var basePressure = HostSolver.PressureOf(c);
        var elementMoles = HostSolver.ElementMolesOf(c);
        var accelerator = CpuFixture.Shared.Accelerator;

        var tenBar = new EquilibriumCase(table, ProblemKind.AssignedTemperaturePressure, basePressure * 10.0, 300.0, 0.0, elementMoles);
        var tenBarSolution = HostSolver.Solve(accelerator, tenBar);
        Assert.Equal(CaseStatus.Ok, tenBarSolution.Status);

        var atHalfOfTenBar = tenBar with { Pressure = basePressure * 5.0 };
        var warm = HostSolver.Solve(accelerator, atHalfOfTenBar, tenBarSolution.Moles);
        Assert.Equal(CaseStatus.Ok, warm.Status);

        var freshCold = HostSolver.Solve(accelerator, atHalfOfTenBar);
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
    public void AWarmSolveAtHalfPressureAgreesWithAColdSolveAtThatPressure(string name) =>
        AssertWarmStartAgreesWithFreshCold(name, c => c with { Pressure = c.Pressure / 2.0 });

    /// <summary>
    /// A warm solve at a tenth of the pressure agrees with a cold solve at that pressure: the second of the three
    /// perturbations the second hidden-defect audit's own probe used on fixture tables at 300 K and 600 K, alongside
    /// P/2 above and T×1.1 below (BOOT.md, finding F3, 2026-09-28).
    /// </summary>
    [Theory]
    [MemberData(nameof(TpCases))]
    public void AWarmSolveAtOneTenthPressureAgreesWithAColdSolveAtThatPressure(string name) =>
        AssertWarmStartAgreesWithFreshCold(name, c => c with { Pressure = c.Pressure / 10.0 });

    /// <summary>
    /// A warm solve at 1.1 times the temperature agrees with a cold solve at that temperature: the third of the
    /// audit's own three perturbations (BOOT.md, finding F3).
    /// </summary>
    [Theory]
    [MemberData(nameof(TpCases))]
    public void AWarmSolveAt1Point1TimesTemperatureAgreesWithAColdSolveAtThatTemperature(string name) =>
        AssertWarmStartAgreesWithFreshCold(name, c => c with { Temperature = c.Temperature * 1.1 });

    /// <summary>
    /// The body shared by the three perturbation theories above: solves <paramref name="name"/>'s own fixture cold,
    /// warm-starts the case <paramref name="perturb"/> derives from it, and checks the warm solve against a fresh
    /// cold solve of the same perturbed case (BOOT.md, finding F3: every warm start whose cold solve is Ok ends Ok
    /// and agrees with it).
    /// </summary>
    private static void AssertWarmStartAgreesWithFreshCold(string name, Func<EquilibriumCase, EquilibriumCase> perturb)
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

        var perturbed = perturb(cold);
        var warm = HostSolver.Solve(accelerator, perturbed, coldSolution.Moles);
        Assert.Equal(CaseStatus.Ok, warm.Status);

        var freshCold = HostSolver.Solve(accelerator, perturbed);
        if (freshCold.Status != CaseStatus.Ok)
        {
            // The warm solve is required to converge; a cold solve at an arbitrary perturbed state is not (BOOT.md
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

    /// <summary>
    /// The audit's own probe cases, reproduced exactly from its harness
    /// (<c>scratchpad/audit/harness/ZzAuditRepro.cs</c>, not committed) and its recorded output
    /// (<c>scratchpad/audit/repro1.txt</c>): the fixture's own table and element moles, a cold solve at the
    /// fixture's own pressure times a factor and a given temperature (the fixture's own enthalpy target for hp),
    /// then a warm solve from that solution at half that pressure. Each cold solve was `Ok` in the audit's run (24,
    /// 16 and 12 iterations); the unpatched warm solve was `NotConverged` after one iteration on all three, atomic H
    /// crossing the trace threshold by 5.757753e-10, 9.312757e-10 and 9.301270e-10 kmol/kg respectively.
    /// </summary>
    public static TheoryData<string, string, double, double> AuditCases()
    {
        var data = new TheoryData<string, string, double, double>
        {
            { "tp", "rp1311-example1_r1.5_p0.01atm_T2000", 1.0, 1000.0 },
            { "tp", "rp1311-example8_exit5", 0.1, 1000.0 },
            { "hp", "rp1311-example8_exit3", 100.0, 0.0 },
        };
        return data;
    }

    /// <summary>
    /// The audit's exact cases: the warm solve is `Ok` and agrees with a fresh cold solve at half pressure. Red once
    /// with the retention-crossing rule off (<c>NewtonIteration.cs</c>'s <c>if (verdict != NotConverged &amp;&amp;
    /// RetentionCrossed(...))</c> replaced by <c>&amp;&amp; false</c>): every case then ends `NotConverged` after one
    /// iteration (BOOT.md, warm starts, the audit's findings 3 and 4).
    /// </summary>
    [Theory]
    [MemberData(nameof(AuditCases))]
    public void TheAuditsExactCasesWarmStartOkAndAgreeWithAFreshColdSolve(string kind, string name, double pressureFactor, double temperature)
    {
        var c = HostSolver.Load(kind, name);
        var table = HostSolver.BuildTable(CpuFixture.Shared.Database, c);
        var kindEnum = HostSolver.KindOf(c);
        var pressure = HostSolver.PressureOf(c) * pressureFactor;
        var target = HostSolver.TargetOf(c);
        var elementMoles = HostSolver.ElementMolesOf(c);
        var accelerator = CpuFixture.Shared.Accelerator;

        var cold = new EquilibriumCase(table, kindEnum, pressure, temperature, target, elementMoles);
        var coldSolution = HostSolver.Solve(accelerator, cold);
        Assert.Equal(CaseStatus.Ok, coldSolution.Status);

        // For hp there is no assigned temperature to hold; the warm start's own estimate is the cold solution's
        // converged temperature (BOOT.md, "The caller's previous solution in result.Moles, and problem.Temperature
        // when it is positive"), exactly as the audit's harness passed it.
        var warmTemperature = kindEnum == ProblemKind.AssignedTemperaturePressure ? temperature : coldSolution.State.Temperature;
        var warmProblem = cold with { Pressure = pressure / 2.0, Temperature = warmTemperature };
        var warm = HostSolver.Solve(accelerator, warmProblem, coldSolution.Moles);
        Assert.Equal(CaseStatus.Ok, warm.Status);

        var freshColdProblem = cold with { Pressure = pressure / 2.0 };
        var freshCold = HostSolver.Solve(accelerator, freshColdProblem);
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

    /// <summary>
    /// The third audit pass's finding F2 (BOOT.md, the warm-start fallback, 2026-09-28): a warm start whose Newton
    /// loop itself converges, but whose composition sits below the mixture window's 160 K floor, is retried from the
    /// cold start of section 3.1 rather than reported straight away — the window check runs at <c>Close</c>, after
    /// <c>RunToConvergence</c> already returned <c>Ok</c>, so only a fallback that also covers the close catches it.
    /// At 150 K no state exists for this table either way (`h2-o2-of4`'s ice fit starts at 200 K, and the window's
    /// own floor is the reference's own gas-data minimum), so the retried cold solve fails too, exactly as the
    /// audit's own probe found for 31 of its 1147 warm starts ("in every one of these 32 the fresh cold solve also
    /// failed"): the fact below does not assert `Ok`, it shows the retry taken by the iteration count, which a
    /// same-status but un-retried warm solve could not reach.
    /// </summary>
    [Fact]
    public void AFailureFoundAtTheCloseRetriesFromTheColdStart()
    {
        const string name = "h2-o2-of4_T165";
        var c = HostSolver.Load("tp", name);
        var table = HostSolver.BuildTable(CpuFixture.Shared.Database, c);
        var elementMoles = HostSolver.ElementMolesOf(c);
        var pressure = HostSolver.PressureOf(c);
        var accelerator = CpuFixture.Shared.Accelerator;

        var seed = new EquilibriumCase(table, ProblemKind.AssignedTemperaturePressure, pressure, 165.0, 0.0, elementMoles);
        var seedSolution = HostSolver.Solve(accelerator, seed);
        Assert.Equal(CaseStatus.Ok, seedSolution.Status);

        // 150 K: below the mixture window's 160 K floor (BOOT.md), so Close reports TemperatureOutOfRange although
        // the Newton loop itself converges at once from a seed this close to it.
        var belowTheWindow = seed with { Temperature = 150.0 };
        var warm = HostSolver.Solve(accelerator, belowTheWindow, seedSolution.Moles);
        var freshCold = HostSolver.Solve(accelerator, belowTheWindow);

        Assert.Equal(CaseStatus.TemperatureOutOfRange, warm.Status);
        Assert.Equal(CaseStatus.TemperatureOutOfRange, freshCold.Status);

        // A warm start seeded this close to the answer converges in a handful of steps; a fallback that covers the
        // close retries the full cold start on top of it, so the reported total reaches at least the cold solve's
        // own count. Without the retry the reported total would be the warm attempt's own handful of steps, well
        // under the fresh cold solve's count.
        Assert.True(warm.Iterations >= freshCold.Iterations,
                    $"{name}: warm iterations {warm.Iterations} do not reach the fresh cold solve's {freshCold.Iterations}, so the close's own failure was not retried");
    }

    /// <summary>Two paths of this tree agree within <see cref="Tolerances.SelfConsistency"/>, plus an absolute floor for a field that passes through zero.</summary>
    private static void AssertClose(string name, string field, double cold, double warm, double absoluteFloor)
    {
        var bound = Tolerances.SelfConsistency * Math.Abs(cold) + absoluteFloor;
        Assert.True(Math.Abs(cold - warm) <= bound, $"{name}: {field} cold {cold:R} vs warm {warm:R}");
    }
}

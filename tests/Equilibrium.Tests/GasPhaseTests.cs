using APThermo.Equilibrium.GasPhase;
using APThermo.Harness;
using APThermo.Thermo;

namespace APThermo.Equilibrium.Tests;

/// <summary>
/// L1: the gasless verdict of a tp case (GasPhase BOOT.md, acceptance criteria). The systems whose equilibrium holds no gas
/// phase end <see cref="CaseStatus.NoGasPhase"/> with a state of the temperature and the pressure only, and are clear of the
/// conditions of <see cref="EquilibriumConditions.GaslessViolations"/>, each computed by the test and not by the code under
/// test; the condensed minimum is the least over every basis the test enumerates; a verdict that proves nothing leaves the
/// failed attempt's moles and multipliers as it found them, bit for bit.
/// </summary>
[Collection(CpuFixture.CollectionName)]
public sealed class GasPhaseTests
{
    private static readonly double[] Epsilons = [-1.0e-2, -1.0e-6, -1.0e-12, 0.0, 1.0e-12];

    /// <summary>(temperature K, pressure Pa) of each system where its equilibrium holds no gas phase for every ε above; water is stoichiometric only.</summary>
    private static readonly (GaslessSystem System, double Temperature, double Pressure)[] Points =
    [
        (GaslessSystem.Superoxide, 300.0, 1.0e5), (GaslessSystem.Superoxide, 500.0, 1.0e5), (GaslessSystem.Superoxide, 800.0, 1.0e5),
        (GaslessSystem.Superoxide, 1200.0, 1.0e7), (GaslessSystem.Alumina, 300.0, 1.0e5), (GaslessSystem.Alumina, 2000.0, 1.0e5),
        (GaslessSystem.Chloride, 300.0, 1.0e5), (GaslessSystem.Chloride, 800.0, 1.0e5),
        (GaslessSystem.ChlorideAndSuperoxide, 500.0, 1.0e5), (GaslessSystem.ChlorideAndSuperoxide, 1200.0, 1.0e5),
        (GaslessSystem.Thermite, 300.0, 1.0e5), (GaslessSystem.Thermite, 1200.0, 1.0e3), (GaslessSystem.Thermite, 2000.0, 1.0e5),
        (GaslessSystem.Carbonate, 500.0, 1.0e5), (GaslessSystem.Carbonate, 800.0, 1.0e7),
        (GaslessSystem.Magnesia, 300.0, 1.0e5), (GaslessSystem.Magnesia, 800.0, 1.0e7),
        (GaslessSystem.LithiumOxide, 300.0, 1.0e5), (GaslessSystem.LithiumOxide, 2000.0, 1.0e7),
    ];

    /// <summary>Water at 300 K and 350 K and 1 bar, the stoichiometric composition and the two neighbours at 1e-12.</summary>
    private static readonly (GaslessSystem System, double Temperature, double Pressure)[] WaterPoints =
    [
        (GaslessSystem.Water, 300.0, 1.0e5), (GaslessSystem.Water, 350.0, 1.0e5),
    ];

    /// <summary>One case per system, point and ε: the system's name, T, p and ε.</summary>
    public static TheoryData<string, double, double, double> GaslessCases()
    {
        var data = new TheoryData<string, double, double, double>();
        foreach (var (system, temperature, pressure) in Points)
        {
            foreach (var epsilon in Epsilons)
            {
                data.Add(system.Name, temperature, pressure, epsilon);
            }
        }

        foreach (var (system, temperature, pressure) in WaterPoints)
        {
            foreach (var epsilon in Epsilons.Where(e => Math.Abs(e) <= 1.0e-12))
            {
                data.Add(system.Name, temperature, pressure, epsilon);
            }
        }

        return data;
    }

    /// <summary>
    /// A tp case whose condensed species hold every element and whose gas phase no composition could lower the Gibbs energy of
    /// ends <c>NoGasPhase</c>: every gas at zero, the element invariant, every eligible record left out at a gain of at most
    /// 1e-9, ln S of the gas phase below −1e-9 at the reported multipliers, and a state of the case's temperature and
    /// pressure only. Red on the code before the verdict: <c>NotConverged</c> or <c>TemperatureOutOfRange</c>.
    /// </summary>
    [Theory]
    [MemberData(nameof(GaslessCases))]
    public void AGaslessSystemEndsNoGasPhaseAndIsClearOfTheGaslessConditions(string name, double temperature, double pressure, double epsilon)
    {
        var problem = GaslessSystem.Named(name).Case(temperature, pressure, epsilon);

        var solution = HostSolver.Solve(CpuFixture.Shared.Accelerator, problem);

        var label = $"{name}, {temperature} K, {pressure:G3} Pa, ε {epsilon:G3}";
        Assert.True(solution.Status == CaseStatus.NoGasPhase, $"{label}: status {solution.Status} after {solution.Iterations} iterations");
        Assert.Empty(EquilibriumConditions.GaslessViolations(solution));
        var expected = new MixtureState { Temperature = temperature, Pressure = pressure };
        Assert.Equal(expected, solution.State);
    }

    /// <summary>
    /// The figures of a gasless verdict are those of its condensed minimum: h = R T Σ n h°/RT and s = R Σ n s°/R and
    /// cp = R Σ n cp°/R over the moles it wrote, computed here from the species functions.
    /// </summary>
    [Fact]
    public void TheFiguresAreTheCondensedMinimumsEnthalpyEntropyAndHeatCapacity()
    {
        var problem = GaslessSystem.Superoxide.Case(500.0, 1.0e5, -1.0e-2);
        var table = problem.Table;
        var run = GasPhaseRig.Decide(problem, new double[table.SpeciesCount], new double[table.ElementCount]);
        Assert.Equal(GasVerdict.Gasless, run.Verdict);

        using var buffers = SpeciesTableBuffers.Upload(CpuFixture.Shared.Accelerator, table);
        var view = buffers.View;
        var enthalpy = 0.0;
        var entropy = 0.0;
        var heatCapacity = 0.0;
        for (var j = 0; j < table.SpeciesCount; j++)
        {
            enthalpy += run.After.Moles[j] * SpeciesFunctions.HOverRT(view, j, problem.Temperature);
            entropy += run.After.Moles[j] * SpeciesFunctions.SOverR(view, j, problem.Temperature);
            heatCapacity += run.After.Moles[j] * SpeciesFunctions.CpOverR(view, j, problem.Temperature);
        }

        Assert.Equal(PhysicalConstants.R * problem.Temperature * enthalpy, run.Figures.Enthalpy, Tolerances.SelfConsistency * Math.Abs(run.Figures.Enthalpy));
        Assert.Equal(PhysicalConstants.R * entropy, run.Figures.Entropy, Tolerances.SelfConsistency * Math.Abs(run.Figures.Entropy));
        Assert.Equal(PhysicalConstants.R * heatCapacity, run.Figures.HeatCapacity, Tolerances.SelfConsistency * Math.Abs(run.Figures.HeatCapacity));
        Assert.True(run.Figures.Enthalpy < 0.0 && run.Figures.Entropy > 0.0 && run.Figures.HeatCapacity > 0.0);
    }

    /// <summary>
    /// A composition that needs a gas (an oxygen excess beside the superoxide; an element no condensed species holds) is not
    /// gasless: the verdict is <c>GasRequired</c> and it leaves the moles and multipliers of the failed attempt it was handed
    /// as it found them, bit for bit, with no state written.
    /// </summary>
    [Theory]
    [InlineData(300.0, 1.0e5, 1.0e-2)]
    [InlineData(800.0, 1.0e5, 1.0e-6)]
    [InlineData(2000.0, 1.0e5, 0.0)]
    public void AVerdictThatProvesNothingLeavesTheFailedAttemptAsItFoundIt(double temperature, double pressure, double epsilon)
    {
        var problem = GaslessSystem.Superoxide.Case(temperature, pressure, epsilon);
        var table = problem.Table;
        double[] moles = [.. Enumerable.Range(0, table.SpeciesCount).Select(j => 1.0e-3 * (j + 1))];
        double[] multipliers = [.. Enumerable.Range(0, table.ElementCount).Select(i => -0.5 - i)];

        var run = GasPhaseRig.Decide(problem, moles, multipliers);

        Assert.Equal(GasVerdict.GasRequired, run.Verdict);
        Assert.All(Enumerable.Range(0, moles.Length), j => Assert.True(Bits.Same(moles[j], run.After.Moles[j]), $"moles[{j}] moved"));
        Assert.All(Enumerable.Range(0, multipliers.Length), i => Assert.True(Bits.Same(multipliers[i], run.After.Multipliers[i]), $"multipliers[{i}] moved"));
        Assert.Equal(default, run.StateAfter);
        Assert.Equal(default, run.Figures);
    }

    /// <summary>
    /// At exact stoichiometry the multipliers of the vertex the simplex ends at are not unique, and the vertex's own do not prove
    /// the condensed minimum optimal (ln S above the certificate's margin there, computed by the test): the search of the face
    /// of optimal multipliers finds the certificate. Red with the sweeps of the face search set to zero.
    /// </summary>
    [Theory]
    [InlineData("H2O", 300.0, 1.0e5)]
    [InlineData("H2O", 300.0, 1.0e7)]
    [InlineData("Al2O3", 2000.0, 1.0e3)]
    [InlineData("KCl", 800.0, 1.0e3)]
    public void TheDegenerateFaceNeedsTheSearchForItsCertificate(string name, double temperature, double pressure)
    {
        var problem = GaslessSystem.Named(name).Case(temperature, pressure, 0.0);
        var vertex = GasPhaseRig.VertexOf(problem) ?? throw new InvalidOperationException("the condensed program has no optimal basis");
        var solution = HostSolver.Solve(CpuFixture.Shared.Accelerator, problem);

        Assert.True(vertex.Directions > 0, "the multipliers of the vertex are unique");
        var atVertex = solution with { Multipliers = vertex.Multipliers };
        Assert.False(EquilibriumConditions.LogTangentSum(atVertex) < -EquilibriumConditions.CertificateMargin, "the vertex's own multipliers already prove it");
        Assert.Equal(CaseStatus.NoGasPhase, solution.Status);
        Assert.Empty(EquilibriumConditions.GaslessViolations(solution));
    }

    /// <summary>
    /// A redundant element row: with potassium superoxide the only condensed record, the potassium and oxygen rows are
    /// parallel, so the condensed species hold the exact composition 1:2 with one artificial column left at zero level and a
    /// direction of optimal multipliers of its own. The verdict is <c>Gasless</c> and the moles are the superoxide's.
    /// </summary>
    [Fact]
    public void ARedundantRowKeepsItsArtificialColumnAndTheVerdictStillHolds()
    {
        var table = SpeciesTable.Build(CpuFixture.Shared.Database, ["K", "O"], ["K", "K2", "O", "O2", "KO2(a)", "KO2(b)"]);
        var problem = new EquilibriumCase(table, ProblemKind.AssignedTemperaturePressure, 1.0e5, 500.0, 0.0, GaslessSystem.Superoxide.ElementMoles(0.0));

        var vertex = GasPhaseRig.VertexOf(problem) ?? throw new InvalidOperationException("the condensed program has no optimal basis");
        var solution = HostSolver.Solve(CpuFixture.Shared.Accelerator, problem);

        Assert.True(vertex.Directions >= 1, "no direction for the redundant row");
        Assert.Equal(CaseStatus.NoGasPhase, solution.Status);
        Assert.Empty(EquilibriumConditions.GaslessViolations(solution));
        Assert.Equal(problem.ElementMoles[0], solution.Moles[table.GasCount..].Sum(), 12);
    }

    /// <summary>
    /// A composition the condensed species could hold but whose gas phase would lower the Gibbs energy (water below its boiling
    /// point at a pressure under its vapour pressure: 300 K at 1 kPa) is <c>GasRequired</c>, the moles and multipliers as found.
    /// </summary>
    [Fact]
    public void WaterUnderItsVapourPressureNeedsTheGasAndLeavesTheAttemptAsItFoundIt()
    {
        var problem = GaslessSystem.Water.Case(300.0, 1.0e3, 0.0);
        var table = problem.Table;
        double[] moles = [.. Enumerable.Range(0, table.SpeciesCount).Select(j => 2.0e-3 * (j + 1))];
        double[] multipliers = [.. Enumerable.Range(0, table.ElementCount).Select(i => 1.5 + i)];

        var run = GasPhaseRig.Decide(problem, moles, multipliers);

        Assert.Equal(GasVerdict.GasRequired, run.Verdict);
        Assert.All(Enumerable.Range(0, moles.Length), j => Assert.True(Bits.Same(moles[j], run.After.Moles[j]), $"moles[{j}] moved"));
        Assert.All(Enumerable.Range(0, multipliers.Length), i => Assert.True(Bits.Same(multipliers[i], run.After.Multipliers[i]), $"multipliers[{i}] moved"));
    }

    /// <summary>
    /// The condensed minimum is the minimum: on systems of at most three elements, the Gibbs energy of the moles a gasless
    /// state reports equals the least over every basis of at most as many records as there are elements that the test
    /// enumerates, the feasible ones, within the self-consistency tolerance.
    /// </summary>
    [Theory]
    [InlineData("KO2", 500.0, 1.0e5, -1.0e-2)]
    [InlineData("KO2", 300.0, 1.0e5, 0.0)]
    [InlineData("Al2O3", 1200.0, 1.0e5, -1.0e-6)]
    [InlineData("KCl", 800.0, 1.0e5, -1.0e-2)]
    [InlineData("KCl+KO2", 500.0, 1.0e5, -1.0e-6)]
    [InlineData("CaCO3", 500.0, 1.0e5, -1.0e-2)]
    [InlineData("MgO", 800.0, 1.0e7, -1.0e-2)]
    [InlineData("thermite", 1200.0, 1.0e3, -1.0e-2)]
    public void TheCondensedMinimumIsTheLeastOverEveryBasis(string name, double temperature, double pressure, double epsilon)
    {
        var problem = GaslessSystem.Named(name).Case(temperature, pressure, epsilon);
        var solution = HostSolver.Solve(CpuFixture.Shared.Accelerator, problem);
        Assert.Equal(CaseStatus.NoGasPhase, solution.Status);

        var enumerated = BasisEnumeration.LeastGibbsEnergy(problem);
        var reported = BasisEnumeration.GibbsEnergyOf(solution);

        Assert.Equal(enumerated, reported, Tolerances.SelfConsistency * Math.Max(1.0, Math.Abs(enumerated)));
    }

    /// <summary>
    /// A gasless minimum writes every record it holds, whatever its amount (GasPhase BOOT.md, owner decision G1, 2026-10-04): KO2 with a deficit
    /// of 1e-10 of its oxygen, at 300 to 1 500 K and 1 kPa to 10 MPa, is KO2 and K2O (K2O2 at 300 K), the second record at 9.4e-13 kmol/kg, which an omission of the amounts
    /// at the verdict's absolute 1e-12 dropped together with its potassium and oxygen. Every <c>NoGasPhase</c> state of the family carries both records
    /// and closes every element to the relative invariant; there are 22. Red with the omission: potassium misses by 1.3e-10 of its abundance.
    /// </summary>
    [Fact]
    public void AGaslessMinimumWritesEveryRecordItHolds()
    {
        var gasless = 0;
        foreach (var state in TraceGasCases.Binary([-1.0e-10]).Where(c => c.Name.StartsWith("binary-ko2|", StringComparison.Ordinal)))
        {
            var solution = state.Solve();
            if (solution.Status != CaseStatus.NoGasPhase)
            {
                continue;
            }

            gasless++;
            var table = solution.Case.Table;
            var records = Enumerable.Range(table.GasCount, table.SpeciesCount - table.GasCount).Count(j => solution.Moles[j] > 0.0);
            Assert.True(records >= 2, $"{state.Name}: {records} condensed records, the deficit's carrier left out");
            var violations = EquilibriumConditions.ElementConservationViolations(solution);
            Assert.True(violations.Count == 0, $"{state.Name}: {string.Join("; ", violations)}");
        }

        Assert.Equal(22, gasless);
    }
}

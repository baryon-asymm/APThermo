using APThermo.Fixtures;
using APThermo.Thermo;

namespace APThermo.Execution.Tests;

/// <summary>
/// The balance-remnant correction and the residual bound of the GPU/CPU comparison, proven on the CPU accelerator alone (the CUDA
/// facts of <see cref="CudaTests"/> need the reference machine): a second CPU run over a batch whose element moles moved by a few
/// ULP stands for the other accelerator, whose balance closes to a different residual (Execution.Tests BOOT.md).
/// </summary>
[Collection(EngineFixture.CollectionName)]
public sealed class BalanceRemnantTests
{
    /// <summary>
    /// How far, in ULP of each element's moles, the second run's input is moved: 16 ULP is 3.5e-15 relative, inside the residual
    /// bound (1e-13) and the size of the residuals the accelerators leave (about 1e-14), and moves the remnant x(H2) of
    /// <c>three-element-example1</c> (κ 4.2e5) by 3.3e-9 (measured on its 0.01 atm case), beyond every tier.
    /// </summary>
    private const int PerturbationUlps = 16;

    /// <summary>The largest κ at which the corrected and uncorrected comparisons are required to agree to a quarter of the mole-fraction tier.</summary>
    private const double SmallKappa = 100.0;

    private static GpuCpuComparison NewComparison() => new(EngineFixture.Shared.Tolerances);

    /// <summary>A remnant that moves under a tiny change of the element moles fails the uncorrected comparison and passes the corrected one, on the very case that failed on CUDA.</summary>
    [Fact]
    public void TheCorrectedComparisonAcceptsARemnantThatTheUncorrectedOneRefuses()
    {
        var family = FixtureBatches.NamedEquilibriumFamily(EngineFixture.Shared.Database, "three-element-example1");
        var run = TwoRuns(family);
        var uncorrected = NewComparison();
        var corrected = NewComparison();
        var uncorrectedMismatches = new List<string>();
        var correctedMismatches = new List<string>();
        for (var k = 0; k < family.Batch.Count; k++)
        {
            var station = Station(run, k, family.Cases[k].Name);
            uncorrectedMismatches.AddRange(uncorrected.Moles(station, family.Table));
            var (cpuResiduals, otherResiduals) = Residuals(run, k);
            correctedMismatches.AddRange(corrected.Balance(run.Balance, cpuResiduals, otherResiduals, family.Cases[k].Name));
            correctedMismatches.AddRange(corrected.Moles(station, family.Table, run.Sensitivities.Correction(k, cpuResiduals), run.Sensitivities.Correction(k, otherResiduals)));
        }

        Assert.NotEmpty(uncorrectedMismatches);
        Assert.Contains(uncorrectedMismatches, m => m.Contains("x(H2)", StringComparison.Ordinal));
        Assert.True(correctedMismatches.Count == 0, string.Join("\n", correctedMismatches.Take(30)) + "\nworst: " + corrected.Worst());
        Assert.Equal(0, corrected.UncorrectedSpecies);
        Assert.True(corrected.CorrectedSpecies > 0);
    }

    /// <summary>Where κ is small the correction moves a mole fraction by at most κ times the two residuals, a quarter of the tier at κ of 100.</summary>
    [Fact]
    public void TheCorrectedAndUncorrectedDeviationsAgreeWhereKappaIsSmall()
    {
        var family = FixtureBatches.EquilibriumFamily(EngineFixture.Shared.Database, "lox-rp1_of2.6_pc10MPa");
        var run = TwoRuns(family);
        var bound = GpuCpuTolerances.Entries["balanceResidual"].Relative;
        var floor = GpuCpuTolerances.MoleFractionFloor(EngineFixture.Shared.Tolerances);
        var quarterOfTier = GpuCpuTolerances.Entries["moleFraction"].Relative / 4.0;
        Assert.True(SmallKappa * 2.0 * bound <= quarterOfTier);
        var checkedSpecies = 0;
        var speciesCount = family.Table.SpeciesCount;
        for (var k = 0; k < family.Batch.Count; k++)
        {
            var (cpuResiduals, otherResiduals) = Residuals(run, k);
            var cpuCorrection = run.Sensitivities.Correction(k, cpuResiduals);
            var otherCorrection = run.Sensitivities.Correction(k, otherResiduals);
            for (var j = 0; j < speciesCount; j++)
            {
                var kappa = run.Sensitivities.Kappa(k, j);
                var x = FractionOf(run.Cpu.Moles, k, j, speciesCount);
                var y = FractionOf(run.Other.Moles, k, j, speciesCount);
                if (kappa > SmallKappa || x < floor || double.IsNaN(cpuCorrection[j]))
                {
                    continue;
                }

                var uncorrectedDeviation = Math.Abs(x - y) / Math.Max(x, y);
                var xc = x * Math.Exp(-cpuCorrection[j]);
                var yc = y * Math.Exp(-otherCorrection[j]);
                var correctedDeviation = Math.Abs(xc - yc) / Math.Max(xc, yc);
                var allowed = kappa * (cpuResiduals.Max(Math.Abs) + otherResiduals.Max(Math.Abs)) + 4.0 * double.Epsilon + 1e-15;
                Assert.True(Math.Abs(correctedDeviation - uncorrectedDeviation) <= allowed,
                            $"{family.Cases[k].Name} x({family.Table.Species[j]}): κ {kappa:G3}, uncorrected {uncorrectedDeviation:E2}, corrected {correctedDeviation:E2}");
                Assert.True(Math.Abs(correctedDeviation - uncorrectedDeviation) <= SmallKappa * 2.0 * bound + 1e-15);
                checkedSpecies++;
            }
        }

        Assert.True(checkedSpecies > 100, $"only {checkedSpecies} species compared");
    }

    /// <summary>The guard keeps the smooth remnant and drops the species of a case at a kink of the solution.</summary>
    [Fact]
    public void TheGuardKeepsTheRemnantAndDropsTheSpeciesAtAKink()
    {
        var database = EngineFixture.Shared.Database;
        var smooth = FixtureBatches.NamedEquilibriumFamily(database, "three-element-example1");
        var kinked = FixtureBatches.NamedEquilibriumFamily(database, "threshold-flip-naclo4");
        var bound = GpuCpuTolerances.Entries["sensitivityDisagreement"].Relative;
        var smoothSensitivities = Sensitivities(smooth);
        var kinkedSensitivities = Sensitivities(kinked);
        var remnant = smooth.Table.Species.ToList().IndexOf("H2");
        var liquid = kinked.Table.Species.ToList().IndexOf("NaCL(L)");
        Assert.True(remnant >= 0 && liquid >= 0);
        for (var k = 0; k < smooth.Batch.Count; k++)
        {
            Assert.True(smoothSensitivities.Disagreement(k, remnant) <= bound, $"{smooth.Cases[k].Name}: disagreement {smoothSensitivities.Disagreement(k, remnant):E2}");
            Assert.True(smoothSensitivities.Kappa(k, remnant) > 1e5);
            Assert.False(double.IsNaN(smoothSensitivities.Correction(k, new double[smooth.Table.ElementCount])[remnant]));
        }

        for (var k = 0; k < kinked.Batch.Count; k++)
        {
            Assert.False(kinkedSensitivities.Disagreement(k, liquid) <= bound, $"{kinked.Cases[k].Name}: disagreement {kinkedSensitivities.Disagreement(k, liquid):E2}");
            Assert.True(double.IsNaN(kinkedSensitivities.Correction(k, new double[kinked.Table.ElementCount])[liquid]));
        }
    }

    /// <summary>A residual injected above the bound is reported, on the CPU accelerator: the check is red when it should be, and silent on the clean result.</summary>
    [Fact]
    public void AResidualInjectedAboveTheBoundIsReported()
    {
        var (batch, table, cases) = FixtureBatches.EquilibriumFamily(EngineFixture.Shared.Database, "lox-rp1_of2.6_pc10MPa");
        using var tables = EngineFixture.Shared.Cpu.Upload(table);
        var result = EngineFixture.Shared.Cpu.Run(tables, batch);
        var balance = new ElementBalance(table, batch.ElementMoles);
        var comparison = NewComparison();
        var clean = balance.Residuals(0, result.Moles, 0);
        Assert.Empty(balance.Exceeding(clean, "the CPU accelerator", cases[0].Name));
        Assert.Empty(comparison.Balance(balance, clean, clean, cases[0].Name));

        var injected = (double[])result.Moles.Clone();
        var largest = Enumerable.Range(0, table.SpeciesCount).MaxBy(j => injected[j]);
        injected[largest] *= 1.0 + 1e-12;
        var residuals = balance.Residuals(0, injected, 0);
        var reported = balance.Exceeding(residuals, "the CPU accelerator", cases[0].Name).ToList();
        Assert.NotEmpty(reported);
        Assert.NotEmpty(comparison.Balance(balance, clean, residuals, cases[0].Name));
        Assert.NotEmpty(comparison.Balance(balance, residuals, clean, cases[0].Name));
        Assert.All(reported, message => Assert.Contains("closed only to", message, StringComparison.Ordinal));
    }

    /// <summary>The rocket comparison checks the balance of every station on both sides: two CPU runs agree and close the balance, and a station of one side scaled out of balance is the only mismatch.</summary>
    [Fact]
    public void TheRocketComparisonChecksTheBalanceOfEveryStation()
    {
        var family = FixtureBatches.RocketFamilies(EngineFixture.Shared.Database)[^1];
        var batch = family.Batch();
        using var tables = EngineFixture.Shared.Cpu.Upload(family.Table, family.Transport);
        var cpu = EngineFixture.Shared.Cpu.Run(tables, batch);
        var other = EngineFixture.Shared.Cpu.Run(tables, batch);
        var comparison = NewComparison();
        Assert.Empty(comparison.Rocket(cpu, other, family, batch));
        Assert.Contains("balanceResidual", comparison.Worst(), StringComparison.Ordinal);

        var speciesCount = family.Table.SpeciesCount;
        for (var j = 0; j < speciesCount; j++)
        {
            other.Moles[j] *= 1.0 + 1e-12;
        }

        var mismatches = comparison.Rocket(cpu, other, family, batch);
        Assert.NotEmpty(mismatches);
        Assert.All(mismatches, message => Assert.Contains("element balance of", message, StringComparison.Ordinal));
        Assert.Contains("station 0", mismatches[0], StringComparison.Ordinal);
    }

    private static BalanceSensitivities Sensitivities((EquilibriumBatch Batch, SpeciesTable Table, IReadOnlyList<CeaCase> Cases) family)
    {
        using var tables = EngineFixture.Shared.Cpu.Upload(family.Table);
        return BalanceSensitivities.Measure(EngineFixture.Shared.Cpu, tables, family.Batch, family.Table);
    }

    private static double FractionOf(double[] moles, int caseIndex, int species, int speciesCount)
    {
        var total = 0.0;
        for (var j = 0; j < speciesCount; j++)
        {
            total += moles[caseIndex * speciesCount + j];
        }

        return moles[caseIndex * speciesCount + species] / total;
    }

    private static MoleStation Station(TwoCpuRuns run, int caseIndex, string label) =>
        new(run.Cpu.Moles, run.Other.Moles, caseIndex, run.Cpu.Iterations[caseIndex] == run.Other.Iterations[caseIndex], label);

    private static (double[] Cpu, double[] Other) Residuals(TwoCpuRuns run, int caseIndex) =>
        (run.Balance.Residuals(caseIndex, run.Cpu.Moles, caseIndex), run.Balance.Residuals(caseIndex, run.Other.Moles, caseIndex));

    /// <summary>
    /// The family on the CPU accelerator, and again over a batch whose element moles moved by <see cref="PerturbationUlps"/> ULP in
    /// alternating directions: the second run is the exact solution of a slightly different balance, as another accelerator's is.
    /// </summary>
    internal static TwoCpuRuns TwoRuns((EquilibriumBatch Batch, SpeciesTable Table, IReadOnlyList<CeaCase> Cases) family)
    {
        var (batch, table, _) = family;
        var moved = FixtureBatches.CopyOf(batch);
        for (var n = 0; n < moved.ElementMoles.Length; n++)
        {
            for (var u = 0; u < PerturbationUlps; u++)
            {
                moved.ElementMoles[n] = n % 2 == 0 ? Math.BitIncrement(moved.ElementMoles[n]) : Math.BitDecrement(moved.ElementMoles[n]);
            }
        }

        using var tables = EngineFixture.Shared.Cpu.Upload(table);
        var cpu = EngineFixture.Shared.Cpu.Run(tables, batch);
        var other = EngineFixture.Shared.Cpu.Run(tables, moved);
        var sensitivities = BalanceSensitivities.Measure(EngineFixture.Shared.Cpu, tables, batch, table);
        return new TwoCpuRuns(cpu, other, new ElementBalance(table, batch.ElementMoles), sensitivities);
    }

    /// <summary>Two CPU results of one family, the second over moved element moles, with the balance and the sensitivities of the first batch.</summary>
    internal sealed record TwoCpuRuns(EquilibriumBatchResult Cpu, EquilibriumBatchResult Other, ElementBalance Balance, BalanceSensitivities Sensitivities);
}

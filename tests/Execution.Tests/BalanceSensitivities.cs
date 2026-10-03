using APThermo.Thermo;

namespace APThermo.Execution.Tests;

/// <summary>
/// <c>D_ij = ∂ ln x_j / ∂ ln b_i</c> of every case of an equilibrium batch, by central differences (relative step
/// <see cref="GpuCpuTolerances.SensitivityStep"/>) through the CPU accelerator over the same batch with one element's moles scaled by
/// (1 ± h), and the guard that says where those derivatives may be used: a case in which a condensed species appears or vanishes
/// between <c>b(1 − h)</c>, <c>b</c> and <c>b(1 + h)</c> has no usable derivative, and a species whose two one-sided differences
/// disagree by more than <see cref="GpuCpuTolerances.Entries"/>["sensitivityDisagreement"] has none either. The correction
/// <c>Σ_i D_ij ρ_i</c> is what a balance residual <c>ρ</c> moves <c>ln x_j</c> by (Execution.Tests BOOT.md).
/// </summary>
internal sealed class BalanceSensitivities
{
    private readonly int _elementCount;
    private readonly int _speciesCount;
    private readonly double[][] _derivative;
    private readonly double[] _disagreement;
    private readonly bool[] _phaseSetHolds;

    private BalanceSensitivities(int elementCount, int speciesCount, double[][] derivative, double[] disagreement, bool[] phaseSetHolds)
    {
        _elementCount = elementCount;
        _speciesCount = speciesCount;
        _derivative = derivative;
        _disagreement = disagreement;
        _phaseSetHolds = phaseSetHolds;
    }

    /// <summary>The cases of the batch without a usable derivative at all: a condensed species appeared or vanished within the step, or a case failed.</summary>
    public int StationsWithoutDerivative => _phaseSetHolds.Count(holds => !holds);

    /// <summary>The cases of the batch.</summary>
    public int Count => _phaseSetHolds.Length;

    /// <summary>
    /// The sensitivities of every case of the batch to its element moles, measured on the CPU accelerator
    /// (<paramref name="tables"/> uploaded to <paramref name="cpu"/>); the batch is not changed.
    /// </summary>
    public static BalanceSensitivities Measure(Engine cpu, UploadedTables tables, EquilibriumBatch batch, SpeciesTable table)
    {
        ArgumentNullException.ThrowIfNull(cpu);
        ArgumentNullException.ThrowIfNull(tables);
        ArgumentNullException.ThrowIfNull(batch);
        ArgumentNullException.ThrowIfNull(table);
        var elementCount = table.ElementCount;
        var speciesCount = table.SpeciesCount;
        var centre = cpu.Run(tables, batch);
        var centreLog = Enumerable.Range(0, batch.Count).Select(k => LogFractions(centre, k, speciesCount)).ToArray();
        var derivative = Enumerable.Range(0, batch.Count).Select(_ => new double[elementCount * speciesCount]).ToArray();
        var gap = Enumerable.Range(0, batch.Count).Select(_ => new double[elementCount * speciesCount]).ToArray();
        var phaseSetHolds = Enumerable.Repeat(true, batch.Count).ToArray();
        var step = GpuCpuTolerances.SensitivityStep;
        for (var i = 0; i < elementCount; i++)
        {
            var plus = cpu.Run(tables, Scaled(batch, i, 1.0 + step));
            var minus = cpu.Run(tables, Scaled(batch, i, 1.0 - step));
            for (var k = 0; k < batch.Count; k++)
            {
                phaseSetHolds[k] &= PhaseSetHolds(centre, plus, minus, k, table);
                var plusLog = LogFractions(plus, k, speciesCount);
                var minusLog = LogFractions(minus, k, speciesCount);
                for (var j = 0; j < speciesCount; j++)
                {
                    var forward = (plusLog[j] - centreLog[k][j]) / step;
                    var backward = (centreLog[k][j] - minusLog[j]) / step;
                    derivative[k][i * speciesCount + j] = (plusLog[j] - minusLog[j]) / (2.0 * step);
                    gap[k][i * speciesCount + j] = Math.Abs(forward - backward);
                }
            }
        }

        return new BalanceSensitivities(elementCount, speciesCount, derivative, Disagreements(derivative, gap, elementCount, speciesCount), phaseSetHolds);
    }

    /// <summary>
    /// The measure of how little the one-sided differences agree for a species of a case: the largest over the elements of
    /// |D⁺ − D⁻|, relative to the larger of 1 and the species' largest |D|; NaN where the species is absent from one of the three
    /// solves. The noise of the centre solve (a remnant's logarithm moves by κ·1e-14 under the balance residual) enters both
    /// one-sided differences with opposite signs as κ·1e-14/h, which relative to κ is the measure's floor.
    /// </summary>
    public double Disagreement(int caseIndex, int species) => _disagreement[caseIndex * _speciesCount + species];

    /// <summary>κ_j = Σ_i |∂ ln x_j / ∂ ln b_i| of a species in a case; NaN where the species is absent from one of the solves.</summary>
    public double Kappa(int caseIndex, int species)
    {
        var kappa = 0.0;
        for (var i = 0; i < _elementCount; i++)
        {
            kappa += Math.Abs(_derivative[caseIndex][i * _speciesCount + species]);
        }

        return kappa;
    }

    /// <summary>
    /// <c>Σ_i D_ij ρ_i</c> for every species of a case, the amount by which the balance residuals move <c>ln x_j</c>; NaN for a species
    /// without a usable derivative (the case's condensed set changed within the step, or the one-sided differences disagree beyond the
    /// bound), which is then compared uncorrected.
    /// </summary>
    public double[] Correction(int caseIndex, double[] residuals)
    {
        ArgumentNullException.ThrowIfNull(residuals);
        var bound = GpuCpuTolerances.Entries["sensitivityDisagreement"].Relative;
        var correction = new double[_speciesCount];
        for (var j = 0; j < _speciesCount; j++)
        {
            if (!_phaseSetHolds[caseIndex] || !(Disagreement(caseIndex, j) <= bound))
            {
                correction[j] = double.NaN;
                continue;
            }

            for (var i = 0; i < residuals.Length; i++)
            {
                correction[j] += _derivative[caseIndex][i * _speciesCount + j] * residuals[i];
            }
        }

        return correction;
    }

    private static double[] Disagreements(double[][] derivative, double[][] gap, int elementCount, int speciesCount)
    {
        var result = new double[derivative.Length * speciesCount];
        for (var k = 0; k < derivative.Length; k++)
        {
            for (var j = 0; j < speciesCount; j++)
            {
                var largestGap = 0.0;
                var largestDerivative = 1.0;
                for (var i = 0; i < elementCount; i++)
                {
                    largestGap = Math.Max(largestGap, gap[k][i * speciesCount + j]);
                    largestDerivative = Math.Max(largestDerivative, Math.Abs(derivative[k][i * speciesCount + j]));
                }

                result[k * speciesCount + j] = largestGap / largestDerivative;
            }
        }

        return result;
    }

    /// <summary>The batch with the moles of one element of every case multiplied by a factor.</summary>
    private static EquilibriumBatch Scaled(EquilibriumBatch batch, int element, double factor)
    {
        var scaled = FixtureBatches.CopyOf(batch);
        for (var k = 0; k < batch.Count; k++)
        {
            scaled.ElementMoles[k * batch.ElementCount + element] *= factor;
        }

        return scaled;
    }

    /// <summary>ln of the mole fractions of a case, NaN for a species whose amount is not positive.</summary>
    private static double[] LogFractions(EquilibriumBatchResult result, int caseIndex, int speciesCount)
    {
        var offset = (long)caseIndex * speciesCount;
        var total = 0.0;
        for (var j = 0; j < speciesCount; j++)
        {
            total += result.Moles[offset + j];
        }

        var logs = new double[speciesCount];
        for (var j = 0; j < speciesCount; j++)
        {
            var amount = result.Moles[offset + j];
            logs[j] = amount > 0.0 ? Math.Log(amount / total) : double.NaN;
        }

        return logs;
    }

    /// <summary>Whether the case succeeded in the three solves and every condensed species is present in all three or in none.</summary>
    private static bool PhaseSetHolds(EquilibriumBatchResult centre, EquilibriumBatchResult plus, EquilibriumBatchResult minus, int caseIndex, SpeciesTable table)
    {
        if (centre.Status[caseIndex] != CaseStatus.Ok || plus.Status[caseIndex] != CaseStatus.Ok || minus.Status[caseIndex] != CaseStatus.Ok)
        {
            return false;
        }

        var offset = (long)caseIndex * table.SpeciesCount;
        for (var j = table.GasCount; j < table.SpeciesCount; j++)
        {
            var present = centre.Moles[offset + j] > 0.0;
            if (present != (plus.Moles[offset + j] > 0.0) || present != (minus.Moles[offset + j] > 0.0))
            {
                return false;
            }
        }

        return true;
    }
}

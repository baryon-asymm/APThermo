using System.Reflection;
using APThermo.Equilibrium;
using APThermo.Fixtures;
using APThermo.Harness;
using APThermo.Thermo;

namespace APThermo.Execution.Tests;

/// <summary>A station that both accelerators solved: the moles of the two sides, the station's row, whether they stopped after the same number of Newton steps, and its label.</summary>
internal sealed record MoleStation(double[] CpuMoles, double[] GpuMoles, long Index, bool SameSteps, string Label);

/// <summary>
/// One CUDA-against-CPU-accelerator comparison: the worst deviation seen so far per field, and how many stations compared so far
/// stopped after a different number of Newton steps on the two accelerators, both accumulated across however many calls the
/// caller makes against the tolerance table this was built from. <see cref="CudaTests"/> owns one per test.
/// </summary>
internal sealed class GpuCpuComparison(ToleranceTable tolerances)
{
    private readonly Dictionary<string, double> _worst = new(StringComparer.Ordinal);

    public int DifferentSteps { get; private set; }

    /// <summary>
    /// True for the cases of a family that runs the 0.2.2 temperature bracket: their <c>Iterations</c> is the sum of the Newton steps of
    /// every attempt and every tp probe, so two accelerators that took the same decisions up to a flip in one probe differ in the
    /// total without differing in the final solve. <see cref="Equilibrium"/> then does not count the difference toward the step share
    /// and compares every mole fraction at the first tier, the one for equal steps, which is stricter than the second (Execution.Tests
    /// BOOT.md, 2026-10-04); the total's difference is recorded as <c>bracketedIterationDifference</c>.
    /// </summary>
    public bool IterationsSumAttempts { get; init; }

    /// <summary>Species compared with the balance-remnant correction applied, over every call of <see cref="Moles"/>.</summary>
    public int CorrectedSpecies { get; private set; }

    /// <summary>Species compared above the floor without a correction, because the call carried none for them (the guard's drops included).</summary>
    public int UncorrectedSpecies { get; private set; }

    /// <summary>
    /// Every rocket station of one batch, cpu against gpu: station status, state, figures, mole fractions, and the closure of the
    /// element balance of <paramref name="batch"/>, which the two results were computed from.
    /// </summary>
    public List<string> Rocket(RocketBatchResult cpu, RocketBatchResult gpu, RocketFamily family, RocketBatch batch)
    {
        var balance = new ElementBalance(family.Table, batch.ElementMoles);
        var mismatches = new List<string>();
        var stationCount = cpu.StationCount;
        for (var k = 0; k < cpu.Count; k++)
        {
            var label = k < family.Members.Count && cpu.Count == family.Members.Count ? family.Members[k] : $"case {k}";
            if (cpu.Status[k] != gpu.Status[k])
            {
                mismatches.Add($"{label}: status cpu {cpu.Status[k]}, cuda {gpu.Status[k]}");
                continue;
            }

            for (var s = 0; s < stationCount; s++)
            {
                var index = k * stationCount + s;
                if (cpu.StationStatus[index] != gpu.StationStatus[index])
                {
                    mismatches.Add($"{label} station {s}: status cpu {cpu.StationStatus[index]}, cuda {gpu.StationStatus[index]}");
                    continue;
                }

                if (cpu.StationStatus[index] != CaseStatus.Ok)
                {
                    continue;
                }

                var sameSteps = cpu.Iterations[index] == gpu.Iterations[index];
                CountSteps(sameSteps);

                var where = $"{label} station {s} ({cpu.Iterations[index]}/{gpu.Iterations[index]} steps)";
                mismatches.AddRange(GpuCpuTolerances.Compare(cpu.Stations[index], gpu.Stations[index], where, Record));
                mismatches.AddRange(GpuCpuTolerances.Compare(cpu.Figures[index], gpu.Figures[index], where, Record));
                mismatches.AddRange(Balance(balance, balance.Residuals(k, cpu.Moles, index), balance.Residuals(k, gpu.Moles, index), where));
                mismatches.AddRange(Moles(new MoleStation(cpu.Moles, gpu.Moles, index, sameSteps, where), family.Table));
            }
        }

        return mismatches;
    }

    /// <summary>
    /// Every case of an equilibrium batch, cpu against gpu: the status, then by the status the CPU accelerator ended in. <c>Ok</c>: the
    /// state within the table, the closure of the element balance and the mole fractions with the balance-remnant correction
    /// (<paramref name="sensitivities"/>). <c>NoGasPhase</c> (0.2.2): <see cref="GaslessCase"/>. Any other status is compared by status
    /// alone, as a failed rocket station is.
    /// </summary>
    public List<string> Equilibrium(EquilibriumBatchResult cpu, EquilibriumBatchResult gpu, EquilibriumBatch batch, SpeciesTable table, IReadOnlyList<string> labels,
                                    BalanceSensitivities sensitivities)
    {
        ArgumentNullException.ThrowIfNull(cpu);
        ArgumentNullException.ThrowIfNull(gpu);
        ArgumentNullException.ThrowIfNull(batch);
        ArgumentNullException.ThrowIfNull(table);
        ArgumentNullException.ThrowIfNull(labels);
        ArgumentNullException.ThrowIfNull(sensitivities);
        var balance = new ElementBalance(table, batch.ElementMoles);
        var mismatches = new List<string>();
        for (var k = 0; k < batch.Count; k++)
        {
            if (cpu.Status[k] != gpu.Status[k])
            {
                mismatches.Add($"{labels[k]}: status cpu {cpu.Status[k]}, cuda {gpu.Status[k]}");
                continue;
            }

            var sameSteps = cpu.Iterations[k] == gpu.Iterations[k];
            if (IterationsSumAttempts)
            {
                Record("bracketedIterationDifference", Math.Abs(cpu.Iterations[k] - gpu.Iterations[k]));
            }
            else
            {
                CountSteps(sameSteps);
            }

            var station = new MoleStation(cpu.Moles, gpu.Moles, k, sameSteps || IterationsSumAttempts, labels[k]);
            if (cpu.Status[k] == CaseStatus.Ok)
            {
                mismatches.AddRange(OkCase(cpu, gpu, balance, station, sensitivities, table));
            }
            else if (cpu.Status[k] == CaseStatus.NoGasPhase)
            {
                mismatches.AddRange(GaslessCase(cpu, gpu, batch, balance, station, table));
            }
        }

        return mismatches;
    }

    private IEnumerable<string> OkCase(EquilibriumBatchResult cpu, EquilibriumBatchResult gpu, ElementBalance balance, MoleStation station,
                                        BalanceSensitivities sensitivities, SpeciesTable table)
    {
        var k = (int)station.Index;
        var cpuResiduals = balance.Residuals(k, cpu.Moles, k);
        var gpuResiduals = balance.Residuals(k, gpu.Moles, k);
        return GpuCpuTolerances.Compare(cpu.State[k], gpu.State[k], station.Label, Record)
            .Concat(Balance(balance, cpuResiduals, gpuResiduals, station.Label))
            .Concat(Moles(station, table, sensitivities.Correction(k, cpuResiduals), sensitivities.Correction(k, gpuResiduals)));
    }

    /// <summary>
    /// A case both accelerators ended <c>NoGasPhase</c> (the 0.2.2 gasless verdict): the state carries the pressure of the case, which
    /// must be the batch's own bit for bit on both sides, and the temperature, which for a tp case is the batch's own bit for bit and
    /// for an hp or sp case, a result of the bracket, agrees within the temperature tier (its deviation is recorded); every other
    /// field of the state is zero on both. The moles are the condensed minimum with every gas zero, compared at the tiers of the table
    /// without the balance-remnant correction (a condensed amount is set by the balance, not by a small difference of large amounts),
    /// both as fractions and as amounts (a fraction of one condensed species is always one), and close the element balance as an <c>Ok</c> station does. The multipliers are not part of a batch result.
    /// </summary>
    private List<string> GaslessCase(EquilibriumBatchResult cpu, EquilibriumBatchResult gpu, EquilibriumBatch batch, ElementBalance balance,
                                            MoleStation station, SpeciesTable table)
    {
        var k = (int)station.Index;
        var cpuState = cpu.State[k];
        var gpuState = gpu.State[k];
        var mismatches = new List<string>();
        foreach (var (side, state) in new[] { ("cpu", cpuState), ("cuda", gpuState) })
        {
            if (!Bits.Same(state.Pressure, batch.Pressure[k]))
            {
                mismatches.Add($"{station.Label}: pressure on {side} {state.Pressure:R}, the case's {batch.Pressure[k]:R}");
            }

            if (batch.Kind[k] == ProblemKind.AssignedTemperaturePressure && !Bits.Same(state.Temperature, batch.Temperature[k]))
            {
                mismatches.Add($"{station.Label}: temperature on {side} {state.Temperature:R}, the case's {batch.Temperature[k]:R}");
            }

            mismatches.AddRange(NonZeroFields(state, $"{station.Label} on {side}"));
        }

        Record("noGasPhaseTemperature", Math.Abs(cpuState.Temperature - gpuState.Temperature) / cpuState.Temperature);
        if (!GpuCpuTolerances.Matches(GpuCpuTolerances.Entries["temperature"].Relative, cpuState.Temperature, gpuState.Temperature))
        {
            mismatches.Add($"{station.Label}: temperature cpu {cpuState.Temperature:R}, cuda {gpuState.Temperature:R}");
        }

        var floor = GpuCpuTolerances.MoleFractionFloor(tolerances) * Enumerable.Range(0, table.SpeciesCount).Sum(j => cpu.Moles[k * table.SpeciesCount + j]);
        for (var j = 0; j < table.SpeciesCount; j++)
        {
            var (x, y) = (cpu.Moles[k * table.SpeciesCount + j], gpu.Moles[k * table.SpeciesCount + j]);
            if (j < table.GasCount ? x != 0.0 || y != 0.0 : (x >= floor || y >= floor) && !GpuCpuTolerances.Matches(GpuCpuTolerances.Entries["condensedMoleFraction"].Relative, x, y))
            {
                mismatches.Add($"{station.Label}: moles of {table.Species[j]}: cpu {x:R}, cuda {y:R}");
            }
        }

        mismatches.AddRange(Balance(balance, balance.Residuals(k, cpu.Moles, k), balance.Residuals(k, gpu.Moles, k), station.Label));
        mismatches.AddRange(Moles(station, table));
        return mismatches;
    }

    /// <summary>The fields of a <c>NoGasPhase</c> state other than the temperature and the pressure that are not zero, as messages.</summary>
    private static IEnumerable<string> NonZeroFields(MixtureState state, string label) =>
        typeof(MixtureState).GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(f => f.Name is not nameof(MixtureState.Temperature) and not nameof(MixtureState.Pressure))
            .Select(f => (f.Name, Value: (double)f.GetValue(state)!))
            .Where(f => f.Value != 0.0)
            .Select(f => $"{label}: {f.Name} {f.Value:R}, zero for a NoGasPhase state");

    /// <summary>
    /// The closure of the element balance of one station on both sides: records the worst residual and reports every element above
    /// the bound of the tolerance table (<c>balanceResidual</c>).
    /// </summary>
    public IEnumerable<string> Balance(ElementBalance balance, double[] cpuResiduals, double[] gpuResiduals, string label)
    {
        ArgumentNullException.ThrowIfNull(balance);
        ArgumentNullException.ThrowIfNull(cpuResiduals);
        ArgumentNullException.ThrowIfNull(gpuResiduals);
        Record("balanceResidual", Math.Max(cpuResiduals.Max(Math.Abs), gpuResiduals.Max(Math.Abs)));
        return balance.Exceeding(cpuResiduals, "the CPU accelerator", label).Concat(balance.Exceeding(gpuResiduals, "CUDA", label));
    }

    /// <summary>
    /// Mole fractions of one case or station, relative to the total moles, within the tier of the mole-fraction tolerance above the
    /// floor; a species at or after <see cref="SpeciesTable.GasCount"/> is condensed (the table lists gaseous species first) and is
    /// held to the condensed tier whatever the Newton counts. With the corrections of the balance-remnant rule
    /// (<see cref="BalanceSensitivities.Correction"/>, one per side) the quantity compared is <c>x_j exp(−Σ_i D_ij ρ_i)</c>, that is
    /// <c>ln x_j − Σ_i D_ij ρ_i</c>; a species whose correction is NaN is compared as it is.
    /// </summary>
    public IEnumerable<string> Moles(MoleStation station, SpeciesTable table, double[]? cpuCorrection = null, double[]? gpuCorrection = null)
    {
        ArgumentNullException.ThrowIfNull(station);
        ArgumentNullException.ThrowIfNull(table);
        var speciesCount = table.SpeciesCount;
        var offset = station.Index * speciesCount;
        var cpuTotal = 0.0;
        var gpuTotal = 0.0;
        for (var j = 0; j < speciesCount; j++)
        {
            cpuTotal += station.CpuMoles[offset + j];
            gpuTotal += station.GpuMoles[offset + j];
        }

        var floor = GpuCpuTolerances.MoleFractionFloor(tolerances);
        for (var j = 0; j < speciesCount; j++)
        {
            var x = station.CpuMoles[offset + j] / cpuTotal;
            var y = station.GpuMoles[offset + j] / gpuTotal;
            if (x < floor && y < floor)
            {
                continue;
            }

            if (cpuCorrection is not null && gpuCorrection is not null && !double.IsNaN(cpuCorrection[j]) && !double.IsNaN(gpuCorrection[j]))
            {
                x *= Math.Exp(-cpuCorrection[j]);
                y *= Math.Exp(-gpuCorrection[j]);
                CorrectedSpecies++;
            }
            else
            {
                UncorrectedSpecies++;
            }

            var condensed = j >= table.GasCount;
            var relative = GpuCpuTolerances.MoleFractionRelative(tolerances, station.SameSteps, condensed);
            Record(condensed ? "condensedMoleFraction" : station.SameSteps ? "moleFraction" : "moleFractionAfterDifferentSteps", Math.Abs(x - y) / Math.Max(x, y));
            if (!GpuCpuTolerances.Matches(relative, x, y))
            {
                yield return $"{station.Label} x({table.Species[j]}): cpu {x:R}, cuda {y:R}";
            }
        }
    }

    /// <summary>Records one field's deviation if it is the worst seen so far for that field.</summary>
    public void Record(string field, double deviation)
    {
        if (!_worst.TryGetValue(field, out var current) || deviation > current)
        {
            _worst[field] = deviation;
        }
    }

    /// <summary>Counts a station or case whose two accelerators stopped after a different number of Newton steps.</summary>
    public void CountSteps(bool sameSteps)
    {
        if (!sameSteps)
        {
            DifferentSteps++;
        }
    }

    /// <summary>The worst deviation recorded so far for one field, zero when none was recorded.</summary>
    public double WorstOf(string field) => _worst.GetValueOrDefault(field);

    /// <summary>The worst deviation per field, largest first, for a failure message.</summary>
    public string Worst() => string.Join(", ", _worst.OrderByDescending(kv => kv.Value).Take(8).Select(kv => $"{kv.Key} {kv.Value:E1}"));
}

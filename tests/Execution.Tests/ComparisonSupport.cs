using APThermo.Equilibrium;
using APThermo.Thermo;

namespace APThermo.Execution.Tests;

/// <summary>
/// What <see cref="GpuCpuComparison"/> may ask about one equilibrium family beyond the two results (Execution.Tests BOOT.md, "What a
/// difference between the accelerators is not", 2026-10-04). Every answer is measured on the CPU accelerator, on demand and per case, and
/// only for a case whose plain comparison has already failed or whose field is a sum that cancels:
/// <list type="bullet">
/// <item><see cref="EnthalpyBound"/>: the rounding bound of the mixture enthalpy, for a state whose enthalpy is zero up to cancellation;</item>
/// <item><see cref="DataEffect"/>: what the other accelerator's own G/RT does to the mole fractions of a tp case, to first order, so that
/// a difference of species data is not read as a difference of solves;</item>
/// <item><see cref="NoiseResponse"/>: how far the CPU accelerator's own composition moves under rounding-level noise of the element moles,
/// the floor below which a comparison of two accelerators measures the arithmetic of the solve and not the accelerators.</item>
/// </list>
/// </summary>
internal sealed class ComparisonSupport(Engine cpu, UploadedTables cpuTables, SpeciesTable table, EquilibriumBatch batch, EquilibriumBatchResult cpuResult,
                                       SpeciesFunctionSources functions)
{
    /// <summary>The replicates of <see cref="NoiseResponse"/>: how many ULP each element's moles move by, in alternating directions, and which element starts upward.</summary>
    private static readonly int[] ReplicateUlps = [1, 2, 3, 4, 6, 8, 12, 16];

    /// <summary>The largest move of any species' G/RT in the solves that measure <see cref="DataEffect"/>: 1e-7, whose curvature a central difference cancels.</summary>
    private const double LargestMove = 1e-7;

    /// <summary>Half of one unit in the last place of 1: the unit roundoff of the double-precision sums.</summary>
    private static readonly double UnitRoundoff = (Math.BitIncrement(1.0) - 1.0) / 2.0;

    private readonly Dictionary<int, double> _enthalpyBounds = [];
    private readonly Dictionary<int, double[]?> _dataEffect = [];
    private readonly Dictionary<int, double[]?> _noise = [];
    private readonly Dictionary<int, (int Min, int Max)> _steps = [];

    /// <summary>Cases whose enthalpy the rounding bound decided, whose composition the same-data reference decided, whose composition the noise response decided.</summary>
    public List<string> Decisions { get; } = [];

    /// <summary>
    /// The rounding bound of the mixture enthalpy of case <paramref name="k"/>, J/kg: two accelerators each sum at most
    /// <c>8 + SpeciesCount</c> terms (the eight terms of a species' H/RT polynomial, then the species), each term bounded by the species'
    /// <c>|H/RT| + Cp/R</c> (every term of the polynomial is a term of Cp/R, or the constant b1/T of H/RT itself), so that their results differ by at
    /// most <c>2 (8 + SpeciesCount) u R T Σ n_j (|H_j/RT| + Cp_j/R)</c> with <c>u</c> the unit roundoff. Relative to a state whose enthalpy
    /// does not cancel this is far below the tier (1e-9 of 1e6 J/kg is 1e-3 J/kg, the bound about 1e-8 J/kg), so it decides only where
    /// the enthalpy is zero up to cancellation, as the elements at their reference temperature are.
    /// </summary>
    public double EnthalpyBound(int k)
    {
        if (_enthalpyBounds.TryGetValue(k, out var known))
        {
            return known;
        }

        var s = table.SpeciesCount;
        var temperature = cpuResult.State[k].Temperature;
        var species = functions.Cpu(AtTemperature(temperature));
        var scale = 0.0;
        for (var j = 0; j < s; j++)
        {
            scale += cpuResult.Moles[(long)k * s + j] * (Math.Abs(species.HOverRT[j]) + species.CpOverR[j]);
        }

        scale *= PhysicalConstants.R * temperature;
        return _enthalpyBounds[k] = 2.0 * (8 + s) * UnitRoundoff * scale;
    }

    /// <summary>
    /// What the two accelerators' own species data do to the mole fractions of tp case <paramref name="k"/>: for every species
    /// <c>ln x_other − ln x_cpu</c> to first order, the directional derivative of the CPU accelerator's <c>ln x</c> along the measured difference
    /// of G/RT at the case's temperature, species by species (a tp solve is a function of G/RT alone). The two accelerators' G/RT differ where a
    /// polynomial's terms cancel by decades (the liquid water fit, 5e-11 measured: <c>functions</c> in <see cref="GpuCpuTolerances.Entries"/>), and
    /// a state near a phase boundary carries that into its mole fractions at the species' own sensitivity: 4e-11 into 1.3e-10 on x(H2O) at 300 K.
    /// The derivative is a central difference of two CPU solves of the case, the G/RT of every species moved by <c>±c</c> times its measured
    /// difference through the constant b2 of its entropy fit (<c>c</c> so that the largest move is <see cref="LargestMove"/>, far above the unit in
    /// the last place of b2, which a move as small as the difference itself could not be represented in). Null for an hp or sp case and for a case
    /// whose condensed set differs between the three solves; the table is restored before this returns.
    /// </summary>
    public double[]? DataEffect(int k)
    {
        if (_dataEffect.TryGetValue(k, out var known))
        {
            return known;
        }

        if (batch.Kind[k] != ProblemKind.AssignedTemperaturePressure)
        {
            return _dataEffect[k] = null;
        }

        var s = table.SpeciesCount;
        var request = AtTemperature(batch.Temperature[k]);
        var ours = functions.Cpu(request);
        var theirs = functions.Other(request);
        var difference = new double[s];
        for (var j = 0; j < s; j++)
        {
            difference[j] = theirs.HOverRT[j] - theirs.SOverR[j] - (ours.HOverRT[j] - ours.SOverR[j]);
        }

        var largest = difference.Max(Math.Abs);
        if (largest == 0.0)
        {
            return _dataEffect[k] = new double[s];
        }

        var c = LargestMove / largest;
        var centre = LogFractions(cpu.Run(cpuTables, CaseOf(k)).Moles, s);
        var plus = LogFractions(SolveWithMovedPotentials(k, difference, c), s);
        var minus = LogFractions(SolveWithMovedPotentials(k, difference, -c), s);
        var effect = new double[s];
        for (var j = 0; j < s; j++)
        {
            if (double.IsNaN(centre[j]) != double.IsNaN(plus[j]) || double.IsNaN(centre[j]) != double.IsNaN(minus[j]))
            {
                return _dataEffect[k] = null;
            }

            effect[j] = double.IsNaN(centre[j]) ? 0.0 : (plus[j] - minus[j]) / (2.0 * c);
        }

        return _dataEffect[k] = effect;
    }

    /// <summary>The composition of case <paramref name="k"/> on the CPU accelerator with every species' G/RT moved by <paramref name="factor"/> times its difference.</summary>
    private double[] SolveWithMovedPotentials(int k, double[] difference, double factor)
    {
        var arrays = table.Arrays;
        void Move(double sign)
        {
            for (var j = 0; j < difference.Length; j++)
            {
                for (var i = 0; i < arrays.IntervalCount[j]; i++)
                {
                    // S/R carries b2 as a constant, so G/RT = H/RT - S/R moves by -(the change of b2).
                    arrays.Coefficients[(arrays.IntervalStart[j] + i) * 9 + 8] -= sign * factor * difference[j];
                }
            }
        }

        Move(1.0);
        try
        {
            using var moved = cpu.Upload(table);
            return [.. cpu.Run(moved, CaseOf(k)).Moles];
        }
        finally
        {
            Move(-1.0);
        }
    }

    /// <summary>
    /// For every species of case <paramref name="k"/>, the largest <c>|ln x_r − ln x_0|</c> over the replicates of the CPU accelerator's solve of
    /// the case over element moles moved by 1 to 16 ULP (<see cref="ReplicateUlps"/>, both directions of the alternation): what the solve does
    /// to a composition under noise at the rounding level of its input. NaN for every species when a replicate's condensed set differs from the
    /// case's own, a case this response does not characterise.
    /// </summary>
    public double[]? NoiseResponse(int k)
    {
        if (_noise.TryGetValue(k, out var known))
        {
            return known;
        }

        var s = table.SpeciesCount;
        var centre = cpu.Run(cpuTables, CaseOf(k));
        var baseline = LogFractions(centre.Moles, s);
        var response = new double[s];
        var steps = (Min: centre.Iterations[0], Max: centre.Iterations[0]);
        foreach (var ulps in ReplicateUlps)
        {
            foreach (var phase in new[] { 0, 1 })
            {
                var replicate = cpu.Run(cpuTables, MovedByUlps(k, ulps, phase));
                steps = (Math.Min(steps.Min, replicate.Iterations[0]), Math.Max(steps.Max, replicate.Iterations[0]));
                if (!Accumulate(response, baseline, LogFractions(replicate.Moles, s)))
                {
                    return _noise[k] = null;
                }
            }
        }

        _steps[k] = steps;
        return _noise[k] = response;
    }

    /// <summary>Case <paramref name="k"/> with the moles of every element moved by <paramref name="ulps"/> units in the last place, upward for the elements of one parity and downward for the others.</summary>
    private EquilibriumBatch MovedByUlps(int k, int ulps, int phase)
    {
        var moved = CaseOf(k);
        for (var n = 0; n < moved.ElementMoles.Length; n++)
        {
            for (var u = 0; u < ulps; u++)
            {
                moved.ElementMoles[n] = (n + phase) % 2 == 0 ? Math.BitIncrement(moved.ElementMoles[n]) : Math.BitDecrement(moved.ElementMoles[n]);
            }
        }

        return moved;
    }

    /// <summary>Raises each species' entry of <paramref name="response"/> to its <c>|ln x_r - ln x_0|</c>; false when a species is present in one composition only.</summary>
    private static bool Accumulate(double[] response, double[] baseline, double[] logs)
    {
        for (var j = 0; j < response.Length; j++)
        {
            if (double.IsNaN(baseline[j]) != double.IsNaN(logs[j]))
            {
                return false;
            }

            if (!double.IsNaN(baseline[j]))
            {
                response[j] = Math.Max(response[j], Math.Abs(logs[j] - baseline[j]));
            }
        }

        return true;
    }

    /// <summary>
    /// The fewest and the most Newton steps the CPU accelerator took over the case and its replicates (<see cref="NoiseResponse"/>); null where the
    /// replicates do not characterise the case. A count of the other accelerator inside the range is a stop decision that rounding-level noise flips
    /// on the CPU accelerator alone, which the share of stations that stop after different numbers of steps does not measure.
    /// </summary>
    public (int Min, int Max)? StepRange(int k) => NoiseResponse(k) is null ? null : _steps[k];

    private SpeciesFunctionBatch AtTemperature(double temperature)
    {
        var request = new SpeciesFunctionBatch(table.SpeciesCount);
        for (var j = 0; j < table.SpeciesCount; j++)
        {
            request.Species[j] = j;
            request.Temperature[j] = temperature;
        }

        return request;
    }

    private EquilibriumBatch CaseOf(int k) => FixtureBatches.CaseOf(batch, k);

    private static double[] LogFractions(double[] moles, int s)
    {
        var total = moles.Take(s).Sum();
        return [.. moles.Take(s).Select(amount => amount > 0.0 ? Math.Log(amount / total) : double.NaN)];
    }
}

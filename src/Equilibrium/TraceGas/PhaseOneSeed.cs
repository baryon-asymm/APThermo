using APThermo.Equilibrium.GasPhase;
using APThermo.Thermo;

namespace APThermo.Equilibrium.TraceGas;

/// <summary>
/// The starts of a pass that begin from the phase-one point of the gas-phase node (BOOT.md, "The starts", 2 to 4), for a
/// state whose failed iterate holds no condensed species or the wrong ones: the point itself or the entry's condensed set,
/// the condensed amounts from the point or by least squares on the elements, the multipliers moved the least distance from the
/// anchor onto the condensed stationarities and then along the unit excess until the gases sum to one, and the gaseous moles
/// from the excess. Kernel-compatible; it borrows the matrix scratch, as the verdict does.
/// </summary>
internal static class PhaseOneSeed
{
    /// <summary>kmol/kg: a record of the point at or below this is at zero level (the fourth start drops it).</summary>
    private const double LevelFloor = 1.0e-12;

    /// <summary>Newton steps on ln S along the excess.</summary>
    private const int MaxLineSteps = 60;

    /// <summary>|ln S| along the excess at which the line search stops.</summary>
    private const double LineTest = 1.0e-12;

    /// <summary>The step along the excess when the slope of ln S is not positive.</summary>
    private const double FallbackStep = 1.0;

    /// <summary>The factor on n when the excess carries no gas: the gas is a trace.</summary>
    private const double TraceFactor = 1.0e-6;

    /// <summary>
    /// Asks the gas-phase node for the phase-one point and keeps it in <c>Tie.CondensedSet</c> and <c>Tie.CondensedMoles</c>
    /// (records in species order, <c>−1</c> after the last). True when the program completes; <paramref name="residual"/> is then
    /// the moles the condensed species cannot hold (positive: the vertex of phase one), or zero (the condensed minimum). The
    /// simplex overwrites <c>Tie.LogMoles</c>, where the pass keeps the entry's moles, so they are put aside in
    /// <c>scratch.LogMoles</c> and back. The result's moles, the condensed set and the state are the caller's to restore
    /// (<see cref="TraceGasStart.RestoreEntry"/>) afterwards: the point's call writes the first and the simplex the second. The
    /// one call site of <see cref="GasPhaseVerdict.PhaseOnePoint"/>.
    /// </summary>
    public static bool Fetch(in SpeciesTableView table, in EquilibriumProblem problem, in EquilibriumScratch scratch,
                             in EquilibriumResult result, out double residual)
    {
        var found = Point(table, problem, scratch, result, false, out residual);
        var records = 0;
        for (var j = table.GasCount; j < table.SpeciesCount && found; j++)
        {
            if (result.Moles[j] > 0.0 && records < ScratchLayout.MaxCondensedInSolution)
            {
                scratch.Tie.CondensedSet[records] = j;
                scratch.Tie.CondensedMoles[records++] = result.Moles[j];
            }
        }

        for (var k = records; k < ScratchLayout.MaxCondensedInSolution; k++)
        {
            scratch.Tie.CondensedSet[k] = -1;
        }

        return found;
    }

    /// <summary>
    /// The one call site of <see cref="GasPhaseVerdict.PhaseOnePoint"/>, over the condensed records alone or, with
    /// <paramref name="withGas"/>, over every gas too (<c>GasBasisSeed</c>). The simplex overwrites <c>Tie.LogMoles</c>, where the
    /// pass keeps the entry's moles, so they are put aside in <c>scratch.LogMoles</c> and back; the optimal basis the program leaves in
    /// the scratch is untouched.
    /// </summary>
    public static bool Point(in SpeciesTableView table, in EquilibriumProblem problem, in EquilibriumScratch scratch,
                             in EquilibriumResult result, bool withGas, out double residual)
    {
        for (var j = 0; j < table.SpeciesCount; j++)
        {
            scratch.LogMoles[j] = scratch.Tie.LogMoles[j];
        }

        var found = GasPhaseVerdict.PhaseOnePoint(table, problem, scratch, result, withGas, out residual);
        for (var j = 0; j < table.SpeciesCount; j++)
        {
            scratch.Tie.LogMoles[j] = scratch.LogMoles[j];
        }

        return found;
    }

    /// <summary>
    /// Makes the point fetched the case's iterate: every mole zero but the point's records, which become the condensed set in
    /// species order, those at or below <see cref="LevelFloor"/> left out when <paramref name="dropLevel"/>.
    /// </summary>
    public static void LoadPoint(in SpeciesTableView table, in EquilibriumScratch scratch, in EquilibriumResult result,
                                 ref IterationState state, bool dropLevel)
    {
        for (var j = 0; j < table.SpeciesCount; j++)
        {
            result.Moles[j] = 0.0;
        }

        var count = 0;
        for (var r = 0; r < ScratchLayout.MaxCondensedInSolution && scratch.Tie.CondensedSet[r] >= 0; r++)
        {
            var moles = scratch.Tie.CondensedMoles[r];
            if (dropLevel && moles <= LevelFloor)
            {
                continue;
            }

            result.Moles[scratch.Tie.CondensedSet[r]] = moles;
            scratch.CondensedInSolution[count++] = scratch.Tie.CondensedSet[r];
        }

        for (var c = count; c < ScratchLayout.MaxCondensedInSolution; c++)
        {
            scratch.CondensedInSolution[c] = -1;
        }

        state.CondensedCount = count;
        KeepRoomForTheGas(table, scratch, result, ref state);
    }

    /// <summary>
    /// Leaves room for the gas (BOOT.md, "The starts"): the phase rule allows at most (active elements − 1) condensed phases beside a gas
    /// at an assigned temperature and pressure, so when the set holds as many records as there are active elements, the record with the
    /// smallest positive amount leaves the set and its moles go to zero; a record at zero, the newcomer of an inclusion, stays. The
    /// verdict has proved a gas is required, so the record that carries the excess the gas replaces is the smallest. It counts in
    /// <c>SetChanges</c> like any change of the set.
    /// </summary>
    public static void KeepRoomForTheGas(in SpeciesTableView table, in EquilibriumScratch scratch, in EquilibriumResult result,
                                         ref IterationState state)
    {
        var active = 0;
        for (var i = 0; i < table.ElementCount; i++)
        {
            active += scratch.ElementActive[i] != 0 ? 1 : 0;
        }

        var count = state.CondensedCount;
        if (count < active)
        {
            return;
        }

        var smallest = -1;
        for (var c = 0; c < count; c++)
        {
            var moles = result.Moles[scratch.CondensedInSolution[c]];
            if (moles > 0.0 && (smallest < 0 || moles < result.Moles[scratch.CondensedInSolution[smallest]]))
            {
                smallest = c;
            }
        }

        if (smallest < 0)
        {
            return;
        }

        result.Moles[scratch.CondensedInSolution[smallest]] = 0.0;
        for (var c = smallest; c < count - 1; c++)
        {
            scratch.CondensedInSolution[c] = scratch.CondensedInSolution[c + 1];
        }

        scratch.CondensedInSolution[count - 1] = -1;
        state.CondensedCount = count - 1;
        state.SetChanges++;
    }

    /// <summary>
    /// Places the start on the condensed set in <c>scratch.CondensedInSolution</c> (<paramref name="frame"/>'s layout counts it):
    /// its amounts are those in the result's moles, or by least squares <c>AᵀA n_c = Aᵀ b</c> when
    /// <paramref name="leastSquares"/>; π moves the least distance onto <c>Σ_i a_ic π_i = g_c/RT</c>, then along the unit excess
    /// <c>r = b − A n_c</c> by Newton on ln S until <c>|ln S| &lt; 1e-12</c> (at most 60 steps); the gaseous moles are
    /// <c>n = (g · r)/(g · g)</c> with <c>g_i = Σ_j a_ij x_j</c>. Returns n, or zero when the start is skipped (a singular
    /// system, an excess of zero). The condensed amounts are written into the result's moles.
    /// </summary>
    public static double Place(in SpeciesTableView table, in EquilibriumProblem problem, in EquilibriumScratch scratch,
                               in EquilibriumResult result, in TraceGasFrame frame, bool leastSquares)
    {
        if (!Amounts(table, problem, scratch, result, frame, leastSquares))
        {
            return 0.0;
        }

        var norm = Excess(table, problem, scratch, frame.Layout);
        if (!(norm > 0.0))
        {
            return 0.0;
        }

        AlongExcess(table, scratch, result, frame);
        var n = Carried(table, scratch, result, frame, norm);
        for (var a = 0; a < frame.Layout.CondensedCount; a++)
        {
            result.Moles[scratch.CondensedInSolution[a]] = scratch.Matrix[AmountsAt(frame.Layout) + a];
        }

        return n;
    }

    /// <summary>Where the unit excess lies in the matrix scratch: after the condensed Gram matrix.</summary>
    private static int ExcessAt(in SystemLayout layout)
    {
        var rows = layout.CondensedCount > 1 ? layout.CondensedCount : 1;
        return rows * rows;
    }

    /// <summary>Where the condensed amounts lie in the matrix scratch: after the unit excess.</summary>
    private static int AmountsAt(in SystemLayout layout) => ExcessAt(layout) + layout.ElementCount;

    /// <summary>The condensed amounts (the result's, or by least squares) into the matrix scratch and the multipliers moved onto the condensed stationarities.</summary>
    private static bool Amounts(in SpeciesTableView table, in EquilibriumProblem problem, in EquilibriumScratch scratch,
                                in EquilibriumResult result, in TraceGasFrame frame, bool leastSquares)
    {
        var layout = frame.Layout;
        var count = layout.CondensedCount;
        if (!leastSquares)
        {
            for (var a = 0; a < count; a++)
            {
                scratch.Matrix[AmountsAt(layout) + a] = result.Moles[scratch.CondensedInSolution[a]];
            }
        }

        for (var pass = leastSquares ? 0 : 1; pass < 2 && count > 0; pass++)
        {
            Normal(table, problem, scratch, result, count, pass == 0);
            if (!DenseSolver.Solve(scratch.Matrix, scratch.RightHandSide, scratch.RowScale, count, count))
            {
                return false;
            }

            if (pass == 0)
            {
                for (var a = 0; a < count; a++)
                {
                    scratch.Matrix[AmountsAt(layout) + a] = scratch.RightHandSide[a];
                }
            }
            else
            {
                MoveOntoCondensed(table, scratch, result, layout);
            }
        }

        return true;
    }

    /// <summary>
    /// The Gram matrix of the condensed element vectors into the matrix scratch (stride <paramref name="count"/>) and the right-hand
    /// side of the amounts (<paramref name="amounts"/>: <c>Aᵀ b</c>) or of the multipliers' shift (<c>g_c/RT − Σ_i a_ic π_i</c>).
    /// </summary>
    private static void Normal(in SpeciesTableView table, in EquilibriumProblem problem, in EquilibriumScratch scratch,
                               in EquilibriumResult result, int count, bool amounts)
    {
        for (var a = 0; a < count; a++)
        {
            var ja = scratch.CondensedInSolution[a];
            for (var b = 0; b < count; b++)
            {
                scratch.Matrix[a * count + b] = ElementDot(table, ja, scratch.CondensedInSolution[b]);
            }

            var r = amounts ? 0.0 : scratch.GOverRT[ja];
            for (var i = 0; i < table.ElementCount; i++)
            {
                var aia = table.Stoichiometry[i * table.SpeciesCount + ja];
                r += amounts ? aia * problem.ElementMoles[i] : -aia * result.Multipliers[i];
            }

            scratch.RightHandSide[a] = r;
        }
    }

    /// <summary>Σ_i a_i,ja a_i,jb over every element.</summary>
    private static double ElementDot(in SpeciesTableView table, int ja, int jb)
    {
        var dot = 0.0;
        for (var i = 0; i < table.ElementCount; i++)
        {
            dot += table.Stoichiometry[i * table.SpeciesCount + ja] * table.Stoichiometry[i * table.SpeciesCount + jb];
        }

        return dot;
    }

    /// <summary>π_i += Σ_a a_i,ja y_a for the active elements, y the solution in the right-hand side: the least change that puts every condensed species on its stationarity.</summary>
    private static void MoveOntoCondensed(in SpeciesTableView table, in EquilibriumScratch scratch, in EquilibriumResult result,
                                          in SystemLayout layout)
    {
        for (var a = 0; a < layout.CondensedCount; a++)
        {
            var ja = scratch.CondensedInSolution[a];
            for (var i = 0; i < layout.ElementCount; i++)
            {
                if (scratch.ElementActive[i] != 0)
                {
                    result.Multipliers[i] += table.Stoichiometry[i * table.SpeciesCount + ja] * scratch.RightHandSide[a];
                }
            }
        }
    }

    /// <summary>The unit excess <c>(b − A n_c)/|b − A n_c|</c> of the active elements into the matrix scratch; returns the norm (zero: nothing for a gas to carry).</summary>
    private static double Excess(in SpeciesTableView table, in EquilibriumProblem problem, in EquilibriumScratch scratch,
                                 in SystemLayout layout)
    {
        var norm = 0.0;
        for (var i = 0; i < layout.ElementCount; i++)
        {
            var r = scratch.ElementActive[i] == 0 ? 0.0 : problem.ElementMoles[i];
            for (var a = 0; a < layout.CondensedCount; a++)
            {
                r -= table.Stoichiometry[i * table.SpeciesCount + scratch.CondensedInSolution[a]] * scratch.Matrix[AmountsAt(layout) + a];
            }

            scratch.Matrix[ExcessAt(layout) + i] = r;
            norm += r * r;
        }

        norm = Math.Sqrt(norm);
        for (var i = 0; norm > 0.0 && i < layout.ElementCount; i++)
        {
            scratch.Matrix[ExcessAt(layout) + i] /= norm;
        }

        return norm;
    }

    /// <summary>π moved along the unit excess by Newton on ln S until the gases sum to one (at most <see cref="MaxLineSteps"/> steps).</summary>
    private static void AlongExcess(in SpeciesTableView table, in EquilibriumScratch scratch, in EquilibriumResult result,
                                    in TraceGasFrame frame)
    {
        var layout = frame.Layout;
        for (var step = 0; step < MaxLineSteps; step++)
        {
            var f = LogSumAlong(table, scratch, result, frame, out var slope);
            if (Math.Abs(f) < LineTest)
            {
                break;
            }

            var t = slope > 0.0 ? -f / slope : FallbackStep;
            for (var i = 0; i < layout.ElementCount; i++)
            {
                result.Multipliers[i] += t * scratch.Matrix[ExcessAt(layout) + i];
            }
        }
    }

    /// <summary>ln S of the gases in play at the multipliers in the result, and its slope along the unit excess (<paramref name="slope"/>), by a stable sum of exponentials.</summary>
    private static double LogSumAlong(in SpeciesTableView table, in EquilibriumScratch scratch, in EquilibriumResult result,
                                      in TraceGasFrame frame, out double slope)
    {
        var top = double.NegativeInfinity;
        for (var j = 0; j < table.GasCount; j++)
        {
            if (SpeciesMarks.InPlay(scratch, j))
            {
                top = KernelMath.Max(top, TraceGasStep.LogFraction(table, scratch, result, frame.LogPressure, j));
            }
        }

        var sum = 0.0;
        var moment = 0.0;
        for (var j = 0; j < table.GasCount; j++)
        {
            if (!SpeciesMarks.InPlay(scratch, j))
            {
                continue;
            }

            var x = Math.Exp(TraceGasStep.LogFraction(table, scratch, result, frame.LogPressure, j) - top);
            sum += x;
            moment += x * ExcessCoefficient(table, scratch, frame.Layout, j);
        }

        slope = moment / sum;
        return top + Math.Log(sum);
    }

    /// <summary>Σ_i a_ij r_i: the change of ln x_j per unit step along the unit excess.</summary>
    private static double ExcessCoefficient(in SpeciesTableView table, in EquilibriumScratch scratch, in SystemLayout layout, int j)
    {
        var ad = 0.0;
        for (var i = 0; i < layout.ElementCount; i++)
        {
            ad += table.Stoichiometry[i * table.SpeciesCount + j] * scratch.Matrix[ExcessAt(layout) + i];
        }

        return ad;
    }

    /// <summary>
    /// The gaseous moles that carry the excess: <c>(g · r)/(g · g)</c> with <c>g_i = Σ_j a_ij x_j</c> and <c>r</c> the excess
    /// before it was normalized (<paramref name="norm"/> times the unit one); a millionth of the entry's n when
    /// the gas carries none of it.
    /// </summary>
    private static double Carried(in SpeciesTableView table, in EquilibriumScratch scratch, in EquilibriumResult result,
                                  in TraceGasFrame frame, double norm)
    {
        var layout = frame.Layout;
        var gr = 0.0;
        var gg = 0.0;
        for (var i = 0; i < layout.ElementCount; i++)
        {
            var g = 0.0;
            for (var j = 0; j < table.GasCount; j++)
            {
                if (SpeciesMarks.InPlay(scratch, j))
                {
                    g += table.Stoichiometry[i * table.SpeciesCount + j] * Math.Exp(TraceGasStep.LogFraction(table, scratch, result, frame.LogPressure, j));
                }
            }

            gr += g * scratch.Matrix[ExcessAt(layout) + i] * norm;
            gg += g * g;
        }

        return gr > 0.0 && gg > 0.0 ? gr / gg : frame.N * TraceFactor;
    }
}

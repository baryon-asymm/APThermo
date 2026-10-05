using APThermo.Thermo;

namespace APThermo.Equilibrium.TraceGas;

/// <summary>
/// What one trace-gas step does around the solved system (BOOT.md, "The step"): the exact gas fractions at the present
/// multipliers, the control factor, the weighted corrections the convergence test reads, the update of the unknowns and the
/// balance test. Kernel-compatible; <c>Corrections</c> of the scratch holds ln x_j between <see cref="Fractions"/> and the next
/// call of it.
/// </summary>
internal static class TraceGasStep
{
    /// <summary>The largest change of ln x_j of a gas above the first retention stage in one step, as equation (3.1)'s limit.</summary>
    private const double StepLimit = 2.0;

    /// <summary>The weight of the temperature correction among the changes of ln x_j: equation (3.1)'s 5.</summary>
    private const double TemperatureWeight = 5.0;

    /// <summary>ln(1e-4): a trace gas may not grow above 1e-4 of the gas in one step, as equation (3.2).</summary>
    private const double SmallSpeciesBound = 9.2103404;

    /// <summary>The element residual a convergence ends within, relative to the element's abundance: 0.3 of the node's invariant, so that the report's own rounding stays inside it.</summary>
    private const double BalanceTest = 0.3 * ElementBalance.Invariant;

    /// <summary>The bound of the close guard on the stationarity of every reported gas, in ln: 50 times the worst residual of the closes the Equilibrium tests make, 10 times a trace-gas convergence's own, 17 times below the smallest false <c>Ok</c> found (BOOT.md, "The close guard").</summary>
    private const double GasStationarityBound = 1.0e-9;

    /// <summary>The largest ln x_j the iteration evaluates; beyond it the iterate has left the problem.</summary>
    private const double MaxLogFraction = 300.0;

    /// <summary>The floor of n relative to its previous value when the linear step would make it non-positive.</summary>
    private const double PositivityFloor = 1.0e-3;

    /// <summary>
    /// ln x_j = Σ_i a_ij π_i − g_j/RT − ln(p/p°) of every gas in play into <c>Corrections</c>, ln n_j = ln n + ln x_j into
    /// <c>LogMoles</c>, and n x_j into the result's moles; returns S = Σ_j x_j, or +∞ when a fraction exceeds e^300. A gas out of
    /// play is zero in all three.
    /// </summary>
    public static double Fractions(in SpeciesTableView table, in EquilibriumScratch scratch, in EquilibriumResult result,
                                   double n, double logPressure)
    {
        var logN = Math.Log(n);
        var sum = 0.0;
        for (var j = 0; j < table.GasCount; j++)
        {
            if (!SpeciesMarks.InPlay(scratch, j))
            {
                scratch.Corrections[j] = 0.0;
                result.Moles[j] = 0.0;
                continue;
            }

            var logFraction = LogFraction(table, scratch, result, logPressure, j);
            if (logFraction > MaxLogFraction)
            {
                return double.PositiveInfinity;
            }

            scratch.Corrections[j] = logFraction;
            scratch.LogMoles[j] = logN + logFraction;
            var x = Math.Exp(logFraction);
            result.Moles[j] = n * x;
            sum += x;
        }

        return sum;
    }

    /// <summary>
    /// The close guard: every gas the result reports with moles above zero sits on its stationarity,
    /// <c>|ln(n_j/Σ_gas n) − ln x_j| ≤ 1e-9</c> with <c>ln x_j</c> the exact fraction the multipliers give
    /// (<see cref="LogFraction"/>). The element invariant cannot see a gas that is 1e-6 of the mixture and off its
    /// stationarity by 1e-4: a tolerance of 1e-12 in absolute moles hides it. A state without gas holds trivially. Reads the
    /// species functions at the settled temperature that <see cref="Composition.Refresh"/> left in the scratch; writes nothing.
    /// </summary>
    public static bool Stationary(in SpeciesTableView table, in EquilibriumScratch scratch, in EquilibriumResult result, double logPressure)
    {
        var sumGas = 0.0;
        for (var j = 0; j < table.GasCount; j++)
        {
            sumGas += result.Moles[j];
        }

        if (!(sumGas > 0.0))
        {
            return true;
        }

        var logSum = Math.Log(sumGas);
        for (var j = 0; j < table.GasCount; j++)
        {
            if (result.Moles[j] > 0.0
                && !(Math.Abs(Math.Log(result.Moles[j]) - logSum - LogFraction(table, scratch, result, logPressure, j)) <= GasStationarityBound))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>ln x_j = Σ_i a_ij π_i − g_j/RT − ln(p/p°) at the multipliers in the result: the exact fraction of the gas, relative to n, that the stationarity of the gas gives.</summary>
    public static double LogFraction(in SpeciesTableView table, in EquilibriumScratch scratch, in EquilibriumResult result,
                                     double logPressure, int j)
    {
        var logFraction = -scratch.GOverRT[j] - logPressure;
        for (var i = 0; i < table.ElementCount; i++)
        {
            logFraction += table.Stoichiometry[i * table.SpeciesCount + j] * result.Multipliers[i];
        }

        return logFraction;
    }

    /// <summary>
    /// The control factor λ of equations (3.1) and (3.2), from the solved system in the right-hand side: the largest change of
    /// ln x_j among the gases above the first retention stage (with the temperature's, weighted) limits the step to
    /// <see cref="StepLimit"/>; a gas below it may not grow past <see cref="SmallSpeciesBound"/>.
    /// </summary>
    public static double ControlFactor(in SpeciesTableView table, in EquilibriumScratch scratch, in SystemLayout layout)
    {
        var largest = layout.IsTp ? 0.0 : TemperatureWeight * Math.Abs(scratch.RightHandSide[layout.TRow]);
        var lambda2 = double.MaxValue;
        for (var j = 0; j < table.GasCount; j++)
        {
            if (!SpeciesMarks.InPlay(scratch, j))
            {
                continue;
            }

            var delta = LogChange(table, scratch, layout, j);
            var logFraction = scratch.Corrections[j];
            if (logFraction > -EquilibriumSolver.TraceThreshold)
            {
                largest = KernelMath.Max(largest, Math.Abs(delta));
            }
            else if (delta > 0.0)
            {
                lambda2 = KernelMath.Min(lambda2, (-logFraction - SmallSpeciesBound) / delta);
            }
        }

        var lambda = largest > StepLimit ? StepLimit / largest : 1.0;
        return KernelMath.Min(lambda, lambda2);
    }

    /// <summary>
    /// The weighted corrections of the convergence test, as RP-1311's equation (3.5) weighs them: the largest of n |δ|, n |u_c|
    /// and n x_j |Δ ln x_j|, over the moles of the gas and of the condensed species of the solution.
    /// </summary>
    public static double Worst(in SpeciesTableView table, in EquilibriumScratch scratch, in EquilibriumResult result, in TraceGasFrame frame)
    {
        var layout = frame.Layout;
        var total = frame.N * frame.Sum;
        var worst = frame.N * Math.Abs(scratch.RightHandSide[layout.NRow]);
        for (var c = 0; c < layout.CondensedCount; c++)
        {
            total += result.Moles[scratch.CondensedInSolution[c]];
            worst = KernelMath.Max(worst, frame.N * Math.Abs(scratch.RightHandSide[layout.ElementCount + c]));
        }

        for (var j = 0; j < table.GasCount; j++)
        {
            if (SpeciesMarks.InPlay(scratch, j))
            {
                worst = KernelMath.Max(worst, result.Moles[j] * Math.Abs(LogChange(table, scratch, layout, j)));
            }
        }

        return worst / total;
    }

    /// <summary>
    /// Takes the step: π += λ dπ, n_c += λ n u_c and n ← max(n (1 + λ δ), <see cref="PositivityFloor"/> n), for the active
    /// elements and the condensed species of the solution.
    /// </summary>
    public static void Apply(in EquilibriumScratch scratch, in EquilibriumResult result, in SystemLayout layout, double lambda, ref double n)
    {
        for (var i = 0; i < layout.ElementCount; i++)
        {
            if (scratch.ElementActive[i] != 0)
            {
                result.Multipliers[i] += lambda * scratch.RightHandSide[i];
            }
        }

        for (var c = 0; c < layout.CondensedCount; c++)
        {
            result.Moles[scratch.CondensedInSolution[c]] += lambda * n * scratch.RightHandSide[layout.ElementCount + c];
        }

        var next = n * (1.0 + lambda * scratch.RightHandSide[layout.NRow]);
        n = next > PositivityFloor * n ? next : PositivityFloor * n;
    }

    /// <summary>ln T += λ τ; false when the temperature leaves the iterate window of hp and sp (or is not a number), which ends the convergence <c>TemperatureOutOfRange</c>.</summary>
    public static bool MoveTemperature(ref IterationState state, double lambda, double tau)
    {
        state.Temperature = NextTemperature(state.Temperature, lambda, tau);
        return state.Temperature is >= EquilibriumSolver.MinTemperature and <= EquilibriumSolver.MaxTemperature;
    }

    /// <summary>The temperature the step λ τ leads to from <paramref name="temperature"/>: <c>exp(ln T + λ τ)</c>.</summary>
    public static double NextTemperature(double temperature, double lambda, double tau) =>
        Math.Exp(Math.Log(temperature) + lambda * tau);

    /// <summary>
    /// Whether every active element's balance holds within <see cref="BalanceTest"/> times b_i over the moles in the result, or, for a
    /// convergence at its step cap in the pass's second round (<paramref name="atCapOfSecondRound"/>), within the node's invariant itself
    /// (BOOT.md, "The rounds", 2026-10-05).
    /// </summary>
    public static bool Balanced(in SpeciesTableView table, in EquilibriumProblem problem, in EquilibriumScratch scratch, in EquilibriumResult result,
                                bool atCapOfSecondRound)
    {
        for (var i = 0; i < table.ElementCount; i++)
        {
            if (scratch.ElementActive[i] == 0)
            {
                continue;
            }

            var residual = Math.Abs(problem.ElementMoles[i] - ElementBalance.Abundance(table, result, i));
            if (!(residual <= BalanceTest * problem.ElementMoles[i]))
            {
                return atCapOfSecondRound && ElementBalance.WithinInvariant(table, problem, scratch, result);
            }
        }

        return true;
    }

    /// <summary>The change of ln x_j the solved system asks for: Σ_i a_ij dπ_i, plus h_j/RT · τ for hp and sp.</summary>
    private static double LogChange(in SpeciesTableView table, in EquilibriumScratch scratch, in SystemLayout layout, int j)
    {
        var delta = layout.IsTp ? 0.0 : scratch.HOverRT[j] * scratch.RightHandSide[layout.TRow];
        for (var i = 0; i < layout.ElementCount; i++)
        {
            delta += table.Stoichiometry[i * table.SpeciesCount + j] * scratch.RightHandSide[i];
        }

        return delta;
    }
}

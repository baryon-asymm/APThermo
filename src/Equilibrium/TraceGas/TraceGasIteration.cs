using System.Runtime.CompilerServices;
using APThermo.Thermo;

namespace APThermo.Equilibrium.TraceGas;

/// <summary>
/// One convergence of one condensed set by the trace-gas Newton (BOOT.md, "The unknowns and rows", "The step",
/// "Converged"): the same conditions as the reduced iteration of RP-1311, with the gaseous stationarity substituted exactly,
/// so that the gas composition is a function of the multipliers alone and the total gaseous moles enter linearly. Kernel-compatible;
/// reached from one call site, <see cref="TraceGasPass"/>'s sequence.
/// </summary>
internal static class TraceGasIteration
{
    /// <summary>Steps of one convergence: 50 left 18 of 19 CaCO3 states near their plateau unconverged (BOOT.md, 2026-10-04).</summary>
    private const int MaxSteps = 150;

    /// <summary>The polish level of the weighted corrections and of τ, as the Newton loop's.</summary>
    private const double PolishTest = 1.0e-11;

    /// <summary>|ln S| at the end: the stationarity residual every gas carries.</summary>
    private const double SumTest = 1.0e-10;

    /// <summary>
    /// Converges the current condensed set from the iterate in <paramref name="state"/> (its ln n, temperature and condensed
    /// set), the multipliers in <c>result.Multipliers</c> and the condensed moles in <c>result.Moles</c>. <c>Ok</c> after a step
    /// with λ = 1 whose corrections were below <see cref="PolishTest"/> and a next evaluation with every active element within
    /// the balance test and |ln S| within <see cref="SumTest"/>; the iterate is then in RP-1311's variables, every gas on its
    /// stationarity, <c>LogN = ln n + ln S</c> and the second retention stage in force. <c>SingularMatrix</c> when the system is
    /// singular, <c>TemperatureOutOfRange</c> when an hp or sp iterate leaves its window, otherwise <c>NotConverged</c>.
    /// Reached through this one method so that ILGPU compiles it once (root BOOT.md, compile size).
    /// </summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static CaseStatus Converge(in SpeciesTableView table, in EquilibriumProblem problem, in EquilibriumScratch scratch,
                                      in EquilibriumResult result, double logPressure, ref IterationState state)
    {
        var layout = TraceGasFrame.LayoutFor(table, problem, state.CondensedCount);
        var pin = new JunctionPin();
        var n = TraceGasStep.Total(state.LogN);
        var smallStep = false;
        for (var step = 0; step <= MaxSteps; step++)
        {
            Composition.Evaluate(table, scratch, ref state);
            var sum = TraceGasStep.Fractions(table, scratch, result, n, logPressure);
            if (sum is not (> 0.0 and <= double.MaxValue))
            {
                return CaseStatus.NotConverged;
            }

            if (smallStep && TraceGasStep.Balanced(table, problem, scratch, result, step == MaxSteps && state.TraceGasRound != 0)
                && Math.Abs(TraceGasStep.LogTotal(sum)) <= SumTest)
            {
                state.LogN = TraceGasStep.LogTotal(n) + TraceGasStep.LogTotal(sum);
                if (DataJunction.Decides(table, problem, scratch, result, ref state, ref pin))
                {
                    state.RetentionSecondStage = true;
                    state.RetainedSetHeld = true;
                    state.TraceCarriers = TraceGasReport.KeepBalanceCarriers(table, problem, scratch, result, state.LogN);
                    return CaseStatus.Ok;
                }

                smallStep = false;
                continue;
            }

            if (step == MaxSteps)
            {
                break;
            }

            var frame = new TraceGasFrame(layout, logPressure, n, sum, state.Temperature);
            if (!TraceGasSystem.Solve(table, problem, scratch, result, frame))
            {
                return CaseStatus.SingularMatrix;
            }

            state.Iterations++;
            var lambda = TraceGasStep.ControlFactor(table, scratch, layout);
            var tau = layout.IsTp ? 0.0 : scratch.RightHandSide[layout.TRow];
            var worst = TraceGasStep.Worst(table, scratch, result, frame);
            TraceGasStep.Apply(scratch, result, layout, lambda, ref n);
            if (!layout.IsTp)
            {
                if (DataJunction.Pins(table, scratch, ref state, ref pin, TraceGasStep.NextTemperature(state.Temperature, lambda, tau), tau))
                {
                    layout = TraceGasFrame.TpLayoutFor(table, state.CondensedCount);
                    smallStep = false;
                    continue;
                }

                if (!TraceGasStep.MoveTemperature(ref state, lambda, tau))
                {
                    return CaseStatus.TemperatureOutOfRange;
                }
            }

            smallStep = lambda == 1.0 && worst <= PolishTest && Math.Abs(tau) <= PolishTest;
        }

        return CaseStatus.NotConverged;
    }
}

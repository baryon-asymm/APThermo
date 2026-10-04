using APThermo.Thermo;

namespace APThermo.Equilibrium.TraceGas;

/// <summary>
/// The data junction (BOOT.md, "The data junction"): an internal interval bound of a species' fits, where the NASA fits of the two
/// ranges do not agree to the last digit (the jump of g/RT is 1e-8 to 1e-9 at 1 000 K for the species of KCl), so that no temperature
/// has the target enthalpy or entropy within the iteration's own tolerance. A temperature step of an hp or sp convergence that crosses
/// the same bound a second time with a small correction is dithering across it; the convergence is then pinned at the junction and
/// continues as a tp convergence at the bound T_J (the lower interval's functions) and at the next double T_J⁺ (the upper's); the
/// answer is the one of the two tp states whose h or s is nearer the target, T_J on a tie. A data junction is a defect of the fits,
/// not physics, and any state between the two is the equilibrium to the data's own precision (owner's decision, 2026-10-04, option (B)).
/// Kernel-compatible; the bounds are found through <c>SpeciesFunctions.IntervalOf</c>, not by reading the table's layout.
/// </summary>
internal static class DataJunction
{
    /// <summary>The largest temperature correction |τ| of a step that is read as dithering: 12 times the largest jump of a mixture's h measured at 1 000 K (8.3e-9 of c_p T) and above the steps of the dither (1e-15 to 1e-8).</summary>
    private const double DitherBound = 1.0e-7;

    /// <summary>The halvings of the search for a bound: 53 bits of mantissa end it, a bound is never further than that.</summary>
    private const int MaxHalvings = 64;

    /// <summary>
    /// Whether the step from the temperature of <paramref name="state"/> to <paramref name="next"/> pins the convergence: it crosses an
    /// internal interval bound of a species in play, the same bound the previous crossing crossed, with |<paramref name="tau"/>| at or below
    /// <see cref="DitherBound"/>. Then <paramref name="pin"/> holds the junction and the temperature of <paramref name="state"/> is T_J.
    /// Otherwise a crossing is remembered and false is returned.
    /// </summary>
    public static bool Pins(in SpeciesTableView table, in EquilibriumScratch scratch, ref IterationState state, ref JunctionPin pin,
                            double next, double tau)
    {
        var bound = Bound(table, scratch, state.CondensedCount, state.Temperature, next, out var upper);
        if (!(bound > 0.0))
        {
            return false;
        }

        if (bound == pin.LastBound && Math.Abs(tau) <= DitherBound)
        {
            pin.Lower = bound;
            pin.Upper = upper;
            pin.Phase = JunctionPin.MeasuringLower;
            state.Temperature = bound;
            return true;
        }

        pin.LastBound = bound;
        return false;
    }

    /// <summary>
    /// What a tp convergence of a pinned problem does after it converged: the first (at T_J) measures its miss and moves to T_J⁺; the
    /// second keeps its state when its miss is smaller than the first's, and otherwise moves back to T_J; the third is the answer. True
    /// when the converged state is the answer, false when the temperature moved and another convergence is wanted. A convergence that is
    /// not pinned is the answer. <c>state.LogN</c> is the converged <c>ln n + ln S</c>.
    /// </summary>
    public static bool Decides(in SpeciesTableView table, in EquilibriumProblem problem, in EquilibriumScratch scratch,
                               in EquilibriumResult result, ref IterationState state, ref JunctionPin pin)
    {
        if (pin.Phase is JunctionPin.Free or JunctionPin.AtLower)
        {
            return true;
        }

        var miss = Miss(table, problem, scratch, result, state);
        if (pin.Phase == JunctionPin.MeasuringLower)
        {
            pin.LowerMiss = miss;
            pin.Phase = JunctionPin.AtUpper;
            state.Temperature = pin.Upper;
            return false;
        }

        if (miss < pin.LowerMiss)
        {
            return true;
        }

        pin.Phase = JunctionPin.AtLower;
        state.Temperature = pin.Lower;
        return false;
    }

    /// <summary>
    /// The junction between the temperatures <paramref name="t1"/> and <paramref name="t2"/>: the largest temperature T_J that
    /// no species in play crosses an interval bound below, with <paramref name="upper"/> the smallest temperature above it; 0 when
    /// no species in play changes interval between them. A bound belongs to the lower interval (<c>IntervalOf</c>), so T_J is the bound
    /// itself and <paramref name="upper"/> the next double. The search halves the bracket until no double lies between its ends.
    /// </summary>
    public static double Bound(in SpeciesTableView table, in EquilibriumScratch scratch, int condensedCount, double t1, double t2, out double upper)
    {
        var lo = KernelMath.Min(t1, t2);
        var hi = KernelMath.Max(t1, t2);
        upper = 0.0;
        if (!Crosses(table, scratch, condensedCount, lo, hi))
        {
            return 0.0;
        }

        for (var halving = 0; halving < MaxHalvings; halving++)
        {
            var middle = 0.5 * (lo + hi);
            if (middle == lo || middle == hi)
            {
                break;
            }

            if (Crosses(table, scratch, condensedCount, lo, middle))
            {
                hi = middle;
            }
            else
            {
                lo = middle;
            }
        }

        upper = hi;
        return lo;
    }

    /// <summary>Whether some species in play, a gas or a condensed species of the solution, is on another interval of its fits at <paramref name="high"/> than at <paramref name="low"/>.</summary>
    private static bool Crosses(in SpeciesTableView table, in EquilibriumScratch scratch, int condensedCount, double low, double high)
    {
        for (var j = 0; j < table.GasCount; j++)
        {
            if (SpeciesMarks.InPlay(scratch, j) && ChangesInterval(table, j, low, high))
            {
                return true;
            }
        }

        for (var c = 0; c < condensedCount; c++)
        {
            if (ChangesInterval(table, scratch.CondensedInSolution[c], low, high))
            {
                return true;
            }
        }

        return false;
    }

    private static bool ChangesInterval(in SpeciesTableView table, int species, double low, double high) =>
        SpeciesFunctions.IntervalOf(table, species, low) != SpeciesFunctions.IntervalOf(table, species, high);

    /// <summary>
    /// The distance of the converged state's enthalpy (hp, J/kg) or entropy (sp, J/(kg K)) from the problem's target, from the sums
    /// of the moles in the result at the temperature of <paramref name="state"/>, every gas in play included.
    /// </summary>
    private static double Miss(in SpeciesTableView table, in EquilibriumProblem problem, in EquilibriumScratch scratch,
                               in EquilibriumResult result, in IterationState state)
    {
        var sums = Composition.FrozenSums(table, scratch, result, state, CaseSetup.LogPressure(problem));
        var value = problem.Kind == ProblemKind.AssignedEnthalpyPressure
            ? PhysicalConstants.R * state.Temperature * sums.HOverRT
            : PhysicalConstants.R * sums.SOverR;
        return Math.Abs(problem.Target - value);
    }
}

using APThermo.Equilibrium.GasPhase;
using APThermo.Thermo;

namespace APThermo.Equilibrium.Recovery;

/// <summary>
/// What a tp pass of an <see cref="AttemptPlan"/> found and what it leaves in the result (BOOT.md, "The ladder" and "The
/// bracket"): whether the gas phase's verdict proves the pass to hold no gas, the state a gasless end carries, the state a
/// failed case is cleared to, and the value and slope of a probe's assigned property. Kernel-compatible.
/// </summary>
internal static class PassOutcome
{
    /// <summary>
    /// Whether the verdict of the gas phase proves the tp pass to hold no gas, the window of the mixture bounding its
    /// temperature as for an Ok state; if so the result is the condensed minimum and its multipliers, and the plan's
    /// <see cref="AttemptPlan.Found"/> is <see cref="EndKind.Gasless"/> with its figures. The one call site of the verdict.
    /// </summary>
    public static bool Finds(in SpeciesTableView table, in EquilibriumScratch scratch, in EquilibriumResult result, ref AttemptPlan plan)
    {
        if (plan.Current.Temperature is < EquilibriumSolver.MinMixtureTemperature or > EquilibriumSolver.MaxMixtureTemperature
            || GasPhaseVerdict.Decide(table, plan.Current, scratch, result, out var figures) != GasVerdict.Gasless)
        {
            return false;
        }

        plan.Found = EndKind.Gasless;
        plan.Figures = figures;
        return true;
    }

    /// <summary>The state of a gasless case: the temperature and the pressure only (the one exception to "no status but Ok carries a state", root BOOT.md, 2026-10-03).</summary>
    public static void WriteState(in EquilibriumResult result, in AttemptPlan plan) =>
        result.State[0] = new MixtureState { Temperature = plan.Current.Temperature, Pressure = plan.Current.Pressure };

    /// <summary>The state of a failed case: zero, whatever a probe of the bracket wrote.</summary>
    public static void ClearState(in EquilibriumResult result) => result.State[0] = default;

    /// <summary>The assigned property of the pass just ended: h (<paramref name="hp"/>) or s, of its converged state or of its condensed minimum.</summary>
    public static double Value(in EquilibriumResult result, in AttemptPlan plan, bool hp) =>
        plan.Found == EndKind.Gasless
            ? (hp ? plan.Figures.Enthalpy : plan.Figures.Entropy)
            : (hp ? result.State[0].Enthalpy : result.State[0].Entropy);

    /// <summary>The slope of <see cref="Value"/> on ln T: T cp (hp) or cp, equilibrium for a state, the condensed minimum's for a gasless end.</summary>
    public static double Slope(in EquilibriumResult result, in AttemptPlan plan, bool hp) =>
        plan.Found == EndKind.Gasless
            ? (hp ? plan.Current.Temperature * plan.Figures.HeatCapacity : plan.Figures.HeatCapacity)
            : (hp ? result.State[0].Temperature * result.State[0].CpEquilibrium : result.State[0].CpEquilibrium);
}

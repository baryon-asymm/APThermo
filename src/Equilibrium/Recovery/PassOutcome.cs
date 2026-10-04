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

    /// <summary>
    /// The case ends <c>NoGasPhase</c>, its state the temperature and the pressure of the pass only (the one exception to "no
    /// status but Ok carries a state", root BOOT.md, 2026-10-03).
    /// </summary>
    public static void EndGasless(in EquilibriumResult result, ref AttemptPlan plan)
    {
        plan.Status = CaseStatus.NoGasPhase;
        result.State[0] = new MixtureState { Temperature = plan.Current.Temperature, Pressure = plan.Current.Pressure };
    }

    /// <summary>The case ends with the status its bracket gave up with, its state cleared.</summary>
    public static void EndGiveUp(in EquilibriumResult result, ref AttemptPlan plan)
    {
        plan.Status = plan.Bracket.GiveUpStatus;
        ClearState(result);
    }

    /// <summary>The temperature of the state the result holds, K.</summary>
    public static double Temperature(in EquilibriumResult result) => result.State[0].Temperature;

    /// <summary>Whether the problem assigns the temperature (tp), not the enthalpy or the entropy.</summary>
    public static bool IsTp(in EquilibriumProblem problem) => problem.Kind == ProblemKind.AssignedTemperaturePressure;

    /// <summary>The hp or sp case itself with <paramref name="temperature"/> as its estimate: the final attempt of the bracket.</summary>
    public static EquilibriumProblem CaseAt(in EquilibriumProblem problem, double temperature) =>
        new(problem.Kind, problem.Pressure, temperature, problem.Target, problem.ElementMoles);

    /// <summary>
    /// The cold retry takes no part of the warm attempt's own seed (Equilibrium BOOT.md, 2026-09-28): for hp and sp it starts at
    /// section 3.1's 3800 K, never at the failed warm attempt's temperature estimate, which is part of the seed the
    /// retry is discarding, since <see cref="CaseSetup.InitialTemperature"/> reads a positive <see cref="EquilibriumProblem.Temperature"/>
    /// as an estimate regardless of source. A tp's temperature is assigned, not an estimate, and is kept.
    /// </summary>
    public static EquilibriumProblem ColdRetry(in EquilibriumProblem problem) =>
        IsTp(problem) ? problem : new EquilibriumProblem(problem.Kind, problem.Pressure, 0.0, problem.Target, problem.ElementMoles);

    /// <summary>Whether the problem assigns the enthalpy (hp), not the entropy (sp).</summary>
    public static bool IsEnthalpy(in EquilibriumProblem problem) => problem.Kind == ProblemKind.AssignedEnthalpyPressure;

    /// <summary>The tp problem at <paramref name="temperature"/> of the case's pressure and element moles: a probe of the bracket.</summary>
    public static EquilibriumProblem TpAt(in EquilibriumProblem problem, double temperature) =>
        new(ProblemKind.AssignedTemperaturePressure, problem.Pressure, temperature, 0.0, problem.ElementMoles);

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

using APThermo.Equilibrium;
using APThermo.Thermo;

namespace APThermo.Performance;

/// <summary>
/// The chamber of an infinite-area combustor (RP-1311 section 6.3.1): the equilibrium at the assigned enthalpy and the chamber
/// pressure, at rest, and, in <see cref="FlowModel.FrozenAtChamber"/> flow, the frozen isentropic exponent and sound speed the
/// reference reports there (section 6.5.3).
/// </summary>
internal static class ChamberSolve
{
    /// <summary>Solves the chamber and writes its station; on Ok the reference every downstream station expands from.</summary>
    public static CaseStatus At(in RocketContext context, out ChamberReference chamber)
    {
        var result = context.Result;
        var problem = context.Problem;
        var pressureChamber = problem.ChamberPressure;

        // Chamber: assigned enthalpy and pressure (6.3.1).
        var chamberProblem = new EquilibriumProblem(ProblemKind.AssignedEnthalpyPressure, pressureChamber, problem.TemperatureEstimate,
                                                    problem.ReactantEnthalpy, problem.ElementMoles);
        var views = StationSolve.ViewsOf(in context, RocketSolver.Chamber);
        EquilibriumSolver.Solve(in context.Table, in chamberProblem, in context.Scratch, in views, false);
        if (result.StationStatus[RocketSolver.Chamber] != (int)CaseStatus.Ok)
        {
            chamber = default;
            return (CaseStatus)result.StationStatus[RocketSolver.Chamber];
        }

        var chamberState = result.Stations[RocketSolver.Chamber];
        chamberState.Velocity = 0.0;
        chamberState.Mach = 0.0;
        if (problem.Flow == FlowModel.FrozenAtChamber)
        {
            // The expansion is frozen from the chamber on: its isentropic exponent, sound speed and derivatives are the
            // frozen ones (6.5.3), as the reference reports them; the equilibrium heat capacities stay.
            // The two formulas are a declared deviation from the node's one-source rule (BOOT.md, Structure): the
            // equilibrium node's frozen solve cannot produce this mixed state, and moving them needs its contract to change.
            chamberState.GammaS = chamberState.CpFrozen / chamberState.CvFrozen;
            chamberState.SoundSpeed = Math.Sqrt(chamberState.GammaS * PhysicalConstants.R * chamberState.Temperature / chamberState.MolarMass);
            chamberState.DlnVdlnT = 1.0;
            chamberState.DlnVdlnP = -1.0;
        }

        result.Stations[RocketSolver.Chamber] = chamberState;
        chamber = new ChamberReference(pressureChamber, chamberState.Enthalpy, chamberState.Entropy, chamberState.GammaS);
        return CaseStatus.Ok;
    }
}

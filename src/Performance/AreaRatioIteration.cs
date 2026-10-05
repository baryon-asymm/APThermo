using APThermo.Thermo;

namespace APThermo.Performance;

/// <summary>
/// One exit station assigned by area ratio (RP-1311 section 6.3.5): the initial ln(p_c/p_e) of the report's estimates (6.21) to
/// (6.23), the correction of (6.24) and (6.25), and the verdict on the station. The supersonic branch only: a pass that falls
/// on the subsonic side of the sonic point is stepped outward, never accepted — a station is accepted only when its last pass
/// was supersonic (BOOT.md, 2026-09-26, finding F3), whether or not an earlier pass of the same iteration had converged.
/// </summary>
internal static class AreaRatioIteration
{
    /// <summary>
    /// The step in ln(p_c/p_e) taken when a pass falls on the subsonic side of the sonic point. This node's choice, not the
    /// report's: it decides how many of the <see cref="RocketSolver.MaxAreaRatioIterations"/> passes a subsonic start costs,
    /// and a start further below the sonic point than the twenty steps reach ends the station as NotConverged.
    /// </summary>
    private const double SubsonicStep = 0.1;

    /// <summary>Iterates the station to its assigned area ratio, writes its figures when it is met, and its status when it is not.</summary>
    public static ExitOutcome At(in RocketContext context, in ChamberReference chamber, in ThroatReference throat,
                                 double areaRatio, int station, ref ExitEstimate estimate)
    {
        var result = context.Result;
        var flow = context.Problem.Flow == FlowModel.ShiftingEquilibrium ? StationFlow.Shifting : StationFlow.Frozen;
        var logAreaRatio = StationFigures.LogOf(areaRatio);
        var logPressureRatio = InitialLogPressureRatio(in throat, areaRatio, logAreaRatio, in estimate);
        var temperatureEstimate = estimate.Temperature;
        var outcome = ExitOutcome.NeverSupersonic;
        var lastPassSupersonic = false;
        var derivative = 1.0;
        for (var iteration = 0; iteration < RocketSolver.MaxAreaRatioIterations; iteration++)
        {
            var pressure = StationFigures.PressureAt(chamber.Pressure, logPressureRatio);
            var request = new StationRequest(station, pressure, temperatureEstimate, chamber.Entropy, flow);
            if (!StationSolve.At(in context, in request))
            {
                estimate.Extrapolable = false;
                return ExitOutcome.SolveFailed;
            }

            var state = result.Stations[station];
            var velocitySquared = StationFigures.VelocitySquared(chamber.Enthalpy, in state);
            var soundSquared = state.SoundSpeed * state.SoundSpeed;
            if (!(velocitySquared > soundSquared))
            {
                // Subsonic side of the sonic point: move outward and try again; never accepted (finding F3).
                lastPassSupersonic = false;
                logPressureRatio += SubsonicStep;
                temperatureEstimate = state.Temperature;
                continue;
            }

            lastPassSupersonic = true;
            var velocity = Math.Sqrt(velocitySquared);
            var currentAreaRatio = StationFigures.AreaRatio(throat.MassFlux, in state, velocity);
            // Equation (6.23): ∂ln(A_e/A_t)/∂ln(p_c/p_e) at constant entropy.
            derivative = (velocitySquared - soundSquared) / (state.GammaS * velocitySquared);
            var correction = (logAreaRatio - StationFigures.LogOf(currentAreaRatio)) / derivative;
            if (Math.Abs(correction) <= RocketSolver.TightTolerance)
            {
                outcome = ExitOutcome.Converged;
                break;
            }

            // Equation (6.25): the report accepts the station when its last correction is this small.
            outcome = Math.Abs(correction) <= RocketSolver.AreaRatioTolerance ? ExitOutcome.WithinReportTolerance : ExitOutcome.NotMet;
            logPressureRatio += correction;
            temperatureEstimate = state.Temperature;
        }

        // Accepted only when the last pass was supersonic and met a tolerance there and then (finding F3): a subsonic
        // final pass keeps neither the verdict nor the acceptance of a supersonic pass earlier in the same iteration.
        if (!lastPassSupersonic || outcome is not (ExitOutcome.Converged or ExitOutcome.WithinReportTolerance))
        {
            estimate.Extrapolable = false;
            result.StationStatus[station] = (int)CaseStatus.NotConverged;
            return outcome;
        }

        estimate.LogAreaRatio = logAreaRatio;
        estimate.Derivative = derivative;
        Accept(in context, in chamber, in throat, areaRatio, station, ref estimate);
        return outcome;
    }

    /// <summary>
    /// The initial ln(p_c/p_e): the report's extrapolation (6.23, 6.24) from the previous station when both area ratios exceed
    /// <see cref="RocketSolver.ExtrapolationAreaRatio"/>, else its empirical formulas (6.21, 6.22).
    /// </summary>
    private static double InitialLogPressureRatio(in ThroatReference throat, double areaRatio, double logAreaRatio, in ExitEstimate estimate)
    {
        return estimate.Extrapolable && areaRatio > RocketSolver.ExtrapolationAreaRatio
            ? estimate.LogPressureRatio + (logAreaRatio - estimate.LogAreaRatio) / estimate.Derivative
            : areaRatio <= RocketSolver.ExtrapolationAreaRatio
                ? throat.LogPressureRatio + Math.Sqrt(3.294 * logAreaRatio * logAreaRatio + 1.535 * logAreaRatio)
                : throat.GammaS + 1.4 * logAreaRatio;
    }

    /// <summary>The station is met: its figures are written and it becomes the station the next one is extrapolated from.</summary>
    private static void Accept(in RocketContext context, in ChamberReference chamber, in ThroatReference throat,
                               double areaRatio, int station, ref ExitEstimate estimate)
    {
        var state = context.Result.Stations[station];
        var velocity = StationFigures.Velocity(chamber.Enthalpy, in state);
        var currentAreaRatio = StationFigures.AreaRatio(throat.MassFlux, in state, velocity);
        var inputs = new StationFigureInputs(velocity, currentAreaRatio, chamber.Pressure / state.Pressure, throat.CharacteristicVelocity);
        StationFigures.Write(in context, station, in inputs, chamber.Entropy);
        estimate.Extrapolable = areaRatio > RocketSolver.ExtrapolationAreaRatio;
        estimate.LogPressureRatio = StationFigures.LogOf(chamber.Pressure / state.Pressure);
    }
}

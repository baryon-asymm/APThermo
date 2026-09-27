using APThermo.Thermo;

namespace APThermo.Performance;

/// <summary>
/// The throat (RP-1311 section 6.3.3, BOOT.md "The throat carries the largest mass flux"): asks
/// <see cref="ThroatBracketSearch"/> for the pressure the throat is solved at — the momentum iterations of (6.15) to
/// (6.17), or, where they end short, the bracket's bisection or its plateau-edge acceptance — and then defines what
/// the accepted station gives the case: its mass flux, the characteristic velocity and the figures of the throat and
/// of the chamber.
/// </summary>
internal static class ThroatSearch
{
    /// <summary>Searches for the throat and writes it; on Ok the reference the exit stations expand from.</summary>
    public static CaseStatus At(in RocketContext context, in ChamberReference chamber, out ThroatReference throat)
    {
        StationSolve.CopyComposition(in context, RocketSolver.Chamber, RocketSolver.Throat);
        var temperatureEstimate = context.Result.Stations[RocketSolver.Chamber].Temperature;

        var query = new ThroatQuery(in context, in chamber);
        var status = ThroatBracketSearch.Locate(in query, temperatureEstimate, out var pressureSolved);
        if (status != CaseStatus.Ok)
        {
            context.Result.StationStatus[RocketSolver.Throat] = (int)status;
            throat = default;
            return status;
        }

        throat = Finish(in context, in chamber, pressureSolved);
        return (CaseStatus)context.Result.StationStatus[RocketSolver.Throat];
    }

    /// <summary>The mass flux and the characteristic velocity of the accepted station, and the figures of the throat and of the chamber.</summary>
    private static ThroatReference Finish(in RocketContext context, in ChamberReference chamber, double pressureThroat)
    {
        var result = context.Result;
        var pressureChamber = chamber.Pressure;
        var throatState = result.Stations[RocketSolver.Throat];
        var velocityThroat = StationFigures.Velocity(chamber.Enthalpy, in throatState);
        var massFluxThroat = throatState.Density * velocityThroat;
        var characteristicVelocity = pressureChamber / massFluxThroat;
        var inputs = new StationFigureInputs(velocityThroat, 1.0, pressureChamber / pressureThroat, characteristicVelocity);
        StationFigures.Write(in context, RocketSolver.Throat, in inputs, chamber.Entropy);
        var chamberFigures = result.Figures[RocketSolver.Chamber];
        chamberFigures.PressureRatio = 1.0;
        chamberFigures.CharacteristicVelocity = characteristicVelocity;
        result.Figures[RocketSolver.Chamber] = chamberFigures;
        return new ThroatReference(pressureThroat, massFluxThroat, characteristicVelocity, Math.Log(pressureChamber / pressureThroat),
                                   throatState.GammaS);
    }
}

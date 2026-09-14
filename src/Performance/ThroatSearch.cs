using AerospacePropellantThermodynamics.Thermo;

namespace AerospacePropellantThermodynamics.Performance;

/// <summary>
/// The throat (RP-1311 section 6.3.3): the initial pressure ratio of equation (6.15), the momentum update of (6.17) and the
/// sonic verdict of (6.16), then what the sonic station defines — its mass flux, the characteristic velocity and the figures
/// of the throat and of the chamber.
/// </summary>
internal static class ThroatSearch
{
    /// <summary>Searches for the sonic station and writes it; on Ok the reference the exit stations expand from.</summary>
    public static CaseStatus At(in RocketContext context, in ChamberReference chamber, out ThroatReference throat)
    {
        var result = context.Result;
        var flow = context.Problem.Flow == FlowModel.FrozenAtChamber ? StationFlow.Frozen : StationFlow.Shifting;
        var pressureChamber = chamber.Pressure;

        // Equation (6.15): the first estimate of the throat pressure from the chamber's isentropic exponent.
        var gammaChamber = chamber.GammaS;
        var pressureThroat = pressureChamber / Math.Pow(0.5 * (gammaChamber + 1.0), gammaChamber / (gammaChamber - 1.0));
        StationSolve.CopyComposition(in context, RocketSolver.Chamber, RocketSolver.Throat);
        var temperatureEstimate = result.Stations[RocketSolver.Chamber].Temperature;
        var sonicRatio = 0.0;
        var throatConverged = false;
        for (var k = 0; k < RocketSolver.MaxThroatIterations; k++)
        {
            var request = new StationRequest(RocketSolver.Throat, pressureThroat, temperatureEstimate, chamber.Entropy, flow);
            if (!StationSolve.At(in context, in request))
            {
                throat = default;
                return (CaseStatus)result.StationStatus[RocketSolver.Throat];
            }

            var state = result.Stations[RocketSolver.Throat];
            var velocitySquared = StationFigures.VelocitySquared(chamber.Enthalpy, in state);
            var soundSquared = state.SoundSpeed * state.SoundSpeed;
            sonicRatio = velocitySquared / soundSquared;
            if (!(velocitySquared > 0.0) || !(soundSquared > 0.0))
            {
                break;
            }

            if (Math.Abs(sonicRatio - 1.0) <= RocketSolver.TightTolerance)
            {
                throatConverged = true;
                break;
            }

            // Equation (6.17): the momentum relation from the current estimate to the sonic point.
            pressureThroat *= (1.0 + state.GammaS * sonicRatio) / (1.0 + state.GammaS);
            temperatureEstimate = state.Temperature;
        }

        if (!throatConverged && !(Math.Abs(sonicRatio - 1.0) <= RocketSolver.SonicTolerance))
        {
            result.StationStatus[RocketSolver.Throat] = (int)CaseStatus.ThroatNotFound;
            throat = default;
            return CaseStatus.ThroatNotFound;
        }

        throat = Finish(in context, in chamber, pressureThroat);
        return CaseStatus.Ok;
    }

    /// <summary>The mass flux and the characteristic velocity of the sonic station, and the figures of the throat and of the chamber.</summary>
    private static ThroatReference Finish(in RocketContext context, in ChamberReference chamber, double pressureThroat)
    {
        var result = context.Result;
        var pressureChamber = chamber.Pressure;
        var throatState = result.Stations[RocketSolver.Throat];
        var velocityThroat = StationFigures.Velocity(chamber.Enthalpy, in throatState);
        var massFluxThroat = throatState.Density * velocityThroat;
        var characteristicVelocity = pressureChamber / massFluxThroat;
        StationFigures.Write(in context, RocketSolver.Throat, velocityThroat, 1.0, pressureChamber / pressureThroat, characteristicVelocity);
        var chamberFigures = result.Figures[RocketSolver.Chamber];
        chamberFigures.PressureRatio = 1.0;
        chamberFigures.CharacteristicVelocity = characteristicVelocity;
        result.Figures[RocketSolver.Chamber] = chamberFigures;
        return new ThroatReference(pressureThroat, massFluxThroat, characteristicVelocity, Math.Log(pressureChamber / pressureThroat),
                                   throatState.GammaS);
    }
}

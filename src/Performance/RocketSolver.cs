using AerospacePropellantThermodynamics.Equilibrium;
using AerospacePropellantThermodynamics.Thermo;
using ILGPU;

namespace AerospacePropellantThermodynamics.Performance;

/// <summary>
/// Theoretical rocket performance of one case with an infinite-area chamber, RP-1311 Part I chapter 6: the chamber at
/// assigned enthalpy and pressure, the throat by the sonic condition (equations 6.15 to 6.17), the exit stations at assigned
/// pressure ratios or area ratios (6.21 to 6.25), in shifting equilibrium or with the composition frozen at the chamber or
/// at the throat (6.5). Kernel-compatible: static, no allocation, no exceptions.
/// </summary>
public static class RocketSolver
{
    /// <summary>Equation (6.16): the throat is sonic when |u² − a²|/u² is within this.</summary>
    public const double SonicTolerance = 4.0e-5;

    /// <summary>Equation (6.25): an assigned area ratio is met when the last correction of ln(p_c/p_e) is within this.</summary>
    public const double AreaRatioTolerance = 4.0e-5;

    /// <summary>Iterations continue past the report's tolerances until the correction is this small, so that the reported station is at rounding level.</summary>
    private const double TightTolerance = 1.0e-10;

    public const int MaxThroatIterations = 20;
    public const int MaxAreaRatioIterations = 20;

    /// <summary>Above this area ratio the report's analytic extrapolation from the previous station gives the initial estimate.</summary>
    private const double ExtrapolationAreaRatio = 2.0;

    /// <summary>The station indices of the two fixed stations; the k-th exit is <see cref="RocketLayout.FixedStations"/> + k.</summary>
    internal const int Chamber = 0;
    internal const int Throat = 1;

    /// <summary>Solves the case; every station's state, composition, figures and status are written, then the case status.</summary>
    public static void Solve(in SpeciesTableView table, in RocketProblem problem, in EquilibriumScratch scratch, in RocketResult result)
    {
        var speciesCount = table.SpeciesCount;
        var elementCount = table.ElementCount;
        var exitCount = (int)problem.ExitValues.Length;
        var stationCount = RocketLayout.StationCount(exitCount);
        result.Status[0] = (int)CaseStatus.InvalidInput;
        for (var station = 0; station < stationCount; station++)
        {
            result.StationStatus[station] = (int)CaseStatus.InvalidInput;
            result.Iterations[station] = 0;
            result.Figures[station] = default;
        }

        if (!(problem.ChamberPressure > 0.0) || speciesCount <= 0 || elementCount <= 0)
        {
            return;
        }

        var context = new RocketContext(in table, in problem, in scratch, in result);
        var pressureChamber = problem.ChamberPressure;

        var chamberStatus = ChamberSolve.At(in context, out var chamber);
        if (chamberStatus != CaseStatus.Ok)
        {
            result.Status[0] = (int)chamberStatus;
            return;
        }

        var frozenAtChamber = problem.Flow == FlowModel.FrozenAtChamber;
        var chamberFlow = frozenAtChamber ? StationFlow.Frozen : StationFlow.Shifting;
        var enthalpyChamber = chamber.Enthalpy;
        var entropyChamber = chamber.Entropy;

        // Throat: the pressure ratio for which the velocity equals the sound speed (6.3.3).
        var gammaChamber = chamber.GammaS;
        var pressureThroat = pressureChamber / Math.Pow(0.5 * (gammaChamber + 1.0), gammaChamber / (gammaChamber - 1.0));
        StationSolve.CopyComposition(in context, Chamber, Throat);
        var temperatureEstimate = result.Stations[Chamber].Temperature;
        var sonicRatio = 0.0;
        var throatConverged = false;
        for (var k = 0; k < MaxThroatIterations; k++)
        {
            var throatRequest = new StationRequest(Throat, pressureThroat, temperatureEstimate, entropyChamber, chamberFlow);
            if (!StationSolve.At(in context, in throatRequest))
            {
                result.Status[0] = result.StationStatus[Throat];
                return;
            }

            var state = result.Stations[Throat];
            var velocitySquared = StationFigures.VelocitySquared(enthalpyChamber, in state);
            var soundSquared = state.SoundSpeed * state.SoundSpeed;
            sonicRatio = velocitySquared / soundSquared;
            if (!(velocitySquared > 0.0) || !(soundSquared > 0.0))
            {
                break;
            }

            if (Math.Abs(sonicRatio - 1.0) <= TightTolerance)
            {
                throatConverged = true;
                break;
            }

            // Equation (6.17): the momentum relation from the current estimate to the sonic point.
            pressureThroat *= (1.0 + state.GammaS * sonicRatio) / (1.0 + state.GammaS);
            temperatureEstimate = state.Temperature;
        }

        if (!throatConverged && !(Math.Abs(sonicRatio - 1.0) <= SonicTolerance))
        {
            result.StationStatus[Throat] = (int)CaseStatus.ThroatNotFound;
            result.Status[0] = (int)CaseStatus.ThroatNotFound;
            return;
        }

        var throatState = result.Stations[Throat];
        var velocityThroat = StationFigures.Velocity(enthalpyChamber, in throatState);
        var massFluxThroat = throatState.Density * velocityThroat;
        var characteristicVelocity = pressureChamber / massFluxThroat;
        StationFigures.Write(in context, Throat, velocityThroat, 1.0, pressureChamber / pressureThroat, characteristicVelocity);
        var chamberFigures = result.Figures[Chamber];
        chamberFigures.PressureRatio = 1.0;
        chamberFigures.CharacteristicVelocity = characteristicVelocity;
        result.Figures[Chamber] = chamberFigures;

        // Exit stations, in the order given (6.3.2, 6.3.5 to 6.3.7).
        var frozen = problem.Flow != FlowModel.ShiftingEquilibrium;
        var exitFlow = frozen ? StationFlow.Frozen : StationFlow.Shifting;
        var freezingStation = frozenAtChamber ? Chamber : Throat;
        var logPressureRatioThroat = Math.Log(pressureChamber / pressureThroat);
        var gammaThroat = throatState.GammaS;
        var previousExtrapolable = false;
        var previousLogPressureRatio = 0.0;
        var previousLogAreaRatio = 0.0;
        var previousDerivative = 1.0;
        var caseStatus = CaseStatus.Ok;
        var lastSolved = Throat;   // the estimate for the next station comes from the last station that converged
        for (var k = 0; k < exitCount; k++)
        {
            var station = RocketLayout.FixedStations + k;
            var value = problem.ExitValues[k];
            var kind = (ExitSpecification)problem.ExitKinds[k];
            var source = frozen ? freezingStation : lastSolved;
            StationSolve.CopyComposition(in context, source, station);
            var previousState = result.Stations[lastSolved];
            var extrapolable = false;
            if (kind == ExitSpecification.PressureRatio)
            {
                if (!(value > 1.0))
                {
                    result.StationStatus[station] = (int)CaseStatus.InvalidInput;
                }
                else
                {
                    var pressure = pressureChamber / value;
                    var request = new StationRequest(station, pressure, previousState.Temperature, entropyChamber, exitFlow);
                    if (StationSolve.At(in context, in request))
                    {
                        var state = result.Stations[station];
                        var velocity = StationFigures.VelocityClamped(enthalpyChamber, in state);
                        var areaRatio = StationFigures.AreaRatio(massFluxThroat, in state, velocity);
                        StationFigures.Write(in context, station, velocity, areaRatio, value, characteristicVelocity);
                    }
                }
            }
            else if (!(value >= 1.0))
            {
                result.StationStatus[station] = (int)CaseStatus.AreaRatioInvalid;
            }
            else
            {
                // Initial estimate of ln(p_c/p_e): the report's extrapolation (6.23, 6.24) from the previous station when both
                // area ratios exceed 2, else its empirical formulas (6.21, 6.22).
                var logAreaRatio = Math.Log(value);
                double logPressureRatio;
                if (previousExtrapolable && value > ExtrapolationAreaRatio)
                {
                    logPressureRatio = previousLogPressureRatio + (logAreaRatio - previousLogAreaRatio) / previousDerivative;
                }
                else if (value <= ExtrapolationAreaRatio)
                {
                    logPressureRatio = logPressureRatioThroat + Math.Sqrt(3.294 * logAreaRatio * logAreaRatio + 1.535 * logAreaRatio);
                }
                else
                {
                    logPressureRatio = gammaThroat + 1.4 * logAreaRatio;
                }

                var estimate = previousState.Temperature;
                var converged = false;
                var lastCorrection = 0.0;
                var derivative = 1.0;
                var failed = false;
                for (var iteration = 0; iteration < MaxAreaRatioIterations; iteration++)
                {
                    var pressure = pressureChamber * Math.Exp(-logPressureRatio);
                    var request = new StationRequest(station, pressure, estimate, entropyChamber, exitFlow);
                    if (!StationSolve.At(in context, in request))
                    {
                        failed = true;
                        break;
                    }

                    var state = result.Stations[station];
                    var velocitySquared = StationFigures.VelocitySquared(enthalpyChamber, in state);
                    var soundSquared = state.SoundSpeed * state.SoundSpeed;
                    if (!(velocitySquared > soundSquared))
                    {
                        // Subsonic side of the sonic point: move outward and try again.
                        logPressureRatio += 0.1;
                        estimate = state.Temperature;
                        continue;
                    }

                    var velocity = Math.Sqrt(velocitySquared);
                    var currentAreaRatio = StationFigures.AreaRatio(massFluxThroat, in state, velocity);
                    // Equation (6.23): ∂ln(A_e/A_t)/∂ln(p_c/p_e) at constant entropy.
                    derivative = (velocitySquared - soundSquared) / (state.GammaS * velocitySquared);
                    lastCorrection = (logAreaRatio - Math.Log(currentAreaRatio)) / derivative;
                    if (Math.Abs(lastCorrection) <= TightTolerance)
                    {
                        converged = true;
                        break;
                    }

                    logPressureRatio += lastCorrection;
                    estimate = state.Temperature;
                }

                if (!failed)
                {
                    if (converged || Math.Abs(lastCorrection) <= AreaRatioTolerance)
                    {
                        var state = result.Stations[station];
                        var velocity = StationFigures.Velocity(enthalpyChamber, in state);
                        var areaRatio = StationFigures.AreaRatio(massFluxThroat, in state, velocity);
                        StationFigures.Write(in context, station, velocity, areaRatio, pressureChamber / state.Pressure, characteristicVelocity);
                        extrapolable = value > ExtrapolationAreaRatio;
                        previousLogPressureRatio = Math.Log(pressureChamber / state.Pressure);
                        previousLogAreaRatio = logAreaRatio;
                        previousDerivative = derivative;
                    }
                    else
                    {
                        result.StationStatus[station] = (int)CaseStatus.NotConverged;
                    }
                }
            }

            previousExtrapolable = extrapolable;
            if (result.StationStatus[station] == (int)CaseStatus.Ok)
            {
                lastSolved = station;
            }
            else if (caseStatus == CaseStatus.Ok)
            {
                caseStatus = (CaseStatus)result.StationStatus[station];
            }
        }

        result.Status[0] = (int)caseStatus;
    }
}

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
    internal const double TightTolerance = 1.0e-10;

    public const int MaxThroatIterations = 20;
    public const int MaxAreaRatioIterations = 20;

    /// <summary>Above this area ratio the report's analytic extrapolation from the previous station gives the initial estimate.</summary>
    internal const double ExtrapolationAreaRatio = 2.0;

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

        var throatStatus = ThroatSearch.At(in context, in chamber, out var throat);
        if (throatStatus != CaseStatus.Ok)
        {
            result.Status[0] = (int)throatStatus;
            return;
        }

        var frozenAtChamber = problem.Flow == FlowModel.FrozenAtChamber;
        var enthalpyChamber = chamber.Enthalpy;
        var entropyChamber = chamber.Entropy;
        var massFluxThroat = throat.MassFlux;
        var characteristicVelocity = throat.CharacteristicVelocity;

        // Exit stations, in the order given (6.3.2, 6.3.5 to 6.3.7).
        var frozen = problem.Flow != FlowModel.ShiftingEquilibrium;
        var exitFlow = frozen ? StationFlow.Frozen : StationFlow.Shifting;
        var freezingStation = frozenAtChamber ? Chamber : Throat;
        var estimate = default(ExitEstimate);
        estimate.Derivative = 1.0;
        var caseStatus = CaseStatus.Ok;
        var lastSolved = Throat;   // the estimate for the next station comes from the last station that converged
        for (var k = 0; k < exitCount; k++)
        {
            var station = RocketLayout.FixedStations + k;
            var value = problem.ExitValues[k];
            var kind = (ExitSpecification)problem.ExitKinds[k];
            var source = frozen ? freezingStation : lastSolved;
            StationSolve.CopyComposition(in context, source, station);
            estimate.Temperature = result.Stations[lastSolved].Temperature;
            if (kind == ExitSpecification.PressureRatio)
            {
                estimate.Extrapolable = false;
                if (!(value > 1.0))
                {
                    result.StationStatus[station] = (int)CaseStatus.InvalidInput;
                }
                else
                {
                    var pressure = pressureChamber / value;
                    var request = new StationRequest(station, pressure, estimate.Temperature, entropyChamber, exitFlow);
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
                estimate.Extrapolable = false;
                result.StationStatus[station] = (int)CaseStatus.AreaRatioInvalid;
            }
            else
            {
                // The station carries the verdict of the iteration: its figures when the area ratio was met, its status when it was not.
                AreaRatioIteration.At(in context, in chamber, in throat, value, station, ref estimate);
            }

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

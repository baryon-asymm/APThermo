using AerospacePropellantThermodynamics.Thermo;

namespace AerospacePropellantThermodynamics.Performance;

/// <summary>
/// The exit stations in the order they were given (RP-1311 sections 6.3.2, 6.3.5 to 6.3.7): the composition each one starts
/// from, the dispatch on its specification, the estimate carried from the last station that converged, and the status of the
/// case. A station that fails does not stop the ones after it; they start from the last station that converged.
/// </summary>
internal static class ExitStations
{
    /// <summary>Solves every exit station and returns the status of the case: Ok, or the first failure found.</summary>
    public static CaseStatus All(in RocketContext context, in ChamberReference chamber, in ThroatReference throat)
    {
        var result = context.Result;
        var problem = context.Problem;
        var exitCount = (int)problem.ExitValues.Length;
        var frozen = problem.Flow != FlowModel.ShiftingEquilibrium;
        var freezingStation = problem.Flow == FlowModel.FrozenAtChamber ? RocketSolver.Chamber : RocketSolver.Throat;
        var estimate = default(ExitEstimate);
        estimate.Derivative = 1.0;
        var caseStatus = CaseStatus.Ok;
        var lastSolved = RocketSolver.Throat;   // the estimate for the next station comes from the last station that converged
        for (var k = 0; k < exitCount; k++)
        {
            var station = RocketLayout.FixedStations + k;
            var source = frozen ? freezingStation : lastSolved;
            StationSolve.CopyComposition(in context, source, station);
            estimate.Temperature = result.Stations[lastSolved].Temperature;
            One(in context, in chamber, in throat, k, ref estimate);
            if (result.StationStatus[station] == (int)CaseStatus.Ok)
            {
                lastSolved = station;
            }
            else if (caseStatus == CaseStatus.Ok)
            {
                caseStatus = (CaseStatus)result.StationStatus[station];
            }
        }

        return caseStatus;
    }

    /// <summary>One exit station: the dispatch on how it was assigned.</summary>
    private static void One(in RocketContext context, in ChamberReference chamber, in ThroatReference throat, int exit,
                            ref ExitEstimate estimate)
    {
        var station = RocketLayout.FixedStations + exit;
        var value = context.Problem.ExitValues[exit];
        var kind = (ExitSpecification)context.Problem.ExitKinds[exit];
        if (kind == ExitSpecification.PressureRatio)
        {
            estimate.Extrapolable = false;
            if (!(value > 1.0))
            {
                // p_c/p_e must exceed 1: a ratio at or below 1 fixes no station downstream of the chamber.
                context.Result.StationStatus[station] = (int)CaseStatus.InvalidInput;
                return;
            }

            PressureRatioStation.At(in context, in chamber, in throat, value, station, estimate.Temperature);
            return;
        }

        if (!(value >= 1.0))
        {
            // The supersonic branch only: an area ratio below 1 is not a station of version 1.
            estimate.Extrapolable = false;
            context.Result.StationStatus[station] = (int)CaseStatus.AreaRatioInvalid;
            return;
        }

        // The station carries the verdict of the iteration: its figures when the area ratio was met, its status when it was not.
        AreaRatioIteration.At(in context, in chamber, in throat, value, station, ref estimate);
    }
}

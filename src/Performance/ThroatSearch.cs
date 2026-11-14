using APThermo.Thermo;

namespace APThermo.Performance;

/// <summary>
/// The throat search's orchestration: locate a candidate, verify that no upstream choke precedes it, then ask
/// <see cref="StationFigures"/> to write the accepted station's figures and downstream reference.
/// </summary>
internal static class ThroatSearch
{
    /// <summary>Searches for the throat and writes it; on Ok the reference the exit stations expand from.</summary>
    public static CaseStatus At(in RocketContext context, in ChamberReference chamber, out ThroatReference throat)
    {
        // Read before any solve touches the scratch, while it still carries the chamber's own condensed set
        // (BOOT.md, 2026-09-28, "the first maximum"): the chamber solve was the case's last solve before this stage.
        var chamberFingerprint = ThroatBracketSearch.CondensedFingerprint(in context);

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

        status = UpstreamChokeCheck.Verify(in query, chamberFingerprint, ref pressureSolved);
        if (status != CaseStatus.Ok)
        {
            context.Result.StationStatus[RocketSolver.Throat] = (int)status;
            throat = default;
            return status;
        }

        throat = StationFigures.Throat(in context, in chamber, pressureSolved);
        return (CaseStatus)context.Result.StationStatus[RocketSolver.Throat];
    }
}

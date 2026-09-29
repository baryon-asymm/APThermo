using APThermo.Thermo;

namespace APThermo.Performance;

/// <summary>
/// Proves that no choke lies between the chamber and a candidate throat <see cref="ThroatBracketSearch"/> located
/// (BOOT.md, 2026-09-28, "the throat is the first maximum of the mass flux met from the chamber", finding F1 of the
/// second hidden-defect audit): where the chamber's and the candidate's condensed fingerprints are equal, the
/// candidate stands as before; otherwise <see cref="PhaseBoundaryLocator"/> locates each boundary between them, and
/// this stage walks them from the chamber side, accepting the first plateau edge or the first smooth sonic crossing
/// found, or the candidate itself when neither precedes it.
/// </summary>
internal static class UpstreamChokeCheck
{
    /// <summary>
    /// Boundaries walked before giving up (BOOT.md, 2026-09-28, the third pass): two suffice for one melting
    /// plateau (its onset and its end), and this leaves headroom without an unbounded search. A walk that meets
    /// more boundaries than this without reaching the candidate's own fingerprint has proved nothing about the
    /// segment beyond the last one it looked at, so it ends <see cref="CaseStatus.ThroatNotFound"/>, never an
    /// unverified <see cref="CaseStatus.Ok"/> (the third pass's own observation: a cold scan of Li/O/H at 3 MPa
    /// already crosses four boundaries).
    /// </summary>
    internal const int MaxPhaseBoundaries = 8;

    /// <summary>
    /// Verifies the candidate at <paramref name="pressureSolved"/>; on return the value is unchanged when the
    /// candidate stands, or moved to an earlier choke's pressure, with the throat's row re-solved there.
    /// </summary>
    public static CaseStatus Verify(in ThroatQuery query, long chamberFingerprint, ref double pressureSolved)
    {
        var context = query.Context;
        var chamber = query.Chamber;
        var candidatePressure = pressureSolved;
        var candidateTemperature = context.Result.Stations[RocketSolver.Throat].Temperature;
        var candidateFingerprint = ThroatBracketSearch.CondensedFingerprint(in context);
        if (candidateFingerprint == chamberFingerprint)
        {
            // The interval from the chamber to the candidate shares one fingerprint throughout: the momentum or
            // bisection search already located the only sonic point in it.
            return CaseStatus.Ok;
        }

        StationSolve.CopyComposition(in context, RocketSolver.Chamber, RocketSolver.Throat);
        var outer = new PhaseBoundaryEnd(chamber.Pressure, context.Result.Stations[RocketSolver.Chamber].Temperature, 0.0, chamberFingerprint);
        var inner = new PhaseBoundaryEnd(candidatePressure, candidateTemperature, 1.0, candidateFingerprint);

        for (var b = 0; b < MaxPhaseBoundaries; b++)
        {
            var span = new PhaseBoundaryQuery(in outer, in inner);
            var status = PhaseBoundaryLocator.Locate(in query, in span, out var boundary);
            if (status != CaseStatus.Ok)
            {
                return status;
            }

            // The interval above this boundary shares the outer fingerprint (chamber's, or the previous boundary's
            // far side): a smooth crossing there, from the outer point's own subsonic ratio up to the boundary's, is
            // the earliest choke.
            if (boundary.Hi.SonicRatio >= 1.0)
            {
                return AcceptEarlierCrossing(in query, in outer, in boundary.Hi, ref pressureSolved);
            }

            // The boundary itself: a jump from subsonic above it to sonic or supersonic below it is the plateau-edge
            // choke, the state on the chamber side of the jump (BOOT.md's plateau-edge rule).
            if (boundary.Lo.SonicRatio >= 1.0)
            {
                return AcceptEdgeChoke(in query, in boundary, ref pressureSolved);
            }

            if (boundary.Lo.Fingerprint == candidateFingerprint)
            {
                // No further boundary separates this point from the candidate: the search already found the only
                // sonic point of that shared segment.
                return RestoreCandidate(in query, candidatePressure, candidateTemperature, ref pressureSolved);
            }

            outer = boundary.Lo;
        }

        // The cap is exhausted without reaching the candidate's own fingerprint or an earlier choke: nothing above
        // the last boundary looked at has been proved choke-free, so the candidate is never accepted unverified
        // (BOOT.md, 2026-09-28, the third pass's observation).
        return CaseStatus.ThroatNotFound;
    }

    /// <summary>
    /// Re-solves the throat row at the candidate's own pressure before accepting it: <see cref="PhaseBoundaryLocator"/>'s
    /// own bisections leave whatever they last solved in that row, which need not be the candidate's state even when
    /// no earlier choke overrides it.
    /// </summary>
    private static CaseStatus RestoreCandidate(in ThroatQuery query, double candidatePressure, double candidateTemperature, ref double pressureSolved)
    {
        var context = query.Context;
        var request = new StationRequest(RocketSolver.Throat, candidatePressure, candidateTemperature, query.Chamber.Entropy, query.Flow);
        if (!StationSolve.At(in context, in request))
        {
            return (CaseStatus)context.Result.StationStatus[RocketSolver.Throat];
        }

        pressureSolved = candidatePressure;
        return CaseStatus.Ok;
    }

    /// <summary>A smooth crossing between <paramref name="outer"/> (subsonic) and <paramref name="hi"/> (sonic or
    /// supersonic), both of the same fingerprint: bisected by the momentum search's own bisection.</summary>
    private static CaseStatus AcceptEarlierCrossing(in ThroatQuery query, in PhaseBoundaryEnd outer, in PhaseBoundaryEnd hi, ref double pressureSolved)
    {
        var bracket = new ThroatBracket
        {
            HasSubsonic = true,
            SubsonicPressure = outer.Pressure,
            SubsonicTemperature = outer.Temperature,
            SubsonicFingerprint = outer.Fingerprint,
            HasSupersonic = true,
            SupersonicPressure = hi.Pressure,
            SupersonicTemperature = hi.Temperature,
            SupersonicFingerprint = hi.Fingerprint,
        };
        var status = ThroatBracketSearch.Bisect(in query, ref bracket, out var pressureFound);
        if (status != CaseStatus.Ok)
        {
            return status;
        }

        pressureSolved = pressureFound;
        return CaseStatus.Ok;
    }

    /// <summary>The boundary's own jump: the single-phase side is accepted the way the momentum search's own
    /// plateau edge is (BOOT.md, finding F3's side check), never the pinned or later side.</summary>
    private static CaseStatus AcceptEdgeChoke(in ThroatQuery query, in PhaseBoundary boundary, ref double pressureSolved)
    {
        var bracket = new ThroatBracket
        {
            HasSubsonic = true,
            SubsonicPressure = boundary.Hi.Pressure,
            SubsonicTemperature = boundary.Hi.Temperature,
            SubsonicFingerprint = boundary.Hi.Fingerprint,
            HasSupersonic = true,
            SupersonicPressure = boundary.Lo.Pressure,
            SupersonicTemperature = boundary.Lo.Temperature,
            SupersonicFingerprint = boundary.Lo.Fingerprint,
        };
        return ThroatBracketSearch.AcceptPlateauEdge(in query, in bracket, ref pressureSolved);
    }
}

using APThermo.Thermo;

namespace APThermo.Performance;

/// <summary>
/// Locates one condensed-set boundary along the chamber isentrope between an outer point (closer to the chamber)
/// and an inner point (closer to a candidate throat) whose fingerprints differ, by a bounded bisection in ln p on
/// the condensed set (BOOT.md, 2026-09-28, "the first maximum"). <see cref="UpstreamChokeCheck"/> calls it once per
/// boundary, walking outward to inward, and reads the sonic ratio at each end to decide whether an earlier choke
/// lies above, at, or below it.
/// </summary>
internal static class PhaseBoundaryLocator
{
    /// <summary>
    /// Narrows <paramref name="span"/> to the boundary between its outer and inner fingerprints: on return,
    /// <paramref name="boundary"/>'s <c>Hi</c> is the last trial (or the outer end itself) still carrying the outer
    /// fingerprint, and its <c>Lo</c> the first trial (or the inner end itself) that no longer does, at most
    /// <see cref="RocketSolver.ThroatBracketWidth"/> apart in ln p.
    /// </summary>
    public static CaseStatus Locate(in ThroatQuery query, in PhaseBoundaryQuery span, out PhaseBoundary boundary)
    {
        var context = query.Context;
        var chamber = query.Chamber;
        var hi = span.Outer;
        var lo = span.Inner;
        for (var b = 0; b < RocketSolver.MaxThroatBisections; b++)
        {
            if (KernelMath.Log(hi.Pressure) - KernelMath.Log(lo.Pressure) < RocketSolver.ThroatBracketWidth)
            {
                break;
            }

            var midPressure = KernelMath.Exp(0.5 * (KernelMath.Log(hi.Pressure) + KernelMath.Log(lo.Pressure)));
            var midTemperature = 0.5 * (hi.Temperature + lo.Temperature);
            var request = new StationRequest(RocketSolver.Throat, midPressure, midTemperature, chamber.Entropy, query.Flow);
            if (!StationSolve.At(in context, in request))
            {
                boundary = default;
                return (CaseStatus)context.Result.StationStatus[RocketSolver.Throat];
            }

            var state = context.Result.Stations[RocketSolver.Throat];
            _ = ThroatBracketSearch.TryRatio(chamber.Enthalpy, in state, out var ratio);
            var fingerprint = ThroatBracketSearch.CondensedFingerprint(in context);
            var mid = new PhaseBoundaryEnd(midPressure, midTemperature, ratio, fingerprint);
            if (fingerprint == span.Outer.Fingerprint)
            {
                hi = mid;
            }
            else
            {
                lo = mid;
            }
        }

        boundary = new PhaseBoundary { Hi = hi, Lo = lo };
        return CaseStatus.Ok;
    }
}

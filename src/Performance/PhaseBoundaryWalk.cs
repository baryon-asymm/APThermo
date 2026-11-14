using APThermo.Thermo;

namespace APThermo.Performance;

/// <summary>What the next boundary establishes about the first choke from the chamber.</summary>
internal enum PhaseBoundaryAction
{
    Continue,
    EarlierCrossing,
    EdgeChoke,
    Candidate,
    Exhausted,
}

/// <summary>
/// The bounded chamber-to-candidate walk, independent of how a boundary is located.
/// Inspecting its last unresolved boundary is a refusal, never candidate acceptance.
/// </summary>
internal struct PhaseBoundaryWalk(PhaseBoundaryEnd outer, CondensedSetIdentity candidate)
{
    private int _visited;
    private readonly CondensedSetIdentity _candidate = candidate;

    internal PhaseBoundaryEnd Outer { get; private set; } = outer;

    internal static CaseStatus ExhaustedStatus => CaseStatus.ThroatNotFound;

    internal PhaseBoundaryAction Inspect(in PhaseBoundary boundary)
    {
        if (_visited >= UpstreamChokeCheck.MaxPhaseBoundaries)
        {
            return PhaseBoundaryAction.Exhausted;
        }

        _visited++;
        if (boundary.Hi.SonicRatio >= 1.0)
        {
            return PhaseBoundaryAction.EarlierCrossing;
        }

        if (boundary.Lo.SonicRatio >= 1.0)
        {
            return PhaseBoundaryAction.EdgeChoke;
        }

        if (boundary.Lo.Fingerprint.SameAs(in _candidate))
        {
            return PhaseBoundaryAction.Candidate;
        }

        Outer = boundary.Lo;
        return _visited < UpstreamChokeCheck.MaxPhaseBoundaries
            ? PhaseBoundaryAction.Continue : PhaseBoundaryAction.Exhausted;
    }
}

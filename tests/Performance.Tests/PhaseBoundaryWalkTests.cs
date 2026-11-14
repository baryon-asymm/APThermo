using APThermo.Thermo;

namespace APThermo.Performance.Tests;

/// <summary>Controlled boundary streams drive the same bounded walk that Verify uses.</summary>
public sealed class PhaseBoundaryWalkTests
{
    /// <summary>Unresolved boundaries at the cap establish no acceptable candidate.</summary>
    [Fact]
    public void AnUnresolvedBoundaryCapRefusesTheCandidate()
    {
        var candidate = CondensedSetIdentity.Single(1000);
        var outer = new PhaseBoundaryEnd(1.0, 1000.0, 0.0, CondensedSetIdentity.Single(0));
        var walk = new PhaseBoundaryWalk(outer, candidate);
        for (var index = 0; index < UpstreamChokeCheck.MaxPhaseBoundaries; index++)
        {
            var boundary = new PhaseBoundary
            {
                Hi = walk.Outer,
                Lo = new PhaseBoundaryEnd(1.0 / (index + 2), 1000.0, 0.4, CondensedSetIdentity.Single(index + 1)),
            };
            var action = walk.Inspect(in boundary);
            Assert.Equal(index + 1 < UpstreamChokeCheck.MaxPhaseBoundaries
                ? PhaseBoundaryAction.Continue : PhaseBoundaryAction.Exhausted, action);
        }

        Assert.Equal(CaseStatus.ThroatNotFound, PhaseBoundaryWalk.ExhaustedStatus);
    }

    /// <summary>A boundary reaching the candidate resolves the walk normally.</summary>
    [Fact]
    public void ReachingTheCandidateIsAValidPositiveControl()
    {
        var candidate = CondensedSetIdentity.Single(1);
        var outer = new PhaseBoundaryEnd(1.0, 1000.0, 0.0, CondensedSetIdentity.Single(0));
        var walk = new PhaseBoundaryWalk(outer, candidate);
        var boundary = new PhaseBoundary
        {
            Hi = outer,
            Lo = new PhaseBoundaryEnd(0.9, 1000.0, 0.4, candidate),
        };
        Assert.Equal(PhaseBoundaryAction.Candidate, walk.Inspect(in boundary));
    }
}
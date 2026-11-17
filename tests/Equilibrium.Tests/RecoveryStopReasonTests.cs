using APThermo.Equilibrium.Recovery;
using APThermo.Thermo;

namespace APThermo.Equilibrium.Tests;

/// <summary>Bracket stop reasons must not be confused with inherited statuses.</summary>
[Collection(CpuFixture.CollectionName)]
public sealed class RecoveryStopReasonTests
{
    /// <summary>A numerical retreat is not an edge certificate just because the first failure was out of range.</summary>
    [Theory]
    [InlineData(ProblemKind.AssignedEnthalpyPressure)]
    [InlineData(ProblemKind.AssignedEntropyPressure)]
    public void AnExhaustedNumericalProbeCannotReuseAnInheritedOutOfRangeStatus(ProblemKind kind)
    {
        RecoveryCandidateTests.WithViews(kind, (table, input, scratch, result) =>
        {
            var plan = new AttemptPlan();
            plan.Begin(input, useMolesAsEstimate: false);
            plan.Status = CaseStatus.Ok;
            RecoveryCandidateTests.WriteCandidate(table, result, RecoveryCandidateTests.State(kind, 0), 1.0);
            DeadEndCandidates.Begin(table, input, scratch, result, ref plan);
            plan.Recheck.Stage = Recheck.Held;
            plan.Recheck.Floor = double.MaxValue;
            plan.Bracket.Start(CaseStatus.TemperatureOutOfRange, 300.0);
            plan.Current = PassOutcome.TpAt(input, 300.0);
            plan.Phase = AttemptPhase.TraceGas;
            plan.Judged = CaseStatus.NotConverged;
            Assert.False(AttemptPlan.Next(table, input, scratch, result, CaseStatus.NotConverged, ref plan));
            Assert.Equal(CaseStatus.NotConverged, plan.Status);
            Assert.True(plan.Recheck.Unresolved);
            Assert.Equal(default, result.State[0]);
            RecoveryCandidateTests.AssertCandidate(table, result, 1.0);
        });
    }
}

using APThermo.Equilibrium.Recovery;
using APThermo.Thermo;

namespace APThermo.Equilibrium.Tests;

/// <summary>
/// The trace-gas finals of the temperature bracket (Recovery BOOT.md, "The trace-gas seams" (b) and (b′)): an hp or sp state at the
/// enthalpy or entropy of a tp state the trace-gas pass found ends <c>Ok</c> at that state's temperature, cold through a final the pass runs
/// because an end of the bracket was found by it, and warm through the retry of an ordinary final that failed.
/// </summary>
[Collection(CpuFixture.CollectionName)]
public sealed class TraceGasFinalTests
{
    /// <summary>The ordinary final that just ended is owed one trace-gas final when it failed with a status the pass can mend, and only once.</summary>
    [Theory]
    [InlineData("Final", CaseStatus.NotConverged, 0, true)]
    [InlineData("Final", CaseStatus.SingularMatrix, 0, true)]
    [InlineData("Final", CaseStatus.TemperatureOutOfRange, 0, false)]
    [InlineData("Final", CaseStatus.Ok, 0, false)]
    [InlineData("Final", CaseStatus.NotConverged, 1, false)]
    [InlineData("TraceGasFinal", CaseStatus.NotConverged, 0, false)]
    [InlineData("Warm", CaseStatus.NotConverged, 0, false)]
    public void AFailedOrdinaryFinalIsOwedOneTraceGasFinal(string phase, CaseStatus status, int launched, bool owed)
    {
        var plan = default(AttemptPlan);
        plan.Phase = Enum.Parse<AttemptPhase>(phase);
        plan.Status = status;
        plan.TraceGasFinals = launched;

        Assert.Equal(owed, plan.OwesTraceGasFinal);
    }

    /// <summary>The seam (a) schedules one trace-gas pass for a pass whose failure the verdict judged, and no pass for a verdict-only pass or another status.</summary>
    [Theory]
    [InlineData("Warm", CaseStatus.NotConverged, true)]
    [InlineData("Cold", CaseStatus.SingularMatrix, true)]
    [InlineData("Cold", CaseStatus.TemperatureOutOfRange, false)]
    [InlineData("VerdictOnly", CaseStatus.NotConverged, false)]
    public void TheVerdictSchedulesOneTraceGasPassForAFailureItJudged(string phase, CaseStatus status, bool scheduled)
    {
        var plan = default(AttemptPlan);
        plan.Phase = Enum.Parse<AttemptPhase>(phase);
        plan.Status = status;

        plan.SeekTraceGas();

        Assert.Equal(scheduled, plan.Phase == AttemptPhase.TraceGas);
        Assert.Equal(scheduled ? status : default, plan.Judged);
        Assert.Equal(scheduled ? EstimateSource.PreviousSolution : default, plan.Source);
    }

    /// <summary>CaCO3 + 1e-8 CO2 below its plateau, the tp states of the calcite family at the smallest excess.</summary>
    public static TheoryData<string> Calcite() => TraceGasCases.Names(TraceGasCases.CalciteBelowThePlateau().Where(c => c.Name.StartsWith("calcite|1E-08|", StringComparison.Ordinal)));

    /// <summary>Seam (b): cold hp and sp at the h or s of the tp states of CaCO3 + 1e-8 CO2 below its plateau end <c>Ok</c> at the tp temperature within 1e-9.</summary>
    [Theory]
    [MemberData(nameof(Calcite))]
    public void AColdHpOrSpStateAtTheEnthalpyOfATraceGasStateEndsOkAtItsTemperature(string name)
    {
        var state = TraceGasCases.Named(name);
        var tp = state.Solve();
        TraceGasChecks.AssertOkAndClear(tp, name + " tp");
        foreach (var mode in new[] { "hp-cold", "sp-cold" })
        {
            var solution = state.SolveInMode(tp, mode);
            TraceGasChecks.AssertOkAndClear(solution, $"{mode} {name}");
            Assert.True(
                Math.Abs(solution.State.Temperature - tp.State.Temperature) <= 1.0e-9 * tp.State.Temperature,
                $"{mode} {name}: T {solution.State.Temperature:R} against {tp.State.Temperature:R}");
        }
    }

    /// <summary>
    /// Seam (b′): MgCO3 + 1e-6 CO2 one kelvin below its plateau at 1 kPa, hp warm from its own tp composition, ends <c>Ok</c> at the tp
    /// temperature: the reduced iteration's first <c>Ok</c> is refused by the close guard, the bracket finds two gas ends, and the final
    /// that fails is run again by the trace-gas pass.
    /// </summary>
    [Fact]
    public void AnOrdinaryFinalThatFailsIsRetriedByTheTraceGasPass()
    {
        var state = TraceGasCases.MagnesiteBelowThePlateau().First(c => c.Name == "magnesite-below|1000|1");
        var tp = state.Solve();
        TraceGasChecks.AssertOkAndClear(tp, "tp");

        var solution = state.SolveInMode(tp, "hp-warm");

        TraceGasChecks.AssertOkAndClear(solution, "hp-warm");
        Assert.True(Math.Abs(solution.State.Temperature - tp.State.Temperature) <= 1.0e-9 * tp.State.Temperature);
    }
}

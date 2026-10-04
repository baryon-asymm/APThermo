using APThermo.Equilibrium.GasPhase;
using APThermo.Equilibrium.TraceGas;
using APThermo.Thermo;
using ILGPU.Runtime;

namespace APThermo.Equilibrium.Tests;

/// <summary>
/// The trace carrier's walk (TraceGas BOOT.md, <c>## Acceptance criteria</c>, seam (a)): MgCO3 under CO2 at Mg:C:O = 1:2:5 and
/// 10 MPa, where MgO(cr) and CO2 satisfy O = Mg + 2C, so the direction π_O − π_Mg − 2π_C is carried only by CO and O2, which the
/// reduced iteration holds at zero below its retention threshold: it walks one e-fold per step along that direction and the
/// element invariant fails at the close. The trace-gas pass converges the gas composition exactly.
/// </summary>
[Collection(CpuFixture.CollectionName)]
public sealed class TraceCarrierWalkTests
{
    /// <summary>
    /// kmol/kg: how far n_CO − 2 n_O2 may be from zero. The mixture satisfies O = Mg + 2C exactly, so the true state has twice as much CO as
    /// O2, but the balance of the combination O − Mg − 2C holds only to the element invariant on each of its three elements
    /// (<see cref="EquilibriumConditions.ElementInvariant"/>, absolute below 1 kmol/kg), and the carriers are 1e-11 to 1e-9 of the
    /// gas: a ratio is not held tighter than a few percent at the lower end (measured 1.9e-2 at 765 K, 3e-5 at 800 K).
    /// </summary>
    private const double CarrierBalance = 3.0 * EquilibriumConditions.ElementInvariant;

    /// <summary>The 41 temperatures from 700 to 900 K every 5 K.</summary>
    public static TheoryData<string> States() => TraceGasCases.Names(TraceGasCases.MagnesiteBand());

    /// <summary>
    /// Cold tp: <c>Ok</c>, clear of the equilibrium conditions at 1e-9 with every reported gas on its stationarity, and CO twice O2 to
    /// the balance of the combination (<see cref="CarrierBalance"/>). Red on the reduced iteration alone: 25 of the 41 ended <c>NotConverged</c>.
    /// </summary>
    [Theory]
    [MemberData(nameof(States))]
    public void EveryStateOfTheBandIsOkClearAndCarriesTwiceAsMuchCoAsO2(string name)
    {
        var state = TraceGasCases.Named(name);
        var solution = state.Solve();

        TraceGasChecks.AssertOkAndClear(solution, name);
        var table = solution.Case.Table;
        var carbonMonoxide = solution.Moles[table.IndexOf("CO")];
        var oxygen = solution.Moles[table.IndexOf("O2")];
        Assert.True(Math.Abs(carbonMonoxide - 2.0 * oxygen) <= CarrierBalance, $"{name}: CO {carbonMonoxide:E3}, O2 {oxygen:E3} kmol/kg");
    }

    /// <summary>
    /// The iterations a case reports are the steps of its attempt and of its trace-gas pass: the cold attempt of the 800 K state, the
    /// verdict, and the pass, driven here in the order <c>Solve</c> runs them, add up to the count of the solve.
    /// </summary>
    [Fact]
    public void TheIterationsOfACaseAreTheStepsOfItsAttemptAndOfItsTracePass()
    {
        var state = TraceGasCases.MagnesiteBand().First(c => c.Temperature == 800.0);
        var table = TraceGasCases.TableOver(state.Elements);
        using var rig = DerivativeRig.Of(table, state.Temperature, _ => 0.0, []);
        using var elements = CpuFixture.Shared.Accelerator.Allocate1D(state.ElementMoles);
        var problem = new EquilibriumProblem(ProblemKind.AssignedTemperaturePressure, state.Pressure, state.Temperature, 0.0, elements.View);
        var logPressure = CaseSetup.LogPressure(problem);
        var view = rig.View;

        var attempt = new IterationState();
        Assert.Equal(CaseStatus.Ok, CaseSetup.Begin(view, problem, rig.Scratch, rig.Result, EstimateSource.Defaults, ref attempt));
        _ = ConvergenceSequence.Run(view, problem, rig.Scratch, rig.Result, logPressure, ref attempt);
        Assert.Equal(GasVerdict.GasRequired, GasPhaseVerdict.Decide(view, problem, rig.Scratch, rig.Result, out _));
        var pass = new IterationState();
        Assert.Equal(CaseStatus.Ok, CaseSetup.Begin(view, problem, rig.Scratch, rig.Result, EstimateSource.PreviousSolution, ref pass));
        Assert.Equal(CaseStatus.Ok, TraceGasPass.Run(view, problem, rig.Scratch, rig.Result, logPressure, ref pass));

        Assert.True(attempt.Iterations > 0 && pass.Iterations > 0);
        Assert.Equal(state.Solve().Iterations, attempt.Iterations + pass.Iterations);
    }
}

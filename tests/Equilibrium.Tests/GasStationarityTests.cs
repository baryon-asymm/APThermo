using APThermo.Equilibrium.TraceGas;
using APThermo.Thermo;

namespace APThermo.Equilibrium.Tests;

/// <summary>
/// The close guard (TraceGas BOOT.md, "The close guard"): every gas a state reports sits on its stationarity within 1e-9. A unit fact on
/// the bound, and the states the reduced iteration closed <c>Ok</c> with a gas that had not converged: with the guard they end <c>Ok</c>
/// at the temperature of their tp state, through the temperature bracket and the trace-gas finals of the <c>Recovery</c> node.
/// </summary>
[Collection(CpuFixture.CollectionName)]
public sealed class GasStationarityTests
{
    private const double Temperature = 1000.0;

    /// <summary>The bound of the guard, 1e-9, against a residual that is the share of gas the amounts leave out of their sum.</summary>
    private const double Bound = 1.0e-9;

    /// <summary>The modes a state is solved at the enthalpy or entropy of its tp state: cold, warm from the tp composition, warm from the tp state 5 K above.</summary>
    private static readonly string[] Modes = ["hp-cold", "sp-cold", "hp-warm", "sp-warm", "hp-warm5", "sp-warm5"];

    /// <summary>The mixtures, tp states of the design's two families: MgCO3 + 1e-6 CO2 below its plateau, and the states with a loose gas.</summary>
    public static TheoryData<string, string> Refused()
    {
        var data = new TheoryData<string, string>();
        var floor = MagnesiteDataFloor();
        foreach (var state in TraceGasCases.MagnesiteBelowThePlateau().Where(c => c.Temperature >= floor).Concat(TraceGasCases.LooseOks()))
        {
            var name = TraceGasCases.Register(state);
            foreach (var mode in Modes)
            {
                data.Add(mode, name);
            }
        }

        return data;
    }

    /// <summary>
    /// The lowest temperature of the MgCO3(cr) record: below it the record is no candidate, the tp state is a supercooled vapour and the
    /// enthalpy of the equilibria is not monotone in the temperature, so an hp or sp state of it is another state (the dead-end floors of
    /// <c>Recovery</c>'s BOOT.md).
    /// </summary>
    private static double MagnesiteDataFloor()
    {
        var table = TraceGasCases.TableOver(["MG", "C", "O"]);
        using var buffers = SpeciesTableBuffers.Upload(CpuFixture.Shared.Accelerator, table);
        return SpeciesFunctions.RecordLow(buffers.View, table.IndexOf("MgCO3(cr)"));
    }

    /// <summary>
    /// A composition whose amounts sit 5e-10 in ln Σn off the stationarity of every gas is accepted, one 2e-9 off is refused: the residual
    /// of every gas is the share left out of the sum, which is built here by moving the multipliers of oxygen until the fractions sum to
    /// the given exp(−shift) and putting the amounts on the normalized fractions.
    /// </summary>
    [Theory]
    [InlineData(5.0e-10, true)]
    [InlineData(-5.0e-10, true)]
    [InlineData(2.0e-9, false)]
    [InlineData(-2.0e-9, false)]
    public void TheGuardAcceptsAShiftOfFiveTenthsOfANanoAndRefusesTwoNanos(double shift, bool accepted)
    {
        var table = TraceGasCases.TableOver(["O"]);
        using var rig = DerivativeRig.Of(table, Temperature, _ => 0.0, []);
        var view = rig.View;
        Composition.EvaluateFunctions(view, rig.Scratch, Temperature);
        var low = -40.0;
        var high = 40.0;
        for (var step = 0; step < 200; step++)
        {
            rig.Result.Multipliers[0] = 0.5 * (low + high);
            if (LogFractionSum(view, rig) < shift)
            {
                low = rig.Result.Multipliers[0];
            }
            else
            {
                high = rig.Result.Multipliers[0];
            }
        }

        var logSum = LogFractionSum(view, rig);
        Assert.Equal(shift, logSum, 1.0e-13);
        for (var j = 0; j < table.GasCount; j++)
        {
            rig.Result.Moles[j] = Math.Exp(TraceGasStep.LogFraction(view, rig.Scratch, rig.Result, 0.0, j) - logSum);
        }

        Assert.Equal(accepted, TraceGasStep.Stationary(view, rig.Scratch, rig.Result, 0.0));
    }

    /// <summary>
    /// Every state the reduced iteration closed on with a gas off its stationarity ends <c>Ok</c>, clear of the conditions at 1e-9, at the
    /// temperature of its tp state within 1e-9, with the gas of its tp state within 1e-9 of its fractions: a trace carrier below the element
    /// invariant is not fixed more closely than that by any state of the same temperature. The states declared in
    /// <c>TraceGasLeftovers.txt</c> end <c>NotConverged</c>.
    /// </summary>
    [Theory]
    [MemberData(nameof(Refused))]
    public void AStateTheGuardRefusesEndsOkAtTheTemperatureOfItsTpState(string mode, string name)
    {
        ArgumentNullException.ThrowIfNull(mode);
        var state = TraceGasCases.Named(name);
        var tp = state.Solve();
        TraceGasChecks.AssertOkAndClear(tp, name + " tp");
        var solution = state.SolveInMode(tp, mode);

        if (TraceGasLeftovers.Declared($"{mode}|{name}"))
        {
            Assert.Equal(CaseStatus.NotConverged, solution.Status);
            return;
        }

        TraceGasChecks.AssertOkAndClear(solution, $"{mode} {name}");
        var label = $"{mode} {name}: T {solution.State.Temperature:R} against {tp.State.Temperature:R}";
        Assert.True(Math.Abs(solution.State.Temperature - tp.State.Temperature) <= Bound * tp.State.Temperature, label);
        var table = tp.Case.Table;
        var gasTp = tp.Moles.Take(table.GasCount).Sum();
        var gas = solution.Moles.Take(table.GasCount).Sum();
        for (var j = 0; j < table.GasCount; j++)
        {
            var deviation = Math.Abs(solution.Moles[j] / gas - tp.Moles[j] / gasTp);
            Assert.True(deviation <= Bound, $"{label}: {table.Species[j]} {solution.Moles[j] / gas:E6} against {tp.Moles[j] / gasTp:E6}");
        }
    }

    private static double LogFractionSum(in SpeciesTableView view, DerivativeRig rig)
    {
        var sum = 0.0;
        for (var j = 0; j < view.GasCount; j++)
        {
            sum += Math.Exp(TraceGasStep.LogFraction(view, rig.Scratch, rig.Result, 0.0, j));
        }

        return Math.Log(sum);
    }
}

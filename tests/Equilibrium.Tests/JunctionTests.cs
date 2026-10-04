using APThermo.Equilibrium.TraceGas;

namespace APThermo.Equilibrium.Tests;

/// <summary>
/// The data junction (TraceGas BOOT.md, "The data junction"): an hp or sp target at the temperature where the NASA fits of two ranges meet
/// has no temperature within the jump of the data, and the convergence pinned at the junction ends at the nearer of the two tp states
/// there, the bound T_J and the next double (the owner's decision, 2026-10-04, option (B)).
/// </summary>
[Collection(CpuFixture.CollectionName)]
public sealed class JunctionTests
{
    /// <summary>The modes of the solve at the enthalpy or entropy of the tp state: cold and warm from its composition, hp and sp.</summary>
    private static readonly string[] Modes = ["hp-cold", "sp-cold", "hp-warm", "sp-warm"];

    private static readonly string[] ColdModes = ["hp-cold", "sp-cold"];

    /// <summary>
    /// The largest miss of the target an <c>Ok</c> state may carry, relative to c_p T (hp) or c_p (sp): 12 times the largest jump of a mixture's
    /// h measured at 1 000 K, 8.3e-9 of c_p T (TraceGas BOOT.md, "The data junction").
    /// </summary>
    private const double MissBound = 1.0e-7;

    /// <summary>The temperature of an <c>Ok</c> state against its tp state's, relative: the jump of the data moves a temperature by about 1e-9.</summary>
    private const double TemperatureBound = 1.0e-8;

    /// <summary>The 135 tp states of <see cref="TraceGasCases.JunctionStates"/>.</summary>
    public static TheoryData<string> States() => TraceGasCases.Names(TraceGasCases.JunctionStates());

    /// <summary>
    /// The hp and sp states, cold and warm, at the enthalpy and entropy of each tp state at the junction end <c>Ok</c>, clear of the conditions at
    /// 1e-9 with every gas, at the tp state's temperature within <see cref="TemperatureBound"/>, and within <see cref="MissBound"/> of the
    /// target. Red without the pin: 51 of the 540 solves end <c>NotConverged</c> or <c>SingularMatrix</c> on the close guard's refusal.
    /// </summary>
    [Theory]
    [MemberData(nameof(States))]
    public void AnHpOrSpTargetAtADataJunctionEndsOkAtTheJunction(string name)
    {
        var state = TraceGasCases.Named(name);
        var tp = state.Solve();
        TraceGasChecks.AssertOkAndClear(tp, name + " tp");
        foreach (var mode in Modes)
        {
            var solution = state.SolveInMode(tp, mode);
            var label = $"{mode} {name}";
            TraceGasChecks.AssertOkAndClear(solution, label);
            Assert.True(
                Math.Abs(solution.State.Temperature / tp.State.Temperature - 1.0) <= TemperatureBound,
                $"{label}: T {solution.State.Temperature:R} against {tp.State.Temperature:R}");
            var miss = mode.StartsWith("hp", StringComparison.Ordinal)
                ? Math.Abs(solution.State.Enthalpy - tp.State.Enthalpy) / (tp.State.CpEquilibrium * tp.State.Temperature)
                : Math.Abs(solution.State.Entropy - tp.State.Entropy) / tp.State.CpEquilibrium;
            Assert.True(miss <= MissBound, $"{label}: miss {miss:E3} of c_p");
        }
    }

    /// <summary>
    /// A state pinned at the junction ends at the nearer of the two tp states there, ties to the bound: for each gas system at 100 kPa the hp and
    /// sp states of the tp states at the junction and at the double above it end at the temperature of the tp state, at the bound or the double,
    /// whose enthalpy or entropy is nearer the target. Red when the answer is always the bound, or always the double.
    /// </summary>
    [Fact]
    public void APinnedStateEndsAtTheNearerOfTheBoundAndTheDoubleAboveIt()
    {
        var upper = Math.BitIncrement(TraceGasCases.JunctionTemperature);
        var atBound = 0;
        var atUpper = 0;
        foreach (var state in TraceGasCases.JunctionStates().Where(c => c.Pressure == 1.0e5 && (c.Temperature == TraceGasCases.JunctionTemperature || c.Temperature == upper)))
        {
            var lower = state.At(TraceGasCases.JunctionTemperature).Solve();
            var above = state.At(upper).Solve();
            var tp = state.Solve();
            foreach (var mode in ColdModes)
            {
                var solution = state.SolveInMode(tp, mode);
                if (solution.State.Temperature != TraceGasCases.JunctionTemperature && solution.State.Temperature != upper)
                {
                    continue;
                }

                var hp = mode == "hp-cold";
                var target = hp ? tp.State.Enthalpy : tp.State.Entropy;
                var missLower = Math.Abs((hp ? lower.State.Enthalpy : lower.State.Entropy) - target);
                var missUpper = Math.Abs((hp ? above.State.Enthalpy : above.State.Entropy) - target);
                var expected = missUpper < missLower ? upper : TraceGasCases.JunctionTemperature;
                Assert.True(solution.State.Temperature == expected, $"{mode} {state.Name}: T {solution.State.Temperature:R}, misses {missLower:E3} (bound) and {missUpper:E3} (double)");
                if (expected == upper)
                {
                    atUpper++;
                }
                else
                {
                    atBound++;
                }
            }
        }

        Assert.True(atBound > 0 && atUpper > 0, $"{atBound} states at the bound and {atUpper} at the double above it");
    }

    /// <summary>
    /// KCl + 1e-6 Cl at 100 kPa and 1 000 K, hp cold, warm and warm from 5 K above, the states the junction left <c>NotConverged</c>: the jump of h
    /// of this mixture is at its rounding, both tp states miss the target alike, and the tie goes to the bound: the state ends <c>Ok</c> at 1 000.0
    /// K exactly with its enthalpy within one ulp of the target.
    /// </summary>
    [Theory]
    [InlineData("hp-cold")]
    [InlineData("hp-warm")]
    [InlineData("hp-warm5")]
    public void KclAtTheJunctionEndsAtTheBoundItself(string mode)
    {
        var state = TraceGasCases.LooseOks().First(c => c.Name.StartsWith("loose-kcl|1E-06|100000|", StringComparison.Ordinal));
        var tp = state.Solve();
        var solution = state.SolveInMode(tp, mode);

        TraceGasChecks.AssertOkAndClear(solution, mode + " " + state.Name);
        Assert.Equal(TraceGasCases.JunctionTemperature, solution.State.Temperature);
        Assert.True(
            Math.Abs(solution.State.Enthalpy - tp.State.Enthalpy) <= Math.Abs(Math.BitIncrement(tp.State.Enthalpy) - tp.State.Enthalpy),
            $"{mode}: h {solution.State.Enthalpy:R} against the target {tp.State.Enthalpy:R}");
    }

    /// <summary>
    /// The search for the junction between two temperatures: KCl and its gases cross their bound at 1 000 K, which is the answer between the
    /// double below and the double above it, with the double above it as the upper end; nothing between 999 and 999.5 K; the lowest of the bounds
    /// between 900 and 6 500 K, where the bounds of 1 000 K and 6 000 K are both crossed. Red when the search does not end on adjacent doubles.
    /// </summary>
    [Fact]
    public void TheSearchFindsTheBoundBetweenTwoTemperaturesAndTheLowestOfTwo()
    {
        var table = TraceGasCases.TableOver(["K", "CL"]);
        using var rig = DerivativeRig.Of(table, TraceGasCases.JunctionTemperature, _ => 0.0, []);
        var view = rig.View;
        for (var j = 0; j < table.SpeciesCount; j++)
        {
            SpeciesMarks.Set(rig.Scratch, j, SpeciesMark.Active);
        }

        var below = Math.BitDecrement(Math.BitDecrement(TraceGasCases.JunctionTemperature));
        var above = Math.BitIncrement(Math.BitIncrement(TraceGasCases.JunctionTemperature));
        Assert.Equal(TraceGasCases.JunctionTemperature, DataJunction.Bound(view, rig.Scratch, 0, below, above, out var upper));
        Assert.Equal(Math.BitIncrement(TraceGasCases.JunctionTemperature), upper);
        Assert.Equal(TraceGasCases.JunctionTemperature, DataJunction.Bound(view, rig.Scratch, 0, above, below, out upper));
        Assert.Equal(0.0, DataJunction.Bound(view, rig.Scratch, 0, 999.0, 999.5, out upper));
        Assert.Equal(0.0, upper);
        Assert.Equal(TraceGasCases.JunctionTemperature, DataJunction.Bound(view, rig.Scratch, 0, 900.0, 6500.0, out upper));
    }
}

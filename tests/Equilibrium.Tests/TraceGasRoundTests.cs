using System.Globalization;

namespace APThermo.Equilibrium.Tests;

/// <summary>
/// The second round of the trace-gas pass (TraceGas BOOT.md, "The rounds", 2026-10-05): below the data of NaCl(cr) and C(gr) a mixture of
/// NaCl or of C:O = 2:1 within 1e-6 to 1e-10 of its stoichiometry is a supersaturated gas (Na3CL3, or C3O2 and C5), whose multipliers of
/// ±176 leave ln x of each gas some 1e-13 of rounding. The reduced iteration converges short of the element invariant, and every
/// convergence of the trace-gas pass stagnated at 3e-14 to 7e-14 of b_i, between its own balance test (0.3 of the invariant) and the
/// invariant itself: 738 states of a scan ended <c>NotConverged</c>. In the second round a convergence at its step cap closes within the
/// invariant; the states end <c>Ok</c>, clear, every element within 1e-13 of b_i.
/// </summary>
[Collection(CpuFixture.CollectionName)]
public sealed class TraceGasRoundTests
{
    /// <summary>One state of each (system, excess, pressure) of the 738, the lowest temperature of each: NaCl and C:O = 2:1 at 250 to 273 K.</summary>
    internal static readonly string[] Supersaturated =
    [
        "C:O=2:1|-1E-09|100|261",
        "C:O=2:1|-1E-10|100000|273",
        "C:O=2:1|-1E-10|10000|265.5",
        "C:O=2:1|1E-06|1000000|259",
        "C:O=2:1|1E-09|100|254.5",
        "C:O=2:1|1E-10|100|267.5",
        "NaCl|-1E-06|10000000|254.355",
        "NaCl|-1E-06|1000000|250",
        "NaCl|-1E-06|100000|251.41",
        "NaCl|-1E-06|10000|250.625",
        "NaCl|-1E-06|1000|251.965",
        "NaCl|-1E-06|100|250.455",
        "NaCl|-1E-07|10000000|250.68",
        "NaCl|-1E-07|1000000|250",
        "NaCl|-1E-07|100000|256.9",
        "NaCl|-1E-07|10000|251.915",
        "NaCl|-1E-07|1000|250.54",
        "NaCl|-1E-07|100|250",
        "NaCl|-1E-08|10000000|252.9",
        "NaCl|-1E-08|1000000|252.74",
        "NaCl|-1E-08|100000|252.505",
        "NaCl|-1E-08|10000|251.62",
        "NaCl|-1E-08|1000|250.985",
        "NaCl|-1E-08|100|251.525",
        "NaCl|-1E-09|1000000|254.065",
        "NaCl|-1E-09|100000|250",
        "NaCl|-1E-10|10000000|250",
        "NaCl|-1E-10|1000000|251.57",
        "NaCl|1E-07|100|260",
        "NaCl|1E-10|10000000|252",
        "NaCl|1E-10|10000|252.5",
    ];

    /// <summary>The states, as cases of the 17 systems at their excess of the last element.</summary>
    public static TheoryData<string> States() => TraceGasCases.Names(Supersaturated.Select(Case));

    /// <summary>Each state ends <c>Ok</c> and clear of the equilibrium conditions at 1e-9, every element within the relative invariant.</summary>
    [Theory]
    [MemberData(nameof(States))]
    public void ASupersaturatedGasStagnatingBetweenTheTestAndTheInvariantEndsOkInTheSecondRound(string name) =>
        TraceGasChecks.AssertOkAndClear(TraceGasCases.Named(name).Solve(), name);

    /// <summary>The state named as the scans name it (system|excess|pressure|temperature), built as <see cref="TraceGasCases.Scan"/> builds it.</summary>
    internal static TraceGasCase Case(string name)
    {
        ArgumentNullException.ThrowIfNull(name);
        var parts = name.Split('|');
        var (_, elements, systemRatio) = TraceGasCases.Systems.Single(s => s.Name == parts[0]);
        var ratio = (double[])systemRatio.Clone();
        ratio[^1] *= 1 + double.Parse(parts[1], CultureInfo.InvariantCulture);
        return new TraceGasCase(
            name, elements, ratio, double.Parse(parts[2], CultureInfo.InvariantCulture), double.Parse(parts[3], CultureInfo.InvariantCulture));
    }
}

using APThermo.Harness;

namespace APThermo.Execution.Tests;

/// <summary>
/// CUDA against the CPU accelerator, and a run against a repeat of itself: every field of every result equal, bit for bit
/// (Execution.Tests BOOT.md, 2026-10-05). The two accelerators run one program on one set of IEEE operations: the tree's own
/// <c>Exp</c>, <c>Log</c> and <c>Pow</c> are correctly rounded and the post-link keeps CUDA from fusing a multiplication into an
/// addition, so there is no tolerance left to state. A status, an iteration count, a state, a figure and every amount is compared,
/// and the message of a difference names the case, the field and both values in round-trip form; the first
/// <see cref="Shown"/> are returned, since a defect that moves one value moves thousands.
/// </summary>
internal static class ExactComparison
{
    /// <summary>The most mismatches a comparison returns.</summary>
    public const int Shown = 30;

    /// <summary>Every field of every station of a rocket batch: status, station status, iterations, states, figures, amounts.</summary>
    public static List<string> Rocket(RocketBatchResult expected, RocketBatchResult actual, Func<int, string> caseLabel)
    {
        ArgumentNullException.ThrowIfNull(expected);
        ArgumentNullException.ThrowIfNull(actual);
        ArgumentNullException.ThrowIfNull(caseLabel);
        var mismatches = new List<string>();
        if (expected.Count != actual.Count || expected.StationCount != actual.StationCount || expected.SpeciesCount != actual.SpeciesCount)
        {
            mismatches.Add($"shape: {expected.Count} cases of {expected.StationCount} stations and {expected.SpeciesCount} species, against {actual.Count}, {actual.StationCount}, {actual.SpeciesCount}");
            return mismatches;
        }

        var stationCount = expected.StationCount;
        string Station(int index) => $"{caseLabel(index / stationCount)} station {index % stationCount}";
        Values("status", expected.Status, actual.Status, caseLabel, mismatches);
        Values("station status", expected.StationStatus, actual.StationStatus, Station, mismatches);
        Values("iterations", expected.Iterations, actual.Iterations, Station, mismatches);
        Structs(expected.Stations, actual.Stations, Station, mismatches);
        Structs(expected.Figures, actual.Figures, Station, mismatches);
        Amounts(expected.Moles, actual.Moles, expected.SpeciesCount, Station, mismatches);
        return mismatches;
    }

    /// <summary>Every field of every case of an equilibrium batch: status, iterations, state, amounts.</summary>
    public static List<string> Equilibrium(EquilibriumBatchResult expected, EquilibriumBatchResult actual, Func<int, string> caseLabel)
    {
        ArgumentNullException.ThrowIfNull(expected);
        ArgumentNullException.ThrowIfNull(actual);
        ArgumentNullException.ThrowIfNull(caseLabel);
        var mismatches = new List<string>();
        if (expected.Count != actual.Count || expected.SpeciesCount != actual.SpeciesCount)
        {
            mismatches.Add($"shape: {expected.Count} cases of {expected.SpeciesCount} species, against {actual.Count} of {actual.SpeciesCount}");
            return mismatches;
        }

        Values("status", expected.Status, actual.Status, caseLabel, mismatches);
        Values("iterations", expected.Iterations, actual.Iterations, caseLabel, mismatches);
        Structs(expected.State, actual.State, caseLabel, mismatches);
        Amounts(expected.Moles, actual.Moles, expected.SpeciesCount, caseLabel, mismatches);
        return mismatches;
    }

    /// <summary>Every station of a transport batch: status and figures.</summary>
    public static List<string> Transport(TransportBatchResult expected, TransportBatchResult actual)
    {
        ArgumentNullException.ThrowIfNull(expected);
        ArgumentNullException.ThrowIfNull(actual);
        var mismatches = new List<string>();
        if (expected.Count != actual.Count)
        {
            mismatches.Add($"shape: {expected.Count} stations, against {actual.Count}");
            return mismatches;
        }

        static string Station(int index) => $"station {index}";
        Values("status", expected.Status, actual.Status, Station, mismatches);
        Structs(expected.Figures, actual.Figures, Station, mismatches);
        return mismatches;
    }

    /// <summary>Every entry of a species-function batch: the three functions and the range flag.</summary>
    public static List<string> Functions(SpeciesFunctionBatchResult expected, SpeciesFunctionBatchResult actual, Func<int, string> entryLabel)
    {
        ArgumentNullException.ThrowIfNull(expected);
        ArgumentNullException.ThrowIfNull(actual);
        ArgumentNullException.ThrowIfNull(entryLabel);
        var mismatches = new List<string>();
        if (expected.Count != actual.Count)
        {
            mismatches.Add($"shape: {expected.Count} entries, against {actual.Count}");
            return mismatches;
        }

        Values("in range", expected.InRange, actual.InRange, entryLabel, mismatches);
        Amounts(expected.CpOverR, actual.CpOverR, 1, i => $"{entryLabel(i)} Cp/R", mismatches);
        Amounts(expected.HOverRT, actual.HOverRT, 1, i => $"{entryLabel(i)} H/RT", mismatches);
        Amounts(expected.SOverR, actual.SOverR, 1, i => $"{entryLabel(i)} S/R", mismatches);
        return mismatches;
    }

    private static void Values<T>(string name, T[] expected, T[] actual, Func<int, string> label, List<string> mismatches) where T : notnull
    {
        for (var i = 0; i < expected.Length && mismatches.Count < Shown; i++)
        {
            if (!Equals(expected[i], actual[i]))
            {
                mismatches.Add($"{label(i)}: {name} {expected[i]} on the CPU accelerator, {actual[i]} on the other");
            }
        }
    }

    private static void Structs<T>(T[] expected, T[] actual, Func<int, string> label, List<string> mismatches) where T : struct
    {
        for (var i = 0; i < expected.Length && mismatches.Count < Shown; i++)
        {
            mismatches.AddRange(Bits.Differences(expected[i], actual[i], label(i)));
        }
    }

    /// <summary>Doubles compared by their bits, <paramref name="perRow"/> to a row of the label's index.</summary>
    private static void Amounts(double[] expected, double[] actual, int perRow, Func<int, string> label, List<string> mismatches)
    {
        for (long i = 0; i < expected.LongLength && mismatches.Count < Shown; i++)
        {
            if (!Bits.Same(expected[i], actual[i]))
            {
                mismatches.Add($"{label((int)(i / perRow))} [{i % perRow}]: {expected[i]:R} on the CPU accelerator, {actual[i]:R} on the other");
            }
        }
    }
}

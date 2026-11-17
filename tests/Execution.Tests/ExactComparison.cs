using APThermo.Harness;

namespace APThermo.Execution.Tests;

/// <summary>
/// CUDA against the CPU accelerator, and a run against a repeat of itself: numerical outputs, statuses and iteration counts equal, bit for bit
/// (Execution.Tests BOOT.md, 2026-10-05). The two accelerators run one program on one set of IEEE operations: the tree's own
/// <c>Exp</c>, <c>Log</c> and <c>Pow</c> are correctly rounded and the post-link keeps CUDA from fusing a multiplication into an
/// addition, so there is no tolerance left to state. A status, an iteration count, a state, a figure and every amount is compared,
/// while accelerator identity and timings follow the separate metadata policy. A difference names its array length or
/// the case, the field and both values in round-trip form; the first
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
        var coverage = new ResultCoverage(typeof(RocketBatchResult), nameof(RocketBatchResult.Count),
            nameof(RocketBatchResult.SpeciesCount), nameof(RocketBatchResult.StationCount));
        ResultInventory.VerifyMetadata(expected.Accelerator, expected.Timings);
        ResultInventory.VerifyMetadata(actual.Accelerator, actual.Timings);
        var mismatches = new List<string>();
        if (expected.Count != actual.Count || expected.StationCount != actual.StationCount || expected.SpeciesCount != actual.SpeciesCount)
        {
            mismatches.Add($"shape: {expected.Count} cases of {expected.StationCount} stations and {expected.SpeciesCount} species, against {actual.Count}, {actual.StationCount}, {actual.SpeciesCount}");
            return mismatches;
        }

        var stationCount = expected.StationCount;
        string Station(int index) => $"{caseLabel(index / stationCount)} station {index % stationCount}";
        coverage.Compare(nameof(RocketBatchResult.Status), () => Values(nameof(RocketBatchResult.Status), "status", expected.Status, actual.Status, caseLabel, mismatches));
        coverage.Compare(nameof(RocketBatchResult.StationStatus), () => Values(nameof(RocketBatchResult.StationStatus), "station status", expected.StationStatus, actual.StationStatus, Station, mismatches));
        coverage.Compare(nameof(RocketBatchResult.Iterations), () => Values(nameof(RocketBatchResult.Iterations), "iterations", expected.Iterations, actual.Iterations, Station, mismatches));
        coverage.Compare(nameof(RocketBatchResult.Stations), () => Structs(nameof(RocketBatchResult.Stations), expected.Stations, actual.Stations, Station, mismatches));
        coverage.Compare(nameof(RocketBatchResult.Figures), () => Structs(nameof(RocketBatchResult.Figures), expected.Figures, actual.Figures, Station, mismatches));
        coverage.Compare(nameof(RocketBatchResult.Moles), () => Amounts(nameof(RocketBatchResult.Moles), expected.Moles, actual.Moles, expected.SpeciesCount, Station, mismatches));
        coverage.Verify();
        return mismatches;
    }

    /// <summary>Every field of every case of an equilibrium batch: status, iterations, state, amounts.</summary>
    public static List<string> Equilibrium(EquilibriumBatchResult expected, EquilibriumBatchResult actual, Func<int, string> caseLabel)
    {
        ArgumentNullException.ThrowIfNull(expected);
        ArgumentNullException.ThrowIfNull(actual);
        ArgumentNullException.ThrowIfNull(caseLabel);
        var coverage = new ResultCoverage(typeof(EquilibriumBatchResult), nameof(EquilibriumBatchResult.Count), nameof(EquilibriumBatchResult.SpeciesCount));
        ResultInventory.VerifyMetadata(expected.Accelerator, expected.Timings);
        ResultInventory.VerifyMetadata(actual.Accelerator, actual.Timings);
        var mismatches = new List<string>();
        if (expected.Count != actual.Count || expected.SpeciesCount != actual.SpeciesCount)
        {
            mismatches.Add($"shape: {expected.Count} cases of {expected.SpeciesCount} species, against {actual.Count} of {actual.SpeciesCount}");
            return mismatches;
        }

        coverage.Compare(nameof(EquilibriumBatchResult.Status), () => Values(nameof(EquilibriumBatchResult.Status), "status", expected.Status, actual.Status, caseLabel, mismatches));
        coverage.Compare(nameof(EquilibriumBatchResult.Iterations), () => Values(nameof(EquilibriumBatchResult.Iterations), "iterations", expected.Iterations, actual.Iterations, caseLabel, mismatches));
        coverage.Compare(nameof(EquilibriumBatchResult.State), () => Structs(nameof(EquilibriumBatchResult.State), expected.State, actual.State, caseLabel, mismatches));
        coverage.Compare(nameof(EquilibriumBatchResult.Moles), () => Amounts(nameof(EquilibriumBatchResult.Moles), expected.Moles, actual.Moles, expected.SpeciesCount, caseLabel, mismatches));
        coverage.Verify();
        return mismatches;
    }

    /// <summary>Every station of a transport batch: status and figures.</summary>
    public static List<string> Transport(TransportBatchResult expected, TransportBatchResult actual)
    {
        ArgumentNullException.ThrowIfNull(expected);
        ArgumentNullException.ThrowIfNull(actual);
        var coverage = new ResultCoverage(typeof(TransportBatchResult), nameof(TransportBatchResult.Count));
        ResultInventory.VerifyMetadata(expected.Accelerator, expected.Timings);
        ResultInventory.VerifyMetadata(actual.Accelerator, actual.Timings);
        var mismatches = new List<string>();
        if (expected.Count != actual.Count)
        {
            mismatches.Add($"shape: {expected.Count} stations, against {actual.Count}");
            return mismatches;
        }

        static string Station(int index) => $"station {index}";
        coverage.Compare(nameof(TransportBatchResult.Status), () => Values(nameof(TransportBatchResult.Status), "status", expected.Status, actual.Status, Station, mismatches));
        coverage.Compare(nameof(TransportBatchResult.Figures), () => Structs(nameof(TransportBatchResult.Figures), expected.Figures, actual.Figures, Station, mismatches));
        coverage.Verify();
        return mismatches;
    }

    /// <summary>Every entry of a species-function batch: the three functions and the range flag.</summary>
    public static List<string> Functions(SpeciesFunctionBatchResult expected, SpeciesFunctionBatchResult actual, Func<int, string> entryLabel)
    {
        ArgumentNullException.ThrowIfNull(expected);
        ArgumentNullException.ThrowIfNull(actual);
        ArgumentNullException.ThrowIfNull(entryLabel);
        var coverage = new ResultCoverage(typeof(SpeciesFunctionBatchResult), nameof(SpeciesFunctionBatchResult.Count));
        ResultInventory.VerifyMetadata(expected.Accelerator, expected.Timings);
        ResultInventory.VerifyMetadata(actual.Accelerator, actual.Timings);
        var mismatches = new List<string>();
        if (expected.Count != actual.Count)
        {
            mismatches.Add($"shape: {expected.Count} entries, against {actual.Count}");
            return mismatches;
        }

        coverage.Compare(nameof(SpeciesFunctionBatchResult.InRange), () => Values(nameof(SpeciesFunctionBatchResult.InRange), "in range", expected.InRange, actual.InRange, entryLabel, mismatches));
        coverage.Compare(nameof(SpeciesFunctionBatchResult.CpOverR), () => Amounts(nameof(SpeciesFunctionBatchResult.CpOverR), expected.CpOverR, actual.CpOverR, 1, i => $"{entryLabel(i)} Cp/R", mismatches));
        coverage.Compare(nameof(SpeciesFunctionBatchResult.HOverRT), () => Amounts(nameof(SpeciesFunctionBatchResult.HOverRT), expected.HOverRT, actual.HOverRT, 1, i => $"{entryLabel(i)} H/RT", mismatches));
        coverage.Compare(nameof(SpeciesFunctionBatchResult.SOverR), () => Amounts(nameof(SpeciesFunctionBatchResult.SOverR), expected.SOverR, actual.SOverR, 1, i => $"{entryLabel(i)} S/R", mismatches));
        coverage.Verify();
        return mismatches;
    }

    private static void Values<T>(string arrayName, string name, T[] expected, T[] actual, Func<int, string> label, List<string> mismatches) where T : notnull
    {
        if (!SameLength(arrayName, expected, actual, mismatches)) { return; }

        for (var i = 0; i < expected.Length && mismatches.Count < Shown; i++)
        {
            if (!Equals(expected[i], actual[i]))
            {
                mismatches.Add($"{label(i)}: {name} {expected[i]} on the CPU accelerator, {actual[i]} on the other");
            }
        }
    }

    private static void Structs<T>(string name, T[] expected, T[] actual, Func<int, string> label, List<string> mismatches) where T : struct
    {
        if (!SameLength(name, expected, actual, mismatches)) { return; }

        for (var i = 0; i < expected.Length && mismatches.Count < Shown; i++)
        {
            mismatches.AddRange(Bits.Differences(expected[i], actual[i], label(i)).Take(Shown - mismatches.Count));
        }
    }

    /// <summary>Doubles compared by their bits, <paramref name="perRow"/> to a row of the label's index.</summary>
    private static void Amounts(string name, double[] expected, double[] actual, int perRow, Func<int, string> label, List<string> mismatches)
    {
        if (!SameLength(name, expected, actual, mismatches)) { return; }

        for (long i = 0; i < expected.LongLength && mismatches.Count < Shown; i++)
        {
            if (!Bits.Same(expected[i], actual[i]))
            {
                mismatches.Add($"{label((int)(i / perRow))} [{i % perRow}]: {expected[i]:R} on the CPU accelerator, {actual[i]:R} on the other");
            }
        }
    }

    private static bool SameLength(string name, Array expected, Array actual, List<string> mismatches)
    {
        if (expected.LongLength == actual.LongLength) { return true; }
        if (mismatches.Count < Shown)
        {
            mismatches.Add($"{name} length: expected {expected.LongLength}, actual {actual.LongLength}");
        }

        return false;
    }
}

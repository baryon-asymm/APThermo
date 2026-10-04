using System.Globalization;

namespace APThermo.Execution.Tests;

/// <summary>
/// The share of stations at which the two accelerators stopped after different numbers of Newton steps, over a whole run (Execution.Tests
/// BOOT.md, the tolerance table: "at most one station in a thousand", a bound on the run and not on each family). Every CUDA family adds
/// its stations here; <see cref="CudaTests.TheStepShareOverTheWholeRun"/>, ordered last in its class, holds the run-wide share. A family
/// of one or a few cases may differ in one station, which would be a share of one in one; the per-family check is only the coarse guard
/// of <see cref="Allowed"/>.
/// </summary>
internal sealed class StepShareLedger
{
    private readonly Lock _gate = new();
    private long _different;
    private long _stations;

    /// <summary>The stations added so far at which the accelerators stopped after different numbers of steps.</summary>
    public long Different
    {
        get
        {
            lock (_gate)
            {
                return _different;
            }
        }
    }

    /// <summary>The stations added so far.</summary>
    public long Stations
    {
        get
        {
            lock (_gate)
            {
                return _stations;
            }
        }
    }

    /// <summary>
    /// The most stations of one family that may differ: the table's share of its stations rounded down, and at least one, so that a
    /// family too small to hold a thousandth of a station is not failed by a single flip of the threshold.
    /// </summary>
    public static int Allowed(int stations) => Math.Max(1, (int)Math.Floor(GpuCpuTolerances.DifferentStepShare * stations));

    /// <summary>Adds one family's stations and the ones among them that stopped after different numbers of steps.</summary>
    public void Add(int different, int stations)
    {
        lock (_gate)
        {
            _different += different;
            _stations += stations;
        }
    }

    /// <summary>The message of a family over the coarse guard, or null when it is inside it.</summary>
    public static string? CoarseViolation(int different, int stations) =>
        different <= Allowed(stations)
            ? null
            : string.Create(CultureInfo.InvariantCulture, $"{different} of {stations} stations stopped after different numbers of Newton steps on CUDA and on the CPU accelerator, above the coarse guard of {Allowed(stations)}");

    /// <summary>The message of a run over the table's share, or of a run that added no station at all, or null when the run is inside it.</summary>
    public string? RunViolation()
    {
        var (different, stations) = (Different, Stations);
        return stations == 0
            ? "no family added a station to the step-share ledger: the run-wide check walked an empty set"
            : different <= GpuCpuTolerances.DifferentStepShare * stations
                ? null
                : string.Create(CultureInfo.InvariantCulture, $"{different} of {stations} stations over the whole run stopped after different numbers of Newton steps, above the table's share of {GpuCpuTolerances.DifferentStepShare:E0}");
    }
}

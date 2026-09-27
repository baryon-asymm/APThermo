using APThermo.Data;

namespace APThermo.Thermo.Tests;

/// <summary>
/// L1: <see cref="TableLimits.MaxIntervalsPerSpecies"/> (BOOT.md, Constraints, "the interval limit is at least the
/// largest interval count of a product record of the committed file after the join") is not exceeded by any product
/// name of the committed file, once its records are joined the way <see cref="SpeciesResolution"/> joins them: a
/// gaseous name has one record, and a condensed name's records that share it are concatenated, whether or not they
/// would in fact be allowed to join (this scan counts intervals before the join's formula/molar-mass/enthalpy checks,
/// exactly as <see cref="SpeciesResolution"/> checks the joined count against the limit before those checks run).
/// </summary>
public sealed class IntervalLimitTests
{
    private static readonly CpuFixture Cpu = new();

    /// <summary>One product name and the interval count of its records joined in file order.</summary>
    private readonly record struct JoinedName(string Name, int Intervals);

    /// <summary>
    /// Every product name of the committed file, deduplicated in file order, with the interval count its records
    /// carry once joined (<see cref="SpeciesDatabase.Records"/>, products only, summed).
    /// </summary>
    private static List<JoinedName> ScanJoinedIntervalCounts()
    {
        var database = Cpu.Database;
        var names = new List<JoinedName>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var record in database.Products)
        {
            if (!seen.Add(record.Name))
            {
                continue;
            }

            var intervals = database.Records(record.Name).Where(r => r.Section == SpeciesSection.Products).Sum(r => r.Intervals.Count);
            names.Add(new JoinedName(record.Name, intervals));
        }

        return names;
    }

    /// <summary>
    /// No product name of the committed file, joined, exceeds <see cref="TableLimits.MaxIntervalsPerSpecies"/>.
    /// Fails on an empty scan, so the fact cannot pass by finding nothing. Red at the previous limit of 5:
    /// <c>NaCN(II)</c> has 6 intervals in its one record (BOOT.md's ⚠ of 2026-09-27).
    /// </summary>
    [Fact]
    public void NoProductNameExceedsTheIntervalLimitAfterTheJoin()
    {
        var names = ScanJoinedIntervalCounts();
        Assert.NotEmpty(names);
        var worst = names.MaxBy(n => n.Intervals);
        Assert.True(worst.Intervals <= TableLimits.MaxIntervalsPerSpecies,
                    $"{worst.Name} has {worst.Intervals} intervals after the join, more than the limit of {TableLimits.MaxIntervalsPerSpecies}");
    }

    /// <summary>
    /// <c>NaCN(II)</c> is the only product name whose joined interval count reaches the limit (BOOT.md), from the
    /// list the scan itself generates rather than a typed name.
    /// </summary>
    [Fact]
    public void NaCnTwoIsTheOnlyRecordAtTheLimit()
    {
        var atLimit = ScanJoinedIntervalCounts()
            .Where(n => n.Intervals == TableLimits.MaxIntervalsPerSpecies)
            .Select(n => n.Name)
            .OrderBy(n => n, StringComparer.Ordinal)
            .ToList();
        Assert.Equal(["NaCN(II)"], atLimit);
    }
}

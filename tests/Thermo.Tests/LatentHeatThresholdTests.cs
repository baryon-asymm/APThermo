using APThermo.Data;

namespace APThermo.Thermo.Tests;

/// <summary>
/// L1: the join-and-cut threshold (BOOT.md, Constraints, "the join-and-cut threshold is
/// <c>SpeciesFunctions.LatentHeatThreshold</c> = 5e-3") separates the committed file's fit noise from its real
/// transitions, with a margin of at least a factor 2 on either side. The scan below finds every shared bound of the
/// condensed product records the same way the builder does: inside one record's own intervals, and between two
/// records of one name once they are concatenated in file order (<see cref="SpeciesResolution"/>'s join, not
/// reimplemented here beyond the bound and the jump it compares). Only <see cref="SpeciesFunctions.HOverRT(TemperatureInterval, double)"/>,
/// the node's own function, computes the jump.
/// </summary>
public sealed class LatentHeatThresholdTests
{
    private static readonly CpuFixture Cpu = new();

    /// <summary>One shared bound of one condensed product name: the kelvin value and |ΔH°/RT| across it.</summary>
    internal readonly record struct SharedBound(string Name, double Bound, double Jump);

    /// <summary>
    /// Every shared bound of the committed file's condensed product records, in the database's own name and file
    /// order: consecutive intervals of the name's records (one record's own intervals, then the next record's,
    /// exactly as <see cref="SpeciesResolution"/> concatenates them), where the earlier interval's upper bound
    /// touches the later one's lower bound. Internal so <see cref="RangeQuestionTests"/> reads the same scan instead
    /// of a second implementation of it (2026-09-28).
    /// </summary>
    internal static List<SharedBound> ScanSharedBounds()
    {
        var database = Cpu.Database;
        var bounds = new List<SharedBound>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var record in database.Products)
        {
            if (record.Phase != SpeciesPhase.Condensed || !seen.Add(record.Name))
            {
                continue;
            }

            var intervals = database.Records(record.Name).SelectMany(r => r.Intervals).ToList();
            for (var k = 1; k < intervals.Count; k++)
            {
                var previous = intervals[k - 1];
                var current = intervals[k];
                if (previous.THigh != current.TLow)
                {
                    continue;
                }

                var jump = Math.Abs(SpeciesFunctions.HOverRT(current, previous.THigh) - SpeciesFunctions.HOverRT(previous, previous.THigh));
                bounds.Add(new SharedBound(record.Name, previous.THigh, jump));
            }
        }

        return bounds;
    }

    /// <summary>
    /// No shared bound of the committed file falls within a factor 2 of <see cref="SpeciesFunctions.LatentHeatThreshold"/>
    /// on either side: the threshold sits in a gap between fit noise and real transitions, not on top of either.
    /// Fails on an empty scan, so the fact cannot pass by finding nothing. Red at 1e-3, the BOOT.md's ⚠ of
    /// 2026-09-27: the NaCN(II) bound at 287.7 K and the NaCN(III) bound at 293.15 K both land inside the old
    /// threshold's factor-2 band.
    /// </summary>
    [Fact]
    public void NoSharedBoundFallsWithinAFactorTwoOfTheThreshold()
    {
        var bounds = ScanSharedBounds();
        Assert.NotEmpty(bounds);
        var threshold = SpeciesFunctions.LatentHeatThreshold;
        var low = threshold / 2.0;
        var high = threshold * 2.0;
        var offenders = bounds.Where(b => b.Jump >= low && b.Jump <= high).ToList();
        Assert.True(offenders.Count == 0,
                    "bounds within a factor 2 of the threshold: " +
                    string.Join("; ", offenders.Select(b => $"{b.Name} at {b.Bound} K, |dH/RT| = {b.Jump:R}")));
    }

    /// <summary>
    /// The cut (a bound at or above the threshold) fires on exactly the two names BOOT.md names, from the list the
    /// scan itself generates rather than a typed pair (AGENTS.md §8, a quantifier checked against a generated list).
    /// </summary>
    [Fact]
    public void TheCutFiresOnlyOnAlnAndSnS()
    {
        var threshold = SpeciesFunctions.LatentHeatThreshold;
        var cutNames = ScanSharedBounds()
            .Where(b => b.Jump >= threshold)
            .Select(b => b.Name)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(n => n, StringComparer.Ordinal)
            .ToList();
        Assert.Equal(["ALN(L)", "SnS(cr)"], cutNames);
    }

    /// <summary>
    /// NaCN(II) and NaCN(III) each stay one piece at the new threshold (BOOT.md's ⚠ of 2026-09-27): both are scanned
    /// (each has at least one internal bound) and neither reaches the threshold. Each now also builds alone through
    /// <see cref="SpeciesTable.Build"/> (BOOT.md's ⚠ of 2026-09-28, the guards audit's finding F6): the interval limit
    /// rose to 6 the same day, so NaCN(II)'s six intervals no longer refuse it, and the sodium fixture's candidate
    /// list carries both names at zero moles.
    /// </summary>
    [Fact]
    public void NaCnTwoAndNaCnThreeEachStayOnePiece()
    {
        var bounds = ScanSharedBounds();
        Assert.Contains(bounds, b => b.Name == "NaCN(II)");
        Assert.Contains(bounds, b => b.Name == "NaCN(III)");
        var threshold = SpeciesFunctions.LatentHeatThreshold;
        Assert.DoesNotContain(bounds, b => b.Name == "NaCN(II)" && b.Jump >= threshold);
        Assert.DoesNotContain(bounds, b => b.Name == "NaCN(III)" && b.Jump >= threshold);

        var database = Cpu.Database;
        foreach (var name in new[] { "NaCN(II)", "NaCN(III)" })
        {
            var elements = database[name].Formula.Select(pair => pair.Symbol).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
            var table = SpeciesTable.Build(database, elements, [name]);
            _ = Assert.Single(table.IndicesOf(name));
        }
    }

    /// <summary>
    /// The builder's own cut names equal the threshold scan's list (the guards audit's finding F6, 2026-09-28): a
    /// wrong constant written directly in <see cref="SpeciesResolution"/>'s cut, bypassing
    /// <see cref="SpeciesFunctions.LatentHeatThreshold"/>, would leave every fact above green while the builder
    /// itself cut something else. Every condensed product name is built alone and its piece count asked of the
    /// table, not assumed from the scan. Red with <c>&gt;= 1.0e-3</c> written in <c>SpeciesResolution.Cut</c> in
    /// place of the constant: NaCN(II) and NaCN(III) then cut too, while the scan (reading the unchanged constant)
    /// still expects only ALN(L) and SnS(cr).
    /// </summary>
    [Fact]
    public void TheBuilderCutsExactlyTheNamesTheScanPredicts()
    {
        var database = Cpu.Database;
        var threshold = SpeciesFunctions.LatentHeatThreshold;
        var expected = ScanSharedBounds()
            .Where(b => b.Jump >= threshold)
            .Select(b => b.Name)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(n => n, StringComparer.Ordinal)
            .ToList();

        var actual = new List<string>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var record in database.Products)
        {
            if (record.Phase != SpeciesPhase.Condensed || !seen.Add(record.Name))
            {
                continue;
            }

            var elements = database[record.Name].Formula.Select(pair => pair.Symbol).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
            SpeciesTable table;
            try
            {
                table = SpeciesTable.Build(database, elements, [record.Name]);
            }
            catch (ArgumentException)
            {
                continue; // an interval-limit or non-joining refusal: not a cut, examined by other facts
            }

            if (table.IndicesOf(record.Name).Count > 1)
            {
                actual.Add(record.Name);
            }
        }

        actual.Sort(StringComparer.Ordinal);
        Assert.Equal(expected, actual);
    }
}

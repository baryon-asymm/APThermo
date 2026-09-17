using System.Text;
using APThermo.Data;
using APThermo.Fixtures;

namespace APThermo.Thermo.Tests;

/// <summary>L1: the join-and-cut of condensed product records in the table builder (BOOT.md), on the committed database.</summary>
public sealed class JoinAndCutTests : IClassFixture<CpuFixture>
{
    private readonly CpuFixture _cpu;

    public JoinAndCutTests(CpuFixture cpu) => _cpu = cpu;

    [Fact]
    public void Touching_records_of_one_name_join_into_one_contiguous_species()
    {
        var records = _cpu.Database.Products.Where(r => r.Name == "Cr(cr)").ToList();
        Assert.Equal(2, records.Count);
        var table = SpeciesTable.Build(_cpu.Database, ["CR"], ["Cr(cr)"]);
        Assert.Equal("Cr(cr)", Assert.Single(table.Species));
        Assert.Same(records[0], table.Records[0]);
        Assert.Equal(new[] { 0 }, table.IndicesOf("Cr(cr)"));

        // All intervals of both records, adjacent and in the records' order.
        var expected = records.SelectMany(r => r.Intervals).ToList();
        Assert.Equal(expected.Count, table.Arrays.IntervalCount[0]);
        for (var k = 0; k < expected.Count; k++)
        {
            Assert.Equal(expected[k].TLow, table.Arrays.IntervalBounds[k * 2]);
            Assert.Equal(expected[k].THigh, table.Arrays.IntervalBounds[k * 2 + 1]);
        }

        // The candidacy test the equilibrium node relies on now spans the joined range (BOOT.md).
        using var buffers = _cpu.Upload("Cr(cr)");
        var view = buffers.View;
        var second = records[1].Intervals[0];
        Assert.True(SpeciesFunctions.IsInRange(view, 0, 0.5 * (second.TLow + second.THigh)),
                    "the second record's range belongs to the joined species");
        Assert.False(SpeciesFunctions.IsInRange(view, 0, expected[^1].THigh + 1.0));
    }

    [Fact]
    public void A_real_latent_heat_cuts_a_species_into_adjacent_pieces()
    {
        var record = _cpu.Database["ALN(L)"];
        Assert.Equal(2, record.Intervals.Count);
        var table = SpeciesTable.Build(_cpu.Database, ["AL", "N"], ["ALN(L)"]);
        Assert.Equal(2, table.SpeciesCount);
        Assert.Equal(0, table.GasCount);
        for (var j = 0; j < 2; j++)
        {
            var interval = record.Intervals[j];
            Assert.Equal(FormattableString.Invariant($"ALN(L)[{interval.TLow:0.####}-{interval.THigh:0.####}]"), table.Species[j]);
            Assert.Same(record, table.Records[j]);
            Assert.Equal(1, table.Arrays.IntervalCount[j]);
            Assert.Equal(interval.TLow, table.Arrays.IntervalBounds[table.Arrays.IntervalStart[j] * 2]);
            Assert.Equal(interval.THigh, table.Arrays.IntervalBounds[table.Arrays.IntervalStart[j] * 2 + 1]);
        }

        Assert.Equal(new[] { 0, 1 }, table.IndicesOf("ALN(L)"));
        Assert.Equal(-1, table.IndexOf("ALN(L)"));
        Assert.True(table.IndexOf(table.Species[0]) == 0 && table.IndexOf(table.Species[1]) == 1,
                    "the pieces are addressable by their own names");

        // The cut criterion is visible across the pieces: a real enthalpy jump at the shared bound.
        using var buffers = _cpu.Upload("ALN(L)");
        var view = buffers.View;
        var bound = record.Intervals[0].THigh;
        var jump = Math.Abs(SpeciesFunctions.HOverRT(view, 1, bound) - SpeciesFunctions.HOverRT(view, 0, bound));
        Assert.True(jump >= SpeciesFunctions.LatentHeatThreshold, $"|dH/RT| = {jump} at the cut at {bound} K");
    }

    /// <summary>
    /// F-TD-09: the join compares the formation enthalpy too. No repeated product-name group of the committed file
    /// disagrees in it (confirmed by a scan of data/thermo.inp), so the rule is exercised on a synthetic pair: the
    /// real, touching Cr(cr) records with only the second record's formation enthalpy nudged.
    /// </summary>
    [Fact]
    public void Records_disagreeing_in_formation_enthalpy_are_refused_by_name()
    {
        var lines = File.ReadAllLines(Path.Combine(RepositoryPaths.Data, "thermo.inp"), Encoding.Latin1);
        var start = Array.FindIndex(lines, l => l.StartsWith("Cr(cr)", StringComparison.Ordinal));
        Assert.True(start > 0, "Cr(cr) must be in the committed file");
        // The first record has one interval (name + properties + 3 lines = 5); the second has two (2 + 6 = 8): 13 lines in all.
        var record = lines.Skip(start).Take(13).ToArray();
        Assert.Equal("0.000", record[6][65..].Trim()); // the second record's formation enthalpy, columns 66-80, before the mutation
        record[6] = record[6][..65] + "          1.000"; // still columns 66-80, now a disagreeing value
        var file = new List<string> { "thermo", "    200.00   1000.00   6000.00  20000.   9/8/2021" };
        file.AddRange(record);
        file.Add("END PRODUCTS");
        file.Add("END REACTANTS");
        var database = SpeciesDatabase.Parse(new StringReader(string.Join('\n', file) + "\n"));

        var e = Assert.Throws<ArgumentException>(() => SpeciesTable.Build(database, ["CR"], ["Cr(cr)"]));
        Assert.Contains("Cr(cr)", e.Message, StringComparison.Ordinal);
        Assert.Contains("joined", e.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Records_that_cannot_be_joined_are_refused_by_name()
    {
        // Two copies of the ALN(L) record: same formula and molar mass, but the ranges do not touch (1800 after 6000).
        var lines = File.ReadAllLines(Path.Combine(RepositoryPaths.Data, "thermo.inp"), Encoding.Latin1);
        var start = Array.FindIndex(lines, l => l.StartsWith("ALN(L)", StringComparison.Ordinal));
        Assert.True(start > 0, "ALN(L) must be in the committed file");
        var record = lines.Skip(start).Take(2 + 3 * 2).ToArray();
        var file = new List<string> { "thermo", "    200.00   1000.00   6000.00  20000.   9/8/2021" };
        file.AddRange(record);
        file.AddRange(record);
        file.Add("END PRODUCTS");
        file.Add("END REACTANTS");
        var database = SpeciesDatabase.Parse(new StringReader(string.Join('\n', file) + "\n"));

        var e = Assert.Throws<ArgumentException>(() => SpeciesTable.Build(database, ["AL", "N"], ["ALN(L)"]));
        Assert.Contains("ALN(L)", e.Message, StringComparison.Ordinal);
        Assert.Contains("joined", e.Message, StringComparison.Ordinal);
    }
}

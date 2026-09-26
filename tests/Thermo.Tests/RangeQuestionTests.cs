using APThermo.Data;
using APThermo.Fixtures;

namespace APThermo.Thermo.Tests;

/// <summary>
/// L1: the table's and the kernel functions' answers to a species' temperature range agree with the interval rule
/// <see cref="SpeciesFunctions.IntervalOf"/> and <see cref="SpeciesFunctions.IsInRange"/> already use (BOOT.md,
/// "the table answers the range questions", the architecture review's F-AR-01).
/// </summary>
public sealed class RangeQuestionTests
{
    private static readonly CpuFixture Cpu = new();

    /// <summary>Every thermo fixture species, from the fixtures node, not typed.</summary>
    public static TheoryData<string> ThermoFixtureSpecies()
    {
        var data = new TheoryData<string>();
        foreach (var fixture in CeaFixtures.LoadAll("thermo"))
        {
            data.Add(fixture.Inputs.GetProperty("species").GetString()!);
        }

        return data;
    }

    /// <summary>
    /// Every condensed record name of the committed file that resolves to exactly one table piece, deduplicated,
    /// from a database listing — not the thermo fixture species, so the expectation below is independent of what
    /// this node's own tests happen to cover (2026-09-26). Left out: a name with more intervals than
    /// <see cref="TableLimits.MaxIntervalsPerSpecies"/> (<c>NaCN(II)</c>), same-name records that do not join
    /// (<see cref="SpeciesTable.Build"/> refuses them), and a name the join-and-cut splits into more than one table
    /// piece (a real latent heat between two joined records, or <c>ALN(L)</c>); none of these has one record-wide
    /// bound to compare, and <see cref="PieceOfNamesThePieceTheIntervalRuleChooses"/> covers the cut names.
    /// </summary>
    public static TheoryData<string> CondensedDatabaseRecordNames()
    {
        var database = SpeciesDatabase.Load(Path.Combine(RepositoryPaths.Data, "thermo.inp"));
        var data = new TheoryData<string>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var record in database.Products)
        {
            if (record.Phase != SpeciesPhase.Condensed || !seen.Add(record.Name))
            {
                continue;
            }

            if (SinglePieceTable(database, record.Name) is not null)
            {
                data.Add(record.Name);
            }
        }

        return data;
    }

    /// <summary>The one-species table of <paramref name="name"/>, or null when it cannot be built or resolves to more than one piece.</summary>
    private static SpeciesTable? SinglePieceTable(SpeciesDatabase database, string name)
    {
        var elements = database[name].Formula.Select(pair => pair.Symbol).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        try
        {
            var table = SpeciesTable.Build(database, elements, [name]);
            return table.IndicesOf(name).Count == 1 ? table : null;
        }
        catch (ArgumentException)
        {
            return null;
        }
    }

    /// <summary>The thermo fixture species the committed file's join-and-cut actually splits into more than one piece.</summary>
    public static TheoryData<string> CutFixtureSpecies()
    {
        var database = SpeciesDatabase.Load(Path.Combine(RepositoryPaths.Data, "thermo.inp"));
        var data = new TheoryData<string>();
        foreach (var fixture in CeaFixtures.LoadAll("thermo"))
        {
            var name = fixture.Inputs.GetProperty("species").GetString()!;
            var elements = database[name].Formula.Select(pair => pair.Symbol).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
            if (SpeciesTable.Build(database, elements, [name]).IndicesOf(name).Count > 1)
            {
                data.Add(name);
            }
        }

        return data;
    }

    /// <summary>PieceOf names the piece the interval rule chooses.</summary>
    [Theory]
    [MemberData(nameof(CutFixtureSpecies))]
    public void PieceOfNamesThePieceTheIntervalRuleChooses(string name)
    {
        using var buffers = Cpu.Upload(name);
        var table = buffers.Table;
        var view = buffers.View;
        var indices = table.IndicesOf(name);
        Assert.True(indices.Count > 1, $"{name} is expected to be a cut name (CutFixtureSpecies found it so)");

        for (var k = 0; k < indices.Count; k++)
        {
            var piece = indices[k];
            var high = SpeciesFunctions.RecordHigh(view, piece);
            Assert.Equal(piece, table.PieceOf(name, high));
            Assert.Equal(piece, table.PieceOf(name, Math.BitDecrement(high)));
            var above = k + 1 < indices.Count ? indices[k + 1] : piece; // above the last bound: still the last piece
            Assert.Equal(above, table.PieceOf(name, Math.BitIncrement(high)));
        }

        var lastPiece = indices[^1];
        Assert.Equal(lastPiece, table.PieceOf(name, SpeciesFunctions.RecordHigh(view, lastPiece) * 2.0 + 1.0));
        Assert.Equal(-1, table.PieceOf("NoSuchSpecies", 300.0));
    }

    /// <summary>RecordLow and RecordHigh are the bounds IsInRange uses.</summary>
    [Theory]
    [MemberData(nameof(ThermoFixtureSpecies))]
    public void RecordLowAndRecordHighAreTheBoundsIsInRangeUses(string name)
    {
        using var buffers = Cpu.Upload(name);
        var view = buffers.View;
        foreach (var piece in buffers.Table.IndicesOf(name))
        {
            var low = SpeciesFunctions.RecordLow(view, piece);
            var high = SpeciesFunctions.RecordHigh(view, piece);
            if (low > high)
            {
                // A record whose only interval is inverted with nothing to continue it (Br2(cr), 2026-09-26): its
                // bounds cross and the range is empty everywhere, in the reference too (Thermo BOOT.md).
                Assert.False(SpeciesFunctions.IsInRange(view, piece, low));
                Assert.False(SpeciesFunctions.IsInRange(view, piece, high));
                continue;
            }

            Assert.True(SpeciesFunctions.IsInRange(view, piece, low));
            Assert.True(SpeciesFunctions.IsInRange(view, piece, high));
            Assert.False(SpeciesFunctions.IsInRange(view, piece, Math.BitDecrement(low)));
            Assert.False(SpeciesFunctions.IsInRange(view, piece, Math.BitIncrement(high)));
        }
    }

    /// <summary>
    /// RecordLow and RecordHigh equal the extremes of the record's own bounds as the Data node stores them
    /// (2026-09-26): not tautological like the fact above, since the expectation is built from
    /// <see cref="SpeciesDatabase.Records"/> directly, never through this node's own functions. Red against the
    /// old first-interval/last-interval rule on the nine records of Thermo BOOT.md's finding 1.
    /// </summary>
    [Theory]
    [MemberData(nameof(CondensedDatabaseRecordNames))]
    public void RecordLowAndRecordHighEqualTheDatabaseRecordsOwnBounds(string name)
    {
        var database = SpeciesDatabase.Load(Path.Combine(RepositoryPaths.Data, "thermo.inp"));
        var table = SinglePieceTable(database, name);
        Assert.NotNull(table);
        var piece = table.IndicesOf(name)[0];
        var intervals = database.Records(name).SelectMany(record => record.Intervals).ToList();
        var expectedLow = intervals.Min(interval => interval.TLow);
        var expectedHigh = intervals.Max(interval => interval.THigh);
        using var buffers = SpeciesTableBuffers.Upload(Cpu.Accelerator, table);
        Assert.Equal(expectedLow, SpeciesFunctions.RecordLow(buffers.View, piece));
        Assert.Equal(expectedHigh, SpeciesFunctions.RecordHigh(buffers.View, piece));
    }
}

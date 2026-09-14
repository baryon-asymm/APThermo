using AerospacePropellantThermodynamics.Data;
using AerospacePropellantThermodynamics.Fixtures;

namespace AerospacePropellantThermodynamics.Thermo.Tests;

/// <summary>
/// L1: the table's and the kernel functions' answers to a species' temperature range agree with the interval rule
/// <see cref="SpeciesFunctions.IntervalOf"/> and <see cref="SpeciesFunctions.IsInRange"/> already use (BOOT.md,
/// "the table answers the range questions", the architecture review's F-AR-01).
/// </summary>
public sealed class RangeQuestionTests : IClassFixture<CpuFixture>
{
    private readonly CpuFixture _cpu;

    public RangeQuestionTests(CpuFixture cpu) => _cpu = cpu;

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

    [Theory]
    [MemberData(nameof(CutFixtureSpecies))]
    public void PieceOf_names_the_piece_the_interval_rule_chooses(string name)
    {
        using var buffers = _cpu.Upload(name);
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

    [Theory]
    [MemberData(nameof(ThermoFixtureSpecies))]
    public void RecordLow_and_RecordHigh_are_the_bounds_IsInRange_uses(string name)
    {
        using var buffers = _cpu.Upload(name);
        var view = buffers.View;
        foreach (var piece in buffers.Table.IndicesOf(name))
        {
            var low = SpeciesFunctions.RecordLow(view, piece);
            var high = SpeciesFunctions.RecordHigh(view, piece);
            Assert.True(SpeciesFunctions.IsInRange(view, piece, low));
            Assert.True(SpeciesFunctions.IsInRange(view, piece, high));
            Assert.False(SpeciesFunctions.IsInRange(view, piece, Math.BitDecrement(low)));
            Assert.False(SpeciesFunctions.IsInRange(view, piece, Math.BitIncrement(high)));
        }
    }
}

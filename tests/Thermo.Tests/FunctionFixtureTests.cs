using System.Text.Json;
using AerospacePropellantThermodynamics.Fixtures;

namespace AerospacePropellantThermodynamics.Thermo.Tests;

/// <summary>L0: the species functions against the independently generated `thermo` fixtures, and R against the `constants` fixture.</summary>
public sealed class FunctionFixtureTests : IClassFixture<CpuFixture>
{
    private readonly CpuFixture _cpu;

    public FunctionFixtureTests(CpuFixture cpu) => _cpu = cpu;

    /// <summary>One theory case per fixture file; the list comes from the directory, not from the test.</summary>
    public static TheoryData<string> ThermoFixtures()
    {
        var data = new TheoryData<string>();
        foreach (var path in FixtureFiles.Enumerate("thermo"))
        {
            data.Add(path);
        }

        return data;
    }

    [Theory]
    [MemberData(nameof(ThermoFixtures))]
    public void Functions_equal_the_independent_evaluation(string fixturePath)
    {
        var fixture = CeaFixtures.Load(fixturePath);
        var name = fixture.Inputs.GetProperty("species").GetString()!;
        using var buffers = _cpu.Upload(name);
        var table = buffers.Table;
        var view = buffers.View;
        var tolerance = "thermoFunction";
        var points = 0;
        foreach (var value in fixture.Outputs.GetProperty("values").EnumerateArray())
        {
            var t = value.GetProperty("temperature").GetDouble();

            // The fixture indexes the record: a species cut at a fit discontinuity (the join-and-cut of the node's
            // BOOT.md) is evaluated on the piece owning the temperature, whose interval start restores the record index.
            var species = PieceOf(table, t);
            var offset = table.Arrays.IntervalStart[species];
            AssertClose(tolerance, value.GetProperty("cpOverR").GetDouble(), SpeciesFunctions.CpOverR(view, species, t), $"{name} Cp/R at {t} K");
            AssertClose(tolerance, value.GetProperty("hOverRT").GetDouble(), SpeciesFunctions.HOverRT(view, species, t), $"{name} H/RT at {t} K");
            AssertClose(tolerance, value.GetProperty("sOverR").GetDouble(), SpeciesFunctions.SOverR(view, species, t), $"{name} S/R at {t} K");
            AssertClose(tolerance, value.GetProperty("gOverRT").GetDouble(), SpeciesFunctions.GOverRT(view, species, t), $"{name} G/RT at {t} K");
            Assert.True(value.GetProperty("interval").GetInt32() == offset + SpeciesFunctions.IntervalOf(view, species, t), $"{name}: interval at {t} K");
            Assert.True(value.GetProperty("inRange").GetBoolean() == SpeciesFunctions.IsInRange(view, species, t), $"{name}: range flag at {t} K");
            points++;
        }

        Assert.True(points >= 3, $"{name}: a fixture with fewer than three points proves little");
    }

    [Fact]
    public void Every_fixture_species_has_an_out_of_range_point_on_each_side()
    {
        foreach (var fixture in CeaFixtures.LoadAll("thermo"))
        {
            var flags = fixture.Outputs.GetProperty("values").EnumerateArray().Select(v => v.GetProperty("inRange").GetBoolean()).ToArray();
            Assert.False(flags[0], fixture.Name);
            Assert.False(flags[^1], fixture.Name);
            Assert.Contains(true, flags);
        }
    }

    [Fact]
    public void R_equals_the_reference_package_constant()
    {
        var fixture = Assert.Single(CeaFixtures.LoadAll("constants"));
        var expected = fixture.Outputs.GetProperty("R").GetDouble();
        Assert.True(_cpu.Tolerances.Matches("gasConstant", expected, PhysicalConstants.R), $"R = {PhysicalConstants.R}, reference {expected}");
    }

    /// <summary>
    /// The entry owning the temperature in a one-name table: the first piece whose last bound is not below it, else
    /// the last. <see cref="SpeciesFunctions.IntervalOf"/>'s rule lifted over the pieces of a cut species; a table of
    /// one uncut species has one entry and the answer is 0.
    /// </summary>
    private static int PieceOf(SpeciesTable table, double temperature)
    {
        for (var j = 0; j < table.SpeciesCount - 1; j++)
        {
            var last = table.Arrays.IntervalStart[j] + table.Arrays.IntervalCount[j] - 1;
            if (temperature <= table.Arrays.IntervalBounds[last * 2 + 1])
            {
                return j;
            }
        }

        return table.SpeciesCount - 1;
    }

    private void AssertClose(string field, double expected, double actual, string what)
    {
        Assert.True(_cpu.Tolerances.Matches(field, expected, actual), $"{what}: expected {expected:R}, got {actual:R}");
    }
}

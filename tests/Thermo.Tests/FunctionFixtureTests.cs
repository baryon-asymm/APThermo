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
        var view = buffers.View;
        var tolerance = "thermoFunction";
        var points = 0;
        foreach (var value in fixture.Outputs.GetProperty("values").EnumerateArray())
        {
            var t = value.GetProperty("temperature").GetDouble();
            AssertClose(tolerance, value.GetProperty("cpOverR").GetDouble(), SpeciesFunctions.CpOverR(view, 0, t), $"{name} Cp/R at {t} K");
            AssertClose(tolerance, value.GetProperty("hOverRT").GetDouble(), SpeciesFunctions.HOverRT(view, 0, t), $"{name} H/RT at {t} K");
            AssertClose(tolerance, value.GetProperty("sOverR").GetDouble(), SpeciesFunctions.SOverR(view, 0, t), $"{name} S/R at {t} K");
            AssertClose(tolerance, value.GetProperty("gOverRT").GetDouble(), SpeciesFunctions.GOverRT(view, 0, t), $"{name} G/RT at {t} K");
            Assert.True(value.GetProperty("interval").GetInt32() == SpeciesFunctions.IntervalOf(view, 0, t), $"{name}: interval at {t} K");
            Assert.True(value.GetProperty("inRange").GetBoolean() == SpeciesFunctions.IsInRange(view, 0, t), $"{name}: range flag at {t} K");
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

    private void AssertClose(string field, double expected, double actual, string what)
    {
        Assert.True(_cpu.Tolerances.Matches(field, expected, actual), $"{what}: expected {expected:R}, got {actual:R}");
    }
}

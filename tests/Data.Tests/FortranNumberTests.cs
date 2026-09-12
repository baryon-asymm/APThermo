using AerospacePropellantThermodynamics.Data;

namespace AerospacePropellantThermodynamics.Data.Tests;

/// <summary>L0: the numeric forms present in the NASA files.</summary>
public sealed class FortranNumberTests
{
    [Theory]
    [InlineData("-3.947960830D+04", -3.947960830e4)]
    [InlineData(" 4.955043490D-09", 4.955043490e-9)]
    [InlineData("0.61205763E 00", 0.61205763)]
    [InlineData("-0.67714354E 02", -67.714354)]
    [InlineData(" 0.76608935E+00", 0.76608935)]
    [InlineData("   18.0152800", 18.01528)]
    [InlineData(".000548579903", 5.48579903e-4)]
    [InlineData("1.5-03", 1.5e-3)]
    [InlineData("      ", 0.0)]
    [InlineData("  -2.0", -2.0)]
    public void Parses_every_form_of_the_files(string field, double expected)
    {
        Assert.Equal(expected, FortranNumberAccessor.Parse(field));
    }

    [Fact]
    public void Rejects_text()
    {
        Assert.Throws<FormatException>(() => FortranNumberAccessor.Parse("abc"));
    }
}

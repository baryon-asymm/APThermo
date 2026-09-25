using APThermo.Data;

namespace APThermo.Thermo.Tests;

/// <summary>L1: the table builder against the Data records of the same species.</summary>
public sealed class TableBuilderTests
{
    private static readonly CpuFixture Cpu = new();

    private static readonly string[] Elements = ["H", "O", "C", "N", "AL", "CL"];

    private static readonly string[] Mixed = ["H2O", "AL2O3(L)", "CO2", "C(gr)", "H2", "AL2O3(a)", "N2", "HCL", "AL", "OH"];

    /// <summary>Gaseous species come first and each group keeps its order.</summary>
    [Fact]
    public void GaseousSpeciesComeFirstAndEachGroupKeepsItsOrder()
    {
        var table = SpeciesTable.Build(Cpu.Database, Elements, Mixed);
        var gaseous = Mixed.Where(s => Cpu.Database[s].Phase == SpeciesPhase.Gas).ToArray();
        var condensed = Mixed.Where(s => Cpu.Database[s].Phase == SpeciesPhase.Condensed).ToArray();
        Assert.Equal(gaseous.Concat(condensed), table.Species);
        Assert.Equal(gaseous.Length, table.GasCount);
        Assert.Equal(condensed.Length, table.CondensedCount);
        Assert.Equal(Mixed.Length, table.SpeciesCount);
        Assert.Equal(Elements, table.Elements);
        for (var j = 0; j < table.SpeciesCount; j++)
        {
            Assert.Equal(table.Species[j], table.Records[j].Name);
            Assert.Equal(j, table.IndexOf(table.Species[j]));
        }

        Assert.Equal(-1, table.IndexOf("NoSuchSpecies"));
    }

    /// <summary>The list of checked entries is generated from the table: every element of every species, and every zero.</summary>
    [Fact]
    public void TheStoichiometryMatrixEqualsTheDataFormulas()
    {
        var table = SpeciesTable.Build(Cpu.Database, Elements, Mixed);
        var arrays = table.Arrays;
        var checkedEntries = 0;
        for (var i = 0; i < table.ElementCount; i++)
        {
            for (var j = 0; j < table.SpeciesCount; j++)
            {
                var expected = table.Records[j].Formula
                    .Where(p => string.Equals(p.Symbol, table.Elements[i], StringComparison.OrdinalIgnoreCase))
                    .Sum(p => p.Count);
                Assert.Equal(expected, arrays.Stoichiometry[i * table.SpeciesCount + j]);
                checkedEntries++;
            }
        }

        Assert.Equal(table.ElementCount * table.SpeciesCount, checkedEntries);
        for (var j = 0; j < table.SpeciesCount; j++)
        {
            Assert.Equal(table.Records[j].MolarMass, arrays.MolarMass[j]);
            Assert.Equal(table.Records[j].FormationEnthalpy, arrays.FormationEnthalpy[j]);
            Assert.Equal(table.Records[j].Intervals.Count, arrays.IntervalCount[j]);
        }
    }

    /// <summary>Intervals are flattened in species order with the record values.</summary>
    [Fact]
    public void IntervalsAreFlattenedInSpeciesOrderWithTheRecordValues()
    {
        var table = SpeciesTable.Build(Cpu.Database, Elements, Mixed);
        var arrays = table.Arrays;
        var next = 0;
        for (var j = 0; j < table.SpeciesCount; j++)
        {
            Assert.Equal(next, arrays.IntervalStart[j]);
            foreach (var interval in table.Records[j].Intervals)
            {
                Assert.Equal(interval.TLow, arrays.IntervalBounds[next * 2]);
                Assert.Equal(interval.THigh, arrays.IntervalBounds[next * 2 + 1]);
                Assert.Equal(interval.Exponents, arrays.Exponents.Skip(next * 8).Take(8));
                Assert.Equal(interval.Coefficients, arrays.Coefficients.Skip(next * 9).Take(7));
                Assert.Equal(interval.B1, arrays.Coefficients[next * 9 + 7]);
                Assert.Equal(interval.B2, arrays.Coefficients[next * 9 + 8]);
                next++;
            }
        }

        Assert.Equal(next, arrays.IntervalTotal);
    }

    /// <summary>A species with a foreign element is refused by name.</summary>
    [Fact]
    public void ASpeciesWithAForeignElementIsRefusedByName()
    {
        var e = Assert.Throws<ArgumentException>(() => SpeciesTable.Build(Cpu.Database, ["H", "O"], ["H2O", "CO2"]));
        Assert.Contains("CO2", e.Message, StringComparison.Ordinal);
        Assert.Contains("'C'", e.Message, StringComparison.Ordinal);
    }

    /// <summary>One refusal per case: the input that is wrong, the exception it must raise, and the name the message must carry.</summary>
    public static TheoryData<string[], string[], Type, string> RefusalCases() => new()
    {
        { ["H", "O"], ["H2O", "NoSuchSpecies"], typeof(KeyNotFoundException), "NoSuchSpecies" },
        { ["H", "O"], ["H2O", "H2O"], typeof(ArgumentException), "H2O" },
        { ["H", "O"], ["H2O", "O2(L)"], typeof(ArgumentException), "O2(L)" },
        { ["H", "H"], ["H2"], typeof(ArgumentException), "H" },
    };

    /// <summary>Unknown names duplicates and records without polynomials are refused.</summary>
    [Theory]
    [MemberData(nameof(RefusalCases))]
    public void UnknownNamesDuplicatesAndRecordsWithoutPolynomialsAreRefused(string[] elements, string[] species, Type exceptionType, string refusedName)
    {
        var e = Assert.Throws(exceptionType, () => SpeciesTable.Build(Cpu.Database, elements, species));
        Assert.Contains(refusedName, e.Message, StringComparison.Ordinal);
    }

    /// <summary>One refusal per case: the (elements, species) pair that breaks a limit of <see cref="TableLimits"/>.</summary>
    public static TheoryData<string[], string[]> LimitCases()
    {
        var tooManyElements = Enumerable.Range(0, TableLimits.MaxElements + 1).Select(i => $"E{i}").ToArray();
        var tooManySpecies = Enumerable.Range(0, TableLimits.MaxSpecies + 1).Select(i => $"S{i}").ToArray();
        return new TheoryData<string[], string[]>
        {
            { tooManyElements, ["H2"] },
            { ["H"], tooManySpecies },
            { [], ["H2"] },
            { ["H"], [] },
        };
    }

    /// <summary>The limits are enforced before any lookup.</summary>
    [Theory]
    [MemberData(nameof(LimitCases))]
    public void TheLimitsAreEnforcedBeforeAnyLookup(string[] elements, string[] species) =>
        _ = Assert.Throws<ArgumentException>(() => SpeciesTable.Build(Cpu.Database, elements, species));

    /// <summary>Element symbols are matched case insensitively.</summary>
    [Fact]
    public void ElementSymbolsAreMatchedCaseInsensitively()
    {
        var table = SpeciesTable.Build(Cpu.Database, ["Al", "O"], ["AL2O3(a)", "ALO"]);
        var aluminiumInAl2O3 = AluminiumCount(Cpu.Database["AL2O3(a)"]);
        var aluminiumInAlo = AluminiumCount(Cpu.Database["ALO"]);
        Assert.Equal(aluminiumInAl2O3, table.Arrays.Stoichiometry[0 * table.SpeciesCount + table.IndexOf("AL2O3(a)")]); // the condensed one, second
        Assert.Equal(aluminiumInAlo, table.Arrays.Stoichiometry[0 * table.SpeciesCount + table.IndexOf("ALO")]); // gaseous, first
    }

    private static double AluminiumCount(Species record) =>
        record.Formula.Where(pair => string.Equals(pair.Symbol, "AL", StringComparison.OrdinalIgnoreCase)).Sum(pair => pair.Count);
}

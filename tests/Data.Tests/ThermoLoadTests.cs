using System.Text.RegularExpressions;
using APThermo.Fixtures;

namespace APThermo.Data.Tests;

/// <summary>L1: the committed data/thermo.inp, loaded in full.</summary>
public sealed partial class ThermoLoadTests : IClassFixture<LoadedDatabase>
{
    private readonly LoadedDatabase _loaded;

    public ThermoLoadTests(LoadedDatabase loaded) => _loaded = loaded;

    // A record's second line: two-column interval count, a blank, a six-character date code, a blank, then the formula.
    [GeneratedRegex(@"^[ 0-9]\d [^\n]{6} [A-Za-z ]")]
    private static partial Regex RecordSecondLine();

    [Fact]
    public void Every_record_of_the_file_is_parsed()
    {
        var lines = File.ReadAllLines(_loaded.ThermoPath, System.Text.Encoding.Latin1);
        var endProducts = Array.FindIndex(lines, l => l.StartsWith("END PRODUCTS", StringComparison.Ordinal));
        var endReactants = Array.FindIndex(lines, l => l.StartsWith("END REACTANTS", StringComparison.Ordinal));
        int products = 0, reactants = 0;
        for (var i = 0; i + 1 < lines.Length; i++)
        {
            var isName = lines[i].Length > 0 && lines[i][0] != ' ' && lines[i][0] != '!' && !lines[i].StartsWith("END", StringComparison.Ordinal) && !lines[i].StartsWith("thermo", StringComparison.Ordinal);
            if (isName && RecordSecondLine().IsMatch(lines[i + 1]))
            {
                if (i < endProducts)
                {
                    products++;
                }
                else if (i < endReactants)
                {
                    reactants++;
                }
            }
        }

        Assert.True(products > 1000, "the independent scan found too few product records to be meaningful");
        Assert.Equal(products, _loaded.Database.Products.Count);
        Assert.Equal(reactants, _loaded.Database.Reactants.Count);
    }

    /// <summary>
    /// L1: several records under one name (thermo.inp splits some condensed species into one record per temperature
    /// range) are all reachable through <see cref="SpeciesDatabase.Records"/>, in file order; the indexer and
    /// <see cref="SpeciesDatabase.TryGet"/> keep returning the first. The repeated names come from this test's own
    /// scan of the file text, generated, not typed.
    /// </summary>
    [Fact]
    public void Every_record_of_a_repeated_name_is_returned_in_file_order()
    {
        var lines = File.ReadAllLines(_loaded.ThermoPath, System.Text.Encoding.Latin1);
        var endProducts = Array.FindIndex(lines, l => l.StartsWith("END PRODUCTS", StringComparison.Ordinal));
        var names = new List<string>();
        for (var i = 0; i < endProducts; i++)
        {
            var isName = lines[i].Length > 0 && lines[i][0] != ' ' && lines[i][0] != '!' && !lines[i].StartsWith("END", StringComparison.Ordinal) && !lines[i].StartsWith("thermo", StringComparison.Ordinal);
            if (isName && i + 1 < lines.Length && RecordSecondLine().IsMatch(lines[i + 1]))
            {
                names.Add((lines[i].Length >= 18 ? lines[i][..18] : lines[i]).TrimEnd());
            }
        }

        var repeated = names.GroupBy(n => n, StringComparer.Ordinal).Where(g => g.Count() > 1).Select(g => g.Key).ToArray();
        Assert.Contains("Cr(cr)", repeated);
        Assert.Contains("Fe(a)", repeated);
        Assert.Contains("Cr2O3(I)", repeated);
        Assert.True(repeated.Length >= 3, "the scan found too few repeated names to be meaningful");

        foreach (var name in repeated)
        {
            var expectedRecords = _loaded.Database.Products.Where(p => p.Name == name).ToList();
            var records = _loaded.Database.Records(name);
            Assert.Equal(expectedRecords, records);
            Assert.True(_loaded.Database.TryGet(name, out var first));
            Assert.Same(expectedRecords[0], first);
            Assert.Same(expectedRecords[0], _loaded.Database[name]);
        }
    }

    [Fact]
    public void Header_carries_the_default_interval_bounds()
    {
        var provenance = _loaded.Database.Provenance;
        Assert.Equal([200.0, 1000.0, 6000.0, 20000.0], provenance.DefaultIntervalBounds);
        Assert.False(string.IsNullOrWhiteSpace(provenance.HeaderDate));
        Assert.Equal(64, provenance.ThermoSha256.Length);
        Assert.Equal(64, provenance.TransSha256!.Length);
    }

    [Theory]
    [MemberData(nameof(SpeciesFixtures))]
    public void Fixture_records_parse_to_the_transcribed_values(string fixturePath)
    {
        var expected = Records.LoadSpecies(fixturePath);
        var actual = _loaded.Database[expected.Name];

        Assert.Equal(expected.Comment, actual.Comment);
        Assert.Equal(expected.DateCode, actual.DateCode);
        Assert.Equal(expected.Formula.Select(p => (p.Symbol, p.Count)), actual.Formula.Select(p => (p.Symbol, p.Count)));
        Assert.Equal(expected.Phase, actual.Phase);
        Assert.Equal(expected.MolarMass, actual.MolarMass);
        Assert.Equal(expected.FormationEnthalpy, actual.FormationEnthalpy);
        Assert.Equal(expected.AssignedTemperature, actual.AssignedTemperature);
        Assert.Equal(expected.Section, actual.Section);
        Assert.Equal(expected.IsInert, actual.IsInert);
        Assert.Equal(expected.Intervals.Count, actual.Intervals.Count);
        for (var k = 0; k < expected.Intervals.Count; k++)
        {
            var e = expected.Intervals[k];
            var a = actual.Intervals[k];
            Assert.Equal(e.TLow, a.TLow);
            Assert.Equal(e.THigh, a.THigh);
            Assert.Equal(e.Exponents, a.Exponents);
            Assert.Equal(e.Coefficients, a.Coefficients);
            Assert.Equal(e.B1, a.B1);
            Assert.Equal(e.B2, a.B2);
            Assert.Equal(e.EnthalpyOffset, a.EnthalpyOffset);
        }
    }

    public static TheoryData<string> SpeciesFixtures()
    {
        var data = new TheoryData<string>();
        foreach (var path in Records.SpeciesFixtures())
        {
            data.Add(path);
        }

        return data;
    }

    [Fact]
    public void Interval_anomalies_equal_the_approved_list()
    {
        var anomalies = new List<string>();
        foreach (var species in _loaded.Database.Products.Concat(_loaded.Database.Reactants))
        {
            for (var k = 0; k < species.Intervals.Count; k++)
            {
                var interval = species.Intervals[k];
                if (!(interval.THigh > interval.TLow))
                {
                    anomalies.Add($"{species.Name} [{species.Section}]: interval {k + 1} bounds {interval.TLow}..{interval.THigh} are not ascending");
                }

                if (k + 1 < species.Intervals.Count && interval.THigh != species.Intervals[k + 1].TLow)
                {
                    anomalies.Add($"{species.Name} [{species.Section}]: interval {k + 1} ends at {interval.THigh}, interval {k + 2} starts at {species.Intervals[k + 1].TLow}");
                }
            }
        }

        var approvedPath = Path.Combine(Records.Directory, "interval-anomalies.approved.txt");
        var actualPath = Path.Combine(Records.Directory, "interval-anomalies.actual.txt");
        var actualText = string.Join('\n', anomalies) + "\n";
        if (!File.Exists(approvedPath))
        {
            File.WriteAllText(actualPath, actualText);
            Assert.Fail($"no approved anomaly list; review {actualPath} and rename it to interval-anomalies.approved.txt");
        }

        var approved = File.ReadAllText(approvedPath).Replace("\r\n", "\n");
        if (approved != actualText)
        {
            File.WriteAllText(actualPath, actualText);
        }

        Assert.Equal(approved, actualText);
    }

    [Fact]
    public void Atomic_weights_come_from_the_monatomic_species()
    {
        var database = _loaded.Database;
        Assert.Equal(database["AL"].MolarMass, database.AtomicWeight("AL"));
        Assert.Equal(database["AL"].MolarMass, database.AtomicWeight("Al"));
        Assert.Equal(database["H"].MolarMass, database.AtomicWeight("H"));
        Assert.Equal(database["CL"].MolarMass, database.AtomicWeight("CL"));
        Assert.Throws<KeyNotFoundException>(() => database.AtomicWeight("Xx"));
    }

    [Fact]
    public void Unknown_names_are_reported_by_name()
    {
        var e = Assert.Throws<KeyNotFoundException>(() => _loaded.Database["NoSuchSpecies"]);
        Assert.Contains("NoSuchSpecies", e.Message, StringComparison.Ordinal);
        Assert.False(_loaded.Database.TryGet("NoSuchSpecies", out _));
        Assert.Empty(_loaded.Database.Records("NoSuchSpecies"));
    }
}

/// <summary>Loads the committed data once per test class.</summary>
public sealed class LoadedDatabase
{
    public LoadedDatabase()
    {
        ThermoPath = Path.Combine(RepositoryPaths.Data, "thermo.inp");
        TransPath = Path.Combine(RepositoryPaths.Data, "trans.inp");
        Database = SpeciesDatabase.Load(ThermoPath, TransPath);
    }

    public string ThermoPath { get; }

    public string TransPath { get; }

    public SpeciesDatabase Database { get; }
}

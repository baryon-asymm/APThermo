using System.Text.RegularExpressions;

namespace APThermo.Data.Tests;

/// <summary>L1: the committed data/trans.inp, loaded in full.</summary>
public sealed partial class TransLoadTests : IClassFixture<LoadedDatabase>
{
    private readonly LoadedDatabase _loaded;

    public TransLoadTests(LoadedDatabase loaded) => _loaded = loaded;

    [GeneratedRegex(@"V(\d)C(\d)")]
    private static partial Regex FitCode();

    [Fact]
    public void Every_block_of_the_file_is_parsed()
    {
        var lines = File.ReadAllLines(_loaded.TransPath, System.Text.Encoding.Latin1);
        int singles = 0, pairs = 0, fits = 0;
        foreach (var line in lines.Skip(1))
        {
            if (line.Length == 0 || line[0] == ' ' || line.StartsWith("end", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var match = FitCode().Match(line);
            Assert.True(match.Success, $"header without a fit code: {line}");
            var names = line[..match.Index].Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (names.Length == 1)
            {
                singles++;
            }
            else
            {
                pairs++;
            }

            fits += int.Parse(match.Groups[1].Value) + int.Parse(match.Groups[2].Value);
        }

        var transport = _loaded.Database.Transport!;
        Assert.True(singles > 30, "the independent scan found too few blocks to be meaningful");
        Assert.Equal(singles, transport.Entries.Count(e => e.Partner is null));
        Assert.Equal(pairs, transport.Entries.Count(e => e.Partner is not null));
        Assert.Equal(fits, transport.Entries.Sum(e => e.Viscosity.Count + e.Conductivity.Count));
    }

    [Theory]
    [MemberData(nameof(TransportFixtures))]
    public void Fixture_blocks_parse_to_the_transcribed_values(string fixturePath)
    {
        var expected = Records.LoadTransport(fixturePath);
        var transport = _loaded.Database.Transport!;
        var actual = expected.Partner is null ? transport.Find(expected.Species) : transport.FindPair(expected.Partner, expected.Species);
        Assert.NotNull(actual);
        Assert.Equal(expected.Reference, actual.Reference);
        AssertFits(expected.Viscosity, actual.Viscosity);
        AssertFits(expected.Conductivity, actual.Conductivity);
    }

    private static void AssertFits(List<Records.FitRecord> expected, IReadOnlyList<TransportFit> actual)
    {
        Assert.Equal(expected.Count, actual.Count);
        for (var k = 0; k < expected.Count; k++)
        {
            Assert.Equal((expected[k].TLow, expected[k].THigh, expected[k].A, expected[k].B, expected[k].C, expected[k].D),
                         (actual[k].TLow, actual[k].THigh, actual[k].A, actual[k].B, actual[k].C, actual[k].D));
        }
    }

    public static TheoryData<string> TransportFixtures()
    {
        var data = new TheoryData<string>();
        foreach (var path in Records.TransportFixtures())
        {
            data.Add(path);
        }

        return data;
    }

    [Fact]
    public void Pairs_are_found_in_either_order()
    {
        var transport = _loaded.Database.Transport!;
        Assert.Same(transport.FindPair("CO", "CO2"), transport.FindPair("CO2", "CO"));
        Assert.Null(transport.FindPair("CO", "NoSuchSpecies"));
        Assert.Null(transport.Find("NoSuchSpecies"));
    }
}

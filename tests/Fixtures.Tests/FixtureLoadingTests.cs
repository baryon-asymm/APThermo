using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace APThermo.Fixtures.Tests;

/// <summary>L1: every committed fixture loads, names its kind, and is tied to the committed data files and the pinned package.</summary>
public sealed partial class FixtureLoadingTests
{
    [GeneratedRegex(@"^cea==(\S+)\s*$", RegexOptions.Multiline)]
    private static partial Regex PinnedPackage();

    /// <summary>The kinds are the directories under cases/, produced by a listing, not typed.</summary>
    public static TheoryData<string> Kinds()
    {
        var data = new TheoryData<string>();
        foreach (var directory in Directory.GetDirectories(FixtureFiles.Root).OrderBy(d => d, StringComparer.Ordinal))
        {
            data.Add(Path.GetFileName(directory));
        }

        return data;
    }

    private static IEnumerable<CeaCase> AllCases() =>
        Directory.GetDirectories(FixtureFiles.Root)
            .OrderBy(d => d, StringComparer.Ordinal)
            .SelectMany(d => CeaFixtures.LoadAll(Path.GetFileName(d)));

    [Theory]
    [MemberData(nameof(Kinds))]
    public void Every_fixture_of_a_kind_loads(string kind)
    {
        var cases = CeaFixtures.LoadAll(kind);
        Assert.NotEmpty(cases);
        foreach (var c in cases)
        {
            Assert.Equal(kind, c.Kind);
            Assert.False(string.IsNullOrWhiteSpace(c.Name), c.Path);
            Assert.Equal(JsonValueKind.Object, c.Inputs.ValueKind);
            Assert.Equal(JsonValueKind.Object, c.Outputs.ValueKind);
            Assert.True(c.Generator.GeneratedOn <= DateOnly.FromDateTime(DateTime.Today), c.Path);
        }
    }

    [Fact]
    public void The_kinds_present_are_those_of_the_case_matrix()
    {
        var present = Directory.GetDirectories(FixtureFiles.Root).Select(Path.GetFileName).OrderBy(k => k, StringComparer.Ordinal);
        Assert.Equal(["constants", "hp", "rocket", "sp", "thermo", "tp", "transport"], present);
    }

    [Fact]
    public void Every_fixture_is_tied_to_the_committed_data_files()
    {
        var thermo = Sha256(Path.Combine(RepositoryPaths.Data, "thermo.inp"));
        var trans = Sha256(Path.Combine(RepositoryPaths.Data, "trans.inp"));
        foreach (var c in AllCases())
        {
            Assert.True(thermo == c.Generator.DataThermoSha256, $"{c.Path}: generated from another thermo.inp");
            Assert.True(trans == c.Generator.DataTransSha256, $"{c.Path}: generated from another trans.inp");
        }
    }

    [Fact]
    public void Every_fixture_names_the_pinned_package()
    {
        var requirements = File.ReadAllText(RepositoryPaths.Resolve("tests", "Fixtures", "generate", "requirements.txt"));
        var pinned = PinnedPackage().Match(requirements);
        Assert.True(pinned.Success, "requirements.txt pins cea==<version>");
        foreach (var c in AllCases())
        {
            Assert.Equal("cea", c.Generator.Package);
            Assert.True(pinned.Groups[1].Value == c.Generator.Version, $"{c.Path}: package version {c.Generator.Version}");
            Assert.True(pinned.Groups[1].Value == c.Generator.LibraryVersion, $"{c.Path}: library version {c.Generator.LibraryVersion}");
            Assert.Equal(64, c.Generator.ThermoLibSha256.Length);
            Assert.Equal(64, c.Generator.TransLibSha256.Length);
            Assert.Equal(64, c.Generator.ScriptSha256.Length);
        }
    }

    [Fact]
    public void Every_fixture_names_the_script_that_wrote_it()
    {
        var scripts = Directory.GetFiles(RepositoryPaths.Resolve("tests", "Fixtures", "generate"), "*.py").Select(Path.GetFileName).ToHashSet(StringComparer.Ordinal);
        foreach (var c in AllCases())
        {
            Assert.True(scripts.Contains(c.Generator.Script), $"{c.Path}: unknown script {c.Generator.Script}");
            Assert.Contains(c.Generator.Method, new[] { "cea-package", "independent-evaluation" });
        }
    }

    private static string Sha256(string path)
    {
        using var stream = File.OpenRead(path);
        return Convert.ToHexStringLower(SHA256.HashData(stream));
    }
}

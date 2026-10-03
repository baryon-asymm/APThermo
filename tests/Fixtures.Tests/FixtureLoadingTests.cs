using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace APThermo.Fixtures.Tests;

/// <summary>L1: every committed fixture loads, names its kind, and is tied to the committed data files and the pinned package.</summary>
public sealed partial class FixtureLoadingTests
{
    private static readonly string[] KnownMethods = ["cea-package", "independent-evaluation", "cea-package-mass-flux-scan"];

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

    /// <summary>Every fixture of a kind loads.</summary>
    [Theory]
    [MemberData(nameof(Kinds))]
    public void EveryFixtureOfAKindLoads(string kind)
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

    /// <summary>
    /// The kinds present are those of the case matrix.
    /// </summary>
    /// <remarks>
    /// 2026-09-28: gains <c>reactant</c> (the second hidden-defect audit's observation 6, Problems BOOT.md): a
    /// reactant-level fact (<c>Br2(cr)</c>'s own enthalpy at 298.15 K) that cannot be an <c>hp</c>/<c>tp</c>/<c>sp</c>/
    /// <c>rocket</c> case, since the package does not converge an equilibrium or a rocket solve for that reactant
    /// alone (generate/BOOT.md records the reason). This is the one hand-typed list AGENTS.md §6 allows for a
    /// quantifier of "all": it is the machine-generated list's own witness that nothing was added silently, so it
    /// is corrected by hand whenever a kind is deliberately added, never by a generator run.
    /// 2026-10-03: gains <c>seeded</c> (the StateRecord node's pinned set, Fixtures BOOT.md).
    /// </remarks>
    [Fact]
    public void TheKindsPresentAreThoseOfTheCaseMatrix()
    {
        var present = Directory.GetDirectories(FixtureFiles.Root).Select(Path.GetFileName).OrderBy(k => k, StringComparer.Ordinal);
        Assert.Equal(["constants", "hp", "reactant", "rocket", "seeded", "sp", "thermo", "throat", "tp", "transport"], present);
    }

    /// <summary>Every fixture is tied to the committed data files.</summary>
    [Fact]
    public void EveryFixtureIsTiedToTheCommittedDataFiles()
    {
        var thermo = Sha256(Path.Combine(RepositoryPaths.Data, "thermo.inp"));
        var trans = Sha256(Path.Combine(RepositoryPaths.Data, "trans.inp"));
        foreach (var c in AllCases())
        {
            Assert.True(thermo == c.Generator.DataThermoSha256, $"{c.Path}: generated from another thermo.inp");
            Assert.True(trans == c.Generator.DataTransSha256, $"{c.Path}: generated from another trans.inp");
        }
    }

    /// <summary>Every fixture names the pinned package.</summary>
    [Fact]
    public void EveryFixtureNamesThePinnedPackage()
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

    /// <summary>Every fixture names the script that wrote it.</summary>
    [Fact]
    public void EveryFixtureNamesTheScriptThatWroteIt()
    {
        var scripts = Directory.GetFiles(RepositoryPaths.Resolve("tests", "Fixtures", "generate"), "*.py").Select(Path.GetFileName).ToHashSet(StringComparer.Ordinal);
        foreach (var c in AllCases())
        {
            Assert.True(scripts.Contains(c.Generator.Script), $"{c.Path}: unknown script {c.Generator.Script}");
            Assert.Contains(c.Generator.Method, KnownMethods);
        }
    }

    /// <summary>
    /// Every fixture's <c>scriptSha256</c> equals the hash of its named script as committed (the guards audit's
    /// F7): the SHA-256 of the script's bytes with CRLF normalized to LF, the same normalization the generator
    /// uses (generate/BOOT.md), so a Windows and a Linux checkout agree.
    /// </summary>
    [Fact]
    public void EveryFixturesScriptSha256MatchesItsCommittedScript()
    {
        var generateDirectory = RepositoryPaths.Resolve("tests", "Fixtures", "generate");
        var hashes = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var c in AllCases())
        {
            if (!hashes.TryGetValue(c.Generator.Script, out var expected))
            {
                expected = Sha256Normalized(Path.Combine(generateDirectory, c.Generator.Script));
                hashes[c.Generator.Script] = expected;
            }

            Assert.True(expected == c.Generator.ScriptSha256, $"{c.Path}: scriptSha256 does not match the committed {c.Generator.Script}");
        }
    }

    /// <summary>
    /// Every fixture's <c>generatorSha256</c> equals the hash the test computes over <c>generate/</c> by the rule
    /// of generate/BOOT.md (the guards audit's F7): the concatenation, over every <c>*.py</c> file of that
    /// directory and <c>requirements.txt</c> in ordinal order of their names, of the file name, a LF, and the
    /// file's bytes with CRLF normalized to LF.
    /// </summary>
    [Fact]
    public void EveryFixturesGeneratorSha256MatchesTheCommittedGenerator()
    {
        var expected = GeneratorSha256(RepositoryPaths.Resolve("tests", "Fixtures", "generate"));
        var cases = AllCases().ToList();
        Assert.NotEmpty(cases);
        foreach (var c in cases)
        {
            Assert.True(expected == c.Generator.GeneratorSha256, $"{c.Path}: generatorSha256 does not match the committed generator");
        }
    }

    /// <summary>All fixtures carry one <c>thermoLibSha256</c> and one <c>transLibSha256</c>: the pinned package
    /// ships one pair of library files, and every fixture was generated with the same one.</summary>
    [Fact]
    public void AllFixturesCarryOneThermoLibSha256AndOneTransLibSha256()
    {
        var cases = AllCases().ToList();
        Assert.NotEmpty(cases);
        _ = Assert.Single(cases.Select(c => c.Generator.ThermoLibSha256).Distinct(StringComparer.Ordinal));
        _ = Assert.Single(cases.Select(c => c.Generator.TransLibSha256).Distinct(StringComparer.Ordinal));
    }

    private static string GeneratorSha256(string generateDirectory)
    {
        var names = Directory.GetFiles(generateDirectory)
            .Select(Path.GetFileName)
            .Where(n => n is not null && (n.EndsWith(".py", StringComparison.Ordinal) || n == "requirements.txt"))
            .OrderBy(n => n, StringComparer.Ordinal);
        using var hasher = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        foreach (var name in names)
        {
            hasher.AppendData(Encoding.UTF8.GetBytes(name!));
            hasher.AppendData([(byte)'\n']);
            hasher.AppendData(NormalizeCrLf(File.ReadAllBytes(Path.Combine(generateDirectory, name!))));
        }

        return Convert.ToHexStringLower(hasher.GetHashAndReset());
    }

    private static string Sha256Normalized(string path) =>
        Convert.ToHexStringLower(SHA256.HashData(NormalizeCrLf(File.ReadAllBytes(path))));

    /// <summary>CRLF sequences replaced by LF, mirroring the generator's own normalization (generate/BOOT.md).</summary>
    private static byte[] NormalizeCrLf(byte[] bytes)
    {
        using var output = new MemoryStream(bytes.Length);
        for (var i = 0; i < bytes.Length; i++)
        {
            if (bytes[i] == (byte)'\r' && i + 1 < bytes.Length && bytes[i + 1] == (byte)'\n')
            {
                continue;
            }

            output.WriteByte(bytes[i]);
        }

        return output.ToArray();
    }

    private static string Sha256(string path)
    {
        using var stream = File.OpenRead(path);
        return Convert.ToHexStringLower(SHA256.HashData(stream));
    }
}

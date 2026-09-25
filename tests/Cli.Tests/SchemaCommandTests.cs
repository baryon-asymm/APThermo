using System.Text;
using APThermo.Fixtures;

namespace APThermo.Cli.Tests;

/// <summary>
/// L0: `apthermo schema` delivers exactly the bytes of the embedded file it names, to standard output and to
/// `--output`; the embedded names (`SchemaResources.Names`) equal a directory listing of `src/Cli/Schemas/`, never a
/// typed list; a missing or an unknown name is exit code 2, no document, every embedded name in the message.
/// </summary>
[Collection(CliCollectionDefinition.Name)]
public sealed class SchemaCommandTests(CliFixture fixture)
{
    private const string Extension = ".schema.json";

    private static string SchemasDirectory => RepositoryPaths.Resolve("src", "Cli", "Schemas");

    /// <summary>The schema names of a directory listing, never typed (AGENTS.md §6): whatever `Schemas/` holds today.</summary>
    private static IReadOnlyList<string> SchemaFileNames() =>
        [.. Directory.GetFiles(SchemasDirectory, "*" + Extension).Select(p => Path.GetFileName(p)[..^Extension.Length]).Order(StringComparer.Ordinal)];

    /// <summary>Schema names.</summary>
    public static TheoryData<string> SchemaNames() => [.. SchemaFileNames()];

    /// <summary>Standard output is exactly the file bytes.</summary>
    [Theory]
    [MemberData(nameof(SchemaNames))]
    public void StandardOutputIsExactlyTheFileBytes(string name)
    {
        var expected = File.ReadAllBytes(Path.Combine(SchemasDirectory, name + Extension));
        var run = CliFixture.Invoke("schema", name);
        Assert.Equal(0, run.Code);
        Assert.Empty(run.Error);
        Assert.Equal(expected, Encoding.UTF8.GetBytes(run.Output));
    }

    /// <summary>The output option writes exactly the file bytes.</summary>
    [Theory]
    [MemberData(nameof(SchemaNames))]
    public void TheOutputOptionWritesExactlyTheFileBytes(string name)
    {
        var expected = File.ReadAllBytes(Path.Combine(SchemasDirectory, name + Extension));
        var target = fixture.TempFile(name + Extension);
        var run = CliFixture.Invoke("schema", name, "--output", target);
        Assert.Equal(0, run.Code);
        Assert.Empty(run.Output);
        Assert.Empty(run.Error);
        Assert.Equal(expected, File.ReadAllBytes(target));
    }

    /// <summary>The embedded names equal the directory listing.</summary>
    [Fact]
    public void TheEmbeddedNamesEqualTheDirectoryListing() => Assert.Equal(SchemaFileNames(), SchemaResources.Names);

    /// <summary>A missing name is exit 2 listing every embedded name.</summary>
    [Fact]
    public void AMissingNameIsExit2ListingEveryEmbeddedName()
    {
        var run = CliFixture.Invoke("schema");
        Assert.Equal(2, run.Code);
        Assert.Empty(run.Output);
        foreach (var name in SchemaResources.Names)
        {
            Assert.Contains(name, run.Error);
        }
    }

    /// <summary>An unknown name is exit 2 listing every embedded name.</summary>
    [Fact]
    public void AnUnknownNameIsExit2ListingEveryEmbeddedName()
    {
        var run = CliFixture.Invoke("schema", "bogus");
        Assert.Equal(2, run.Code);
        Assert.Empty(run.Output);
        foreach (var name in SchemaResources.Names)
        {
            Assert.Contains(name, run.Error);
        }
    }
}

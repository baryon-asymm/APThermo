using System.Text;
using APThermo.Fixtures;

namespace APThermo.Cli.Tests;

/// <summary>
/// L0: `apthermo schema` delivers exactly the bytes of the embedded file it names, to standard output and to
/// `--output`; the embedded names (`SchemaResources.Names`) equal a directory listing of `src/Cli/Schemas/`, never a
/// typed list; a missing or an unknown name is exit code 2, no document, every embedded name in the message.
/// </summary>
[Collection(CliCollection.Name)]
public sealed class SchemaCommandTests(CliFixture fixture)
{
    private const string Extension = ".schema.json";

    private static string SchemasDirectory => RepositoryPaths.Resolve("src", "Cli", "Schemas");

    /// <summary>The schema names of a directory listing, never typed (AGENTS.md §6): whatever `Schemas/` holds today.</summary>
    private static IReadOnlyList<string> SchemaFileNames() =>
        Directory.GetFiles(SchemasDirectory, "*" + Extension).Select(p => Path.GetFileName(p)[..^Extension.Length]).Order(StringComparer.Ordinal).ToList();

    public static IEnumerable<object[]> SchemaNames() => SchemaFileNames().Select(name => new object[] { name });

    [Theory]
    [MemberData(nameof(SchemaNames))]
    public void Standard_output_is_exactly_the_file_bytes(string name)
    {
        var expected = File.ReadAllBytes(Path.Combine(SchemasDirectory, name + Extension));
        var run = fixture.Invoke("schema", name);
        Assert.Equal(0, run.Code);
        Assert.Empty(run.Error);
        Assert.Equal(expected, Encoding.UTF8.GetBytes(run.Output));
    }

    [Theory]
    [MemberData(nameof(SchemaNames))]
    public void The_output_option_writes_exactly_the_file_bytes(string name)
    {
        var expected = File.ReadAllBytes(Path.Combine(SchemasDirectory, name + Extension));
        var target = fixture.TempFile(name + Extension);
        var run = fixture.Invoke("schema", name, "--output", target);
        Assert.Equal(0, run.Code);
        Assert.Empty(run.Output);
        Assert.Empty(run.Error);
        Assert.Equal(expected, File.ReadAllBytes(target));
    }

    [Fact]
    public void The_embedded_names_equal_the_directory_listing()
    {
        Assert.Equal(SchemaFileNames(), SchemaResources.Names);
    }

    [Fact]
    public void A_missing_name_is_exit_2_listing_every_embedded_name()
    {
        var run = fixture.Invoke("schema");
        Assert.Equal(2, run.Code);
        Assert.Empty(run.Output);
        foreach (var name in SchemaResources.Names)
        {
            Assert.Contains(name, run.Error);
        }
    }

    [Fact]
    public void An_unknown_name_is_exit_2_listing_every_embedded_name()
    {
        var run = fixture.Invoke("schema", "bogus");
        Assert.Equal(2, run.Code);
        Assert.Empty(run.Output);
        foreach (var name in SchemaResources.Names)
        {
            Assert.Contains(name, run.Error);
        }
    }
}

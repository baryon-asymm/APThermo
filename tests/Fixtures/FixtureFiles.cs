namespace APThermo.Fixtures;

/// <summary>Where the reference cases live and how they are enumerated.</summary>
public static class FixtureFiles
{
    /// <summary><c>tests/Fixtures/cases</c> under the repository root.</summary>
    public static string Root => RepositoryPaths.Resolve("tests", "Fixtures", "cases");

    /// <summary>The fixture files of one kind (a subdirectory of <see cref="Root"/>), sorted by name.</summary>
    public static IReadOnlyList<string> Enumerate(string kind)
    {
        var directory = Path.Combine(Root, kind);
        if (!Directory.Exists(directory))
        {
            throw new DirectoryNotFoundException($"no fixtures of kind '{kind}' under {Root}");
        }

        return Directory.GetFiles(directory, "*.json").OrderBy(p => p, StringComparer.Ordinal).ToArray();
    }
}

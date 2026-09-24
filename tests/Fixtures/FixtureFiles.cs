namespace APThermo.Fixtures;

/// <summary>Where the reference cases live and how they are enumerated.</summary>
public static class FixtureFiles
{
    /// <summary><c>tests/Fixtures/cases</c> under the repository root.</summary>
    public static string Root => RepositoryPaths.Resolve("tests", "Fixtures", "cases");

    /// <summary>The fixture files of one kind (a subdirectory of <see cref="Root"/>), sorted by name.</summary>
    /// <param name="kind">The fixture kind, a subdirectory name under <see cref="Root"/>.</param>
    /// <returns>The full paths of every <c>*.json</c> file of that kind, sorted ordinally.</returns>
    public static IReadOnlyList<string> Enumerate(string kind)
    {
        var directory = Path.Combine(Root, kind);
        return Directory.Exists(directory)
            ? [.. Directory.GetFiles(directory, "*.json").OrderBy(p => p, StringComparer.Ordinal)]
            : throw new DirectoryNotFoundException($"no fixtures of kind '{kind}' under {Root}");
    }
}

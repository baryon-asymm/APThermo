using System.Runtime.CompilerServices;

namespace APThermo.Fixtures;

/// <summary>
/// The repository root, found from this source file (AGENTS.md §13: from the source, never from the binary),
/// so that data and fixture paths do not depend on where the build output lands.
/// </summary>
public static class RepositoryPaths
{
    private static readonly Lazy<string> RootLazy = new(FindRoot);

    /// <summary>The directory holding <c>AGENTS.md</c>.</summary>
    public static string Root => RootLazy.Value;

    /// <summary>The directory of the committed NASA data files.</summary>
    public static string Data => Path.Combine(Root, "data");

    /// <summary>Resolves a path relative to the repository root.</summary>
    public static string Resolve(params string[] segments) => Path.Combine([Root, .. segments]);

    private static string FindRoot()
    {
        var directory = Path.GetDirectoryName(ThisFile());
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory, "AGENTS.md")))
            {
                return directory;
            }

            directory = Path.GetDirectoryName(directory);
        }

        throw new InvalidOperationException("the repository root (a directory with AGENTS.md) was not found above " + ThisFile());
    }

    private static string ThisFile([CallerFilePath] string path = "") => path;
}

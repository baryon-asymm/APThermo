using System.Reflection;
using System.Reflection.Metadata;

namespace APThermo.Protocol.Tests;

/// <summary>
/// The C# source files an assembly was actually compiled from, read from its own portable PDB's document table (the guards
/// audit's F5): a directory walk with name exclusions misses a source the SDK still compiles because it sits in a directory
/// like <c>templates</c>, which the default glob does not exclude, while the compiler's own record of what it read cannot
/// miss it. Every project of this tree builds a portable PDB (the SDK default since .NET Core 3, unchanged by any project
/// file), so every assembly <see cref="NodeAssemblies"/> loads carries one beside its DLL.
/// </summary>
internal static class CompiledSources
{
    private static readonly Lazy<IReadOnlyList<string>> AllLazy = new(LoadAll);

    /// <summary>Every <c>*.cs</c> path, across every assembly of the tree, that a compiler actually read (deduplicated:
    /// several nodes can share one assembly), ordinally by path. What <see cref="DiagnosticsSyntax"/> reads in place of a
    /// directory walk for the facts that must see a source wherever the SDK compiles it.</summary>
    public static IReadOnlyList<string> All => AllLazy.Value;

    /// <summary>The <c>*.cs</c> paths a single assembly was compiled from, from its own portable PDB document table.</summary>
    public static IReadOnlyList<string> Of(Assembly assembly)
    {
        var pdbPath = Path.ChangeExtension(assembly.Location, ".pdb");
        if (assembly.Location.Length == 0 || !File.Exists(pdbPath))
        {
            throw new InvalidOperationException($"{assembly.GetName().Name} carries no portable PDB beside its DLL at {assembly.Location}; " +
                                                 "the diagnostics and shape levels read a node's sources from it");
        }

        using var stream = File.OpenRead(pdbPath);
        using var provider = MetadataReaderProvider.FromPortablePdbStream(stream);
        var reader = provider.GetMetadataReader();
        var paths = new List<string>();
        var root = EnsureTrailingSeparator(Path.GetFullPath(Tree.Root));
        foreach (var handle in reader.Documents)
        {
            var name = reader.GetString(reader.GetDocument(handle).Name);
            if (!name.EndsWith(".cs", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var full = Path.GetFullPath(name);
            if (full.StartsWith(root, StringComparison.OrdinalIgnoreCase) && !IsBuildOutput(full) && !IsPackageOwned(full, root))
            {
                paths.Add(full);
            }
        }

        return paths;
    }

    /// <summary>Whether a compiled source sits under a <c>bin</c> or <c>obj</c> directory: build output the tree never
    /// commits, the source generator's own <c>[GeneratedRegex]</c> implementation included. Every other directory a
    /// document names is read, the guards audit's F5 in full; only these two, already outside the repository, are not.</summary>
    private static bool IsBuildOutput(string path) =>
        path.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar).Any(segment => segment is "bin" or "obj");

    /// <summary>
    /// Whether a compiled source belongs to a restored NuGet package rather than to the tree: an ancestor directory of it,
    /// below the tree root, holds a <c>*.nupkg.metadata</c> file, the sentinel NuGet writes into every package folder of the
    /// global packages folder, wherever that folder is. CI keeps the cache inside the workspace (<c>NUGET_PACKAGES</c>), so a
    /// package's own source, such as the test SDK's <c>Program.cs</c>, is then compiled from a path under the root. No other
    /// path is skipped: a source in <c>templates</c>, <c>artifacts</c> or a dot-directory of the tree is still read (the
    /// guards audit's F5).
    /// </summary>
    /// <param name="path">The full path of a compiled source, under <paramref name="root"/>.</param>
    /// <param name="root">The tree root with a trailing separator; the upward search stops below it.</param>
    internal static bool IsPackageOwned(string path, string root)
    {
        var directory = Path.GetDirectoryName(path);
        while (directory is not null && EnsureTrailingSeparator(directory).Length > root.Length)
        {
            if (Directory.EnumerateFiles(directory, "*.nupkg.metadata").Any())
            {
                return true;
            }

            directory = Path.GetDirectoryName(directory);
        }

        return false;
    }

    private static string EnsureTrailingSeparator(string path) =>
        path.EndsWith(Path.DirectorySeparatorChar) ? path : path + Path.DirectorySeparatorChar;

    private static IReadOnlyList<string> LoadAll()
    {
        var paths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var assembly in NodeAssemblies.Assemblies.Values.Distinct())
        {
            paths.UnionWith(Of(assembly));
        }

        return [.. paths.OrderBy(path => path, StringComparer.Ordinal)];
    }
}

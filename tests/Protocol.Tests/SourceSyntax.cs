using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace APThermo.Protocol.Tests;

/// <summary>
/// The C# syntax trees of a node's own source files: every path <see cref="CompiledSources"/> attributes to the node's own
/// effective assembly (the guards audit's F5) that sits under the node's own directory and not under a descendant node's
/// (AGENTS.md §1 — a subtree that is itself a node belongs to that descendant, not to this one). Read from the compiled
/// source list rather than a directory walk with name exclusions, so a source the SDK compiles from a directory like
/// <c>templates</c> is measured like any other; the directory walk stays only for the files no compiler reads at all
/// (<see cref="DiagnosticsSyntax.BuildFiles"/>, <see cref="DiagnosticsSyntax.AnalyzerConfigFiles"/>).
/// Parsed at the language version the tree actually builds with (<c>Directory.Build.props</c> sets <c>LangVersion</c> to
/// <c>latest</c>, which the .NET 10 SDK resolves to C# 14, root <c>BOOT.md</c>, Dependencies), named explicitly rather than
/// as "latest" so that a future bump of the pinned Roslyn package cannot silently change what this node parses.
/// </summary>
internal static class SourceSyntax
{
    private static readonly CSharpParseOptions ParseOptions = new(LanguageVersion.CSharp14);

    /// <summary>Every source file of the node, path and parsed tree, ordered by path.</summary>
    public static IEnumerable<(string Path, SyntaxTree Tree)> Trees(Node node) =>
        Files(node).Select(path => (path, CSharpSyntaxTree.ParseText(File.ReadAllText(path), ParseOptions, path)));

    /// <summary>Every <c>*.cs</c> file the node owns (compiled into its own effective assembly, under its own directory,
    /// not under a descendant node's), ordinally by path.</summary>
    public static IEnumerable<string> Files(Node node)
    {
        var assembly = NodeAssemblies.AssemblyOf(node);
        if (assembly is null)
        {
            return [];
        }

        var directory = EnsureTrailingSeparator(Path.GetFullPath(node.Directory));
        var descendants = Tree.Nodes.Where(candidate => candidate != node && candidate.IsDescendantOf(node))
            .Select(candidate => EnsureTrailingSeparator(Path.GetFullPath(candidate.Directory)))
            .ToList();
        return CompiledSources.Of(assembly)
            .Where(path => path.StartsWith(directory, StringComparison.OrdinalIgnoreCase)
                           && !descendants.Any(descendant => path.StartsWith(descendant, StringComparison.OrdinalIgnoreCase)))
            .OrderBy(path => path, StringComparer.Ordinal);
    }

    private static string EnsureTrailingSeparator(string path) =>
        path.EndsWith(Path.DirectorySeparatorChar) ? path : path + Path.DirectorySeparatorChar;
}

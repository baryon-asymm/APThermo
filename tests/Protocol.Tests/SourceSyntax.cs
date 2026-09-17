using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace APThermo.Protocol.Tests;

/// <summary>
/// The C# syntax trees of a node's own source files: every <c>*.cs</c> file directly under the node's directory and in its
/// non-node subdirectories, the build directories <see cref="Tree"/> already skips excluded, and a subdirectory that is itself
/// a node (AGENTS.md §1) excluded too, because that subtree belongs to the descendant, not to this node. There is no separate
/// "generated files" filter beyond that: the tree's one source generator (the <c>[GeneratedRegex]</c> partial method) writes its
/// implementation under <c>obj/</c>, already a skipped directory, and no other generated file is committed outside it.
/// Parsed at the language version the tree actually builds with (<c>Directory.Build.props</c> sets <c>LangVersion</c> to
/// <c>latest</c>, which the .NET 10 SDK resolves to C# 14, root <c>BOOT.md</c>, Dependencies), named explicitly rather than as
/// "latest" so that a future bump of the pinned Roslyn package cannot silently change what this node parses.
/// </summary>
internal static class SourceSyntax
{
    private static readonly CSharpParseOptions ParseOptions = new(LanguageVersion.CSharp14);

    /// <summary>Every source file of the node, path and parsed tree, ordered by path.</summary>
    public static IEnumerable<(string Path, SyntaxTree Tree)> Trees(Node node) =>
        Files(node).Select(path => (path, CSharpSyntaxTree.ParseText(File.ReadAllText(path), ParseOptions, path)));

    /// <summary>Every <c>*.cs</c> file the node owns, ordinally by path.</summary>
    public static IEnumerable<string> Files(Node node)
    {
        var descendants = Tree.Nodes.Where(candidate => candidate != node && candidate.IsDescendantOf(node))
            .Select(candidate => Path.GetFullPath(candidate.Directory))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        return Walk(node.Directory, descendants).OrderBy(path => path, StringComparer.Ordinal);
    }

    private static IEnumerable<string> Walk(string directory, IReadOnlySet<string> descendants)
    {
        if (descendants.Contains(Path.GetFullPath(directory)))
        {
            yield break;
        }

        foreach (var file in Directory.GetFiles(directory, "*.cs"))
        {
            yield return file;
        }

        foreach (var child in Directory.GetDirectories(directory))
        {
            if (!Tree.Skipped.Contains(Path.GetFileName(child)))
            {
                foreach (var file in Walk(child, descendants))
                {
                    yield return file;
                }
            }
        }
    }
}

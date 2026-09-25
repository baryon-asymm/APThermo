using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace APThermo.Protocol.Tests;

/// <summary>
/// The whole-tree file walk the Diagnostics level reads (`tests/Protocol.Tests/BOOT.md`, "Diagnostics check"): every C#
/// source file, every MSBuild project/properties/targets file and every analyzer-configuration file from the tree root down,
/// the same directories <see cref="Tree.Skipped"/> already excludes from every other level. Unlike <see cref="SourceSyntax"/>,
/// which reads one node's own files, a suppression can hide in any file of the tree, this node's own included, so this walk is
/// never scoped to a single node.
/// </summary>
internal static class DiagnosticsSyntax
{
    private static readonly CSharpParseOptions ParseOptions = new(LanguageVersion.CSharp14);

    /// <summary>Every C# source file of the tree, path and parsed syntax tree, ordered by path.</summary>
    public static IEnumerable<(string Path, SyntaxTree Tree)> CSharpTrees() => Walk(Tree.Root, "*.cs")
        .OrderBy(path => path, StringComparer.Ordinal)
        .Select(path => (path, CSharpSyntaxTree.ParseText(File.ReadAllText(path), ParseOptions, path)));

    /// <summary>Every MSBuild project, properties and targets file of the tree, ordered by path.</summary>
    public static IEnumerable<string> BuildFiles() =>
        Walk(Tree.Root, "*.csproj", "*.props", "*.targets").OrderBy(path => path, StringComparer.Ordinal);

    /// <summary>Every analyzer-configuration file of the tree (<c>.editorconfig</c> and <c>*.globalconfig</c>), ordered by path.</summary>
    public static IEnumerable<string> AnalyzerConfigFiles() =>
        Walk(Tree.Root, ".editorconfig", "*.globalconfig").OrderBy(path => path, StringComparer.Ordinal);

    private static IEnumerable<string> Walk(string directory, params string[] patterns)
    {
        foreach (var pattern in patterns)
        {
            foreach (var file in Directory.GetFiles(directory, pattern))
            {
                yield return file;
            }
        }

        foreach (var child in Directory.GetDirectories(directory))
        {
            if (Tree.Skipped.Contains(Path.GetFileName(child)))
            {
                continue;
            }

            foreach (var file in Walk(child, patterns))
            {
                yield return file;
            }
        }
    }
}

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace APThermo.Protocol.Tests;

/// <summary>
/// The whole-tree file walk the Diagnostics level reads (`tests/Protocol.Tests/BOOT.md`, "Diagnostics check"): every C#
/// source file, every MSBuild project/properties/targets file and every analyzer-configuration file from the tree root down.
/// The C# sources come from <see cref="CompiledSources"/> (the guards audit's F5), every assembly's own portable PDB
/// document table, plus a narrow directory walk of `.github` for the one declared deviation the tree root names
/// (`.github/diagnostics/IsaProbe`, a C# project with no `BOOT.md`/`API.md` and so no assembly this node's reflection ever
/// loads, whose build files the Diagnostics constraint still covers). The build and analyzer-configuration files carry no
/// compiled record at all, so they stay a directory walk with <see cref="Tree.Skipped"/>'s exclusions, the same walk every
/// other level reads its own files with. Unlike <see cref="SourceSyntax"/>, which reads one node's own files, a suppression
/// can hide in any file of the tree, this node's own included, so this walk is never scoped to a single node.
/// </summary>
internal static class DiagnosticsSyntax
{
    private static readonly CSharpParseOptions ParseOptions = new(LanguageVersion.CSharp14);

    /// <summary>Every C# source file of the tree, path and parsed syntax tree, ordered by path: every assembly's own
    /// compiled sources (<see cref="CompiledSources.All"/>) plus `.github`'s own <c>*.cs</c> files, which compile into no
    /// assembly this node loads (the declared deviation of `.github/diagnostics/IsaProbe`).</summary>
    public static IEnumerable<(string Path, SyntaxTree Tree)> CSharpTrees() => CompiledSources.All
        .Concat(Walk(Path.Combine(Tree.Root, ".github"), "*.cs"))
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .OrderBy(path => path, StringComparer.Ordinal)
        .Select(path => (path, CSharpSyntaxTree.ParseText(File.ReadAllText(path), ParseOptions, path)));

    /// <summary>Every MSBuild project, properties and targets file of the tree, ordered by path.</summary>
    public static IEnumerable<string> BuildFiles() =>
        Walk(Tree.Root, "*.csproj", "*.props", "*.targets").OrderBy(path => path, StringComparer.Ordinal);

    /// <summary>Every analyzer-configuration file of the tree (<c>.editorconfig</c> and <c>*.globalconfig</c>), ordered by path.</summary>
    public static IEnumerable<string> AnalyzerConfigFiles() =>
        Walk(Tree.Root, ".editorconfig", "*.globalconfig").OrderBy(path => path, StringComparer.Ordinal);

    /// <summary>Every <c>Directory.Build.rsp</c> of the tree (MSBuild's own response-file channel, the guards audit's F4):
    /// mere existence is the violation the Diagnostics level refuses, wherever it sits.</summary>
    public static IEnumerable<string> ResponseFiles() =>
        Walk(Tree.Root, "Directory.Build.rsp").OrderBy(path => path, StringComparer.Ordinal);

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
            var name = Path.GetFileName(child);

            // A directory name starting with '.' is a tool's cache or state (.nuget-packages, .vs, .venv, .idea, .claude),
            // never part of the repository, except .github: it is committed, and CI builds the C# project under
            // .github/diagnostics/IsaProbe with the root's settings, so its build files stay checked (BOOT.md, "Diagnostics
            // check", the ⚠ of 2026-09-26).
            if (Tree.Skipped.Contains(name) || (name.StartsWith('.') && name is not ".github"))
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

using System.Xml.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace APThermo.Protocol.Tests;

/// <summary>
/// Diagnostics level: the root's Diagnostics constraint (`tests/Protocol.Tests/BOOT.md`, "Diagnostics check"). Every build
/// already fails on any diagnostic that is raised; this level reads the tree's text and syntax, not its assemblies, for a
/// diagnostic that is never raised because something suppressed it, plus (the guards audit's O6, 2026-09-28) the one IL
/// walk that reads assemblies here: whether a kernel-reached method throws, allocates or boxes. Eight facts, each failing
/// on an empty set.
/// </summary>
public sealed class DiagnosticsTests
{
    private static readonly string[] BelowWarningSeverities = ["none", "silent", "suggestion", "refactoring"];

    private static readonly string[] BannedRootOnlyProperties =
    [
        "TreatWarningsAsErrors", "WarningLevel", "Features", "AnalysisLevel", "EnforceCodeStyleInBuild",
        "GenerateDocumentationFile", "Nullable", "RunAnalyzers", "RunAnalyzersDuringBuild", "EnableNETAnalyzers",
        "CodeAnalysisTreatWarningsAsErrors", "MSBuildTreatWarningsAsErrors", "RunCodeAnalysis",
    ];

    /// <summary>The MSBuild item names that bring in an analyzer configuration or an analyzer assembly of the item's own
    /// choosing (the guards audit's F4): refused everywhere, root included, since none of the tree's own build files
    /// needs one.</summary>
    private static readonly string[] BannedItemNames = ["GlobalAnalyzerConfigFiles", "EditorConfigFiles", "Analyzer"];

    /// <summary>No C# file of the tree holds a <c>#pragma warning</c> directive or a <c>#nullable</c> directive that
    /// disables or restores a context, read from the syntax trees' directive trivia so that a string or a comment mentioning
    /// them does not count.</summary>
    [Fact]
    public void NoSourceFileSuppressesADiagnostic()
    {
        var trees = DiagnosticsSyntax.CSharpTrees().ToList();
        Assert.True(trees.Count > 0, "no C# file was found in the tree; this fact has nothing to check");
        var problems = trees.SelectMany(entry => DirectiveProblems(entry.Path, entry.Tree)).ToList();
        Assert.True(problems.Count == 0, string.Join("\n", problems));
    }

    private static IEnumerable<string> DirectiveProblems(string path, SyntaxTree tree)
    {
        foreach (var trivia in tree.GetRoot().DescendantTrivia(descendIntoTrivia: true))
        {
            var line = tree.GetLineSpan(trivia.Span).StartLinePosition.Line + 1;
            if (trivia.IsKind(SyntaxKind.PragmaWarningDirectiveTrivia))
            {
                yield return $"{Tree.Relative(path)}:{line}: a #pragma warning directive";
            }
            else if (trivia.GetStructure() is NullableDirectiveTriviaSyntax nullable
                     && nullable.SettingToken.Text is "disable" or "restore")
            {
                yield return $"{Tree.Relative(path)}:{line}: a #nullable {nullable.SettingToken.Text} directive";
            }
        }
    }

    /// <summary>No attribute named <c>SuppressMessage</c> or <c>UnconditionalSuppressMessage</c>, with or without the
    /// <c>Attribute</c> suffix, sits on any target of any C# file, <c>assembly:</c> and <c>module:</c> included; no file of
    /// the tree is named <c>GlobalSuppressions.cs</c>.</summary>
    [Fact]
    public void NoSourceFileCarriesASuppressionAttribute()
    {
        var trees = DiagnosticsSyntax.CSharpTrees().ToList();
        Assert.True(trees.Count > 0, "no C# file was found in the tree; this fact has nothing to check");
        var problems = trees.SelectMany(entry => AttributeProblems(entry.Path, entry.Tree)).ToList();
        foreach (var (path, _) in trees)
        {
            if (string.Equals(Path.GetFileName(path), "GlobalSuppressions.cs", StringComparison.OrdinalIgnoreCase))
            {
                problems.Add($"{Tree.Relative(path)}: a file named GlobalSuppressions.cs");
            }
        }

        Assert.True(problems.Count == 0, string.Join("\n", problems));
    }

    private static IEnumerable<string> AttributeProblems(string path, SyntaxTree tree)
    {
        foreach (var attribute in tree.GetRoot().DescendantNodes().OfType<AttributeSyntax>())
        {
            var simple = attribute.Name.ToString().Split('.')[^1];
            if (simple is "SuppressMessage" or "SuppressMessageAttribute" or "UnconditionalSuppressMessage"
                or "UnconditionalSuppressMessageAttribute")
            {
                var line = tree.GetLineSpan(attribute.Span).StartLinePosition.Line + 1;
                yield return $"{Tree.Relative(path)}:{line}: a [{simple}] attribute";
            }
        }
    }

    /// <summary>No <c>.csproj</c>, <c>.props</c> or <c>.targets</c> file of the tree sets <c>NoWarn</c> or
    /// <c>WarningsNotAsErrors</c>, except that the root <c>Directory.Build.targets</c> resets <c>NoWarn</c> to empty; none but
    /// the root <c>Directory.Build.props</c> sets a property this constraint reserves to it; no build file anywhere sets a
    /// <c>CodeAnalysisRuleSet</c> property or brings in a <c>GlobalAnalyzerConfigFiles</c>, <c>EditorConfigFiles</c> or
    /// <c>Analyzer</c> item; and no <c>Directory.Build.rsp</c> or <c>*.ruleset</c> file exists anywhere in the tree (the
    /// guards audit's F4; the third audit pass's finding 4b adds the rule-set forms, 2026-09-28).</summary>
    [Fact]
    public void NoBuildFileSuppressesOrOverridesADiagnostic()
    {
        var files = DiagnosticsSyntax.BuildFiles().ToList();
        Assert.True(files.Count > 0, "no MSBuild project, properties or targets file was found in the tree; this fact has nothing to check");
        var problems = files.SelectMany(BuildFileProblems).ToList();
        problems.AddRange(DiagnosticsSyntax.ResponseFiles().Select(path => $"{Tree.Relative(path)}: a Directory.Build.rsp exists"));
        problems.AddRange(DiagnosticsSyntax.RuleSetFiles().Select(path => $"{Tree.Relative(path)}: a *.ruleset file exists"));
        Assert.True(problems.Count == 0, string.Join("\n", problems));
    }

    private static IEnumerable<string> BuildFileProblems(string path)
    {
        var isRootProps = PathsEqual(path, Path.Combine(Tree.Root, "Directory.Build.props"));
        var isRootTargets = PathsEqual(path, Path.Combine(Tree.Root, "Directory.Build.targets"));
        var document = XDocument.Load(path);
        foreach (var element in document.Descendants())
        {
            var name = element.Name.LocalName;
            if (name == "NoWarn" && !(isRootTargets && element.Value.Trim().Length == 0))
            {
                yield return $"{Tree.Relative(path)}: sets NoWarn to '{element.Value}'";
            }
            else if (name == "WarningsNotAsErrors")
            {
                yield return $"{Tree.Relative(path)}: sets WarningsNotAsErrors";
            }
            else if (name == "CodeAnalysisRuleSet")
            {
                yield return $"{Tree.Relative(path)}: sets CodeAnalysisRuleSet to '{element.Value}'";
            }
            else if (BannedItemNames.Contains(name, StringComparer.Ordinal))
            {
                yield return $"{Tree.Relative(path)}: brings in a {name} item";
            }
            else if (!isRootProps && IsRootOnlyProperty(name))
            {
                yield return $"{Tree.Relative(path)}: sets {name}, reserved to the root Directory.Build.props";
            }
        }
    }

    private static bool IsRootOnlyProperty(string name) => BannedRootOnlyProperties.Contains(name, StringComparer.Ordinal)
        || name.StartsWith("AnalysisMode", StringComparison.Ordinal) || name.StartsWith("AnalysisLevel", StringComparison.Ordinal);

    private static bool PathsEqual(string left, string right) =>
        string.Equals(Path.GetFullPath(left), Path.GetFullPath(right), StringComparison.OrdinalIgnoreCase);

    /// <summary>Every <c>.editorconfig</c> and <c>.globalconfig</c> of the tree gives every key ending in <c>.severity</c> the
    /// value <c>warning</c> or <c>error</c>, and every option value carries no <c>:severity</c> suffix below warning.</summary>
    [Fact]
    public void NoAnalyzerConfigurationLowersASeverity()
    {
        var files = DiagnosticsSyntax.AnalyzerConfigFiles().ToList();
        Assert.True(files.Count > 0, "no .editorconfig or .globalconfig was found in the tree; this fact has nothing to check");
        var problems = files.SelectMany(SeverityProblems).ToList();
        Assert.True(problems.Count == 0, string.Join("\n", problems));
    }

    private static IEnumerable<string> SeverityProblems(string path)
    {
        var lines = File.ReadAllLines(path);
        for (var i = 0; i < lines.Length; i++)
        {
            var line = lines[i].Trim();
            var equals = line.IndexOf('=', StringComparison.Ordinal);
            if (line.Length == 0 || line[0] is '#' or ';' or '[' || equals < 0)
            {
                continue;
            }

            var key = line[..equals].Trim();
            var value = line[(equals + 1)..].Trim();
            if (key.EndsWith(".severity", StringComparison.OrdinalIgnoreCase)
                && value is not ("warning" or "error"))
            {
                yield return $"{Tree.Relative(path)}:{i + 1}: '{key}' is '{value}', not warning or error";
            }

            var colon = value.LastIndexOf(':');
            if (colon >= 0 && BelowWarningSeverities.Contains(value[(colon + 1)..].Trim(), StringComparer.OrdinalIgnoreCase))
            {
                yield return $"{Tree.Relative(path)}:{i + 1}: '{key}' carries the inline severity '{value[colon..]}', below warning";
            }
        }
    }

    /// <summary>
    /// No source file of the tree carries a generated-code marker, read the way Roslyn itself recognises one (root
    /// BOOT.md, the guards audit's F3): a file name ending <c>.g.cs</c>, <c>.g.i.cs</c>, <c>.generated.cs</c> or
    /// <c>.designer.cs</c>, or starting <c>TemporaryGeneratedFile_</c>, case-insensitively; any comment of the leading
    /// trivia, a block comment included, that contains <c>&lt;auto-generated</c> or <c>&lt;autogenerated</c>; a
    /// <c>[GeneratedCode]</c> attribute; a hand-written <c>[CompilerGenerated]</c> attribute; or a <c>generated_code</c>
    /// key in any <c>.editorconfig</c> or <c>.globalconfig</c>. Every one of these is a way to make an analyzer, and
    /// every check here that calls <see cref="TypeShape.IsCompilerGenerated"/>, skip a type or member the author wrote
    /// by hand.
    /// </summary>
    [Fact]
    public void NoSourceFileCarriesAGeneratedCodeMarker()
    {
        var trees = DiagnosticsSyntax.CSharpTrees().ToList();
        Assert.True(trees.Count > 0, "no C# file was found in the tree; this fact has nothing to check");
        var problems = trees.SelectMany(entry => GeneratedCodeProblems(entry.Path, entry.Tree)).ToList();
        var configFiles = DiagnosticsSyntax.AnalyzerConfigFiles().ToList();
        Assert.True(configFiles.Count > 0, "no .editorconfig or .globalconfig was found in the tree; this fact has nothing to check");
        problems.AddRange(configFiles.SelectMany(GeneratedCodeConfigurationProblems));
        Assert.True(problems.Count == 0, string.Join("\n", problems));
    }

    private static IEnumerable<string> GeneratedCodeProblems(string path, SyntaxTree tree)
    {
        if (HasGeneratedFileName(path))
        {
            yield return $"{Tree.Relative(path)}: a generated file name ({Path.GetFileName(path)})";
        }

        foreach (var trivia in tree.GetRoot().GetLeadingTrivia())
        {
            if (IsGeneratedMarkerComment(trivia))
            {
                var line = tree.GetLineSpan(trivia.Span).StartLinePosition.Line + 1;
                yield return $"{Tree.Relative(path)}:{line}: a comment naming <auto-generated or <autogenerated";
            }
        }

        foreach (var attribute in tree.GetRoot().DescendantNodes().OfType<AttributeSyntax>())
        {
            var simple = attribute.Name.ToString().Split('.')[^1];
            if (simple is "GeneratedCode" or "GeneratedCodeAttribute" or "CompilerGenerated" or "CompilerGeneratedAttribute")
            {
                var line = tree.GetLineSpan(attribute.Span).StartLinePosition.Line + 1;
                yield return $"{Tree.Relative(path)}:{line}: a [{simple}] attribute";
            }
        }
    }

    /// <summary>The file-name forms Roslyn treats as generated on their own, with no marker inside the file at all
    /// (the guards audit's F3).</summary>
    private static bool HasGeneratedFileName(string path)
    {
        var name = Path.GetFileName(path);
        return name.EndsWith(".g.cs", StringComparison.OrdinalIgnoreCase)
               || name.EndsWith(".g.i.cs", StringComparison.OrdinalIgnoreCase)
               || name.EndsWith(".generated.cs", StringComparison.OrdinalIgnoreCase)
               || name.EndsWith(".designer.cs", StringComparison.OrdinalIgnoreCase)
               || name.StartsWith("TemporaryGeneratedFile_", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Whether a piece of leading trivia is a comment (line or block) naming <c>&lt;auto-generated</c> or
    /// <c>&lt;autogenerated</c> anywhere in its text, on any line of the leading trivia, not only the first.</summary>
    private static bool IsGeneratedMarkerComment(SyntaxTrivia trivia)
    {
        if (!trivia.IsKind(SyntaxKind.SingleLineCommentTrivia) && !trivia.IsKind(SyntaxKind.MultiLineCommentTrivia))
        {
            return false;
        }

        var text = trivia.ToString();
        return text.Contains("<auto-generated", StringComparison.OrdinalIgnoreCase)
               || text.Contains("<autogenerated", StringComparison.OrdinalIgnoreCase);
    }

    private static IEnumerable<string> GeneratedCodeConfigurationProblems(string path)
    {
        var lines = File.ReadAllLines(path);
        for (var i = 0; i < lines.Length; i++)
        {
            var line = lines[i].Trim();
            var equals = line.IndexOf('=', StringComparison.Ordinal);
            if (line.Length == 0 || line[0] is '#' or ';' or '[' || equals < 0)
            {
                continue;
            }

            var key = line[..equals].Trim();
            if (string.Equals(key, "generated_code", StringComparison.OrdinalIgnoreCase))
            {
                yield return $"{Tree.Relative(path)}:{i + 1}: a generated_code key";
            }
        }
    }

    /// <summary>
    /// Every compiled C# source (<see cref="CompiledSources.All"/>, the guards audit's F5) lives in a directory that itself
    /// holds a <c>BOOT.md</c> and an <c>API.md</c>: a directory holding at least one code file is a source-code directory
    /// and must be a node (AGENTS.md §1). The linter's own directory walk cannot see a source hidden inside a directory
    /// name it treats as a build artifact (<c>templates</c>, <c>artifacts</c>, …); this fact reads the compiler's own
    /// record instead, so no such directory escapes the tree invariant.
    /// </summary>
    [Fact]
    public void EveryCompiledSourceLivesInANodeDirectory()
    {
        var sources = CompiledSources.All;
        Assert.True(sources.Count > 0, "no compiled C# source was found in the tree's assemblies; this fact has nothing to check");
        var problems = sources.Select(path => Path.GetDirectoryName(path)!).Distinct(StringComparer.OrdinalIgnoreCase)
            .Where(directory => !IsNodeDirectory(directory))
            .Select(directory => $"{Tree.Relative(directory)}: holds a compiled C# source but no BOOT.md and API.md (AGENTS.md §1)")
            .ToList();
        Assert.True(problems.Count == 0, string.Join("\n", problems));
    }

    private static bool IsNodeDirectory(string directory) =>
        File.Exists(Path.Combine(directory, "BOOT.md")) && File.Exists(Path.Combine(directory, "API.md"));

    /// <summary>
    /// No method reachable from a kernel entry point (the execution node's internal <c>Kernels</c> type) throws, allocates
    /// an array, allocates a reference type, or boxes a value (the guards audit's O6): ILGPU cannot compile any of the four
    /// into a kernel, so today they fail only at the reference machine's own PTX compile, invisible to the hosted suite.
    /// </summary>
    [Fact]
    public void KernelReachedMethodsAllocateNothingAndThrowNothing()
    {
        var entryPoints = KernelReachability.EntryPoints().ToList();
        Assert.True(entryPoints.Count > 0, "no kernel entry point was found in the tree's assemblies; this fact has nothing to check");
        var problems = KernelReachability.Problems().ToList();
        Assert.True(problems.Count == 0, string.Join("\n", problems));
    }

    /// <summary>The root <c>Directory.Build.props</c> sets <c>TreatWarningsAsErrors</c> true, <c>WarningLevel</c> 9999,
    /// <c>Features</c> strict, <c>AnalysisLevel</c> latest-all, <c>EnforceCodeStyleInBuild</c> true and
    /// <c>GenerateDocumentationFile</c> true; the root <c>Directory.Build.targets</c> sets <c>NoWarn</c> to empty.</summary>
    [Fact]
    public void TheRootBuildRunsAtTheMaximum()
    {
        var props = XDocument.Load(Path.Combine(Tree.Root, "Directory.Build.props"));
        AssertRootProperty(props, "TreatWarningsAsErrors", "true");
        AssertRootProperty(props, "WarningLevel", "9999");
        AssertRootProperty(props, "Features", "strict");
        AssertRootProperty(props, "AnalysisLevel", "latest-all");
        AssertRootProperty(props, "EnforceCodeStyleInBuild", "true");
        AssertRootProperty(props, "GenerateDocumentationFile", "true");

        var targets = XDocument.Load(Path.Combine(Tree.Root, "Directory.Build.targets"));
        var noWarn = targets.Descendants("NoWarn").ToList();
        Assert.True(noWarn.Count > 0, "the root Directory.Build.targets sets no NoWarn element; this fact has nothing to check");
        Assert.True(noWarn.All(element => element.Value.Trim().Length == 0), "the root Directory.Build.targets' NoWarn is not empty");
    }

    private static void AssertRootProperty(XDocument document, string name, string expected)
    {
        var elements = document.Descendants(name).ToList();
        Assert.True(elements.Count > 0, $"the root Directory.Build.props sets no {name} element; this fact has nothing to check");
        Assert.True(elements.All(element => element.Value.Trim() == expected), $"the root Directory.Build.props' {name} is not '{expected}'");
    }
}

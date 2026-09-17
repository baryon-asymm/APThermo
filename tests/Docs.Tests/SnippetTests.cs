using System.Text.RegularExpressions;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace APThermo.Docs.Tests;

/// <summary>
/// L1 (BOOT.md): every C# fence of README.md, docs/guide/*.md and the package READMEs under docs/nuget/ (info
/// `csharp`, `cs` or `c#`, any case, the first word of the info string) must be preceded by a
/// `&lt;!-- snippet: name --&gt;` marker (`Every_csharp_fence_is_preceded_by_a_snippet_marker`), and a marked block
/// must equal that region of the samples node's source, byte for byte after the common indentation is stripped and
/// line endings normalized to LF (`Every_marked_csharp_block_equals_its_sample_region`; the samples' own invariant:
/// a region may be quoted by several pages, never edited). A third fact ties the region names themselves to the
/// samples' scenarios (`Region_names_are_exactly_each_scenario_class_name_or_that_name_plus_Usings`), and a fourth
/// proves each scenario's body region sits inside that scenario's own `Run` method rather than a method no scenario
/// calls (`Each_scenario_body_region_lies_inside_its_class_Run_method`; this task's own ma2, closing the escape
/// its finding X7 named).
/// Each fails when its own population is empty (AGENTS.md §13: a check that can pass on an empty set is
/// indistinguishable from an absent one).
/// </summary>
public sealed class SnippetTests
{
    private static readonly Regex Marker = new(@"^<!--\s*snippet:\s*(\w+)\s*-->$", RegexOptions.Compiled);

    [Fact]
    public void Every_csharp_fence_is_preceded_by_a_snippet_marker()
    {
        var fences = new List<(string File, int Line)>();
        foreach (var file in GuideDocuments.SnippetSources())
        {
            var lines = GuideDocuments.Lines(file);
            foreach (var block in GuideDocuments.FencedBlocks(lines, file))
            {
                if (GuideDocuments.IsCSharpFenceInfo(block.Info))
                {
                    fences.Add((file, block.StartLine));
                }
            }
        }

        Assert.True(fences.Count > 0, "no C# fence (csharp/cs/c#) was found in README.md, docs/guide/*.md or docs/nuget/*.md");

        foreach (var (file, line) in fences)
        {
            CheckPrecededByMarker(file, line);
        }
    }

    private static void CheckPrecededByMarker(string file, int fenceLine)
    {
        var lines = GuideDocuments.Lines(file);
        var i = fenceLine - 1;
        while (i >= 0 && string.IsNullOrWhiteSpace(lines[i]))
        {
            i--;
        }

        Assert.True(
            i >= 0 && Marker.IsMatch(lines[i].Trim()),
            $"{file}:{fenceLine + 1}: this C# fence is not preceded by a '<!-- snippet: name -->' marker");
    }

    [Fact]
    public void Every_marked_csharp_block_equals_its_sample_region()
    {
        var markers = new List<(string File, int Line, string Name)>();
        foreach (var file in GuideDocuments.SnippetSources())
        {
            var lines = GuideDocuments.Lines(file);
            for (var i = 0; i < lines.Length; i++)
            {
                var match = Marker.Match(lines[i].Trim());
                if (match.Success)
                {
                    markers.Add((file, i, match.Groups[1].Value));
                }
            }
        }

        Assert.True(markers.Count > 0, "no '<!-- snippet: name -->' marker was found in README.md, docs/guide/*.md or docs/nuget/*.md");

        var regions = GuideDocuments.SnippetRegions();
        foreach (var (file, line, name) in markers)
        {
            CheckMarker(file, line, name, regions);
        }
    }

    private static void CheckMarker(string file, int line, string name, IReadOnlyDictionary<string, string> regions)
    {
        var where = $"{file}:{line + 1}";
        var lines = GuideDocuments.Lines(file);

        var j = line + 1;
        while (j < lines.Length && string.IsNullOrWhiteSpace(lines[j]))
        {
            j++;
        }

        Assert.True(j < lines.Length, $"{where}: the snippet marker is not followed by a fenced code block");
        Assert.True(GuideDocuments.TryFencedBlockAt(lines, j, file, out var block), $"{where}: the snippet marker is not followed by a fenced code block");

        Assert.True(
            GuideDocuments.IsCSharpFenceInfo(block.Info),
            $"{where}: the snippet must be a C# block (csharp/cs/c#), found a ```{block.Info} block");

        Assert.True(
            regions.TryGetValue(name, out var region),
            $"{where}: no snippet region named '{name}' exists under samples/Samples/; known regions: {string.Join(", ", regions.Keys.Order(StringComparer.Ordinal))}");

        var quoted = GuideDocuments.Lf(string.Join("\n", block.Body));
        var expected = GuideDocuments.Lf(region!);
        Assert.True(
            expected.Equals(quoted, StringComparison.Ordinal),
            $"{where}: the '{name}' block differs from its sample region\n--- guide ---\n{quoted}\n--- sample ---\n{expected}");
    }

    /// <summary>
    /// Its finding m10, fixed in `657410d`: the set of region names `GuideDocuments.SnippetRegions()` finds under
    /// samples/Samples/ is exactly, for each scenario the samples node's tree contract lists
    /// (`APThermo.Samples.Program.Scenarios`, `ClassNameOf`), its class name and that name plus `Usings` — the two
    /// regions `samples/Samples/API.md` (Scenarios) declares every scenario carries. A region under neither name
    /// (a leftover, or one inside a method no scenario's `Run` calls, X7) fails as unexpected; a scenario missing
    /// either of its two regions fails as missing. This does not require a region to be quoted anywhere in the
    /// guide — L1's other two facts above prove a quoted one is faithful; this one proves the region set itself has
    /// no extra or missing member.
    /// </summary>
    [Fact]
    public void Region_names_are_exactly_each_scenario_class_name_or_that_name_plus_Usings()
    {
        var scenarios = APThermo.Samples.Program.Scenarios;
        Assert.True(scenarios.Count > 0, "no scenario was found in APThermo.Samples.Program.Scenarios");

        var expected = new SortedSet<string>(StringComparer.Ordinal);
        foreach (var scenario in scenarios)
        {
            var className = APThermo.Samples.Program.ClassNameOf(scenario);
            expected.Add(className);
            expected.Add(className + "Usings");
        }

        var actual = new SortedSet<string>(GuideDocuments.SnippetRegions().Keys, StringComparer.Ordinal);

        var missing = expected.Except(actual).ToList();
        var unexpected = actual.Except(expected).ToList();
        Assert.True(missing.Count == 0, $"samples/Samples/: missing region(s) a scenario declares: {string.Join(", ", missing)}");
        Assert.True(unexpected.Count == 0, $"samples/Samples/: region(s) named by no scenario's class name (or that name plus Usings): {string.Join(", ", unexpected)}");
    }

    /// <summary>
    /// ma2: the previous fact proves the region *set* has no extra or missing member, but not where a region
    /// physically sits — a `&lt;ClassName&gt;` region moved into a helper method the class declares but its own
    /// `Run` never calls would still pass that fact (finding X7). This one parses the scenario's own source file
    /// with Roslyn and asserts the region's line span lies inside the block body of `ClassName.Run` itself, so the
    /// guide can only ever quote what actually executes when the scenario runs.
    /// </summary>
    [Fact]
    public void Each_scenario_body_region_lies_inside_its_class_Run_method()
    {
        var scenarios = APThermo.Samples.Program.Scenarios;
        Assert.True(scenarios.Count > 0, "no scenario was found in APThermo.Samples.Program.Scenarios");

        var locations = GuideDocuments.SnippetRegionLocations();
        foreach (var scenario in scenarios)
        {
            var className = APThermo.Samples.Program.ClassNameOf(scenario);
            Assert.True(
                locations.TryGetValue(className, out var region),
                $"samples/Samples/: no snippet region named '{className}' exists");
            CheckRegionInsideRun(className, region);
        }
    }

    private static void CheckRegionInsideRun(string className, (string File, int BodyStartLine, int BodyEndLine) region)
    {
        var tree = CSharpSyntaxTree.ParseText(File.ReadAllText(region.File), path: region.File);
        var classNode = tree.GetCompilationUnitRoot().DescendantNodes().OfType<ClassDeclarationSyntax>()
            .FirstOrDefault(c => c.Identifier.Text == className);
        Assert.True(classNode is not null, $"{region.File}: no class '{className}' was found");

        var runMethod = classNode!.Members.OfType<MethodDeclarationSyntax>().FirstOrDefault(m => m.Identifier.Text == "Run");
        Assert.True(runMethod is not null, $"{region.File}: class '{className}' declares no 'Run' method");
        Assert.True(runMethod!.Body is not null, $"{region.File}: '{className}.Run' has no block body");

        var lineSpan = tree.GetLineSpan(runMethod.Body!.Span);
        var bodyStart = lineSpan.StartLinePosition.Line;
        var bodyEnd = lineSpan.EndLinePosition.Line;

        Assert.True(
            region.BodyStartLine >= bodyStart && region.BodyEndLine <= bodyEnd,
            $"{region.File}: the '{className}' snippet region (0-based lines {region.BodyStartLine}-{region.BodyEndLine}) "
                + $"does not lie inside '{className}.Run' (0-based body lines {bodyStart}-{bodyEnd})");
    }
}

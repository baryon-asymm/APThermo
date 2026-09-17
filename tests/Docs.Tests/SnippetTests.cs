using System.Text.RegularExpressions;

namespace APThermo.Docs.Tests;

/// <summary>
/// L1 (BOOT.md): every C# fence of README.md, docs/guide/*.md and the package READMEs under docs/nuget/ (info
/// `csharp`, `cs` or `c#`, any case) must be preceded by a `&lt;!-- snippet: name --&gt;` marker
/// (`Every_csharp_fence_is_preceded_by_a_snippet_marker`), and a marked block must equal that region of the samples
/// node's source, byte for byte after the common indentation is stripped and line endings normalized to LF
/// (`Every_marked_csharp_block_equals_its_sample_region`; the samples' own invariant: a region may be quoted by
/// several pages, never edited). A third fact ties the region names themselves to the samples' scenarios
/// (`Region_names_are_exactly_each_scenario_class_name_or_that_name_plus_Usings`), so a region in a method no
/// scenario runs cannot pass unnoticed (X7, `SCRATCH/audit/review-docs-2.md`). Each fails when its own population is
/// empty (AGENTS.md §13: a check that can pass on an empty set is indistinguishable from an absent one).
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
            foreach (var block in GuideDocuments.FencedBlocks(lines))
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
        Assert.True(GuideDocuments.TryFencedBlockAt(lines, j, out var block), $"{where}: the snippet marker is not followed by a fenced code block");

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
    /// m10 (`SCRATCH/audit/review-docs-2.md`): the set of region names `GuideDocuments.SnippetRegions()` finds under
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
}

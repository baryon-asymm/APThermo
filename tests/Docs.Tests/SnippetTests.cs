using System.Text.RegularExpressions;

namespace APThermo.Docs.Tests;

/// <summary>
/// L1 (BOOT.md): every C# block of README.md, docs/guide/*.md and the package READMEs under docs/nuget/ that is
/// preceded by a line `&lt;!-- snippet: name --&gt;` equals that region of the samples node's source, byte for byte
/// after the common indentation is stripped and line endings normalized to LF (the samples' own invariant: a region
/// may be quoted by several pages, never edited). Fails when no marker exists anywhere in the guide (AGENTS.md §13:
/// a check that can pass on an empty set is indistinguishable from an absent one).
/// </summary>
public sealed class SnippetTests
{
    private static readonly Regex Marker = new(@"^<!--\s*snippet:\s*(\w+)\s*-->$", RegexOptions.Compiled);

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

        var block = GuideDocuments.FencedBlocks(lines).FirstOrDefault(b => b.StartLine == j);
        Assert.True(
            block.Info.Equals("csharp", StringComparison.OrdinalIgnoreCase),
            $"{where}: the snippet must be a ```csharp block, found a ```{block.Info} block");

        Assert.True(
            regions.TryGetValue(name, out var region),
            $"{where}: no snippet region named '{name}' exists under samples/Samples/; known regions: {string.Join(", ", regions.Keys.Order(StringComparer.Ordinal))}");

        var quoted = GuideDocuments.Lf(string.Join("\n", block.Body));
        var expected = GuideDocuments.Lf(region!);
        Assert.True(
            expected.Equals(quoted, StringComparison.Ordinal),
            $"{where}: the '{name}' block differs from its sample region\n--- guide ---\n{quoted}\n--- sample ---\n{expected}");
    }
}

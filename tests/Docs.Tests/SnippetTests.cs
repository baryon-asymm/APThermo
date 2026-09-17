using System.Text.RegularExpressions;
using APThermo.Fixtures;

namespace APThermo.Docs.Tests;

/// <summary>
/// L1 (BOOT.md): every C# block of the guide is preceded by a line `<!-- snippet: <name> -->` and equals that
/// sample's snippet region byte for byte (line endings normalized to LF); every scenario name is quoted exactly
/// once. The region in the sample source runs from its `// <!-- snippet: <name> -->` marker to the end of the file;
/// one class per file keeps that exact. Passes vacuously while no document carries a snippet marker.
/// </summary>
public sealed class SnippetTests
{
    private static readonly Regex Marker = new(@"^<!--\s*snippet:\s*(\w+)\s*-->$", RegexOptions.Compiled);

    [Fact]
    public void Every_csharp_block_equals_its_sample_region()
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

        if (markers.Count == 0)
        {
            return; // no document carries a snippet yet: nothing to check
        }

        var expected = ScenarioClasses().Order(StringComparer.Ordinal).ToList();
        var actual = markers.Select(m => m.Name).Order(StringComparer.Ordinal).ToList();
        Assert.True(
            expected.SequenceEqual(actual),
            "every scenario name must be quoted exactly once; the guide quotes: " + string.Join(", ", markers.Select(m => m.Name)));

        foreach (var (file, line, name) in markers)
        {
            var where = $"{file}:{line + 1}";
            var className = GuideDocuments.ScenarioClass(name);
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

            var regionPath = RepositoryPaths.Resolve("samples", "Samples", className + ".cs");
            var srcLines = GuideDocuments.Lines(regionPath);
            var start = Array.FindIndex(srcLines, l => l.Contains($"<!-- snippet: {name} -->"));
            Assert.True(start >= 0, $"{where}: no '// <!-- snippet: {name} -->' marker in {regionPath}");

            var region = string.Join("\n", srcLines.Skip(start + 1));
            var quoted = string.Join("\n", block.Body);
            Assert.True(
                region.Equals(quoted, StringComparison.Ordinal),
                $"{where}: the {name} block differs from its sample region\n--- guide ---\n{quoted}\n--- sample ({regionPath}) ---\n{region}");
        }
    }

    /// <summary>The scenario classes, in the order of <c>APThermo.Samples.Program.Scenarios</c>.</summary>
    private static IReadOnlyList<string> ScenarioClasses() =>
        APThermo.Samples.Program.Scenarios.Select(GuideDocuments.ScenarioClass).ToList();
}

using System.Text.RegularExpressions;

namespace APThermo.Docs.Tests;

/// <summary>
/// L6 (BOOT.md): every page of docs/guide/ carries the shared headings in order, once each: `## Purpose`,
/// `## When to use`, `## Steps`, `## Errors`, `## See also`. Passes vacuously while no guide page exists.
/// </summary>
public sealed class GuideShapeTests
{
    private static readonly string[] Required = ["## Purpose", "## When to use", "## Steps", "## Errors", "## See also"];

    [Fact]
    public void Every_guide_page_has_the_shared_shape()
    {
        foreach (var path in GuideDocuments.GuidePages())
        {
            CheckPage(path);
        }
    }

    private static void CheckPage(string path)
    {
        var headings = GuideDocuments.Lines(path)
            .Where(line => Regex.IsMatch(line, @"^ {0,3}##(?!#)\s+\S"))
            .Select(line => line.Trim())
            .ToList();

        foreach (var required in Required)
        {
            Assert.True(headings.Count(h => h == required) == 1, $"{path}: the heading '{required}' must appear exactly once");
        }

        var positions = Required.Select(h => headings.IndexOf(h)).ToList();
        for (var i = 1; i < positions.Count; i++)
        {
            Assert.True(
                positions[i - 1] < positions[i],
                $"{path}: the shared headings are out of order: '{Required[i - 1]}' must come before '{Required[i]}'");
        }
    }
}

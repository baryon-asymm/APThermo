using System.Text.RegularExpressions;

namespace APThermo.Docs.Tests;

/// <summary>
/// L6 (BOOT.md): every page of docs/guide/ carries the shared headings in order, once each: `## Purpose`,
/// `## When to use`, `## Steps`, `## Errors`, `## See also`. Headings are read outside fenced code blocks only, so
/// a `##`-looking line quoted inside an example does not count. Fails when no guide page exists.
/// </summary>
public sealed partial class GuideShapeTests
{
    private static readonly string[] Required = ["## Purpose", "## When to use", "## Steps", "## Errors", "## See also"];

    /// <summary>Every guide page has the shared shape.</summary>
    [Fact]
    public void EveryGuidePageHasTheSharedShape()
    {
        var pages = GuideDocuments.GuidePages();
        Assert.True(pages.Count > 0, "no guide page was found under docs/guide/");
        foreach (var path in pages)
        {
            CheckPage(path);
        }
    }

    private static void CheckPage(string path)
    {
        var headings = GuideDocuments.OutsideFences(GuideDocuments.Lines(path))
            .Where(line => MyRegex().IsMatch(line))
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

    [GeneratedRegex(@"^ {0,3}##(?!#)\s+\S")]
    private static partial Regex MyRegex();
}

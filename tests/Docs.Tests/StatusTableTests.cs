using System.Text.RegularExpressions;
using APThermo.Fixtures;
using APThermo.Thermo;

namespace APThermo.Docs.Tests;

/// <summary>
/// M4 (`SCRATCH/audit/review-docs-2.md`): the per-case status table of docs/guide/troubleshooting.md — the one
/// headed `| Status | Meaning |` — lists exactly the names of `CaseStatus` (`Thermo`'s API.md), in the library's own
/// declaration order, one row per name. A status the table forgets, misspells or orders differently, or a row for a
/// name `CaseStatus` does not declare, fails naming the mismatch; the population itself (the table must be found at
/// all) is asserted first, so a table heading rename cannot turn this into a vacuous pass (AGENTS.md §13).
/// </summary>
public sealed class StatusTableTests
{
    private static readonly Regex Row = new(@"^\|\s*`([A-Za-z]+)`\s*\|", RegexOptions.Compiled);

    [Fact]
    public void The_status_table_lists_exactly_the_names_of_CaseStatus()
    {
        var path = RepositoryPaths.Resolve("docs", "guide", "troubleshooting.md");
        Assert.True(File.Exists(path), "docs/guide/troubleshooting.md does not exist");

        var rows = StatusRowsOf(GuideDocuments.Lines(path));
        Assert.True(rows.Count > 0, $"{path}: no '| Status | Meaning |' table was found");

        var expected = Enum.GetNames<CaseStatus>();
        Assert.True(
            rows.SequenceEqual(expected, StringComparer.Ordinal),
            $"{path}: the status table lists [{string.Join(", ", rows)}], CaseStatus declares [{string.Join(", ", expected)}]");
    }

    /// <summary>The first column of the markdown table headed `| Status | Meaning |`, in row order, backticks stripped.</summary>
    private static List<string> StatusRowsOf(string[] lines)
    {
        var headingIndex = Array.FindIndex(lines, l => l.Trim() == "| Status | Meaning |");
        if (headingIndex < 0 || headingIndex + 1 >= lines.Length)
        {
            return [];
        }

        var rows = new List<string>();
        for (var i = headingIndex + 2; i < lines.Length; i++)
        {
            var match = Row.Match(lines[i]);
            if (!match.Success)
            {
                break;
            }

            rows.Add(match.Groups[1].Value);
        }

        return rows;
    }
}

using System.Text.RegularExpressions;
using APThermo.Fixtures;

namespace APThermo.Docs.Tests;

/// <summary>
/// ma1: `samples/Samples/API.md`'s "## Scenarios" table (`| Scenario name | Class | Answers | Prints | Approved
/// output |`) is read by `.github/workflows/ci.yml`'s "Samples run against the packaged library" step instead of a
/// second, typed-in scenario list, and by a human deciding what a scenario answers. Nothing before this task proved
/// the table's own first two columns — the scenario name and its class — still matched
/// `APThermo.Samples.Program.Scenarios` and `ClassNameOf`, in order: a scenario renamed, reordered or added in code
/// without the table following would go unnoticed here and read as zero rows in CI (a vacuous pass, ma1). This fact
/// parses the table with the same column split the workflow's `awk` step uses and compares both columns, in order,
/// against the code. Fails when no row is found.
/// </summary>
public sealed class ScenarioTableTests
{
    private static readonly Regex Row = new(@"^\|\s*`([^`]+)`\s*\|\s*`([^`]+)`\s*\|", RegexOptions.Compiled);

    [Fact]
    public void The_scenario_table_of_the_API_matches_Program_Scenarios_and_ClassNameOf()
    {
        var path = RepositoryPaths.Resolve("samples", "Samples", "API.md");
        Assert.True(File.Exists(path), "samples/Samples/API.md does not exist");

        var rows = ScenarioRowsOf(GuideDocuments.Lines(path));
        Assert.True(rows.Count > 0, $"{path}: no '| Scenario name | Class | ... |' table row was found");

        var expected = APThermo.Samples.Program.Scenarios
            .Select(scenario => (Scenario: scenario, Class: APThermo.Samples.Program.ClassNameOf(scenario)))
            .ToList();

        Assert.True(
            rows.SequenceEqual(expected),
            $"{path}: the scenario table lists [{Describe(rows)}], the code declares [{Describe(expected)}]");
    }

    private static string Describe(IReadOnlyList<(string Scenario, string Class)> rows) =>
        string.Join(", ", rows.Select(r => $"{r.Scenario}/{r.Class}"));

    /// <summary>The first two columns (scenario name, class) of every data row under the "## Scenarios" table's heading, in row order, backticks stripped.</summary>
    private static List<(string Scenario, string Class)> ScenarioRowsOf(string[] lines)
    {
        var headingIndex = Array.FindIndex(lines, l => l.Trim() == "| Scenario name | Class | Answers | Prints | Approved output |");
        if (headingIndex < 0 || headingIndex + 1 >= lines.Length)
        {
            return [];
        }

        var rows = new List<(string, string)>();
        for (var i = headingIndex + 2; i < lines.Length; i++)
        {
            var match = Row.Match(lines[i]);
            if (!match.Success)
            {
                break;
            }

            rows.Add((match.Groups[1].Value, match.Groups[2].Value));
        }

        return rows;
    }
}

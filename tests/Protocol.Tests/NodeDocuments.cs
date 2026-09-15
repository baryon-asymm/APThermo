using System.Text.RegularExpressions;

namespace APThermo.Protocol.Tests;

/// <summary>
/// What a node's own <c>BOOT.md</c> declares: the links of its <c>## Dependencies</c> section, and the rows of its
/// <c>## Shape exceptions</c> table (2026-09-14).
/// </summary>
internal static class NodeDocuments
{
    /// <summary>The nodes a BOOT.md declares in its `## Dependencies` section: every link resolving to a node's API.md, and the
    /// links that resolve to none.</summary>
    public static (IReadOnlySet<Node> Nodes, IReadOnlyList<string> Unresolved) DeclaredDependencies(Node node)
    {
        var boot = File.ReadAllText(node.Boot).ReplaceLineEndings("\n");
        var section = Regex.Match(boot, @"^## Dependencies\s*$(.*?)(?=^## |\z)", RegexOptions.Multiline | RegexOptions.Singleline);
        var declared = new HashSet<Node>();
        var unresolved = new List<string>();
        if (!section.Success)
        {
            return (declared, unresolved);
        }

        var byDirectory = Tree.Nodes.ToDictionary(n => Path.GetFullPath(n.Directory), n => n, StringComparer.OrdinalIgnoreCase);
        foreach (Match link in Regex.Matches(section.Groups[1].Value, @"\]\(([^)\s]+)\)"))
        {
            var target = link.Groups[1].Value;
            if (!target.EndsWith("API.md", StringComparison.Ordinal))
            {
                continue;
            }

            var directory = Path.GetFullPath(Path.Combine(node.Directory, Path.GetDirectoryName(target) ?? string.Empty));
            if (byDirectory.TryGetValue(directory, out var found))
            {
                declared.Add(found);
            }
            else
            {
                unresolved.Add(target);
            }
        }

        return (declared, unresolved);
    }

    /// <summary>The rows of a node's own `## Shape exceptions` table, in the form `tests/Protocol.Tests/BOOT.md` defines
    /// ("Shape check"): `Where` with its wrapping backticks stripped, `Rule` and `Reason` as written, `Measured` as an
    /// integer. Empty when the node's `BOOT.md` has no such section.</summary>
    public static IReadOnlyList<ShapeException> ShapeExceptions(Node node)
    {
        var boot = File.ReadAllText(node.Boot).ReplaceLineEndings("\n");
        var section = Regex.Match(boot, @"^## Shape exceptions\s*$(.*?)(?=^## |\z)", RegexOptions.Multiline | RegexOptions.Singleline);
        if (!section.Success)
        {
            return [];
        }

        var rows = new List<ShapeException>();
        var pattern = @"^\|\s*(?<where>[^|\r\n]+?)\s*\|\s*(?<rule>[^|\r\n]+?)\s*\|\s*(?<measured>\d+)\s*\|\s*(?<reason>[^|\r\n]+?)\s*\|\s*$";
        foreach (Match row in Regex.Matches(section.Groups[1].Value, pattern, RegexOptions.Multiline))
        {
            rows.Add(new ShapeException(StripBackticks(row.Groups["where"].Value), row.Groups["rule"].Value,
                int.Parse(row.Groups["measured"].Value), row.Groups["reason"].Value));
        }

        return rows;
    }

    private static string StripBackticks(string cell) => cell.Length >= 2 && cell[0] == '`' && cell[^1] == '`' ? cell[1..^1] : cell;
}

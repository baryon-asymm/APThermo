using System.Text.RegularExpressions;

namespace AerospacePropellantThermodynamics.Protocol.Tests;

/// <summary>
/// What a node's own <c>BOOT.md</c> declares: the links of its <c>## Dependencies</c> section. (The rows of a
/// <c>## Shape exceptions</c> table are a later phase's concern; this node reads only the dependency links.)
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
}
